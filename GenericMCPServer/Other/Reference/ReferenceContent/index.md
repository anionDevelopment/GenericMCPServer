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
| `InitialToolsDefinitionFilePath` | Which file declares the mcp-servers, as the value which is written into the configuration-file the first time it is generated (default: `./Tools.json`, relative to the tools-folder). It has no effect on a server which already has a configuration-file. |
| `InitialEnableEndpointHealthCheckValue` | Whether the maintenance-health-check-route is available (default: `true`). |
| `InitialEnableEndpointAvailabilityCheckValue` | Whether the availability-check-route is available (default: `true`). |
| `InitialEnableEndpointInitializationStateValue` | Whether the route which states the initialization-state is available. |
| `InitialEnableEndpointCurrentVersionValue` | Whether the version-route is available. |
| `InitialEnableEndpointShowAllEndpointsValue` | Whether the route which lists all endpoints is available. |
| `InitialEnableEndpointMetricsValue` | Whether the metrics-route is available. |
| `EnforceVerbose` | Set it to `true` to get verbose logging. |

Which file declares the mcp-servers with their tools is read from the persisted configuration-file of the server
itself (`ToolDefinitionsFile`) and never from the environment. `InitialToolsDefinitionFilePath` decides only which
value that configuration-file gets the first time it is generated, instead of the default `./Tools.json` (relative to
the tools-folder `/Workspace/Tools`); from the second start on the configuration-file is what counts, so setting the
variable afterwards changes nothing and an administrator who wants the declaration somewhere else edits that file.

The declaration and the scripts it points to have to be mounted together at the resolved location, because a script is
resolved relative to the file which declares it - the example in `Other/Examples/MinimalDockerComposeFile` shows that.
Without a file there the server hosts no mcp-server at all.

The tools-folder is not the configuration-folder `/Workspace/Configuration`: the latter is where the server writes its
own configuration-file and the files of its clients, roles and access-tokens. Mount both - the first one to hand the
declaration over, the second one so that the state of the server survives a new container.

## Health-check

The container checks itself over `http://127.0.0.1:80/API/Other/Maintenance/HealthCheck`, which is the
maintenance-route GRYLibrary provides.
