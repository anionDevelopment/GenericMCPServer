# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP.

It offers exactly the tools which an administrator declares: a declaration names the script which implements a tool,
what the tool does and which parameters it takes. The product itself brings no tool along, which is what makes it
generic.

## Features which are not implemented yet

- Client-authentication
- Restrict specific tools to specific clients

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

The example declares five tools (CVE-details and the available versions of a maven-, nuget-, pypi- and npm-package)
and mounts them into the container. The MCP-endpoint is then available under
`http://127.0.0.1:8080/API/v1/MCP`.

How a tool is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
