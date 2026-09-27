# Reference of GenericMCPServer

This is the reference of the repository as a whole: how the parts of the product fit together and which decisions
behind it are worth knowing. What the product is and how it is installed, configured and reached by a client is
described in the [ReadMe of the repository](../../ReadMe.md); the reference of a single codeunit is located in that
codeunit.

## Parts of the product

| Codeunit | Content |
| -------- | ------- |
| [GenericMCPServerBackend](../../GenericMCPServerBackend/ReadMe.md) | The server itself: it reads the declarations, offers them as mcp-servers with their tools, resources, prompts and completions and runs what is behind them. |
| [GenericMCPServer](../../GenericMCPServer/ReadMe.md) | The container-image which packages the backend together with a python-interpreter, because a declared tool is usually a python-script. |

The MCP-protocol itself is not implemented here. It is implemented by the MCP-SDK of Microsoft; the backend provides
one object per declaration - a tool, a resource respectively a prompt -, one handler for the possible values of the
arguments of the prompts, one handler for a cancellation and one endpoint per mcp-server.

## Features

| Feature | Description |
| ------- | ----------- |
| Tools are declared, not built in | Which tools exist is decided by the tool-definitions-file of an administrator. A tool-name in the sourcecode of this product is a defect. |
| Three kinds of tools | `python` (a script in a virtual environment of its own), `shell` (a script run with `/bin/sh`, linux only) and `web-request` (no script: an address which the declaration allows). |
| Requirements per python-tool | The packages a python-tool needs are installed into an environment which belongs to that tool alone, built when it is first called and rebuilt when the declared requirements change. |
| Whitelist per web-request-tool | Every rule is matched against the whole address, the protocol is always https, redirects are not followed and what no rule allows is denied. |
| Credentials per tool | An access of the operator (from an environment-variable or a docker-secret) is assigned to a tool by the declaration; no tool receives the accesses of another one, and values are removed from failure-messages. |
| Several mcp-servers in one process | Every declared mcp-server has its own tools and its own address `<domain>/<url-namespace>`. A client of one of them can neither see nor reach the tools of another one, and the same tool-name may exist in two of them. |
| Clients with several access-tokens | A client can hold several valid access-tokens at the same time, every token ends at a stated point in time, and only the hash is stored. |
| Roles | A client is in none, one or several roles; an mcp-server declares which roles may use it. Who is in a role and what a role may do are two decisions in two files. |
| Anonymous mcp-servers | An mcp-server can be declared as one which everybody may use; it then needs no token, while the others of the same process stay behind theirs. |
| Resources and prompts are declared like tools | Next to its tools an mcp-server offers `resources` (content which a client reads by a uri, which takes no parameter and whose script receives that uri) and `prompts` (a text which a client asks for by its name and hands to its model, whose script receives the declared arguments like a tool). Both are declared in the same file and implemented in the same ways as a tool, except that a prompt is never a `web-request`. |
| The values which an argument of a prompt can have | An argument of a prompt may declare what provides its possible values: a script which is told the argument and the value which was entered so far and writes one value per line. An mcp-server whose prompts declare none of them does not offer the completions of the protocol at all. |
| Structured answers of a tool | A tool may declare the shape of its answer as an `output-schema`. That schema is offered to the client with the tool, and what the script wrote is handed over as the structured content of the result next to the text. An answer which is no json-document or does not fit the schema is a failed tool-call. |
| A running tool-call can be cancelled | A `notifications/cancelled` of the client ends the script behind that call together with everything it started, respectively aborts its web-request. The call and the cancellation are two http-requests, which is why the running calls are kept in one registry of the process; the script needs no capability of its own for it. |

## Where the declaration has to be

Which file declares the mcp-servers and their tools is part of the persisted configuration of the server
(`ToolDefinitionsFile`). That value is the **whole path of that file** including its name, so an administrator reads in
the configuration-file where the declaration is looked for and moves it by changing that one value. The
configuration-file is generated with the path of `Tools.json` in the **tools-folder**:

| Environment | Tools-folder | Default value of `ToolDefinitionsFile` |
| ----------- | ------------ | -------------------------------------- |
| Local run | `GenericMCPServerBackend/Other/Workspace/Tools` | `GenericMCPServerBackend/Other/Workspace/Tools/Tools.json` |
| Container | `/Workspace/Tools` | `/Workspace/Tools/Tools.json` |

Which path the configuration-file is generated with can be stated from outside with the environment-variable
`InitialToolsDefinitionFilePath`, again as the whole path of the file. It decides that initial value and nothing else:
from the second start on the configuration-file is what the server reads.

The declaration and every script it names belong in the tools-folder or in a folder below it. Every script a tool, a
resource or a prompt `calls` is resolved relative to the declaration and has to stay inside its folder; a declaration
whose scripts lie somewhere else fails when the tool is called. Because the configured value names a file and not a
folder, a container can receive the declaration as a single mounted file
(`./Tools.json:/Workspace/Tools/Tools.json`) instead of as a whole folder - which fits a declaration whose tools are
web-requests, because those name no script.

The tools-folder holds what an administrator writes; the configuration-folder (`/Workspace/Configuration`) holds what
the server writes: its configuration-file and the files of its clients, roles and access-tokens. They are two folders
next to each other for exactly that reason - a declaration is handed over without the state of the server being handed
over with it.

Three things about the declaration matter while working with it:

- **The server reads it only once, at startup.** Unlike the csv-files of the clients, roles and access-tokens (which
  are read for every request), a changed declaration needs a restart to take effect.
- **A file which does not exist yet is no error.** The server creates it as an empty declaration (`{}`) and starts
  while offering nothing, because a freshly set up server which nobody pointed at a declaration yet is an expected
  state. The folder of that file has to exist though: a folder which is not there makes the server fail while starting
  instead of being created, because such a path is a mistake and not a place.
- **A mistake in the file is an error** (an mcp-server without a url-namespace, an mcp-server which neither is
  anonymous nor names a role, a tool without a type, a prompt which is declared as a web-request, a name which is used
  twice, invalid json, ...) and makes the server fail while starting, instead of when somebody happens to call the
  affected tool.

The format of the file and the meaning of each field is documented in the
[reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Who may use an mcp-server

Three things decide it, and they are deliberately kept apart: `ClientAdministrationTokens.csv` states which clients
exist, `Roles.csv` and `RoleAssignments.csv` state which roles exist and who is in them, and the declaration of an
mcp-server states which roles may use it. All of these files live in the configuration-folder, are created when they
do not exist yet and are read for every request - so a new token or a changed role works immediately and without a
restart, unlike a changed declaration.

Every token is shown exactly once, because only its hash is stored. The routes with which they are administrated are
described in the [ReadMe of the repository](../../ReadMe.md).

## What a failed tool-call looks like

A tool-call which did not work is answered in two ways at once: as a result of the protocol which carries the message
of the failure (so that the model learns what was wrong), and with the http-status-code 502 for the whole answer,
because the failure comes from the program behind the server and the server is the gateway to it.

A read of a resource, a prompt and a completion which did not work are answered as an error of the protocol instead,
with the status-code of a normal answer: a client which asked for a document, for a text or for the possible values of
an argument must not receive the reason of a failure as if it were that document, that text respectively one of those
values.

A call which the client itself cancelled is neither of those two. It is answered with exactly that reason and with the
status-code of a normal answer, because nothing failed. A request which may not reach an mcp-server at all is answered
with 401, whatever the reason was.

## Why a declared tool can be trusted with untrusted values

The values of the parameters come from the client, so they are untrusted. The product does not try to make them
harmless by escaping them; it makes escaping unnecessary:

- No shell is involved when a script is started, and every argument is handed to the script as exactly one argument by
  the operating-system.
- Every value is passed as one token in the form `--name=value`, so a value which looks like an option can not become
  one.
- Whether a value has the format it must have is checked by the script itself, next to the code which depends on it,
  and a rejected value becomes a failed tool-call whose message says what was wrong.

What a declared tool *does*, on the other hand, is not checked by this product and can not be: that is the
responsibility of the administrator who declared it.

## Protocol

GenericMCPServerBackend implements the tools-, the resources-, the prompts- and the completions-primitive of the
MCP-protocol - the `initialize`-handshake, `tools/list`, `tools/call`, `resources/list`, `resources/read`,
`prompts/list`, `prompts/get` and `completion/complete` - through the official C#-SDK (`ModelContextProtocol`, pinned
to version `2.2.0`). This product contributes no protocol-code of its own. An mcp-server which declares no resource
does not claim the resources-capability at all, one which declares no prompt does not claim the prompts-capability and
one which declares no completion does not claim the completions-capability, so a client never asks for a list which is
empty by definition.

The transport is the current **Streamable HTTP** transport, run in its stateless mode: every request is answered on
its own, no session-id has to be kept by the client, and no separate long-lived connection is opened first - unlike
the older, meanwhile deprecated HTTP+SSE-transport of the protocol-revision `2024-11-05`. Which protocol-revision a
client uses is negotiated during `initialize`; the pinned SDK-version understands `2024-11-05`, `2025-03-26`,
`2025-06-18`, `2025-11-25` and `2026-07-28`.

There is no `stdio`-transport: the server is reachable only over HTTP, never by starting it as a local process.

## What is not available yet

This list follows the capabilities the pinned SDK-version defines (`ServerCapabilities` and `ClientCapabilities` of
`ModelContextProtocol.Core` 2.2.0) plus the mechanisms of the `Tools`-capability itself and the transport- and
auth-related points. It does not cover the `Experimental`/`Extensions` dictionary of either side, which the protocol
deliberately leaves open for custom capabilities.

| Mechanism | State |
| --------- | ----- |
| Resources | **Implemented.** Missing are subscriptions (they would need a polling- or watch-mechanism per resource) and uri-templates (this server offers the resources which are declared and nothing derived from them). |
| Prompts | **Implemented.** Missing is a notification when the list of the prompts changed, which would need a watch-mechanism on the declaration - which this server reads once, while it starts. |
| Completions | **Implemented** for the arguments of a prompt. Missing is a completion for a resource, which would need uri-templates, and one for the parameter of a tool, which the protocol does not have at all. |
| Structured tool-output | **Implemented.** Missing are the `resource_link`- and `resource`-content-blocks, which would let a tool answer with a reference to a resource instead of with its own content. |
| Cancellation of a tool-call | **Implemented.** Missing is the cancellation of a read of a resource, of a prompt and of a completion: the notification names a request-id and not what that request was about, and each of those three is answered in one go. |
| Sampling | Not available, and not open as a next step: the running script would need a callback into the backend mid-execution to ask the client for a completion and wait for the answer. That is a new contract between server and script and not a protocol addition. |
| Elicitation | Not available, for the same reason as sampling. |
| Logging-notifications | Not available: a script would have to push structured messages while it is still running, on top of its final result. |
| Progress of a tool-call | Not available: the call would have to move from run-to-completion to streaming, with the script writing intermediate markers while it runs. |
| Roots | Not used. A root is a local path of the client, and the scripts of a remote HTTP-server run on a different machine, so there is nothing meaningful to hand them. |
| stdio-transport | Deliberately out of scope: a transport decision, not a question of what a tool-script can do. |
| OAuth 2.1 | Deliberately out of scope: this product uses its own client/role/access-token-mechanism instead. |

The four mechanisms which are not open as a next step (sampling, elicitation, logging-notifications, progress) all
fail on the same point: they require a declared script to send something back while it is still running. Everything
this product runs is one call in and one answer out, and that is what makes a script cancellable, replaceable and
writable without knowing anything about this server.

## Building and running it

```
task bb
task bma
```

`task bb` builds both codeunits including their testcases, the linting and the container-image. `task bma` starts the
minimal example, `task bae` the advanced one; `task bmat` respectively `task baet` stop them again. The minimal
example declares one anonymous mcp-server which is reachable right away under `http://127.0.0.1:1222/GeneralTools`,
the advanced one declares three.

## Where the details are documented

| Question | Document |
| -------- | -------- |
| How is an mcp-server declared, how is a tool, a resource, a prompt or the values of one of its arguments declared, who may use it and how is it tested without a client? | [Reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md) |
| What is needed to work on the backend and to start it during development? | [Hints of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/Hints.md) |
| Which environment-variables does the container-image take and which folders does it keep? | [Reference of the image](../../GenericMCPServer/Other/Reference/ReferenceContent/index.md) |
| Which mcp-servers and tools do the examples declare and how are they started? | [Examples](../../GenericMCPServer/Other/Reference/ReferenceContent/Examples) |
| Which versions of the product are supported? | [InformationAboutSupportedVersions.csv](../Resources/Support/InformationAboutSupportedVersions.csv) |
| What changed in a version? | [Changelog](../Resources/Changelog) |
