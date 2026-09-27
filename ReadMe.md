# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP.
It offers exactly the tools, the resources and the prompts which an administrator declares - each of them implemented
by a python-script, by a shell-script or by a web-request - and brings none of them of its own along, which is what
makes it generic.
It also offers an authorization-system which hosts several MCP-servers in one process and lets certain clients reach
certain MCP-servers, depending on the roles of the client.

## Getting started

### Install MCP-server

The image is available on docker-hub as
[aniondev/genericmcpserver](https://hub.docker.com/r/aniondev/genericmcpserver).

An example docker-compose file can look like this:

```yml
name: 'genericmcpserver'
services:
  genericmcpserver:
    image: genericmcpserver:latest
    container_name: 'genericmcpserver'
    environment:
      EnforceVerbose: 'true'
      InitialToolsDefinitionFilePath: '/Workspace/Tools/Tools.json'
    volumes:
      - ./Tools:/Workspace/Tools
      - ./Configuration:/Workspace/Configuration
      - ./Logs:/Workspace/Logs
    ports:
      - "1222:80"
```

On first run the admin-token is printed in the container-output (not in the logs) on the first run.
You can recognize this token by the token-prefix `gmcpadmin_`.

For the next configuration steps, let us first save the most important variables in env-variables:

```bash
export MCP_SERVER_URL="http://127.0.0.1:1222"
export MCP_ADMIN_TOKEN="gmcpadmin_xxx"
```

```bash
curl -X POST http://127.0.0.1:1222/API/v2/Admin/Roles/developer -H "X-Admin-Token: $MCP_ADMIN_TOKEN"
curl -X POST http://127.0.0.1:1222/API/v2/Admin/Clients/$CLIENT_NAME -H "X-Admin-Token: $MCP_ADMIN_TOKEN"
curl -X POST http://127.0.0.1:1222/API/v2/Admin/Clients/$CLIENT_NAME/Roles/developer -H "X-Admin-Token: $MCP_ADMIN_TOKEN"
```

The second call answers with the **client-administration-token** of that client, and only that answer contains it. It
is handed to whoever runs the client, who creates access-tokens with it:

```bash
curl -X POST "http://127.0.0.1:1222/API/v2/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

That access-token is sent to an MCP-server as `Authorization: Bearer <token>`.

How a tool, how a resource, how a prompt and how the completion of an argument of a prompt is declared is described
in the [reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

### Configure MCP-server

#### Create and administrate Clients

Set the server URL and administrator token once per terminal session:

```bash
export CLIENT_NAME="myclient"
export CLIENT_ADMIN_TOKEN="gmcpclientadmin_xxx"
export CLIENT_TOKEN="gmcp_xxx"
```

All administration routes require `X-Admin-Token`.

##### Manage Clients

```bash
# List clients, their roles, and the number of access tokens.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients"

# Create a client. Store the returned clientAdministrationToken immediately.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME"

# Replace a leaked or expired client-administration token.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/AdministrationToken"

# Delete a client, including its access tokens and role assignments.
curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME"
```

##### Manage roles

```bash
# List all roles.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles"

# Create or delete a role.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles/developer"

curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles/developer"
```

```bash
# List the roles of a client.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles"

# Assign or remove a role for a client.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles/developer"

curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles/developer"
```

#### Manage Access-token

##### List access tokens

```sh
curl -X GET http://127.0.0.1:1222/API/v2/ClientAdministration/AccessTokens -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

##### Create access token

```sh
curl -X POST "http://127.0.0.1:1222/API/v2/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

##### Delete access token

```sh
curl -X DELETE "http://127.0.0.1:1222/API/v2/ClientAdministration/AccessTokens/<hash>" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

#### Configure Access-token on client

##### Configure Access-token for GitHub Copilot

###### GitHub Copilot on MacOS for VSCode

In VS Code, run `MCP: Open User Configuration` and add the following to the opened `~/Library/Application Support/Code/User/mcp.json` file:

```json
{
	"servers": {
		"general-tools": {
			"type": "http",
			"url": "http://127.0.0.1:1222/GeneralTools"
		},
		"security": {
			"type": "http",
			"url": "http://127.0.0.1:1222/Security",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		},
		"developer": {
			"type": "http",
			"url": "http://127.0.0.1:1222/DeveloperTools",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		}
	}
}
```

Copilot can then use the anonymous `GeneralTools` server. Replace `<base64(username:password)>` with the Base64 encoding of the Basic Auth credentials for `Security`; for example, run `printf '%s' 'username:password' | base64`. The `Security` and `DeveloperTools` servers require credentials with the corresponding role. Do not use the administrator token in the mcp-configuration on the client.

Available namespaces:

- `GeneralTools`: 1 anonymous tool
- `Security`: 1 tool, role `security`
- `DeveloperTools`: 8 tools, roles `developer` or `security`

###### GitHub Copilot on MacOS for CLI

Create `~/.copilot/mcp-config.json` and add the MCP servers under `mcpServers`:

```json
{
	"mcpServers": {
		"general-tools": {
			"type": "http",
			"url": "http://127.0.0.1:1222/GeneralTools"
		},
		"security": {
			"type": "http",
			"url": "http://127.0.0.1:1222/Security",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		},
		"developer": {
			"type": "http",
			"url": "http://127.0.0.1:1222/DeveloperTools",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		}
	}
}
```

##### Configure Access-token for Claude

TODO

## Reference

The reference of the repository can be found [here](./Other/Reference/ReadMe.md).

## Changelog

See the [Changelog-folder](./Other/Resources/Changelog).

## License

See [License.txt](./License.txt).
