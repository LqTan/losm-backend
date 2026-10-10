FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LocationSearch.slnx ./
COPY global.json ./
COPY src/ ./src/
COPY tests/ ./tests/

RUN dotnet restore LocationSearch.slnx
RUN dotnet publish src/LocationSearch.Api/LocationSearch.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    docker.io \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "LocationSearch.Api.dll"]

