# Production image for the Pantrio API. Built in CI (or locally), never on the server.
# Build context: ./backend

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Pantrio.slnx ./
COPY src/Pantrio.Domain/Pantrio.Domain.csproj src/Pantrio.Domain/
COPY src/Pantrio.Infrastructure/Pantrio.Infrastructure.csproj src/Pantrio.Infrastructure/
COPY src/Pantrio.Api/Pantrio.Api.csproj src/Pantrio.Api/
RUN dotnet restore src/Pantrio.Api/Pantrio.Api.csproj
COPY src/ src/
RUN dotnet publish src/Pantrio.Api/Pantrio.Api.csproj -c Release -o /app --no-restore \
      -p:OpenApiGenerateDocuments=false

# chiseled-extra includes ICU and tzdata (household time zones need tzdata).
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x4B
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Pantrio.Api.dll"]
