FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Web/SiteSyncTracker.Web.csproj", "Web/"]
COPY ["Web/Models/", "Models/"]
COPY ["Web/Data/", "Data/"]
RUN dotnet restore "Web/SiteSyncTracker.Web.csproj"

COPY Workspace/ Workspace/
COPY InventoryUi/ InventoryUi/
COPY Web/ Web/
WORKDIR /src/Web
RUN dotnet publish "SiteSyncTracker.Web.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
RUN mkdir -p /data && chown -R $APP_UID:$APP_UID /data
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish .
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/api/health || exit 1
ENTRYPOINT ["dotnet", "SiteSyncTracker.Web.dll"]
