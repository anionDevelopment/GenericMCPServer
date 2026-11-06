# Reference of GenericMCPServer

## Features

The following table lists the features which can typically be expected from a product of this kind (a generic host for
MCP-servers). The state is derived from the current sourcecode.

| id      | state       | description |
| ------- | ----------- | ----------- |
| feat-1  | available   | Host multiple independent MCP-servers in one process, each reachable under its own url-namespace. |
| feat-2  | available   | Fully generic tool-declaration: tools, resources and prompts are declared in a json-file without any change to the sourcecode. |
| feat-3  | available   | Provide MCP-tools. |
| feat-4  | available   | Provide MCP-resources (read by uri). |
| feat-5  | available   | Provide MCP-prompts. |
| feat-6  | available   | Provide completions for the arguments of a prompt. |
| feat-7  | available   | Tools of type `python`, each running in its own virtual environment built from declared requirements. |
| feat-8  | available   | Tools of type `shell` (executed with `/bin/sh`, therefore only on linux). |
| feat-9  | available   | Tools of type `web-request` with an allow-list of permitted urls (https only). |
| feat-10 | available   | Validate the answer of a tool against a declared `output-schema` (structured output). |
| feat-11 | available   | Inject operator-owned credentials/secrets into a tool (also from a `<NAME>_FILE`-variable, e.g. a docker-secret). |
| feat-12 | available   | Pass declared mandatory and optional server environment-variables into a script, isolated from the rest of the server-environment. |
| feat-13 | available   | Role-based authorization which decides per client and per MCP-server who may use which tools. |
| feat-14 | available   | Anonymous MCP-servers which are usable without any token. |
| feat-15 | available   | Administration of clients, roles and role-assignments via REST-API (secured by an admin-token). |
| feat-16 | available   | Per-client management of access-tokens with an expiry via REST-API. |
| feat-17 | available   | Changes to clients, roles and tokens take effect without a restart of the server. |
| feat-18 | available   | Cancellation of a running tool-call (stops the started program together with all its child-processes). |
| feat-19 | available   | Per-tool-call logging of the exact invocation and the output into `<tool-name>.log` (without credentials/environment). |
| feat-20 | available   | Stateless HTTP-transport, so an MCP-server is usable with a plain http-request. |
| feat-21 | available   | Health-check-endpoint for container-orchestration and monitoring. |
| feat-22 | available   | Metrics-endpoint (present but disabled by default). |
| feat-23 | unavailable | Built-in HTTPS/TLS-termination (the server speaks plain HTTP and is meant to run behind a reverse-proxy). |
| feat-24 | unavailable | Sandboxed/isolated execution of tools (tools run directly on the host; isolation is the responsibility of the administrator). |
| feat-25 | unavailable | Hot-reload of the tool-declaration without a restart (the declaration is read once at startup). |
| feat-26 | unavailable | Web-based administration-ui/dashboard (administration is done via the REST-API). |

## Findings

| Id  | Priority | State | Description | Impact | Notes |
| --- | -------- | ----- | ----------- | ------ | ----- |
