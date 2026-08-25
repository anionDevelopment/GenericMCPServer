# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP. This codeunit builds the container-image which packages
the backend.

## Content of the image

The image contains the build-result of GenericMCPServerBackend, the .NET-runtime it needs and a python-interpreter
including the module `venv`, so that tools which are implemented as python-scripts can be run in a virtual environment
of their own. It contains no tool and no declaration: both are provided from outside.

## Configuration

Every value is passed as an environment-variable.

| Environment-variable | Meaning |
| -------------------- | ------- |
| `InitialDomain` | The domain the server uses in the links it generates. |
| `InitialEnableEndpointHealthCheckValue` | Whether the maintenance-health-check-route is available (default: `true`). |
| `InitialEnableEndpointCurrentVersionValue` | Whether the version-route is available. |
| `InitialEnableEndpointShowAllEndpointsValue` | Whether the route which lists all endpoints is available. |
| `InitialEnableEndpointMetricsValue` | Whether the metrics-route is available. |
| `EnforceVerbose` | Set it to `true` to get verbose logging. |

Which file declares the mcp-servers with their tools is not one of these environment-variables: it is part of the
persisted configuration-file of the server itself (`ToolDefinitionsFile`), which defaults to `./Tools.json`
(relative to the configuration-folder) the first time that file is generated. The declaration and the scripts it
points to have to be mounted together at that location, because a script is resolved relative to the file which
declares it - the example in `Other/Examples/MinimalDockerComposeFile` shows that. Without a file there the server
hosts no mcp-server at all.

## Health-check

The container checks itself over `http://127.0.0.1:80/API/Other/Maintenance/HealthCheck`, which is the
maintenance-route GRYLibrary provides.
