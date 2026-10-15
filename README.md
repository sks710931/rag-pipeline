# RagPipeline

Modular monolith backend and React frontend for the Enterprise Multimodal RAG Platform.

## Development

### Prerequisites

- [.NET SDK 10.0.402](https://dotnet.microsoft.com/) pinned via `global.json`.
- [Node.js v22.23.3](https://nodejs.org/) pinned via `.nvmrc` and `apps/web/.nvmrc`.
- [pnpm 12.11.2](https://pnpm.io/) pinned via `packageManager` in `apps/web/package.json`.

---

### Frontend (`apps/web`)

The frontend application is scaffolded under `apps/web` with Vite, React 19, strict TypeScript (`strict: true`, `noImplicitAny: true`, `noUncheckedIndexedAccess: true`), Tailwind CSS, and Vitest.

#### Frontend Commands

Navigate to `apps/web`:
```bash
cd apps/web
```

Restore dependencies deterministically using frozen lockfile:
```bash
pnpm install --frozen-lockfile
```

Run TypeScript type check:
```bash
pnpm run typecheck
```

Run ESLint:
```bash
pnpm run lint
```

Run unit tests:
```bash
pnpm run test
```

Verify formatting:
```bash
pnpm run format:check
```

Build production static assets (emits to `apps/web/dist`):
```bash
pnpm run build
```

Start local development server (with `/api` proxy to `http://localhost:5000`):
```bash
pnpm run dev
```

---

### Backend & API Host (`apps/api`)

The backend is built as an ASP.NET Core 10 Minimal API modular monolith that compositionally hosts feature modules and serves the React SPA static assets from `apps/web/dist`.

#### Backend Commands

From repository root:

Restore dependencies using locked packages:
```bash
dotnet restore --locked-mode
```

Build solution with warnings treated as errors:
```bash
dotnet build --no-restore -warnaserror
```

Run test suite (both unit and integration tests):
```bash
dotnet test --no-build
```

Verify code formatting:
```bash
dotnet format --verify-no-changes
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

---

### SPA Hosting & Routing Invariants

When `apps/web/dist` exists (or a custom web root is configured via `Spa:Root`), `RagPipeline.Api`:
1. Serves production static files (`/assets/*`, `/index.html`) via `UseStaticFiles`.
2. Provides client-side SPA fallback for root (`/`) and deep client navigation routes (e.g., `/app/status`) to `index.html` **strictly for GET and HEAD requests that accept HTML**.
3. **HTTP Method Invariant:** Non-GET/HEAD mutation requests (`POST`, `PUT`, `PATCH`, `DELETE`) to non-API client routes return HTTP 405 Method Not Allowed conforming to RFC 9457 `application/problem+json`, never `index.html`.
4. **Accept Header Invariant:** Requests not accepting HTML (e.g., `Accept: application/json` on client routes) return HTTP 404 Problem Details, never `index.html`.
5. **Strict `/api/*` Invariant:** The SPA fallback never handles any `/api/*` routes (including case variants and encoded slashes `/api%2f*`). Unmatched `/api/*` requests return HTTP 404 with Content-Type `application/problem+json` conforming to RFC 9457.
6. **Security & Cache Headers:** All SPA and static asset responses include defense-in-depth security headers:
   - `X-Content-Type-Options: nosniff`
   - `Referrer-Policy: strict-origin-when-cross-origin`
   - `X-Frame-Options: DENY`
   - `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'`
   - `index.html`: `Cache-Control: no-cache`
   - Hashed files under `/assets/*`: `Cache-Control: public, max-age=31536000, immutable`
7. **Resilience:** The API host builds, starts up, and passes tests without hard failure even if `apps/web` has not yet been built.

#### Verifying SPA Routing with curl

Start the API host and verify routing behavior:
```bash
# 1. Root route serves SPA index.html with security and cache headers
curl -s -i http://localhost:5000/ | grep -E "HTTP/|Content-Type|Cache-Control|X-Content-Type-Options"
# HTTP/1.1 200 OK
# Content-Type: text/html; charset=utf-8
# Cache-Control: no-cache
# X-Content-Type-Options: nosniff

# 2. Client-side fallback route serves index.html
curl -s -i http://localhost:5000/app/status | grep -E "HTTP/|Content-Type|Cache-Control"
# HTTP/1.1 200 OK
# Content-Type: text/html; charset=utf-8
# Cache-Control: no-cache

# 3. POST to client route returns RFC 9457 Problem Details 405 (NEVER index.html)
curl -s -i -X POST http://localhost:5000/app/status
# HTTP/1.1 405 Method Not Allowed
# Content-Type: application/problem+json

# 4. Live health endpoint returns JSON
curl -s -i http://localhost:5000/api/v1/system/health/live
# HTTP/1.1 200 OK
# Content-Type: application/json; charset=utf-8
# {"status":"live"}

# 5. Unknown /api route returns RFC 9457 Problem Details 404 (NEVER index.html)
curl -s -i http://localhost:5000/api/v1/system/unknown-route-that-does-not-exist
# HTTP/1.1 404 Not Found
# Content-Type: application/problem+json
```

---

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

---

### Package Ownership & Dependency Management

- **Central Package Management (CPM)**: Enabled globally via `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). Package versions are pinned centrally and shared across all .NET projects.
- **Lock Files**: Enabled via `RestorePackagesWithLockFile=true` in `Directory.Build.props`. Each .NET project maintains a `packages.lock.json` committed to source control for reproducible builds.
- **Frontend Dependencies**: Pinned exactly in `apps/web/package.json` and tracked with `apps/web/pnpm-lock.yaml`.
- **Project Ownership**:
  - `apps/web`: React + Vite frontend SPA with strict TypeScript, Tailwind CSS, accessible shell, and unit tests.
  - `apps/api/RagPipeline.Api`: Composition root, ASP.NET Core 10 Minimal API host, maps module endpoints, configures RFC 9457 error handling, and hosts SPA static assets.
  - `src/BuildingBlocks/RagPipeline.BuildingBlocks`: Shared foundation providing `IModule`, RFC 9457 Problem Details error contract, and startup configuration validation.
  - `src/IdentityAccess/RagPipeline.IdentityAccess`: Identity and access control module placeholder.
  - `src/TenantManagement/RagPipeline.TenantManagement`: Multi-tenant isolation and context management module placeholder.
  - `src/DeploymentCapabilities/RagPipeline.DeploymentCapabilities`: Deployment features and system health checks (`/live`, `/ready`).
  - `tests/Unit/RagPipeline.UnitTests`: Unit tests for validators, sanitizers, and architecture boundary enforcement.
  - `tests/Integration/RagPipeline.IntegrationTests`: End-to-end integration tests using `WebApplicationFactory<Program>`, including SPA hosting and routing invariant verification.

### Modular Monolith Boundaries

- **Single Host**: One runtime process (`RagPipeline.Api`) serves the backend and SPA static files. There are no independent microservice deployments.
- **Class Library Modules**: Feature modules (`IdentityAccess`, `TenantManagement`, `DeploymentCapabilities`) compile as class libraries implementing `IModule`.
- **Reference Invariant**: Feature modules may reference only `RagPipeline.BuildingBlocks` and runtime libraries; modules must never directly reference each other.
- **Single Persistence Boundary**: Persistence is managed centrally; independent, per-feature `DbContext`s are not permitted.
- **Frontend SPA Boundary**: The React SPA in `apps/web` interacts with the backend strictly via the BFF HTTP API (`/api/*`).

---

## Continuous Integration

The repository enforces automated validation on pull requests and pushes to `main` via GitHub Actions (`.github/workflows/ci.yml`).

### CI Jobs & Local Equivalents

| CI Job | Local Equivalent Command |
|---|---|
| `build-test` | `dotnet restore --locked-mode && dotnet build --no-restore -c Release -warnaserror && dotnet test --no-build -c Release --logger trx --results-directory TestResults` |
| `format` | `dotnet format --verify-no-changes --no-restore` |
| `secret-scan` | `gitleaks git --redact --no-banner .` |
| `dependency-advisories` | `dotnet list package --vulnerable --include-transitive` |
| `container-build` | `docker build -f apps/api/Dockerfile -t ragpipeline-api:sha-$(git rev-parse --short=12 HEAD) --label org.opencontainers.image.revision=$(git rev-parse HEAD) .` |
| `web` | `cd apps/web && pnpm install --frozen-lockfile && pnpm format:check && pnpm lint && pnpm typecheck && pnpm test && pnpm build && pnpm audit --audit-level=high --prod` |

### Container Tag Convention

In the `container-build` job, the API container image is built without pushing to a registry or performing registry login. The image is tagged following the commit SHA convention:
- **Image tag:** `ragpipeline-api:sha-<first 12 chars of github.sha>` (e.g. `ragpipeline-api:sha-${GITHUB_SHA:0:12}` or locally `ragpipeline-api:sha-$(git rev-parse --short=12 HEAD)`)
- **OCI label:** `org.opencontainers.image.revision=${{ github.sha }}` (the full commit SHA)

Local equivalent command:
```bash
docker build -f apps/api/Dockerfile \
  -t "ragpipeline-api:sha-$(git rev-parse --short=12 HEAD)" \
  --label "org.opencontainers.image.revision=$(git rev-parse HEAD)" .
```

### Downloading CI Artifacts

CI produces two artifacts:
- **`test-results`**: Test execution reports uploaded on every run (`if: always()`) from the `TestResults` directory.
- **`web-logs`**: Frontend output (`apps/web/dist`) uploaded only when the `web` job fails (`if: failure()`, `if-no-files-found: ignore`).

To download these artifacts:
- **GitHub UI**: Navigate to the repository's **Actions** tab, click on the workflow run, scroll down to the **Artifacts** section at the bottom of the Summary page, and click `test-results` or `web-logs` to download the zip file.
- **GitHub CLI (`gh`)**:
  ```bash
  # Download .NET test results
  gh run download <run-id> -n test-results --dir ./TestResults

  # Download web build/logs artifact (if the web job failed)
  gh run download <run-id> -n web-logs --dir ./web-logs
  ```

### Release & Compliance Gaps

SBOM generation, image signing and release certification are NOT implemented yet.

