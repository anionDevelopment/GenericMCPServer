# GenericMCPServer

![Docker Pulls](https://img.shields.io/docker/pulls/aniondev/genericmcpserver)
![Coverage](./GenericMCPServerBackend/Other/Resources/TestCoverageBadges/badge_shieldsio_linecoverage_blue.svg)

### General information

GenericMCPServer hosts MCP-servers which are reachable over HTTP.
It offers exactly the tools, the resources and the prompts which an administrator declares - each of them implemented
by a python-script, by a shell-script or by a web-request - and brings none of them of its own along, which is what
makes it generic.
It also offers an authorization-system which hosts several MCP-servers in one process and lets certain clients reach
certain MCP-servers, depending on the roles of the client.

GenericMCPServer is supposed to run on a server using HTTP to communicate.
Running it locally for testing- and development-purposes is possible too.
Of course it is recommended to use HTTPS for the communication instead of HTTP.
For this purpose a simple reverse-proxy which manage the certificates and TLS-termination can be used.

GenericMCPServer allows you to host multiple MCP-servers at once.
This is useful to separate multiple tool-sets for different AI-clients.
For this reason, every MCP-server needs a name which is part of the MCP-server URL.
This guide shows a simple minimal example configuration with one MCP-server without the requirement of authentication.
The commands to set role based authentication is described below anyway.

GenericMCPServer does not provide any tools itself.
It is only a wrapper to easily provide tools in a generic way.
Any tool which should be available by the AI-client must be added/configured by you.

## Getting started

### Install MCP-server

Let us create a MCP-server with a simple tool to retrieve the current time as an example.

The image is available on docker-hub as [aniondev/genericmcpserver](https://hub.docker.com/r/aniondev/genericmcpserver).

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

Additionally, create a simple `Tools.json` which provides a tool to query the current timestamp for demonstration purposes.
For the beginning, the following minimal content is enough:

```json
{
    "mcp-servers": [
        {
            "url-namespace": "GeneralTools",
            "anonymous": true,
            "tools": [
                {
                    "tool-name": "get_current_utc_time",
                    "type": "shell",
                    "calls": "./GetCurrentUTCTime.sh",
                    "description": "Returns the current point in time in utc.",
                    "parameters": []
                }
            ]
        }
    ]
}
```

Because of `"url-namespace": "GeneralTools"` we can reach the MCP-server then under the URL `<server-URL>/GeneralTools`.

As a simple proof-of-concept-tool we use `GetCurrentUTCTime.sh` with the following content:

```bash
date -u +%Y-%m-%dT%H:%M:%SZ
```

Save this files in a folder structure like this:

```
docker-compose.yml
Tools/
    GetCurrentUTCTime.sh
    Tools.json
```

After `docker compose up -d` your MCP-server is technically available and can be configured.
Only on the first run the admin-token is printed in the container-output (not in the logs).
Use `docker logs genericmcpserver` for example to get the admin-token.
You can recognize this token by the token-prefix `gmcpadmin_`.
Save this token in your password-manager, you will need it later for administration via REST-API.

For this example MCP-server which requires no kind of authentication and authorization due to `"anonymous": true` it can be used directly by clients.
If this is enough for you then you can directly jump to the section [Configure MCP-server on client](#configure-mcp-server-on-client).

Now we can configure clients and roles for granular authorization control.
The next configuration steps to use role based authorization, let us first save the most important variables in env-variables:

```bash
export MCP_SERVER_URL="http://127.0.0.1:1222"
export MCP_ADMIN_TOKEN="gmcpadmin_xxx"
```

### Configure MCP-server

#### Create and administrate Clients

First of all:
For any MCP-server which does not have `"anonymous": true` you need a "client".
A client can then create an access-token for this own.
Only with an access-token a client can use non-anonymous MCP-servers hosted by GenericMVPServer.

All administration routes require `X-Admin-Token`.

Let us say, as an admin we now want to create a client called `myclient`:

```bash
export CLIENT_NAME="myclient"
```

##### Manage Clients

List clients, their roles, and the number of access tokens:

```bash
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients"
```

Create a client:

```bash
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME"
```

When you created a client, then the server returns a client-administration-token for the client.
Store the returned clientAdministrationToken immediately
With this client-administration-token a client can then organize his access-token.
Let us store it like this:

```bash
export CLIENT_ADMIN_TOKEN="gmcpclientadmin_xxx"
```

Replace a leaked or expired client-administration token:

```bash
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/AdministrationToken"
```

The following API-endpoints may also be important to know for future administration:

Delete a client, including its access tokens and role assignments:

```bash
curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME"
```

##### Manage roles

List all roles:

```bash
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles"
```

In this example the role is called `developer`, so let us store it:

```bash
export ROLE_NAME="developer"
```

Create the role:

```bash
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles/$ROLE_NAME"
```

Delete a role:

```bash
curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Roles/$ROLE_NAME"
```

Assign a role to a client:

```bash
curl --fail-with-body -X POST -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles/$ROLE_NAME"
```

Unassign a role from a client:

```bash
curl --fail-with-body -X DELETE -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles/$ROLE_NAME"
```

List all roles of a client:

```bash
curl --fail-with-body -H "X-Admin-Token: $MCP_ADMIN_TOKEN" "$MCP_SERVER_URL/API/v2/Admin/Clients/$CLIENT_NAME/Roles"
```

#### Manage Access-token of a client

Now with the client-admin-token the client can do the following things:

List access tokens:

```sh
curl -X GET $MCP_SERVER_URL/API/v2/ClientAdministration/AccessTokens -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

Create access token:

```sh
curl -X POST "$MCP_SERVER_URL/API/v2/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

This command returns a token which looks like `gmcp_xxx`.
This token can now be used to configure your AI-client using the configuration below.
The API-client can then use all MCP-servers which are authorized for the roles the client have.

Delete access token:

```sh
curl -X DELETE "$MCP_SERVER_URL/API/v2/ClientAdministration/AccessTokens/<hash>" -H "X-Client-Administration-Token: $CLIENT_ADMIN_TOKEN"
```

#### Configure MCP-server on client

The following configurations show examples for MCP-server-configuration for your AI-agents with 3 MCP-server:
The examples have multiple MCP-servers which fits the `Tools.json` from the [advanced example](./GenericMCPServer/Other/Reference/ReferenceContent/Examples/AdvancedDockerComposeFile/ReadMe.md):
One with anonymous access (`GeneralTools`) and two which require authentication (`Security` and `DeveloperTools`).
Dependent on your actual available `Tools.json` you must adapt this config to match your requirements.

##### Configure MCP-server for GitHub Copilot

###### GitHub Copilot for CLI

Create `~/.copilot/mcp-config.json` and add the MCP servers under `mcpServers`.
The content should look like this:

```json
{
	"mcpServers": {
		"general-tools": {
			"type": "http",
			"url": "https://mymcpserver.example.com/GeneralTools"
		},
		"security": {
			"type": "http",
			"url": "https://mymcpserver.example.com/Security",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		},
		"developer": {
			"type": "http",
			"url": "https://mymcpserver.example.com/DeveloperTools",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		}
	}
}
```

###### GitHub Copilot for VSCode

In VS Code, run `MCP: Open User Configuration` and add the following to the opened `mcp.json` file.
The content should look like this:

```json
{
	"servers": {
		"general-tools": {
			"type": "http",
			"url": "https://mymcpserver.example.com/GeneralTools"
		},
		"security": {
			"type": "http",
			"url": "https://mymcpserver.example.com/Security",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		},
		"developer": {
			"type": "http",
			"url": "https://mymcpserver.example.com/DeveloperTools",
			"headers": {
				"Authorization": "Bearer gmcp_xxx"
			}
		}
	}
}
```

##### Configure MCP-server for Claude

In your `~/.claude.json`-file, the `mcpServers`-settings should look like this:

```json
{
    //...
    "mcpServers": {
    "now327-GeneralTools": {
      "type": "http",
      "url": "https://mymcpserver.example.com/GeneralTools"
    },
    "now327-Security": {
      "type": "http",
      "url": "https://mymcpserver.example.com/Security",
      "headers": {
        "X-Authorization": "gmcp_xxx"
      }
    },
    "now327-DeveloperTools": {
      "type": "http",
      "url": "https://mymcpserver.example.com/DeveloperTools",
      "headers": {
        "X-Authorization": "gmcp_xxx"
      }
    }
  }
}
```

## About the tools

The tools provided in the examples are just for testing- and demonstration purposes.
In general the tools must be added and configured by the administrator of the GenericMCPServer.
GenericMCPServer only provides the overhead to make tools usable in a simple way for AI-agents.

## Security hints

GenericMCPServer runs tools.
There tools are just scripts you have to define.
Never use untrusted tools.
Never use tools which can somehow cause damage.
You must ensure that your script is not exploitable by an AI-agent by using unexpected values for parameter for example.

The reason is:
GenericMCPServer does not run the tools in a sandbox or in a container.
So, use only tools which are technically just not able to damage your system somehow.
If you want to use untrusted code as tool then you must put it in a sandbox of course and set a call-tool-in-sandbox-script as tool.
To do this and to ensure the untrusted code can not escape from the sandbox is task of the admin.
GenericMCPServer does not provide any sandbox-environment for tools yet.

## Reference

The general reference of the repository can be found [here](./Other/Reference/ReadMe.md).
The technical reference of the backend can be found [here](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).
The technical reference of the docker-image can be found [here](./GenericMCPServer/Other/Reference/ReferenceContent/index.md).

## Changelog

See the [Changelog-folder](./Other/Resources/Changelog).

## License

See [License.txt](./License.txt).
