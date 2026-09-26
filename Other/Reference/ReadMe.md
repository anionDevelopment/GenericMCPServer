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

- The tool-definitions-file, whose path (`ToolDefinitionsFile`, relative to the tools-folder unless given as
  an absolute path) is part of the persisted configuration-file of the server. It defaults to `./Tools.json` the
  first time that configuration-file is generated; an administrator who wants the tools declared somewhere else
  changes the value there afterwards.
- The scripts which the declarations name. A script is resolved relative to the folder of the declaration and has to
  stay inside it, so mounting only the declaration into the container leaves every tool broken.

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

An administrator manages clients and roles over the routes below `/API/v1/Admin`, which the token in the
server opens (it is shown on the console at the first start; only its hash is stored). Creating a client answers with a client-administration-token, and whoever runs
that client creates its access-tokens with it over the routes below `/API/v1/ClientAdministration`. Every token is
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
`http://127.0.0.1:8080/GeneralTools`.

## Where the details are documented

| Question | Document |
| -------- | -------- |
| How is an mcp-server declared, how is a tool, a resource, a prompt or the values of one of its arguments declared, who may use it and how is it tested without a client? | [Reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md) |
| What is needed to work on the backend and to start it during development? | [Hints of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/Hints.md) |
| What is the responsibility of an administrator who declares a tool, a resource, a prompt or the values of one of its arguments? | [ReadMe of the image](../../GenericMCPServer/ReadMe.md) |
| Which mcp-servers and tools does the example declare and how is it started? | [Example](../../GenericMCPServer/Other/Examples/MinimalDockerComposeFile) |
| Which versions of the product are supported? | [InformationAboutSupportedVersions.csv](../Resources/Support/InformationAboutSupportedVersions.csv) |
| What changed in a version? | [Changelog](../Resources/Changelog) |
