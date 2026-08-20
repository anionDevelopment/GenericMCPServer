# Hints

Hints for whoever works on this codeunit. What the product does, how an mcp-server with its tools is declared and who
may use it is documented in the [reference](./index.md).

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) (see `GenericMCPServerBackend.csproj` for the required version)
- A python-interpreter including the module `venv`, if the declared tools are implemented as python-scripts: every
  python-tool runs in a virtual environment of its own

This codeunit needs neither a database nor any other external service.

## IDE

Recommended IDE: [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with the C#-extension.

## Run it locally

```
dotnet run --project GenericMCPServerBackend -- Run --RealRun true --InitialToolDefinitionsFile <path-to-Tools.json>
```

Every declared mcp-server is then reachable under `http://127.0.0.1:80/<url-namespace>`. Without the parameter
the server starts as well but hosts no mcp-server at all.

The access-tokens and the roles are kept in `Other/Workspace/Configuration` (`AccessTokens.csv` and `Roles.csv`), which
the server creates when they do not exist yet. A token is created with

```
dotnet run --project GenericMCPServerBackend -- CreateAccessToken --Client my-client --ValidForDays 30
```

and an mcp-server which is declared with `"anonymous": true` needs none.

If a start was aborted, delete the folder `Other/Workspace`: it contains the configuration the server wrote, and an
incomplete one makes every following start fail with an error which does not point at the actual cause.

## Add an mcp-server or a tool

See the [reference](./index.md).
