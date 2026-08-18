# GenericMCPServer

GenericMCPServer is a simple MCP-server which is reachable over HTTP.

It exposes the tools which [GenericMCPServerBackend](./GenericMCPServerBackend/ReadMe.md) implements over the MCP-protocol (JSON-RPC 2.0 over
HTTP), so that an MCP-client can discover and call them.

## Codeunits

| Codeunit | Description |
| -------- | ----------- |
| [GenericMCPServerBackend](./GenericMCPServerBackend/ReadMe.md) | The backend which implements the MCP-protocol and the tools. |
| [GenericMCPServer](./GenericMCPServer/ReadMe.md) | The container-image which packages the backend. |

## Getting started

Build all codeunits and start the resulting image:

```
task bb
cd GenericMCPServer\Other\Examples\MinimalDockerComposeFile
python RunExample.py
```

The MCP-endpoint is then available under `http://127.0.0.1:8080/API/v1/MCP`. How to register it in an MCP-client and
which tools exist is described in the [reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
