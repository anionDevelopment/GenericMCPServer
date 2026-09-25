---
name: product-knowledge
description: What GenericMCPServer is, how this repository is structured, how an mcp-server with its tools, resources and prompts is declared and which mechanisms exist for building, testing and running it. Use this before fixing a defect or developing a feature in this repository, to know where things belong and how to verify a change.
---

# GenericMCPServer

GenericMCPServer hosts MCP-servers which are reachable over HTTP. It offers exactly the tools, resources and prompts
which an administrator declares; it brings none of them of its own along, which is what the "generic" in its name
means.

A declaration names an mcp-server, the tools it offers, what implements each of them and which roles may use it. A model
which talks to the server therefore sees tools like `get_cve_details` or `get_npm_versions`, although nothing in the
sourcecode knows those names.

## Structure of the repository

The repository follows the "common project structure": all sourcecode lives in code-units, and every code-unit has its
own `Other`-folder with its build-, quality-check- and reference-files. Use the
`work-with-common-project-structure`-skill when you need the details of that structure.

The code-units are:

- `GenericMCPServerBackend` — the dotnet-api-server. It implements the wiring around the MCP-protocol, hosts the
  declared mcp-servers and runs what is behind their tools.
- `GenericMCPServer` — the container-image which packages the backend.

## How an mcp-server and its tools come into existence

This is the central mechanism of the product, and it is deliberately not the usual one of the MCP-SDK.

The SDK offers `[McpServerTool]` on a method, which only works for tools which are known at compile-time. Here they are
not, so `Misc/ScriptTool.cs` derives from the abstract `McpServerTool` of the SDK and overrides `ProtocolTool`,
`Metadata` and `InvokeAsync`. The tools are not registered once for the whole process either: `Program.cs` builds one
tool-collection per declared mcp-server and sets the fitting one per request with
`HttpServerTransportOptions.ConfigureSessionOptions`, which is what makes several mcp-servers in one process possible.
Everything else of the protocol (the initialize-handshake, `tools/list`, `tools/call`, the delivery of the schemas)
stays with the SDK.

The resources work exactly the same way: `Misc/ScriptResource.cs` derives from `McpServerResource` and overrides
`ProtocolResourceTemplate`, `Metadata`, `IsMatch` and `ReadAsync`, and `Program.cs` sets a `ResourceCollection` per
request. An mcp-server which declares no resource gets no collection at all, so it does not claim the
resources-capability of the protocol.

The prompts work the same way once more: `Misc/ScriptPrompt.cs` derives from `McpServerPrompt` and overrides
`ProtocolPrompt`, `Metadata` and `GetAsync`, and `Program.cs` sets a `PromptCollection` per request - again only for an
mcp-server which declares at least one prompt, so one without prompts does not claim the prompts-capability.

The declaration is a json-file whose path (`ToolDefinitionsFile`) is part of the persisted configuration-file of the
server, defaulting to `./Tools.json` (relative to the configuration-folder) the first time that file is
generated:

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

- Every mcp-server is reachable under `<domain>/<url-namespace>`; a client of one of them can neither see nor
  reach the tools and resources of another one, so the same tool-name may exist in two of them.
- Next to `tools` an mcp-server may declare `resources`: content which a client reads by a uri. Such a declaration
  states `uri`, `resource-name`, `type`, `mime-type` and (for a script) `calls`, it declares no `parameters` (the uri
  is the whole request and is handed to the script as `--uri=<uri>`), and a resource of the type `web-request`
  declares no `allowed-urls` because its own uri is the address which is requested.
- Next to those it may declare `prompts`: a text which a client asks for by its name and hands to its model. Such a
  declaration states `prompt-name`, `type`, `description`, `calls` and its `parameters`, which reach the script exactly
  like the parameters of a tool; what the script writes is the text of the prompt, handed over as one message of the
  role `user`. A prompt is never of the type `web-request`, because such a declaration takes the address it requests
  from the client (a tool) or from its own uri (a resource) and a prompt has neither. The shared part of all three
  declarations lives in `Model/CallableDefinition.cs`.
- `authorized-roles` states which roles may use the mcp-server; `"anonymous": true` makes it usable by everybody and
  then no role is needed. An mcp-server which is neither anonymous nor names a role is a mistake and fails the start.
- `type` is `python`, `shell` or `web-request` and has to be declared. It is never derived from the extension of a
  file: which interpreter runs a script decides what the script may do.
- `calls` is resolved against the folder of the declaration and has to stay inside it.
- Every entry of `parameters` is passed to the script as `--<name>=<value>`, so it has to be the name of an option the
  script accepts. Every parameter is offered to the client as a required string.
- A field which does not belong to the declared type (`requirements` for a shell-tool, `calls` for a
  web-request-tool, `headers` for a python-tool, …) is an error and not something which is ignored.

What the three types mean:

- `python`: the script runs in a virtual environment which belongs to that declaration alone
  (`Services/VirtualEnvironmentService.cs`). It is built when the tool is called for the first time and built again as
  soon as the declared `requirements` differ from the ones it was built with — otherwise a changed declaration would be
  without effect. The environments live below the data-folder of the server, not next to the scripts: that folder is
  usually mounted read-only. `--InitialVirtualEnvironmentsFolder` moves them.
- `shell`: the script is run with `/bin/sh`, which requires a linux-system; on another system the tool answers with
  exactly that reason. Running it with the shell also means the file needs no execute-permission.
- `web-request`: no script at all (`Services/WebRequestService.cs`). The client states the address as the parameter
  `url`, the declaration states the allowed ones as regular expressions in `allowed-urls`. The protocol is always
  https, every rule is matched anchored at both ends (`"^" + rule + "$"`), everything which no rule allows is denied,
  redirects are not followed (the address behind them was never checked) and the answer is
  `{"status-code":…,"body":…}`.

A mistake in the file (an mcp-server without a url-namespace, a url-namespace which is used twice, a tool without a
name or without a type, an unknown type, a tool-name or a prompt-name which is used twice within one mcp-server, a
prompt of the type `web-request`, invalid json) makes the server fail on startup. A missing file is not such a mistake: neither no file being configured nor the configured
file not existing at its resolved location stops the server - both mean "nothing is offered", because a freshly set
up server which nobody pointed at a declaration yet is an expected state.

**When adding a tool, a resource or a prompt, nothing in the backend changes.** If you find yourself writing a tool-name into a c#-file, the
change is wrong: run `grep -rniE "cve|maven|nuget|npm" GenericMCPServerBackend --include=*.cs` — it has to stay empty.

## Who may use an mcp-server

Three things decide it, and they are deliberately kept apart:

- `ClientAdministrationTokens.csv` (`Services/ClientService.cs`): which clients exist and with which token each of them
  administrates itself.
- `Roles.csv` and `RoleAssignments.csv` (`Services/RolesService.cs`): which roles exist and which client is in which of
  them. A role exists before anybody is in it, so an mcp-server can be declared for a role whose clients are created
  later.
- `AccessTokens.csv` (`Services/AccessTokenService.cs`): which access-token belongs to which client and until when it is
  valid.

A client reaches an mcp-server when its access-token is valid and it is in at least one of the roles which the
declaration of that mcp-server states. All files live in the configuration-folder of the server (which is not
stated from outside), are created when they do not exist yet and are read for every request, so a change works
immediately without a restart. A line which can not be read is an error and is not skipped.

Everything is administrated over the api and not on the commandline:

- `Controller/AdminController.cs` (`/API/v1/Admin`) manages clients, roles and assignments. It needs the header
  `X-Admin-Token` with the token of the administrator. That token is created by `Services/AdminTokenService.cs` at the
  first start which finds no `AdminToken.csv` and is written to the log while the file-targets of the log are switched
  off, so it appears on the console and in no file; only its hash is stored. Creating a client answers with the
  client-administration-token of that client - the only moment at which it is readable.
- `Controller/ClientAdministrationController.cs` (`/API/v1/ClientAdministration`) lets a client create and remove its
  own access-tokens. It needs the header `X-Client-Administration-Token`; which client is calling follows from that
  token, so a client can not even name another one.

Only hashes are stored (`Misc/Token.cs`), and the comparison of a token with a hash runs in a constant time.

The client sends its access-token as `Authorization: Bearer <token>` (the header `X-Authorization` of GRYLibrary works
as well). Everything which is not allowed answers with 401 — whether the token was missing, unknown, expired or
belonged to a client without a fitting role is nothing a caller learns.

`Misc/AccessTokenValidatorMiddleware.cs` guards the routes of the mcp-servers, the admin-routes and the
client-administration-routes, but it decides none of who-may-reach-what itself: every such decision (whether a token
matches the administrator, which client an access- or administration-token belongs to, whether that client's roles
fit the mcp-server) is asked from `IBusinessLogicService`, which is also where `AdminController` and
`ClientAdministrationController` get the administration of clients, roles and tokens from - a controller or a
middleware which decided one of those things itself would be a decision the next one could forget. The middleware
derives from the api-key-middleware of GRYLibrary and overrides `Invoke`, because the base-class decides by the
`[Authorize]`-attributes of controller-actions and the route of an mcp-server is no controller-action.

## Credentials of a tool

An access to a foreign service belongs to the operator and never to the client: it is set as an environment-variable of
the server (or as a file which a `<NAME>_FILE`-variable points at, which is how a docker-secret is handed over), and
the declaration assigns it to a tool with two names — the one the tool sees and the one of the variable. A python-tool
receives it as an environment-variable, a web-request-tool uses it in a header with `${<name>}`, a shell-tool receives
none.

The variables which credentials are read from are removed from the environment of every started script before the ones
of that tool are added, so a tool can not read the access of another tool. A missing credential keeps the server from
starting. The values are removed from the message of a failed tool-call before it reaches the client.

## Security-model of the arguments

The values of the parameters come from the client and are untrusted. Two mechanisms carry that, and both are easy to
break by "improving" them:

- **No shell is involved.** `UseShellExecute` is false and every argument is added as its own entry of
  `ProcessStartInfo.ArgumentList` (see `Misc/ProcessExecutor.cs`), so the operating-system hands each of them to the
  script as exactly one argument. Do not replace this by building a command-line and escaping the values: an escaping
  which is forgotten or applied twice is a bug of its own, while the absence of a shell can not be forgotten.
- **Every value is one token in the form `--name=value`.** Passing the option and the value as two arguments would let
  a value which starts with a hyphen be read as an option.
- **The format is checked in the script**, as the `type`-function of the argument of `argparse`, with a regular
  expression which is anchored at both ends. It lives there and not in the backend, so it also applies when the script
  is called directly and so that the format is defined once, next to the code which depends on it.

A failure is answered as a `CallToolResult` with `IsError` and the message of the script, and additionally with the
http-status-code 502 for the whole answer (`Misc/ToolCallFailureStatusCodeMiddleware.cs`), because the failure comes
from the program behind this server. A failed read of a resource and a prompt which could not be built are answered as
an error of the protocol (`McpException`, whose message is the one which reaches the client) with the status-code of a
normal answer: a result which carries an error-flag does not exist for either of them, and a client which asked for a
document or for a text must not receive the reason of a failure as if it were that document respectively that text.

**When testing this, use a canary and not a destructive payload.** A payload like `; rm -rf /` tests a protection with
a failure-mode which destroys the machine if the protection does not hold. Use a value which would create a file in a
scratch-folder instead and check afterwards whether that file exists — that is not only harmless, it is the stronger
test: "the call answered with an error" only says that something went wrong, an empty canary-folder says that nothing
was executed.

**What a declared tool does is the responsibility of the administrator who declared it.** The product runs what a
declaration names and does not check what it does; `GenericMCPServer/ReadMe.md` states that explicitly.

## Building

`scbuildcodeunits` builds everything; the task `task bb` does the same. For a single code-unit, run the scripts in
`<code-unit>/Other/Build` and `<code-unit>/Other/QualityCheck` from the folder they are located in.

A manual `dotnet build` or `dotnet test` has to pass `-p:RestorePackagesWithLockFile=false`, because the lock-files of
this repository are the runtime-specific ones (`packages.<rid>.lock.json`) and a restore would otherwise write one
under the default-name, which the next build rejects with NU1005.

The backend needs no certificate: it serves plain HTTP, and `Other/CommonTasks.py` therefore passes
`CertificateGeneratorInformationNoGenerate`. The image needs a python-interpreter including `venv`, because a declared
tool is usually a python-script which runs in a virtual environment of its own.

## Testing

The unit-tests live in `GenericMCPServerBackendTests` and are executed by `RunTestcases.py`. They bring their own
scripts and their own declaration in a temporary folder: a testcase which used one of the example-tools of the
repository would test the example instead of the mechanism. `TestUtilities/MCPServerWrapper.cs` puts the tools of a
testcase into one mcp-server, so a testcase states only what it is about.

For a manual test, start the backend and talk to it directly:

```
dotnet GenericMCPServerBackend.dll Run --RealRun true
curl -X POST http://127.0.0.1:80/GeneralTools -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

The `Accept`-header has to name both content-types: the transport answers as an event-stream, and the answer arrives as
a line which starts with `data: `. An mcp-server which is not anonymous additionally needs
`-H "Authorization: Bearer <token>"`.

The transport runs in the stateless mode (`HttpServerSessionMode.Stateless`), so no session-id has to be kept and every
request can be sent on its own.

`GenericMCPServer/Other/Examples/MinimalDockerComposeFile` contains the example: three mcp-servers (`GeneralTools`
anonymous, `Security` and `DeveloperTools`), their scripts and a compose-file which mounts both into the container. The
scripts can also be called directly, which is the fastest way to see what a tool answers:

```
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
```

## Things which are easy to get wrong

- **A start which was aborted poisons every following one.** The server writes its configuration to
  `GenericMCPServerBackend/Other/Workspace/Configuration/Configuration.xml`. If a start fails before that file is
  complete, every following start fails with a `NullReferenceException` in the logger (`GRYLogSubNamespaceProvider`),
  because the log-configuration in it is null. The message points at the logger and not at the actual cause. Delete the
  folder `Other/Workspace` and start again.
- **GRYLibrary needs assemblies at runtime which this codeunit does not use itself.**
  `Microsoft.EntityFrameworkCore.Abstractions` (its types carry attributes which the xml-serializer resolves while
  loading the configuration) and `prometheus-net.AspNetCore` are referenced although there is neither a database nor a
  metrics-endpoint here. Removing them makes the build succeed and the start fail with a `FileNotFoundException` about
  a missing assembly.
- **The declaration and the scripts belong together.** A script is resolved relative to the file which declares it, so
  mounting only the declaration into the container leaves every tool broken.
- **The maintenance-health-check-route needs `AddHealthChecks()`.** Without it
  `/API/Other/Maintenance/HealthCheck` answers with 500, and that route is what the `HEALTHCHECK` of the image and
  `Other/OnBuildingFinished.py` use. It stays reachable without an access-token for exactly that reason.
- **The tool-name is what a model sees.** It is worth choosing it as carefully as the description: both are the only
  information a model has to decide whether a tool fits a task.
