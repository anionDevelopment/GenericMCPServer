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
| `InitialToolDefinitionsFile` | The file which declares the mcp-servers with their tools. Without it the server hosts none. |
| `InitialAccessTokensFile` | The file which holds the access-tokens. Without it: `AccessTokens.csv` in the configuration-folder. |
| `InitialRolesFile` | The file which states which client is in which role. Without it: `Roles.csv` in the configuration-folder. |
| `InitialVirtualEnvironmentsFolder` | The folder which holds the virtual environments of the python-tools. Without it: below the data-folder. |
| `InitialDomain` | The domain the server uses in the links it generates. |
| `InitialEnableEndpointHealthCheckValue` | Whether the maintenance-health-check-route is available (default: `true`). |
| `InitialEnableEndpointCurrentVersionValue` | Whether the version-route is available. |
| `InitialEnableEndpointShowAllEndpointsValue` | Whether the route which lists all endpoints is available. |
| `InitialEnableEndpointMetricsValue` | Whether the metrics-route is available. |
| `EnforceVerbose` | Set it to `true` to get verbose logging. |

The declaration and the scripts it points to have to be mounted together, because a script is resolved relative to the
file which declares it. The example in `Other/Examples/MinimalDockerComposeFile` shows that.

## Health-check

The container checks itself over `http://127.0.0.1:80/API/Other/Maintenance/HealthCheck`, which is the
maintenance-route GRYLibrary provides.
