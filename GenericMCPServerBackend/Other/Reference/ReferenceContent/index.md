# GenericMCPServerBackend

GenericMCPServerBackend is the backend for GenericMCPServer. It is a MCP-server which is reachable over HTTP.

## How it is built

The MCP-protocol itself is not implemented in this codeunit. It is implemented by the MCP-SDK of Microsoft (the
packages `ModelContextProtocol`, `ModelContextProtocol.AspNetCore` and `ModelContextProtocol.Core`), which is
registered as a service and whose endpoint is mapped in `Program.cs`:

```csharp
services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<BusinessLogicService>();
...
webApplication.MapMcp("/API/v1/MCP");
```

The SDK therefore handles the initialize-handshake, the methods `tools/list` and `tools/call`, the generation of the
argument-schemas and the error-handling. This codeunit only contributes the tools themselves, which are the methods of
the `BusinessLogicService`.

The transport runs in the stateless mode, so no session-id has to be kept by the client and every request can be sent
on its own. That is sufficient because the tools hold no state between two calls.

## Endpoint

All messages of the protocol are exchanged over one single endpoint:

```
POST /API/v1/MCP
```

## Tools

A tool is a method of the `BusinessLogicService` which is marked with the `[McpServerTool]`-attribute; the class
itself is marked with `[McpServerToolType]`, which is what makes the SDK look at its methods at all. The name of a
tool comes from the attribute, its description and the description of every argument come from the
`[Description]`-attribute, and the schema of the arguments is derived from the signature of the method.

| Tool | Arguments | Meaning |
| ---- | --------- | ------- |
| `get_cve_details` | `cveIdentifier` | Returns description, severity and references of a CVE. |
| `get_available_maven_versions` | `groupId`, `artifactId` | Returns the versions of a package on maven-central. |
| `get_available_nuget_versions` | `packageId` | Returns the versions of a package on nuget.org. |
| `get_available_pypi_versions` | `packageName` | Returns the versions of a package on pypi. |
| `get_available_npm_versions` | `packageName` | Returns the versions of a package on the npm-registry. |

Every one of them is implemented as a python-script in `GenericMCPServerBackend/Resources/Tools`, which the
corresponding method starts. A script can therefore be changed without recompiling anything.

## Security

The arguments of a tool are provided by the client and are therefore untrusted. Two mechanisms handle that:

**The argument can not become a command.** No shell is involved when a script is started: `UseShellExecute` is false
and every argument is added as its own entry of `ProcessStartInfo.ArgumentList`, so the operating-system hands each of
them to the script as exactly one argument. A value can therefore not end a command and start another one, no matter
which characters it contains. This is stronger than escaping the values would be, because there is no quoting which
could be escaped wrongly, forgotten or applied twice. Every value is additionally passed in the form
`--option=value` as one single token, so a value which looks like an option (for example `--help`) can not be read as
one.

**The argument has to have the expected format.** Every script checks each of its arguments against a regular
expression which is anchored at both ends, and rejects everything which does not match completely. The check lives in
the script (as the `type` of the argument of `argparse`), so it also applies when a script is called directly and the
format is defined exactly once, next to the code which depends on it. For the package-tools this is not only a
plausibility-check: the values become a part of the path of the url which is requested, so a value like `..` would
otherwise address a different resource than the package it claims to be.

A value which is rejected makes the script end with an exit-code which is not 0, which the server turns into a failed
tool-call.

## How to use it in a MCP-client

This version does not implement a discovery- or installation-mechanism, so the server has to be registered manually.
Configure it as a server with the transport "http" and the address of the endpoint:

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

## How to test it without a client

The endpoint answers a plain http-request. The `Accept`-header has to name both content-types, because the transport
answers as an event-stream:

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

Calling one tool:

```
curl -X POST http://127.0.0.1:8080/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"get_available_nuget_versions\",\"arguments\":{\"packageId\":\"Newtonsoft.Json\"}}}"
```

Every script can also be called on its own, which is the fastest way to check what a tool answers:

```
python GetAvailableNuGetVersions.py --package-id=Newtonsoft.Json
python GetAvailableMavenVersions.py --group-id=org.apache.logging.log4j --artifact-id=log4j-core
python GetAvailablePyPIVersions.py --package-name=requests
python GetAvailableNPMVersions.py --package-name=@angular/cli
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
```

## How to add a tool

Add a method to the `BusinessLogicService`, mark it with `[McpServerTool]` and describe it and its parameters with
`[Description]`. Declare it in `IBusinessLogicService` as well, so that the operation is part of the interface of the
business-logic. If it is implemented as a script, put the script into `Resources/Tools`, let it parse its arguments
with `argparse` and give every argument a `type`-function which checks it against the format it must have.
