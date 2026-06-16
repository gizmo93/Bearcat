FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /App

# Stamped into the assembly so the running app can report its version and check for updates.
# The release workflow passes the actual version.
ARG BEARCAT_VERSION=0.0.0-dev

COPY . ./
RUN dotnet restore src/Bearcat.Host/Bearcat.Host.csproj -a x64

RUN dotnet publish src/Bearcat.Host/Bearcat.Host.csproj -a x64 --no-restore -o /App/out -p:Version=$BEARCAT_VERSION


FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble

ENV \
    ASPNETCORE_ENVIRONMENT=Production \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    p7zip-full \
    wget \
    && wget https://www.win-rar.com/fileadmin/winrar-versions/rarlinux-x64-712.tar.gz \
    && tar -xzf rarlinux-x64-712.tar.gz \
    && cp rar/rar rar/unrar /usr/local/bin/ \
    && chmod +x /usr/local/bin/rar /usr/local/bin/unrar \
    && rm -rf rar rarlinux-x64-712.tar.gz \
    && apt-get remove -y wget \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "Bearcat.Host.dll"]
