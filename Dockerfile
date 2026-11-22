
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY RadarInnowacji.sln ./
COPY src/RadarInnowacji.Core/RadarInnowacji.Core.csproj src/RadarInnowacji.Core/
COPY src/RadarInnowacji.Infrastructure/RadarInnowacji.Infrastructure.csproj src/RadarInnowacji.Infrastructure/
COPY src/RadarInnowacji.Worker/RadarInnowacji.Worker.csproj src/RadarInnowacji.Worker/
COPY src/RadarInnowacji.Web/RadarInnowacji.Web.csproj src/RadarInnowacji.Web/
COPY tests/RadarInnowacji.Tests/RadarInnowacji.Tests.csproj tests/RadarInnowacji.Tests/
COPY tests/RadarInnowacji.IntegrationTests/RadarInnowacji.IntegrationTests.csproj tests/RadarInnowacji.IntegrationTests/
RUN dotnet restore RadarInnowacji.sln

COPY . .
RUN dotnet publish src/RadarInnowacji.Web/RadarInnowacji.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    RADAR_PORT=8080 \
    RADAR_OPEN_BROWSER=false \
    RADAR_SINGLE_INSTANCE=false \
    RADAR_DATA_DIR=/data \
    RADAR_DB_PATH=/data/radar.db \
    RADAR_LOG_DIR=/data/logs

VOLUME ["/data"]
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RadarInnowacji.Web.dll"]
