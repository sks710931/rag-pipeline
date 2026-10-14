# Vertical Slicing Strategy — Production-Ready UX from Slice 0

> **Status:** Implementation strategy / proposed delivery plan, not a claim of implemented functionality
> **Version:** 1.2 — lean, progressive Slice 0; recoverable software/KMS root; authoritative .NET worker/CDM contracts; earlier OCR review and security gates
> **Date:** 2026-10-15
> **Architecture baseline:** [architecture.md](architecture.md), **active/unfrozen v1.6** (34 major sections), especially §§32–33 MEK and cross-runtime contracts
> **Product:** Open-source self-hosted / enterprise on-premises / managed multi-tenant SaaS
> **Stack:** ASP.NET Core 10 + React / strict TypeScript + Tailwind; PostgreSQL; separate Python document workers; pluggable AI providers
> **Primary architectural style:** Modular monolith + feature-oriented vertical slices, with domain-driven boundaries and a small shared foundation

## 1. Product decision and non-negotiable rules

**Recommendation: adopt Vertical Slice Architecture.** Build complete, user-observable workflows from UI through authorization, application/domain logic, persistence, tests and operations. Organize slices **inside bounded contexts** instead of creating one service, project, database or microservice per slice. Keep the complex domain invariants in modules/aggregates; share only stable infrastructure primitives.

**Slice 0 must look, behave and be secured like a product that could be installed by a customer today**, despite containing only foundational capabilities. It is *production-quality for its advertised scope*, not falsely advertised as a complete RAG product. There are no demo-only pages, dead buttons, broken empty states, fake integration connections or hardcoded sample results in production paths.

1. **First-run ownership:** On a genuinely uninitialized self-hosted deployment, show a one-time secure setup flow asking for a username and password, which creates the initial **deployment Superadmin / Platform Owner**, plus an initial tenant and tenant-owner membership. Do not present this option again after successful bootstrap, even after logouts, restarts, updates or visits from new browsers. See §4.
2. **SaaS separation:** A public SaaS user can create or join a **tenant** through controlled registration/invitation, **never** bootstrap a platform Superadmin. SaaS operator initialization is private CLI/operator-plane work, not a public web route.
3. **First authenticated owner login:** Offer a polished, guided **Integration Setup Center** with one provider or capability at a time. Users can test, save, skip permitted optional steps, revisit them later and see precise consequences. Do not harass every user at every login. See §5.
4. **One shared core:** Self-hosted and SaaS use identical domain, API, policy, permission and CDM contracts. The self-hosted app does not contact a vendor SaaS control plane for startup, authentication or normal document processing.
5. **Security by default:** No default password, public setup loophole, privileged role inheritance into tenant documents, raw provider token in a browser, client-side-only permission check or unverified evidence disclosure.
6. **No premature microservices:** Deploy the ASP.NET Core modular monolith and React UI as one coherent app; Python document processing and queues remain independent worker processes per architecture.md.
7. **Production UX is continuous:** Every slice has a real entry point, understandable state, success path, failure path, keyboard/mobile support, accessible feedback, tenant/role checks, tracing and release tests. UI is never scheduled as a separate horizontal “polishing phase”.
8. **Evidence integrity first:** The prior architecture's version pinning, CDM provenance, immutable originals, review states, claim-check, fencing, redaction policy and verified answer requirements survive every simplification.
9. **Feature claims are truthful:** If an integration/model feature is absent, show “Not configured” or “Unavailable” and guide an authorized owner to setup; don't imply that a click performed OCR or AI search when it did not.
10. **Key protection before secret storage:** Integration credentials are encrypted with per-secret authenticated DEKs under a versioned tenant/deployment wrapping hierarchy; the first wizard secret cannot be saved before its configured protector is ACTIVE. Hardware TPM is optional and deferred until a tested on-prem host-agent is available; recovery and no-insecure-downgrade remain mandatory.
11. **Single trusted persistence authority:** Python workers create immutable CDM/output artifacts and progress reports; .NET authenticates worker/job/stage/epoch and owns authoritative PostgreSQL CDM, page-state and READY commits.
12. **Progressive integrations:** The Setup Center is real in S0; only provider integrations whose consuming feature ships can become ACTIVE. S0 shall not require every AI/SSO adapter before the first document upload.

## 2. What “production-ready UX from Slice 0” means

### 2.1 The shared product shell must ship in S0

- **Visual system:** Consistent typography, spacing, color tokens, light/dark theme if supported, Lucide icons, restrained animation, branded but neutral OSS defaults, and a small, documented component kit.
- **App layout:** Responsive sidebar/navigation, breadcrumb/header, tenant switcher (shown when applicable), profile menu, settings, notifications placeholder only if actionable, and a clearly labeled System Health / Setup Center area.
- **Real states:** Loading and skeletons, permission denial, empty lists, validation errors, expired sessions, connectivity problems, maintenance mode, partially configured integrations, and successful saves.
- **Accessibility:** WCAG 2.2 AA **design target**, semantic forms and headings, predictable tab order, visible focus, error summaries, associated labels, screen-reader status announcements, accessible dialogs, minimum contrast, and reduced-motion support. Audit with automated and manual keyboard/screen-reader checks.
- **Screen sizes:** Primary desktop usability; correctly usable at narrow mobile widths, with no overflow of forms, dialogs or setup cards; protect long tables and code viewers with deliberate responsive patterns.
- **Feedback:** Specific validation/help text, non-misleading toast/inline alerts, confirmation for destructive actions, save-progress cues, deterministic retry and supportable error IDs. No toast that claims success before an API response.
- **Copy and trust:** User-facing language (“Connect Azure Foundry”, “Test connection”, “Not configured”, “Retry”), clear handling of credentials and data egress, no unexplained acronyms in onboarding, and no exposed internal stack traces or tokens.
- **Design-and-code parity:** Storybook or equivalent component examples where useful, typed design tokens, Playwright visual/interaction coverage, strict TypeScript, lint/format and no hardcoded magic CSS scattered across slices.
- **Meaningful shell:** System Overview, Setup Center, Profile & Security, and Tenant Settings have real backend-backed read models from S0. Navigation to future capabilities is absent or visibly disabled with an explanation—not a fake working page.

### 2.2 Release-quality bar for every screen

A screen is not complete until its **empty, populated, loading, validation, error, unauthorized, expired-session, and narrow-viewport** behavior has been reviewed. User input is persisted only through authorized APIs; all forms are keyboard-accessible, errors do not wipe entered information, and diagnostics reveal no secrets. Any capability exposed in navigation must have a working end-to-end path.

Mobile and accessible UX are test gates, not promise of identical desktop features on small screens. Exact latency and Web Vitals targets are defined after measurement; avoid fabricated performance guarantees.

## 3. Architecture and slice ownership

~~~text
apps/
  web/
    src/
      app/                   # shell, router, providers, design system
      features/
        bootstrap/
        auth/
        setup-center/
        tenants/
        members/
        documents/
        evidence/
        search/
        review/
        governance/
        briefs/
        integrations/
      shared/                # only stable UI primitives and generated API client

  api/                       # composition root, endpoint registration, auth, hosting

src/
  BuildingBlocks/            # tenancy, auth policy, outbox, result, clock, telemetry
  IdentityAccess/
    Features/
      CreateInitialOwner/
      LoginLocal/
      Logout/
      ConfigureIdentityProvider/
  TenantManagement/
    Features/
      CreateTenant/
      SwitchTenant/
      InviteMember/
  DocumentManagement/
    Features/
      UploadDocument/
      GetDocument/
  Ingestion/
    Features/
      StartIngestion/
      PublishDocumentVersion/
  EvidenceCatalog/
  Search/
  QueryEngine/
  HumanReview/
  EvidenceGovernance/
  EvidencePublishing/
  SensitivityPolicy/
  IntegrationsMcp/
  KnowledgeQuality/

workers/
  parser/                    # Python worker code organized by parsing capability
  indexing/

contracts/
  permissions/               # versioned permission catalog
  events/                    # durable event schemas
  cdm/                       # source/locator and evidence schemas

tests/
  unit/
  integration/
  security/
  e2e/
  accessibility/
  contract/
  resilience/
~~~

A **slice** comprises endpoint/handler/validation, feature-specific domain behavior, authorization, schema changes (where necessary), UI route/components, success/error telemetry, tests and release notes. Command/query handlers may use EF Core directly for straightforward CRUD; domain aggregates/services protect stateful invariants (tenant membership, owner transfer, processing lease, published document version, approved overlay, etc.).

Modules exchange documented application contracts or transactional-outbox events. Do not inject another module's DbContext and query its internal tables. Do not make “Controller”, “Services”, “Repositories”, “Models” global horizontal folders. Shared BuildingBlocks cannot become a dumping ground for business rules. Python workers use independently versioned job/artifact contracts, not C# entity classes.

**Slice IDs (S0...S15) are delivery increments, not replacements for architecture.md milestones I1...I6.** A slice can span a bounded module and frontend feature while remaining one coherent user capability. Prior architecture phase gates still apply.

## 4. Slice 0 — Secure first-run bootstrap, login and product shell

### 4.1 S0 user journey (self-hosted / on-prem)

~~~mermaid
flowchart TD
    A["App starts"]
    B["Infrastructure and DB preflight"]
    C{"Deployment bootstrap state"}
    D["One-time Secure Setup"]
    E["Provide bootstrap proof + choose username/password"]
    F["Atomic owner + initial tenant creation"]
    G["Seal bootstrap permanently"]
    H["Local sign-in"]
    I["Authenticated product shell"]
    P["Protect installation: software-root / KMS"]
    R["Verify recovery or approved external escrow"]
    Q["Activate encrypted secret store"]
    J["Guided Integration Setup Center"]
    K["Normal dashboard"]
    A --> B --> C
    C -->|UNINITIALIZED| D --> E --> F --> G --> H
    C -->|SEALED| H
    H --> I --> P --> R --> Q --> J --> K
~~~

When initialized but a dependency is down, show an actionable **Service Unavailable / Maintenance** state, not a false fresh-install screen. When no database or incompatible migrations exist, the app must not offer to create an owner until the authoritative bootstrap state is safely readable.

**First-run visible fields:** username, password, confirm password; email/contact for recovery is recommended but can be collected in a subsequent secured step if SMTP is not yet available. Display password strength and requirements without revealing secrets. Provide a clear note that the first account controls the local installation, plus a link to the security/recovery instructions.

A successful bootstrap must create:
1. Internal user with robust ASP.NET Core Identity password handling, locked-down authentication defaults and verified audit trail.
2. A **deployment-level Superadmin/Platform Owner** assignment for this installation.
3. The initial tenant, an active membership for this user and initial **Tenant Owner** role under that tenant, so setup can continue without another invite.
4. Deployment initialization state marked **SEALED** and a consumed one-time bootstrap credential, in the same consistent initialization transaction.

**Important security boundary:** Deployment Superadmin means operational administration of this installation. It is **not** an implicit right to bypass tenant/document ACLs. SaaS operator accounts cannot automatically read customer content.

**Master-key timing:** Initial user/tenant creation and permanent SEALED bootstrap **do not** depend on TPM availability. Physical TPM host agents are not mandatory in S0 and must be shown as unsupported until implemented; select software-root or managed KMS. After the first owner logs in, initialize an appropriate protector before any Integration Setup Center credential is stored. A failed key-protector initialization never reopens the web owner-creation route. The user may navigate the limited local dashboard/admin-recovery screens while secrets remain locked.

### 4.2 One-time guarantee must be enforced on the server

Use an authoritative **deployment_bootstrap_state** record, guarded with a unique constraint/serializable transaction or equivalent exclusive row lock. Suggested states:

| State | Allowed actions |
| --- | --- |
| UNINITIALIZED | Valid, protected bootstrap only |
| BOOTSTRAPPING | Exclusive active attempt; other requests rejected; rollback/recovery is safe |
| SEALED | No owner-creation API/UI path, ever, for this deployment database |

A React route guard is **not** security. After SEALED, the backend rejects the initial-owner endpoint and setup challenge; direct POSTs, reloads, new devices, restarts and upgrades cannot re-enable it. GET /setup may show Not Found or redirect to /login, but the server always decides. Never reinitialize simply because a user account is deleted; use separately audited recovery procedures.

**Prevent remote first-claim takeover.** The public “create first Superadmin” form is gated by a **one-time high-entropy bootstrap proof** available only to the local deployment operator (e.g. a CLI-generated, short-lived bootstrap code in a protected file; surfaced via local administrator CLI). Require HTTPS for remote use or restrict initial bootstrap to loopback through a secure setup tunnel. The proof is single-use, time-limited and rate-limited; do not log it, commit it, or expose it in a shared container log. CSRF/origin protection, request size limits and anti-enumeration apply.

Create user + tenant + memberships + SEALED atomically. If setup fails, roll back rather than leaving a partial owner. Concurrent attempts must yield exactly **one** successful owner. After successful setup invalidate all outstanding bootstrap proofs.

**Recovery is not re-bootstrap:** Provide a local, privileged, auditable CLI recovery process for lost owner credentials/IdP outage requiring OS/secret-store operator control, explicit warnings and session revocation. It may reset or recover an existing deployment owner but must **not reopen** web “Create Superadmin.” Backups/restores preserve SEALED and account records. A deliberately new empty database is a **new deployment**, not an exemption from this rule.

### 4.3 SaaS first-run is different

The SaaS deployment platform operator is initialized privately by CLI/IaC and a tightly controlled operator workflow. Never publish the deployment Superadmin enrollment route to customer-facing SaaS domains. Customer onboarding creates a **tenant owner**, after verified signup/invitation and controls, without granting deployment/platform privileges. A user's first login joins or creates only an authorized tenant. A hosted operator may administer infrastructure but does not automatically receive permission to read customers' RAG documents.

### 4.4 Local login and security baseline in S0

- ASP.NET Core Identity local credential verification; BFF-style secure HttpOnly session cookie, CSRF protection, logout, expiration and revocation. No password/token storage in localStorage.
- Configured strong password policy, throttling/lockout, account/session audit, safe login errors, reauthentication for sensitive changes, session inventory and sign out-all-sessions.
- Recovery mechanism documented before production deployment; default local recovery must not depend on a configured SMTP server. Encourage MFA after bootstrap; establish MFA/passkey integration interfaces and phased readiness per architecture §22.
- Username uniqueness rules, Unicode normalization/case behavior, explicit tenant membership, protected deployment-admin and tenant-owner assignment.
- Authentication page, first-run setup, profile, admin settings, sign out and session-expired UX are all actual working routes in S0.

### 4.5 S0 deliverables / release gate

**S0.1 — Foundation & visual system:** Docker Compose, migrations, app health, logging, secrets configuration, React shell, reusable form/data-state components, responsive navigation and automated CI.

**S0.2 — Deployment bootstrap:** protected single-use setup proof, username/password owner creation, first tenant, SEALED record, rollback/concurrency protection, first-run branded UX.

**S0.3 — Local authentication:** login/logout/session management, user profile/password change, authorization policies, initial tenant context and credible empty dashboard.

**S0.4 — Protect Installation and Recovery:** deliver tested **unattended persistent software-root** protector for self-hosted Docker and managed Vault/KMS protector for hosted environments. Generate or securely mount root secret outside DB/image, verify automatic restart and separately encrypted offline recovery, support opt-in manual unlock, rotate/test wrapped keys and report supported TPM only as a future capability (target S14). Activate secret storage before saving provider credentials.

**S0.5 — Guided Integration Setup Center:** ship **framework only plus any genuinely used S0 integration**: persisted resumable stepper/registry, tenant/profile/security/health cards, honest unavailable/coming-later states, secret-backed provider form/test pattern and safe diagnostic UX. Actual SMTP/OIDC/OCR/embedding/LLM/vision adapters and real probes ship with S1/S4/S5/S6/S7 functionality, never as dead S0 configuration.

**S0.6 — Hardening:** end-to-end first-install/login/SEAL, root-secret mount surviving unattended restart, missing-root locked mode, key rotation/credential recovery on replacement host, KMS outage, tenant-scoped RLS pooling and secure UI tests. TPM hardware/PCR/IPC tests apply only when optional TPM agent actually ships in S14. No OCR/model integration is needed to complete S0.

**S0 cannot be marked complete until all six increments integrate end-to-end.** Do not defer UX polish, integration-state errors or security tests to a later design sprint.

## 5. Guided third-party integration onboarding

### 5.1 Experience after the first successful Superadmin login

**Show each AVAILABLE integration one at a time after key protection is active.** Start with a brief checklist/stepper: “Secure your installation; connect services when they become available.” Each step has purpose, data-sharing explanation, scope, fields, **Test connection**, Save, Back, and Skip/Do later where safe. Show the present status and next enabled capability. Configuration is **persisted securely** and may be resumed across sessions.

Suggested setup journey for an initial self-hosted installation:

| Order | Step | Mandatory? | Outcome |
| --- | --- | --- | --- |
| 0 | Deployment health (PostgreSQL/auth migrations plus optional object store, queue and Qdrant) | **Only active Slice 0 services are mandatory** | Honest per-service health; unavailable future dependencies cannot block local login |
| 1 | Installation profile: tenant name, display name, timezone, logo (optional) | Core identity / minimum tenant name | Recognizable working tenant and profile |
| 2 | Account protection: local recovery and MFA setup | Strongly recommended; policy may require | Admin account recovery and verified security controls |
| 2a | **Protect your installation: MEK/key-protector setup and independent recovery** | **Mandatory before any third-party credential can be persisted** | Configure persistent mounted software root or approved KMS, independently recoverable escrow, verify restart and mark protector ACTIVE; optional TPM agent is a future feature |
| 3 | Email provider (SMTP or supported email API) | Available when a real mail/invitation feature ships (normally S1) | Test authorized connection and email delivery only when adapter exists |
| 4 | Primary document OCR: Mistral in Microsoft Foundry | **S4**, not S0 | Test actual OCR endpoint/model/region only with consuming worker, broker and budget controls |
| 5 | Azure Document Intelligence fallback | **S4**, optional specialist | Independent endpoint/version tested by specialist OCR feature |
| 6 | Text embedding provider | **S5** | Verify embedding dimensions, quotas, region and actual indexing capability |
| 7 | Answer-generation LLM (OpenAI/Azure OpenAI or compatible approved adapter) | **S6** | Verify generation and citation-safe structured output; not required for local login/upload |
| 8 | Vision/multimodal embedding provider | **S7** | Images and diagram retrieval when enabled |
| 9 | Enterprise login / OIDC provider | **S1**, optional when local auth allowed | Verified issuer/redirect/claims/client test with proper recovery |
| 10 | Review setup and “Go to dashboard” | Always | Honest readiness summary: Ready / Limited / Needs attention |

**Steps 3–9 are a future capability registry, not S0 implementation requirements.** Unavailable steps are non-interactive or hidden; users can reach the real dashboard with no AI provider configured. **Provider steps are dynamic and capability-gated.** An OSS deployment configured for local embeddings/LLMs should present those adapters instead of forcibly demanding Azure. OIDC setup is optional; local bootstrap must remain available as a safe recovery method before SSO-only enforcement can be enabled. A normal employee signing in must **not** see platform integration wizard steps; only explicitly authorized deployment/tenant administrators can view or change their scope's provider configuration.

No step may automatically upload customer documents to a cloud AI provider. Explain data residency/egress and require explicit policy approval before enabling provider-backed processing. **No integration configuration may persist new secret material while key protection is NOT_INITIALIZED, DEGRADED or RECOVERY_REQUIRED.** Settings and admin login remain available in limited mode to resolve the condition. Do not bundle third-party API keys into distribution images or logs.

### 5.2 Integration state machine and failure UX

| State | Visible UI | Allowed product behavior |
| --- | --- | --- |
| NOT_CONFIGURED | “Connect provider” with requirements | Provider-dependent functionality not advertised as available |
| DRAFT | Unsaved edit fields; secrets never echoed | Existing active configuration remains intact |
| TESTING | Pending indicator and cancel/retry | Never claim connected before backend confirmation |
| ACTIVE | Last checked, model/region/capabilities, edit/rotate | Feature may run with runtime health checks |
| DEGRADED | Warning with status and safe next steps | Controlled failure/retry; no silent fallback crossing egress rules |
| INVALID | Actionable validated error, no secret values | Feature blocked until fixed |
| SKIPPED | “Set up later” and clear disabled feature summary | Normal navigation remains possible |
| DISABLED | Explicit operator action + audit | Feature disabled, existing evidence preserved |

Test calls must be **real safe probes** against configured services (not “configuration syntax looks valid” masquerading as a successful connection), use server-side credentials and bounded cost/quota, and record only redacted diagnostics. Use Key Vault/vault integration or protected local secret storage; persist secret references, never show saved secret plaintext in UI/API. Rotation should test the new credential **before atomically activating it** and preserve a recoverable previous config until promotion if policy allows.

Wizard completion is persisted per deployment/tenant and administrator where appropriate. Show it prominently **once** at first admin login; once finished or deliberately postponed, ordinary logins open the dashboard. Use a dismissible “Setup incomplete” card and *contextual* setup suggestions on disabled features, not an unavoidable recurring modal. Settings > Integrations is always available to authorized users.

### 5.3 Dependency truth table

| Working capability | Needed integration |
| --- | --- |
| Create owner, login, tenant management, role administration | Local database and identity; **no external IdP, SMTP, TPM, Qdrant or cloud model required** |
| Save wizard secret / OAuth client secret / SMTP password | ACTIVE key protector with per-secret AES-GCM encryption, tenant-scoped wrapping and verified recovery policy |
| Use previously saved third-party integration | Available authorized key unwrap; otherwise explicit locked/unavailable failure |
| Upload and preserve originals | Operational object storage, database and ingestion workflow |
| Parse TXT/MD/native PDF without OCR | Local parser and storage; **no paid cloud OCR** |
| Scanned PDF extraction | Approved OCR provider (or explicitly implemented local alternative) |
| Semantic/vector search | Valid embedding profile and index backend |
| Grounded AI answer | Authorized evidence plus configured generator/verification capability |
| Email invites and notifications | Configured email/notification channel; otherwise explicit limited workflow |
| Enterprise SSO | Tested OIDC/SAML integration and safe local recovery policy |
| MCP/brief export/redaction | Only after the corresponding later slices and security tests ship |

A feature must never silently bypass missing configuration or fabricate a sample result.

## 6. Delivery sequence — outcome-based vertical slices

The table below is the **planned order**, adjusted based on dependencies and evidence. Each row represents a visible user-facing capability plus its backend, data, security, observability and acceptance tests.

| Slice | What a user can really do at completion | Main modules / dependencies | Architecture phase |
| --- | --- | --- | --- |
| **S0** Secure installation & UX shell | Securely create first Superadmin **once**, login/profile, software-root/KMS protector and recovery, real health and Setup Center shell (no unused AI adapters) | IdentityAccess, TenantManagement, DeploymentCapabilities, SecretManagement; minimal local infrastructure | I1 |
| **S1** Tenancy, invitations, scoped RBAC | Create/switch tenants safely across tabs, set classification/egress policies, invite members, enforce RLS/RBAC, configure real OIDC/email adapters | IdentityAccess, TenantManagement, access policy, Setup Center provider cards | I1 |
| **S2** Document registration and upload | Upload/quarantine and validate, see status/errors, store immutable source; .NET-owned fenced outbox/worker skeleton and honest malware-scan policy | DocumentManagement, storage, outbox, .NET stage authority | I1 |
| **S3** Native document evidence | Parse TXT/Markdown/native PDF into attempt-scoped CDM artifacts; .NET imports; show functional minimal line/span/PDF overlay evidence viewer | Python parsers, .NET EvidenceCatalog persistence, offset tests | I2 |
| **S4** Cloud OCR and robust ingestion | Real Foundry Mistral/DI setup and worker credential broker, per-tenant usage/budgets, checkpoints and PARTIAL diagnostics with reviewer triage | Ingestion, provider adapters, .NET credential/lease/cost services | I2 |
| **S5** Evidence indexing and initial reviewer gate | Build authorized lexical/embedding indexes and fenced READY; ship minimal approved correction overlay/review queue for partial OCR before S6 | EvidenceCatalog, Search/Indexing, HumanReview | I2/I3 |
| **S6** Search and grounded answers | Search permitted documents and ask questions; receive **verified** answer blocks with stable clickable citations, explicit abstention | Search, QueryEngine, model gateway | I3 |
| **S7** Visual and HTML content | Search/view original images and diagrams; parse and safely render HTML; correct visual source locators | Ingestion, visual search, React evidence viewer | I4 |
| **S8** Advanced human review and correction | Extend the **already working S4/S5 minimum review** with advanced workflow/dual control, typed scale/header corrections, reviewer analytics | HumanReview, EvidenceCatalog, Ingestion | I5 after initial I2 minimum |
| **S9** Verified tables and analytics | Calculate with unit/scale/header-aware verified cells, excluded input explanations and source trace | TableAnalytics, HumanReview | I5 |
| **S10** Version comparison and governance | Compare versions, show added/removed/changed source elements; saved-answer freshness and reviewed conflicts | EvidenceGovernance, QueryEngine | I5 |
| **S11** Publish evidence briefs | Pin verified blocks, publish immutable briefs and export signed/offline-verifiable manifests | EvidencePublishing, audit, crypto | I5 |
| **S12** Policy-safe sensitive evidence | Authorize protected source classifications and serve validated sanitized views/indexes/exports | SensitivityPolicy, tenant auth | Design in I1; release with I4/I5 hardening |
| **S13** Enterprise agents/MCP | Configure least-privilege delegated/service client; run read-only verified MCP tools with complete audit | IntegrationsMcp, IdentityAccess | I6 |
| **S14** SaaS operations & enterprise distribution | Tenant lifecycle, metering/quotas, billing integrations, backup/restore, private deployment packaging and migration | SaaS control plane; distribution | Foundation I1, production I6 |
| **S15** Knowledge health and governance UX | View privacy-safe abstention clusters, review backlog, stale evidence alerts and actionable quality trends | KnowledgeQuality, EvidenceGovernance | I6 |

**Mandatory sequencing:** basic reviewer triage begins in S4 and a publishable approved correction path completes in S5 **before S6**; full review enhancements remain S8. Classification/egress gates ship in S1/S2, while span/polygon PII detection and sanitized derivatives remain S12. Cloud usage/budget controls ship in S4 rather than waiting for S14 billing. Do not defer a required security invariant because its UI slice has a later number.

### Slice S0 detailed acceptance

Given a fresh self-hosted DB, an operator installs the stack and obtains the protected bootstrap proof. The app shows the real one-time setup page; valid username and password plus proof atomically create the deployment owner and first tenant. A second attempt from another browser cannot create another owner. On initial sign-in the owner sees the real app, chooses a recovery-capable software/KMS protector, verifies uninterrupted restart/recovery, and opens a **capability-aware Setup Center**. Only genuinely implemented providers can accept credentials or run tests; unused AI adapters are not prerequisites. Leaving setup incomplete never breaks login, settings or logout. A restart or restore never exposes the bootstrap page again for that initialized DB. All flows pass responsive, keyboard, authorization, trace, browser and recovery tests.

### Slice S1/S2 user experience example

After S1 the user sees a real tenant and workspace overview, not a mock dashboard. An owner can invite permitted users, manage memberships, and demonstrate that a Reader cannot access security settings or another tenant. After S2 the **Upload document** CTA accepts a supported file and genuinely stores the original, shows upload progress and a durable job state; it does not claim OCR/indexing is complete before those slices exist.

## 7. Feature slicing template — required for every task/PR

Every planned slice and pull request contains this short feature contract:

~~~text
Slice ID / bounded module:
User outcome and accessible UI route:
Roles, tenant scope and policy requirements:
Preconditions and required integrations:
Happy path (API, UI, domain, persistence, event/worker):
Empty/loading/validation/error/unauthorized/session-expired states:
Data changes, migrations and backwards compatibility:
External contracts, retries, idempotency and fencing:
Source/content sensitivity and data-egress policy:
Telemetry, audit, provider cost and diagnostic IDs:
Automated unit + integration + e2e + security tests:
Manual keyboard/mobile UX check and screen evidence:
Recovery/rollback plan and feature flag, if needed:
Explicit out-of-scope and acceptance criteria:
~~~

A UI-only slice without functional backend is **not** complete. A backend-only feature exposed to end users without the correct accessible UI and state design is also **not** complete. Some cross-cutting internal infrastructure tasks may stand alone only when tied to the next user-visible slice and covered by automated tests.

## 8. Mandatory quality gates and test strategy

| Gate | Minimum requirement on every slice |
| --- | --- |
| Correctness | Successful and adverse paths tested; no success status before durable commit and authorized read-back |
| Authentication | Secure session, denial of stale/revoked logins, no default credentials, scoped bootstrap and step-up for privileged changes |
| Multi-tenancy | At least two tenants in integration tests; deny cross-tenant requests, joins, asset URLs, caches, search and jobs |
| RBAC | Deny-by-default permission catalog; scope inherited properly; SA/operator cannot automatically access private content |
| UX | Responsive, keyboard-navigable, accessible labels/errors, honest empty/error/loading states and no dead-end controls |
| Data protection | AES-GCM secret versions, ACTIVE protector and tested unattended software-root/KMS recovery before secret saves; no plaintext in API/log/UI; tenant data-egress budgets enforced before cloud calls |
| Resilience | Idempotency, retry, timeouts, no stale fencing commits, object/queue failure recovery as relevant |
| Observability | Structured correlation IDs, health, trace, audit events and diagnostic identifiers with no sensitive data leakage |
| Performance | Publish measured p50/p95 and real device/browser constraints; avoid unverified speed guarantees |
| Packaging | Docker/Compose startup (or targeted SaaS deploy), migrations, config validation and rollback documented |
| API contracts | Versioned OpenAPI/events/CDM and frontend generated types where appropriate |
| Automated validation | Unit tests for business invariants; real PostgreSQL/integration tests for persistence/security; Playwright e2e for meaningful user flows; accessibility/security smoke checks |
| Documentation | Configuration, setup, role permissions, required services, known limitations and upgrade notes match shipped behavior |

**No “only smoke tests” exception for security and data correctness.** Smoke tests protect the happy path, but bootstrap concurrency, tenant isolation, session revocation, OCR provenance, verified citations, signed manifests and redaction require focused negative/integration tests.

### 8.1 Required S0 adversarial test matrix

1. Fresh deployment, missing bootstrap proof, expired proof, replayed proof, wrong proof and brute-force attempts.
2. Two simultaneous owner-creation requests: exactly one succeeds; no partial user/tenant.
3. First-run succeeds, then logout/login/new browser/API POST/restart/upgrade/restore: setup never reappears and create-owner API rejects.
4. Database unreachable or migration fails: maintenance message, no accidental UNINITIALIZED default and no partial bootstrap.
5. Owner password reset via authorized CLI recovery: SEALED remains true; sessions/previous credentials revoked; no new free-for-all admin enrollment.
6. On-prem remote access without TLS, insecure proxy headers, forged CSRF/origin and leaked container-log attempts are rejected.
7. SaaS mode: public requests cannot access deployment bootstrap, only controlled tenant signup/invite.
8. Wrong tenant membership, role escalation, spoofed tenantId, tenant switch across browser tabs, absent MFA when required and stolen/replayed cookie handling.
9. The configured S0 protector/any actually shipped provider fails safely with bad credentials, timeout, region mismatch, missing secret and rotation; cloud OCR/LLM provider-specific 429, budget and egress tests apply at S4/S6.
10. Settings refresh, browser back, abandoned wizard and ordinary non-admin login: no sensitive integration secrets or repetitive blocking onboarding.
11. S0 secret file missing/replaced, KMS unavailable, wrong recovery envelope, corrupt wrapped DEK, swapped tenant/secret ID and bad AEAD tag fail closed with no plaintext/cross-tenant leak. TPM cleared/PCR/host IPC tests are required only when the optional TPM adapter ships in S14.
12. Rotate a wrapping key with concurrent wizard save or process crash: rewrap journal allows resume; active ciphertext remains readable by the authorized key version; never retire the old key prematurely.
13. Hardware replacement rehearsal: restore database, protected recovery envelope and permitted file storage on a replacement host; rewrap under new protector, recover integration test without revealing existing secrets in UI; bootstrap stays SEALED.
14. SaaS Vault/KMS outage and multi-node access, tenant mismatched key reference, credential rotation and leakage through logs/traces/backups are tested in their corresponding scope; host TPM device permissions are validated when the TPM agent ships.

### 8.2 User experience review evidence

For each PR: link the feature's acceptance scenarios, passing Playwright tests, screenshots or recordings of desktop and mobile narrow width **from the real app**, accessibility results, unauthorized/error-state evidence, and any known trade-offs. No claiming “pixel perfect” or WCAG compliance solely because automated tools ran.

## 9. Slicing strategy for third-party integrations

Use a common **IIntegrationProvider** or domain-specific adapter contract for capabilities, connection test, configuration validation, activation/deactivation, secret reference and diagnostic status. The domain-specific contracts from architecture.md remain the authority for OCR/embedding/LLM; do not collapse their incompatible provider APIs into a single pretend-universal model.

For each provider build a complete vertical slice **on top of an ACTIVE SecretManagement protector** (§13):

1. Authorized provider tile and purpose-specific integration form.
2. API validation, tenant/deployment scoping and egress/cost disclosure.
3. Server-side secret storage, permitted region and model/deployment fields.
4. Real test connection/capability probe (bounded and safe), redacted errors.
5. Atomic activation and audit, post-activation health and rollback.
6. UX for skip, invalid, degraded, disabled and renewal/rotation.
7. Unit, contract, integration, e2e and denied-access tests.

Start with common built-in local authentication and the S0 provider registry. Add generic enterprise OIDC at S1 and Foundry Mistral at S4 as first concrete external integrations for their respective slices; Azure Document Intelligence follows as independent fallback. Email and nonessential providers are optional adapters. Do not conflate “I saved the URL” with “the service works”.

**One-by-one UX does not mean serial app availability.** Integration cards can be configured in any permitted order by an authorized admin; the wizard recommends dependencies, and every feature is capability-gated.

## 10. Deployment readiness and release process

- **OSS self-hosted:** Tagged OCI builds, Compose installation path, install/setup docs, no platform-SaaS handshake, tested local first-run/bootstrap, recoverable secrets protector, repeatable migrations, key metadata/secret-envelope and storage backup/restore and upgrade rollback. License still awaits owner decision in architecture.md §21.2.
- **Enterprise on-prem:** Reverse proxy/TLS, offline capability boundaries, optional private provider endpoints, SSO if configured, operator recovery, explicit outgoing calls and secrets lifecycle.
- **SaaS:** Privately initialized operator plane, invitation/tenant creation rather than web platform bootstrap, multi-tenant RLS and policy enforcement, rate/usage budgets and tenant-aware telemetry. Dedicated placement remains a later validated option.
- **Release channels:** dev/preview/stable, forward-compatible event/CDM schemas, migration verification and controlled rollback. Feature flags cannot override authorization.
- **Definition of release:** All applicable slice gates pass; installer UX and permissions match the docs; unsupported future functionality is absent or honestly explained. Never label a milestone complete solely because API routes compile or a UI screenshot looks polished.

## 11. Decisions to record before implementation

| Topic | Decision / pending clarification |
| --- | --- |
| Implementation style | **Accepted:** DDD-bounded modular monolith with vertical feature slices; Python workers independent |
| Slice 0 UX | **Accepted:** polished product shell, secure first-run bootstrap/login, first-owner flow and guided Setup Center |
| Initial role | **Accepted:** self-hosted initial user is deployment Superadmin **and** initial tenant owner, with separate scopes |
| Bootstrap exposure | **Accepted:** single-use operator proof + authoritative SEALED gate; no public anonymous SaaS Superadmin registration |
| MEK hierarchy | **Accepted:** per-secret DEKs, AES-256-GCM, purpose/tenant-bound authenticated context, versioned KEK and key-protector contract; separate from login cookie key ring |
| TPM policy | **Accepted:** optional on-prem security improvement **deferred to S14 host-agent capability**; not a mandatory S0 default. A signing-only TPM ECC key is not an encryption key |
| Self-hosted recovery | **Accepted:** unattended S0 Docker boot from persistent host-restricted root secret mount (or explicitly chosen manual unlock), with independent encrypted recovery envelope; no root secret alongside DB backup |
| SaaS/multi-node protector | **Accepted:** Azure Key Vault / approved KMS with workload identities; no reliance on single physical TPM |
| Host TPM Docker access | **Accepted:** optional restricted host key agent/IPC; do not give entire container unrestricted TPM device access |
| Integrations | **Accepted:** S0 Setup Center UI/framework and any real S0 consumer only; provider forms and true probes ship with S1/S4/S5/S6/S7 consuming slices |
| Normal login | **Accepted:** no repeatedly forced wizard; only authorized admins see contextual incomplete-setup warnings |
| OAuth/SSO | **Accepted:** OIDC with secure BFF sessions; local identity first; customer tenant SSO configured later per policy |
| Open-source license | **Open:** Apache 2.0 vs AGPLv3 vs well-governed dual licensing (architecture §21.2) |
| MFA enforcement at first bootstrap | **Policy choice:** encourage immediately; operator may require before sensitive provider/data operations |
| Local recovery mechanism | **Implementation choice:** privileged, audited CLI path; must not reopen web setup |
| UI style/theme | **Design decision:** establish tokens and component kit in S0; visual direction can evolve without reworking behavior |
| SaaS plan/pricing and advanced IdPs | **Deferred business/integration choices:** contracts exist but do not block secure local bootstrap |

## 12. Immediate engineering sequence

Start by implementing **S0.1 → S0.2 → S0.3 → S0.4 → S0.5 → S0.6**, merging only cohesive, testable increments. The first publicly demonstrated workflow should be:

**Install → protected single-use Superadmin creation → login → initialize persistent software-root or configured KMS + independent recovery → actual Setup Center with available capabilities only → real dashboard and Settings → logout/relogin/restart without renewed bootstrap or MEK loss.**

Then implement **S1 tenant/roles** and **S2 immutable upload** before a complex OCR pipeline. Every later slice preserves the same production-ready UX and security discipline.

**Do not trade away first-run security to improve apparent onboarding convenience.** A remotely claimable initial Superadmin account or persistent setup route would be a serious security flaw in a distributed open-source and hosted product.

## 13. Slice 0 Master Encryption Key (MEK) / TPM implementation strategy

This section is a **delivery contract** for the full lifecycle specified in [architecture.md §32](architecture.md). It is part of **S0.4** and underpins all credential-collecting integrations in S0.5. Key-protection UX must meet the same visual quality, accessibility, testing and security gates as the login and setup screens.

### 13.1 Vertical slice boundaries and dependencies

| Slice | User-visible outcome | Server capability | Gate |
| --- | --- | --- | --- |
| S0.4a — Select protector | First owner sees supported TPM / Vault / secure software choices and limitations | \`IKeyProtectionProvider\` capabilities, OS/TPM detection, verified deployment mode | Never claim TPM available until actual provider capability check |
| S0.4b — Initialize envelope | Owner starts protected key management without copying plaintext keys | Key version/reference, secure random wrapping material, tenant-aware policies, staged durable records | All secret data is authenticated and stored encrypted; retry is idempotent |
| S0.4c — Recovery kit | Owner can securely export recovery envelope to independent storage and learn restore steps | Separate encrypted recovery envelope, safe key derivation, key metadata, offline verifier | No plaintext MEK or recovery passphrase in logs, database dump or support bundle |
| S0.4d — Verify & activate | Real test succeeds; Setup Center shows “Protected / Ready” | Wrap/unwrap + simulated recovery check, ACTIVE state, audited activation | First integration-secret POST still denied until ACTIVE |
| S0.4e — Key management settings | Owner can inspect non-secret protector status and initiate rotation or recovery | Least privilege, key versions, journaled rewrap and recovery CLI | Rotation cannot make active integration secret unrecoverable |
| S0.5 — Provider integration | Email/OCR/LLM/OIDC credentials saved and tested one by one | Per-secret fresh DEK, AES-GCM with tenant/purpose AAD, wrapped DEK metadata, server-only plaintext | No plaintext credential/key returned to browser or exposed in logs |

**Authority:** Deployment operator can set the root-protection policy; tenant-admin authority is constrained to permitted tenant provider secrets. SaaS operator role does not by itself allow decrypting tenant secrets. Local login, restoring account access and reading installation status never depend on SMTP/Foundry/TPM presence.

### 13.2 First-login "Protect your installation" screen

Recommended UX:

1. **Detect:** “We found TPM 2.0 on this machine”, “External vault configured” or “No hardware-backed protector detected”. Detection is advisory until a real capability test passes. Show known hardware/VM migration limitations.
2. **Choose an implemented protector:** **Unattended software-root** (self-hosted), **external Vault/KMS** (hosted) or **manual unlock** (opt-in). TPM2 host protection is shown as future optional on-prem capability until its real S14 adapter is supported. **Never** include an “unencrypted” choice.
3. **Generate:** For unattended self-hosted use, run the privileged operator bootstrap CLI to generate a high-entropy root in a persistent restricted host secret mount **outside DB/container images/backups**; UI provisions wrapped tenant keys from that material. Alternatively select intentional manual unlock or approved KMS. Show only nonsecret key version and state.
4. **Protect recovery:** Offer a one-time **encrypted recovery kit download** or supported enterprise key escrow. Require independent high-entropy recovery secret/organizational secret-management control, a clear warning against saving it beside database backups, and acknowledgment of recovery limitations.
5. **Verify:** Run a safe cryptographic self-test (including an independently protected recovery envelope for recoverable installations). A first-run self-test is **not** a substitute for a real replacement-hardware DR drill, which belongs in the release gate.
6. **Activate:** Persist ACTIVE state and move to the normal step-by-step third-party provider wizard. Failed/abandoned setup remains RECOVERY_PENDING or LOCKED, never a plaintext fallback.
7. **Revisit:** Settings > Security > Encryption Keys displays non-secret status, protector type, key ID/version, recovery readiness, audit trail, “Rotate wrapping key” and authorized recovery instructions. Do not print encryption keys or recovery passphrases.

The screen supports keyboard navigation, narrow widths, missing TPM, no outbound vault access, invalid recovery envelope, elapsed activation and readable remediation. Key-related failure never redirects to “Create first Superadmin”; bootstrap remains SEALED.

### 13.3 Protector implementation and Docker placement

~~~text
S0 React Setup Center
   |
   v
ASP.NET Core Secret Management API (tenant + policy + session checks)
   |
   +-- ISecretEncryptionService: per-secret AES-256-GCM DEK
   |       |
   |       +-- immutable ciphertext record + wrapped DEK + key metadata
   |
   +-- IKeyProtectionProvider
           +-- Azure Key Vault/KMS (SaaS / shared multi-node)
           +-- External Vault/KMS (on-prem multi-node)
           +-- TPM Host Agent (optional S14 on-prem add-on; not shipped in S0)
           +-- Recovery-enabled software protector (S0, persistent host-restricted mounted root)
~~~

TPM keys require **appropriate seal/wrap operations**; do not attempt to encrypt with a TPM **ECDSA signing-only** key. A host-side key agent must authenticate the allowed calling workload and restrict key IDs, operations and namespaces. Exposing a general-purpose \`/dev/tpm0\` interface to the application container or logging unseal output is not an acceptable default.

A SaaS deployment should normally use managed identity + shared-access KMS and may use separate tenant KEK policies; tenant isolation includes vault authorization and secret-level purpose checks. A single hardware TPM must not become the SaaS-wide availability dependency.

### 13.4 Faults, recovery and activation truthfulness

| Condition | User experience | Enforcement |
| --- | --- | --- |
| TPM unavailable | “Hardware protection not available”; offer explicit safe alternative | No auto-fallback to plaintext or silent change of protector |
| TPM cleared / host replaced | “Encrypted credentials locked; recovery required” | Disable secret-dependent jobs; keep local admin login |
| Vault/KMS outage | “Temporarily unavailable, retry later” | Fail closed, rate-limit retries, no insecure cached persistent secret |
| No recovery envelope for recoverable on-prem profile | “Recovery protection incomplete” | Do not claim installation protected/recoverable; block S0 secret saves under this policy |
| Recovery envelope invalid | Clear safe error without key details | Do not change key lineage or attempt a new Superadmin bootstrap |
| KEK rotation incomplete | Show journaled progress / retry | Old working envelope remains until all records verified |
| Provider credential revoked at Azure/SMTP/IdP | “Connection invalid; update provider credential” | Separate provider secret rotation from MEK/DEK rewrap |
| Tenant mismatch / forged wrapped-key metadata | Generic forbidden/integrity error | Unwrap authorization checks + authenticated encryption context |

### 13.5 Mandatory automated and manual tests

- Concurrent bootstrap + protector initialization: exactly one deployment owner, sealed setup; no duplicate active root keys or orphan plaintext files.
- Ciphertext/tag/AAD tampering (tenant, secret ID, provider purpose, key version) fails closed; high-volume encrypted stores never reuse DEK/nonce pairs.
- **S0:** software-root mount present/missing/replaced, unattended restart, recovery/escrow, manual-unlock mode and KMS failures handled without disclosure. **S14 optional TPM:** Windows/Linux, PCR, cleared TPM and host IPC caller authentication tested before enabling TPM.
- Rotation fault halfway through rewrap, concurrent provider secret update and old key retirement rejected while active envelopes still refer to it.
- Backup and **restore onto replacement hardware** using encrypted recovery kit; audit access and ensure app starts with bootstrap still SEALED; no provider secret is displayed in UI.
- Vault/KMS outage, rate limits, managed identity unavailable, tenant-scoped key isolation and multi-node request with a key version change.
- Browser dev tools, API payloads, telemetry/trace/logs, support bundles, database snapshots and object storage contain no stored plaintext secrets or raw MEKs.
- For each **shipped** provider, a real connection test reports sanitized status and distinguishes wrong API credentials from key-protector failures. No unimplemented provider can claim ACTIVE in S0.
- Manual UX/a11y review of each protector choice, recovery step, error state, locked state, long-running rotation and mobile layouts.

**Exit criterion:** S0 cannot be production-ready without a functioning encrypted wizard secret store and a demonstrated recoverability or explicitly documented approved external escrow policy. Optional TPM support should be released only when its platform-specific integration/security tests pass. Key loss is reported truthfully; software cannot recover a truly irrecoverable key.

**Open-source assurance:** Local users can install, initialize, restore and rotate supported key protectors without vendor SaaS access. No secret key, recovery material or third-party credentials are bundled in repo/default container images.

## 14. v1.2 independent-review implementation disposition

Architecture v1.6 is **unfrozen and active**; this section turns its clarified trust boundaries into sequenced vertical engineering tasks, without requiring a fresh architecture redesign. The frozen historical v1.5 commit remains available in Git.

### 14.1 Accepted delivery choices

| Boundary | Implementation rule | First needed |
| --- | --- | --- |
| Key store at restart | Unattended software-root uses a persistent host-restricted secret mount created by operator CLI, outside DB/backups; manual unlock/KMS alternatives are explicit | S0.4 |
| Optional TPM | Leave adapter capability behind interface; do not build CNG/TSS daemon/IPC for S0 | S14, only if demanded |
| Setup Center | Real registry/stepper and encrypted secret plumbing in S0; register concrete providers with functional features, no fake Test connection | S0.5, then S1/S4/S5/S6/S7 |
| Tenant context | Tenant selected per route/tab and reauthorized server-side; RLS set transaction-locally with non-bypass runtime role | S0.1 foundation, exercised S1 |
| Python worker credentials | .NET broker validates workload identity, active stage epoch, tenant, provider and budget; or higher-isolation model gateway | Contract S2, functional S4 |
| CDM persistence | Python produces immutable verified artifacts; .NET owns page state, stage lease, bulk CDM staging and READY cutover | S2–S3 |
| OCR review | S4 triage/Inspector; S5 approved correction overlay/READY; advanced S8 later | S4–S5 |
| Cloud OCR budgets | Per-tenant usage ledger, spending budget/circuit breaker and egress check **before** first external OCR call | S4 |
| Verified answers | Deterministic synchronous checks; batched entailment per atomic group, measured TTFVB without provisional factual release | S6 |
| Sanitized evidence | Classification and egress early, scoped/limited derivative profiles and privileged retrieval where authorized | S1/S2; advanced S12 |

### 14.2 Worker interface contract — no split-brain database authority

~~~text
.NET assigns job / tenant / stage / lease epoch
    → small claim-check message (no plaintext keys)
    → Python untrusted parsing work (sandboxed)
    → trusted provider worker (if needed) requests scoped credential from .NET
    → Python writes immutable attempt-scoped CDM/page artifact and hash
    → Python reports completion/checkpoint to authenticated .NET endpoint
    → .NET validates job/epoch/tenant/hash and stages bounded DB batches
    → .NET atomically publishes one fenced validated generation
~~~

The provider broker is internal only; it checks workload identity, job/stage/attempt, active lease, provider purpose, classification/egress policy, quota and credential version. A short *authorization* lease does not convert a long-lived provider API key into a revocable short-lived key; the trusted worker remains a privileged process. Prefer actual managed identities/ephemeral provider tokens where supported.

Python cannot write authoritative \`document_elements\`, \`table_cells\`, \`stage_leases\`, checkpoint or version promotion tables directly. Worker callbacks and broker messages are idempotent and size-bounded. .NET uses bounded import batches and one final short fenced cutover. Renew message locks and DB leases separately; checkpoint/yield/reschedule extended provider backoff.

### 14.3 Stage gates for new release

**S0 must prove:** fully working responsive bootstrap, password/sessions, one-time SEALED state, local dashboard/settings, encrypted root persistence surviving restart, an *independent* recovery test, live available Setup Center states and no dependency on unbuilt AI integrations or TPM agents. Confirm wrong root leaves secrets LOCKED without resetting owner.

**S1 must prove:** pooled Postgres connections cannot bleed tenant RLS variables; \`SET LOCAL\` and \`NOBYPASSRLS\` behavior tested on actual PostgreSQL with two tenants, simultaneous browser tabs, cancellation, reconnect and background jobs. External identity is authenticated without hand-made bearer token cryptography.

**S2–S3 must prove:** quarantined and bounded uploads; approved scanner policy; immutable originals; only .NET commits fenced CDM/processing metadata; real source viewer handles emoji, non-BMP Unicode, CRLF/BOM, source byte/code-point/JS UTF-16 conversion.

**S4–S5 must prove:** trusted provider credential broker cannot be used with wrong/stale job; cloud calls produce tenant-level costs/budget records and egress checks; PARTIAL OCR has real inspector/approved overlay path; indexed READY is correct.

**S6 must prove:** a user sees no unverified factual answer block, while latency, costs and claim support are benchmarked on multilingual evidence.

**S7/S10/S12 later:** container-isolated browser rendering; bounded version alignment/contradiction checks; classification-safe derived indexing and content. These must not be implemented as empty S0 scaffolding.

### 14.4 Open choices and known trade-offs

- First cloud OCR adapter's identity: prefer provider-managed workload identity where available; use .NET broker credential access otherwise, or validated full model gateway for high-isolation deployments.
- Software-root mount is recoverable only if the operator secures both the normal root source and the independently encrypted recovery kit; file mode \`0600\` alone is insufficient.
- A policy cap on derivative variants is an optimization, not a reason to forbid legitimate privileged search or confuse post-OCR PII classification with pre-OCR egress authorization.
- The exact lease expiration semantics remain the existing fail-closed policy (\`expires_at > now()\` for completion) unless changed through a tested ADR.
- Do not add extra independent DbContext/project boundaries solely because a feature folder exists. Preserve business modules and typed contracts, but introduce tables only when the corresponding feature requires them.

**No implementation is claimed by this document.** Accepted decisions must be realized through reviewed PRs and security/integration tests, not accepted merely because they appear in a plan.
