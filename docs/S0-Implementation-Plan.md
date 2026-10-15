# S0 Implementation Plan — Production-Ready Platform Foundation

> **Status:** Approved architectural inputs; **proposed executable implementation plan**, not a claim of delivered code
> **Plan version:** 1.0 · **Prepared:** 2026-10-15
> **Repository:** [sks710931/rag-pipeline](https://github.com/sks710931/rag-pipeline)
> **Branch target:** \`main\` via individually reviewed pull requests
> **Authoritative inputs:** [Architecture v1.6](architecture.md), §§1, 3–4, 8, 14, 17–18, 21–24, 32–34; [Slicing Strategy v1.2](Slicing%20strategy.md), §§1–8, 11–14
> **Baseline:** The repository currently contains only the two specification files above; no application implementation or test suite is assumed.
> **Delivery span:** S0.1 → S0.2 → S0.3 → S0.4 → S0.5 → S0.6; **12 independently reviewable PRs**. S0 is production-quality **for its limited advertised functionality**, not a usable RAG engine.

## 1. Outcome, success criteria and immutable boundaries

**Primary user outcome:** An operator installs the self-hosted system, uses a one-time operator-controlled proof to create the **first username/password** (becomes **deployment Superadmin** plus **initial Tenant Owner**), signs in to a polished, responsive product, protects integration secrets with a persistent recovery-capable key protector, sees truthful system and setup status, reaches a real dashboard, then logs out/in and restarts **without ever seeing initial Superadmin creation again**. An operator can recover the installation on replacement hardware using independent protected recovery material.

**S0 exit criteria — all required:**
1. Fresh database: **exactly one** successful web owner-bootstrap claim when a valid one-time CLI proof is supplied; user, initial tenant, memberships, audit and \`SEALED\` are committed atomically. All future requests to initial-owner API are rejected, including concurrent/replayed requests and after updates/restores.
2. Local login, logout, password change, session revocation, profile/tenant context, protected settings, CLI emergency account recovery and actual admin health dashboard work **without** SMTP, OAuth, external AI, Qdrant, OCR providers or a physical TPM.
3. Standard Docker Compose self-hosted mode can **restart unattended** with a secure persistent root-key mount and still decrypt its own S0 test encrypted record. If the root is missing/wrong, the store is **LOCKED/RECOVERY_REQUIRED**; login/admin repair remains possible, but protected writes/provider use fail closed. No silent reset or fallback to plaintext.
4. Protected **independent recovery** and a **replacement-host recovery exercise** pass; the DB backup alone is insufficient for unlocking the system. The previous \`SEALED\` state is preserved.
5. A real, capable **Setup Center** guides a first-owner through health, profile/security and key protection in sequence; only **implemented** optional providers can be configured/tested. Missing future features are absent or labeled unavailable. No fake connectors, dead buttons, placeholder dashboard data or success-before-response.
6. Every released surface has production-quality visual design, loading/empty/error/unauthorized/recovery states, narrow-screen and keyboard usability, secret-safe audit/telemetry, contract tests, real PostgreSQL integration tests and Playwright happy/adverse flows.
7. S0 security guarantees are demonstrated with at least two synthetic tenants and denied access paths; S1 remains responsible for full multi-tenant membership/role UX, enterprise OIDC and document ACL flows.
8. Distribution includes pinned image builds, reproducible install/upgrade instructions, least-privilege deployment defaults and a tested self-hosted backup/restore procedure for **the actual S0 state**.

**Non-negotiable invariants:** No default/supplied universal Superadmin password; no publicly claimable first owner; no SaaS customer route creating a platform Superadmin; no reusable bootstrap proof; no secret plaintext in DB/log/queue/browser read APIs; no crypto key in image/repo; no runtime admin implicit content-read privilege; no tenant RLS state leakage; no UI-only security; no faux integration state. Retain v1.6 reference and change-control policy.

## 2. Deliberately excluded from S0

**Do not implement** document upload or parsing (S2–S3), Python workers, queues/outbox ingestion, CDM data, semantic search/Qdrant, OCR/embedding/generation/vision model adapters or probes (S4–S7), enterprise OIDC/invitations (S1), MCP/OpenIddict authorization server (S13), subscription billing, full SaaS tenant operator plane (S14), TPM CNG/TSS agents/IPC (optional S14), policy-aware document redaction or evidence governance (later slices). Do not create future domain tables or empty module projects merely to imitate the ultimate architecture diagram.

**Exception:** Establish thin, testable **building-block contracts** for later slices (tenant-scoped request context/RLS, capability registry, \`IKeyProtectionProvider\`, secret storage) where S0 itself uses them. Creating a \`MissingProvider\` adapter that pretends to connect is prohibited.

**SaaS mode distinction:** A SaaS operator is initialized privately through controlled CLI/IaC procedures and cannot bootstrap through the public customer domain. A customer signup/tenant enrollment UX is **not S0**. Validate that deployment mode configuration cannot be changed by a request header or arbitrary user parameter.

## 3. Target repository structure and implementation choices

Use a **modular monolith** with vertical feature folders. Code organization may adapt, but the core boundaries and commands below are stable.

~~~text
/
├── docs/
│   ├── architecture.md
│   ├── Slicing strategy.md
│   ├── S0-Implementation-Plan.md
│   └── runbooks/                         # created by S0 PRs
├── apps/
│   ├── api/                              # ASP.NET Core 10, BFF, hosting, API composition root
│   └── web/                              # React + strict TypeScript, Tailwind, real UI
├── src/
│   ├── BuildingBlocks/                   # error envelope, time, auth policy, tenant/RLS, diagnostics
│   ├── IdentityAccess/
│   │   └── Features/                     # bootstrap, login, logout, sessions, password, recovery
│   ├── TenantManagement/
│   │   └── Features/                     # initial tenant profile, current context only in S0
│   ├── DeploymentCapabilities/
│   │   └── Features/                     # bootstrap state, capability catalog, health, wizard
│   └── SecretManagement/
│       └── Features/                     # protector activation, vault, recovery, rotation
├── tests/
│   ├── Unit/
│   ├── Integration/                      # real PostgreSQL + test helpers
│   ├── Security/
│   └── E2E/                              # Playwright browser tests
└── distribution/
    └── compose/                          # minimal self-hosted profile and overrides
~~~

**Physical project count is not an architectural target.** Use the fewest necessary \`.csproj\` boundaries; do not create independent DbContexts for every folder. Use EF Core and migrations managed exclusively by .NET; one transactional tenant context provider supports all tenancy-aware data access. Use modular APIs (ASP.NET Core Minimal APIs or equivalent feature endpoints) with generated OpenAPI client types for React.

**S0 runtime composition:** .NET API/BFF serves or securely hosts React static assets behind a same-origin reverse proxy; PostgreSQL stores identity/tenant/setup/secret records. A durable storage choice for the ASP.NET Core **Data Protection key ring** (encrypted at rest, independently backed up) is required for session continuity and restart-safe recovery, but is **not** a substitute for the separately versioned wizard-secret MEK. No worker or Qdrant service is required. A local dev Vite server is allowed only in development.

**Container profiles:** \`compose.s0.yml\` (or equivalently documented Compose profile) is minimal; future components may be in opt-in profiles. No \`privileged: true\`, hardcoded passwords, plaintext \`.env\` committed to Git, broad TPM device mappings or root-mechanism default embedded in images.

**Database authority:** Migration process has a separately controlled schema-privileged identity. Application DB role must not own tenant data tables, must be \`NOBYPASSRLS\`, and applies transaction-local tenant context; use \`ENABLE/FORCE RLS\` appropriately. Separate deployment-global bootstrap/installation rows from tenant-owned rows—do not fake a tenant context before a tenant exists.

## 4. PR breakdown, ordered dependencies and deliverable ownership

Every PR below includes API+UI when its user outcome is visible, migrations only if used, unit/integration/e2e coverage, an actual screenshot/recording for touched UI, operational notes and a rollback statement. **Do not merge a PR that relies on mocks for its claimed user-visible success state.**

| PR | Increment | Output | Needs | Acceptance |
| --- | --- | --- | --- | --- |
| **S0-PR01** | S0.1 | Greenfield solution, containers, CI, health/readiness, config validation | None | Local one-command build and smoke startup work |
| **S0-PR02** | S0.1 | Design system, accessible responsive application/public shell | PR01 | Production-ready real shell/states, no dead navigation |
| **S0-PR03** | S0.1 | .NET persistence, Identity schema, tenant/RLS/security policies and baseline audit | PR01 | Real PostgreSQL two-tenant isolation and pooled reuse tests |
| **S0-PR04** | S0.2 | Privileged bootstrap-proof CLI, one-time atomic owner/tenant creation, sealed server API | PR03 | Two concurrent claims → one owner, permanently sealed |
| **S0-PR05** | S0.2 | One-time bootstrap UX, maintenance and sealed redirect | PR02 + PR04 | Fresh/no-proof/success/sealed paths work in browser |
| **S0-PR06** | S0.3 | Local BFF login/logout, authorized sessions, revocation, CSRF | PR04 + PR05 | Secure login and revoked/expired sessions denied |
| **S0-PR07** | S0.3 | Profile/password, optional TOTP and recovery codes, privileged CLI break-glass recovery | PR06 | Owner can maintain/recover identity without SMTP |
| **S0-PR08** | S0.4 | Secret encryption/key versioning and Key Protection interfaces + negative tests | PR03 + PR06 | AES-GCM/AAD tampering, tenant mismatch and no plaintext read |
| **S0-PR09** | S0.4 | Persistent software-root and approved KMS protectors + lifecycle API | PR08 | Unattended restart, missing root, KMS outage and no downgrade |
| **S0-PR10** | S0.4 | Independent recovery kit, verified restore, versioned rewrap/rotation, key UX | PR02 + PR07 + PR09 | Restore on replacement host; keys rotate without data loss |
| **S0-PR11** | S0.5 | Resumable setup registry/stepper, real deployment/tenant settings and health views | PR07 + PR10 | Capabilities truthful, progress persisted, no fake providers |
| **S0-PR12** | S0.6 | Security/adversarial suite, backup/upgrade rehearsal, Compose hardening and release UX review | PR01–PR11 | Entire S0 release checklist and end-to-end drill pass |

PRs 01–03 may progress on separate branches; avoid independent overlapping migration changes. PR02 can deliver shell routes against real health/readiness read-only API from PR01. Merge PR04 before wiring PR05, and merge encryption PR08 before implementing S0-PR09 recovery work. Each PR should reference this plan, its exact numbered test scenarios and architecture clauses.

## 5. Detailed PR task cards and acceptance tests

### S0.1 — Foundation & production-grade design system

#### S0-PR01 — Runtime foundation and developer experience

**Implement**
- Create \`.sln\` / \`.slnx\`, API host, feature modules, React app, strict TS build, Tailwind tokens, error/result contracts and structured log/correlation middleware. Use .NET 10 and a consistently pinned Node toolchain/lockfile; pin container base images and document supported versions.
- Implement \`GET /api/v1/system/health/live\` and \`GET /api/v1/system/health/ready\`, distinguishing process liveness from genuinely functional configured dependencies. Authenticated diagnostics endpoint is separate from anonymous minimal health; never show connection strings, auth details or key material.
- Provide local \`docker compose\` instructions that start only .NET/BFF + PostgreSQL (plus an actual required reverse proxy if used). Validate required configuration at startup; do not treat absent Qdrant, RabbitMQ or Azure keys as fatal for S0.
- Add GitHub Actions for restore/build, formatting, .NET unit tests, frontend typecheck/lint/tests, secret scan, dependency advisories and container build. Establish image tagging and minimal test reporting; add SBOM/signature tasks to release pipeline when that infrastructure is ready.
- Establish configuration precedence (environment/secret mounts/CLI), safe reverse-proxy settings (explicit trusted proxies/network lists), request limits and local TLS guidance.

**Prove**
- Fresh clone builds; validated Compose startup, clean shutdown/restart, migration preflight and \`ready/live\` responses work with no cloud API keys. Invalid required database config fails explicitly; missing optional Qdrant/model is reported as not configured, not a failed installation.
- CI runs on PR and blocks known failing gates. Logs include correlation IDs but not the literal values of supplied secrets.

#### S0-PR02 — Application design system and actual UX shell

**Implement**
- Establish documented Tailwind tokens, typography, spacing, colors, icons (Lucide), form/error primitives, modal/drawer/stepper, status indicators, skeleton, toast, empty/error states, focus management and responsive shell. Define a clear accessible visual hierarchy, **no generic Bootstrap-like admin placeholder**.
- Real routes: \`/setup\` (gated by server state, initially only educational), \`/login\`, \`/app\` (auth protected), \`/app/setup-center\`, \`/app/settings/profile\`, \`/app/settings/security\`, \`/app/settings/integrations\` (implemented-capability list), \`/app/system\`. Routes not yet backed by their feature remain intentionally absent until later PRs; PR02 may initially show real health state rather than a fake authenticated app.
- Responsive desktop/tablet/mobile form and navigation, explicit keyboard and screen reader states, proper empty/permission-denied/stale-session screens. Avoid revealing setup route and provider capabilities to anonymous users beyond the legitimate bootstrap entry.
- Production app shell loads real server bootstrap/readiness states before deciding what to render; no JSX-only role/tenant access decision is trusted by the backend.

**Prove**
- Keyboard-only setup/sign-in navigation, labels, error focus, reduced motion, dark/light styling if exposed, mobile 320–375px and desktop 1440px screenshots, axe-core smoke checks plus manual tab order/screen-reader review. No visible click target is inert or triggers fictitious completion.

#### S0-PR03 — Persistence, tenant isolation and authorization foundation

**Implement**
- EF Core + Npgsql migrations for **minimum actual S0 tables**: ASP.NET Core Identity users/credentials/tokens, deployment bootstrap state/proof metadata, deployment owners/role assignments, initial tenants, memberships, initial profile/settings, durable session/revocation metadata and append-only audit events. Add encryption-specific tables only in PR08–PR09.
- Model **deployment Superadmin** separately from **Tenant Owner** membership. Installers/operator accounts must not acquire automatic document/content access. Only initial membership grants tenant-scoped settings access.
- Implement \`TenantContext\` resolved from authenticated membership and route rather than a mutable session-global tenant. Use an explicit transaction boundary to set \`set_config('app.current_tenant_id', @tenantId, true)\` before tenant-scoped SQL; restrict app role \`NOBYPASSRLS\`, avoid table ownership, enforce \`ENABLE/FORCE ROW LEVEL SECURITY\` where appropriate, and use composite tenant FK/indexes for actual S0 relations.
- Separate application, migrator and minimal read-only operational probe credentials. Ensure no direct SQL path bypasses tenant transaction wrapper. Add a test-only seeded second tenant in integration tests—no demo tenant in product builds.
- Use named authorization policy checks on every server mutating endpoint. Put permission catalog in a single versioned place; no broad \`IsSuperadmin => all tenant content\` shortcut.

**Prove**
- Real PostgreSQL tests for Tenant A/B reads/writes; missing tenant context denies, forged route tenant ID denies, pooled Npgsql connection reused after exception/cancellation does not leak \`SET LOCAL\` state. Migrations are rerunnable after interrupted deploy using explicit recovery instructions; app role cannot bypass tenant RLS or run DDL.

### S0.2 — Protected, permanent first-run bootstrap

#### S0-PR04 — Operator proof, atomic initial owner and sealed state

**Implement**
- Deployment-global singleton \`deployment_bootstrap_state\` with \`UNINITIALIZED\`, exclusive attempted bootstrap and irreversible \`SEALED\` state, plus hash/expiry/consumption metadata for the one-time proof. Missing DB/migration = **SERVICE_UNAVAILABLE**, never guessed UNINITIALIZED.
- Privileged \`rag-admin bootstrap-proof generate\` command usable from a controlled \`docker compose exec\` / local CLI. It generates **cryptographically strong, short-lived, single-use proof**, stores only a hash server-side and displays proof only to a privileged *interactive* operator (or protected runtime file); **never** print it on default container startup, stdout collected by log shipping, HTTP status endpoints or checked-in docs.
- Anonymous \`GET /api/v1/bootstrap/status\` exposes only minimal state needed for UI (e.g. \`requiresSetup\`); \`POST /api/v1/bootstrap/complete\` requires single-use proof, username, strong password, matching confirmation validated server-side, same-origin/CSRF protection, TLS outside loopback, strict rate limits and content limits.
- **Single DB transaction** claims proof under serialization/lock/unique constraints, creates Identity account, deployment owner assignment, initial tenant and tenant owner membership, audits outcome and sets \`SEALED\`. Concurrent attempts: exactly one success, no orphan users/proofs. Never permit \`SEALED -> UNINITIALIZED\` through web/API/upgrade. A new empty database represents a **different deployment** and must be protected by deployment ops.
- SaaS mode has **no public bootstrap endpoint**; use privileged operator provisioning, not customer sign-up or magic environment flags originating from request headers.
- Document idempotency after ambiguous HTTP disconnect: an exact retry after successful commit receives a safe sealed/already initialized result, not a second owner. Do not return credentials in responses.

**Prove**
- Parallel clients race, proof replay/expiry/invalid proof, brute-force, forged forwarded headers, bad password/invalid tenant/profile, transaction rollback mid-creation and restart/restore preserve precisely one owner/tenant and irreversible seal.
- Direct anonymous POST to setup endpoint after sealing is always rejected; removing owner records or clearing cookies cannot reopen public setup.

#### S0-PR05 — Install wizard and first-owner browser flow

**Implement**
- A polished first-run page with username/password/confirmation, proof field, comprehensible password policy, inline validation, submit pending state and recoverable API failures. Explain that first user becomes deployment Superadmin and initial Tenant Owner, but **not** an unrestricted tenant-content reader.
- Render setup only if trusted server setup status indicates uninitialized. If sealed, redirect to \`/login\` or clear “Already initialized” screen; no persistent “Create Superadmin” CTA anywhere.
- If PostgreSQL is unavailable or migrations incompatible, show repair instructions without accidentally offering signup. Keep input except actual secrets on safe validation retries; clear high-value proof after consumption/failure as policy dictates.
- Include “Where do I get the setup proof?” instructions pointing to CLI, without exposing shell commands that echo secret into shared logs.

**Prove**
- Playwright new installation browser walkthrough: CLI proof obtained by operator, valid account created, redirect to login, second tab/new browser cannot register another admin, refresh/Back cannot regain setup. Error and 320px keyboard flows verified.

### S0.3 — Local identity, sessions and emergency recovery

#### S0-PR06 — Real BFF login and session lifecycle

**Implement**
- ASP.NET Core Identity salted/iterated password hashing, normalized unique username, password policy (starting target: at least 12 characters and reject obviously compromised/common passwords where supported), account lockout/rate limiting, safe anti-enumeration errors and security audit.
- Same-origin, HttpOnly, Secure BFF session cookie; explicit SameSite/CSRF Origin handling; trusted proxy enforcement; no auth token or password in localStorage/sessionStorage. A **separately durable and at-rest protected ASP.NET Core Data Protection key ring** must survive restart and be distinct from wizard-secret KEK/MEK.
- Create durable user session ID and revocation/security-stamp lifecycle; \`POST /api/v1/auth/login\`, \`POST /api/v1/auth/logout\`, \`GET /api/v1/auth/me\`, \`GET /api/v1/auth/sessions\`, \`DELETE /api/v1/auth/sessions/{id}\` (or equivalent explicit logout-all). Logout actually invalidates server state, not merely clears the browser cookie.
- Default target session timeouts should be documented and configurable (e.g. 30-minute idle/12-hour absolute unless validated product policy differs). Reauthentication for password changes and later secret-key policy actions.
- Product dashboard only after a server-validated session and authorized tenant membership; expired/invalid session UI directs user to login without losing nonsecret navigation context.

**Prove**
- Wrong password/rate-limit, role/tenant forged URL, CSRF and untrusted proxy headers, cookie replay after logout, password change revocation, session rotation, idle/absolute expiry and server restart are tested. Two browser tabs remain tenant-safe.

#### S0-PR07 — Account settings, MFA baseline and offline recovery

**Implement**
- Authenticated profile view/change username where permitted, password change requiring current password, profile validation, user session list/revoke and security activity with redacted audit metadata. Avoid compulsory SMTP for first owner.
- Support **TOTP MFA plus recovery codes** in S0 if enabled in UI; stored TOTP/recovery material protected according to Identity standards, codes displayed once and hashed/revocable. Do not show inert passkey controls; optional WebAuthn support remains future until implemented. High-risk key/owner operations require recent reauthentication/step-up as configured.
- Privileged local \`rag-admin owner recover\` command requiring OS-level operator access, auditable target validation, explicit confirmation, reset existing authorized owner authentication/recovery configuration without creating a new public enrollment pathway. CLI recovery never toggles \`SEALED\`, rotates invalidated sessions/credentials and never logs password/recovery material.
- Show friendly locked account/MFA failure/recovery UX; no silent bypass when email unavailable.

**Prove**
- Session invalidation after CLI recovery, expired/current session behavior, wrong TOTP, used recovery code replay, unauthorized profile/key actions and persistence across restart; no Superadmin re-bootstrap.

### S0.4 — MEK, envelope encryption and recoverable key protection

#### S0-PR08 — Secret crypto and storage primitives

**Implement**
- \`IKeyProtectionProvider\`, \`ISecretEncryptionService\`, \`SecretReference\`, tenant/purpose/record-version context, protector capabilities/status, and explicit \`NOT_INITIALIZED / RECOVERY_PENDING / ACTIVE / LOCKED / ROTATING / DEGRADED\` state contracts.
- Migrations for \`key_protection_profiles\`, \`key_versions\`, \`key_envelopes\`, \`integration_secret_records\`, \`integration_secret_versions\`, key operations and audit. Persist **ciphertext + AEAD nonce/tag + wrapped DEK + key/protector version + authenticated tenant/deployment/secret/purpose/schema context**, never raw root/DEK/secret.
- Generate independent random **per-secret-version 256-bit DEKs**, encrypt via \`AesGcm\` with correctly managed unique nonces, wrap through protector, use versioned authenticated context and vetted cryptographic primitives. Do not create custom cipher/KDF or leave plaintext via EF change tracker logging.
- Implement server-only credential **store/rotate/delete/reference** operations enforced by scope, and a narrow internal authorized acquisition contract **without a public GET plaintext secret API**. Future .NET-to-Python provider broker is **not** implemented in S0; only contract/trust boundary is documented.
- Use a generated **server-side cryptographic canary secret** for local round-trip/recovery testing; do not ship a public debug decrypt endpoint or fake third-party connection.

**Prove**
- Round trip, ciphertext/tag/nonce/AAD alteration, tenant/purpose/key substitution, stale versions, replay, truncated payload, concurrent writes and mass encrypt tests. Public JSON, browser state, SQL dump, support export and logs reveal no original canary plaintext or unwrapped keys.

#### S0-PR09 — Software-root, KMS and unavailable-key behavior

**Implement**
- Privileged \`rag-admin key-root init\` generates a **persistent random high-entropy root** into a protected *host-managed* secret mount, separate from PostgreSQL and normal DB backups; document Linux \`0600\` owner/group restrictions and Windows ACL equivalent. Compose passes the read-only secret file to the app at a stable configured path; neither image nor \`.env\` embeds it. Avoid printing it in routine logs.
- On restart read the existing root and verify an authenticated canary/key version. If database has encrypted keys and file is absent/wrong, enter \`LOCKED\`/recovery state; never create a fresh root automatically.
- Provide **explicit** \`manual-unlock\` mode if supported (operator accepts per-restart unlock), with safe in-memory scope and documented downtime; if not implemented, do **not** present it as available.
- Implement a real **Azure Key Vault/KMS** wrap/unwrap provider for hosted profile through managed workload identity, with key ID/version allowlist, IAM, timeouts, retry/backoff and audit. Mocked KMS tests in CI are useful, but production capability must be verified against an actual test vault before claiming support. Unsupported cloud environments cannot be marked production ready for their profile.
- Expose only safe \`GET /api/v1/security/key-protection/status\` read model and authorized initialization/activation mutations. Decrypt-dependent operations fail closed if KMS/root is missing, without breaking local sign-in/admin repair.

**Prove**
- One host restart with same root succeeds, absent/wrong/rotated-out root locks secrets but not the app, secret not present in image/Compose variables/logs/database and KMS IAM failure/rate-limit/timeout never triggers software downgrade. Document separate SaaS deployment conformance test.

#### S0-PR10 — Independent recovery, migration, rotation and UI

**Implement**
- Authorized first-run **Protect your installation** wizard: show only supported software/KMS choices, initialize approved protector, explain host-loss risk, export encrypted recovery envelope, **separately generate/handle high-entropy recovery secret** or use externally controlled escrow, verify an independent recovery challenge, then activate \`ACTIVE\`. Do not display the MEK/DEK or bundle recovery secret with its envelope.
- For offline software-root recovery, create a versioned authenticated kit including encrypted key envelope, key/version metadata, scheme and restore instructions. Store recovery material separately from root and DB/backup. Provide privileged recovery CLI that consumes independently protected kit and rewraps keys under a *new* host root, producing nonsecret audit/report.
- Allow key rotation by creating new KEK/root version and **journaled idempotent DEK rewrapping**; keep dual-version reads while any active envelope references old key; test crash/resume before retiring the old key. Provider credential rotation is a **different operation** from rewrapping.
- Design nonsecret Settings > Security > Encryption Keys showing protector, key version, activation/recovery readiness, audit, rotate/recover actions with reauthentication; no unsupported TPM connect button.
- **KMS caveat:** Non-exportable external KMS roots require provider-supported recovery/escrow policies and a *separate* tested hosted recovery drill; do not promise exporting KMS private material.

**Prove**
- Actual **replacement-host exercise** using database backup + protected recovery envelope + separately held recovery secret, *not* copying original root mount. Owner and tenant remain, bootstrap stays SEALED, key reference rewrapped under new root, test canary decrypts; original account sessions are appropriately revoked and plaintext never exported.
- Concurrent secret save/rotation, interruption halfway through rewrap, old-key retirement while referenced, unauthorized rotate/recover and wrong recovery code are denied or safely recoverable.

### S0.5 — Actual Setup Center (no speculative providers)

#### S0-PR11 — Guided setup, capabilities and settings

**Implement**
- Setup Center uses **server-owned, versioned, tenant/deployment-scoped capability definitions** and durable wizard progress. Each available step has clear description, prerequisites, editable fields only where implemented, status, test, save, retry, skip-when-permitted and contextual errors. Current S0 steps: infrastructure readiness, initial tenant/profile, account/MFA recovery readiness, encryption/recovery readiness, finish → real dashboard.
- Future provider descriptors may be displayed under “Available after [feature] is installed”, **noninteractive**. Do not persist Foundry/embedding/LLM/OIDC secrets or show live test buttons without actual adapter and consuming feature.
- After successful first admin login, guide owner once; later logins go to dashboard, with **dismissible** incomplete-setup status and Settings > Setup Center for authorized admins. Regular users never see deployment-secret screens.
- Implement server-backed \`GET /api/v1/setup/status\`, \`GET /api/v1/setup/capabilities\`, \`PATCH /api/v1/setup/progress\` (authorized + validated) and \`GET/PATCH /api/v1/tenant/profile\` as appropriate. Feature availability must be derived from backend capability readiness, not a browser checkbox or arbitrary client-provided \`ACTIVE\`.
- Dashboard shows real deployment name, tenant/profile, installed build version, DB health, authentication, protector health and remaining genuine setup tasks. No fake ingestion/search counters or navigation to non-existent features.

**Prove**
- Resume after refresh/logout/restart, skip optional steps, no forced wizard for normal users, tenant profile saves and survives restart, invalid backend health shows actionable degradation, and unavailable providers have no reachable save/test API. Browser and API deny privilege escalation or setting completion purely by forging requests.

### S0.6 — Production hardening and release gate

#### S0-PR12 — Security testing, UX audit, release packaging and operations

**Implement**
- Aggregate focused adversarial tests and an S0 threat register (bootstrap takeover, credential/session compromise, CSRF/forwarded-header spoof, RLS bleed, root-key loss, recovery escrow theft, audit/log secret leakage, malicious tenant overrides). Each HIGH/CRITICAL item has a test, mitigation owner and no unresolved unsafe default.
- Test a clean install and a **deployment restart/upgrade/rollback rehearsal** using supported DB migration and Data Protection key-ring handling; distinguish schema rollback limits. Produce safe backup/restore scripts or documented commands with clear DB/secret/recovery separation and audit requirements.
- CI: real PostgreSQL integration suite (e.g. Testcontainers), auth/bootstrap concurrency, crypto tamper/rotation, route/RLS, Playwright desktop/mobile, axe automation plus manual keyboard/screen-reader smoke, dependency/licensing/secret scanning and release artifact checks. No mocked KMS claimed as production-tested KMS.
- Security defaults: TLS/reverse-proxy trust, least privilege runtime DB, safe cookie/headers/CSP, rate limits, secret-safe tracing, health endpoint disclosure limits, restrictive container filesystem/capabilities where compatible, reasonable resource budgets and upgrade/security runbooks.
- Final release review records supported deployment matrix (minimum Windows/Linux/Compose versions, storage/CPU/RAM), measured startup and auth UX latency, recovery times, known limitations and decisions still pending (license, vendor-backed managed SaaS profile and general RPO/RTO). Avoid claiming global security compliance.
- Create \`docs/runbooks/initial-install.md\`, \`bootstrap-and-owner-recovery.md\`, \`secrets-and-key-recovery.md\`, \`backup-restore.md\`, \`upgrade.md\` and \`troubleshooting.md\`. Document how to generate proof and key-root secrets without automatic log disclosure.

**Prove**
- Release candidate runs the scenarios in §§6–8 below from a clean checkout and passes them under the same image artifacts intended for distribution. No critical/high security issue without an accepted owner-signed mitigation.

## 6. Minimum API and database contract inventory

Contracts below are **implementation starting points**, not a claim of checked-in routes or schemas. Retain versioned OpenAPI, generated TS clients and consistent Problem Details errors (with nonsecret correlation ID).

| API / CLI | Auth/scoping | Delivery |
| --- | --- | --- |
| \`GET /api/v1/system/health/live\`, \`.../ready\` | Anonymous minimal status; detailed diagnostics authenticated | PR01 |
| \`GET /api/v1/bootstrap/status\` | Anonymous minimal state only; SaaS customer plane disabled | PR04 |
| \`rag-admin bootstrap-proof generate\` | Local operator CLI, explicit interactive output or protected file | PR04 |
| \`POST /api/v1/bootstrap/complete\` | Anonymous only with valid one-use proof + CSRF/origin/TLS + uninitialized DB | PR04 |
| \`POST /api/v1/auth/login\`, \`logout\`, \`GET /auth/me\` | BFF cookie session, credential/CSRF rules | PR06 |
| \`GET /auth/sessions\`, \`DELETE /auth/sessions/{id}\` | Authenticated current user | PR06 |
| \`GET/PATCH /api/v1/users/me\`, password change/MFA | Current authenticated user; reauthenticate sensitive mutations | PR07 |
| \`rag-admin owner recover\` | Privileged local operator CLI only | PR07 |
| \`rag-admin key-root init\`, \`key-root restore\` | Operator identity plus protected files/escrow | PR09–PR10 |
| \`GET /api/v1/security/key-protection/status\` | Authorized deployment/security administrator; no key bytes | PR09 |
| \`POST .../initialize\`, \`.../verify-recovery\`, \`.../activate\`, \`.../rotate\` | Authorized, step-up, audited, version/ETag guarded | PR09–PR10 |
| \`GET /api/v1/setup/status\`, \`.../capabilities\`, \`PATCH .../progress\` | Deployment/tenant scoped; server derives truth | PR11 |
| \`GET/PATCH /api/v1/tenant/profile\` | Valid tenant membership and scoped settings permission | PR11 |

**S0 schema only:** Identity user/role/session records; bootstrap singleton/proof usage; deployments/installation profiles; tenants/memberships; audit; tenant settings; key-protection profile/version/wrapped key envelope; encrypted secret record/version; key rotation journal and setup-progress/capability status. Use secure Data Protection key-ring storage independently. No \`documents\`, CDM/element/table/search, \`stage_leases\`, outbox, OCR usage, subscriptions, MCP or knowledge-quality tables in S0 solely for future features.

**Bootstrap consistency:** Bootstrap identity is a deployment-global operation with intentionally restricted DB access. It is not accidentally subject to an unset tenant RLS policy. Tenant-owned rows are written under a controlled transaction after initial tenant creation; the bootstrap's privileged migration/setup path is tightly isolated from general runtime authorization. All ordinary subsequent tenant requests use the non-bypass app role.

**Key setup consistency:** \`SEALED\` refers to the initial-owner bootstrap, **not** MEK completion. A sealed installation with absent key protector must still permit login and secured setup completion; it may never reopen owner creation.

## 7. Executable end-to-end acceptance scenarios

Each ID is implemented as one or more automated tests; the corresponding PR can add coverage incrementally. Use **a real browser and PostgreSQL**, separate host/container restarts and negative tests where the scenario requires them.

| ID | Test scenario | Expected result |
| --- | --- | --- |
| **E2E-01** | Fresh Compose with valid DB and missing integrations | Only real S0 required dependencies gate startup; first-run setup available behind proof |
| **E2E-02** | First operator generates proof via privileged CLI, supplies username/password | Exactly one user, tenant owner membership, audit entry and SEALED commit |
| **E2E-03** | Parallel first-user POSTs, replayed/expired/guessed proofs | At most one succeeds, no orphan membership/user; setup cannot reopen |
| **E2E-04** | After sealing: new browser, logout, upgrade, normal restart, old setup URL/direct POST | Owner-registration rejected/redirected; protected login still works |
| **E2E-05** | DB down or migration mismatch | Maintenance state; never incorrectly assumes fresh installation |
| **E2E-06** | Login, logout, expired/revoked cookie, password change, CSRF and hostile proxy header | Unauthorized requests denied; sessions truly revoked and traceable |
| **E2E-07** | Two tenants, forged route IDs, reused Npgsql connections after cancellation/rollback | No cross-tenant read/write, no lingering tenant RLS context |
| **E2E-08** | First owner follows Setup Center, skips allowed optional step, revisits after logout | Real progress resumes; no repeated blocking wizard for normal login |
| **E2E-09** | Attempt to test/save nonexistent Azure/LLM/OIDC provider from UI/direct API | No interactive fake provider and no actual unsupported save/test mutation |
| **E2E-10** | Initialize software root, encrypt test canary, restart Compose unattended | Canary decrypts; protected key version and bootstrap state remain unchanged |
| **E2E-11** | Remove or change root secret mount with existing encrypted data | LOCKED/RECOVERY_REQUIRED, no silent new MEK; admin can login/recover |
| **E2E-12** | Restore DB onto replacement host using independent encrypted recovery kit | Owner remains sealed, key material rewrapped, test canary decrypts, no plaintext export |
| **E2E-13** | Tamper ciphertext/tag/AAD/purpose/tenant/version and attempt unauthorized rotate | Cryptographic/auth failures; no plaintext disclosure or cross-tenant access |
| **E2E-14** | Crash halfway through KEK rewrap and resume | Old and new key references consistent; no inaccessible ACTIVE secret |
| **E2E-15** | SaaS/public customer mode calls initial-owner setup route | No public platform Superadmin creation |
| **E2E-16** | Keyboard + mobile form flows, error focus, back navigation and load/retry states | Accessible, responsive, real state and no broken CTA |
| **E2E-17** | KMS provider unavailable or permission denied in hosted test deployment | Secret operations fail closed; login and secure diagnostics remain available |
| **E2E-18** | Remove optional Qdrant/OCR/model/queue components | Local S0 login, setup, settings and recovery still work |
| **E2E-19** | Dump app logs, DB, CI output, error reports and network responses | No saved wizard secret, root key, recovery secret, bootstrap proof or raw session token |
| **E2E-20** | CLI owner emergency recovery after lost login/MFA | Existing owner restored under operator control, all prior sessions revoked, SEALED unchanged |

For E2E-12, don't substitute “we manually re-entered all API keys” for actual recovered secret decryptability. For E2E-19, redaction/absence must be checked against **known generated canary values**; generic keyword searches alone are insufficient.

## 8. Release gate checklist — S0 production ready for stated scope

- [ ] PR01–PR12 merged, no unresolved critical/high defects, all applicable CI checks green; no production claim based on documentation.
- [ ] Functional self-hosted install bootstrap, sealed account, login/logout, tenant profile, setup dashboard and encryption/recovery flow demonstrated on release images.
- [ ] First-run owner creation is server-side single-use, race-proof, auditable and permanently sealed; SaaS mode cannot expose it.
- [ ] Runtime DB, migration identity, tenant/RLS, session cookie, reverse-proxy, CSRF and secret access are hardened and negative-tested.
- [ ] Local secrets encryption and key protection remain decryptable across restart; missing key locks safely; replacement-host recovery and partial key rotation tested.
- [ ] Data Protection key ring persists with appropriate protection; bootstrap, key-encryption and auth cookie/recovery lifecycles are separately documented.
- [ ] Setup Center registry is truthful; only real feature-backed providers may display Connect/Test/ACTIVE. No AI integration or TPM agent required for S0.
- [ ] All visible S0 routes pass loading/empty/validation/error/unauthorized/session-expired/mobile/keyboard and manual accessibility review.
- [ ] Scoped audit/trace, metrics/readiness/diagnostic IDs, process limits and actionable failure UX exist; no leaked secrets or sensitive correlation details.
- [ ] CLI commands, secret mount access, off-host recovery envelope storage, independent backups, migration/version compatibility and full restore documented.
- [ ] Signed/tagged OCI release mechanism and dependency/license inventory are in place or explicitly gated before **public** distribution. Open-source license selection remains an owner decision; do **not** present unlicensed code as an approved OSS release.
- [ ] Minimal supported OS/Compose/memory version matrix and measured startup/login/runtime baselines published; RPO/RTO/SLOs remain planning objectives until validated.
- [ ] Demo follows §1 user outcome using *actual application functionality* rather than mocked visual states.

**No automatic acceptance:** The release checklist is a required test protocol. Items not met are blockers for labeling the relevant feature production-ready; unsupported deployment modes may be marked unavailable rather than misrepresented as supported.

## 9. Risks, decisions and escalation

| Risk / open decision | Plan response |
| --- | --- |
| First owner taken by internet attacker | Require protected interactive CLI proof, strict TLS/proxy/origin/CSRF/rate limits and atomic SEALED state |
| DB/session key persistence confusion | Keep bootstrap state, Data Protection key ring, wizard MEK and recovery escrow distinct; test restart/restore of each |
| Software-root host compromise | A protected file is not hardware-backed; use separate recovery and recommend KMS for hostile hosts/multi-node deployments |
| Operator loses both boot root and independent recovery | Credentials may be permanently unrecoverable; report truthfully, no magic bypass/reset |
| Repeated S0 delays due to out-of-scope AI integrations | Enforce PR11 capability registry only; cards are activated in their consuming slices |
| RLS pooling or tenant role bypass | Transaction-scoped tenant context, nonbypass runtime role and real two-tenant pooled tests before role UX |
| Custom auth/token crypto | Use ASP.NET Core Identity and established sessions; defer OpenIddict until a real OAuth token-issuer use case |
| KMS credentials not available in CI | Test with stub for logic but require separate real test-vault integration before labeling hosted KMS supported |
| Windows/Linux deployment differences | Target one tested minimal self-hosted profile initially; publish accurately supported matrix rather than claiming untested cross-platform TPM |
| Sensitive configuration shown in support UI | API returns only nonsecret status, IDs and redacted error codes; audit redaction and log-canary tests |
| License not yet chosen | Block public OSS release decision, **not** private S0 engineering |

**Escalate via reviewed issue/ADR** when a PR proposes an altered v1.6 trust boundary (e.g. allowing Python DB writes, bypassing RLS, reintroducing public first-user signup, permanently unprotected keys, or claiming TPM support without agent validation). A change to sequence or a UI component that preserves invariants can update this plan without gratuitous architecture redesign.

## 10. Work initiation checklist (first handoff)

**First engineering assignment: S0-PR01.** A developer or Codex agent should inspect the repo, confirm it contains only architecture/slicing docs, scaffold the smallest runnable .NET/React/PostgreSQL solution, add minimal Compose profile, CI and health checks, and open a PR **without changing the core architecture**. Deliver a running local startup command, passing smoke suite, screenshot of real API-driven health UI where present, and precise missing next dependencies.

**Second assignment: S0-PR02** can start against the PR01 shell once routing/API contracts are stable; **S0-PR03** builds tenant-safe migrations and tests before first-owner creation. Ship visible UX continuously, but do not create an unprotected anonymous “first admin” login path to accelerate demo.

**No application code has been implemented by writing this plan.** Implementation readiness is achieved only when PRs, smoke/integration/adversarial tests, Compose startup and user-journey evidence actually pass.
