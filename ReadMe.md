# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP.

It offers exactly the tools and resources which an administrator declares: a declaration names what implements a tool,
what the tool does and which parameters it takes, respectively under which uri a resource is read and what implements
it. The product itself brings neither a tool nor a resource along, which is what makes it generic.

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
| Failures are visible | A tool-call which did not work is answered with the message of the failure and with the http-status-code 502, because the failure comes from the program behind this server. A read of a resource which did not work is answered as an error of the protocol with the message of the failure, because a client which asked for a document must not receive the reason of a failure as if it were that document. |

## Codeunits

| Codeunit | Description |
| -------- | ----------- |
| [GenericMCPServerBackend](./GenericMCPServerBackend/ReadMe.md) | The backend which implements the MCP-protocol and runs the declared tools and resources. |
| [GenericMCPServer](./GenericMCPServer/ReadMe.md) | The container-image which packages the backend. |

## Getting started

The image is available on docker hub as
[aniondev/genericmcpserver](https://hub.docker.com/r/aniondev/genericmcpserver), so it can be used without building
this repository. Building it yourself and starting the example works like this:

```
task bb
cd GenericMCPServer\Other\Examples\MinimalDockerComposeFile
python RunExample.py
```

The example declares three mcp-servers and mounts them into the container:

| mcp-server | Address | Who may use it | Tools |
| ---------- | ------- | -------------- | ----- |
| `GeneralTools` | `http://127.0.0.1:8080/GeneralTools` | everybody (anonymous) | the current point in time |
| `Security` | `http://127.0.0.1:8080/Security` | the role `security` | the details of a CVE |
| `DeveloperTools` | `http://127.0.0.1:8080/DeveloperTools` | the roles `developer` and `security` | the available versions of a maven-, NuGet-, PyPI- and npm-package, the metadata of a PyPI-package and a research-tool for the usual sources of a developer |

`GeneralTools` works right away. For the other two, a client needs an access-token. The server generates a token for
its administrator at the first start and writes it to the console; only its hash is stored, so it can not be looked
up later.
With it, an administrator creates a role, a client and the assignment:

```
curl -X POST http://127.0.0.1:8080/API/v1/Admin/Roles/developer -H "X-Admin-Token: <admin-token>"
curl -X POST http://127.0.0.1:8080/API/v1/Admin/Clients/my-client -H "X-Admin-Token: <admin-token>"
curl -X POST http://127.0.0.1:8080/API/v1/Admin/Clients/my-client/Roles/developer -H "X-Admin-Token: <admin-token>"
```

The second call answers with the **client-administration-token** of that client, and only that answer contains it. It
is handed to whoever runs the client, who creates access-tokens with it:

```
curl -X POST "http://127.0.0.1:8080/API/v1/ClientAdministration/AccessTokens?validForDays=90" -H "X-Client-Administration-Token: <client-administration-token>"
```

That access-token is sent to an mcp-server as `Authorization: Bearer <token>`.

How a tool and how a resource is declared is described in the
[reference of the backend](./GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Where Tools.json has to be

Which file declares the mcp-servers and their tools is itself part of the persisted configuration of the server
(`ToolDefinitionsFile`) and defaults to `./Tools.json`, resolved against the **configuration-folder** of the server -
not the working directory the server happens to be started from:

| Environment | Configuration-folder | Default location of `Tools.json` |
| ----------- | --------------------- | --------------------------------- |
| Local run (`dotnet run ...`) | `GenericMCPServerBackend/Other/Workspace/Configuration` | `GenericMCPServerBackend/Other/Workspace/Configuration/Tools.json` |
| Container | `/Workspace/Configuration` | `/Workspace/Configuration/Tools.json` |

Every script which a tool or a resource `calls` has to lie in that same folder (the folder of `Tools.json`), because
it is resolved relative to it - a declaration whose scripts lie somewhere else fails when the tool is called
respectively when the resource is read.

An administrator edits `Tools.json` like any other text-file - locally on disk, or, for a container, through
whatever volume the configuration-folder is mounted with (see the example `docker-compose.yml` under
`GenericMCPServer/Other/Examples/MinimalDockerComposeFile`). Two things matter while doing so:

- **The server reads it only once, at startup.** Unlike the csv-files of the clients, roles and access-tokens (which
  are read for every request), a changed `Tools.json` needs a restart of the server to take effect.
- **A file which is configured but does not exist is an error**, and so is a mistake in it (an mcp-server without a
  url-namespace, a tool without a type, invalid json, ...) - both make the server fail while starting. Only *no*
  configured file at all means "nothing is offered".

## Protocol

GenericMCPServerBackend implements the tools- and the resources-primitive of the MCP-protocol - the
`initialize`-handshake, `tools/list`, `tools/call`, `resources/list` and `resources/read` - through the official
C#-SDK (`ModelContextProtocol`, pinned to version `2.2.0`); this product contributes no protocol-code of its own,
only the tools and the resources. An mcp-server which declares no resource does not claim the resources-capability at
all, so a client of it never asks for a list which is empty by definition.

The transport is the current **Streamable HTTP** transport, run in its stateless mode: every request is answered on
its own, no session-id has to be kept by the client, and no separate long-lived connection is opened first - unlike
the older, meanwhile deprecated HTTP+SSE-transport of the protocol-revision `2024-11-05`. Which protocol-revision a
client uses is negotiated with it during `initialize`; the pinned SDK-version understands the revisions `2024-11-05`,
`2025-03-26`, `2025-06-18`, `2025-11-25` and `2026-07-28`.

There is no `stdio`-transport: the server is reachable only over HTTP, never by starting it as a local process.

## Not available yet

GenericMCPServerBackend implements the tools- and the resources-primitive of the MCP-protocol and nothing else. What
a "usual" MCP-server can additionally offer, and which this product does not have (yet); an entry which is struck
through is one which is not open anymore - either because it is implemented by now or because it is deliberately out
of scope, which the entry itself states:

| Primitive / mechanism | What is missing | Notes / What is missing | Simple to add on top of today's tool-scripts?<br>(no new requirements on the script, no send-back to the backend, no mandatory extra parameter) |
| ---------------------- | ---------------- | ---------------------------------------- | ---------------------------------------- |
| ~~**1: Resources**~~ | **Implemented**: `resources/list` and `resources/read` exist, a resource is declared in `Tools.json` next to the tools of its mcp-server (see [Resources are declared like tools](#features)). Still missing are subscriptions, which would need an additional polling- or watch-mechanism per resource, and uri-templates, because this server offers the resources which are declared and nothing which is derived from them. | Fits the existing model: declare resources in `Tools.json` like tools, backed by a script or web-request that returns content and mime-type. Subscriptions would need an additional polling or watch-mechanism per resource. | ✅ A resource-read is the same call-once/return-once shape a tool already has; a script only gets the declared parameters (for example a resource-uri) and returns its content in one go. |
| **2: Prompts** | No `prompts/list` or `prompts/get`. | Fits the existing model: prompt-templates declared statically in `Tools.json` or generated by a script that takes arguments and returns text - no callback while running is needed. | ✅ Same call-once/return-once shape as a tool; no callback and no parameter beyond the ones the prompt itself declares. |
| ~~**3: Sampling**~~ | The server can not ask the client for a completion of its own. | Needs a new runtime channel: the running script-process would need a callback (a local endpoint, or a line-protocol over stdin/stdout) to ask the server for a completion mid-execution, have the server forward it to the client synchronously, and receive the answer back. This is a new contract between server and script, not just a protocol addition. | ❌ Only works if the script can actively call back into the backend mid-execution and wait for an answer - exactly the "must send back" case, plus a new callback-capability every participating script would need to implement. |
| ~~**4: Elicitation**~~ | The server can not ask the client to collect structured input from its user. | Same new runtime channel as sampling: the script pauses, asks for structured input over that callback, and resumes once the answer arrives. | ❌ Same reasoning as sampling: needs an active mid-execution callback from the script to the backend, which today's scripts have no way to do. |
| **5: Completions** | No autocompletion for the argument of a prompt or a resource. | Fits the existing model: a self-contained request (`completion/complete`), not a running tool-call - could be declared as its own small tool-type ("completion-provider") per prompt/resource argument. | ✅ A completion-request is still one call in, one list of suggestions out - the same shape as a tool-call, just invoked for a different protocol method. |
| ~~**6: Logging-notifications**~~ | No `notifications/message` is sent to the client. | Small extension: define a contract where a script may write structured lines (e.g. JSON-lines with level and text) to stdout; the server reads the process as a stream instead of only its final result and forwards each line as a `notifications/message`. | ❌ Needs the script to actively push several structured messages while it is still running, on top of its final result - that is a new, script-specific output-format requirement and repeated send-back rather than one plain answer. |
| **7: Roots** | A root the client offers is not used. | Doubtful fit: roots are mainly meaningful for a local/stdio-server that shares a filesystem with the client. For a remote HTTP-server the script runs on a different machine, so a client-reported local path has no counterpart there - unless roots were redefined entirely (for example as a URI-restriction for web-request tools). | ✅ Precisely because roots are not passed on to a script at all today (there is nothing meaningful to hand it), accepting them from the client would not touch scripts, force new parameters or need any send-back - it would just stay functionally unused, as noted above. |
| **8: Structured tool-output** | Every tool answers with plain text; no `outputSchema` and no `structuredContent` of a `tools/call`-result is used. | Fits the existing model: a script emits JSON, the server validates and wraps it as `structuredContent` against an `outputSchema` declared in `Tools.json` - a plain extension of the existing call-result. | ✅ Still one call in, one answer out - only the shape of that one answer changes (JSON instead of free text) for tools that opt into it; scripts which don't declare an `outputSchema` are unaffected. |
| ~~**9: Progress of a tool-call**~~ | A `progressToken` a client sends with `tools/call` is not used; no `notifications/progress` is sent while a tool runs. | Needs a streaming adjustment: the call would have to move from run-to-completion to streaming, with the script writing intermediate markers to stdout that the server reads while the process runs and turns into `notifications/progress`. Less than the full bidirectional channel sampling/elicitation need, but more than a plain wrapper. | ❌ Needs the script to emit intermediate markers while still running, in addition to its final result - a new output-format requirement and a repeated send-back instead of one plain answer. |
| **10: Cancellation of a tool-call** | A `notifications/cancelled` of the client does not stop the script or the web-request behind a running tool-call. | Fits the existing model: the server keeps the process-handle or running web-request per `progressToken`/request-id and terminates or aborts it on `notifications/cancelled` - purely server-side, the script needs no special capability. | ✅ Entirely server-side (kill the process / abort the request); the script itself needs no new capability, sends nothing back and takes no new parameter. |
| ~~**11: stdio-transport**~~ | Only HTTP is offered; a client which needs a local process (for example a classic desktop-integration) can not use it. | Out of scope here: a transport, not a tool-passthrough question - a fundamental product decision (local process instead of HTTP), not a matter of script capabilities. | ➖ Not applicable - a transport choice, does not concern tool-scripts at all. |
| ~~**12: OAuth 2.1**~~ | The current MCP-specification recommends it for a remote HTTP-server; this product uses its own client/role/access-token-mechanism instead (see [Several clients, each with several access-tokens](#features)). | Out of scope here: an auth question, not a tool-passthrough question - the existing client/role/token-model is already a deliberate alternative. | ➖ Not applicable - an authentication choice, does not concern tool-scripts at all. |

This list follows the capabilities the pinned SDK-version defines (`ServerCapabilities` and `ClientCapabilities` of
`ModelContextProtocol.Core` 2.2.0: `Logging`, `Prompts`, `Resources`, `Tools`, `Completions` on the server-side,
`Roots`, `Sampling`, `Elicitation` on the client-side) plus the mechanisms of the `Tools`-capability itself
(structured output, progress, cancellation) and the transport- and auth-related points above. It does **not** cover
the `Experimental`/`Extensions` dictionary of either side: the protocol deliberately leaves that open for
custom, non-standardized capabilities a client could declare, so no finite list can state everything a client could
possibly send there.

## Reference

The reference of the repository as a whole can be found [here](./Other/Reference/ReadMe.md).

## Development-state

Active development.

## License

See [License.txt](./License.txt).
