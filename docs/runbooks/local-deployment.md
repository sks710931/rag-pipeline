# Local Deployment Runbook (Docker Compose)

This runbook guides operators and developers through deploying the minimal self-hosted stack for RagPipeline using Docker Compose (`distribution/compose/compose.yaml`).

The minimal stack consists of two services:
- **`postgres`**: PostgreSQL 17 Alpine database for persistent relational storage.
- **`api`**: ASP.NET Core 10 Minimal API container running the modular monolith backend and SPA frontend.

---

## 1. Prerequisites

Before deploying, ensure the host system satisfies:

1. **Docker Engine**: Version 24.0 or newer.
2. **Docker Compose**: Version 2.20 or newer (`docker compose` v2 plugin).
3. **OpenSSL**: Command-line tool for generating cryptographically secure secrets.
4. **Host Port Availability**: Ports `8080` (API) and `5432` (PostgreSQL) available on host loopback `127.0.0.1` (configurable via `.env`).

---

## 2. Secrets Creation & Configuration

The deployment enforces secret files mounted at runtime rather than cleartext environment variables.

### Step 2.1: Generate Required Secrets

The stack expects two secret files in `distribution/compose/secrets`:
- `db_password.txt`: Raw database password read by PostgreSQL (`POSTGRES_PASSWORD_FILE=/run/secrets/db_password`).
- `ConnectionStrings__Database.txt`: ADO.NET connection string read by the API host via KeyPerFile (`/run/secrets/ConnectionStrings__Database`).

Generate secrets using `openssl rand -hex 32`. Hexadecimal characters `[0-9a-f]` are safe for URLs and connection strings without requiring delimiter escaping (avoiding `;` or `=` conflicts):

```bash
# Ensure secrets directory exists
mkdir -p distribution/compose/secrets

# Generate a 32-byte hex random password
DB_PASS=$(openssl rand -hex 32)

# Write the postgres password file (no trailing newline)
printf "%s" "$DB_PASS" > distribution/compose/secrets/db_password.txt

# Write the API connection string file
printf "Host=postgres;Port=5432;Database=ragpipeline;Username=ragpipeline;Password=%s" "$DB_PASS" > distribution/compose/secrets/ConnectionStrings__Database.txt

# Set permissions: directory 700 keeps other host users out, files 644 readable by container user
chmod 700 distribution/compose/secrets
chmod 644 distribution/compose/secrets/db_password.txt
chmod 644 distribution/compose/secrets/ConnectionStrings__Database.txt
```

> [!IMPORTANT]
> With plain Docker Compose, file secrets are bind-mounted keeping host owner/mode, and the api container runs as non-root uid 1654 (user app), so chmod 600 files owned by the host user would be unreadable and the api would fail to start.
> Always set directory `distribution/compose/secrets` to `chmod 700` (`drwx------`) (keeps other host users out) and the two secret files to `chmod 644` (`-rw-r--r--`) (readable by the container user; protection comes from the 700 directory).
>
> **Troubleshooting**: if api logs a missing/empty ConnectionStrings:Database after secrets exist, check file readability.

### Step 2.2: Optional Port Configuration (`.env`)

If default ports `5432` or `8080` conflict with local services, copy `.env.example` to `.env` in `distribution/compose/`:

```bash
cp distribution/compose/.env.example distribution/compose/.env
```

Edit `distribution/compose/.env` to customize host port bindings (ports remain bound strictly to `127.0.0.1`):
```env
POSTGRES_HOST_PORT=5432
API_HOST_PORT=8080
```

---

## 3. Stack Lifecycle Operations

All lifecycle commands are run using `docker compose -f distribution/compose/compose.yaml`.

### Start the Stack (Build & Detached)

Build the API image and start containers in detached mode:

```bash
docker compose -f distribution/compose/compose.yaml up -d --build
```

Docker Compose builds the API container using `apps/api/Dockerfile`, waits for PostgreSQL to pass its healthcheck (`service_healthy`), and then starts the API container.

### Check Container Health & Status

View running containers and health status:

```bash
docker compose -f distribution/compose/compose.yaml ps
```

Expected output shows both services `Up` and `(healthy)`:
```text
NAME                     IMAGE                  COMMAND                  SERVICE      STATUS                    PORTS
compose-postgres-1       postgres:17-alpine     "docker-entrypoint.s…"   postgres     Up (healthy)              127.0.0.1:5432->5432/tcp
compose-api-1            compose-api            "dotnet RagPipeline.…"   api          Up (healthy)              127.0.0.1:8080->8080/tcp
```

### Inspect Container Logs

Stream logs from individual services:

```bash
# API container logs
docker compose -f distribution/compose/compose.yaml logs -f api

# PostgreSQL container logs
docker compose -f distribution/compose/compose.yaml logs -f postgres
```

### Restart Services

Restart all containers without rebuilding images:

```bash
docker compose -f distribution/compose/compose.yaml restart
```

### Stop the Stack (Preserving Data)

Gracefully stop and remove containers while preserving persistent volumes:

```bash
docker compose -f distribution/compose/compose.yaml down
```

> [!WARNING]
> Running `docker compose -f distribution/compose/compose.yaml down -v` removes named volumes (`pgdata` and `app-keys`).
> This will **permanently destroy** all stored PostgreSQL data and DataProtection keys. Only use `-v` if you intend to perform a complete factory reset.

---

## 4. Configuration Precedence Hierarchy

The API host resolves configuration according to the following strict hierarchy (higher sources override lower sources):

```
┌────────────────────────────────────────────────────────┐
│ 1. Secret Files (/run/secrets via KeyPerFile)          │  ◄── Highest Precedence
├────────────────────────────────────────────────────────┤
│ 2. Environment Variables (e.g., ConnectionStrings__*)  │
├────────────────────────────────────────────────────────┤
│ 3. appsettings.{Environment}.json                      │
├────────────────────────────────────────────────────────┤
│ 4. appsettings.json                                    │  ◄── Lowest Precedence
└────────────────────────────────────────────────────────┘
```

1. **Secret Files (Highest)**: Key-per-file provider reads directory `/run/secrets` (or custom directory set via `RAGPIPELINE_SECRETS_DIR`). A file named `ConnectionStrings__Database` automatically binds to `ConnectionStrings:Database`.
2. **Environment Variables**: Operating system environment variables.
3. **Environment-Specific JSON**: `appsettings.Production.json` or `appsettings.Development.json`.
4. **Base JSON (Lowest)**: Default `appsettings.json`.

Because secret files take precedence over environment variables, production credentials can be mounted into containers via file secrets without exposing raw database passwords in environment inspection (`docker inspect`, `/proc/environ`, or process listings).

---

## 5. Security & Hardening Summary

The Docker Compose configuration enforces robust defense-in-depth security:

- **Non-Root Execution**: The API container runs under unprivileged non-root user `app` (`USER app`, UID `1654`).
- **Read-Only Root Filesystem**: The API container runs with `read_only: true`. Temporary files are constrained to a mounted tmpfs at `/tmp`.
- **Linux Capabilities Dropped**:
  - `api`: `cap_drop: ALL`, `privileged: false`, `security_opt: [no-new-privileges:true]`.
  - `postgres`: `cap_drop: ALL` with only `[CHOWN, DAC_OVERRIDE, FOWNER, SETGID, SETUID]` added back (minimal set strictly required by PostgreSQL entrypoint to manage permissions and step down to `postgres` user).
- **Loopback Interface Binding**: All host ports bind explicitly to `127.0.0.1` (`127.0.0.1:${POSTGRES_HOST_PORT:-5432}:5432` and `127.0.0.1:${API_HOST_PORT:-8080}:8080`), preventing accidental exposure to public or internal networks.
- **No Passwords in Environment**: Neither container exposes database passwords via `environment:` blocks. Passwords and connection strings are mounted strictly via Docker secrets.
- **Persistent DataProtection Keys**: A dedicated named volume `app-keys` mounts at `/var/lib/ragpipeline/keys` owned by `app:app`, persisting encryption keys across container restarts.
- **Database Role (Known Limitation)**: The api connects to Postgres as the superuser until S0-PR03 introduces separate database roles (migrator, rag_app, rag_probe).

---

## 6. Healthcheck Architecture: Why `/live` and NOT `/ready`

The API provides two distinct health check endpoints:
- **`/api/v1/system/health/live`**: Process liveness probe. Returns HTTP 200 `{"status":"live"}` as long as the ASP.NET Core process is responsive.
- **`/api/v1/system/health/ready`**: Dependency readiness probe. Checks operational status of required backing services (PostgreSQL database). Returns HTTP 503 `{"status":"not_ready"}` if any required dependency is unavailable.

### Invariant: Docker Compose Healthcheck Monitors `/live`

Docker container healthchecks and restart policies **must always monitor `/live` and never `/ready`**:

1. **Avoid Cascading Restart Loops**: If Docker checked `/ready`, a temporary database restart or transient connection blip would cause Docker to declare the API container unhealthy and forcefully terminate it. This triggers a destructive restart loop while the database is still initializing.
2. **Graceful Connection Recovery**: By checking `/live`, Docker verifies that the API runtime is healthy and executing. The API can wait, retry connection pooling, and resume handling requests once PostgreSQL is ready.
3. **Implementation**: The API runtime image (`mcr.microsoft.com/dotnet/aspnet:10.0`) lacks `curl` and `wget`. The healthcheck is implemented via native bash socket redirection (`/dev/tcp`):
   ```bash
   exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/v1/system/health/live HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n' >&3 && cat <&3 | head -n 1 | grep -q ' 200 '
   ```

---

## 7. Troubleshooting & Failure Modes

### Missing Database or Connection Failure

- **At Startup**: If `ConnectionStrings:Database` is missing or malformed, startup validation (`ValidateOnStart`) halts initialization with exit code `1`. The application writes sanitized diagnostic messages to `stderr` with credentials redacted.
- **Secret File Readability**: With plain Docker Compose, file secrets are bind-mounted keeping host owner/mode, and the api container runs as non-root uid 1654 (user app), so chmod 600 files owned by the host user would be unreadable and the api would fail to start. If api logs a missing/empty ConnectionStrings:Database after secrets exist, check file readability: ensure directory `distribution/compose/secrets` is `chmod 700` (keeps other host users out) and the two secret files are `chmod 644` (readable by the container user; protection comes from the 700 directory).
- **At Runtime**: If PostgreSQL becomes unreachable while the API is running:
  - `/api/v1/system/health/live` returns HTTP 200 (process is alive).
  - `/api/v1/system/health/ready` returns HTTP 503 Service Unavailable:
    ```json
    {
      "status": "not_ready",
      "dependencies": {
        "database": "unchecked",
        "qdrant": "not_configured",
        "ai": "not_configured",
        "azure": "not_configured"
      }
    }
    ```

> [!CAUTION]
> **Security Invariant: Missing DB is Never Treated as Fresh Superadmin Setup**
> An unreachable or empty database is NEVER interpreted as an uninitialized instance prompting for fresh Superadmin creation. The system fails closed (HTTP 503 / fatal startup error) to prevent malicious instance hijacking, unauthorized bootstrapping, or privilege escalation during database downtime.

---

## 8. Production Reverse Proxy & TLS Guidance

In `distribution/compose/compose.yaml`, the API service listens on unencrypted HTTP and binds strictly to `127.0.0.1`.

For production deployment:
1. **Terminate TLS at Reverse Proxy**: Place a production-grade reverse proxy (Caddy, Nginx, Traefik, Envoy, or AWS ALB) in front of the API. Terminate TLS with valid certificates (e.g., Let's Encrypt / ACME).
2. **Keep API on Loopback**: Ensure the API port remains bound exclusively to `127.0.0.1`. Traffic between the reverse proxy and API travels over loopback.
3. **Forwarded Headers & Explicit Trusted Proxies**:
   - The reverse proxy must set `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`.
   - Explicit trusted proxy network configurations (`ForwardedHeadersOptions.KnownNetworks` / `KnownProxies`) should be configured to prevent client IP spoofing (note: explicit trusted proxy middleware is not yet built in).
