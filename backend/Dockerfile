FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LocationSearch.slnx ./
COPY global.json ./
COPY src/ ./src/
COPY tests/ ./tests/
COPY docs/ ./docs/

RUN dotnet restore LocationSearch.slnx
RUN dotnet publish src/LocationSearch.Api/LocationSearch.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "LocationSearch.Api.dll"]