# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP.

It offers exactly the tools which an administrator declares: a declaration names what implements a tool, what the tool
does and which parameters it takes. The product itself brings no tool along, which is what makes it generic.

A tool is implemented by a python-script, by a shell-script or by a web-request whose addresses the declaration
restricts.

## Features which are not implemented yet

- Client-authentication: admins should be able to create accesstoken for clients for non-anonymous access. one client can have multiple valid access token at the same time. an accesstoken must have an expire-timestamp.
- allow admins to optionally Restrict specific tools to specific clients using accesstoken-checks. 
- allow (or enforce) hosting multiple mcp server at once using separation (some kind like a namespace) in the domain (url "<domain>/teamA/message" as mcp-server for team A and "<domain>/teamB/message" as mcp-server for team B)

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

The example declares seven tools and mounts them into the container: five python-tools (CVE-details and the available
versions of a maven-, nuget-, pypi- and npm-package), one shell-tool (the current point in time) and one
web-request-tool (the metadata of a package on pypi). The MCP-endpoint is then available under
`http://127.0.0.1:8080/API/v1/MCP`.

How a tool is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Reference

The reference of the repository as a whole can be found [here](./Other/Reference/ReadMe.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
