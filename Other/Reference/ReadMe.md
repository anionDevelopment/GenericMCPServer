# Reference of GenericMCPServer

This is the reference of the repository as a whole: what the product does, how its parts fit together and where
the details of each part are documented. The reference of a single codeunit is located in that codeunit.

## What the product does

GenericMCPServer is a MCP-server which is reachable over HTTP. It offers exactly the tools which an administrator
declares and brings no tool of its own along - that is what "generic" means here.

A declaration names a tool, the script which implements it, what it does and which parameters it takes. A client
therefore sees tools like `get_cve_details`, although nothing in the sourcecode knows that name. Adding a tool
means writing a script and one entry in a file; it means no change of the sourcecode and no new build.

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
