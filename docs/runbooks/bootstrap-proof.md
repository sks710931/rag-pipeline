# Bootstrap Proof Operator Runbook

This runbook guides deployment operators on generating, handling, and consuming the high-entropy one-time bootstrap proof required for initial Superadmin and Tenant Owner setup during deployment installation (`S0-016`).

---

## 1. Overview and Security Principles

To prevent unauthorized public initialization or takeover of self-hosted deployments:
1. **Out-of-Band Generation**: The bootstrap proof is generated exclusively via privileged CLI access (`rag-admin bootstrap-proof generate`), requiring direct operator possession of the host environment or container runtime.
2. **High Entropy**: Each proof provides 256 bits of CSPRNG entropy, rendering brute-force attacks infeasible.
3. **No Plaintext Persistence**: The database persists only a SHA-256 cryptographic digest alongside an expiration timestamp and consumption marker. The server cannot disclose or recover plaintext proof values.
4. **Log Suppression**: Bootstrap proof values are strictly forbidden from automatic container startup logs, standard output collected by log shipping agents, HTTP health or status responses, and audit records.
5. **Fail-Closed Verification**: The proof is valid for single use within a 15-minute window. Rate-limiting guards against guessing attempts.

---

## 2. Docker Exec and Operator Invocation

In containerized environments (such as Docker Compose or Kubernetes), operators invoke the administrative CLI via interactive shell execution:

```bash
# Interactive generation (displays proof directly to operator terminal; never logged to container stdout)
docker compose exec -it api dotnet RagPipeline.Api.dll rag-admin bootstrap-proof generate
```

When invoking non-interactively or from automation scripts, piping output to standard out is rejected by default to prevent accidental leakage into container logging infrastructure. Operators must specify an explicit, protected destination file:

```bash
# Non-interactive generation to a protected file
docker compose exec api dotnet RagPipeline.Api.dll rag-admin bootstrap-proof generate --file /run/secrets/bootstrap-proof.txt
```

---

## 3. Privileged Operator Identity and Role Separation

Administrative operations enforce strict role separation:
- **Maintenance / Migrator Role (`migrator`)**: The CLI must connect with maintenance or migrator credentials (`ConnectionStrings:Maintenance` or `RAGPIPELINE_MAINTENANCE_CONNECTION`).
- **Runtime Role Exclusion (`rag_app`)**: The runtime web application role is strictly prohibited from running administrative CLI commands. The CLI verifies `CURRENT_USER` and aborts if invoked under the runtime role.
- **Container User Privileges**: The API container runs as non-root user `app` (UID 1654). Administrative executions inside the container (`docker compose exec`) run with appropriate operator identity and do not require elevated host kernel privileges.

---

## 4. Transport Security and TLS Requirements

When submitting the generated proof during browser or API enrollment (`POST /api/v1/bootstrap/complete`):
- **TLS Protection**: All external traffic must be encrypted with TLS (HTTPS). Transmitting the proof over unencrypted cleartext HTTP outside loopback (`127.0.0.1`) is prohibited.
- **Reverse Proxy / Ingress**: When deployed behind reverse proxies (Nginx, Traefik, Cloudflare, etc.), ensure proxy headers (`X-Forwarded-Proto: https`) are trusted and validated to prevent downgrade attacks.
- **Browser Security**: The setup UI transmits the proof securely in the request payload with `no-store, no-cache` headers, preventing disk caching or browser history exposure.

---

## 5. One-Time Consumption and File Cleanup

1. **Immediate Single-Use Consumption**: Upon submitting the proof during first-owner creation, the server atomically validates and records `proof_consumed_at = clock_timestamp()` within the same database transaction that seals the deployment.
2. **File Cleanup**:
   - If the proof was written to a file via `--file <path>`, the operator or automated deployment pipeline **must delete** the file immediately after enrollment:
     ```bash
     rm -f /run/secrets/bootstrap-proof.txt
     ```
   - On Linux systems, `rag-admin` creates files with `0600` (`-rw-------`) permissions, restricting access exclusively to the owner.
3. **Invalidation**: Generating a new proof immediately supersedes and invalidates any previous unconsumed proof. Once the deployment transitions to `SEALED`, all proof generation and verification pathways are permanently locked.
