# Hints

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) (see `GenericMCPServerBackend.csproj` for the required version)
- A python-interpreter, if the declared tools are implemented as python-scripts

This codeunit needs neither a database nor any other external service.

## IDE

Recommended IDE: [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with the C#-extension.

## Run it locally

```
dotnet run --project GenericMCPServerBackend -- Run --RealRun true --InitialToolDefinitionsFile <path-to-Tools.json>
```

The MCP-endpoint is then reachable under `http://127.0.0.1:80/API/v1/MCP`. Without the parameter the server starts as
well but offers no tool.

If a start was aborted, delete the folder `Other/Workspace`: it contains the configuration the server wrote, and an
incomplete one makes every following start fail with an error which does not point at the actual cause.

## Add a tool

See the [reference](./index.md).
