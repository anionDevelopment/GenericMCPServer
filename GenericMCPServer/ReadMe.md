# GenericMCPServer

GenericMCPServer hosts the MCP-servers which an administrator declares, reachable over HTTP.

This codeunit contains the definition of the container-image which packages [GenericMCPServerBackend](../GenericMCPServerBackend/ReadMe.md).

The built image is published on docker hub as
[aniondev/genericmcpserver](https://hub.docker.com/r/aniondev/genericmcpserver).

## The tools are the responsibility of the administrator

**This product runs the tools which an administrator declares. It does not check what they do, and it can not do so.**

A declared tool is a program on the server: a python-script, a shell-script or a request to an address. It runs with
the rights of the server, it can read and change what the server can read and change, and it can reach whatever the
network of the server reaches. The product decides which tool exists and which values reach it - it never decides
whether what the tool does is a good idea. **A declared resource, a declared prompt and a declared provider of the
values of an argument of a prompt are the same kind of program**, only that a client reads the first by a uri instead of
calling it with values, asks for the second by its name to hand its text to a model and receives from the third the
values it offers while somebody fills in that argument.

**Whoever declares a tool is responsible for it**, and that responsibility covers at least these questions:

- What does the script do, including everything it calls and every package it installs as a requirement?
- Who may declare a tool and who may change the folder which holds the declarations and the scripts?
- Which addresses does a tool of the type `web-request` allow? A rule like `".*"` lets a client reach every address
  which the server reaches, including the ones inside its own network.
- Which credentials does a tool, a resource, a prompt or such a provider of values receive, and what can be done with
  them if the script is not what it seemed?
- Who holds an access-token, who is in which role, and which mcp-servers are declared as anonymous and are
  therefore usable by everybody?

A tool, a resource, a prompt or such a provider of values from an unknown source is a program from an unknown source. It
is to be read before it is declared, in the same way as any other program which is put onto a server.

## Reference

GenericMCPServer-specific-reference including development-hints can be found [here](./Other/Reference/ReferenceContent/index.md).
