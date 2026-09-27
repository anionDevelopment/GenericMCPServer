# Advanced docker compose example

The following files will be used in this example:

- [docker-compose.yml](./docker-compose.yml)
- [Tools.json](./Tools/Tools.json)
- Tool-scripts beside the `Tools.json`

Startable by `task bae`.
Stoppable by `task baet`.

| MCP-server       | Address                                | Who may use it         | Tools                                                                                                                                                                                    |
| ---------------- | -------------------------------------- | ---------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GeneralTools`   | `http://127.0.0.1:1222/GeneralTools`   | everybody (anonymous)  | the current point in time and the content of a web-page which its declaration allows                                                                                                     |
| `Security`       | `http://127.0.0.1:1222/Security`       | the role `security`    | the details of a CVE                                                                                                                                                                     |
| `DeveloperTools` | `http://127.0.0.1:1222/DeveloperTools` | the roles `developer`  | the available versions of a maven-, NuGet-, PyPI- and npm-package, the release-notes of a NuGet-package, where the changelog of a PyPI-package stands and the metadata of a PyPI-package |
