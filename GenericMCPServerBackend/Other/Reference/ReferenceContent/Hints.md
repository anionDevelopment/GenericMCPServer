# Hints

Hints for whoever works on this codeunit. What the product does, how an mcp-server with its tools, its resources, its
prompts and the values which their arguments can have is declared and who may use it is documented in the
[reference](./index.md).

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) (see `GenericMCPServerBackend.csproj` for the required version)
- A python-interpreter including the module `venv`, if the declared tools are implemented as python-scripts: every
  python-tool runs in a virtual environment of its own

This codeunit needs neither a database nor any other external service.

## IDE

Recommended IDE: [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with the C#-extension.

## Run it locally

```
dotnet run --project GenericMCPServerBackend -- Run --RealRun true
```

The first start creates `Other/Workspace/Configuration/Configuration.xml`, which - among the rest of the persisted
configuration - defaults `ToolDefinitionsFile` to `./Tools.json` (relative to the configuration-folder). Put your
`Tools.json` (and whatever it declares) at `Other/Workspace/Configuration/Tools.json`, or change the value in
`Configuration.xml` to point somewhere else; either way the server has to be restarted afterwards, because it only
reads the tool-definitions-file once, at startup. Without a file at the resolved location the server starts as well
but hosts no mcp-server at all.

Every declared mcp-server is then reachable under `http://127.0.0.1:80/<url-namespace>`.

The clients, their roles and their access-tokens are kept in the csv-files of `Other/Workspace/Configuration`, which
the server creates when they do not exist yet. The token with which an administrator reaches the admin-routes is
generated at the first start and written to the console; only its hash is stored (`AdminToken.csv`). Whoever missed it
deletes that file and starts the server again. How a client and its tokens
are created is described in the [reference](./index.md); an mcp-server which is declared with `"anonymous": true` needs
none of it.

If a start was aborted, delete the folder `Other/Workspace`: it contains the configuration the server wrote, and an
incomplete one makes every following start fail with an error which does not point at the actual cause.

## Add an mcp-server, a tool, a resource, a prompt or the values of one of its arguments

See the [reference](./index.md).
