---
name: product-knowledge
description: What GenericMCPServer is, how this repository is structured, how a tool is declared and which mechanisms exist for building, testing and running it. Use this before fixing a defect or developing a feature in this repository, to know where things belong and how to verify a change.
---

# GenericMCPServer

GenericMCPServer is a MCP-server which is reachable over HTTP. It offers exactly the tools which an
administrator declares; it brings no tool of its own along, which is what the "generic" in its name means.

A declaration states the name of a tool, the script which implements it, what the tool does and which
parameters it takes. A model which talks to the server therefore sees tools like `get_cve_details` or
`get_npm_versions`, although nothing in the sourcecode knows those names.

## Structure of the repository

The repository follows the "common project structure": all sourcecode lives in code-units, and every code-unit
has its own `Other`-folder with its build-, quality-check- and reference-files. Use the
`work-with-common-project-structure`-skill when you need the details of that structure.

The code-units are:

- `GenericMCPServerBackend` — the dotnet-api-server. It implements the wiring around the MCP-protocol and runs
  the scripts which implement the declared tools.
- `GenericMCPServer` — the container-image which packages the backend.

## How a tool comes into existence

This is the central mechanism of the product, and it is deliberately not the usual one of the MCP-SDK.

The SDK offers `[McpServerTool]` on a method, which only works for tools which are known at compile-time. Here
they are not, so `Misc/ScriptTool.cs` derives from the abstract `McpServerTool` of the SDK and overrides
`ProtocolTool`, `Metadata` and `InvokeAsync`. `Program.cs` creates one instance per declaration and hands them
over with `WithTools(IEnumerable<McpServerTool>)`. Everything else of the protocol (the initialize-handshake,
`tools/list`, `tools/call`, the delivery of the schemas) stays with the SDK.

The declaration is a json-file which the server is pointed to with `--InitialToolDefinitionsFile` (in the
image: the environment-variable of the same name):

```json
{
    "tools": [
        {
            "tool-name": "get_cve_details",
            "calls": "GetCVEDetails.py",
            "description": "Returns the details of a CVE from the national vulnerability database.",
            "parameters": [ "cve-identifier" ]
        }
    ]
}
```

- `calls` is resolved against the folder of the declaration and has to stay inside it.
- Every entry of `parameters` is passed to the script as `--<name>=<value>`, so it has to be the name of an
  option the script accepts.
- Every parameter is offered to the client as a required string. What a value actually has to look like is
  checked by the script, not by the backend.

A mistake in the file (a tool without a name, a tool without a script, a name which is used twice, invalid
json, a configured file which does not exist) makes the server fail on startup. Only *no* configured file means
"no tools" — that is the difference between "nothing was declared" and "the declaration is broken".

**When adding a tool, nothing in the backend changes.** If you find yourself writing a tool-name into a
c#-file, the change is wrong: run
`grep -rniE "cve|maven|nuget|npm" GenericMCPServerBackend --include=*.cs` — it has to stay empty.

## Security-model of the arguments

The values of the parameters come from the client and are untrusted. Two mechanisms carry that, and both are
easy to break by "improving" them:

- **No shell is involved.** `UseShellExecute` is false and every argument is added as its own entry of
  `ProcessStartInfo.ArgumentList`, so the operating-system hands each of them to the script as exactly one
  argument. Do not replace this by building a command-line and escaping the values: an escaping which is
  forgotten or applied twice is a bug of its own, while the absence of a shell can not be forgotten. The
  program which is started has to stay a real executable — for a batch-file windows would parse the
  command-line again and the property would be lost.
- **Every value is one token in the form `--name=value`.** Passing the option and the value as two arguments
  would let a value which starts with a hyphen be read as an option.
- **The format is checked in the script**, as the `type`-function of the argument of `argparse`, with a regular
  expression which is anchored at both ends. It lives there and not in the backend, so it also applies when the
  script is called directly and so that the format is defined once, next to the code which depends on it.

A failure is answered as a `CallToolResult` with `IsError` and the message of the script, and not by letting an
exception escape: the SDK would replace it by a generic text, and a model can only correct a wrong argument if
it learns what was wrong.

**When testing this, use a canary and not a destructive payload.** A payload like `; rm -rf /` tests a
protection with a failure-mode which destroys the machine if the protection does not hold. Use a value which
would create a file in a scratch-folder instead and check afterwards whether that file exists — that is not
only harmless, it is the stronger test: "the call answered with an error" only says that something went wrong,
an empty canary-folder says that nothing was executed.

## Building

`scbuildcodeunits` builds everything; the task `task bb` does the same. For a single code-unit, run the scripts
in `<code-unit>/Other/Build` and `<code-unit>/Other/QualityCheck` from the folder they are located in.

The backend needs no certificate: it serves plain HTTP, and `Other/CommonTasks.py` therefore passes
`CertificateGeneratorInformationNoGenerate`. The image needs a python-interpreter, because a declared tool is
usually a python-script.

## Testing

The unit-tests live in `GenericMCPServerBackendTests` and are executed by `RunTestcases.py`. They bring their
own script and their own declaration in a temporary folder: a testcase which used one of the example-tools of
the repository would test the example instead of the mechanism.

For a manual test, start the backend and talk to it directly:

```
dotnet GenericMCPServerBackend.dll Run --RealRun true --InitialToolDefinitionsFile <path-to-Tools.json>
curl -X POST http://127.0.0.1:80/API/v1/MCP -H "Content-Type: application/json" -H "Accept: application/json, text/event-stream" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}"
```

The `Accept`-header has to name both content-types: the transport answers as an event-stream, and the answer
arrives as a line which starts with `data: `.

The transport runs in the stateless mode (`HttpServerSessionMode.Stateless`), so no session-id has to be kept
and every request can be sent on its own. If that is ever switched to stateful, a plain curl-call stops working
without the id which the initialize-answer returns.

`GenericMCPServer/Other/Examples/MinimalDockerComposeFile` contains five example-scripts, the declaration which
turns them into tools and a compose-file which mounts both into the container. The scripts can also be called
directly, which is the fastest way to see what a tool answers:

```
python GetCVEDetails.py --cve-identifier=CVE-2021-44228
```

## Things which are easy to get wrong

- **A start which was aborted poisons every following one.** The server writes its configuration to
  `GenericMCPServerBackend/Other/Workspace/Configuration/Configuration.xml`. If a start fails before that file
  is complete, every following start fails with a `NullReferenceException` in the logger
  (`GRYLogSubNamespaceProvider`), because the log-configuration in it is null. The message points at the logger
  and not at the actual cause. Delete the folder `Other/Workspace` and start again.
- **GRYLibrary needs assemblies at runtime which this codeunit does not use itself.**
  `Microsoft.EntityFrameworkCore.Abstractions` (its types carry attributes which the xml-serializer resolves
  while loading the configuration) and `prometheus-net.AspNetCore` are referenced although there is neither a
  database nor a metrics-endpoint here. Removing them makes the build succeed and the start fail with a
  `FileNotFoundException` about a missing assembly.
- **The declaration and the scripts belong together.** A script is resolved relative to the file which declares
  it, so mounting only the declaration into the container leaves every tool broken.
- **The maintenance-health-check-route needs `AddHealthChecks()`.** Without it
  `/API/Other/Maintenance/HealthCheck` answers with 500, and that route is what the `HEALTHCHECK` of the image
  and `Other/OnBuildingFinished.py` use.
- **The tool-name is what a model sees.** It is worth choosing it as carefully as the description: both are the
  only information a model has to decide whether a tool fits a task.
