FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.102-noble AS build
ARG TARGETARCH
WORKDIR /App

COPY . ./
RUN dotnet restore -a $TARGETARCH

RUN dotnet publish -a $TARGETARCH --no-restore -o /App/out


FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-noble

ENV \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8

WORKDIR /App
COPY --from=build /App/out .
ENTRYPOINT ["dotnet", "Bearcat.Frontend.dll"]