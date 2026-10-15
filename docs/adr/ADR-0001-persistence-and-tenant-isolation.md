# S0-PR03 persistence, tenant isolation and RLS design proposal (issues #10-#13)

## 1. Table list for #10 (Minimum S0 Schema)
Only minimum tables required for S0. Explicitly **no** encryption/key-ring tables (`key_protection_profiles`, `key_versions`, `key_envelopes`, `integration_secret_records` deferred to PR08–PR10) and **no** document/CDM/search/worker tables (`documents`, `elements`, `retrieval_units`, `index_entries`, `stage_leases` deferred to S1+).

| Table | Scope | Key Columns & Constraints | Description |
|---|---|---|---|
| `asp_net_users` | Deployment-Global | `id` (UUID PK), `user_name` (VARCHAR(256) UNIQUE NOT NULL), `normalized_user_name` (VARCHAR(256) UNIQUE NOT NULL), `password_hash` (TEXT NOT NULL), `security_stamp` (TEXT NOT NULL), `concurrency_stamp` (TEXT NOT NULL), `lockout_enabled` (BOOL NOT NULL), `lockout_end` (TIMESTAMPTZ NULL), `access_failed_count` (INT NOT NULL) | ASP.NET Core Identity credentials. Roles-free (no `AspNetRoles`/`AspNetUserRoles`). |
| `asp_net_user_tokens` | Deployment-Global | `user_id` (UUID FK `asp_net_users.id`), `login_provider` (VARCHAR(128)), `name` (VARCHAR(128)), `value` (TEXT NULL). PK: `(user_id, login_provider, name)` | Verification tokens, MFA state, and password reset tokens. |
| `deployment_bootstrap_state` | Deployment-Global (Singleton) | `id` (INT PK CHECK (`id = 1`)), `status` (VARCHAR(32) NOT NULL: `UNINITIALIZED`, `SEALED`), `bootstrap_proof_hash` (VARCHAR(128) NULL), `proof_expires_at` (TIMESTAMPTZ NULL), `proof_consumed_at` (TIMESTAMPTZ NULL), `sealed_at` (TIMESTAMPTZ NULL), `sealed_by_user_id` (UUID NULL FK `asp_net_users.id`) | Irreversible deployment initialization state; ensures exactly one first-run bootstrap. |
| `deployment_owners` | Deployment-Global | `id` (UUID PK), `user_id` (UUID NOT NULL FK `asp_net_users.id`), `role` (VARCHAR(64) NOT NULL: `DeploymentSuperadmin`), `assigned_at` (TIMESTAMPTZ NOT NULL), `assigned_by` (UUID NULL). UNIQUE (`user_id`, `role`) | Deployment-level platform operator assignments; completely separate from tenant membership. |
| `tenants` | Deployment-Global | `id` (UUID PK), `name` (VARCHAR(128) NOT NULL), `slug` (VARCHAR(64) UNIQUE NOT NULL), `status` (VARCHAR(32) NOT NULL: `Active`, `Suspended`, `Terminated`), `created_at` (TIMESTAMPTZ NOT NULL) | Organization tenant registry. |
| `tenant_memberships` | Tenant-Scoped | `tenant_id` (UUID NOT NULL FK `tenants.id`), `user_id` (UUID NOT NULL FK `asp_net_users.id`), `role` (VARCHAR(64) NOT NULL: `TenantOwner`, `TenantAdmin`, `TenantMember`), `is_first_owner` (BOOL NOT NULL DEFAULT FALSE), `joined_at` (TIMESTAMPTZ NOT NULL). PK: `(tenant_id, user_id)`. Partial UNIQUE: `CREATE UNIQUE INDEX uq_first_owner ON tenant_memberships(tenant_id) WHERE (is_first_owner = true)` | Immutable membership association. Enforces exactly one initial tenant owner. |
| `tenant_profiles` | Tenant-Scoped | `tenant_id` (UUID PK FK `tenants.id`), `display_name` (VARCHAR(128) NOT NULL), `settings_json` (JSONB NOT NULL DEFAULT '{}'), `created_at` (TIMESTAMPTZ NOT NULL), `updated_at` (TIMESTAMPTZ NOT NULL), `concurrency_token` (UUID NOT NULL) | Tenant-specific configuration and preferences. |
| `auth_sessions` | Deployment-Global | `id` (UUID PK), `user_id` (UUID NOT NULL FK `asp_net_users.id`), `refresh_token_family_id` (UUID NOT NULL), `refresh_token_hash` (VARCHAR(128) NOT NULL), `expires_at` (TIMESTAMPTZ NOT NULL), `created_at` (TIMESTAMPTZ NOT NULL), `last_seen_at` (TIMESTAMPTZ NOT NULL) | Durable session tracker for BFF token rotation and refresh validation. |
| `auth_session_revocations` | Deployment-Global | `session_id` (UUID PK FK `auth_sessions.id`), `revoked_at` (TIMESTAMPTZ NOT NULL), `reason` (VARCHAR(64) NOT NULL) | Immutable revocation records for logout, password changes, and compromised sessions. |
| `audit_events` | Append-Only (Global/Tenant) | `id` (UUID PK), `occurred_at` (TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp()), `actor_user_id` (UUID NULL), `actor_type` (VARCHAR(32) NOT NULL), `deployment_id` (UUID NULL), `tenant_id` (UUID NULL), `action` (VARCHAR(128) NOT NULL), `target_resource` (VARCHAR(128) NOT NULL), `target_id` (VARCHAR(128) NULL), `outcome` (VARCHAR(32) NOT NULL: `Success`, `Failure`, `Denied`), `correlation_id` (VARCHAR(64) NULL), `client_ip` (INET NULL), `details_json` (JSONB NULL) | Immutable audit log. DB rules/triggers prohibit `UPDATE` and `DELETE`. |

## 2. TenantContext Resolved from Authenticated Membership + Route
- **Resolution Flow**:
  1. Incoming requests targeting tenant resources match routes containing the tenant identifier (e.g., `/api/v1/tenants/{tenantId}/...` or route metadata).
  2. The server extracts the authenticated caller (`ClaimsPrincipal.Subject` / `userId`) from the verified session.
  3. The server validates that the caller holds an active record in `tenant_memberships` for `(route_tenant_id, user_id)`.
  4. On match, `ITenantContext` is initialized as scoped for the request lifetime with `TenantId = route_tenant_id` and caller's `TenantRole`.
- **Anti-Patterns Prevented**:
  - **No Session-Global Tenant**: Tenant context is never stored in a mutable session cookie or ambient browser claim. A user belonging to multiple organizations switches tenants by navigating routes, preventing cross-tab bleeding and race conditions.
  - **No Client Body Trust**: Tenant identity is never accepted from unverified request payloads or client-supplied headers.
  - **Fail-Closed**: Requests with missing, inactive, or mismatched memberships immediately abort with RFC 7807 403 Forbidden / 404 Not Found before hitting application services. Deployment-global endpoints execute with `TenantContext.None`.

## 3. Transaction Boundary & Pooled-Connection Safety
- **PostgreSQL Session Variable**:
  - RLS policies read `current_setting('app.current_tenant_id', true)::uuid`.
  - Tenant queries must execute inside an explicit database transaction boundary (`NpgsqlTransaction`).
  - Upon transaction start, execute:
    ```sql
    SELECT set_config('app.current_tenant_id', @tenantId::text, true);
    ```
  - The third parameter (`is_local = true`) guarantees the setting is local to the transaction and automatically cleared on `COMMIT` or `ROLLBACK`.
- **Pooled-Connection Leak Mitigation**:
  1. **Fail-Closed Predicates**: In all RLS policies, use `NULLIF(current_setting('app.current_tenant_id', true), '')::uuid`. When unset, it returns `NULL`, causing tenant comparisons to fail closed (evaluating to false, returning 0 rows).
  2. **Connection Lifecycle Hooks**: In EF Core, register an `IDbConnectionInterceptor` / `IDbTransactionInterceptor`. If a connection is returned to the Npgsql pool after a cancellation or unhandled exception without a clean commit/rollback, execute `DISCARD TEMP;` or `RESET ALL;` on return/checkout.
  3. **Mandatory Transaction Enforcement**: Tenant-scoped `DbContext` operations throw an `InvalidOperationException` if an attempt is made to execute tenant SQL outside an active tenant-configured transaction.

## 4. Database Roles, Grants, and RLS Enforcement
- **Three Dedicated Roles**:
  1. `migrator` (Schema Owner):
     - Owns the database schema, tables, and migrations.
     - Has full DDL privileges (`CREATE`, `ALTER`, `DROP`).
     - Used exclusively during deployment/bootstrap by CLI tools (`rag-admin`) or migration runners; never used by the runtime API.
  2. `rag_app` (Runtime Application Role):
     - Configured with `NOBYPASSRLS`.
     - Non-owner of all tables; zero DDL permissions (`REVOKE ALL ON SCHEMA public FROM rag_app; GRANT USAGE ON SCHEMA public TO rag_app;`).
     - DML only: `GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO rag_app;`.
     - Bound by Row Level Security on all tables where RLS is enabled.
  3. `rag_probe` (Operational Health Role):
     - Read-only probe for readiness checks (`/ready`).
     - Minimal privilege: `GRANT USAGE ON SCHEMA public TO rag_probe; GRANT SELECT ON deployment_bootstrap_state TO rag_probe;`.
     - Has zero access to tenant tables, identity records, credentials, or audit logs.
- **RLS Policy Definition**:
  - Applied to all tenant-scoped tables (`tenant_memberships`, `tenant_profiles`, etc.):
    ```sql
    ALTER TABLE tenant_profiles ENABLE ROW LEVEL SECURITY;
    ALTER TABLE tenant_profiles FORCE ROW LEVEL SECURITY;

    CREATE POLICY tenant_isolation_policy ON tenant_profiles
      AS RESTRICTIVE
      USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
      WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
    ```
  - `FORCE ROW LEVEL SECURITY` ensures policies apply even if table-owner privileges are ever inadvertently used.
  - Deployment-global tables (`asp_net_users`, `deployment_bootstrap_state`, `deployment_owners`, `tenants`, `auth_sessions`) do not use tenant RLS; their access is guarded by server authorization policies.

## 5. Composite Tenant Foreign Keys `(tenant_id, id)`
- **Referential Isolation**:
  - Single-column UUID foreign keys (e.g., `FOREIGN KEY (parent_id) REFERENCES parents(id)`) allow accidental cross-tenant links if code mixes Tenant A's child row with Tenant B's parent ID.
  - All tenant-scoped entities must define composite primary or unique keys: `PRIMARY KEY (tenant_id, id)` or `UNIQUE (tenant_id, id)`.
  - All downstream relations must enforce composite foreign keys:
    ```sql
    FOREIGN KEY (tenant_id, parent_id) REFERENCES parent_table (tenant_id, id) ON DELETE CASCADE
    ```
- **Performance & Indexing**:
  - Primary indexes leading with `tenant_id` (`(tenant_id, id)`) align exactly with the RLS filter predicate, guaranteeing index-backed seek performance without scanning cross-tenant pages.

## 6. Persistence Architecture & Module Boundary Isolation
- **Code Organization**:
  - Create a single infrastructure persistence project: `src/Persistence/RagPipeline.Persistence/RagPipeline.Persistence.csproj`.
  - Project references **only** `RagPipeline.BuildingBlocks`.
  - Contains a single unified `RagPipelineDbContext` managing all S0 entity configurations, Npgsql connection interceptors (`set_config`), and migrations.
  - Functional modules (`IdentityAccess`, `TenantManagement`, `DeploymentCapabilities`) remain purely decoupled: they define repository/query interfaces or rely on domain events/contracts in `BuildingBlocks`.
  - `apps/api/RagPipeline.Api` references `RagPipeline.Persistence` strictly at the composition root for service registration.
- **Architecture Test Adjustments (`ArchitectureTests.cs`)**:
  1. `NoProject_ReferencesProhibitedAssemblies` & `NoCsproj_ReferencesProhibitedPackagesOrProjects`:
     - Keep `"EntityFramework"` prohibited across all modules (`src/IdentityAccess`, `src/TenantManagement`, etc.), `BuildingBlocks`, and API controllers.
     - Add an explicit allowlist permitting `"EntityFramework"` / `"Npgsql.EntityFrameworkCore.PostgreSQL"` **only** in `RagPipeline.Persistence` and test projects.
  2. `ModuleCsprojFiles_OnlyHaveProjectReferenceToBuildingBlocks`:
     - Retain the rule enforcing that modules only reference `BuildingBlocks`. `RagPipeline.Persistence` complies by referencing only `BuildingBlocks`.
- **Tradeoffs & Alternatives**:
  - *Alternative A: Per-Module DbContexts*: Each module has its own DbContext.
    - *Rejection*: Makes atomic multi-module transactions (such as first-run bootstrap creating User, Tenant, Membership, and sealing State in one transaction) overly complex, requiring distributed transaction managers or manual Npgsql connection sharing.
  - *Alternative B (Proposed): Unified DbContext in Infrastructure*:
    - *Benefits*: Clean atomic transactions across S0 aggregates; centralized RLS configuration and connection lifecycle; preserves zero-leakage architectural boundaries in functional modules.

## 7. Superadmin vs. Tenant Owner Modeling
- **Distinct Identities & Permissions**:
  - **Deployment Superadmin (`deployment_owners`)**:
    - Global platform operator role. Manages system bootstrap, infrastructure health, global audit review, tenant provisioning/suspension, and emergency recovery.
    - **No Content Shortcut**: Possesses **zero** automatic access to tenant-scoped settings, workspaces, or documents. There is strictly no `IsSuperadmin => AllowAll` bypass in authorization handlers.
  - **Tenant Owner (`tenant_memberships`)**:
    - Scoped strictly to a specific `tenant_id`. Manages tenant profile, member invitations, and tenant policies.
    - Has no deployment-level platform capabilities (cannot view other tenants, cannot seal bootstrap, cannot run platform recovery).
- **First-Run Bootstrap Unification**:
  - In `POST /api/v1/bootstrap/complete`, the initial user is granted:
    1. A `deployment_owners` record (`role = 'DeploymentSuperadmin'`).
    2. A `tenant_memberships` record for the initial default tenant (`role = 'TenantOwner'`, `is_first_owner = true`).
  - Access to the initial tenant is achieved via explicit membership, preserving the invariant that operational administration and tenant data access are decoupled.

## 8. Test Plan, Migration Recovery, and Open Questions
- **Test Plan (Testcontainers / Real PostgreSQL)**:
  1. `TenantIsolationIntegrationTests`: Concurrently query Tenant A and Tenant B under `rag_app`. Verify zero leakage on reads/writes.
  2. `PooledConnectionLeakTests`: Execute a query setting `app.current_tenant_id`, simulate cancellation or error, recycle the connection back to the pool, and assert that subsequent operations fail closed without a lingering tenant ID.
  3. `DatabaseRolePrivilegeTests`: Connect as `rag_app` and attempt DDL (`CREATE TABLE`, `DROP TABLE`); assert Postgres rejects with permission denied. Connect as `rag_probe` and assert access to `asp_net_users` and `tenant_profiles` is denied.
  4. `AtomicBootstrapConcurrencyTests`: Fire parallel bootstrap requests; assert exactly one commits, sealing the singleton, while all others fail without orphaned rows.
- **Migration Recovery Runbook**:
  - All migrations run under the `migrator` role inside transactional DDL blocks (`BEGIN; ... COMMIT;`).
  - If a migration fails:
    1. Transactional rollback ensures no partial schema corruption.
    2. Check `__EFMigrationsHistory` to verify current applied version.
    3. The application startup detects schema version mismatch and returns HTTP 503 `SERVICE_UNAVAILABLE` (never falling back to `UNINITIALIZED`).
    4. Operators execute `dotnet ef database update` via `rag-admin` after resolving environmental issues; migrations provide explicit `Down()` steps.
- **Open Questions for Nexus**:
  1. *Audit Event Isolation*: Should `audit_events` RLS restrict tenant admins strictly to their `tenant_id`, while reserving deployment-global audit queries for `DeploymentSuperadmin` via a non-tenant query path?
  2. *ASP.NET Identity Custom Stores*: Should we implement lightweight custom `IUserStore<User>` / `IPasswordHasher` implementations to avoid generating EF Core migrations for unused Identity tables (`AspNetRoles`, `AspNetUserClaims`, `AspNetUserLogins`)?
  3. *Module Persistence Abstraction*: Should functional modules dispatch commands/queries via MediatR / BuildingBlocks interfaces, with handlers directly implemented in `RagPipeline.Persistence`?
