# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP.

It offers exactly the tools which an administrator declares: a declaration names what implements a tool, what the tool
does and which parameters it takes. The product itself brings no tool along, which is what makes it generic.

A tool is implemented by a python-script, by a shell-script or by a web-request whose addresses the declaration
restricts.

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

A client needs an access-token for it, which is created in the running container:

```
docker exec genericmcpserver dotnet /Workspace/Application/Backend/GenericMCPServerBackend.dll CreateAccessToken --Client my-client --ValidForDays 30
```

The token is shown once and is sent as `Authorization: Bearer <token>`. A tool which everybody may use is declared
with `"anonymous": true` and needs none.

How a tool is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Reference

The reference of the repository as a whole can be found [here](./Other/Reference/ReadMe.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
