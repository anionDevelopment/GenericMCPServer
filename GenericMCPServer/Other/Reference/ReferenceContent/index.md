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
| `InitialToolsDefinitionFilePath` | Which file declares the mcp-servers, as the whole path of that file and as the value which is written into the configuration-file the first time it is generated (default: `/Workspace/Tools/Tools.json`). It has no effect on a server which already has a configuration-file. |
| `InitialEnableEndpointHealthCheckValue` | Whether the maintenance-health-check-route is available (default: `true`). |
| `InitialEnableEndpointAvailabilityCheckValue` | Whether the availability-check-route is available (default: `true`). |
| `InitialEnableEndpointInitializationStateValue` | Whether the route which states the initialization-state is available. |
| `InitialEnableEndpointCurrentVersionValue` | Whether the version-route is available. |
| `InitialEnableEndpointShowAllEndpointsValue` | Whether the route which lists all endpoints is available. |
| `InitialEnableEndpointMetricsValue` | Whether the metrics-route is available. |
| `EnforceVerbose` | Set it to `true` to get verbose logging. |

Which file declares the mcp-servers with their tools is read from the persisted configuration-file of the server
itself (`ToolDefinitionsFile`) and never from the environment. That value is the whole path of the file including its
name, and it defaults to `/Workspace/Tools/Tools.json`. `InitialToolsDefinitionFilePath` decides only which path that
configuration-file is generated with; from the second start on the configuration-file is what counts, so setting the
variable afterwards changes nothing and an administrator who wants the declaration somewhere else edits that file.

The declaration and every script it names belong in the tools-folder `/Workspace/Tools` or in a folder below it, and
nowhere else: a script is resolved relative to the file which declares it and has to stay inside its folder, so that
folder decides what a declaration is able to reach.

Because the configured value names a file and not a folder, the declaration can be handed into that folder as a
**single mounted file** instead of as a whole folder:

```yml
    volumes:
      - ./Tools.json:/Workspace/Tools/Tools.json
```

Nothing else of the host-folder around that file reaches the container then, and nothing the image keeps in
`/Workspace/Tools` is covered by the mount. This fits a declaration whose tools are web-requests, because those need
no script; a declaration which names scripts is handed over together with them, as
`Other/Examples/MinimalDockerComposeFile` shows.

Without a file at the configured place the server hosts no mcp-server at all; it creates an empty declaration there
and starts. The folder of that file has to exist - a path whose folder is not there makes the server fail while
starting instead of being created.

The tools-folder is not the configuration-folder: the latter is where the server writes its own files. Everything the
server uses lies below `/Workspace`:

| Folder | What is in it | Mount it? |
| ------ | ------------- | --------- |
| `/Workspace/Tools` | The declaration and the scripts it names, which an administrator writes. | Yes - it is how they reach the server. |
| `/Workspace/Configuration` | What the server writes about itself: its configuration-file and the files of its clients, roles and access-tokens. | Yes - without it every client and every access-token is gone with the container. |
| `/Workspace/Logs` | The logs of the server. | Only to keep them beyond the life of the container. |
| `/Workspace/Data` | The virtual environments of the python-tools, below `VirtualEnvironments`. Each of them is built when its tool is called for the first time. | Only to keep them; without it they are built again in a new container. |
| `/Workspace/Application` | The backend itself. | No. |

## Examples

See:

- [Minimal example](../../Examples/MinimalDockerComposeFile/ReadMe.md)
- [Advanced example](../../Examples/AdvancedDockerComposeFile/ReadMe.md)

## Health-check

The container checks itself with `HealthCheck.sh`. That script reads the protocol and the port from
`/Workspace/Configuration/Configuration.xml` and then asks `/API/Other/Maintenance/HealthCheck`, which is the
maintenance-route GRYLibrary provides - with the configuration this image generates that is
`http://127.0.0.1:80/API/Other/Maintenance/HealthCheck`.

The route therefore has to stay available: `InitialEnableEndpointHealthCheckValue` set to `false` makes that route
answer with 404, which makes the container report itself as unhealthy.
