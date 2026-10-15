# RagPipeline

Modular monolith backend for the Enterprise Multimodal RAG Platform.

## Development

### Prerequisites

- [.NET SDK 10.0.402](https://dotnet.microsoft.com/) pinned via `global.json`.

### Build & Run Commands

Restore dependencies using locked packages:
```bash
dotnet restore --locked-mode
```

Build solution with warnings treated as errors:
```bash
dotnet build --no-restore -warnaserror
```

Run test suite:
```bash
dotnet test --no-build
```

Run API host (listens at `http://localhost:5000` via `Properties/launchSettings.json` or `ASPNETCORE_URLS`):
```bash
dotnet run --project apps/api/RagPipeline.Api
```

Check health endpoints:
```bash
curl -i http://localhost:5000/api/v1/system/health/live
curl -i http://localhost:5000/api/v1/system/health/ready
```
The `/ready` endpoint returns HTTP 503 while any required dependency check (such as the database) is unchecked or not ready, returning HTTP 200 only when all required dependencies are ready.


### Reproducing Sanitized Config Failure

Required database configuration is validated at startup using `ValidateOnStart`. To verify sanitized diagnostic reporting and non-zero exit code:

```bash
dotnet run --project apps/api/RagPipeline.Api --ConnectionStrings:Database="Password=SECRETVALUE123;garbage"
```

Or run without a connection string in Production mode:
```bash
dotnet run --no-launch-profile --project apps/api/RagPipeline.Api --environment Production
```

The application immediately exits with code `1` and emits sanitized diagnostics to `stderr` specifying the failed key name without leaking secrets, connection strings, configured values, or stack traces:
```text
Configuration validation failed for 'DatabaseOptions':
  - Configuration key 'ConnectionStrings:Database' is malformed and could not be parsed as a valid connection string.
```

### Package Ownership & Dependency Management

- **Central Package Management (CPM)**: Enabled globally via `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). Package versions are pinned centrally and shared across all projects.
- **Lock Files**: Enabled via `RestorePackagesWithLockFile=true` in `Directory.Build.props`. Each project maintains a `packages.lock.json` committed to source control for reproducible builds.
- **Project Ownership**:
  - `apps/api/RagPipeline.Api`: Composition root, ASP.NET Core 10 Minimal API host, maps module endpoints, configures RFC 9457 error handling.
  - `src/BuildingBlocks/RagPipeline.BuildingBlocks`: Shared foundation providing `IModule`, RFC 9457 Problem Details error contract, and startup configuration validation.
  - `src/IdentityAccess/RagPipeline.IdentityAccess`: Identity and access control module placeholder.
  - `src/TenantManagement/RagPipeline.TenantManagement`: Multi-tenant isolation and context management module placeholder.
  - `src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities`: Deployment features and system health checks (`/live`, `/ready`).
  - `tests/Unit/RagPipeline.UnitTests`: Unit tests for validators, sanitizers, and architecture boundary enforcement.
  - `tests/Integration/RagPipeline.IntegrationTests`: End-to-end integration tests using `WebApplicationFactory<Program>`.

### Modular Monolith Boundaries

- **Single Host**: One runtime process (`RagPipeline.Api`) serves the backend. There are no independent microservice deployments.
- **Class Library Modules**: Feature modules (`IdentityAccess`, `TenantManagement`, `DeploymentCapabilities`) compile as class libraries implementing `IModule`.
- **Reference Invariant**: Feature modules may reference only `RagPipeline.BuildingBlocks` and runtime libraries; modules must never directly reference each other.
- **Single Persistence Boundary**: Persistence is managed centrally; independent, per-feature `DbContext`s are not permitted.
- **Frontend Separation**: Web UI assets will live under `apps/web` in a subsequent PR; no static SPA files or frontend hosting are included in this PR.
