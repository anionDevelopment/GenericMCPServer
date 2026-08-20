# Reference of GenericMCPServer

This is the reference of the repository as a whole: what the product does, how its parts fit together and where
the details of each part are documented. The reference of a single codeunit is located in that codeunit.

## What the product does

GenericMCPServer is a MCP-server which is reachable over HTTP. It offers exactly the tools which an administrator
declares and brings no tool of its own along - that is what "generic" means here.

A declaration names a tool, what implements it, what it does and which parameters it takes. A client therefore sees
tools like `get_cve_details`, although nothing in the sourcecode knows that name. Adding a tool means one entry in a
file (and, depending on its type, a script); it means no change of the sourcecode and no new build.

A tool is of one of three types, which the declaration has to state:

| Type | What implements the tool |
| ---- | ------------------------ |
| `python` | A python-script. It runs in a virtual environment which belongs to this tool alone, so the packages of two tools are independent of each other. The packages are declared as `requirements` and installed when the tool is called for the first time. |
| `shell` | A shell-script, run with `/bin/sh`. This requires a linux-system. |
| `web-request` | No script: the client states an address, and the declaration states which addresses the tool allows. The protocol is always https, everything which no rule allows is denied, and the answer contains the status-code and the body. |

## Parts of the product

| Codeunit | Content |
| -------- | ------- |
| [GenericMCPServerBackend](../../GenericMCPServerBackend/ReadMe.md) | The server itself: it reads the declarations, offers them as tools and runs the scripts behind them. |
| [GenericMCPServer](../../GenericMCPServer/ReadMe.md) | The container-image which packages the backend together with a python-interpreter, because a declared tool is usually a python-script. |

The MCP-protocol itself is not implemented here. It is implemented by the MCP-SDK of Microsoft; the backend only
provides one tool-object per declaration and hands them to the SDK. The whole protocol is exchanged over the single
endpoint `POST /API/v1/MCP`.

## What has to be provided to run it

Two things belong together and have to be reachable by the server:

- The tool-definitions-file, which the server is pointed to with the commandline-parameter
  `--InitialToolDefinitionsFile` (in the image: the environment-variable of the same name).
- The scripts which the declarations name. A script is resolved relative to the folder of the declaration and has
  to stay inside it, so mounting only the declaration into the container leaves every tool broken.

A mistake in the declaration (a tool without a name, a name which is used twice, invalid json, a configured file
which does not exist) makes the server fail on startup instead of when somebody happens to call the affected tool.
Only *no* configured file means "no tools".

The exact format of the file and the meaning of each field is documented in the
[reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md).

## Who may use a tool

A client reaches a tool only with an access-token. An administrator creates one per client with the call
`CreateAccessToken` of the backend; a client can hold several valid tokens at the same time, every token ends at a
stated point in time, and only the hash of a token is stored - in a csv-file in the configuration-folder of the server,
which is created when it does not exist yet. A tool which everybody may use is declared with `"anonymous": true`; such
a tool is also the only thing which a caller without a token sees.

## Where the accesses of a tool come from

A tool which uses a foreign service needs an access to it. Such an access belongs to the administrator and never to
the client, so it is no parameter: the operator sets it as an environment-variable of the container (or, as a
docker-secret, as a file which such a variable points at), and the declaration states which tool receives which one and
under which name. A tool receives exactly its own accesses; the variables of the others are removed from the
environment of its script. A missing access keeps the server from starting, so that a forgotten secret is visible while
deploying.

## What a failed tool-call looks like

A tool-call which did not work is answered in two ways at once: as a result of the protocol which carries the message
of the failure (so that the model learns what was wrong), and with the http-status-code 502 for the whole answer,
because the failure comes from the program behind the server and the server is the gateway to it.

## Why a declared tool can be trusted with untrusted values

The values of the parameters come from the client, so they are untrusted. The product does not try to make them
harmless by escaping them; it makes escaping unnecessary:

- No shell is involved when a script is started, and every argument is handed to the script as exactly one
  argument by the operating-system.
- Every value is passed as one token in the form `--name=value`, so a value which looks like an option can not
  become one.
- Whether a value has the format it must have is checked by the script itself, next to the code which depends on
  it, and a rejected value becomes a failed tool-call whose message says what was wrong.

## Building and running it

```
task bb
cd GenericMCPServer\Other\Examples\MinimalDockerComposeFile
python RunExample.py
```

`task bb` builds both codeunits including their testcases, the linting and the container-image. The example
declares five tools, mounts them into the container and makes the endpoint available under
`http://127.0.0.1:8080/API/v1/MCP`.

## Where the details are documented

| Question | Document |
| -------- | -------- |
| How is a tool declared, what does the server do with it and how is it tested without a client? | [Reference of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/index.md) |
| What is needed to work on the backend and to start it during development? | [Hints of the backend](../../GenericMCPServerBackend/Other/Reference/ReferenceContent/Hints.md) |
| Which tools does the example declare and how is it started? | [Example](../../GenericMCPServer/Other/Examples/MinimalDockerComposeFile) |
| Which versions of the product are supported? | [InformationAboutSupportedVersions.csv](../Resources/Support/InformationAboutSupportedVersions.csv) |
| What changed in a version? | [Changelog](../Resources/Changelog) |
