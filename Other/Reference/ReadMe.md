# Reference of GenericMCPServer

This is the reference of the repository as a whole: what the product does, how its parts fit together and where the
details of each part are documented. The reference of a single codeunit is located in that codeunit.

## What the product does

GenericMCPServer hosts MCP-servers which are reachable over HTTP. It offers exactly the tools, resources and prompts
which an administrator declares and brings none of them of its own along - that is what "generic" means here.

A declaration names an mcp-server, the tools it offers, what implements each of them and who may use it. A client
therefore sees tools like `get_cve_details`, although nothing in the sourcecode knows that name. Adding a tool means
one entry in a file (and, depending on its type, a script); it means no change of the sourcecode and no new build.

## Features

| Feature | Description |
| ------- | ----------- |
| Tools are declared, not built in | Which tools exist is decided by a declaration-file of an administrator. A new tool needs an entry there and no change of this product, no new version and no restart of anything but the server itself. |
| Three kinds of tools | A tool is implemented by a **python-script**, by a **shell-script** or by a **web-request** whose reachable addresses the declaration restricts. |
| A virtual environment per python-tool | The packages a python-tool needs are declared as its requirements and installed into an environment which belongs to that tool alone, so two tools can need two versions of the same package. |
| Web-requests with a whitelist | A tool of the kind web-request reaches exactly the addresses which the rules of its declaration match; everything else is denied. The protocol is always https and redirects are not followed. |
| Credentials per tool | A tool receives the accesses which its declaration assigns to it (from an environment-variable of the server or from a docker-secret), and no tool receives the accesses of another one. |
| **Several mcp-servers at the same time** | One process hosts as many mcp-servers as are declared. Each one has its **own tools** and its own address `<domain>/<url-namespace>`, so a client of one of them can neither see nor reach the tools of another one - even the same tool-name may exist in two of them. |
| **Several clients, each with several access-tokens** | An administrator creates a client and hands over its client-administration-token; whoever runs that client creates its access-tokens with it. **A client can hold several valid tokens at the same time**, which is what makes an exchange possible without an interruption, and every token ends at a stated point in time. Only the hash of a token is stored. |
| **Administration over the api** | An administrator manages clients, roles and assignments over routes which the admin-token of the server opens; a client manages its own access-tokens and nothing else. |
| **Roles decide who may use what** | **A client is in none, one or several roles**, and an mcp-server declares **which roles** may use it. Who is in a role and what a role may do are therefore two decisions in two places: a new client needs no change of the declaration, and a changed declaration needs no new client. |
| **Anonymous mcp-servers** | An mcp-server can be declared as one which everybody may use. It then needs no token at all, while the other mcp-servers of the same process stay behind theirs. |
| Resources are declared like tools | Next to its tools an mcp-server offers **resources**: content which a client reads by a uri. A resource is declared with that uri, its name and the type of its content and is implemented by a python-script, a shell-script or a web-request, exactly like a tool. It takes no parameter - its uri is the whole request, and a script receives exactly that uri, so one script can implement several resources. |
| Prompts are declared like tools | Next to its tools and resources an mcp-server offers **prompts**: a text which a client asks for by its name and hands to the model behind it. A prompt is declared with that name, its description and the arguments it takes, and it is implemented by a python-script or a shell-script which writes that text; the values of the arguments reach the script exactly like the values of a tool. A prompt is no web-request: such a declaration takes the address it requests from the client or from its own uri, and a prompt has neither. |
| Autocompletion for the argument of a prompt | An argument of a prompt can declare **which values it may have**: a python-script or a shell-script which is told the argument and the value which was entered so far and writes the possible values, one per line. A client offers them while somebody fills in that argument (`completion/complete`). An argument without such a declaration is one whose values a client states without being offered any. |
| Structured answers of a tool | A tool can declare the shape of its answer as an `output-schema`. Its script then writes a json-document instead of free text, and that document reaches the client as the **structured content** of the result next to the text, so the client reads data instead of parsing a text. An answer which is no json-document or does not fit the declared schema is a failed call, and a tool which declares no schema answers with text exactly as before. |
| A running tool-call can be cancelled | A client which does not need the answer of a tool-call anymore sends `notifications/cancelled`; the **program behind that call is really stopped** - the script together with everything it started, respectively the web-request which is waiting for a foreign service. It works although the call and its cancellation are two independent requests, and it needs nothing of the script: a declared tool is cancellable without knowing that it is. |
| Failures are visible | A tool-call which did not work is answered with the message of the failure and with the http-status-code 502, because the failure comes from the program behind this server. A read of a resource, a prompt and a completion which did not work are answered as an error of the protocol with the message of the failure, because a client which asked for a document, for a text or for the possible values of an argument must not receive the reason of a failure as if it were that document, that text respectively one of those values. A call which the client itself cancelled is none of those two: it is answered with that reason and with the status-code of a normal answer, because nothing failed. |


## Features

| Feature | Description |
| ------- | ----------- |
| Tools are declared, not built in | Which tools exist is decided by the tool-definitions-file of an administrator. A tool-name in the sourcecode of this product is a defect. |
| Three kinds of tools | `python` (a script in a virtual environment of its own), `shell` (a script run with `/bin/sh`, linux only) and `web-request` (no script: an address which the declaration allows). |
| Requirements per python-tool | The packages a python-tool needs are installed into an environment which belongs to that tool alone, built when it is first called and rebuilt when the declared requirements change. |
| Whitelist per web-request-tool | Every rule is matched against the whole address, the protocol is always https, redirects are not followed and what no rule allows is denied. |
| Credentials per tool | An access of the operator (from an environment-variable or a docker-secret) is assigned to a tool by the declaration; no tool receives the accesses of another one, and values are removed from failure-messages. |
| Several mcp-servers in one process | Every declared mcp-server has its own tools and its own address `<domain>/<url-namespace>`. A client of one of them can neither see nor reach the tools of another one, and the same tool-name may exist in two of them. |
| Clients with several access-tokens | An administrator creates clients and roles over the api; a client creates its own access-tokens with the token it received. A client can hold several valid ones at the same time, every token ends at a stated point in time, and only the hash is stored. |
| Roles | A client is in none, one or several roles; an mcp-server declares which roles may use it. Who is in a role and what a role may do are two decisions in two files. |
| Anonymous mcp-servers | An mcp-server can be declared as one which everybody may use; it then needs no token, while the others of the same process stay behind theirs. |
| Resources and prompts are declared like tools | Next to its tools an mcp-server offers `resources` (content which a client reads by a uri, which takes no parameter and whose script receives that uri) and `prompts` (a text which a client asks for by its name and hands to its model, whose script receives the declared arguments like a tool). Both are declared in the same file and implemented in the same ways as a tool, except that a prompt is never a `web-request`. |
| The values which an argument of a prompt can have | An argument of a prompt may declare what provides its possible values: a script which is told the argument and the value which was entered so far and writes one value per line. A client offers them while somebody fills in that argument. An mcp-server whose prompts declare none of them does not offer the completions of the protocol at all. |
| Structured answers of a tool | A tool may declare the shape of its answer as an `output-schema`. That schema is offered to the client with the tool, and what the script wrote is handed over as the structured content of the result next to the text. An answer which is no json-document or does not fit the schema is a failed tool-call. |
| A running tool-call can be cancelled | A `notifications/cancelled` of the client ends the script behind that call together with everything it started, respectively aborts its web-request. The call and the cancellation are two http-requests, which is why the running calls are kept in one registry of the process; the script needs no capability of its own for it. |
| Failures are visible | A tool-call which did not work is answered with its message and with the http-status-code 502; a read of a resource, a prompt and a completion which did not work are answered as an error of the protocol with the message of the failure; a request which may not reach an mcp-server is answered with 401. A call which its client cancelled carries the status-code of a normal answer, because nothing failed. |

## Where Tools.json has to be

Which file declares the mcp-servers and their tools is itself part of the persisted configuration of the server
(`ToolDefinitionsFile`). That value is the **whole path of that file** including its name, so an administrator reads
in the configuration-file where the declaration is looked for and moves it by changing that one value. It is written
while the configuration-file is generated, and it then names `Tools.json` in the **tools-folder** of the server:

| Environment | Tools-folder | Default value of `ToolDefinitionsFile` |
| ----------- | ------------ | --------------------------------- |
| Local run (`dotnet run ...`) | `GenericMCPServerBackend/Other/Workspace/Tools` | `GenericMCPServerBackend/Other/Workspace/Tools/Tools.json` |
| Container | `/Workspace/Tools` | `/Workspace/Tools/Tools.json` |

Which path that generated value has can be stated from outside with the environment-variable
`InitialToolsDefinitionFilePath`, again as the whole path of the file. It decides the value that file is generated
with and nothing else: from the second start on the configuration-file is what the server reads, so the variable
changes nothing on a server which is already running somewhere.

The declaration and every script it names belong in that tools-folder or in a folder below it. The value can be
pointed anywhere else - into the configuration-folder as well - but nothing of the server expects it there, so that
is a decision an administrator makes against the grain and not one the server offers.

Because the configured value names a file and not a folder, a container can receive the declaration as a **single
mounted file** into that folder instead of as a whole folder:

```yml
    volumes:
      - ./Tools.json:/Workspace/Tools/Tools.json
```

Nothing else of the host-folder around that file reaches the container then, and nothing the image keeps in
`/Workspace/Tools` is covered by the mount. This fits a declaration whose tools are web-requests, because those need
no script; a declaration which names scripts is handed over together with them.

The tools-folder holds what an administrator writes and the configuration-folder (`/Workspace/Configuration`
respectively `GenericMCPServerBackend/Other/Workspace/Configuration`) holds what the server writes: its
configuration-file and the files of its clients, roles and access-tokens. They are two folders next to each other for
exactly that reason - a declaration is handed over without the state of the server being handed over with it.

Every script which a tool, a resource or a prompt `calls` has to lie inside that same folder (the folder of
`Tools.json`) or below it, because it is resolved relative to it and has to stay inside it - a declaration whose
scripts lie somewhere else fails when the tool is called, when the resource is read respectively when the prompt is
asked for.

An administrator edits `Tools.json` like any other text-file - locally on disk, or, for a container, through
whatever volume the declaration is mounted with (see the example `docker-compose.yml` under
`GenericMCPServer/Other/Examples/MinimalDockerComposeFile`). Two things matter while doing so:

- **The server reads it only once, at startup.** Unlike the csv-files of the clients, roles and access-tokens (which
  are read for every request), a changed `Tools.json` needs a restart of the server to take effect.
- **A file which does not exist yet is no error.** The server creates it as an empty declaration (`{}`) at the
  configured location and starts while offering nothing, because a freshly set up server which nobody pointed at a
  declaration yet is an expected state. The folder of that file therefore has to exist and to be writable; a folder
  which is not there makes the server fail while starting instead of being created, because such a path is a mistake
  and not a place.
- **A mistake in the file is an error** (an mcp-server without a url-namespace, an mcp-server which neither is
  anonymous nor names a role, a tool without a type, a prompt which is declared as a web-request, a name which is used
  twice, invalid json, ...) and makes the server fail while starting, instead of when somebody happens to call the
  affected tool.

## Protocol

GenericMCPServerBackend implements the tools-, the resources-, the prompts- and the completions-primitive of the
MCP-protocol - the `initialize`-handshake, `tools/list`, `tools/call`, `resources/list`, `resources/read`,
`prompts/list`, `prompts/get` and `completion/complete` - through the official C#-SDK (`ModelContextProtocol`, pinned
to version `2.2.0`); this product contributes no protocol-code of its own, only the tools, the resources, the prompts
and the possible values of their arguments. A tool which declares an `output-schema` additionally hands its answer
over as the `structuredContent` of the result of `tools/call`, and a `notifications/cancelled` of the client stops the
program behind the tool-call it names. An mcp-server which declares no resource does not claim the
resources-capability at all, one which declares no prompt does not claim the prompts-capability and one which declares
no completion does not claim the completions-capability, so a client of it never asks for a list which is empty by
definition.

The transport is the current **Streamable HTTP** transport, run in its stateless mode: every request is answered on
its own, no session-id has to be kept by the client, and no separate long-lived connection is opened first - unlike
the older, meanwhile deprecated HTTP+SSE-transport of the protocol-revision `2024-11-05`. Which protocol-revision a
client uses is negotiated with it during `initialize`; the pinned SDK-version understands the revisions `2024-11-05`,
`2025-03-26`, `2025-06-18`, `2025-11-25` and `2026-07-28`.

There is no `stdio`-transport: the server is reachable only over HTTP, never by starting it as a local process.

## Not available yet

GenericMCPServerBackend implements the tools-, the resources-, the prompts- and the completions-primitive of the
MCP-protocol and nothing else. What a "usual" MCP-server can additionally offer, and which this product does not have
(yet); an entry which is struck through is one which is not open as a next step anymore - either because it is
implemented by now, or because it would need something which a declared script deliberately does not do (send something
back to the server while it is still running), or because it is deliberately out of scope. Which of the three it is, the
entry itself states, and an entry which states "**Implemented**" is the only kind which the product has:

| Primitive / mechanism | What is missing | Notes / What is missing | Simple to add on top of today's tool-scripts?<br>(no new requirements on the script, no send-back to the backend, no mandatory extra parameter) |
| ---------------------- | ---------------- | ---------------------------------------- | ---------------------------------------- |
| ~~**1: Resources**~~ | **Implemented**: `resources/list` and `resources/read` exist, a resource is declared in `Tools.json` next to the tools of its mcp-server (see [Resources are declared like tools](#features)). Still missing are subscriptions, which would need an additional polling- or watch-mechanism per resource, and uri-templates, because this server offers the resources which are declared and nothing which is derived from them. | Fits the existing model: declare resources in `Tools.json` like tools, backed by a script or web-request that returns content and mime-type. Subscriptions would need an additional polling or watch-mechanism per resource. | ✅ A resource-read is the same call-once/return-once shape a tool already has; a script only gets the declared parameters (for example a resource-uri) and returns its content in one go. |
| ~~**2: Prompts**~~ | **Implemented**: `prompts/list` and `prompts/get` exist, a prompt is declared in `Tools.json` next to the tools and the resources of its mcp-server (see [Prompts are declared like tools](#features)). Still missing is a notification when the list of the prompts changed, which would need a watch-mechanism on the declaration - which this server reads once, while it starts. | Fits the existing model: a prompt is declared in `Tools.json` and is implemented by a script which takes the declared arguments and writes the text of the prompt - no callback while running is needed. Autocompletion for the argument of a prompt is entry 5 of this list. | ✅ Same call-once/return-once shape as a tool; no callback and no parameter beyond the ones the prompt itself declares. |
| ~~**3: Sampling**~~ | The server can not ask the client for a completion of its own. | Needs a new runtime channel: the running script-process would need a callback (a local endpoint, or a line-protocol over stdin/stdout) to ask the server for a completion mid-execution, have the server forward it to the client synchronously, and receive the answer back. This is a new contract between server and script, not just a protocol addition. | ❌ Only works if the script can actively call back into the backend mid-execution and wait for an answer - exactly the "must send back" case, plus a new callback-capability every participating script would need to implement. |
| ~~**4: Elicitation**~~ | The server can not ask the client to collect structured input from its user. | Same new runtime channel as sampling: the script pauses, asks for structured input over that callback, and resumes once the answer arrives. | ❌ Same reasoning as sampling: needs an active mid-execution callback from the script to the backend, which today's scripts have no way to do. |
| ~~**5: Completions**~~ | **Implemented**: `completion/complete` exists, the values which an argument of a prompt can have are declared in the `completions` of that prompt, keyed by the name of the argument, and are written by a script exactly like a tool writes its answer (see [Autocompletion for the argument of a prompt](#features)). Still missing is a completion for a resource, which would need uri-templates - this server offers the resources which are declared and nothing which is derived from them - and one for the parameter of a tool, which the protocol does not have at all. | Fits the existing model: a self-contained request (`completion/complete`), not a running tool-call - declared as its own small provider per prompt-argument, which is told the argument and the value entered so far and writes one possible value per line. | ✅ A completion-request is still one call in, one list of suggestions out - the same shape as a tool-call, just invoked for a different protocol method. |
| ~~**6: Logging-notifications**~~ | No `notifications/message` is sent to the client. | Small extension: define a contract where a script may write structured lines (e.g. JSON-lines with level and text) to stdout; the server reads the process as a stream instead of only its final result and forwards each line as a `notifications/message`. | ❌ Needs the script to actively push several structured messages while it is still running, on top of its final result - that is a new, script-specific output-format requirement and repeated send-back rather than one plain answer. |
| **7: Roots** | A root the client offers is not used. | Doubtful fit: roots are mainly meaningful for a local/stdio-server that shares a filesystem with the client. For a remote HTTP-server the script runs on a different machine, so a client-reported local path has no counterpart there - unless roots were redefined entirely (for example as a URI-restriction for web-request tools). | ✅ Precisely because roots are not passed on to a script at all today (there is nothing meaningful to hand it), accepting them from the client would not touch scripts, force new parameters or need any send-back - it would just stay functionally unused, as noted above. |
| ~~**8: Structured tool-output**~~ | **Implemented**: a tool declares the shape of its answer as an `output-schema` in `Tools.json`, that schema is offered to the client as the `outputSchema` of the tool, and what the script wrote is handed over as the `structuredContent` of the result of `tools/call` next to the text (see [Structured answers of a tool](#features)). An answer which is no json-document or does not fit the schema is a failed tool-call. Still missing is a schema for the answer of a resource or of a prompt, which neither answers with something whose shape a client reads as data, and the `resource_link`- and `resource`-content-blocks of a result, which would let a tool answer with a reference to a resource instead of with its own content. | Fits the existing model: a script emits JSON, the server validates and wraps it as `structuredContent` against an `outputSchema` declared in `Tools.json` - a plain extension of the existing call-result. | ✅ Still one call in, one answer out - only the shape of that one answer changes (JSON instead of free text) for tools that opt into it; scripts which don't declare an `outputSchema` are unaffected. |
| ~~**9: Progress of a tool-call**~~ | A `progressToken` a client sends with `tools/call` is not used; no `notifications/progress` is sent while a tool runs. | Needs a streaming adjustment: the call would have to move from run-to-completion to streaming, with the script writing intermediate markers to stdout that the server reads while the process runs and turns into `notifications/progress`. Less than the full bidirectional channel sampling/elicitation need, but more than a plain wrapper. | ❌ Needs the script to emit intermediate markers while still running, in addition to its final result - a new output-format requirement and a repeated send-back instead of one plain answer. |
| ~~**10: Cancellation of a tool-call**~~ | **Implemented**: a `notifications/cancelled` of the client stops the program behind the tool-call it names - the script together with everything it started, respectively the web-request which is waiting for a foreign service (see [A running tool-call can be cancelled](#features)). Still missing is the cancellation of a read of a resource, of a prompt and of a completion: the notification names a request-id and not what that request was about, and each of those three is answered in one go, so a cancellation which names one of them stops nothing. | Fits the existing model: the server keeps the process-handle or running web-request per `progressToken`/request-id and terminates or aborts it on `notifications/cancelled` - purely server-side, the script needs no special capability. | ✅ Entirely server-side (kill the process / abort the request); the script itself needs no new capability, sends nothing back and takes no new parameter. |
| ~~**11: stdio-transport**~~ | Only HTTP is offered; a client which needs a local process (for example a classic desktop-integration) can not use it. | Out of scope here: a transport, not a tool-passthrough question - a fundamental product decision (local process instead of HTTP), not a matter of script capabilities. | ➖ Not applicable - a transport choice, does not concern tool-scripts at all. |
| ~~**12: OAuth 2.1**~~ | The current MCP-specification recommends it for a remote HTTP-server; this product uses its own client/role/access-token-mechanism instead (see [Several clients, each with several access-tokens](#features)). | Out of scope here: an auth question, not a tool-passthrough question - the existing client/role/token-model is already a deliberate alternative. | ➖ Not applicable - an authentication choice, does not concern tool-scripts at all. |

This list follows the capabilities the pinned SDK-version defines (`ServerCapabilities` and `ClientCapabilities` of
`ModelContextProtocol.Core` 2.2.0: `Logging`, `Prompts`, `Resources`, `Tools`, `Completions` on the server-side,
`Roots`, `Sampling`, `Elicitation` on the client-side) plus the mechanisms of the `Tools`-capability itself
(structured output, progress, cancellation) and the transport- and auth-related points above. It does **not** cover
the `Experimental`/`Extensions` dictionary of either side: the protocol deliberately leaves that open for
custom, non-standardized capabilities a client could declare, so no finite list can state everything a client could
possibly send there.


## Parts of the product

| Codeunit | Content |
| -------- | ------- |
| [GenericMCPServerBackend](../../GenericMCPServerBackend/ReadMe.md) | The server itself: it reads the declarations, offers them as mcp-servers with their tools, resources, prompts and completions and runs what is behind them. |
| [GenericMCPServer](../../GenericMCPServer/ReadMe.md) | The container-image which packages the backend together with a python-interpreter, because a declared tool is usually a python-script. |

The MCP-protocol itself is not implemented here. It is implemented by the MCP-SDK of Microsoft; the backend provides
one object per declaration - a tool, a resource respectively a prompt -, one handler for the possible values of the
arguments of the prompts, one handler for a cancellation and one endpoint per mcp-server.

## What has to be provided to run it

Two things belong together and have to be reachable by the server:

- The tool-definitions-file, whose whole path (`ToolDefinitionsFile`) is part of the persisted configuration-file of
  the server. That file is generated with the path of `Tools.json` in the tools-folder; an administrator who wants the
  tools declared somewhere else changes the value there afterwards.
- The scripts which the declarations name. A script is resolved relative to the folder of the declaration and has to
  stay inside it, so mounting only the declaration into the container leaves every tool of it which is a script
  broken. A declaration whose tools are web-requests names no script and is therefore complete on its own, which is
  what lets it be mounted as a single file.

Both belong to the administrator and therefore lie in the tools-folder of the server (`/Workspace/Tools` in the
container), which is a different folder than the configuration-folder: the latter holds what the server itself writes
- its configuration-file and the files of its clients, roles and access-tokens.

A mistake in the declaration itself (an mcp-server without a url-namespace, an mcp-server which neither is anonymous
nor names a role, a tool without a type, a name which is used twice, invalid json) makes the server fail on startup
instead of when somebody happens to call the affected tool. No file at the resolved tool-definitions-path - whether
because none was ever declared or because the configured one does not exist (yet) - is not treated as such a mistake:
the server starts and simply offers no tool, which is the expected state of a freshly set up server.

The exact format of the file and the meaning of each field is documented in the
[reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Who may use an mcp-server

Three things decide it, and they are deliberately kept apart: `ClientAdministrationTokens.csv` states which clients
exist, `Roles.csv` and `RoleAssignments.csv` state which roles exist and who is in them, and the declaration of an
mcp-server states which roles may use it. All of these files live in the configuration-folder of the server, are
created when they do not exist yet and are read for every request, so a new token or a changed role works immediately.

An administrator manages clients and roles over the routes below `/API/v2/Admin`, which the token in the
server opens (it is shown on the console at the first start; only its hash is stored). Creating a client answers with a client-administration-token, and whoever runs
that client creates its access-tokens with it over the routes below `/API/v2/ClientAdministration`. Every token is
shown exactly once, because only its hash is stored, and every access-token ends at a stated point in time.

## What a failed tool-call looks like

A tool-call which did not work is answered in two ways at once: as a result of the protocol which carries the message
of the failure (so that the model learns what was wrong), and with the http-status-code 502 for the whole answer,
because the failure comes from the program behind the server and the server is the gateway to it.

A read of a resource and a prompt which did not work are answered as an error of the protocol instead, with the
status-code of a normal answer: a client which asked for a document or for a text must not receive the reason of a
failure as if it were that document respectively that text.

A call which the client itself cancelled is neither of those two. It is answered with exactly that reason and with the
status-code of a normal answer, because nothing failed: the client stated that it does not want the result anymore.

## Why a declared tool can be trusted with untrusted values

The values of the parameters come from the client, so they are untrusted. The product does not try to make them
harmless by escaping them; it makes escaping unnecessary:

- No shell is involved when a script is started, and every argument is handed to the script as exactly one
  argument by the operating-system.
- Every value is passed as one token in the form `--name=value`, so a value which looks like an option can not
  become one.
- Whether a value has the format it must have is checked by the script itself, next to the code which depends on
  it, and a rejected value becomes a failed tool-call whose message says what was wrong.

What a declared tool *does*, on the other hand, is not checked by this product and can not be: that is the
responsibility of the administrator who declared it, which the [ReadMe of the image](../../GenericMCPServer/ReadMe.md)
states in detail.

## Building and running it

```
task bb
cd GenericMCPServer\Other\Examples\MinimalDockerComposeFile
python RunExample.py
```

`task bb` builds both codeunits including their testcases, the linting and the container-image. The example declares
three mcp-servers and mounts them into the container; `GeneralTools` is anonymous and is reachable right away under
`http://127.0.0.1:1222/GeneralTools`.

## Where the details are documented

| Question | Document |
| -------- | -------- |
| How is an mcp-server declared, how is a tool, a resource, a prompt or the values of one of its arguments declared, who may use it and how is it tested without a client? | [Reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md) |
| What is needed to work on the backend and to start it during development? | [Hints of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/Hints.md) |
| What is the responsibility of an administrator who declares a tool, a resource, a prompt or the values of one of its arguments? | [ReadMe of the image](../../GenericMCPServer/ReadMe.md) |
| Which mcp-servers and tools does the example declare and how is it started? | [Example](../../GenericMCPServer/Other/Examples/MinimalDockerComposeFile) |
| Which versions of the product are supported? | [InformationAboutSupportedVersions.csv](../Resources/Support/InformationAboutSupportedVersions.csv) |
| What changed in a version? | [Changelog](../Resources/Changelog) |
