# GenericMCPServerBackend

GenericMCPServerBackend is the backend for GenericMCPServer. It hosts MCP-servers which are reachable over HTTP.

It brings **no tool, no resource and no prompt of its own**. Which mcp-servers exist, which tools, resources and
prompts they offer, which values the argument of a prompt can have and who may use them is decided entirely by an
administrator, who declares it in a tool-definitions-file. Without such a file the server starts and offers nothing.

## How it is built

The MCP-protocol itself is not implemented in this codeunit. It is implemented by the MCP-SDK of Microsoft (the
packages `ModelContextProtocol`, `ModelContextProtocol.AspNetCore` and `ModelContextProtocol.Core`), which is
registered as a service and whose endpoints are mapped in `Program.cs`. Because the tools, the resources and the
prompts are not known at compile-time, they can not be methods which carry an attribute: one `ScriptTool`, one
`ScriptResource` respectively one `ScriptPrompt` is created per declaration. A completion is no primitive of the SDK -
the protocol has one request for all of them - so it is a handler and not one object per declaration: one
`ScriptCompletionHandler` per mcp-server answers which values an argument of one of its prompts can have.

They are not registered once for the whole process either. Which of them exist is decided per request by the
mcp-server the route of that request belongs to - that is what makes several mcp-servers in one process possible:

```csharp
services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
        options.ConfigureSessionOptions = (httpContext, serverOptions, cancellationToken) =>
        {
            MCPServerDefinition mcpServer = businessLogicService.GetMCPServerOfRoute(httpContext.Request.Path);
            serverOptions.ToolCollection = mcpServer == null ? null : toolsOfTheMCPServers[mcpServer.UrlNamespace];
            serverOptions.ResourceCollection = mcpServer != null && resourcesOfTheMCPServers.TryGetValue(mcpServer.UrlNamespace, out McpServerResourceCollection resources) ? resources : null;
            serverOptions.PromptCollection = mcpServer != null && promptsOfTheMCPServers.TryGetValue(mcpServer.UrlNamespace, out McpServerPrimitiveCollection<McpServerPrompt> prompts) ? prompts : null;
            serverOptions.Handlers.CompleteHandler = mcpServer != null && completionHandlersOfTheMCPServers.TryGetValue(mcpServer.UrlNamespace, out ScriptCompletionHandler completionHandler) ? completionHandler.HandleAsync : null;
            return Task.CompletedTask;
        };
    });
...
foreach (MCPServerDefinition mcpServer in businessLogicService.GetMCPServers())
{
    webApplication.MapMcp($"/{mcpServer.UrlNamespace}");
}
```

An mcp-server which declares no resource gets no collection at all and therefore does not claim the
resources-capability of the protocol, one which declares no prompt does not claim the prompts-capability and one which
declares no completion gets no handler and therefore does not claim the completions-capability, so a client of it
never asks for a list which is empty by definition.

The transport runs in the stateless mode, so no session-id has to be kept by the client.

## Endpoints

Every declared mcp-server has one endpoint of its own, over which all messages of the protocol with it are exchanged:

```
POST /<url-namespace>
```

A client of one mcp-server can neither see nor reach the tools, resources, prompts and completions of another one.
The same tool-name, the same resource-uri and the same prompt-name may therefore exist in two mcp-servers without
becoming ambiguous.

## How an mcp-server is declared

Which file declares the tools is itself part of the persisted configuration-file of the server (`ToolDefinitionsFile`,
relative to the configuration-folder unless given as an absolute path). It defaults to `./Tools.json` the first
time the configuration-file is generated; an administrator who wants the tools declared somewhere else changes the
value there afterwards. Without a file at the resolved location the server simply offers no tool.

```json
{
    "mcp-servers": [
        {
            "url-namespace": "Security",
            "authorized-roles": [ "security" ],
            "tools": [
                {
                    "tool-name": "get_cve_details",
                    "type": "python",
                    "calls": "GetCVEDetails.py",
                    "description": "Returns the details of a CVE from the national vulnerability database.",
                    "parameters": [ "cve-identifier" ]
                }
            ]
        }
    ]
}
```

| Field | Meaning |
| ----- | ------- |
| `url-namespace` | The address the mcp-server is reachable under: `<domain>/<url-namespace>`. It may consist of parts of letters, digits, underscores and hyphens which are separated by `/` (for example `teamA` or `teamA/mcpserver1`). |
| `authorized-roles` | The roles which may use this mcp-server. A client reaches it when it is in at least one of them. See [Access-tokens, clients and roles](#access-tokens-clients-and-roles). |
| `anonymous` | Optional, `false` by default. `true` makes the mcp-server usable without an access-token; it then needs no `authorized-roles`. |
| `tools` | The tools of this mcp-server. |
| `resources` | The resources of this mcp-server. |
| `prompts` | The prompts of this mcp-server. |

An mcp-server which is not anonymous and states no role could be used by nobody, which is a mistake in the declaration
and makes the server fail while starting. The same holds for a url-namespace which is declared twice, for a tool-name,
a resource-uri or a prompt-name which is declared twice within one mcp-server, for a tool, a resource or a prompt
without a type and for invalid json. A file which is configured but does not exist is an error as well - only *no* configured file means
"nothing is offered".

## How a tool is declared

These fields belong to every tool:

| Field | Meaning |
| ----- | ------- |
| `tool-name` | The name under which the tool is offered. This is what the client and the model behind it see. It may consist of letters, digits, underscores and hyphens. |
| `type` | What implements the tool: `python`, `shell` or `web-request`. It has to be declared, it is not guessed from the extension of a file. |
| `description` | What the tool does. The model reads this to decide whether the tool fits a task, so it should describe the answer and not the implementation. |

The remaining fields depend on the type. A field which does not belong to the declared type is an error and not
something which is ignored, because ignoring it would let an administrator believe in an effect which does not exist.

### Type `python`

| Field | Meaning |
| ----- | ------- |
| `calls` | The script which implements the tool. A relative path is resolved against the folder of the tool-definitions-file, and the script has to stay inside that folder. |
| `parameters` | The names of the parameters. Each of them is passed to the script as `--<name>=<value>`, so a name here has to be the name of an option the script accepts. |
| `requirements` | Optional. The packages the script needs, in the notation of a requirements.txt. |
| `credentials` | Optional. The accesses the script needs; they arrive as environment-variables. See [Credentials](#credentials). |

```json
{
    "tool-name": "get_repository_information",
    "type": "python",
    "calls": "GetRepositoryInformation.py",
    "description": "Returns the description and the amount of stars of a repository on github.",
    "requirements": [ "requests==2.32.3" ],
    "credentials": { "TOKEN": "GITHUB_TOKEN" },
    "parameters": [ "owner", "repository" ]
}
```

**Every python-tool runs in a virtual environment of its own**, named after the tool. That is what makes the packages
of two tools independent of each other: without it, two tools which need two versions of the same package could not
both work.

The environment is built when the tool is called for the first time and not when the server starts, so a server which
offers ten tools does not build ten environments before it answers anything. The first call of a tool whose packages
have to be downloaded therefore takes noticeably longer than the following ones.

The environment is built again as soon as the declared requirements differ from the ones it was built with. Otherwise
an administrator would change the requirements of a tool, nothing would happen, and the reason for that would be a
folder which nobody looks at. An environment whose creation failed is removed, so a half-built one can not be mistaken
for a working one.

The environments are located below the data-folder of the server (in the image `/Workspace/Data/VirtualEnvironments`).
The commandline-parameter `--InitialVirtualEnvironmentsFolder` puts them somewhere else, for example on a volume which
survives a restart of the container. They can not lie next to the scripts, because that folder is usually mounted
read-only.

### Type `shell`

| Field | Meaning |
| ----- | ------- |
| `calls` | The script which implements the tool, resolved like the script of a python-tool. |
| `parameters` | The names of the parameters. Each of them is passed to the script as `--<name>=<value>`. |

```json
{
    "tool-name": "get_current_utc_time",
    "type": "shell",
    "calls": "GetCurrentUTCTime.sh",
    "description": "Returns the current point in time in utc.",
    "parameters": [ ]
}
```

The script is run with `/bin/sh`, so **a shell-tool needs a linux-system**; on another system the tool answers with
exactly that reason instead of a failure which looks like a broken script. Running it with the shell (instead of
starting the file itself) also means that the file does not have to carry the permission to be executed, which a file
that is mounted into a container usually does not.

The arguments arrive as one token each, so a script reads them like this:

```sh
for argument in "$@"; do
  case "$argument" in
    --package=*) package="${argument#--package=}" ;;
  esac
done
```

A value still has to be checked before it is used - see [Security](#security).

### Type `web-request`

Such a tool implements no script at all: the client states which address it wants, and the declaration states which
addresses the tool allows.

| Field | Meaning |
| ----- | ------- |
| `allowed-urls` | The addresses the tool may request, as regular expressions. At least one rule has to be declared. |
| `credentials` | Optional. The accesses the tool needs; a header refers to them. See [Credentials](#credentials). |
| `headers` | Optional. The headers the request carries. A value may refer to a credential of this tool with `${<name>}`. |

```json
{
    "tool-name": "get_pypi_package_metadata",
    "type": "web-request",
    "description": "Returns the metadata which pypi publishes about a python-package.",
    "allowed-urls": [ "pypi\\.org/pypi/.*" ]
}
```

Such a tool declares no `parameters`: it takes exactly the one parameter which the mechanism defines, namely `url`,
which is the address the client wants.

Rules for the addresses:

- **The protocol is not part of an address.** It is always `https`, and it can not be changed. An address which states
  another protocol is rejected instead of being corrected silently.
- **A rule has to match the whole address.** Every rule is anchored at both ends, so `example\.com/.*` allows
  everything below `example.com` while `example\.com` allows exactly that one address. Without the anchoring, an
  address of a foreign host which contains an allowed one somewhere (`other.com/?x=example.com/`) would satisfy the
  rule.
- **What no rule allows is denied.** A tool without a matching rule requests nothing, which is why a declaration
  without a rule is an error: such a tool could never do anything.
- **`".*"` allows everything.** It is possible and it is not recommended: such a tool can reach every address which the
  server can reach, including the ones inside its own network.
- **The address which the rules see is the one which is requested.** It is taken from the parsed address (host, port
  and path), so an address which only looks like an allowed one does not become one. An address with credentials is
  rejected, because the rules would not see them while the request would send them.
- **Redirects are not followed.** The answer of a redirect is handed over as what it is, because the address which the
  rules allowed would otherwise only be the first one of a chain.

The answer of the tool is a json-document with the status-code and the body of the answer:

```json
{"status-code":200,"body":"..."}
```

## How a resource is declared

A resource is content which a client reads by a uri - a document, a list, the state of something. It is declared in
the `resources` of the mcp-server it belongs to and is implemented exactly like a tool, by a python-script, a
shell-script or a web-request.

The difference to a tool is what decides the answer: **a resource takes no parameter**. Its uri is the whole request,
so nothing which a client sends influences what it receives.

| Field | Meaning |
| ----- | ------- |
| `uri` | The uri under which the resource is read. It is what a client sees in the list and what it sends to read the content, so it identifies the resource within its mcp-server. It has to be an absolute uri; which scheme it uses is free (`https://…`, `file:///…`, an own one like `example://…`) - except for the type `web-request`, where it is the address which is requested and therefore states `https`. |
| `resource-name` | The name under which the resource is offered. It is what a client shows a reader, while the uri is what it reads the content by. It may consist of letters, digits, underscores and hyphens. |
| `type` | What implements the resource: `python`, `shell` or `web-request`, declared exactly like the type of a tool. |
| `description` | What the resource contains. |
| `mime-type` | The type of the content, for example `text/markdown`. It is declared and not guessed from the content or from the uri, because it is what tells a client what it received. |
| `calls` | For `python` and `shell`: the script which implements the resource, resolved like the script of a tool. |
| `requirements` | Optional, only for `python`: the packages the script needs. |
| `credentials` | Optional, for `python` and `web-request`: the accesses which are needed. See [Credentials](#credentials). |
| `headers` | Optional, only for `web-request`: the headers the request carries. |

```json
{
    "mcp-servers": [
        {
            "url-namespace": "Security",
            "authorized-roles": [ "security" ],
            "resources": [
                {
                    "uri": "handbook://security/incident-response",
                    "resource-name": "incident_response_handbook",
                    "type": "python",
                    "calls": "ReadHandbook.py",
                    "description": "The handbook which states what to do when an incident was reported.",
                    "mime-type": "text/markdown"
                }
            ]
        }
    ]
}
```

**The script receives the uri of the resource it is read for**, as `--uri=<uri>` and in the same way in which a tool
receives its values (one token, no shell involved). That is what lets one script implement several resources: the
script decides by the uri what it answers, and an administrator declares the same script several times with different
uris. A script which implements a resource therefore has to accept that option, also when it implements exactly one.

```python
parser = argparse.ArgumentParser()
parser.add_argument('--uri', required=True)
```

A resource of the type `python` runs in a virtual environment of its own, exactly like a python-tool. The name of that
environment carries the kind of the declaration (`resource.<resource-name>`), so a resource and a tool of the same
name do not end up in the same folder.

A resource of the type `web-request` is requested from its own uri and declares no `allowed-urls`: there is no address
which a client could ask for, so there are no rules which could allow or deny one. The answer is the body of the
request and not the json-document with the status-code which a tool of that type answers with - a client reads a
resource to receive a document, and a document which is wrapped in something else is no document anymore. An answer
whose status-code is no success is therefore a failed read and not a content, so that what a client receives is never
the error-page of a foreign service.

```json
{
    "uri": "https://example.com/handbook.md",
    "resource-name": "handbook",
    "type": "web-request",
    "description": "The handbook as it is published.",
    "mime-type": "text/markdown"
}
```

What a resource does **not** offer:

- **No subscriptions.** A client is not notified when the content of a resource changed; it reads the resource again.
- **No uri-templates.** A resource is declared with the uri it has. A declaration whose uri contains `{` or `}` is a
  mistake and makes the server fail while starting, instead of offering something which nothing answers.
- **No binary content.** What a script writes is handed over as text.

## How a prompt is declared

A prompt is a text which a client asks for by its name and hands to the model behind it - an instruction, a
question, a checklist. It is declared in the `prompts` of the mcp-server it belongs to and is implemented like a
tool, by a python-script or a shell-script which writes that text.

The difference to a tool is what a client does with the answer: it hands it to its model instead of reading it as the
result of something which was done. A prompt therefore takes the values of its arguments from the client, exactly
like a tool takes the values of its parameters.

| Field | Meaning |
| ----- | ------- |
| `prompt-name` | The name under which the prompt is offered. It is what a client asks for, so it identifies the prompt within its mcp-server. It may consist of letters, digits, underscores and hyphens. |
| `type` | What implements the prompt: `python` or `shell`, declared exactly like the type of a tool. |
| `description` | What the prompt is for. A client shows this to whoever chooses a prompt, so it should describe what the text asks for. |
| `calls` | The script which writes the text of the prompt, resolved like the script of a tool. |
| `parameters` | Optional. The names of the arguments of the prompt. Each of them is passed to the script as `--<name>=<value>`, exactly like the parameter of a tool, and each of them is offered to the client as a required one. |
| `requirements` | Optional, only for `python`: the packages the script needs. |
| `credentials` | Optional, only for `python`: the accesses which are needed. See [Credentials](#credentials). |
| `completions` | Optional. What provides the possible values of the arguments of this prompt, keyed by the name of the argument. See [How a completion is declared](#how-a-completion-is-declared). |

```json
{
    "mcp-servers": [
        {
            "url-namespace": "Security",
            "authorized-roles": [ "security" ],
            "prompts": [
                {
                    "prompt-name": "review_a_finding",
                    "type": "python",
                    "calls": "BuildReviewPrompt.py",
                    "description": "Asks for a review of a security-finding in a stated component.",
                    "parameters": [ "component" ]
                }
            ]
        }
    ]
}
```

The script writes the text of the prompt to its standard-output, and it receives the values of the declared arguments
in the same way in which a tool receives its values (one token per value, no shell involved):

```python
parser = argparse.ArgumentParser()
parser.add_argument('--component', required=True)
arguments = parser.parse_args()
sys.stdout.write('Review the finding in ' + arguments.component + ' and state whether it is reachable.')
```

What the script wrote becomes one message of the role `user` of the answer of `prompts/get`, together with the
declared description. A prompt of this server is a text and not a conversation of several turns: a declaration states
what is asked, and what an assistant answered is not part of it.

A prompt of the type `python` runs in a virtual environment of its own, exactly like a python-tool. The name of that
environment carries the kind of the declaration (`prompt.<prompt-name>`), so a prompt and a tool of the same name do
not end up in the same folder.

What a prompt does **not** offer:

- **No type `web-request`.** Such a declaration takes the address it requests either from the client (a tool, whose
  parameter `url` states it) or from its own uri (a resource, whose uri is it), and a prompt has neither: its
  arguments are the values which are filled into its text. A prompt which states that type is a mistake and makes the
  server fail while starting, instead of behaving like one of the two without saying which.
- **No optional argument and no description per argument.** Every declared argument is offered as a required one,
  exactly like the parameter of a tool.
- **No notification when the list changed.** The declaration is read once, while the server starts, so a new prompt
  needs a restart of the server and not a notification to the clients.

## How a completion is declared

A completion states which values one argument of one prompt can have (`completion/complete` of the protocol). A client
asks for them while somebody fills in that argument and offers what it receives; the model behind it never sees them,
so a completion is a help for whoever chooses the values and not a further answer of the server.

It is declared inside the prompt whose argument it completes, under the name of that argument, because it belongs to
that one argument and to nothing else. An argument has one completion or none: an argument without one is simply one
whose values a client states without being offered any.

| Field | Meaning |
| ----- | ------- |
| `type` | What implements the completion: `python` or `shell`, declared exactly like the type of a tool. |
| `calls` | The script which writes the possible values, resolved like the script of a tool. |
| `requirements` | Optional, only for `python`: the packages the script needs. |
| `credentials` | Optional, only for `python`: the accesses which are needed. See [Credentials](#credentials). |

The key under which the completion stands has to be one of the `parameters` of its prompt and may consist of letters,
digits, underscores and hyphens; a completion for an argument which the prompt does not take would be a script nobody
ever calls and makes the server fail while starting.

```json
{
    "prompt-name": "review_a_finding",
    "type": "python",
    "calls": "BuildReviewPrompt.py",
    "description": "Asks for a review of a security-finding in a stated component.",
    "parameters": [ "component" ],
    "completions": {
        "component": {
            "type": "python",
            "calls": "ListComponents.py"
        }
    }
}
```

The script receives which argument it is asked about and which value was entered so far - both in the same way in
which a tool receives its values (one token per value, no shell involved) - and writes one value per line to its
standard-output. It is told the argument although it is declared for exactly one, which is what lets one script
complete the arguments of several prompts:

```python
parser = argparse.ArgumentParser()
parser.add_argument('--argument', required=True)
parser.add_argument('--value', required=True)
arguments = parser.parse_args()
for component in ['backend', 'frontend', 'database']:
    if component.startswith(arguments.value):
        sys.stdout.write(component + '\n')
```

`--value` is empty when nothing was entered yet, which is the usual case for the first request. Empty lines of the
answer are dropped, because a trailing newline is what every usual way of writing a line leaves behind.

At most 100 values are handed to the client, which is the limit the protocol states; how many there are in total is
handed over with them, so that whoever asked can tell "these are all of them" from "these are the first of them".

A completion of the type `python` runs in a virtual environment of its own, exactly like a python-tool. The name of
that environment is `completion.<prompt-name>.<argument-name>`, so a completion and a tool, a resource or a prompt of
the same name do not end up in the same folder.

What a completion does **not** offer:

- **No type `web-request`.** Such a declaration takes the address it requests either from the client (a tool) or from
  its own uri (a resource), and a completion has neither: it is asked which values an argument of a prompt can have.
  A completion which states that type makes the server fail while starting.
- **No description.** A client asks for the possible values of an argument it already chose and never chooses a
  completion itself, so a declared `description` is a text nobody would read and is a mistake.
- **No completion of a resource.** The other reference the protocol knows is a uri-template, and this server declares
  every resource with the uri it has and derives none from a template - so such a request names nothing which could be
  completed and is answered with no value.
- **No completion of the parameter of a tool.** The protocol knows completions for prompts and for uri-templates only;
  what a tool accepts is stated by the schema of its parameters.

## Access-tokens, clients and roles

Who may use an mcp-server is decided by three things which are deliberately kept apart:

| Where | What it states |
| ----- | -------------- |
| `ClientAdministrationTokens.csv` | Which clients exist and with which token each of them administrates itself. |
| `Roles.csv` and `RoleAssignments.csv` | Which roles exist and which client is in which of them. |
| The declaration of an mcp-server | Which roles may use that mcp-server (`authorized-roles`). |

A client reaches an mcp-server when its access-token is valid and it is in at least one of the roles which that
mcp-server declares. What a role may do and who is in that role are therefore two decisions in two places, which are
usually made by two different people.

All of these files live in the configuration-folder of the server (movable with `--InitialAdministrationFolder`), they
are created when they do not exist yet, and they are read for every request - so every change works immediately and
without a restart.

### The admin-token

The server generates a token for its administrator the first time it starts without one and writes it to its log,
while the log-file is switched off - so it appears on the console of the starting server and in no file. Only its hash
is stored (`AdminToken.csv` in the configuration-folder), exactly like the token of a client: whoever gets the files of
the server into their hands holds no access, and a token which was missed can not be looked up but only replaced. To
replace it, delete `AdminToken.csv` and start the server again.

The token is deliberately no token of a client: whoever administrates the server is not one of the clients which use
it.

### What an administrator does

Every one of these routes needs the header `X-Admin-Token` with that token.

| Route | What it does |
| ----- | ------------ |
| `GET /API/v1/Admin/Clients` | The clients with their roles and the amount of their access-tokens. |
| `POST /API/v1/Admin/Clients/<client>` | Creates a client and answers with its client-administration-token. |
| `POST /API/v1/Admin/Clients/<client>/AdministrationToken` | Creates a new client-administration-token for that client and answers with it. The previous one stops working at once; the access-tokens of the client stay as they are. |
| `DELETE /API/v1/Admin/Clients/<client>` | Removes the client together with its access-tokens and its roles. |
| `GET /API/v1/Admin/Roles` | The roles which exist. |
| `POST /API/v1/Admin/Roles/<role>` | Creates a role. |
| `DELETE /API/v1/Admin/Roles/<role>` | Removes the role and every assignment of it. |
| `POST /API/v1/Admin/Clients/<client>/Roles/<role>` | Puts the client into the role. |
| `DELETE /API/v1/Admin/Clients/<client>/Roles/<role>` | Removes the client from the role. |

```
curl -X POST http://127.0.0.1:8080/API/v1/Admin/Clients/team-a -H "X-Admin-Token: <admin-token>"
```

The answer contains the **client-administration-token**, and only this answer does: only its hash is stored. It is
handed to whoever runs that client.

### What a client does

Every one of these routes needs the header `X-Client-Administration-Token`. Which client is calling follows from that
token, so a client can only ever see and change its own access-tokens - it can not even name another one.

| Route | What it does |
| ----- | ------------ |
| `GET /API/v1/ClientAdministration/AccessTokens` | The own access-tokens, with their hash and their end. |
| `POST /API/v1/ClientAdministration/AccessTokens?validForDays=90` | Creates an access-token and answers with it. |
| `DELETE /API/v1/ClientAdministration/AccessTokens/<hash>` | Removes one of the own access-tokens. |

```
curl -X POST "http://127.0.0.1:8080/API/v1/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: <client-administration-token>"
```

- **Only the hash of a token is stored.** Every token exists in a readable form exactly once, namely in the answer
  which creates it. A token which somebody lost can therefore not be looked up but only replaced.
- **Every access-token ends.** `validForDays` states when; a token without an end does not exist.
- **A client can hold several valid access-tokens at the same time**, which is what makes an exchange possible without
  an interruption: the new token is created, whatever uses it is switched over, and only then the old one is removed.

The client sends its access-token to an mcp-server as a header:

```
Authorization: Bearer <access-token>
```

An mcp-server which is declared with `"anonymous": true` needs none of this: it is reachable by everybody. The other
mcp-servers of the same process stay behind their tokens.

Everything which is not allowed is answered with the status-code 401 - whether the token is missing, unknown, expired
or belongs to a client without a fitting role is nothing a caller learns from the answer.

## Credentials

A tool which uses a foreign service usually needs an access to it (a token, an api-key, a login). Such an access
belongs to the administrator who runs the server and never to the client, so it is not a parameter: the operator sets
it as an environment-variable of the server, and the declaration states which tool gets which one.

```json
"credentials": { "TOKEN": "GITHUB_TOKEN" }
```

The name on the left is the one the tool sees, the name on the right is the environment-variable of the server the
value is read from. They are stated separately so that the same script can be declared twice and use a different access
each time. A python-script reads its access as an environment-variable:

```python
token = os.environ["TOKEN"]
```

A tool of the type `web-request` uses its accesses in its headers, where `${<name>}` refers to one of them:

```json
{
    "tool-name": "get_private_repository",
    "type": "web-request",
    "description": "Returns what the github-api answers about a repository.",
    "allowed-urls": [ "api\\.github\\.com/repos/.*" ],
    "credentials": { "TOKEN": "GITHUB_TOKEN" },
    "headers": {
        "Authorization": "Bearer ${TOKEN}",
        "Accept": "application/vnd.github+json"
    }
}
```

A tool of the type `shell` receives no credentials. A resource and a prompt are assigned their accesses in exactly the
same way and with the same rules, because a credential belongs to the declaration and not to what a client does with
the answer.

### How the value reaches the container

Either directly as an environment-variable, or - better - as a docker-secret. For a credential which is read from
`GITHUB_TOKEN`, the variable `GITHUB_TOKEN_FILE` states a file whose content is the value; the value itself then does
not have to stand in the compose-file. A trailing line-break of that file is not part of the value.

```yaml
services:
  genericmcpserver:
    environment:
      GITHUB_TOKEN_FILE: '/run/secrets/github_token'
    secrets:
      - github_token
secrets:
  github_token:
    file: ./github_token
```

### What the server guarantees - and what it does not

- **A tool receives exactly its own credentials.** A script inherits the environment of the server, so the variables
  which credentials are read from are removed from it before the declared ones are added under their own names. Every
  other variable (`PATH`, `HOME`, …) stays untouched.
- **A missing credential keeps the server from starting**, with the name of the credential in the message. A forgotten
  secret is therefore visible while deploying and not when a model happens to call the affected tool.
- **A value never reaches a client through a message.** The message of a failed tool-call is handed to the client, so
  every known value is replaced by `***` in it beforehand. This is a safety-net and not a permission: a script should
  never write its access into its output, because the answer of a successful call is not searched.
- **This is no sandbox.** All tools run as the same user in the same container. The separation is about what is handed
  over, not about what a script could reach if it went looking for it.

## Security

The values of the parameters are provided by the client and are therefore untrusted. Two mechanisms handle that:

**A value can not become a command.** No shell is involved when a script is started: `UseShellExecute` is false and
every argument is added as its own entry of `ProcessStartInfo.ArgumentList`, so the operating-system hands each of
them to the script as exactly one argument. This is stronger than escaping would be, because there is no quoting which
could be escaped wrongly, forgotten or applied twice. Every value is additionally passed as one single token in the
form `--name=value`, so a value which looks like an option (for example `--help`) can not be read as one. This holds
for a shell-tool as well: the shell runs the script, it never sees the values as part of a command-line.

**A value has to have the format the script expects.** Each script checks each of its arguments against a regular
expression which is anchored at both ends and rejects everything which does not match completely. A value which is
rejected makes the script end with an exit-code which is not 0; the server turns that into a failed tool-call whose
message contains what the script wrote, so the model learns what was wrong and can correct itself.

For a tool of the type `web-request` the same question is answered by the rules of its declaration instead of by a
script, because there is no script which could answer it.

The declaration itself comes from an administrator and is therefore trusted, but the script it names still has to be
located inside the folder of the tool-definitions-file, so that a mistake there can not start an unrelated program.
**What a declared tool does is the responsibility of the administrator who declared it** - see the
[ReadMe of the image](../../../../GenericMCPServer/ReadMe.md).

## What a failed tool-call, a failed read, a failed prompt and a failed completion look like

A tool-call which did not work - a script which ended with an exit-code which is not 0, a script which ran into the
timeout of 60 seconds, an address which no rule allows, a virtual environment which could not be built - is answered in
two ways at once:

- as a result of the protocol with `isError` and the message of the failure, so that the model learns what was wrong
  and can correct itself,
- and with the **http-status-code 502** for the whole answer, because the failure comes from the program behind this
  server and this server is the gateway to it.

The body of such an answer is the normal answer of the protocol; only its status-code differs.

A **read of a resource**, a **prompt which could not be built** and a **completion which did not work** are answered
differently, namely as an error of the protocol (a json-rpc-error with the message of the failure) and with the
status-code of a normal answer. A result of the protocol which carries an error-flag does not exist for any of them: a
client which asked for a document, for a text or for the possible values of an argument must not receive the reason of
a failure as if it were the content of that document, that text respectively one of those values, and an error of the
protocol is unmistakable on its own, while a failed tool-call is a successful answer of the protocol and needs the
status-code to be visible as a failure. The values of the credentials are removed from those messages as well.

## Example

`GenericMCPServer/Other/Examples/MinimalDockerComposeFile/Tools` contains the scripts and the declaration which turns
them into three mcp-servers: `GeneralTools` (anonymous, the current point in time), `Security` (the role `security`,
the details of a CVE) and `DeveloperTools` (the roles `developer` and `security`, the available versions of a maven-,
nuget-, pypi- and npm-package, the metadata of a pypi-package and a research-tool for the usual sources of a
developer). None of these tools is known to this codeunit; they are an example of what can be declared.

## How to use it in a MCP-client

```json
{
  "mcpServers": {
    "developertools": {
      "type": "http",
      "url": "http://127.0.0.1:8080/DeveloperTools",
      "headers": { "Authorization": "Bearer <access-token>" }
    }
  }
}
```

With the Claude-CLI:

```
claude mcp add --transport http --scope user developertools http://127.0.0.1:8080/DeveloperTools --header "Authorization: Bearer <access-token>"
```

## How to test it without a client

The answer arrives as an event-stream, so it is the line which starts with `data: `. The accept-header has to name both
content-types.

Which tools an mcp-server offers (this one is anonymous, so it needs no token):

```
curl -X POST http://127.0.0.1:8080/GeneralTools -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

Calling a **shell**-tool (this one takes no argument):

```
curl -X POST http://127.0.0.1:8080/GeneralTools -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"get_current_utc_time\",\"arguments\":{}}}"
```

Calling a **python**-tool of an mcp-server which needs a token:

```
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"get_cve_details\",\"arguments\":{\"cve-identifier\":\"CVE-2021-44228\"}}}"
```

Calling a **web-request**-tool - the address is the argument, and it carries no protocol:

```
curl -X POST http://127.0.0.1:8080/DeveloperTools -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"research\",\"arguments\":{\"url\":\"stackoverflow.com/questions/11227809\"}}}"
```

An answer looks like this (the result of the tool is the text of its content):

```
data: {"result":{"content":[{"type":"text","text":"2026-08-20T09:12:44Z"}],"isError":false},"id":2,"jsonrpc":"2.0"}
```

A call which failed answers with the status-code 502 and states the reason:

```
data: {"result":{"content":[{"type":"text","text":"The tool \"research\" does not allow a request to \"example.com/\". A tool allows exactly the addresses which the rules of its declaration match."}],"isError":true},"id":4,"jsonrpc":"2.0"}
```

A request which may not reach the mcp-server at all answers with the status-code 401 and no result.

Which **resources** an mcp-server offers, and reading one of them by its uri:

```
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"resources/list\"}"
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"resources/read\",\"params\":{\"uri\":\"handbook://security/incident-response\"}}"
```

The answer of a read contains the content together with the uri it was read by and the declared type of the content:

```
data: {"result":{"contents":[{"uri":"handbook://security/incident-response","mimeType":"text/markdown","text":"# Incident-response\n..."}]},"id":6,"jsonrpc":"2.0"}
```

A read which did not work answers with an error instead of a result:

```
data: {"error":{"code":-32603,"message":"The resource \"incident_response_handbook\" failed (exit-code 1): the document does not exist"},"id":6,"jsonrpc":"2.0"}
```

Which **prompts** an mcp-server offers, and asking for one of them by its name:

```
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"prompts/list\"}"
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"prompts/get\",\"params\":{\"name\":\"review_a_finding\",\"arguments\":{\"component\":\"the login-service\"}}}"
```

The answer contains the text which the script wrote, as one message of the role `user`:

```
data: {"result":{"description":"Asks for a review of a security-finding in a stated component.","messages":[{"role":"user","content":{"type":"text","text":"Review the finding in the login-service and state whether it is reachable."}}]},"id":8,"jsonrpc":"2.0"}
```

A prompt which could not be built answers with an error instead of a result, exactly like a failed read:

```
data: {"error":{"code":-32603,"message":"The prompt \"review_a_finding\" failed (exit-code 1): the component is unknown"},"id":8,"jsonrpc":"2.0"}
```

Which values an **argument of a prompt** can have, for the value which was entered so far:

```
curl -X POST http://127.0.0.1:8080/Security -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -H "Authorization: Bearer <access-token>" -d "{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"completion/complete\",\"params\":{\"ref\":{\"type\":\"ref/prompt\",\"name\":\"review_a_finding\"},\"argument\":{\"name\":\"component\",\"value\":\"b\"}}}"
```

The answer contains the values which the script wrote, together with how many of them there are:

```
data: {"result":{"completion":{"values":["backend"],"total":1,"hasMore":false}},"id":9,"jsonrpc":"2.0"}
```

An argument which declares no completion is answered with no value at all and not with an error, because "which values
are possible" is a question a client may ask about every argument.

Every script can also be called on its own, which is the fastest way to check what a tool, a resource, a prompt or a
completion answers:

```
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
python ReadHandbook.py --uri=handbook://security/incident-response
python BuildReviewPrompt.py --component="the login-service"
python ListComponents.py --argument=component --value=b
```

## How to add a tool, a resource, a prompt or a completion

Put the tool into the `tools` of the mcp-server it belongs to. For a tool with a script: put the script into the folder
of the tool-definitions-file, let it check every argument against the format it must have, and state its type and its
parameters. For a tool of the type `web-request`: state the addresses it may request. Nothing in this codeunit has to
be changed and nothing has to be recompiled - the server picks the tool up when it is restarted.

Put a resource into the `resources` of that mcp-server, with the uri it is read by, its name, its type and the
`mime-type` of its content. A script of a resource takes no argument of the client but receives the uri it was read
for, so it has to accept `--uri`.

Put a prompt into the `prompts` of that mcp-server, with its name, its type, its description and the arguments it
takes. A script of a prompt receives those arguments like a tool does and writes the text of the prompt; a prompt is
never of the type `web-request`.

Put a completion into the `completions` of the prompt whose argument it completes, under the name of that argument. A
script of a completion is told which argument it is asked about and which value was entered so far, so it has to
accept `--argument` and `--value`, and it writes one possible value per line.

To add a whole mcp-server, add an entry to `mcp-servers` with its `url-namespace` and either its `authorized-roles` or
`"anonymous": true`.

## What is needed to work on this codeunit

The requirements, the recommended IDE and how the server is started locally are stated in the
[hints](./Hints.md).
