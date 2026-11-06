---
name: product-knowledge
description: What GenericMCPServer is, how this repository is structured, how an mcp-server with its tools, resources, prompts and completions is declared and which mechanisms exist for building, testing and running it. Use this before fixing a defect or developing a feature in this repository, to know where things belong and how to verify a change.
---

# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP. It offers exactly the tools, resources and prompts
which an administrator declares and brings none of them of its own along - that is what "generic" means here. A client
therefore sees tools like `get_cve_details`, although nothing in the sourcecode knows that name.

**Adding a tool changes nothing in the backend.** If you find yourself writing a tool-name into a c#-file, the change
is wrong: `grep -rniE "cve|maven|nuget|npm" GenericMCPServerBackend --include=*.cs` has to stay empty.

## Structure of the repository

The repository follows the "common project structure": all sourcecode lives in code-units, each with its own
`Other`-folder for build-, quality-check- and reference-files. The code-units are:

- `GenericMCPServerBackend` - the dotnet-api-server which hosts the declared mcp-servers and runs what is behind them.
- `GenericMCPServer` - the container-image which packages the backend.

## Where the declaration lives

The declaration is a json-file whose path (`ToolDefinitionsFile`) is part of the persisted configuration-file of the
server, as the **whole path of that file** and not as a name resolved against a folder. That configuration-file is
generated with the path of `Tools.json` in the **tools-folder** `<base-folder>/Tools` - `/Workspace/Tools` in the
container, `GenericMCPServerBackend/Other/Workspace/Tools` locally. `Program.cs` creates that folder itself, because
GRYLibrary only knows the configuration-, the data- and the log-folder.

The tools-folder is deliberately not the configuration-folder: it holds the declaration and the scripts it names,
which belong to the administrator, while the configuration-folder holds the configuration-file and the files of the
clients, roles and access-tokens, which belong to the server. A declaration is therefore handed over without the state
of the server going with it.

`--InitialToolsDefinitionFilePath` (the image passes the environment-variable of the same name on to it) states which
path the configuration-file is generated with, again as the whole path of the file. It decides that one value and
nothing else, because the configuration-file stays the single place the server reads this from. **The value names a
file and not a folder on purpose**: that lets a container receive the declaration as a single mounted file
(`./Tools.json:/Workspace/Tools/Tools.json`) instead of as a folder-mount which would cover everything else in the
tools-folder. A relative value is resolved against the tools-folder. A missing declaration is created as `{}`; a
missing *folder* makes the server fail while starting, because such a path is a mistake and not a place.

## How a declaration looks

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
                    "requirements": [ "requests==2.32.3" ],
                    "credentials": { "TOKEN": "GITHUB_TOKEN" },
                    "parameters": [ "cve-identifier" ]
                }
            ]
        }
    ]
}
```

- Every mcp-server is reachable under `<domain>/<url-namespace>`; a client of one can neither see nor reach the tools,
  resources and prompts of another, so the same tool-name may exist in two of them.
- `authorized-roles` states which roles may use it; `"anonymous": true` makes it usable by everybody. Declaring
  neither of the two is a mistake which fails the start.
- `type` is `python`, `shell` or `web-request` and has to be declared. It is never derived from a file-extension:
  which interpreter runs a script decides what the script may do.
- `calls` is resolved against the folder of the declaration and has to stay inside it.
- Every entry of `parameters` reaches the script as `--<name>=<value>` and is offered to the client as a required
  string.
- A declaration which runs a script (type `python` or `shell`) may state `mandatory-env-variables` and
  `optional-env-variables`: environment-variables of the server which reach the script. A mandatory one has to be set
  (the call fails otherwise), an optional one is handed over only when it is available. A script receives none of the
  server's environment by itself - only these, the credentials and a few os-variables (`PATH`, `HOME`, …).
- Every run of a script is logged to `<name>.log` in the log-folder (`GetLogFolder()`): the exact invocation (program
  plus arguments) and the output. The environment is not logged, so no credential and no declared
  environment-variable ends up there. Writing the log never fails a tool-call. The folder is passed into
  `BusinessLogicService`; the writing lives in `AppendToToolLog`.
- Next to `tools` an mcp-server may declare `resources` (read by a `uri`, no `parameters` - the uri is the whole
  request and reaches the script as `--uri=<uri>`), `prompts` (a text, `parameters` like a tool, handed over as one
  message of the role `user`) and, per prompt, `completions` (keyed by an argument of that prompt; the script is told
  `--argument=<name>` and `--value=<value>` and writes one value per line, at most 100 reach the client). A prompt and
  a completion are never `web-request`: such a declaration takes its address from the client or from its own uri, and
  neither has one.
- A tool may declare an `output-schema`. `Model/StructuredOutput.cs` checks every answer against it; an answer which
  is no json-document or does not fit is a failed tool-call. A resource, a prompt or a completion which declares one
  fails the start.
- A field which does not belong to the declared type (`requirements` for a shell-tool, `calls` for a
  web-request-tool, …) is an error and not something which is ignored.
- The shared part of all four declarations lives in `Model/CallableDefinition.cs`, the validation in
  `Services/ToolDefinitionsReader.cs`.

What the three types mean:

- `python`: the script runs in a virtual environment of its own (`Services/VirtualEnvironmentService.cs`), built at
  the first call and rebuilt when the declared `requirements` differ. The environments live below the data-folder,
  because an environment is data the server builds and not something an administrator edits.
- `shell`: run with `/bin/sh`, so linux only; on another system the tool answers with exactly that reason.
- `web-request`: no script (`Services/WebRequestService.cs`). The client states the address as the parameter `url`,
  the declaration states the allowed ones as regular expressions in `allowed-urls`. The protocol is always https,
  every rule is anchored at both ends, redirects are not followed and the answer is `{"status-code":…,"body":…}`.

## How an mcp-server comes into existence in the code

This is the central mechanism and deliberately not the usual one of the MCP-SDK. `[McpServerTool]` only works for
tools known at compile-time; here they are not.

- `Misc/ScriptTool.cs` derives from the abstract `McpServerTool` of the SDK, `Misc/ScriptResource.cs` from
  `McpServerResource`, `Misc/ScriptPrompt.cs` from `McpServerPrompt`.
- `Program.cs` builds one collection per declared mcp-server and sets the fitting one per request with
  `HttpServerTransportOptions.ConfigureSessionOptions` - that is what makes several mcp-servers in one process
  possible. An mcp-server without resources, without prompts respectively without completions gets no collection at
  all and therefore does not claim that capability of the protocol.
- The completions are the exception: the protocol has one request for all of them, so there is no SDK-primitive to
  derive from. `Misc/ScriptCompletionHandler.cs` is assigned to `serverOptions.Handlers.CompleteHandler` per request.

Everything else of the protocol stays with the SDK.

## Who may use an mcp-server

Three things decide it, kept apart on purpose: `ClientAdministrationTokens.csv` (`Services/ClientService.cs`) states
which clients exist, `Roles.csv` and `RoleAssignments.csv` (`Services/RolesService.cs`) which roles exist and who is in
them, `AccessTokens.csv` (`Services/AccessTokenService.cs`) which token belongs to which client and until when. All of
them live in the configuration-folder and are read for every request, so a change works without a restart - unlike the
declaration, which is read once at startup.

- `Controller/AdminController.cs` (`/API/v2/Admin`) manages clients, roles and assignments and needs the header
  `X-Admin-Token`. That token is created by `Services/AdminTokenService.cs` at the first start and written to the log
  while the file-targets are off, so it appears on the console and in no file. Creating a client answers with that
  client's administration-token - the only moment it is readable.
- `Controller/ClientAdministrationController.cs` (`/API/v2/ClientAdministration`) lets a client manage its own
  access-tokens and needs `X-Client-Administration-Token`; which client is calling follows from the token, so a client
  can not even name another one.
- Whether a client of that name already exists (when one is created) and whether it exists at all (when its
  administration-token is renewed) is decided in `BusinessLogicService` and not in `ClientService`: those are rules
  about the domain and not properties of the file the clients lie in. Each is checked in one place, so a caller
  receives one failure with one reason.
- **Every access to the csv-files goes through `BusinessLogicService` and is serialized there** by the semaphore
  `_AccessToTheConfigurationFiles`. It is held for a whole operation and not only for a single read or write, because
  the administration asks and writes afterwards (creating a client) and writes into three files in a row (deleting
  one). The semaphore is not re-entrant, so **a method which holds it must not call another method of that service
  which takes it as well** - it would wait for itself forever.

Only hashes are stored (`Misc/Token.cs`), compared in constant time. The client sends its access-token as
`Authorization: Bearer <token>`. Everything which is not allowed answers with 401 - which of the reasons it was is
nothing a caller learns. `Misc/AccessTokenValidatorMiddleware.cs` guards the routes but decides none of it itself:
every such decision is asked from `IBusinessLogicService`.

## Credentials of a tool

An access to a foreign service belongs to the operator, never to the client: it is an environment-variable of the
server (or a file a `<NAME>_FILE`-variable points at, which is how a docker-secret arrives), and the declaration
assigns it to a tool with two names - the one the tool sees and the one of the variable. A python-tool receives it as
an environment-variable, a web-request-tool uses it in a header with `${<name>}`, a shell-tool receives none.

The variables which credentials are read from are removed from the environment of every started script, because a
script does not inherit the environment of the server at all: it is cleared and rebuilt from a few os-variables
(`PATH`, `HOME`, …), the credentials and the declared environment-variables (`mandatory-env-variables` /
`optional-env-variables`), so no tool can read the access or the configuration of another. A missing credential keeps
the server from starting, and the values are removed from the message of a failed tool-call.

## Security-model of the arguments

The values of the parameters come from the client and are untrusted. These mechanisms carry that, and they are easy to
break by "improving" them:

- **No shell is involved.** `UseShellExecute` is false and every argument is its own entry of
  `ProcessStartInfo.ArgumentList` (`Misc/ProcessExecutor.cs`). Do not replace this by building a command-line and
  escaping: an escaping which is forgotten or applied twice is a bug of its own, while the absence of a shell can not
  be forgotten.
- **Every value is one token `--name=value`.** Passing option and value as two arguments would let a value which
  starts with a hyphen be read as an option.
- **The format is checked in the script**, as the `type`-function of the argument of `argparse`, with a regular
  expression anchored at both ends - so it also applies when the script is called directly.

**When testing this, use a canary and not a destructive payload.** A value which would create a file in a
scratch-folder is not only harmless but the stronger test: "the call answered with an error" only says something went
wrong, an empty canary-folder says nothing was executed.

**What a declared tool does is the responsibility of the administrator who declared it.** The product runs what a
declaration names and does not check what it does.

## How a running tool-call is cancelled

A `notifications/cancelled` stops the program behind the named tool-call - the script together with everything it
started, respectively the web-request. **A declared tool needs nothing for it.**

The SDK can not carry this: the transport is stateless, so the call and its cancellation are two http-requests
answered by two instances of the SDK-server, and the SDK matches a cancellation to a request only within one session.

- `Misc/RunningToolCalls.cs` is the registry of the running calls, one for the whole process - that is what the two
  requests have in common.
- `Misc/ToolCallIdentity.cs` identifies a call: request-id plus mcp-server plus calling client, because a request-id is
  only unambiguous within the client which chose it.
- `Misc/ToolCallCancellationHandler.cs` reads the notification and is built per request. A notification naming a call
  which is not running stops nothing and is no failure.
- `Misc/ProcessTree.cs` ends the started program. `Process.Kill(true)` alone is not enough: it ends the program first
  and walks its children afterwards, so a child which already ended is one whose own children it does not look for. On
  windows that is the normal case, because the interpreter of a virtual environment is a launcher which takes the real
  interpreter down with itself. The started program is therefore assigned to a job-object which is ended as a whole;
  on linux the tree of the runtime is used. **Do not replace this by `Kill(true)` again** - the testcase
  `AProgramWhichTheScriptOfACancelledCallStartedIsStoppedAsWell` is what states the difference.

Only a tool-call is stopped. A read of a resource, a prompt and a completion are answered in one go, and the
notification names a request-id and not what that request was about.

## How a failure is answered

A failed tool-call is a `CallToolResult` with `IsError` plus the http-status-code 502
(`Misc/ToolCallFailureStatusCodeMiddleware.cs`), because the failure comes from the program behind this server. A
failed read of a resource, a prompt which could not be built and a completion which did not work are answered as an
error of the protocol (`McpException`) with the status-code of a normal answer: a client which asked for a document,
a text respectively the possible values of an argument must not receive the reason of a failure as if it were one of
them. A call which the client cancelled is neither: that reason with the status-code of a normal answer, because
nothing failed.

## Building, testing, running

- `task bb` (`scbuildcodeunits`) builds everything: both code-units, their testcases, the linting and the image. For a
  single code-unit, run the scripts in `<code-unit>/Other/Build` and `<code-unit>/Other/QualityCheck` from the folder
  they are in.
- A manual `dotnet build` / `dotnet test` in `GenericMCPServerBackend` works as it is and changes no git-tracked file.
  A script which still passes `-p:RestorePackagesWithLockFile=false` describes a problem which does not exist anymore.
- The unit-tests live in `GenericMCPServerBackendTests` and are run by `RunTestcases.py`. They bring their own scripts
  and their own declaration in a temporary folder: a testcase which used an example-tool of the repository would test
  the example instead of the mechanism. `TestUtilities/MCPServerWrapper.cs` puts the tools of a testcase into one
  mcp-server. The integration-tests start a server on a fixed port and serialize themselves with
  `IntegrationTestServerLock`.
- That a cancellation really stopped something is proven with a canary (`TestUtilities/WaitingScript.cs`) and not with
  the answer of the call, which would only state that whoever waited stopped waiting.
- `task bma` starts the minimal example, `task bae` the advanced one, `task bmat`/`task baet` stop them. They live in
  `GenericMCPServer/Other/Reference/ReferenceContent/Examples`; the minimal one declares one anonymous mcp-server on
  `http://127.0.0.1:1222/GeneralTools`, the advanced one three. Their scripts can also be called directly, which is
  the fastest way to see what a tool answers.
- For a manual test against a locally started backend:

```
dotnet GenericMCPServerBackend.dll Run --RealRun true
curl -X POST http://127.0.0.1:80/GeneralTools -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

  The `Accept`-header has to name both content-types: the transport answers as an event-stream, and the answer arrives
  as a line which starts with `data: `. A non-anonymous mcp-server additionally needs
  `-H "Authorization: Bearer <token>"`.

## Things which are easy to get wrong

- **A start which was aborted poisons every following one.** If a start fails before
  `Other/Workspace/Configuration/Configuration.xml` is complete, every following start fails with a
  `NullReferenceException` in the logger (`GRYLogSubNamespaceProvider`), because the log-configuration in it is null.
  The message points at the logger and not at the cause. Delete `Other/Workspace` and start again.
- **GRYLibrary needs assemblies at runtime which this code-unit does not use itself.**
  `Microsoft.EntityFrameworkCore.Abstractions` (the xml-serializer resolves attributes of its types while loading the
  configuration) and `prometheus-net.AspNetCore` are referenced although there is neither a database nor a
  metrics-endpoint. Removing them makes the build succeed and the start fail with a `FileNotFoundException`.
- **The declaration and its scripts belong together.** A script is resolved relative to the file which declares it, so
  handing only the declaration to a container leaves every tool which is a script broken. Only a declaration whose
  tools are web-requests is complete on its own.
- **The maintenance-health-check-route needs `AddHealthChecks()`.** Without it
  `/API/Other/Maintenance/HealthCheck` answers with 500, and that route is what the `HEALTHCHECK` of the image and
  `Other/OnBuildingFinished.py` use. It stays reachable without an access-token for exactly that reason.
- **The tool-name is what a model sees.** Choose it as carefully as the description: both are the only information a
  model has to decide whether a tool fits a task.
