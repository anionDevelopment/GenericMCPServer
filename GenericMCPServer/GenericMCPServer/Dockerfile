ARG image_debian
FROM ${image_debian}
ARG CodeUnitVersion
ARG DotNetRuntime
LABEL version=${CodeUnitVersion}
ENV ISRUNNINGINCONTAINER="true"
ENV ASPNETCORE_URLS="http://+:80"
RUN mkdir /Workspace && \
    mkdir /Workspace/Application && \
    mkdir /Workspace/Application/Backend && \
    mkdir /Workspace/Configuration && \
    mkdir /Workspace/Logs && \
    mkdir /Workspace/Other && \
    mkdir /Workspace/Other/EntryPoint && \
    apt-get update && \
    apt-get install -y curl wget uuid-runtime libuuid1 libicu-dev libssl-dev python3 python3-venv && \
    wget https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y aspnetcore-runtime-10.0 && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /Workspace/Other/EntryPoint

ADD Other/Resources/DependentCodeUnits/GenericMCPServerBackend/BuildResult_DotNet_${DotNetRuntime} /Workspace/Application/Backend

COPY GenericMCPServer/EntryPoint.sh /Workspace/Other/EntryPoint
COPY GenericMCPServer/HealthCheck.sh /Workspace/Other/EntryPoint
RUN chmod +x /Workspace/Other/EntryPoint/EntryPoint.sh && \
    chmod +x /Workspace/Other/EntryPoint/HealthCheck.sh

HEALTHCHECK --interval=30s --timeout=20s --start-period=2m CMD ./HealthCheck.sh

ENTRYPOINT ["./EntryPoint.sh"]
