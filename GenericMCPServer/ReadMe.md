# GenericMCPServer

GenericMCPServer is a simple MCP-server which is reachable over HTTP.

This codeunit contains the definition of the container-image which packages [GenericMCPServerBackend](../GenericMCPServerBackend/ReadMe.md).

## The tools are the responsibility of the administrator

**This product runs the tools which an administrator declares. It does not check what they do, and it can not do so.**

A declared tool is a program on the server: a python-script, a shell-script or a request to an address. It runs with
the rights of the server, it can read and change what the server can read and change, and it can reach whatever the
network of the server reaches. The product decides which tool exists and which values reach it - it never decides
whether what the tool does is a good idea.

**Whoever declares a tool is responsible for it**, and that responsibility covers at least these questions:

- What does the script do, including everything it calls and every package it installs as a requirement?
- Who may declare a tool and who may change the folder which holds the declarations and the scripts?
- Which addresses does a tool of the type `web-request` allow? A rule like `".*"` lets a client reach every address
  which the server reaches, including the ones inside its own network.
- Which credentials does a tool receive, and what can be done with them if the script is not what it seemed?
- Who holds an access-token, who is in which role, and which mcp-servers are declared as anonymous and are
  therefore usable by everybody?

A tool from an unknown source is a program from an unknown source. It is to be read before it is declared, in the same
way as any other program which is put onto a server.

## Reference

GenericMCPServer-specific-reference including development-hints can be found [here](./Other/Reference/ReferenceContent/index.md).
