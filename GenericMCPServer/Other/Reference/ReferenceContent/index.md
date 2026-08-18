# GenericMCPServer

GenericMCPServer is a simple MCP-server which is reachable over HTTP.

This codeunit builds the container-image which packages the backend.

## Content of the image

The image contains the build-result of GenericMCPServerBackend and the .NET-runtime it needs. It does not contain a reverse-proxy:
the backend serves the MCP-endpoint over HTTP itself.

## Configuration

Every value is passed as an environment-variable and is optional. Without them the defaults of the backend are used.

| Environment-variable | Meaning |
| -------------------- | ------- |
| `InitialDomain` | The domain the server uses in the links it generates. |
| `InitialEnableEndpointHealthCheckValue` | Whether the maintenance-health-check-route is available (default: `true`). |
| `InitialEnableEndpointCurrentVersionValue` | Whether the version-route is available. |
| `InitialEnableEndpointShowAllEndpointsValue` | Whether the route which lists all endpoints is available. |
| `InitialEnableEndpointMetricsValue` | Whether the metrics-route is available. |
| `EnforceVerbose` | Set it to `true` to get verbose logging. |

## Health-check

The container checks itself over `http://127.0.0.1:80/API/Other/Maintenance/HealthCheck`, which is the
maintenance-route GRYLibrary provides.

## Example

The folder `Other/Examples/MinimalDockerComposeFile` contains a docker-compose-file which starts the built image and
the script `RunExample.py` which runs it.
