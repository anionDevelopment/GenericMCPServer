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
BusinessLogicService businessLogicService = new BusinessLogicService(toolDefinitionsFile, virtualEnvironmentsFolder);
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
            "type": "python",
            "calls": "GetCVEDetails.py",
            "description": "Returns the details of a CVE from the national vulnerability database.",
            "parameters": [ "cve-identifier" ]
        }
    ]
}
```

These fields belong to every tool:

| Field | Meaning |
| ----- | ------- |
| `tool-name` | The name under which the tool is offered. This is what the client and the model behind it see. It may consist of letters, digits, underscores and hyphens. |
| `type` | What implements the tool: `python`, `shell` or `web-request`. It has to be declared, it is not guessed from the extension of a file. |
| `description` | What the tool does. The model reads this to decide whether the tool fits a task, so it should describe the answer and not the implementation. |

The remaining fields depend on the type. A field which does not belong to the declared type is an error and not
something which is ignored, because ignoring it would let an administrator believe in an effect which does not exist.

A mistake in the file (a tool without a name, a tool without a type, an unknown type, a name which is used twice,
invalid json) makes the server fail on startup instead of when somebody happens to call the affected tool. A file which
is configured but does not exist is an error as well - only *no* configured file means "no tools".

### Type `python`

| Field | Meaning |
| ----- | ------- |
| `calls` | The script which implements the tool. A relative path is resolved against the folder of the tool-definitions-file, and the script has to stay inside that folder. |
| `parameters` | The names of the parameters. Each of them is passed to the script as `--<name>=<value>`, so a name here has to be the name of an option the script accepts. |
| `requirements` | Optional. The packages the script needs, in the notation of a requirements.txt. |
| `credentials` | Optional. The accesses the script needs; they arrive as environment-variables. See [Credentials](#credentials). |

```json
{
    "tool-name": "get_repository_information",
    "type": "python",
    "calls": "GetRepositoryInformation.py",
    "description": "Returns the description and the amount of stars of a repository on github.",
    "requirements": [ "requests==2.32.3" ],
    "parameters": [ "owner", "repository" ]
}
```

**Every python-tool runs in a virtual environment of its own**, named after the tool. That is what makes the packages
of two tools independent of each other: without it, two tools which need two versions of the same package could not
both work.

The environment is built when the tool is called for the first time and not when the server starts, so a server which
offers ten tools does not build ten environments before it answers anything. The first call of a tool whose packages
have to be downloaded therefore takes noticeably longer than the following ones.

The environment is built again as soon as the declared requirements differ from the ones it was built with. Otherwise
an administrator would change the requirements of a tool, nothing would happen, and the reason for that would be a
folder which nobody looks at. An environment whose creation failed is removed, so a half-built one can not be mistaken
for a working one.

The environments are located below the data-folder of the server (in the image: `/Workspace/Data/VirtualEnvironments`).
The commandline-parameter `--InitialVirtualEnvironmentsFolder` puts them somewhere else, for example on a volume which
survives a restart of the container. They can not lie next to the scripts, because that folder is usually mounted
read-only.

### Type `shell`

| Field | Meaning |
| ----- | ------- |
| `calls` | The script which implements the tool, resolved like the script of a python-tool. |
| `parameters` | The names of the parameters. Each of them is passed to the script as `--<name>=<value>`. |

```json
{
    "tool-name": "get_current_utc_time",
    "type": "shell",
    "calls": "GetCurrentUTCTime.sh",
    "description": "Returns the current point in time in utc.",
    "parameters": [ ]
}
```

The script is run with `/bin/sh`, so **a shell-tool needs a linux-system**; on another system the tool answers with
exactly that reason instead of a failure which looks like a broken script. Running it with the shell (instead of
starting the file itself) also means that the file does not have to carry the permission to be executed, which a file
that is mounted into a container usually does not.

The arguments arrive as one token each, so a script reads them like this:

```sh
for argument in "$@"; do
  case "$argument" in
    --package=*) package="${argument#--package=}" ;;
  esac
done
```

A value still has to be checked before it is used - see [Security](#security).

### Type `web-request`

Such a tool implements no script at all: the client states which address it wants, and the declaration states which
addresses the tool allows.

| Field | Meaning |
| ----- | ------- |
| `allowed-urls` | The addresses the tool may request, as regular expressions. At least one rule has to be declared. |
| `credentials` | Optional. The accesses the tool needs; a header refers to them. See [Credentials](#credentials). |
| `headers` | Optional. The headers the request carries. A value may refer to a credential of this tool with `${<name>}`. |

```json
{
    "tool-name": "get_pypi_package_metadata",
    "type": "web-request",
    "description": "Returns the metadata which pypi publishes about a python-package. The address of the package is \"pypi.org/pypi/<package-name>/json\".",
    "allowed-urls": [ "pypi\\.org/pypi/.*" ]
}
```

Such a tool declares no `parameters`: it takes exactly the one parameter which the mechanism defines, namely `url`,
which is the address the client wants.

Rules for the addresses:

- **The protocol is not part of an address.** It is always `https`, and it can not be changed. An address which states
  another protocol is rejected instead of being corrected silently.
- **A rule has to match the whole address.** Every rule is anchored at both ends, so `example\.com/.*` allows
  everything below `example.com` while `example\.com` allows exactly that one address. Without the anchoring, an
  address of a foreign host which contains an allowed one somewhere (`other.com/?x=example.com/`) would satisfy the
  rule.
- **What no rule allows is denied.** A tool without a matching rule requests nothing, which is why a declaration
  without a rule is an error: such a tool could never do anything.
- **`".*"` allows everything.** It is possible and it is not recommended: such a tool can reach every address which the
  server can reach, including the ones inside its own network.
- **The address which the rules see is the one which is requested.** It is taken from the parsed address (host, port
  and path), so an address which only looks like an allowed one does not become one. An address with credentials is
  rejected, because the rules would not see them while the request would send them.
- **Redirects are not followed.** The answer of a redirect is handed over as what it is, because the address which the
  rules allowed would otherwise only be the first one of a chain.

The answer of the tool is a json-document with the status-code and the body of the answer:

```json
{"status-code":200,"body":"..."}
```

## Credentials

A tool which uses a foreign service usually needs an access to it (a token, an api-key, a login). Such an access
belongs to the administrator who runs the server and never to the client, so it is not a parameter: the operator sets
it as an environment-variable of the server, and the declaration states which tool gets which one.

```json
{
    "tool-name": "get_repository_information",
    "type": "python",
    "calls": "GetRepositoryInformation.py",
    "description": "Returns the description and the amount of stars of a repository on github.",
    "credentials": { "TOKEN": "GITHUB_TOKEN" },
    "parameters": [ "owner", "repository" ]
}
```

The name on the left is the one the tool sees, the name on the right is the environment-variable of the server the
value is read from. They are stated separately so that the same script can be declared twice and use a different access
each time. A python-script reads its access as an environment-variable:

```python
token = os.environ["TOKEN"]
```

A tool of the type `web-request` uses its accesses in its headers, where `${<name>}` refers to one of them:

```json
{
    "tool-name": "get_private_repository",
    "type": "web-request",
    "description": "Returns what the github-api answers about a repository.",
    "allowed-urls": [ "api\\.github\\.com/repos/.*" ],
    "credentials": { "TOKEN": "GITHUB_TOKEN" },
    "headers": {
        "Authorization": "Bearer ${TOKEN}",
        "Accept": "application/vnd.github+json"
    }
}
```

A tool of the type `shell` receives no credentials.

### How the value reaches the container

Either directly as an environment-variable, or - better - as a docker-secret. For a credential which is read from
`GITHUB_TOKEN`, the variable `GITHUB_TOKEN_FILE` states a file whose content is the value; the value itself then does
not have to stand in the compose-file. A trailing line-break of that file is not part of the value.

```yaml
services:
  genericmcpserver:
    environment:
      InitialToolDefinitionsFile: '/Workspace/Configuration/Tools/Tools.json'
      GITHUB_TOKEN_FILE: '/run/secrets/github_token'
    secrets:
      - github_token
secrets:
  github_token:
    file: ./github_token
```

### What the server guarantees - and what it does not

- **A tool receives exactly its own credentials.** A script inherits the environment of the server, so the variables
  which credentials are read from are removed from it before the declared ones are added under their own names. Every
  other variable (`PATH`, `HOME`, …) stays untouched.
- **A missing credential keeps the server from starting**, with the name of the credential in the message. A forgotten
  secret is therefore visible while deploying and not when a model happens to call the affected tool.
- **A value never reaches a client through a message.** The message of a failed tool-call is handed to the client, so
  every known value is replaced by `***` in it beforehand. This is a safety-net and not a permission: a script should
  never write its access into its output, because the answer of a successful call is not searched.
- **This is no sandbox.** All tools run as the same user in the same container. The separation is about what is handed
  over, not about what a script could reach if it went looking for it.

## Security

The values of the parameters are provided by the client and are therefore untrusted. Two mechanisms handle that:

**A value can not become a command.** No shell is involved when a script is started: `UseShellExecute` is false and
every argument is added as its own entry of `ProcessStartInfo.ArgumentList`, so the operating-system hands each of
them to the script as exactly one argument. This is stronger than escaping would be, because there is no quoting which
could be escaped wrongly, forgotten or applied twice. Every value is additionally passed as one single token in the
form `--name=value`, so a value which looks like an option (for example `--help`) can not be read as one. This holds
for a shell-tool as well: the shell runs the script, it never sees the values as part of a command-line.

**A value has to have the format the script expects.** Each script checks each of its arguments against a regular
expression which is anchored at both ends and rejects everything which does not match completely. A value which is
rejected makes the script end with an exit-code which is not 0; the server turns that into a failed tool-call whose
message contains what the script wrote, so the model learns what was wrong and can correct itself.

For a tool of the type `web-request` the same question is answered by the rules of its declaration instead of by a
script, because there is no script which could answer it.

The declaration itself comes from an administrator and is therefore trusted, but the script it names still has to be
located inside the folder of the tool-definitions-file, so that a mistake there can not start an unrelated program.

## What a failed tool-call looks like

A tool-call which did not work - a script which ended with an exit-code which is not 0, a script which ran into the
timeout of 60 seconds, an address which no rule allows, a virtual environment which could not be built - is answered in
two ways at once:

- as a result of the protocol with `isError` and the message of the failure, so that the model learns what was wrong
  and can correct itself,
- and with the **http-status-code 502** for the whole answer, because the failure comes from the program behind this
  server and this server is the gateway to it.

The body of such an answer is the normal answer of the protocol; only its status-code differs.

## Example

`GenericMCPServer/Other/Examples/MinimalDockerComposeFile/Tools` contains the scripts and the declaration which turns
them into the tools `get_cve_details`, `get_maven_versions`, `get_nuget_versions`, `get_pypi_versions`,
`get_npm_versions`, `get_current_utc_time` and `get_pypi_package_metadata` - one of each type. None of them is known to
this codeunit; they are an example of what can be declared.

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

The answer arrives as an event-stream, so it is the line which starts with `data: `. The accept-header has to name both
content-types.

Which tools are offered:

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

Calling a **python**-tool:

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cve_details\",\"arguments\":{\"cve-identifier\":\"CVE-2021-44228\"}}}"
```

Calling a **shell**-tool (this one takes no argument):

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"get_current_utc_time\",\"arguments\":{}}}"
```

Calling a **web-request**-tool - the address is the argument, and it carries no protocol:

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"get_pypi_package_metadata\",\"arguments\":{\"url\":\"pypi.org/pypi/requests/json\"}}}"
```

An answer looks like this (the result of the tool is the text of its content):

```
data: {"result":{"content":[{"type":"text","text":"2026-08-20T09:12:44Z"}],"isError":false},"id":3,"jsonrpc":"2.0"}
```

A call which failed answers with the status-code 502 and states the reason:

```
data: {"result":{"content":[{"type":"text","text":"The tool \"get_pypi_package_metadata\" does not allow a request to \"example.com/\". A tool allows exactly the addresses which the rules of its declaration match."}],"isError":true},"id":4,"jsonrpc":"2.0"}
```

Every script can also be called on its own, which is the fastest way to check what a tool answers:

```
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
```

## How to add a tool

For a tool with a script: put the script into the folder of the tool-definitions-file, let it check every argument
against the format it must have, and add an entry with its type and its parameters. For a tool of the type
`web-request`: add an entry with the addresses it may request. Nothing in this codeunit has to be changed and nothing
has to be recompiled - the server picks the tool up when it is restarted.
