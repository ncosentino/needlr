# syntax=docker/dockerfile:1.7

FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:ed034a8bf0b24ded0cbbac07e17825d8e9ebfe21e308191d0f7421eaf5ad4664 AS dotnet

FROM --platform=linux/amd64 myoung34/github-runner:ubuntu-noble@sha256:43afb5b81ac1981351b46d9054e14b3bdb15742d7aef28165b5be1a840bddbae

LABEL org.opencontainers.image.source="https://github.com/ncosentino/needlr"
LABEL org.opencontainers.image.description="Needlr CI runner with the pinned .NET SDK and Native AOT prerequisites"

COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet

ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV DOTNET_NOLOGO=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        clang \
        file \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

RUN test -x /actions-runner/bin/Runner.Listener \
    && dotnet --list-sdks | grep -F '10.0.302' \
    && pwsh --version \
    && git --version \
    && gh --version \
    && clang --version \
    && file --version
