# Answers with the current point in time in utc.
#
# This script is one of the tools the MCP-server offers and it is the example of a tool of the type "shell". It takes no
# argument, so it also shows the smallest form such a tool can have: everything it needs is part of the system it runs
# on.
#
# A shell-tool is run with "/bin/sh", so it does not have to carry the permission to be executed - which a file that is
# mounted into a container usually does not.
date -u +%Y-%m-%dT%H:%M:%SZ
