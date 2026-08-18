# GenericMCPServerBackend

GenericMCPServerBackend is the backend for GenericMCPServer. It is a MCP-server which is reachable over HTTP.

It brings **no tool of its own**. Which tools it offers is decided entirely by an administrator, who declares them in a
tool-definitions-file. Without such a file the server starts and offers nothing.

## How it is built

The MCP-protocol itself is not implemented in this codeunit. It is implemented by the MCP-SDK of Microsoft (the
packages `ModelContextProtocol`, `ModelContextProtocol.AspNetCore` and `ModelContextProtocol.Core`), which is
registered as a service and whose endpoint is mapped in `Program.cs`. Because the tools are not known at
compile-time, they can not be methods which carry an attribute: one `ScriptTool` is created per declaration and handed
to the SDK.

```csharp
BusinessLogicService businessLogicService = new BusinessLogicService(toolDefinitionsFile);
services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools(businessLogicService.GetToolDefinitions().Select(definition => new ScriptTool(businessLogicService, definition)));
...
webApplication.MapMcp("/API/v1/MCP");
```

The transport runs in the stateless mode, so no session-id has to be kept by the client.

## Endpoint

All messages of the protocol are exchanged over one single endpoint:

```
POST /API/v1/MCP
```

## How a tool is declared

The server is pointed to the file with the commandline-parameter `--InitialToolDefinitionsFile` (in the image: the
environment-variable `InitialToolDefinitionsFile`). The file declares every tool:

```json
{
    "tools": [
        {
            "tool-name": "get_cve_details",
            "calls": "GetCVEDetails.py",
            "description": "Returns the details of a CVE from the national vulnerability database.",
            "parameters": [ "cve-identifier" ]
        }
    ]
}
```

| Field | Meaning |
| ----- | ------- |
| `tool-name` | The name under which the tool is offered. This is what the client and the model behind it see. |
| `calls` | The script which implements the tool. A relative path is resolved against the folder of this file, and the script has to stay inside that folder. |
| `description` | What the tool does. The model reads this to decide whether the tool fits a task, so it should describe the answer and not the implementation. |
| `parameters` | The names of the parameters. Each of them is passed to the script as `--<name>=<value>`, so a name here has to be the name of an option the script accepts. |

Every parameter is offered as a required string. What a value actually has to look like is checked by the script
itself, which keeps that knowledge next to the code which depends on it.

A mistake in the file (a tool without a name, a tool without a script, a name which is used twice, invalid json) makes
the server fail on startup instead of when somebody happens to call the affected tool. A file which is configured but
does not exist is an error as well - only *no* configured file means "no tools".

## Security

The values of the parameters are provided by the client and are therefore untrusted. Two mechanisms handle that:

**A value can not become a command.** No shell is involved when a script is started: `UseShellExecute` is false and
every argument is added as its own entry of `ProcessStartInfo.ArgumentList`, so the operating-system hands each of
them to the script as exactly one argument. This is stronger than escaping would be, because there is no quoting which
could be escaped wrongly, forgotten or applied twice. Every value is additionally passed as one single token in the
form `--name=value`, so a value which looks like an option (for example `--help`) can not be read as one.

**A value has to have the format the script expects.** Each script checks each of its arguments against a regular
expression which is anchored at both ends and rejects everything which does not match completely. A value which is
rejected makes the script end with an exit-code which is not 0; the server turns that into a failed tool-call whose
message contains what the script wrote, so the model learns what was wrong and can correct itself.

The declaration itself comes from an administrator and is therefore trusted, but the script it names still has to be
located inside the folder of the tool-definitions-file, so that a mistake there can not start an unrelated program.

## Example

`GenericMCPServer/Other/Examples/MinimalDockerComposeFile/Tools` contains five scripts and the declaration which turns them
into the tools `get_cve_details`, `get_maven_versions`, `get_nuget_versions`, `get_pypi_versions` and
`get_npm_versions`. None of them is known to this codeunit - they are an example of what can be declared.

## How to use it in a MCP-client

```json
{
  "mcpServers": {
    "genericmcpserver": {
      "type": "http",
      "url": "http://127.0.0.1:8080/API/v1/MCP"
    }
  }
}
```

With the Claude-CLI:

```
claude mcp add --transport http --scope user genericmcpserver http://127.0.0.1:8080/API/v1/MCP
```

## How to test it without a client

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

Every script can also be called on its own, which is the fastest way to check what a tool answers:

```
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
```

## How to add a tool

Put a script into the folder of the tool-definitions-file, let it parse its arguments with `argparse`, give every
argument a `type`-function which checks it against the format it must have, and add an entry to the file. Nothing in
this codeunit has to be changed and nothing has to be recompiled - the server picks the tool up when it is restarted.
