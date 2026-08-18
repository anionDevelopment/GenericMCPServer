# Hints

## Requirements

- [.NET SDK](https://dotnet.microsoft.com/download) (see `GenericMCPServerBackend.csproj` for the required version)

This codeunit needs neither a database nor any other external service.

## IDE

Recommended IDE: [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/) with the C#-extension.

## Run it locally

Run the following command in the folder `GenericMCPServerBackend` to start the server on the local machine:

```
dotnet run --project GenericMCPServerBackend -- Run --RealRun true
```

The MCP-endpoint is then reachable under `http://127.0.0.1:80/API/v1/MCP`.

## Add a tool

See the [reference](./index.md).
