#!/bin/bash
set -o errexit
configfile="/Workspace/Configuration/Configuration.xml"

protocol=$(sed -n 's/.*<Protocol[^>]*exs:type="ns[0-9]\+:\([^"]*\)".*/\1/p' $configfile)
protocol="${protocol,,}" # make protocol to lower
if [[ -z "$protocol" ]]; then
  echo "Protocol for healthcheck not available."
  exit 3
fi

port=$(sed -n 's/.*<Protocol[^>]*Port="\([^"]*\)".*/\1/p' $configfile)
if [[ -z "$port" ]]; then
  echo "Port for healthcheck not available."
  exit 4
fi

healthcheckcommand="curl --fail -v"
if [ "$protocol" == "https" ]; then
    healthcheckcommand="$healthcheckcommand --insecure"
fi
healthcheckcommand="$healthcheckcommand \"$protocol://127.0.0.1:$port/API/Other/Maintenance/HealthCheck\""
echo "health-check-command: $healthcheckcommand"
eval $healthcheckcommand
