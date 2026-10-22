# Web build stage
# Multi-arch index digest; needs periodic refresh
FROM node:22-bookworm-slim@sha256:c3de60bf2f9dd0ac6370e6117950ff62d6e339527e7472301c9c78a017978392 AS web-build
WORKDIR /web

RUN corepack enable && corepack prepare pnpm@12.11.2 --activate

COPY apps/web/package.json apps/web/pnpm-lock.yaml apps/web/pnpm-workspace.yaml apps/web/.npmrc ./
RUN pnpm install --frozen-lockfile

COPY apps/web/ ./
RUN pnpm build

# Build stage
# Multi-arch index digest; needs periodic refresh
FROM mcr.microsoft.com/dotnet/sdk:10.0.402-noble@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
WORKDIR /src

# Copy global.json, Directory.*.props, .editorconfig, slnx, and project lock files
COPY global.json Directory.*.props .editorconfig RagPipeline.slnx ./
COPY apps/api/RagPipeline.Api/*.csproj apps/api/RagPipeline.Api/packages.lock.json ./apps/api/RagPipeline.Api/
COPY src/BuildingBlocks/RagPipeline.BuildingBlocks/*.csproj src/BuildingBlocks/RagPipeline.BuildingBlocks/packages.lock.json ./src/BuildingBlocks/RagPipeline.BuildingBlocks/
COPY src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/*.csproj src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/packages.lock.json ./src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities/
COPY src/IdentityAccess/RagPipeline.IdentityAccess/*.csproj src/IdentityAccess/RagPipeline.IdentityAccess/packages.lock.json ./src/IdentityAccess/RagPipeline.IdentityAccess/
COPY src/Persistence/RagPipeline.Persistence/*.csproj src/Persistence/RagPipeline.Persistence/packages.lock.json ./src/Persistence/RagPipeline.Persistence/
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
# Multi-arch index digest; needs periodic refresh
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
COPY --from=web-build /web/dist ./wwwroot

RUN mkdir -p /var/lib/ragpipeline/keys && chown -R app:app /var/lib/ragpipeline/keys

# Non-root app user
# Health check: use /api/v1/system/health/live if any (no HEALTHCHECK against /ready)
USER app

ENTRYPOINT ["dotnet", "RagPipeline.Api.dll"]
