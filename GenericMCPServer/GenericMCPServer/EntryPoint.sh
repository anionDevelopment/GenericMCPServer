#!/bin/bash

export IsRunningInContainer=true

argument="Run --RealRun true"

if [[ -n "${InitialDomain}" ]]; then
    argument+=" --InitialDomain $InitialDomain"
fi

if [[ -n "${InitialEnableEndpointAvailabilityCheckValue}" ]]; then
    argument+=" --InitialEnableEndpointAvailabilityCheckValue $InitialEnableEndpointAvailabilityCheckValue"
fi

if [[ -n "${InitialEnableEndpointInitializationStateValue}" ]]; then
    argument+=" --InitialEnableEndpointInitializationStateValue $InitialEnableEndpointInitializationStateValue"
fi

if [[ -n "${InitialEnableEndpointCurrentVersionValue}" ]]; then
    argument+=" --InitialEnableEndpointCurrentVersionValue $InitialEnableEndpointCurrentVersionValue"
fi

if [[ -n "${InitialEnableEndpointShowAllEndpointsValue}" ]]; then
    argument+=" --InitialEnableEndpointShowAllEndpointsValue $InitialEnableEndpointShowAllEndpointsValue"
fi

if [[ -n "${InitialEnableEndpointHealthCheckValue}" ]]; then
    argument+=" --InitialEnableEndpointHealthCheckValue $InitialEnableEndpointHealthCheckValue"
fi

if [[ -n "${InitialEnableEndpointMetricsValue}" ]]; then
    argument+=" --InitialEnableEndpointMetricsValue $InitialEnableEndpointMetricsValue"
fi

if [[ "${EnforceVerbose}" == "true" ]]; then
    argument+=" --EnforceVerbose true"
fi

cd /Workspace/Application/Backend && exec dotnet ./GenericMCPServerBackend.dll $argument
