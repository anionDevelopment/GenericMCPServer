# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP.

It offers exactly the tools which an administrator declares: a declaration names what implements a tool, what the tool
does and which parameters it takes. The product itself brings no tool along, which is what makes it generic.

A tool is implemented by a python-script, by a shell-script or by a web-request whose addresses the declaration
restricts.

## Features which are not implemented yet

- allow (or enforce) hosting multiple mcp server at once using separation (some kind like a namespace) in the domain (url "<domain>/teamA/message" as mcp-server for team A and "<domain>/teamB/message" as mcp-server for team B). so the tools-list in tools.json should be wrapped in a list of mcp-server-properties which has the tool-list and a this properties must also declare for each mcp server its url-namespace (for example `teamA` or `teamA/mcpserver1`). the access-token must then be valid for a specific mcp server (instead of a specific tool). when an access-token is valid for a mcp-server, then it is automatically valid for all of its tools. the option for anonymous access should also be available only for mcp-server and not for a single tool.

## Codeunits

| Codeunit | Description |
| -------- | ----------- |
| [GenericMCPServerBackend](./GenericMCPServerBackend/ReadMe.md) | The backend which implements the MCP-protocol and runs the declared tools. |
| [GenericMCPServer](./GenericMCPServer/ReadMe.md) | The container-image which packages the backend. |

## Getting started

```
task bb
cd GenericMCPServer\Other\Examples\MinimalDockerComposeFile
python RunExample.py
```

The example declares some tools and mounts them into the container. With this you can receive CVE-details and the available
versions of a maven-, NuGet-, PyPI- and npm-package, get the current point in time access whitelisted URLs.
The MCP-endpoint is then available under
`http://127.0.0.1:8080/API/v1/MCP`.

How a tool is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Reference

The reference of the repository as a whole can be found [here](./Other/Reference/ReadMe.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
