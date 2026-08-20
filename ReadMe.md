# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP.

It offers exactly the tools which an administrator declares: a declaration names what implements a tool, what the tool
does and which parameters it takes. The product itself brings no tool along, which is what makes it generic.

## Features

| Feature | Description |
| ------- | ----------- |
| Tools are declared, not built in | Which tools exist is decided by a declaration-file of an administrator. A new tool needs an entry there and no change of this product, no new version and no restart of anything but the server itself. |
| Three kinds of tools | A tool is implemented by a **python-script**, by a **shell-script** or by a **web-request** whose reachable addresses the declaration restricts. |
| A virtual environment per python-tool | The packages a python-tool needs are declared as its requirements and installed into an environment which belongs to that tool alone, so two tools can need two versions of the same package. |
| Web-requests with a whitelist | A tool of the kind web-request reaches exactly the addresses which the rules of its declaration match; everything else is denied. The protocol is always https and redirects are not followed. |
| Credentials per tool | A tool receives the accesses which its declaration assigns to it (from an environment-variable of the server or from a docker-secret), and no tool receives the accesses of another one. |
| **Several mcp-servers at the same time** | One process hosts as many mcp-servers as are declared. Each one has its **own tools** and its own address `<domain>/<url-namespace>`, so a client of one of them can neither see nor reach the tools of another one - even the same tool-name may exist in two of them. |
| **Several clients, each with several access-tokens** | An administrator creates access-tokens per client. **A client can hold several valid tokens at the same time**, which is what makes an exchange possible without an interruption, and every token ends at a stated point in time. Only the hash of a token is stored. |
| **Roles decide who may use what** | **A client is in none, one or several roles**, and an mcp-server declares **which roles** may use it. Who is in a role and what a role may do are therefore two decisions in two places: a new client needs no change of the declaration, and a changed declaration needs no new client. |
| **Anonymous mcp-servers** | An mcp-server can be declared as one which everybody may use. It then needs no token at all, while the other mcp-servers of the same process stay behind theirs. |
| Failures are visible | A tool-call which did not work is answered with the message of the failure and with the http-status-code 502, because the failure comes from the program behind this server. |

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

The example declares three mcp-servers and mounts them into the container:

| mcp-server | Address | Who may use it | Tools |
| ---------- | ------- | -------------- | ----- |
| `GeneralTools` | `http://127.0.0.1:8080/GeneralTools` | everybody (anonymous) | the current point in time |
| `Security` | `http://127.0.0.1:8080/Security` | the role `security` | the details of a CVE |
| `DeveloperTools` | `http://127.0.0.1:8080/DeveloperTools` | the roles `developer` and `security` | the available versions of a maven-, NuGet-, PyPI- and npm-package, the metadata of a PyPI-package and a research-tool for the usual sources of a developer |

`GeneralTools` works right away. For the other two, a client needs an access-token, which is created in the running
container:

```
docker exec genericmcpserver dotnet /Workspace/Application/Backend/GenericMCPServerBackend.dll CreateAccessToken --Client my-client --ValidForDays 30
```

The token is shown once and is sent as `Authorization: Bearer <token>`. Which mcp-servers that client reaches is
decided by its roles, which are stated in `Roles.csv` in the configuration-folder of the server (one line
`my-client;developer` puts the client into the role `developer`).

How a tool is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Reference

The reference of the repository as a whole can be found [here](./Other/Reference/ReadMe.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
