# Noble: Ubuntu 24.04 LTS
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-noble-chiseled AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

RUN apt-get update && \
    apt-get install -y clang zlib1g-dev libssl-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AccountsApi/AccountsApi.csproj", "AccountsApi/"]
COPY ["AccountsApi/appsettings.json", "AccountsApi/"]
COPY ["Models/Models.csproj", "Models/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "AccountsApi/AccountsApi.csproj"
COPY . .
WORKDIR "/src/AccountsApi"
RUN dotnet build "./AccountsApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish -c $BUILD_CONFIGURATION -r linux-x64 \
    -o /app/publish \
    --self-contained true \
    /p:UseAppHost=true \
    /p:PublishTrimmed=true && \
    chmod +x /app/publish/AccountsApi

COPY ["AccountsApi/appsettings.json", "/app/publish/"]

FROM base AS final
COPY --from=build src/AccountsApi/appsettings.json .
COPY --from=publish /app/publish .

ENTRYPOINT ["./AccountsApi"]
