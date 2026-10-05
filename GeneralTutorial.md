# MCPServer

## Administration

Set the server URL and administrator token once per terminal session:

```bash
export MCP_SERVER_URL="http://127.0.0.1:1222"
export MCP_ADMIN_TOKEN="gmcpadmin_xxx"
export CLIENT_NAME="myclient"
export CLIENT_ADMIN_TOKEN="gmcpclientadmin_xxx"
export CLIENT_TOKEN="gmcp_xxx"
```

All administration routes require `X-Admin-Token`.

### Clients

```bash
# List clients, their roles, and the number of access tokens.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients"

# Create a client. Store the returned clientAdministrationToken immediately.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME"

# Replace a leaked or expired client-administration token.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME/AdministrationToken"

# Delete a client, including its access tokens and role assignments.
curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME"
```

### Roles

```bash
# List all roles.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Roles"

# Create or delete a role.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Roles/developer"

curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Roles/developer"
```

### Role Assignments

```bash
# List the roles of a client.
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME/Roles"

# Assign or remove a role for a client.
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME/Roles/developer"

curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v1/Admin/Clients/$CLIENT_NAME/Roles/developer"
```

## Client Administration

### List access tokens

```sh
curl -X GET http://127.0.0.1:1222/API/v1/ClientAdministration/AccessTokens -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

### Create access token

```sh
curl -X POST "http://127.0.0.1:1222/API/v1/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

### Delete access token

```sh
curl -X DELETE "http://127.0.0.1:1222/API/v1/ClientAdministration/AccessTokens/<hash>" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

## Configure for usage

### Configure GitHub Copilot

#### GitHub Copilot on MacOS for VSCode

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

#### GitHub Copilot on MacOS for CLI

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
