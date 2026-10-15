# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble AS build
WORKDIR /src

# Copy global.json, Directory.*.props, .editorconfig, slnx, and project lock files
COPY global.json Directory.*.props .editorconfig RagPipeline.slnx ./
COPY apps/api/RagPipeline.Api/*.csproj apps/api/RagPipeline.Api/packages.lock.json ./apps/api/RagPipeline.Api/
COPY src/BuildingBlocks/RagPipeline.BuildingBlocks/*.csproj src/BuildingBlocks/RagPipeline.BuildingBlocks/packages.lock.json ./src/BuildingBlocks/RagPipeline.BuildingBlocks/
COPY src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/*.csproj src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/packages.lock.json ./src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/
COPY src/IdentityAccess/RagPipeline.IdentityAccess/*.csproj src/IdentityAccess/RagPipeline.IdentityAccess/packages.lock.json ./src/IdentityAccess/RagPipeline.IdentityAccess/
COPY src/TenantManagement/RagPipeline.TenantManagement/*.csproj src/TenantManagement/RagPipeline.TenantManagement/packages.lock.json ./src/TenantManagement/RagPipeline.TenantManagement/
COPY tests/Integration/RagPipeline.IntegrationTests/*.csproj tests/Integration/RagPipeline.IntegrationTests/packages.lock.json ./tests/Integration/RagPipeline.IntegrationTests/
COPY tests/Unit/RagPipeline.UnitTests/*.csproj tests/Unit/RagPipeline.UnitTests/packages.lock.json ./tests/Unit/RagPipeline.UnitTests/

# Restore dependencies
RUN dotnet restore --locked-mode

# Copy source code
COPY apps/ ./apps/
COPY src/ ./src/

# Publish API
RUN dotnet publish apps/api/RagPipeline.Api -c Release --no-restore -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./

# Non-root app user
# Health check: use /api/v1/system/health/live if any (no HEALTHCHECK against /ready)
USER app

ENTRYPOINT ["dotnet", "RagPipeline.Api.dll"]
