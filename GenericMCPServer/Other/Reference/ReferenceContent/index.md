# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP. This codeunit builds the container-image which packages the
backend.

## Content of the image

The image contains the build-result of GenericMCPServerBackend, the .NET-runtime it needs and a python-interpreter, so that tools
which are implemented as python-scripts can be run. It contains no tool and no declaration: both are provided from
outside.

## Configuration

Every value is passed as an environment-variable.

| Environment-variable | Meaning |
| -------------------- | ------- |
| `InitialToolDefinitionsFile` | The file which declares the tools. Without it the server offers no tool. |
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
