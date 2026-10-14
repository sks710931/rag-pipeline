# Enterprise Multimodal RAG Platform — Architecture

> **Status:** **FROZEN ARCHITECTURE BASELINE — v1.5**, approved as the implementation reference (design specification; no claim of completed code or production certification)  
> **Version:** 1.5 — adds pluggable Master Encryption Key (MEK) lifecycle and optional TPM-backed on-premises key protection; all prior governance, SaaS, identity and RAG decisions remain in force  
> **Date:** 2026-10-15  
> **Architecture freeze date:** 2026-10-15  
> **Revision basis:** v1.4 plus encrypted Integration Setup Center wizard secrets, an explicit DEK/KEK/MEK lifecycle, optional Windows/Linux TPM2 key protection, multi-node/SaaS KMS, tested recovery and rotation. Prior requirements and the open-source license decision remain unchanged.  
> **Repository:** sks710931/rag-pipeline  
> **Target:** Open-source core; self-hosted/on-premises and managed multi-tenant SaaS distributions; approximately 10,000 initial documents per sizing scenario, multilingual, cloud AI by default (customer-configurable provider policies).

## Architecture freeze and change-control policy (v1.5)

**Baseline decision:** Version **1.5 is frozen for implementation.** Sections 1–33, their technical invariants, selected provider boundaries, distribution model, security requirements, and accepted/deferred ADR decisions collectively form the approved design baseline. This freeze is **not** a source-code freeze, a claim that the design is already implemented, or a guarantee that production readiness tests have passed.

- **Build against the baseline:** Use this document and [Slicing strategy.md](Slicing%20strategy.md) when implementing S0 onward. Implementation details and delivery sequencing may evolve in supporting documents and code provided they preserve v1.5 invariants.
- **No unreviewed scope growth:** New architecture features, changed security/trust boundaries, replacement technologies, or weakened correctness/authorization requirements must be proposed through a versioned ADR/change request with impact, alternatives, acceptance tests, migration implications and rollback. An approved substantive change creates a new architecture revision (starting with **v1.6**), never a silent rewrite of v1.5.
- **Fixing design defects:** Security/correctness issues uncovered in implementation are not ignored because of the freeze. Record the problem, severity and decision in an issue/ADR; implement urgent safe remediation as needed, and update the next architecture revision. Only non-semantic errata (typos, formatting and broken links) may be corrected without a new architecture version, with clear commit history.
- **Protect the historical reference:** The Git commit representing this freeze is the canonical **immutable v1.5 reference**. Future edits to `main` do not alter that historical commit. The branch remains open for implementation and supporting docs; no Git repository branch-protection or release-tag guarantee is implied by this textual freeze.
- **Outstanding decisions remain outstanding:** Open-source license/commercialization, implementation-time provider availability, formal threat-model details, deployment-specific SLO/RPO/RTO, release operations and tested disaster recovery still require resolution in their respective workflows. They are not implicitly approved by freezing this design.
- **Acceptance remains empirical:** Architecture quality and production readiness must be demonstrated by the smoke, integration, adversarial security, source-provenance, tenant isolation, MEK recovery, accessibility and release gates defined here and in the slicing strategy.

## 1. Purpose and architectural principles

Build an **open-source, distributable enterprise-grade multimodal Retrieval-Augmented Generation (RAG) platform** using one shared application core for self-hosted/on-premises installations and managed multi-tenant SaaS. It ingests PDF, HTML, images, Markdown, and plain-text files; preserves source information; and delivers grounded answers with clickable citations, original images, visual search, document comparison and exact calculations. Deployment packaging and commercial operations may vary, but content isolation, permission rules and RAG correctness must not.

**Central architectural decision:** This is an **evidence-centric document intelligence platform**, not a pipeline that extracts text, divides it into fixed chunks, and stores only embeddings.

The following invariants govern all designs and implementations:

1. **Originals are immutable.** Preserve the original uploaded bytes and every published source version.
2. **The canonical document model (CDM) is authoritative.** Text, tables, figures, equations, links, and their relationships are independently addressable objects. A vector index is a derived projection, never the source of truth.
3. **Provenance is mandatory.** Every retrieval unit resolves to one or more original document elements and precise source locations.
4. **Citations are verified server-side.** A language model can select supplied evidence IDs but cannot invent a source URL, asset ID, page number, or evidence reference.
5. **Source content is untrusted.** No document, HTML fragment, Markdown code block, or OCR result may override system instructions, permissions, or execution policy.
6. **Access is checked before exposure.** Enforce tenant/collection/document permissions before candidates reach an LLM, and revalidate before evidence, assets, or answers are returned.
7. **Calculations are deterministic.** Financial and tabular calculations use verified typed data and controlled execution, not LLM arithmetic.
8. **Failures remain visible.** Missing assets, uncertain parsing, unresolved citations, and unsupported formats must be marked explicitly, never silently omitted.
9. **Reprocessing is reproducible.** Extraction, OCR, embeddings, and indexes are versioned with input hashes, software versions, configuration, and processing manifests. Stage outputs live in immutable claim-check objects, not broker messages.
10. **Providers are replaceable.** Parser, OCR, embedding, reranking, generative model, queue, object store, and index have adapters and versioned contracts.
11. **One product, multiple distributions.** Self-hosted and SaaS deployments share versioned domain contracts, data model, APIs and authorization policies; self-hosted operation does not require the vendor's SaaS control plane.
12. **Identity is not authorization.** External IdPs prove identity; tenant membership, resource-scoped permissions and content classification govern access.
13. **Isolation follows the content.** Every original, derivative, index entry, audit record, conversation, job, cache and export belongs to a tenant and an enforced authorization scope.
14. **Commerce is not permission.** Billing/feature entitlements and quotas never grant document access; subscription state is evaluated separately from RBAC and document ACLs.
15. **Evidence freshness is distinct from citation validity.** Immutable historical citations can remain valid while the associated *answer* becomes stale; supersession, conflicting authority and effective dates require separate verified governance states.
16. **Human corrections never rewrite originals.** Reviewer decisions create append-only, versioned derived overlays and controlled re-publication with source lineage and separation of duties.
17. **Classification precedes disclosure.** Restricted evidence is never disclosed as raw text, original asset, snippet, embedding context, exported file or agent-tool result to an unauthorized principal; visual black rectangles are not redaction.
18. **Portable bundles prove integrity, not factual truth.** Signed manifests preserve hashes and lineage, while claim support and calculations require their own verification and documented assumptions.
19. **External agents have no exceptional authority.** MCP/tools run through the same authenticated tenant, permissions, sanitization, source-verification and usage gates as browser and API callers.
20. **Quality analytics do not become surveillance.** Abstention and retrieval diagnostics are privacy-controlled tenant summaries, not a way to reveal another user's questions or inaccessible documents.
21. **Secrets are encrypted independently of credentials/session data.** Provider wizard secrets use authenticated envelope encryption with tenant-associated context, key versioning, narrow decrypt authority and independently tested recovery. A TPM is optional, not a requirement or sole recovery path.

## 2. Scope and inputs

### 2.1 Required source types

| Source | Extensions / content types | Mandatory extraction and preservation |
| --- | --- | --- |
| PDF | .pdf / application/pdf | Native text, scans and OCR, headings, layout/reading order, tables, figures, charts, equations, form fields, annotations, embedded images, page geometry |
| HTML | .html, .htm / text/html | Raw HTML, normalized semantic DOM, rendered snapshot when needed, tables, images, SVG, links, lists, code, metadata, anchors |
| Images | .png, .jpg, .jpeg, .webp, .tif, .tiff, .bmp, supported image MIME | Original pixels, metadata, OCR text and regions, image descriptions, diagrams/charts, visual embeddings |
| Markdown | .md, .markdown / text/markdown and relevant text MIME | Original Markdown, front matter, heading tree, paragraphs, lists, GFM tables, fenced/inline code, links, footnotes, images, blockquotes, tasks, raw HTML, exact line/span provenance |
| Plain text | .txt, .text / text/plain | Original bytes and line endings, encoding, lines/paragraphs, optional detected headings and sections, exact line/span provenance, multilingual text |

Accept file type only after extension/MIME inspection and safe content sniffing; extension alone does not establish trust. Version the support policy by input type and parser. Files that cannot be parsed are retained as quarantined/unsupported with diagnostics.

### 2.2 Required user-facing capabilities

- Semantic, lexical, hybrid, and multimodal search with language-aware retrieval.
- Grounded question answering, original source images in answers, source citation navigation, and evidence highlighting.
- Document explorer with pages, outlines, element tree, original files, image zoom, version history, and extraction diagnostics.
- Cross-document and cross-version comparisons with citations on both sides.
- Structured table exploration, verified aggregations, units/currency-aware calculations, and cell-level provenance.
- Multi-user access control, tenant/workspace/collection/document ACLs, tenant-defined RBAC, audit logs, built-in sign-in and enterprise federation, service-account identities, and automated deprovisioning.
- Multilingual documents and multilingual query/answer support.
- Observability, replay/reindex, cost monitoring, quality gates, and failure recovery.

### 2.2.1 Product distributions and enterprise SaaS scope (v1.3)

The same versioned product must support community self-hosting (Docker/VM), customer-managed private/on-premises deployment and managed multi-tenant SaaS. SaaS requires subscription-aware onboarding, tenant lifecycle, isolated ingestion/retrieval, metering, quotas and operational controls. Enterprise installation must support deployment-local administration, private directories, offline license/status independence and optional bring-your-own inference endpoints. Fully air-gapped document intelligence is a separately evaluated capability because the baseline Mistral/Azure OCR and cloud LLM adapters require network access. See §§21–25.

### 2.3 Explicit non-goals for the initial release

- Guaranteed lossless semantic understanding of arbitrary scans or engineering schematics. Original content must be preserved; interpretations are probabilistic.
- Unrestricted LLM-generated SQL, Python, file operations, shell commands, or arbitrary URL fetching.
- Microservice-per-module architecture from day one.
- Training a proprietary foundation model.
- Claiming 100% answer accuracy; enforce 100% *referential citation integrity* as a software invariant, and independently evaluate semantic grounding.

## 3. Platform and technology selections

| Responsibility | Preferred choice | Boundary / reasoning |
| --- | --- | --- |
| Web | React + **strict TypeScript** + Tailwind | Typed source viewer and answer renderer |
| API and application | ASP.NET Core 10; modular monolith | Central identity, authorization, workflows, domain behavior and query orchestration |
| Document intelligence | Python workers | Native extraction via Docling/PyMuPDF, with primary OCR through a Mistral deployment in Microsoft Foundry (Azure AI Foundry) |
| Primary cloud OCR | **Mistral Document AI / OCR on Microsoft Foundry** | Azure-hosted OCR endpoint; configurable deployed model/version, region, credentials, supported limits and response format |
| Specialized OCR/layout fallback | **Azure Document Intelligence in Foundry Tools** | Separate Azure resource/endpoint and API version; use for cell geometry, forms, or low-confidence layout requiring specialized extraction |
| Vision, generation | OpenAI or Azure OpenAI cloud APIs | Configurable by task/model policy; structured answer contract |
| Text embeddings | Configurable multilingual text embedding adapter | Store profile, dimensions, model/version, preprocessing |
| Image/multimodal embeddings | Provider supporting compatible text/image embedding spaces | Required for genuine text-to-image and image-to-image retrieval; do not substitute text-only embeddings |
| Retrieval | Qdrant + PostgreSQL lexical/structured retrieval | Filtered vector search, fusion and exact data queries; replaceable indexes |
| System of record | PostgreSQL | Document versions, element graph, ACLs, provenance, processing and audit |
| Binary objects | MinIO locally; Azure Blob in cloud | Original and derived immutable assets |
| Messaging | RabbitMQ locally; Azure Service Bus in cloud | Durable queues via shared messaging contract |
| Authentication and sessions | ASP.NET Core Identity; optional OpenIddict token service; BFF cookies; OIDC federation | Built-in users, enterprise SSO, external identities, rotation/revocation; see §22 |
| Integration secrets and MEK lifecycle | AES-256-GCM per-secret envelope encryption; pluggable Key Protection Provider | TPM2 optional for on-prem; Azure Key Vault/KMS for managed SaaS; safe software/external vault recovery; see §32 |
| Enterprise federation | Entra ID / Entra External ID, generic OIDC; optional SAML/AD DS or LDAPS integration | Tenant-configured identity providers and group/role mapping |
| Authorization | Scoped permission catalog + PostgreSQL policy resolver / RLS | Tenant roles, resource ACLs, conditional security policies; OpenFGA remains conditional |
| SaaS control plane | ASP.NET Core bounded module, optional hosted operator deployment | Tenant provisioning, entitlements, usage, billing and support boundaries |
| Distribution packaging | OCI images, Docker Compose, Helm/Kubernetes as appropriate | Same core self-hosted or operated as SaaS; signed releases/SBOM |
| Telemetry | OpenTelemetry + centralized metrics/logging/traces | End-to-end per-stage correlation |
| Deployment | Docker Compose locally; Azure Container Apps or Kubernetes | Hybrid, with cloud inference and optional managed infrastructure |

**Deployment rule:** Keep the document system, metadata, object storage, workers, and vector index independently deployable. Cloud AI processing is an explicit data-egress boundary and must comply with per-collection model/data policies.

### 3.1 Azure-hosted OCR provider topology (selected)

- **Default OCR provider:** a **Mistral OCR / Document AI deployment in Microsoft Foundry (Azure AI Foundry)**. Use the actual Foundry deployment URL, model/deployment identifier, authentication mechanism and supported request shape; never assume the native Mistral API endpoint or model ID is interchangeable with a Foundry deployment.
- **Fallback / specialist:** **Azure Document Intelligence in Foundry Tools**, via its own configured endpoint and API version (for example, the version-pinned `prebuilt-layout` or `prebuilt-invoice` model). The user refers to this as the "document foundry" service; the Microsoft documentation names it *Azure Document Intelligence in Foundry Tools*. It can be backed by a Document Intelligence resource or an eligible Foundry Tools resource. Do not assume these two providers share credentials, endpoint conventions or billing meters.
- **Native-first preprocessing:** Docling/PyMuPDF remains responsible for non-OCR native PDF extraction, page rendering, geometric normalization, and preservation of original content. `.md`, `.txt` and safe native HTML do not incur OCR by default.
- **Provider identity and availability:** Resolve model identifiers, API versions, supported regions, quotas, input page/file limits, response capabilities and data-handling policy from configuration and startup capability checks. A specific model shown in the Foundry catalog might be preview or not deployable in a chosen region/subscription. Never hardcode a model (including OCR 4.x) without verifying its availability for the deployment.
- **Failover policy:** invoke Document Intelligence if the Mistral result fails required structure, geometry, provenance or field/cell quality gates; an expensive second pass must be deliberate and auditable. A fallback failure results in `PARTIAL` / review, not a fabricated complete extraction.
- **Provider reconciliation:** normalize both outputs into the same CDM, preserve each provider's raw response, and record per-element provider/run/quality metadata. Do not blindly merge conflicting cell text, numbers, bounding boxes or table shapes. Validate source-space coordinates before issuing citations.
- **Long document handling:** use capability-checked page-count/byte limits, deterministic per-page/page-range processing, checkpoints and idempotent stitching. Treat the currently documented Foundry maximum of 30 pages per OCR request as a deployment-specific upper bound subject to runtime verification (also check file/payload limits). Preserve global page numbering and cross-batch table continuation.

Implementation contracts:

~~~text
IDocumentExtractionProvider
  AnalyzeAsync(source, requestedFeatures, policy, cancellationToken)
  GetCapabilitiesAsync(configuration, cancellationToken)

Provider implementations:
  FoundryMistralOcrProvider
  AzureDocumentIntelligenceProvider
  NativeDocumentParserProvider (no cloud OCR for supported native inputs)
~~~

Illustrative configuration shape (values are environment/Key Vault references, **not secrets**):

~~~yaml
DocumentIntelligence:
  PrimaryOcrProvider: foundry-mistral
  FallbackOcrProvider: azure-document-intelligence
  NativePdfExtraction: true
  Providers:
    FoundryMistral:
      Endpoint: ${FOUNDRY_MISTRAL_ENDPOINT}
      DeploymentId: ${FOUNDRY_MISTRAL_DEPLOYMENT}
      CredentialSource: key-vault
      Region: ${FOUNDRY_MISTRAL_REGION}
    AzureDocumentIntelligence:
      Endpoint: ${AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT}
      ApiVersion: ${AZURE_DOCUMENT_INTELLIGENCE_API_VERSION}
      LayoutModelId: prebuilt-layout
      CredentialSource: managed-identity-or-key-vault
  Routing:
    NativeFirst: true
    DefaultOcrTier: foundry-mistral
    MaxPagesPerRequest: 20       # conservative configured cap, never above deployed limit
    TargetPagesPerBatch: 15      # adaptive to serialized payload size and endpoint policy
    MaxInlinePayloadBytes: null  # required deployment-specific capability/configuration
    FallbackOnMissingRequiredGeometry: true
    FallbackOnInvalidTableStructure: true
    AllowPartialResults: true
    CloudOcrMonthlyBudget: null   # configured currency/amount/tenant guardrail
~~~

Mistral's Foundry deployment and Document Intelligence must have separate adapters, metrics, rate limits, circuit breakers, cost accounting and diagnostic provenance. Do not claim that using both services inside Azure guarantees that no cross-region movement occurs; validate actual selected regions and enterprise egress restrictions.

Official references:
- [Microsoft Foundry model catalog — Mistral](https://ai.azure.com/catalog/models?publisher=Mistral+AI)
- [Microsoft Learn — Image-to-text models on Foundry](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/use-image-models)
- [Microsoft Learn — Azure Document Intelligence in Foundry Tools](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/overview?view=doc-intel-4.0.0)
- [Microsoft Learn — Document Intelligence SDK / REST API](https://learn.microsoft.com/en-us/azure/ai-services/document-intelligence/how-to-guides/use-sdk-rest-api?view=doc-intel-4.0.0)

Do not hardcode one exact external model version into the domain. Pin actual provider model IDs, API versions, parser releases, image embedding dimensions and pricing via deploy configuration and record them in processing runs.

### 3.2 Adaptive, quality-gated OCR routing (adopted in v1.1)

Use a per-page classifier before invoking cloud extraction. It inspects native text coverage/Unicode mapping quality, raster coverage, image quality, layout density, language, table structure, and requested evidence granularity.

| Route | Purpose | Provider choice | Admission and failure behavior |
| --- | --- | --- | --- |
| 0 — Native | High-quality born-digital text and layout | Docling + PyMuPDF locally | Accept only if text and source geometry pass checks; local extraction has compute cost, not zero cost |
| 1 — Standard cloud OCR | Simple scanned pages | Cheapest **actually deployed, supported** Foundry OCR profile meeting language/quality requirements | Escalate on low OCR coverage, missing blocks, or failed evidence quality |
| 2 — Complex cloud OCR | Mixed layouts, multilingual text, equations, figures/charts | Capable Foundry-hosted Mistral OCR/Document AI deployment | Validate blocks, coordinates, language, figures and tables; escalate selectively |
| 3 — Specialist | Forms, merged/borderless tables, precise cell geometry, unresolved conflicts | Azure Document Intelligence in Foundry Tools | Require field/cell verification and source location; mark PARTIAL or review if still unreliable |

#### Route 0: native text-layer integrity admission (v1.2)

A high native-text density **does not** prove correct PDF text. Legacy scanners can embed an invisible/broken OCR layer; CAD exports can use unusual glyph encodings. Before accepting Route 0, run a lightweight, deterministic diagnostic profile:

- **Unicode/glyph sanity:** count U+FFFD replacement characters, suspicious private-use/control glyphs, unmapped glyphs, repeated gibberish patterns and encoding anomalies, relative to eligible extracted text. Inspect PDF font mapping quality and available `ToUnicode` CMaps where technically exposed. Absence of `ToUnicode` is a **signal**, not automatic proof of corruption: valid text may map via other mechanisms; mathematical symbols or non-Latin scripts need script-aware assessment.
- **Source visibility and overlap:** compare low-resolution page ink/connected components (e.g. configurable 72-DPI render) with native text bounding regions to detect visible textual regions without corresponding reliable text. **Exclude or separately classify** diagrams, bitmap photographs, vector graphics, whitespace and decorative elements; visual ink alone is not OCR evidence.
- **Text/content agreement:** flag hidden duplicated OCR layers, implausibly sparse native text on text-heavy raster pages, broken reading order and mismatches between actual glyph renderings and extracted text. Escalate uncertain regions to cloud OCR (Routes 1/2), with selective sampling on apparently clean pages.

The suggested 0.5% replacement-character and 72-DPI values from the final review are **candidate diagnostics**, not hard-coded safety cutoffs. Calibrate per PDF type, language and script using a labeled gold corpus, record each trigger and false-positive/false-negative rates, and make fail-safe escalation explicit. A valid text layer can coexist with figures that still need independent visual extraction.


The router selects **a suitable available deployment** by quality, policy, quotas and *current regional meter*; it must not depend on legacy or retired model identifiers listed in the review. Classifier thresholds and routing shares are empirical configuration, not presumed 60/20/15/5 percentages. To avoid silently treating a native text layer as complete, test small sampled/flagged regions against cloud extraction and record misses.

**Mistral Foundry request handling:** validate endpoint/auth audience, supported request modes, page/file limits and actual serialized request size during deployment checks. Split a long PDF deterministically into eligible page windows (configurable starting point 15–20 pages) and *shrink a window further* if base64 transport or size constraints demand it. Do not assume all Foundry deployments accept external URLs, direct base64, or identical SDK payloads; use the verified interface. Checkpoint each page's state, provider request ID, source document hash, run/profile hash, result asset, cost and retry status. On 429/5xx or stage crash, retry only unfinished work with bounded exponential backoff/jitter; record that a timed-out cloud request may still have been billed. A quality failure triggers an explicit specialist fallback for affected pages/regions, not blind second-pass OCR of the entire file.

For tables crossing page windows, retain and verify boundary context (headers, table ID, column geometry/units, continuation candidates). Add `continuation_of` relations only when page transitions are supported by source evidence; flag ambiguous joins for review. All generated page-local regions must be transformed to original **global** page coordinates before publication.

**Unit economics:** calculate `sum(processed_pages_by_actual_provider_model_region * contracted_rate)` plus enrichment/embeddings/retries/storage/compute. Capture per-tenant budgets and request counts. Review-proposed dollar rates and 67–87% savings are scenario claims, **not commitments**: validate rate cards, model availability, actual page distribution and measured false-negative/fallback cost before publishing estimates.

## 4. Logical architecture

~~~mermaid
flowchart TD
    A["React + TypeScript UI<br/>Search • Chat • Viewer • Admin"]
    B["ASP.NET Core API<br/>OIDC • Authorization • Orchestration"]
    C["Document Management + Ingestion"]
    D["Durable Queue + Outbox"]
    E["Python Processing Workers<br/>PDF • HTML • Image • Markdown • Text"]
    F["Canonical Document Model<br/>Elements • Relations • Locators"]
    G[("PostgreSQL<br/>Metadata • Provenance • ACL • Audit")]
    H[("Object Storage<br/>Originals • Pages • Figures • Snapshots")]
    I["Indexing Workers<br/>Chunk • Embed • Publish"]
    J[("Qdrant<br/>Text + Multimodal Vector Indices")]
    K["Retrieval Engine<br/>Lexical • Vector • Visual • Structured"]
    L["Reranker + Evidence Fetch"]
    M["Cloud Model Gateway<br/>Vision • Answer • Embeddings"]
    N["Answer + Claim/Citation Validator"]
    A --> B
    B --> C --> D --> E
    E --> F
    E --> H
    F --> G
    F --> I --> J
    B --> K
    K --> G
    K --> J
    K --> L --> M --> N --> B
    N --> G
    B --> H
~~~

### 4.1 Bounded modules

1. **Identity & Access:** deployment-local accounts, identity brokering (OIDC/SAML/AD as configured), sessions, verified identity links, tenant membership, scoped roles, group mapping, resource ACLs, machine identities, SCIM integration and tenant policy.
2. **Document Management:** upload/import, logical identity, immutable versions, hashing/deduplication, document lifecycle.
3. **Ingestion:** stage orchestration, retries, checkpoints, extraction policy, parser routing.
4. **Evidence Catalog:** CDM elements, source assets, relations, locators, corrections, lineage.
5. **Indexing:** retrieval units, embedding profiles, index generations, backfill and publication.
6. **Search:** permission-filtered lexical/vector/visual search, candidate merging, reranking.
7. **Query & Answer:** intent planning, evidence packing, model gateway, grounded structured output and citation verification.
8. **Table Analytics:** typed cells, calculation plans, verified deterministic execution, result provenance.
9. **Operations & Audit:** metrics, traceability, diagnostic events, retention, reprocessing and governance.
   - v1.4 **Evidence Governance** tracks saved-answer freshness, reviewed contradictions and notifications.
   - v1.4 **Evidence Publishing** manages briefs and cryptographic verification bundles.
   - v1.4 **Human Review** manages overlays, approvals and correction publication.
   - v1.4 **Sensitivity Policy** produces authorized document/text/asset views and policy-safe retrieval units.
   - v1.4 **Integrations** serves scoped MCP tools through the existing RAG authority.
   - v1.4 **Knowledge Quality** aggregates privacy-safe abstention and retrieval diagnostics.
10. **Tenant & Workspace Management:** tenant onboarding/lifecycle, workspace hierarchy, cross-tenant isolation, settings and configuration policy.
11. **SaaS Control Plane (optional in self-hosted):** plan entitlements, subscriptions, metering, billing integrations, quotas, platform operator support and deployment allocation.
12. **Distribution & Extension Management:** deployment modes, installed edition capabilities, migrations, tenant portability, optional integrations and signed release metadata.

Modules communicate through typed in-process interfaces initially. Python and indexing workers use versioned job/event schemas, not shared database-entity classes.

## 5. Canonical Document Model (CDM)

### 5.1 Hierarchy and independent identities

~~~text
Tenant
  └── Collection
      └── Logical Document (stable document ID)
          └── Document Version (immutable ID + content SHA-256)
              ├── Original binary/source asset
              ├── Pages / HTML snapshots / text source views
              ├── Sections and typed elements
              │    ├── Text / headings / lists / code
              │    ├── Tables -> row/column/cell objects
              │    ├── Figures / images / charts / diagrams
              │    ├── Equations / form fields / references
              │    └── Links / footnotes / annotations
              ├── Element relationships
              ├── Evidence locators
              ├── Extraction runs and diagnostics
              └── Retrieval units -> index entries
~~~

### 5.2 Required element fields

Every content element must have:

- elementId, documentVersionId, optional parentElementId, type and order within its scope;
- exact or structured content, original language(s), and metadata;
- one or more evidence locator IDs (or explicit reason why no locator is available);
- extractionRunId and parser/profile versions;
- extraction status and available quality indicators, without fabricated confidence;
- source asset references and typed relationships to other elements.

Examples of relationships: contains, follows, caption_of, depicts, references, footnote_of, continuation_of, header_for, label_for, derived_from, explains. Relationships generated by AI must be distinguishable from directly parsed or human-verified relations.

### 5.3 Provenance locators by source format

| Source kind | Primary locator | Viewer behavior |
| --- | --- | --- |
| PDF | Version ID, 1-based page, page-coordinate polygon/rect, text-span offsets as available | Open immutable PDF version at page and highlight region |
| HTML | Immutable snapshot ID, DOM locator, exact quote + surrounding text anchor, optional rendered-box geometry | Open sanitized snapshot and highlight actual node/span |
| Image | Original asset ID, native pixel dimensions, pixel-coordinate polygon/rect | Open source image, zoom to region |
| Markdown | Version ID, **1-based start/end line and column**, plus source code-point and/or byte offsets with encoding | Open raw Markdown or rendered view with matching source span |
| Plain text | Version ID, **1-based start/end line and column**, plus source code-point and/or byte offsets with encoding | Open original text with highlighted lines/characters |
| Table | Element/table ID, row and cell IDs, underlying page/DOM/text span | Highlight exact original cell(s), not just chunk |
| Computed result | Verified input cell/field IDs + expression + execution ID | Show formula, source cells and exclusions |

**Offset rule:** Store the original bytes and text encoding, and a deterministic offset map from normalized parser text to original byte/code-point/line-column locations. Unicode normalization and CRLF conversion must never destroy the ability to highlight original source text. Define whether columns use Unicode code points or UTF-16 code units in each API; prefer documented code-point offsets in storage and perform browser conversion at the UI boundary. Coordinates for PDF/image are stored with explicit coordinate system, dimensions, orientation and units.

### 5.4 Source, derived and inferred data

Each persisted artifact or field declares one provenance class:

- **Source-observed:** read directly from native text/DOM/metadata or OCR output, tied to the original region.
- **Parser-derived:** structural grouping, reading order, recognized table boundaries.
- **AI-inferred:** figure descriptions, chart interpretations, diagram entities, summaries.
- **Human-verified:** approved corrections or confirmed structured values.

A model-generated description is useful for search but is never substituted for an original image as evidence. Human corrections form versioned overlays or revisions with audit records; immutable originals stay unchanged.

## 6. Format-specific parsing strategy

### 6.1 PDFs

Determine per-page strategy: native extraction, OCR, or mixed. Preserve pages, rotation, dimensions, vector/raster originals, paragraphs, headings, columns, reading order, tables (including merged cells and spanning pages), captions, figures, forms, equation source and renderings, embedded attachments, and citations/references where available.

Use Docling/PyMuPDF for native PDF structure, rendering and geometric grounding. Classify pages using the adaptive routing policy in §3.2. When OCR/document understanding is necessary, send eligible page windows or images to a **Mistral model deployed on Microsoft Foundry** as the **primary cloud OCR family**. Use **Azure Document Intelligence in Foundry Tools** selectively for ambiguous layouts, specialized form extraction, missing cell-level geometry, poor-confidence output, or evidence integrity failures. Check regional model availability and actual endpoint capabilities; preserve the raw outputs and extraction provenance of both providers. Route based on diagnostics, supported features, evaluation results and policy, not an assumption one engine is always best.

Engineering drawings and dense charts require high-resolution source assets and region-oriented retrieval. Measurements, symbols, connectivity, tolerances and plotted values inferred by vision are **unverified until validated**. Never claim manufacturing-grade extraction accuracy without evidence.

### 6.2 HTML

Retain uploaded HTML bytes, a sanitized semantic DOM, extracted metadata and links, images, SVGs, tables, lists, code, inline semantics and anchors. For dynamic pages, use an isolated, network-restricted Playwright worker to generate a versioned rendered snapshot when policy permits.

Do not fetch arbitrary remote images/URLs. Resolve relative references against a trusted base URL or uploaded bundle; enforce scheme/host allowlists, redirect rules, maximum size, timeouts, no private/link-local/metadata IP access, and SSRF defenses. Render evidence in a sandboxed, sanitized viewer that cannot execute arbitrary scripts.

### 6.3 Images

Retain original pixels and EXIF/orientation metadata where allowed; create reproducible thumbnails/crops; extract OCR with source regions; identify charts, diagrams and photographic objects. Store AI-generated descriptions separately from extracted text. Generate visual embeddings for both entire images and selected regions. Text-to-image and image-to-image search require a multimodal embedding model with compatible input spaces.

### 6.4 Markdown (.md / .markdown) — first-class support

Markdown is **not** just plain text. Parse with a version-pinned CommonMark/GFM-compatible Markdown AST parser and preserve both **raw source** and **semantic structure**. Enable approved extensions via a documented parser profile.

Required extraction:

- YAML/TOML front matter where present, retaining raw representation and parsed values separately; reject unsafe or unexpected tags/types.
- ATX and Setext headings; heading hierarchy, paths and slugs.
- Paragraphs, emphasis, strong emphasis, inline code, blockquotes, horizontal rules.
- Ordered/unordered/nested lists and GitHub-Flavored Markdown task lists.
- GFM tables with header associations and exact source cell spans where parser capability permits.
- Fenced and indented code blocks, declared language and literal content (do **not** execute).
- Inline links, reference-style link definitions, anchors, autolinks, footnotes where supported.
- Embedded images with alt text, captions/adjacent text, local asset references and external URLs.
- Raw inline/block HTML retained as untrusted content and sanitized for rendering.
- Mermaid or similar fenced diagrams stored as original code and optionally rendered **only in a safe renderer**; generated images and descriptions are derived artifacts, not authoritative source.
- Mathematical extensions such as inline/block LaTeX when enabled; preserve original notation.
- MDX-like components or unsupported custom directives must be retained as unknown/code-like elements unless a separate vetted parser profile is enabled. Never evaluate JSX/JS/expressions on ingestion.

**Markdown asset resolution:** For Markdown files uploaded with an asset bundle, resolve relative image paths only within that bundle's safe root. Store asset relationships and references. Do not automatically download external URLs. Broken or blocked images produce explicit diagnostics and citation-safe placeholders.

**Markdown citation anchors:** Preserve original source encoding, line endings, line/column spans, and quote hashes. For example, a paragraph at lines 24–27 is cited as a versioned source range; a code fence at lines 39–52 retains the exact fence/content boundaries. Table cells preserve row/column plus source offsets where parsable. Viewers offer *Raw* and *Rendered* modes, with source-to-rendered mappings when available.

**Markdown indexing:** Index heading-aware sections, paragraphs, code blocks (separate code-aware profile), table segments and image descriptions as separate retrieval units. Attach ancestor headings and nearby definitions as context, without losing element-level citations. Keep literal fenced code searchable without obeying instructions inside it.

### 6.5 Plain text (.txt / .text) — first-class support

Preserve raw bytes, detected/declared charset, BOM, original newline style, normalization profile, and line-offset map. Support UTF-8 and common BOM-indicated UTF-16 encodings, and explicit charset selection where ambiguous. Flag invalid sequences and uncertain encodings; do not silently corrupt text.

Extract logical paragraphs and lines with deterministic source offsets; optionally infer headings, lists, key/value blocks and sections as **inferred structure** with confidence/diagnostics. Keep special content such as logs, code snippets, CSV-like text and ASCII diagrams as literal text unless routed to a specific configured parser. Index multilingual content and ensure source citations identify exact line ranges.

### 6.6 Parser completeness manifests

For each input, save a manifest containing discovered pages/sections, parsed element counts by type, assets found/resolved/missing, unsupported content, OCR coverage, parser warnings, confidence availability, processing duration, and source hashes. A completed run is not necessarily a semantically complete document: show a separate extraction quality/coverage status.

## 7. Durable ingestion workflow

~~~mermaid
flowchart TD
    U["Upload / Import"]
    V["Validate type, size, permissions, malware; SHA-256"]
    S["Store immutable original + metadata"]
    Q["Queue stage with outbox"]
    D{"Detected source type"}
    P["PDF native parse + Foundry Mistral OCR as needed"]
    H["HTML parser"]
    I["Image parser + Foundry Mistral OCR as needed"]
    MD["Markdown AST parser"]
    T["Plain-text parser"]
    N["Normalize -> CDM + locators"]
    C["Check quality/coverage and assets"]
    F{"Accepted?"}
    R["Azure Document Intelligence in Foundry Tools<br/>or manual review / partial status"]
    K["Persist elements + provenance"]
    X["Create retrieval units and embeddings"]
    Y["Stage and validate all indexes"]
    PUB["Atomic READY publication"]
    U --> V --> S --> Q --> D
    D --> P --> N
    D --> H --> N
    D --> I --> N
    D --> MD --> N
    D --> T --> N
    N --> C --> F
    F -->|No| R --> C
    F -->|Yes| K --> X --> Y --> PUB
~~~

A document version progresses through RECEIVED, VALIDATED, EXTRACTING, NORMALIZING, QUALITY_REVIEW, INDEXING, and READY. FAILED, PARTIAL, QUARANTINED and DELETED are separately represented outcomes. A PARTIAL version must never silently replace a READY version.

**Idempotency and recovery:**

- Every job has stable job ID, tenant ID, version ID, stage ID, processing profile hash and idempotency key.
- Store stage outputs in versioned, content-addressed objects and manifest records before acknowledging work. Queue envelopes contain only identifiers and verified claim-check references.
- Use transactional outbox to publish messages tied to metadata commits; assume at-least-once delivery.
- Reprocessing creates a new processing run and index generation; do not mutate past lineage.
- Quarantine malicious/oversized input and apply stage-specific timeouts, concurrency limits, exponential backoff and dead-letter handling.
- Commit publication only when mandatory assets, element graph, authorization checks, exact retrieval-unit/index manifests and source locators are valid; keep the older ready version visible until cutover.
- Allow explicit reindex without re-upload or reparse if source/CDM remains compatible.

### 7.1 Claim-check queue contract (adopted)

Document content, CDM JSON, OCR responses, page images, offset maps and vector batches **must not be placed on RabbitMQ/Service Bus messages**. Write immutable stage artifacts to object storage, verify SHA-256/size and existence, then send a small envelope through the transactional outbox. Configure a **target maximum of 16 KiB serialized envelope size** (lower than any applicable broker tier limit); reject oversized publishes.

~~~json
{
  "schemaVersion": 1,
  "jobId": "job-uuid",
  "tenantId": "tenant-uuid",
  "documentVersionId": "version-uuid",
  "processingRunId": "run-uuid",
  "stage": "normalize",
  "profileHash": "sha256:...",
  "idempotencyKey": "stable-stage-key",
  "artifact": {
    "assetId": "asset-uuid",
    "sha256": "sha256:...",
    "sizeBytes": 12345678
  }
}
~~~

`assetId` resolves to an authorized immutable object through the Evidence/Asset catalog; workers never trust arbitrary blob URLs received in messages. Enforce schema versioning, tenant alignment, hash/length checks, processing lease ownership and outbox durability before acknowledgement.

**Retention race:** never delete the referenced object when the consumer finishes. Keep it through broker redelivery/recovery, dependent runs and required audit retention. Garbage collection is a separate audited workflow that checks final status, dependency graph, retention/legal holds and absence of active leases. Failures go to bounded retry and a diagnosable dead-letter queue, with an administrative replay operation.

### 7.2 Checkpoint granularity and budget-aware retry (refined in v1.2)

**Cloud OCR/vision (Routes 1–3):** Persist per-page `PENDING / IN_PROGRESS / COMPLETE / PARTIAL / FAILED / SKIPPED` status, provider/deployment/request ID, global page ordinal, run/profile hash, attempt fencing token, quality diagnostics, source/result asset hashes, accumulated cost and retry metadata. A provider call may be billed even if it times out: do not assume retry is free. Checkpoint stage output and page/result manifests before acknowledgement; resume incomplete page windows and reconcile possible duplicate requests.

**Local native PDF (Route 0), Markdown, plain text and deterministic local HTML:** Default to **document-level or bounded-batch checkpoints** with immutable manifest entries rather than one PostgreSQL row transition per page. Escalated cloud pages subsequently receive granular cloud checkpoints. For unusually expensive native tasks, tune local batch size and checkpoint frequency from measured recovery cost; never lose page/global-offset traceability.

In both routes, the worker first proves lease ownership and commits through the fencing/CAS protocol (§7.3). Recovery reconstructs work from authoritative manifests, never from broker delivery count. Test mixed 150-page PDFs, partial cloud retries, source-global-page stitching, table continuation, local worker crashes and missing/duplicated page detection.

### 7.3 Enforced monotonic fencing and conditional writes (I1 correctness invariant)

**A broker lock is not the authoritative lease.** A redelivered job can leave an older process running; `deliveryCount` alone is **not** a fencing token. Keep a PostgreSQL-authoritative, per-stage lease record with `lease_epoch BIGINT` (monotonically incremented in a transaction on acquire/takeover), `owner_attempt_id`, `expires_at`, `status` and an optimistic row version. Workers receive their expected epoch when acquiring the lease.

Every stage status/checkpoint mutation must compare **`processingRunId + stageId + lease_epoch + owner_attempt_id`** (and tenant/version scope) and succeed only while the lease is current. Reject zero affected rows as **stale writer**, not success. Representative compare-and-swap, inside a transaction with appropriate row locking/clock policy:

~~~sql
UPDATE stage_leases
SET status = 'COMPLETE', updated_at = now()
WHERE processing_run_id = @runId
  AND stage_id = @stageId
  AND owner_attempt_id = @attemptId
  AND lease_epoch = @expectedEpoch
  AND expires_at > now()
  AND status = 'IN_PROGRESS';
-- Exactly one updated row is required; otherwise abort publication.
~~~

A consumer must **not** check ownership, release its lock, and later write without a guarded transaction; use conditional writes against the authoritative stage lease for the actual data rows and subsequent state transitions. PostgreSQL writes that commit CDM elements/manifests must be guarded in the same transaction by the current epoch and attempt. For multi-table bulk staging, use an attempt-scoped staging namespace and a final fenced merge/publish operation; an expired writer can at most leave unreachable staging data.

Store derived Blob objects under immutable `runId/attemptId/contentHash` paths, with create-only conditional upload (`If-None-Match: *`) or appropriate storage lease/precondition. **Do not allow overwrite of shared mutable output paths.** Object creation alone does not grant authority to publish: only the winning fenced attempt's verified artifact hashes may be linked by a guarded PostgreSQL transaction. Apply the same check when publishing page manifests, completion messages and the active document/index generation (§15.1). Reconcile stale artifacts asynchronously, respecting claim-check retention and active query leases.

Acceptance tests must pause Worker A past lease expiry, let Worker B finish and publish, then resume A: all of A's stale PostgreSQL writes and active publication must fail; its blob output must never replace B's evidence.

### 7.4 Tenant-fair outbox scheduling and coordinated OCR backpressure (I1/I2)

Use distinct **priority classes** for interactive/small tasks and bulk ingestion, while retaining per-document or per-stage causal order. Lease outbox rows transactionally with bounded batches (e.g. PostgreSQL `FOR UPDATE SKIP LOCKED`), then dispatch using per-tenant quotas/weighted round-robin and aging so neither a large tenant nor nonstop high-priority traffic starves other queues. Query audit events must remain durable and not depend on bulk-ingestion throughput. Define publisher recovery, duplicate publication and poison-message behavior explicitly.

Cloud OCR queues are also globally subject to **provider + deployment + region rate/concurrency budgets**, not only each Python worker's private retry counter. Use a coordinated token-bucket/backoff state (durable PostgreSQL coordinator initially; optionally Redis/managed rate limiter when justified), enforce a shared concurrency limit, observe `Retry-After`, and pause/reduce dispatch on provider-wide 429/503 or outage signals. Retain per-tenant usage/budget guards and avoid a retry storm when multiple replicas restart.

Broker ordering, distributed token-bucket accuracy and fairness depend on implementation; validate under 5,000-document bulk uploads, parallel workers, provider throttling, malformed work items and lease loss. Scheduling cannot bypass authorization, fencing or the immutable artifact contract.


## 8. Persistent data model

PostgreSQL is the system of record; use UUID/ULID identifiers consistently and define all foreign keys, indexes, unique constraints and tenant consistency checks in migrations.

| Table / aggregate | Main data |
| --- | --- |
| deployments, deployment_capabilities | Instance identifier, hosting mode, optional operator controls, capabilities and migration version |
| tenants, tenant_lifecycle_events, verified_domains | Organization settings, onboarding/suspension/termination, verified login domains and model/data egress rules |
| workspaces | Tenant-scoped subdivisions above collections; settings, ownership and policies |
| tenant_identity_providers, external_identities | Issuer/subject identities, federation settings, verified linking and tenant SSO rules |
| auth_sessions, refresh_token_grants, credentials | Built-in authentication session/grant metadata, rotation families and revocation records; protect sensitive fields |
| key_protection_profiles, key_versions, key_envelopes | Protector type/capabilities, KEK/MEK lifecycle, tenant association, wrapped-key references, recovery and activation status (never raw key material) |
| integration_secret_records, integration_secret_versions, secret_access_audit | Encrypted wizard secret ciphertext, AEAD context, wrapped DEKs, provider credential version/rotation and auditable access |
| role_definitions, permissions, role_assignments, policy_conditions | Built-in and tenant-custom roles, permission keys, scoped assignments and conditional policy gates |
| service_accounts, api_clients, credential_rotations | Tenant/workspace-scoped machine identities, grants, expiry and audit |
| scim_connections, provisioning_events | Automated tenant user/group provisioning and deprovisioning |
| subscriptions, plan_entitlements, tenant_usage, tenant_quotas | Commercial plan state, feature gates, per-provider metering and workload limits |
| tenants | Settings, security policy, model/data egress rules |
| users, groups, group_members, memberships | External identity mapping and tenant/group membership |
| collections, access_policies | Workspace/collection/document scope, inherited grants, explicit denials and classification conditions |
| documents | Logical document identity, tenant/collection, active ready version |
| document_versions | Immutable hash, MIME, parser profile, original asset, processing/publication status |
| document_pages_or_views | Page geometry, rendered HTML snapshot, source text viewport metadata; optional cached compressed line-start maps for hot citations |
| document_elements | Typed hierarchy, reading order, source/derived/inferred data, extraction run |
| element_relationships | Typed element-to-element relations |
| evidence_locators | PDF polygons, DOM anchors, image regions, Markdown/text line and offset ranges |
| assets | Immutable binaries, size/hash/type, original/derived classification |
| extracted_tables, table_rows, table_cells, table_cell_header_links | Cell spans, hierarchical column/row headers and element evidence, original literal, raw/normalized decimal values, inherited scale/unit/currency and locators |
| retrieval_units, retrieval_unit_elements | Search text/metadata and exact underlying element membership |
| embedding_profiles, index_generations, index_entries | Provider/model settings, vector IDs, generation/version and state |
| processing_runs, processing_events, stage_leases, cloud_page_processing_states, job_outbox | Input profile, epoch/attempt-fenced stage ownership, cloud-page checkpoints/costs, verified artifact references and durable workflow |
| conversations, queries, answers | Query/answer versions and provenance |
| answer_claims, answer_citations | Evidence-backed claim references and semantic support verification |
| calculation_runs, calculation_inputs | Authorized deterministic expression, contributing input values and result |
| audit_events, corrections | Actor, action, targets, version history and review |
| saved_answers, evidence_dependencies, answer_freshness_states | Saved/pinned answer versions, reverse element/claim dependencies and source-freshness status |
| evidence_governance_events, contradiction_candidates, notification_subscriptions, notification_outbox | Supersession/contradiction review events, authority/effective-date policy, deduplicated alerts |
| evidence_briefs, evidence_brief_revisions, brief_blocks, verification_manifests | Editable briefs, immutable published contents, citation/calculation snapshots, signed hashes and export lineage |
| review_tasks, review_decisions, verification_overlays | Tenant-scoped extraction review state, optimistic task ownership, approvals, immutable human-correction evidence |
| sensitivity_annotations, redaction_policies, sanitized_derivatives | Located PII/classification findings, explicit enforcement policy, policy-versioned safe content and assets |
| mcp_client_grants, integration_audit_events | MCP client identification, delegated user/service principal scopes, tool/action budgets and audit |
| knowledge_quality_events, knowledge_quality_aggregates | Privacy-filtered abstention/retrieval outcomes and tenant/workspace-level analytics |

### 8.0.1 Tenant-scoped identities and resource hierarchy (v1.3)

Canonical resource ancestry is **Deployment → Tenant → Workspace → Collection → Document → Version → Evidence**. Existing versions/claims remain document-bound, with workspace ownership derived through immutable audited parent references. Stable internal user IDs are global within a deployment; tenant-specific membership and role assignments are separate records; external identity uniqueness uses **issuer + subject** (plus issuer configuration/realm where required), never email alone. Tenant-scoped role IDs, provider configs, policies, service accounts and usage records cannot be reused across tenants.

Shared SaaS databases require composite tenant-aware referential constraints and row-level security as defense in depth, with explicit tenant context resolved server-side from authenticated membership. Document/index/object/cache/query/workflow records carry or inherit an immutable tenant reference and are tested for cross-tenant join and cache-key leakage. Deployment isolation levels are selectable via a data-placement abstraction; details in §23.

### 8.1 Core schema constraints

- Tenant ownership is mandatory. Validate tenant consistency across every document, element, asset, retrieval unit, calculation, and citation relation; do not rely solely on application joins.
- A document version is immutable once published. A new upload/revision creates a new version ID.
- The logical document points to one active READY version. Never point it to an incomplete index generation.
- Each retrieval unit maps to one or more CDM element IDs; each index entry records its source retrieval unit and generation.
- Persisted processing work is attempt-fenced: `stage_leases.lease_epoch` increments on takeover, stale attempt writes must fail through guarded database writes, and binary derivatives are write-once/attempt-scoped; only the current attempt may publish.
- Store exact-value table cells using NUMERIC/decimal fields plus original literal, locale, unit/currency, **source scale and normalized magnitude**, source header/caption evidence and verification state; no floating-point arithmetic for money.
- Evidence locators have format-specific validation (PDF page bounds; image pixel bounds; Markdown/text valid source span and encoding; HTML snapshot/anchor existence).
- Answers link claims to authorized, resolvable evidence IDs, never opaque arbitrary URLs.
- Support row-level security as defense in depth, with vetted service roles and explicit tenant context; avoid a privileged application role that bypasses all checks.

### 8.1.1 Scale-aware numeric and offset-map fields (adopted)

Add these fields to the future table-cell contract (illustrative types; not yet a migration):

~~~sql
-- Values must be tenant-scoped and tied to original cell + header/caption evidence.
original_literal         TEXT NOT NULL,
raw_numeric_value        NUMERIC(38, 10) NULL,
scale_power_of_ten       SMALLINT NULL,
normalized_numeric_value NUMERIC(38, 10) NULL,
unit_code                TEXT NULL,
currency_code            TEXT NULL,
scale_source_element_id  UUID NULL,
numeric_verification     TEXT NOT NULL
-- e.g. VERIFIED, UNVERIFIED_SCALE, UNVERIFIED_OCR, MISSING_UNIT
~~~

`scale_power_of_ten = 7` represents a source declaration such as "figures in crores" (10^7), provided that currency, context and scaling convention have been verified. A nullable scale distinguishes unknown from zero; never silently default an unresolved scale to 1. For arbitrary unit conversions, percentages, negative parentheses and non-decimal multipliers, use explicit typed conversion/normalization metadata rather than forcing all conversions into a power of ten.

For Markdown/text citations, the canonical original offset map remains a durable object; optional compressed line-start offsets (byte/code-point) in PostgreSQL or a bounded cache reduce repeated blob fetches. Introduce this cache after measuring citation lookup latency, and validate cached data against the original version/normalization hash.

**Partitioning policy:** implement tenant-scoped composite keys, tenant-filtered indexes and observable slow-query plans from I1. PostgreSQL partitioning by tenant/hash or document/version is an *evaluated scaling option* when real cardinality and explain plans warrant it; partitioning at ingestion start is not a correctness dependency, and naïve per-tenant partitions may create serious operational overhead.

### 8.1.2 Hierarchical table headers and cell-level provenance (v1.2)

Do **not** flatten a stacked header into a single label or depend on one `scale_source_element_id`. Represent the table header structure as CDM elements with `rowspan`, `colspan`, structural parent/child or `header_for` links, reading order and source locators. A data cell may inherit **multiple row and column ancestors** (year, quarter, audit status, unit scale, row category).

Provide a relational `table_cell_header_links` mapping `(tenant_id, table_cell_id, header_element_id, axis, level, path_order, relation_type)` to the original verified header elements. Optionally materialize `column_header_path TEXT[]` and `row_header_path TEXT[]` on the cell/read model **as derived indexes**, not substitutes for source-element references. Source evidence also records whether a header is parsed, AI-inferred or human-verified.

Example for a cell: column path `['FY 2025', 'Q2', 'Audited', 'INR Crores']`; row path `['Revenue', 'Domestic']`. Typed dimensions (`fiscal_year = 2025`, `quarter = 2`, `audit_status = 'Audited'`) are derived from validated header ancestry, not unreviewed string comparisons. Propagate units/scale from the **correct branch**, apply precise precedence for cell/column/row/table/footnote declarations, and mark conflict or ambiguity `UNVERIFIED_HEADER` / `UNVERIFIED_SCALE`.

Acceptance: stacked/multi-row/merged headers, bilingual labels and partial continuation tables preserve all header relationships; filtering by audited quarter and summing normalized values returns precisely the authorized, verified source cells.

### 8.2 Binary object layout

~~~text
tenant/{tenantId}/documents/{documentId}/versions/{versionId}/
  originals/source.ext
  manifests/ingestion.json
  extraction/{runId}/raw-parser-output.json
  extraction/{runId}/pages/{pageNumber}/page-manifest.json
  canonical/{runId}/cdm.json
  pages/page-0001.png
  assets/figures/{assetId}.png
  assets/tables/{assetId}.png
  html/{snapshotId}/sanitized.html
  text/{runId}/offset-map.json
  indexes/{generationId}/manifest.json
~~~

Paths are internal implementation details, **never** substitutes for authorization. Object access uses short-lived server-authorized URLs or mediated streaming. Retain a content hash for each original and derivative.

## 9. Index construction and retrieval

### 9.1 Retrieval representations

| Unit | Representation |
| --- | --- |
| Document | Title/metadata/verified abstract; document-wide summary as derived |
| Section | Heading path and section summary plus child references |
| Paragraph | Original text, nearby context, section ancestry |
| Markdown text | Paragraph/list/heading grouped by AST hierarchy; preserve exact source spans |
| Markdown code | Literal source + language and nearby headings, stored in a separate retrieval class |
| Plain text | Line/paragraph range with source offsets and optional inferred headings |
| Table | Caption, headers, units, verified data; logical row/column regions |
| Figure or diagram | Original image reference, OCR, caption, inferred description, multimodal image embedding |
| Chart | Figure embedding, axis/legend OCR, validated data where available |
| Equation | Original notation and source locator |
| Page | Optional whole-page visual embedding and layout description |
| Structured field | Typed name/value/unit/date with record and source reference |

Use section-aware chunking rather than a universal character splitter. Start text units around 400–700 tokens, tune empirically, and do not split tables/figures solely at token limits. Long tables may produce linked subtable units while preserving complete structure.

### 9.2 Search paths

1. Lexical search (exact part numbers, filenames, phrases, code terms, table field names).
2. Multilingual semantic text search.
3. True text-image and image-image multimodal search.
4. Structured field and table queries.
5. Hierarchical document/section discovery and version-aware comparison.

Fuse candidates (e.g., reciprocal rank fusion), deduplicate by canonical element, rerank, expand to parent context, then fetch immutable evidence. Filter by tenant, permissions, collection, document and active version **before** candidates are exposed to the model. Re-check on evidence fetch and asset delivery.

Qdrant and lexical indexes are rebuildable projections. Index entries store tenantId, collectionId, documentId, documentVersionId, retrievalUnitId, modality and indexGenerationId. Prefer immutable structural identifiers instead of denormalized per-user/group ACL arrays. Authorization is resolved from the authoritative policy provider before candidate exposure, with indexed structural scope prefilters where feasible and a final fail-closed check before context or assets are returned. Immediately deny revoked documents independently of asynchronous index maintenance. Handle large authorized scopes through tested batching/overfetch/planning, without silently collapsing recall. Do not trust stale vector payloads to grant access.

### 9.2.1 Authorization provider and filtered recall (adopted interface; conditional provider)

Define `IAuthorizationPolicyResolver` with `CheckAsync` and `ListAccessibleResourcesAsync` operations. Begin with PostgreSQL-backed resource grants/RLS plus short-lived, invalidatable permissions; keep OpenFGA as an **optional** implementation after measured requirements justify ReBAC graph traversal. Avoid claiming instant propagation or sub-millisecond checks: consistency, cache behavior, permission graph size and list-object latency are observable SLO concerns.

The retrieval planner obtains an authorized, version-pinned document/collection scope from this provider and applies tenant + indexed structural filters as early as practical. It then verifies each candidate before model-context inclusion and reauthorizes on evidence/asset delivery. Measure Recall@K under real mixed-permission filters, authorized-set cardinality and revocation races. No optimization may expose cross-tenant material.

#### Cardinality-aware permission-safe query planning (v1.2)

Do not serialize thousands of allowed document/version IDs into every Qdrant request by default. Use a planner with **validated, exhaustive** permission-scope alternatives and configurable thresholds derived from filter bytes, cardinality, CPU time and recall measurements:

1. **Broad collections:** only when the principal is genuinely authorized for the collection, use indexed `tenantId + collectionId` filters, with any complete, explicitly resolved per-document deny/override exclusions (`must_not`) and separate union branches for explicit document grants. A broad collection filter **without correct exceptions** is forbidden. When exclusions are huge or authorization cannot be enumerated exactly, this plan is ineligible.
2. **Small document scopes:** use explicit bounded document/version ID filters (the review's ~500 is a tuning hypothesis, **not** a fixed boundary).
3. **Mixed or very large scopes:** decompose into bounded disjunctive branches or select an alternative index/overfetch plan supported by authoritative permission checks. Do not silently truncate allowed IDs. If a safe efficient plan is not available, fail closed with a diagnosable policy/planner limitation rather than executing an unauthorized search.
4. **Active-version pinning:** broad collection prefilters alone may return stale generations. Enforce the query-pinned `activeVersionId + indexGenerationId` set at candidate resolution and before LLM input; expand/requery when stale or denied candidates reduce recall. Permission revocation invalidates accessible candidates even if payload/index updates lag.

Index the tenant, collection, document, version and generation structural payload fields. Benchmark collection-level scopes, explicit grants, negative overrides, 500/5,000/8,500-document scopes, the serialized filter size, p95 latency, Recall@20, and concurrent revocation. Do not equate prefilter efficiency with final security: **zero unauthorized evidence may reach the model or client**.


### 9.2.2 Advanced retrieval alternatives (benchmark-gated)

For dense engineering drawings or visually complex pages, assess page/region **late-interaction multi-vector retrieval** (ColPali-family, MaxSim) with two-stage dense/MUVERA-like candidate prefetch. Compare against the default cloud multimodal embedding pipeline on a labeled visual gold set before making it required. Candidate patch saliency is an **approximate localization hint**, never authoritative page geometry or a valid citation polygon without source alignment/verification.

Evaluate Qdrant scalar/binary quantization, original-vector disk placement, oversampling and exact rescoring *per actual embedding family*. Quantization ratios, Recall@20, RAM footprint, and p95 latency must be measured and cannot be copied from illustrative review benchmarks. These are optional I4/I6 optimization ADRs; production must remain correct without them.

### 9.3 Query stages

~~~mermaid
flowchart TD
    A["Authenticated query / optional image"]
    B["Resolve principal, scope and policies"]
    C["Intent detection and language handling"]
    D["Lexical + semantic + visual + structured retrieval"]
    E["Authorization filtering, fusion, dedupe"]
    F["Rerank, context expansion, evidence fetch"]
    G{"Numerical / table calculation?"}
    H["Typed validated calculation plan"]
    I["Read-only deterministic executor"]
    J["Grounded LLM with evidence IDs"]
    K["Validate claim support, citations, assets and ACL"]
    L["Typed answer blocks + original images + citations"]
    A --> B --> C --> D --> E --> F --> G
    G -->|Yes| H --> I --> J
    G -->|No| J
    J --> K --> L
~~~

Routing intents include general Q&A, visual search, diagram request, exact lookup, table computation, cross-document comparison, and document navigation. Queries may use more than one retrieval path concurrently.

A model receives short-lived, preauthorized evidence IDs and content. In structured output it may *reference only those IDs*. The backend validates references and runs an independent grounding evaluation. For insufficient evidence, return an explicit abstention or partial answer, not invented support.

## 10. Table analytics and precise calculations

Store parsed cells separately with table ID, row/column, merged spans, headers, typed value, original text, units/currency, locale, verification status and source locator. Preserve footnotes and multi-page continuation.

Workflow:

1. Retrieve permitted and verified tables/fields.
2. LLM emits a **typed calculation intent**, never arbitrary SQL.
3. Validate schema, allowed operations, filters, units, time zone/date semantics, currency, user permission and data quality.
4. Compile to parameterized, read-only SQL over authorized data, with resource/time limits.
5. Execute NUMERIC/decimal arithmetic with explicit rounding rules.
6. Return result, expression, input cell IDs, excluded/ambiguous records, and evidence citations.

Example plan:

~~~json
{
  "operation": "sum",
  "dataset": "verified_invoice_fields",
  "filters": [
    { "field": "invoice_date", "operator": "between", "values": ["2026-09-01", "2026-09-30"] }
  ],
  "measure": "net_amount",
  "currency": "INR",
  "requireVerifiedValues": true
}
~~~

Do not silently calculate across incompatible currencies/units or ambiguously OCR-recognized values.

### 10.1 Inherited units and numerical magnitude (adopted)

During normalization, traverse **the full column and row header hierarchies (§8.1.2)** along with table-level titles, adjacent notes, headings and footnotes for declarations such as "USD in thousands", "₹ crores", "millions", or "%". Link every normalized cell value to **the actual header/caption/footnote source element** that establishes its scale and currency. Verify precedence when cell, column and table-level declarations differ. Materialize normalized values only after type, scale, currency and unit validation; explicitly mark ambiguous cells `UNVERIFIED_SCALE`.

The analytics compiler excludes unverified values by default and returns them in `excluded_records` with reasons and evidence IDs. A request requiring complete totals must return `partial_result` or `insufficient_verified_data` if any material input is excluded; it must not present a partial sum as a complete total. Test mixed "units / thousands / crores", percentages, local decimal separators, signs and cross-document currency mismatches.

### 10.2 Resource-bounded read-only analytics sandbox (v1.2)

A typed/parameterized query can still execute a pathological join or aggregation. Restrict the analytics compiler to approved tables/columns, bounded predicates, joins and aggregation shapes; validate per-tenant authorized records and verified units before execution. Prohibit arbitrary SQL, functions, DDL and model-generated code execution.

Run calculations under a **dedicated least-privilege, read-only PostgreSQL role and connection pool** (read replica/isolated analytics store is optional after load tests). Every request runs inside an explicit read-only transaction with tenant/RLS context and configured statement, lock, memory, temporary-file, row/result and concurrency limits; use query-plan budget checks for expensive cross-table operations. Illustrative per-transaction limits (tune against real workloads, not guarantees):

~~~sql
BEGIN TRANSACTION READ ONLY;
SET LOCAL statement_timeout = '3s';
SET LOCAL lock_timeout = '250ms';
SET LOCAL work_mem = '16MB';
-- Execute only a compiler-validated, parameterized SELECT.
COMMIT;
~~~

Server-enforced admission and output limits remain necessary: `work_mem` is applied per eligible executor node and does **not** cap total query memory. A timeout or rejected plan returns an explicit `resource_limit` / partial result with safe diagnostics; it must never be silently reinterpreted as "no data". Profile and audit query time, intermediate rows, spill, input-cell count, results and tenant. Test permission and RLS isolation independently of performance.

## 11. Grounded answer and citation contract

### 11.1 Answer structure

Use discriminated answer blocks: paragraph, list, table, image, equation, calculation, citation group and warning. Images are **retrieved original assets** or explicitly labeled derived crops; never substitute an AI-generated illustration for a source image.

Illustrative response:

~~~json
{
  "queryId": "query_983",
  "status": "answered",
  "language": "en",
  "blocks": [
    { "type": "paragraph", "text": "The pump supplies the filter.", "citationIds": ["ev_12"] },
    { "type": "image", "assetId": "asset_009", "caption": "Lubrication circuit", "citationIds": ["ev_13"] }
  ],
  "citations": [
    {
      "evidenceId": "ev_12",
      "documentId": "doc_42",
      "documentVersionId": "ver_7",
      "elementId": "elem_52",
      "locator": { "sourceKind": "markdown", "startLine": 24, "startColumn": 1, "endLine": 27, "endColumn": 42 }
    },
    {
      "evidenceId": "ev_13",
      "documentId": "doc_42",
      "documentVersionId": "ver_7",
      "elementId": "elem_53",
      "locator": { "sourceKind": "image", "assetId": "asset_009", "polygon": [[120, 40], [800, 40], [800, 540], [120, 540]] }
    }
  ],
  "warnings": []
}
~~~

### 11.2 Citation verification

- **Identity:** evidence ID was issued by retrieval for this query and principal.
- **Reference:** document version, element and locator exist and hashes are valid.
- **Authorization:** principal still has permission to view this evidence/asset.
- **Structural accuracy:** cited source range resolves to matching bytes/region/DOM anchor.
- **Semantic support:** cited content actually supports the associated claim; unsupported assertions are removed, revised or flagged.
- **Completeness:** numerical output includes input-cell and calculation provenance.

Do not conflate a citation that resolves with a citation that proves a claim. Both are evaluated separately.

Citation links open exact immutable source versions. Historical citations remain stable even when a newer version is published, subject to retention and access policy.

### 11.3 Streaming contract — verified blocks only (adopted, NOT speculative claims)

SSE may begin immediately for `query_started`, `retrieval_progress`, `evidence_found`, `verification_progress` and heartbeat events. However, **do not render raw, unverified factual LLM token deltas as an authoritative answer**, even with a provisional badge.

Server processing for each typed block: generate candidate block -> resolve the cited evidence IDs -> enforce current authorization, locator and type checks -> validate claim support against evidence (or deterministic calculation provenance) -> validate the dependency graph -> emit `answer_block_verified` carrying a stable ordered block ID and full authorized citation data. Only verified blocks are appended to the answer display. On failure, regenerate, downgrade to an explicit warning, or abstain; never stream an unsupported block and retract it later. The server can pipeline generation/verification of separate blocks, but must preserve deterministic block ordering and query-version pinning.

**Cross-block logical consistency (v1.2):**

- Generate **self-contained blocks** by default: no dangling pronouns, unsupported "as noted above", or conclusions that require a hidden/failed statement.
- Model each candidate's `dependsOnBlockIds` and `verificationGroupId` in the internal answer contract. Verify a **dependency closure**: a block is releasable only if every referenced earlier block is authorized, successfully grounded, and either already committed or committed atomically with it.
- Treat connected prose, a numerical conclusion with its prerequisite calculation, or multi-block reasoning as one **atomic verification/commit group** where independence cannot be guaranteed. Images, tables and citation cards may be independently released only when semantically self-contained.
- If any member of an uncommitted group fails, **withhold the entire group**, regenerate from currently validated evidence or output one verified warning/abstention. Never expose dependent Block 3 after suppression of Block 2.
- Commit event groups in deterministic sequence order. For independently verified blocks already emitted, later failures must not silently alter their source meaning; retain pinned version/authorization requirements throughout.

Acceptance: simulate a suppressed middle paragraph, failed prerequisite table calculation, modified evidence and mid-stream revocation. No released block may contain a dangling reference to a rejected/uncommitted block, and no unverified claim becomes visible as an authoritative answer.

Client protocol: `query_started`, `progress`, `answer_block_verified`, `warning`, `query_completed`, `query_failed`. Events carry sequence IDs, schema versions and query IDs; reconnect/resume behavior must not duplicate or reorder blocks. If authentication or document access is revoked mid-stream, abort/suppress further protected blocks. Semantic grounding is a graded assessment rather than proof of absolute truth; track unsupported-claim and abstention rates.

LLM prompt caching is an **opportunity, not a promised 80–90% hit rate**. Keep stable system instructions/policy prefixes where provider caching supports them; order evidence deterministically when doing so does not harm relevance, and measure cache-hit/cost against retrieval quality. Never reuse an unauthorized, revoked, changed-version or cross-tenant prompt prefix.

## 12. React UI and evidence navigation

Workspaces:

1. **Search/Chat:** multilingual queries, optional image uploads, filters, typed answer blocks, original figures/tables and clickable citations.
2. **Evidence Viewer:** split view for answer/source, PDF.js page overlays, HTML snapshots, image zoom/region overlays, Markdown raw/rendered source spans, text line highlighting; policy-safe evidence renditions where required.
3. **Document Explorer:** files, collection hierarchy, outlines, extracted elements, tables/figures, metadata, versions and indexed status.
4. **Extraction Inspector + Review Inbox:** original vs parsed layout, warnings, missing assets, OCR regions, table reconstruction, reviewer-assigned correction tasks, verification/approval and derived overlay publication.
5. **Admin + Knowledge Health:** collections, policies, roles, ingestion retries, parser profiles, model configurations, usage/cost, governance/contradiction alerts, sensitivity policy, privacy-safe abstention trends, audit and evaluations.
6. **Evidence Briefs:** pin permitted verified answer blocks, annotate findings, review evidence freshness, publish immutable revisions and export signed verification bundles; retain per-section source verification state.
7. **Notifications / Governance Center:** permission-scoped saved-answer changes, review tasks, contradiction candidates, subscriptions and notification preferences; no confidential excerpts in untrusted notification channels.

**Markdown/text viewer requirements:** show line numbers, exact original line breaks, Raw/Rendered toggle for Markdown, highlighted citation ranges, copy source selection, and safe rendering that never runs embedded scripts or code.

**Identity/tenant UI:** provider-aware login and logout, explicit tenant switcher with session-scoped active tenant, invites and membership management, role/permission editor, group mappings, sessions/devices and revocations, service account/API client management, workspace/collection policy editor, and tenant audit/security screens. **SaaS operator UI** additionally provides onboarding, plan/quota consumption, subscription management and tenant operations; it must be inaccessible/absent in a self-hosted instance unless explicitly configured. Frontend visibility is convenience only; APIs reauthorize every action.

Use generated OpenAPI TypeScript clients, strict compiler checks, discriminated unions, and runtime validation at external boundaries. Avoid directly exposing database or model-provider DTOs as frontend contracts.

### 12.1 Deterministic document/version comparison (adopted)

For `POST /api/v1/queries/compare`, select explicit immutable versions then perform CDM/AST structural alignment **before** semantic retrieval/synthesis. Match using heading ancestry, stable section keys, normalized content hashes and similarity measures, supported by table/figure relationships and provenance. Record `UNCHANGED`, `MODIFIED`, `ADDED`, `REMOVED`, `MOVED`, and `AMBIGUOUS` with original evidence locators. Compare exact cell values, units/scales, figures, captions and deleted clauses, not only the top-K semantically similar paragraphs.

Generate narratives only from verified alignment pairs and resolved evidence. For a change, cite both sides where elements exist; for addition/removal, cite the existing-side element **and** identify the other-side absence as a structural diff result, not a fabricated citation. Ambiguous matches require review or explicit uncertainty. Evaluate added/deleted/modified recall separately and test document restructuring, reordered sections and cross-language revisions.

## 13. External API v1

| Method | Route | Responsibility |
| --- | --- | --- |
| POST | /api/v1/documents/uploads | Create validated upload session |
| POST | /api/v1/documents | Register document, enqueue ingestion |
| GET | /api/v1/documents | List/search accessible documents |
| GET | /api/v1/documents/{id} | Document metadata |
| GET | /api/v1/documents/{id}/versions | Immutable version history |
| GET | /api/v1/versions/{id}/elements | Canonical element tree |
| GET | /api/v1/versions/{id}/views | Available pages, raw/rendered views |
| GET | /api/v1/evidence/{id} | Authorized source locator and context |
| GET | /api/v1/assets/{id}/content | Authorized original or derived asset |
| POST | /api/v1/search | Hybrid/visual/structured evidence search |
| POST | /api/v1/queries | Grounded Q&A with query ID |
| GET | /api/v1/queries/{id}/events | SSE answer streaming and status |
| POST | /api/v1/queries/compare | Version-aware comparison |
| POST | /api/v1/analytics/query | Typed deterministic analytics |
| GET | /api/v1/jobs/{id} | Processing/diagnostic status |
| POST | /api/v1/documents/{id}/reprocess | New extraction or index run |
| PUT | /api/v1/documents/{id}/permissions | ACL administration |
| GET | /api/v1/audit/events | Authorized audit events |
| GET | /api/v1/session | Current principal, active tenant, effective coarse privileges |
| POST | /api/v1/session/tenant | Change active tenant after membership/policy validation |
| POST | /api/v1/session/logout | Revoke app session and optionally initiate IdP logout |
| GET | /api/v1/auth/providers | Allowed login methods / tenant login discovery |
| GET | /api/v1/tenants | List current user's eligible tenants |
| POST | /api/v1/tenants | Provision tenant with authenticated authorized bootstrap |
| GET/PUT | /api/v1/tenants/{id}/settings | Read/update tenant settings |
| POST | /api/v1/tenants/{id}/invitations | Issue tenant invitation |
| GET | /api/v1/tenants/{id}/members | Member listing |
| PUT/DELETE | /api/v1/tenants/{id}/members/{memberId} | Scoped membership changes |
| GET/POST | /api/v1/tenants/{id}/identity-providers | Inspect/create tenant SSO configuration |
| POST | /api/v1/tenants/{id}/domains/verify | Controlled domain verification workflow |
| GET/POST | /api/v1/tenants/{id}/roles | Role templates and tenant custom roles |
| PUT | /api/v1/tenants/{id}/roles/{roleId} | Update assignable permission set |
| POST/DELETE | /api/v1/tenants/{id}/role-assignments | Scoped grant/revoke |
| POST/GET | /api/v1/tenants/{id}/workspaces | Manage tenant workspaces |
| GET/POST | /api/v1/tenants/{id}/service-accounts | Scoped machine credentials |
| GET | /api/v1/tenants/{id}/usage | Tenant-authorized metering |
| GET | /api/v1/tenants/{id}/entitlements | Effective feature/plan limits |
| POST/GET | /api/v1/evidence-briefs | Create/list authorized working briefs |
| POST | /api/v1/evidence-briefs/{id}/blocks | Pin verified query block with provenance |
| POST | /api/v1/evidence-briefs/{id}/publish | Publish immutable verified brief revision |
| POST | /api/v1/evidence-briefs/{id}/exports | Request policy-safe signed bundle/PDF/HTML export |
| GET | /api/v1/evidence-briefs/exports/{exportId}/verify | Authorized manifest verification metadata (offline verifier also available) |
| GET | /api/v1/governance/alerts | Permission-scoped freshness and conflict alerts |
| POST | /api/v1/governance/alerts/{id}/resolve | Record authorized review/revalidation decision |
| GET/POST | /api/v1/reviews | Scoped review tasks/assignment |
| GET | /api/v1/reviews/{id}/source | Authorized reviewer evidence/crop (original if entitled) |
| POST | /api/v1/reviews/{id}/decision | Verify/reject proposed correction with concurrency guard |
| POST | /api/v1/reviews/{id}/publish | Publish approved derived overlay/generation |
| GET/PUT | /api/v1/tenants/{id}/redaction-policies | Authorized sensitivity policy administration |
| GET | /api/v1/knowledge-health | Authorized aggregated quality/gap analytics |
| POST/GET | /api/v1/mcp | Tenant-scoped MCP Streamable HTTP transport (enabled deployments only) |
| /scim/v2 | SCIM 2.0 resource API (optional) | Bearer-scoped provisioning endpoint for configured tenant connection |
| /api/v1/operator/* | SaaS operator-only APIs | Explicitly separated operator identity and authorization boundary |

API requirements: BFF session-cookie + CSRF protection for browser flows, validated OIDC bearer tokens for API/machine access, tenant/resource authorization, opaque identifiers, problem-details errors, pagination, idempotency keys for mutating requests, cancellation, rate limits, request IDs, and streaming error semantics. Enterprise auth callback/provisioning routes have independent security policies, token signing/trust metadata and rate limits. Billing webhooks require signature/idempotency verification. Uploaded files use short-lived upload sessions rather than pushing large binaries through query endpoints.

Search supports query text, optional image asset reference, collection/document scope, mode (auto/text/visual/structured), language, filters, and response preferences. Server determines effective authorized scope regardless of caller-supplied identifiers.

## 14. Identity, tenant isolation and security

This section preserves v1.2 RAG-specific protections; §§21–25 define distribution, full sign-in flows, scoped tenant roles and SaaS lifecycle. Authentication never substitutes for source authorization.

- OIDC SSO (Azure Entra ID or configured provider); users may belong to multiple enterprise tenants with explicit active tenant context.
- Roles: platform admin, tenant admin, collection owner, contributor, reader and auditor; apply least-privilege resource permissions.
- Permissions inherited by retrieval units, table cells, figures, Markdown images and other evidence from the source document/collection, with explicit supported overrides.
- Enforce isolation in PostgreSQL policies/domain authorization, Qdrant prefilters, evidence validation, cache keys, object delivery and streaming.
- Never allow unauthorized material into LLM input, diagnostic logs, comparisons, summaries, reranking, or cached results.
- Quarantine uploads; use malware scanning, file limits, decompression limits, image pixel limits, PDF bomb defenses, safe parser subprocesses and worker isolation.
- Sandbox HTML/Markdown rendering. Block active scripts, unsafe HTML, executable MDX, external fetches, and dangerous URI schemes by default.
- Enforce SSRF protections for HTML images, links and import URLs, including redirects and DNS rebinding.
- Retrieved prompt-injection instructions are data, not commands; tool access is allowlisted and no model-generated arbitrary code execution is permitted.
- Encrypt at rest and in transit, rotate provider credentials, use managed secrets, and configure per-collection provider/data-egress restrictions.
- Audit uploads, access changes, evidence retrieval, exports, corrections, provider calls, deletion and admin operations.
- Define document retention, legal hold, right-to-delete behavior, backup restore, audit retention and secure deletion of object/index projections.

## 15. Operational consistency and failure handling

PostgreSQL transactionally records authoritative metadata and outbox messages; workers use idempotent at-least-once handlers. External object/index writes cannot be in the same ACID transaction, so use **staging generations, durable claim-check references, reconciliations and atomic PostgreSQL publication**. This is an application-level publish/saga protocol, not distributed two-phase commit.

**Publication invariant:** A document version is READY only if its mandatory original asset, CDM elements, locator integrity, required retrieval/index generation, and ACL projection pass validation. An older READY version remains served until a replacement is healthy.

Deletion must block new reads first (tombstone + authorization), then remove projections/assets asynchronously with auditable completion. Revocations apply immediately to query/evidence/asset authorization even before all search projections refresh.

Record parser/model failures by stage; never claim partial OCR/visual coverage is complete. Provide operational reconciliation tasks for orphan assets, stale index entries, unresolved citations and incomplete processing.

### 15.1 Version-pinned search and index-generation cutover (adopted)

1. Ingestion builds an immutable new document version and staging retrieval units under a unique `indexGenerationId`; Qdrant vector writes are tagged with `documentVersionId` and `indexGenerationId`.
2. Reconcile expected retrieval-unit/embedding IDs, page counts, asset/locator integrity and authorization scope against the index manifest. Partially staged generations are never queryable.
3. In **one PostgreSQL transaction**, mark the new version/index generation READY and update the logical document's `activeVersionId` and `activeIndexGenerationId`. Do not update this pointer until validation succeeds.
4. Each query pins the authorized active version/generation set it selected (including cross-document searches) and uses that set for filtering, citations and answer construction. Recheck revocation before disclosure.
5. Superseded generations enter DRAINING. In-flight queries obtain bounded leases, and GC runs only after no active leases, a safe grace period and retention/legal-hold checks. **A fixed 24-hour TTL alone is not a correctness guarantee**; historical source retention depends on enterprise policy.
6. Reconciler detects partial vector writes, orphan assets, stale active references, expired leases and interrupted cutovers; repair/retry must be idempotent.

An evidence citation can only resolve while its retained immutable source exists and caller authorization permits it. For deletion/legal erasure, revoke reads first; expired/removed historical versions must return an explicit `source_unavailable` rather than claiming permanent availability.

### 15.2 Fenced publication on stage and index cutover (v1.2)

The v1.1 READY transaction in §15.1 is necessary but **must be conditional on the still-current stage lease epoch and expected active document pointer**. The winning processing attempt stages its vector generation and immutable object manifests, verifies completeness, and executes one fenced compare-and-swap transaction that (a) proves ownership/epoch is current, (b) confirms the previous `activeVersionId/activeIndexGenerationId` matches the expected pointer or configured conflict resolution rule, and (c) promotes the new READY pointer plus durable outbox event. If any condition fails, do not publish; leave attempt outputs unreachable for reconciliation. This prevents a stale worker or competing reindex from rolling back a newer generation.

Do not bind correctness to wall-clock lock expiry alone: durable monotonic fencing identifies the latest owner. Use attempt-scoped object keys and conditional writes so stale workers cannot overwrite stage artifacts. GC must respect active query leases, outbox redelivery, retention and legal holds. Test stale worker publication after lease loss and simultaneous competing revisions.

## 16. Evaluation and acceptance gates

Build a labeled dataset of 300–500 representative multilingual queries, spanning PDFs, HTML, figures, engineering drawings, Markdown, plain text, scanned tables, cross-document comparisons, exact numeric questions, and forbidden-access probes. Include gold evidence locators and answer requirements.

| Dimension | Measure | Initial acceptance target (on defined supported test corpus) |
| --- | --- | --- |
| Clean text extraction | Character/word recognition | >= 98% |
| Table structure | Cell/header/merged-span F1 | >= 95% |
| Figure detection | Recall | >= 95% |
| Evidence retrieval | Recall@20 | >= 95% |
| Citation integrity | Existing authorized exact source references | 100% |
| Answer grounding | Supported factual claims | >= 98% |
| Calculations | Correct outputs for validated inputs, including inherited scale | 100% |
| Isolation | Cross-tenant/document unauthorized leaks | 0 |
| Publication consistency | Missing mandatory ready assets/indexes | 0 |
| OCR batching/replay | Missing, duplicated or out-of-order source page references after retry | 0 |
| Claim-check durability | Messages with oversized content payloads or missing referenced artifacts on redelivery | 0 |
| Permissioned retrieval | Unauthorized material reaching LLM, cache or streamed blocks | 0 |
| Version comparison | Added/removed/modified element recall and bilateral provenance | Measured per document class; acceptance thresholds set by gold suite |
| Answer streaming | Unverified factual blocks visible as final answer | 0 |
| Markdown/text citations | Correct original line/span resolution | 100% |
| Fencing | Stale worker stage/result/READY writes accepted after lease takeover | 0 |
| Fair scheduling | Tenant bulk traffic starving an unrelated permitted small ingestion | 0 in specified load-test window |
| OCR local admission | Corrupt/hidden native text escaping diagnostic escalation | Track false negatives by script/PDF class; thresholds calibrated |
| Header hierarchy | Lost stacked row/column headers or unverified-scale aggregate | 0 in gold suite |
| Permission scope planning | Unauthorized context and unbounded/truncated filter lists | 0; recall/latency measured at broad and narrow scopes |
| Verified streaming | Dangling block dependencies reaching final UI | 0 |
| Analytical sandbox | Unbounded query resource usage / timed-out query reported as empty result | 0 under fault tests |
| Saved citation freshness | Changed cited source missed after READY publication / invalid old citation falsely labeled corrupt | 0 in gold regression; measured detection lag |
| Contradiction governance | False confirmed contradictions or unapproved cross-document precedence | 0 unreviewed final conflicts; measure candidate precision/recall |
| Evidence Brief integrity | Tampered manifest/evidence/calculation accepted as valid offline | 0; unsigned/unverifiable elements marked explicitly |
| Reviewer overlays | Unapproved/conflicting/stale correction becomes active | 0; optimistic concurrency + separation-of-duty tests |
| Sensitivity control | Unauthorized plain text, raster pixels, PDF text layers, snippets, vectors or tool responses leaked | 0 in supported redaction threat-model corpus; unresolved => deny |
| MCP integration | Unauthorized tool result; cross-tenant client, confused deputy or token-audience substitution | 0 |
| Knowledge quality | Query text/rare-topic leak or mistaken definite missing-manual diagnosis | 0 tested leaks; classifier uncertainty labeled |

These are **targets, not measured performance or universal guarantees**. Evaluate per language, format and document difficulty; publish failure/abstention rates separately. Use golden files for source-offset reconstruction (especially Markdown CRLF, Unicode, BOM, tabs, footnotes, tables, code fences and relative links).

Automated resilience tests: duplicate leases and monotonic fencing with paused/stale writers; outbox fair-share under 5,000 bulk uploads; cross-replica 429/Retry-After coordination; multi-header table and scale inheritance; rejected middle-answer block with dependent conclusion; native PDF hidden OCR layer/glyph and visual-text mismatch; cardinality-aware Qdrant filtering with grants/denies; analytics timeout/memory/row limits; worker crash at each stage; duplicate delivery; oversized CDM (> broker limit) through claim-check; artifact retry/GC race; same-file concurrent upload; malformed document; 150-page mixed PDF page splitting and stitched multi-page tables; OCR 429/413/timeouts and potential duplicate billing; unavailable embedding/model provider; revoked permissions mid-query; permission-filtered recall; staged index cutover and in-flight leases; missing original asset; concurrent reprocess; mixed scale financial tables; re-ordered/removed version sections; tombstone during active streaming; unsupported claim suppression; HTML/Markdown XSS/prompt-injection payloads.

Track ingestion throughput and p95 latency, source coverage, bad/missing asset references, retrieval Recall@K/nDCG, claim support, citation resolution, per-query tokens/cost, queue age, retries and tenant-level errors. Establish latency SLOs only after realistic load tests.

### 16.1 Performance experiments — decisions, not guaranteed numbers

| Optimization | Baseline and alternative | Required observations before adoption |
| --- | --- | --- |
| **Local ONNX/heuristic intent routing** | Existing cloud/configurable intent routing versus lightweight local rules or ONNX | Per-language intent F1, uncertainty/fallback, cold/warm p50/p95, CPU/memory, overall query latency and total provider cost |
| **Local ONNX semantic support/NLI** | Existing configurable grounded verification versus locally hosted candidate verifier | Factual entailment precision/recall, multilingual and long-document support, numeric/diagram failure modes, abstention rates, cold/warm p95, false-positive harm; **never** treat model score as formal proof |
| **PostgreSQL Binary COPY** | Transactional batched INSERT/ORM versus validated/staged `NpgsqlBinaryImporter` or psycopg `COPY` | Throughput, WAL/I/O, indexing/constraint cost, transactional rollback/retry and correctness of tenant IDs, element relations and provenance |
| **Multiprocess PDF analysis/rendering** | Sequential/native batch processing versus **independent-process** document/page work | Pages/sec, peak memory, startup cost, ordering/offset correctness, worker crash recovery and scaling; **do not share PyMuPDF document objects between threads** |

No "<15 ms", "15–30 ms", "<80 ms", "10x" or cache-hit target from the external review is an architecture guarantee. Adopt a faster implementation only if it preserves evaluation gates for evidence quality, authorization, source offset fidelity, reproducibility and recovery. Running local ONNX is optional and does not change the chosen default cloud AI strategy.

## 17. Capacity and hybrid deployment

Initial planning size: **10,000 documents**; actual compute and storage depend on pages, images, tables, OCR proportion, scanned languages and generated retrieval units. A hypothetical 100 pages/document gives approximately 1 million pages; this is a sizing scenario, not an input guarantee.

**Development / self-hosted:** Docker Compose with ASP.NET API, React app, Python workers, PostgreSQL, Qdrant, MinIO, RabbitMQ, local authentication, and observability. Provider credentials supplied by secrets/environment injection; wizard-managed credentials use the separately initialized encrypted secrets store (§32), not plaintext database/configuration rows. The same application supports single-tenant default and optional multiple tenants, without a vendor SaaS account or network license heartbeat.

**Managed SaaS production:** Azure Container Apps or Kubernetes for API/workers, managed PostgreSQL, Blob storage, Service Bus and appropriate managed/hosted vector infrastructure. Cloud OCR uses a **Mistral deployment hosted in Microsoft Foundry**, with a separately configured **Azure Document Intelligence in Foundry Tools** resource as specialist fallback. Use horizontal worker queues split by resource class (native extraction, OCR, vision, indexing); separately throttle each Azure provider's expensive AI calls and track provider/region-specific limits.

Run ingestion asynchronously with backpressure, quotas, provider retry budgeting, model routing and per-tenant usage reporting. Split long PDFs by *actual deployed endpoint capability*, checkpoint at page granularity, and enforce provider-specific budgets. Measure monthly cost by model/region/tenant, including retries, vision enrichment and embeddings, rather than assuming a fixed OCR savings percentage. Keep index/asset formats portable between local and cloud adapters.

### 17.1 Deployment modes, isolation and self-hosted operability (v1.3)

**Single-source core:** built artifacts and migrations are common. Distribution is configured using deployment capabilities (self-hosted, enterprise-private, SaaS) rather than distinct product forks. Self-hosted must initialize a deployment owner and first tenant locally; built-in identity, configuration, authorization, migrations and backups work without vendor connectivity. Azure inference still requires outbound access unless alternative locally hosted providers are explicitly installed.

**Managed SaaS:** deployment-wide operator control plane provisions isolated tenants, manages subscription entitlements/quotas, applies resource placement and provides tenant-scoped metering. Support three isolation policies over time: shared app/shared data with enforced tenant scopes; shared app/dedicated data and index; dedicated app + workers + data. Only the **shared** policy is an initial implemented target. Dedicated placements are future capabilities, not implied by the shared topology.

**Private/on-prem:** distribute OCI images, Docker Compose bundles, secrets templates, documented reverse-proxy/TLS/database setup, migrations and health checks; provide Helm charts only when their lifecycle can be supported. Publish a clear compatibility matrix (versions, DB requirements, document models, supported IdPs and inference provider egress). Provide signed artifacts, migration/rollback documentation and reproducible builds. BYO Azure Foundry/provider connections remain opt-in and billed according to the customer's chosen arrangement.

Never couple ingestion correctness or user identity to a SaaS billing provider; plan enforcement can pause billable new work but may not erase customer evidence or silently bypass access controls.

## 18. Implementation roadmap

### I1 — Platform foundations
- **MEK/key protection prerequisite:** provide a pluggable `IKeyProtectionProvider`, encrypted integration secret contract (per-secret DEKs and AES-256-GCM), deployment-root/tenant-wrapping key versions, provider secret access audit and a **recovery-capable protector** before enabling S0 Integration Setup Center writes. TPM is optional; SaaS defaults to Key Vault/KMS. See §32 and [Slicing strategy](Slicing%20strategy.md).
- Add event contract placeholders for immutable saved-answer dependencies, review provenance, sensitivity classification and auditable governance notifications; machine-readable permission catalog includes v1.4 actions, with default-deny policies. The governance UI and remote MCP server are NOT I1 dependencies.
- Add **deployment mode and capability boundaries** (OSS self-hosted and hosted SaaS share core); automated local bootstrap must not require a vendor SaaS control-plane call.
- Implement **Tenant → Workspace → Collection → Document** hierarchy, immutable tenant ownership, tenant switcher and tenant-aware DB/RLS, object, queue and cache contracts.
- Implement **ASP.NET Core Identity local accounts + OIDC federation** (Entra ID/External ID and generic enterprise OIDC), secure BFF session cookie, tenant invitation/registration, verified issuer/subject identity mapping, local admin recovery and revocation.
- Add machine-readable permission catalog, standard scoped role templates, PostgreSQL authorization resolver, fine-grained default-deny policies, and tenant ACL/security tests (see §24).
- Add initial tenant lifecycle, quota and feature entitlement interfaces; self-hosted uses local entitlements/default configuration, not a remote SaaS dependency.
- Repository structure, architecture decision records, contracts, Docker stack and CI.
- OIDC tenant-aware auth, collection/document metadata, upload sessions, immutable originals and versioning.
- Durable queue/outbox using **claim-check-only messages (target <=16 KiB)** with content-hashed immutable artifacts, **database-enforced epoch fencing**, idempotency, retention and replay.
- Add **tenant-fair priority outbox scheduling** with SKIP LOCKED claims, recovery and bounded starvation; establish shared provider budgets/throttling interfaces.
- Add `IAuthorizationPolicyResolver` and PostgreSQL-backed tenant/collection/document checks; design structural scope filtering for future Qdrant indexes. OpenFGA remains optional.
- Record active version/index generation, staged/durable publication and revocation-first deletion contracts.
- **Gate:** repeated delivery and worker crashes preserve artifacts; expired Worker A cannot overwrite/publish after Worker B takes over; large extraction payloads never enter the broker; competing outbox tenants get fair service; **built-in/OIDC login, tenant switching, scoped role checks, revoked sessions and cross-tenant RAG isolation** hold under tests, including a completely local self-hosted bootstrap. S0 wizard secrets cannot be saved until key protection is active; a failed/corrupt protector fails closed without reopening Superadmin bootstrap.

### I2 — Canonical extraction and provenance
- Implement tenant-scoped reviewer task creation for PARTIAL / UNVERIFIED_* and extraction quality failures, immutable human correction overlays and approval/publish gates without re-OCR. Add sensitivity annotation hooks and conservative restricted-content admission; do not expose partly masked content until full safe-rendition tests pass.
- Enforce tenant-scoped ingestion quotas, tenant-assigned OCR/model policy, provider egress configuration and per-tenant processing audit/usage events.
- CDM v1, typed element relationships, exact evidence locators and immutable source-derived assets.
- Configure Foundry Mistral OCR and Azure Document Intelligence separately; verify deployed endpoint identity, quotas, auth, page/file limits, request shape and pricing.
- Implement **adaptive native/standard/complex/specialist OCR routing**, including native ToUnicode/glyph and visual-text integrity diagnostics; deterministic page-window batching, **cloud page-level vs local batch/document-level checkpoints**, selective retry/fallback and cross-window table continuation checks.
- Coordinate cloud OCR rate limits and backoff across worker replicas by deployment/region and tenant.
- PDF native/OCR/table/figure parsing with quality/coverage manifests; Markdown AST/line spans and TXT encoding/newline offsets.
- Optional hot line-offset cache (backed by immutable offset maps), original source viewers and extraction inspector.
- **Gate:** a 150-page mixed PDF survives batch failures with correct global page citations and cross-page tables; broken/hidden text layers escalate correctly; local parsing does not require gratuitous per-page DB writes; shared throttles prevent coordinated retry storms; costs and routing remain observable.

### I3 — Grounded hybrid RAG
- Persist evidence dependency edges for pinned/saved verified answers; record granular retrieval failures/abstentions and authorization-aware cause codes. Ensure any redacted retrieval context is sanitized BEFORE LLM input and all answer/citation delivery.
- Enforce workspace/collection/document ACLs end to end for text, tables, figures, citations, query history, exports and service accounts; add permission-revocation tests.
- Retrieval units, text embeddings, lexical index, Qdrant and fused/reranked retrieval.
- Authority-based permission resolution, **cardinality-aware Qdrant plans** for collection-wide grants, explicit overrides and bounded document scopes; access checks before LLM context and again at evidence/asset delivery; version-pinned read paths.
- Citation and semantic-support validation; **verified-block SSE with dependency-closed atomic groups**, immediate progress events and no unverified factual token display.
- Validate staged index generations against manifests and atomically promote READY with drain leases and reconciler.
- **Gate:** citation/grounding tests pass; no leaked/dangling dependent block survives a rejected middle block; permission scopes with many document IDs retain tested recall without leaking access; revoked access fails closed and no partial indexes are queryable.

### I4 — Full multimodal + HTML
- Add safe server-generated redacted PDF/image derivatives and policy-aware text/HTML/figure citation rendering; validate text layers, original URLs, caches, vector descriptions and image crops cannot leak masked content.
- Maintain tenant/evidence ownership across image crops, OCR assets, multimodal indexes and rendered HTML; prove no asset URL/cached-result leakage across tenants.
- Standalone image parsing, OCR, vision descriptions, true cloud multimodal embeddings and image-image queries.
- HTML parsing, sanitized DOM snapshots and optional isolated browser rendering; original figure/region viewer overlays.
- **Benchmark only:** ColPali/late-interaction MaxSim with candidate prefetch, compact multi-vectors and source-aligned regions; adopt if measurable advantage over default retrieval.
- **Gate:** visual queries display authorized originals and correct source locators; HTML/Markdown are safe to render; report per-modality recall/latency/cost.

### I5 — Tables, comparisons, multilingual
- Build publishable immutable Evidence Brief revisions with offline-verifiable signed artifact manifests; trigger READY-driven structural citation freshness checks and reviewed conflict candidates. Extend calculation inputs and signed bundles with exact provenance, review decisions and effective-date policies.
- Enforce permission-scoped comparisons, deterministic analytics and authorized export actions; tenant policy must govern both source and derived output.
- Cell-level typed extraction with **multi-level row/column header paths and source-element relations**, caption/header unit and magnitude inheritance; `UNVERIFIED_SCALE`/`UNVERIFIED_HEADER` handling, exact NUMERIC calculations and exclusion provenance.
- Execute analytics with a **dedicated read-only role/transaction, validated query plan and bounded statement/memory/row budgets**.
- **Structural CDM/AST alignment before LLM synthesis** for cross-version comparisons, including added/deleted/moved clauses and bilateral source evidence.
- Language-aware indexing, code and Markdown quality improvements.
- **Gate:** all gold-suite verified calculations are exact across stacked FY/quarter/audit headers; uncertain scales cannot silently corrupt totals; expensive analytics fail explicitly within budgets; diff classes and citations pass evaluation.

### I6 — Enterprise readiness
- Validate key rotation/rewrap, restore onto replacement hardware, TPM clearing, SaaS multi-node access, compromised-key response, retention/crypto-erasure and tested tenant-independent key policy. Document alternative secret re-entry when recovery is not possible, and prohibit silent protector downgrade.
- Expose scoped READ-ONLY remote MCP Streamable HTTP endpoint behind OAuth/resource audience validation and full per-tool RBAC. Add configurable notification delivery, privacy-preserving Knowledge Health dashboards, governance event observability, scheduled revalidation, export signature verification, retention/key rotation and cross-tenant/redaction penetration tests.
- Harden managed SaaS: tenant provisioning/suspension/termination, plan entitlements, audit-ready usage metering, rate limits, platform-operator separation, backup/restore and SaaS data lifecycle.
- Add enterprise federation extensions **SAML 2.0, AD DS/LDAPS/federation, Okta, Keycloak and other OIDC**, plus SCIM 2.0, controlled JIT, SSO enforcement and directory-group mapping based on customer priority.
- Document signed OSS releases, install/upgrade/migrate/export contracts and tenant isolation tiers; implement private deployment upgrade flow and customer-owned AI credentials as policy allows.
- Enterprise policies, audit/retention, model egress controls, backups/restore, production Azure deployment and SLOs.
- Load tests for permission-scope filters, vector footprint, ingestion throughput, OCR cost, cold/hot query latency and massive document tables.
- **Benchmark-driven ADRs:** OpenFGA ReBAC, PostgreSQL partitioning, vector quantization/oversampling/rescoring, hosted late-interaction models, selective prompt caching and model tiers. **Separately benchmark local ONNX routing/NLI, bulk PostgreSQL COPY and multiprocess PDF rendering** against quality, correctness and cost baselines. Adopt only where benefit outweighs complexity.
- Recovery/security testing, operational dashboards and release gates.
- **Gate:** measured isolation, extraction, retrieval, citation, availability, cost and disaster-recovery targets pass.

## 19. Repository structure (target)

~~~text
rag-pipeline/
├── docs/
│   ├── architecture.md
│   ├── adr/
│   ├── api/
│   ├── contracts/
│   └── runbooks/
├── apps/
│   ├── web/                        # React + strict TypeScript
│   └── api/                        # ASP.NET Core modular monolith + BFF/auth
├── distribution/                  # Planned self-hosted / private / SaaS packaging
│   ├── compose/
│   ├── helm/
│   └── release/
├── src/
│   ├── BuildingBlocks/
│   ├── IdentityAccess/
│   ├── TenantManagement/
│   ├── SubscriptionsAndMetering/
│   ├── DeploymentCapabilities/
│   ├── DocumentManagement/
│   ├── Ingestion/
│   ├── EvidenceCatalog/
│   ├── Search/
│   ├── QueryEngine/
│   ├── TableAnalytics/
│   ├── EvidenceGovernance/
│   ├── EvidencePublishing/
│   ├── HumanReview/
│   ├── SensitivityPolicy/
│   ├── IntegrationsMcp/
│   └── KnowledgeQuality/
├── workers/
│   ├── parser/
│   │   ├── pdf/
│   │   ├── html/
│   │   ├── image/
│   │   ├── markdown/
│   │   ├── text/
│   │   └── normalization/
│   ├── indexing/
│   └── evaluation/
├── contracts/
│   ├── openapi/
│   ├── cdm/
│   ├── permissions/              # Machine-readable permission catalogue
│   ├── governance/               # Event / freshness / review contracts
│   ├── exports/                  # Signed verification manifest schemas
│   ├── mcp/                      # Tool descriptions + schema, optional
│   └── events/
├── tests/
│   ├── unit/
│   ├── integration/
│   ├── contract/
│   ├── security/
│   └── evaluation/
└── infrastructure/
    ├── docker/
    └── azure/
~~~

The directory tree is **proposed**, not a claim that the project is already implemented.

## 20. Key architecture decisions and future extensions

| ADR candidate | Decision |
| --- | --- |
| ADR-001 | PostgreSQL/CDM/object storage authoritative; vector index is a projection |
| ADR-002 | Modular ASP.NET Core application plus independent Python workers |
| ADR-003 | Immutable document versions, typed evidence locators and stable citation resolution |
| ADR-004 | Separate lexical, text-semantic, multimodal and structured retrieval |
| ADR-005 | Restricted typed calculation plans and deterministic execution |
| ADR-006 | Tenant isolation and fail-closed authorization through the entire query path |
| ADR-007 | Markdown and plain text are native sources, not downgraded to generic unlocated chunks |
| ADR-008 | Staged index generations with atomic READY cutover and recovery reconciliation |
| ADR-009 | Cloud AI adapter with data classification/egress controls and model-version tracking |
| ADR-010 | Azure Foundry-hosted Mistral is the primary cloud OCR family; Document Intelligence in Foundry Tools is an independent specialized fallback adapter |
| ADR-011 | Claim-check-only durable messaging, small envelopes, immutable artifacts and lease/retention-aware GC |
| ADR-012 | Adaptive and capability-checked page OCR routing, bounded batching, page checkpoints and selective fallback |
| ADR-013 | Source-scale/units validation for normalized table calculations, with explicit partial results |
| ADR-014 | Structural CDM/AST document comparison before evidence-grounded synthesis |
| ADR-015 | Version/generation-pinned queries, staged indexes, atomic READY publication and drain leases |
| ADR-016 | Verify factual answer blocks before streaming; SSE progress does not imply answer validation |
| ADR-017 | Authorization abstraction; benchmark OpenFGA; no mutable principal ACL lists in vector payloads |
| ADR-018 | Benchmark late-interaction visual search, quantization, partitioning and caching before mandatory adoption |
| ADR-019 | Monotonic database lease fencing, guarded writes and conditional staged publication |
| ADR-020 | Tenant-fair outbox scheduling and centrally coordinated provider quotas/backoff |
| ADR-021 | Native PDF text-layer glyph/Unicode/visual integrity admission; calibrated escalation |
| ADR-022 | Cloud OCR/vision per-page checkpoints; native deterministic work batch/document checkpoints |
| ADR-023 | Cardinality-aware, override-correct Qdrant scope planning and active-version enforcement |
| ADR-024 | Self-contained answer blocks, dependency graph and atomic verified group publication |
| ADR-025 | Multi-level row/column header evidence and derived typed dimensions for analytics |
| ADR-026 | Read-only, cost/resource-bounded deterministic table query execution |
| ADR-027 | ONNX verification/routing, COPY and multiprocess PDF work are benchmark-gated alternatives |
| ADR-028 | One OSS product core for self-hosted, enterprise-private and managed SaaS deployments; no self-hosted SaaS-control-plane dependency |
| ADR-029 | Built-in ASP.NET Core Identity; optional OpenIddict; OIDC-first tenant SSO with BFF sessions, controlled refresh/grant revocation |
| ADR-030 | Entra ID and Entra External ID supported separately; SAML and AD DS/LDAPS as enterprise federation adapters |
| ADR-031 | Deployment → Tenant → Workspace → Collection → Document → Version → Evidence ownership and tenant membership model |
| ADR-032 | Machine-readable permission catalog, role templates/custom roles, scoped assignments, resource ACL and conditional policies |
| ADR-033 | Service-account and API-client workload identities separate from employee login sessions |
| ADR-034 | SaaS operator control plane, subscription entitlements and metering separated from user authorization and self-hosted core |
| ADR-035 | Shared SaaS data first; dedicated data/deployment placement later behind explicit isolation contracts |
| ADR-036 | Source/license choice (Apache 2.0 vs AGPLv3 vs properly managed dual licensing) remains open for owner approval before public release |
| ADR-037 | Secure identity provisioning, SCIM/group mapping, SSO enforcement, verified domains and emergency recovery are phased capabilities |
| ADR-038 | Revalidate saved evidence through version-pinned reverse dependencies; distinguish supersession from contradiction |
| ADR-039 | Editable Evidence Briefs publish immutable revisions and portable signed manifests; integrity is not semantic truth |
| ADR-040 | Read-only MCP tool endpoint shares delegated/service-account authorization, verified evidence and redaction policies |
| ADR-041 | Human review produces append-only, authorized overlays and staged re-publication; dual control for high-impact corrections |
| ADR-042 | Safe server-side redacted renditions and policy-scoped search/index/LLM context; black visual overlays alone are forbidden |
| ADR-043 | Tenant-private knowledge quality analytics distinguish missing knowledge from quality, permission or pipeline limitations |
| ADR-044 | Integration secrets use authenticated per-secret DEK envelope encryption; a pluggable protector wraps tenant KEKs/MEKs, not a monolithic TPM data store |
| ADR-045 | TPM2 (Windows CNG / Linux TPM2-TSS) is an optional on-prem protector; SaaS uses shared-access KMS/Vault and a separately controlled recovery/migration path |
| ADR-046 | Key initialization, activation, envelope metadata, rewrap, compromise handling, escrow/recovery, disable and destruction follow versioned, audited lifecycle gates |
| ADR-047 | Docker app containers avoid broad host TPM device access; a restricted host key agent/IPC is an optional deployment capability, not a SaaS dependency |

Later extensions can add DOCX/PPTX/XLSX, Git repositories, remote crawlers, incremental sync, human annotation workflows and knowledge-graph features without changing the core provenance contracts.

### 20.1 Review disposition (v1.1)

| Review proposal | Decision | Reason / guardrail |
| --- | --- | --- |
| Claim-check envelopes and immutable stage artifacts | **Adopt in I1** | Broker size independence and at-least-once replay safety |
| Page-window OCR + page checkpoints | **Adopt in I2** | Capability-driven limits and selective reruns |
| Four-route page complexity classification | **Adopt as policy in I2** | No fixed model generation, pricing or workload distribution |
| Inherited table scale and units | **Adopt in I5** | Prevent mathematically exact but semantically incorrect totals |
| Structural version comparison | **Adopt in I5** | Detect deletions/changes missed by top-K search |
| Version-pinned index cutover + safe GC | **Adopt in I1/I3** | Immutable consistency under crashes and concurrent queries |
| OpenFGA | **Evaluate in I6** | Provider abstraction now; benchmark scale/consistency before installing |
| ColPali/MaxSim, MUVERA-like prefetch | **Evaluate in I4** | Optional advanced index after real visual retrieval benchmarks |
| Binary/vector quantization | **Evaluate in I6** | Model-specific recall/latency/RAM experiments required |
| Speculative streaming of unverified claims | **Reject as default** | Stream verified blocks and non-factual progress instead |
| Automatic PostgreSQL tenant partitioning | **Defer pending query plans** | Avoid premature partition explosion and migration complexity |
| Claimed OCR price tiers and fixed 67–87% savings | **Do not assume** | Region/model availability, rate cards and corpus distribution must be measured |
| Guaranteed 80–90% prompt-cache hits | **Do not assume** | Optimize stable prompts only if evidence freshness and relevance are retained |

This disposition is an engineering decision informed by the supplied 19-page review; it does not claim that its numerical benchmarks or deployment observations have been reproduced in this repository.

### 20.2 Final review disposition (v1.2)

All nine user-selected final review refinements are incorporated into the base architecture and milestones:

| Review item | Disposition | Enforcement milestone |
| --- | --- | --- |
| Monotonic fencing + conditional publication | **Mandatory** | I1; index publication in I3 |
| Tenant-fair outbox + shared provider throttling | **Mandatory** | I1 / I2 |
| Native PDF text-layer integrity checks | **Mandatory quality gate; calibrated thresholds** | I2 |
| Cloud page-level vs local batch-level checkpoints | **Mandatory differentiated checkpoint policy** | I2 |
| Cardinality-aware permission-safe Qdrant plans | **Mandatory, benchmarked thresholds** | I3 |
| Block dependencies and atomic grounding | **Mandatory verified streaming correctness** | I3 |
| Multi-level row/column header provenance | **Mandatory CDM/schema support** | I2 schema; I5 analytics |
| Read-only resource-bounded analytics | **Mandatory** | I5 |
| ONNX, COPY, multiprocess PDF alternatives | **Evaluation required; adoption conditional** | I6, with earlier representative microbenchmarks |

No existing v1.1 architecture decisions are revoked. Previously deferred OpenFGA, ColPali/MUVERA, quantization and partitioning remain optional, benchmark-driven. Review-specific latency/throughput and price figures are **not** production SLOs or guaranteed improvements.

## 21. Product distributions, open source and licensing (v1.3)

### 21.1 One product, three distribution targets

| Distribution | Deployment owner | Platform tenancy | Identity | Connectivity to our SaaS control plane |
| --- | --- | --- | --- | --- |
| Community OSS self-hosted | End user; Docker/VM, optional Kubernetes | Single tenant default; multiple tenants optionally enabled | Built-in accounts + configured external IdPs | **Not required** for install, login, document access, ingestion or migrations |
| Enterprise private/on-premises | Customer IT/security | One organization or multiple departmental tenants | OIDC, Entra, SAML/AD federation as installed | **Not required** |
| Managed multi-tenant SaaS | Service operator | Many isolated customer tenants in shared infrastructure initially | Built-in + tenant-controlled social/enterprise sign-in | Hosted control plane is internal to the service, not a dependency for self-hosted copies |

The **same** RAG API, CDM, workers, policy/permission contracts and migration sources apply to every distribution. Deployment-specific features are capability/entitlement flags with explicit server-side enforcement, not divergent business logic. SaaS-only operator functions are disabled/absent from community installations. Full offline/air-gapped **operation of the application** must be distinguished from processing through cloud AI providers, which needs external connectivity unless replaced with approved local backends.

### 21.2 Commercialization and the open-source license — decision pending

| Strategy | Benefit | Trade-off / legal consideration |
| --- | --- | --- |
| **Apache License 2.0** | Permissive enterprise adoption and redistribution | Other parties may offer commercial managed forks consistent with the license |
| **AGPLv3** | Network-use source obligations for covered modified versions | Some enterprises prohibit or avoid AGPL; legal assessment necessary |
| **Dual licensing** | Open-source plus separate negotiated commercial license | Requires contributor copyright rights/agreements and consistent third-party dependency compliance |

**No license is selected by this document**: repository owner must approve license strategy, trademark policy and whether paid value is hosting/support or proprietary modules. Do not assume dual licensing is available without required rights from contributors. Define community vs paid feature boundaries transparently and avoid describing a source-available/non-OSI license as open source.

Public-release requirements: root LICENSE and NOTICE as appropriate, contribution guide and DCO/CLA decision, code of conduct, security disclosure policy, code owners, reproducible tagged releases, dependency license inventory, SBOM, signed images/packages, vulnerability/patch response, documented data formats, stable upgrade/migration path and compatibility policy. Respect AI model, OCR and other third-party provider licensing separately: publishing the application does not make Azure/Mistral processing free or redistributable.

### 21.3 Self-hosted product requirements

- Self-contained local bootstrap creates a deployment administrator and first tenant through a single-use, audited setup ceremony. No default shared password; bootstrap secret is single-use and protected.
- External billing, license heartbeat, telemetry, SaaS login and SaaS DNS must **not** be required for core startup/maintenance. Make opt-in update checking/usage reporting explicit with privacy documentation.
- Local identity, tenant settings, backup/export, audit, data deletion and migrations work offline; network access is needed only for configured external integrations/model calls.
- Use installable OCI images, Docker Compose baseline, documented TLS/reverse-proxy, storage/database backups, image signing/verification and install/upgrade/rollback testing. Consider Helm for supported Kubernetes deployments.
- Enterprise portability: export original documents and their versioned CDM/evidence/provenance, policies and compatible metadata to a documented interchange manifest; exclude secrets and private refresh tokens. Rebuild search indexes after import, mapping tenant/issuer identifiers and verifying hashes and permissions. Import requires a deliberate authorization and conflict-resolution process.

## 22. Identity, authentication and federation (v1.3)

**Authentication proves an identity; it does not confer membership or document access.** Provider claims (email, groups, domain, roles) are untrusted for platform authorization until verified and mapped through tenant-controlled policy. Never merge accounts only because email matches. Internal subject records are separate from external issuer/subject identities.

### 22.1 Supported providers and phasing

| Source | Preferred protocol/integration | Delivery |
| --- | --- | --- |
| Built-in user/password, email verification, recovery | ASP.NET Core Identity with vetted password hashing; optional OpenIddict authorization server if first-party token issuance required | **I1 baseline** |
| Microsoft Entra ID (workforce) | Tenant-configured OIDC Authorization Code + PKCE | **I1 baseline** |
| Microsoft Entra External ID (customers/external identities) | Configured, separately validated OIDC deployment | **I1 baseline integration target** |
| Generic enterprise OIDC | Discovery, issuer validation, authorization code + PKCE | **I1 baseline** |
| Google / Google Workspace | OIDC sign-in with verified claims and tenant-controlled domain policy | P1 |
| Okta, Auth0, Keycloak, Ping and similar | Prefer OIDC; optional SAML integration | P1 |
| Generic SAML 2.0 | Metadata/certificate validated service-provider adapter | P1 |
| Active Directory Domain Services (on-premises) | Prefer federation through AD FS/Entra/Keycloak OIDC or SAML; secure LDAP/LDAPS directory adapter where needed | P1 on-prem |
| GitHub and other social IdPs | Provider-specific OAuth login adapter with explicit verified identity mapping; do not assume raw OAuth grants are OIDC | P2 |

Entra External ID is not the same as on-premises AD DS. When AD DS is required, prefer federation; any LDAP bind/search must use configured TLS/certificate verification, least-privilege directory accounts and carefully scoped group discovery. IdP settings (issuer, audiences, client IDs, signing keys, metadata endpoints and tenant policy) are per deployment or tenant as appropriate, stored with sensitive values only in a secure secret store.

### 22.2 Built-in Auth Service and browser session architecture

ASP.NET Core Identity stores local accounts, password recovery/MFA configuration and device/session records. Use OpenIddict only where the deployment actually needs an OAuth/OIDC token issuer for API clients or other first-party applications; never invent custom token cryptography. Browser authentication uses **BFF**: HttpOnly Secure cookies, explicit SameSite/CSRF and origin handling, rotation and logout/revocation. Provider access/refresh tokens stay encrypted server-side (not localStorage or sessionStorage). External OIDC access/refresh grants and app sessions are distinct lifecycles; provider refresh tokens might be unavailable and must **not** be assumed.

Built-in login security: strong password policy and breached-password protection where configured, account verification, configurable registration/invitation, rate limiting/lockout, passkeys/WebAuthn and TOTP MFA, recovery codes, audited password reset, high-risk reauthentication, device/session listing, session invalidation and protected emergency administrator recovery. Enterprise policy can require MFA and IdP-exclusive SSO, with narrowly controlled local break-glass accounts.

**Refresh token rules:** Request offline access only when needed and allowed, use supported rotation/sender-constraining and replay detection for tokens the platform issues, bind grants to session/client/issuer, store hashed or protected grants as appropriate, revoke token families after suspected replay, and rotate signing keys/JWKS with overlap and clear expiration. Enforce token audience/issuer/nonce/state and PKCE checks, logout/backchannel event handling where supported, and account deactivation revocation on all active application sessions. Do not return provider refresh tokens to browsers.

### 22.3 Tenant-aware login and identity lifecycle

- Support tenant-specific IdP discovery by explicit tenant selection and **verified** email domain, without disclosing which private tenant accounts exist. Domain ownership verification precedes JIT membership; email suffix alone is insufficient.
- Users may link several external identities only after proving control of both the currently logged-in account and the new identity, with reauthentication/audit. Do not auto-link solely on an email claim.
- Support invitation-based membership, approved just-in-time provisioning, enforceable SSO-only policy, account linking/unlinking, multiple IdPs per tenant and per-tenant provider precedence (initial release may expose a subset).
- Use **SCIM 2.0** for automatic user/group provisioning and deprovisioning through tenant-scoped credentials, idempotent create/update/delete semantics and explicit mapping. Suspension/deprovisioning must revoke platform access, sessions and memberships according to configured authoritative policy.
- Map IdP groups to platform role assignments with strict tenant configuration, stable external group IDs, scope rules and membership sync. Do not trust arbitrary group claim text to grant Tenant Owner or Platform Operator.
- Break-glass access is rare, limited, protected by strong MFA and security notification, cannot silently bypass document classifications, and is audited.

### 22.4 Machine and workload identities

Tenant-scoped **service accounts** and OAuth API clients are distinct from people. Support scoped client credentials or strongly hashed/API tokens where appropriate, key/credential rotation, expiration/revocation, optional mTLS/proof-of-possession for high-assurance cases, separate usage budgets, rate limiting, and audit. Limit each credential to specific tenant/workspace/collection actions. Internal workers use deployment workload identities and per-job tenant context; they never inherit a human refresh token or a global content browsing permission.

## 23. Multi-tenancy and resource isolation (v1.3)

### 23.1 Ownership and membership

~~~text
Deployment (self-hosted or managed)
  └── Tenant (company/customer organization)
      ├── Memberships / Tenant identity-provider bindings
      ├── Role definitions / scoped role assignments
      ├── Plan entitlements / usage / quotas
      ├── Workspace (project / team / department)
      │   └── Collection (knowledge and ACL scope)
      │       └── Document
      │           └── Immutable Version
      │               └── Source Elements / Assets / Evidence
      └── Audit / Retention / Data Egress Policies
~~~

One global deployment user may belong to many tenants with different roles. Resolve an explicit active tenant at each browser/API request; it is **not** authoritative merely because a request body supplies tenantId. A user has no access to a tenant without a validated membership or operator-only support grant. The API carries immutable tenant context into job envelopes, Blob paths, PostgreSQL RLS, Qdrant payloads, caches, model calls, usage metering, SSE events and document exports.

### 23.2 Isolation tiers

| Tier | Deployment/data | Intended use | Implementation status |
| --- | --- | --- | --- |
| **Shared SaaS** | Shared application, tenant-isolated PostgreSQL/Qdrant/object storage and workers | Standard hosted plans | Initial SaaS target |
| **Dedicated-data SaaS** | Shared application/control plane; dedicated DB/index/bucket or storage accounts for selected tenants | Higher-assurance organizations | Future behind data-placement abstraction |
| **Dedicated deployment** | Dedicated API/workers, datastore/index/storage and networking | Enterprise/private or regulated customers | Separate enterprise target |
| **Single-tenant self-hosted** | Customer-operated one-tenant defaults; identical tenant schema | Community/private/on-prem | Initial OSS target |

Shared tenant isolation uses RLS where applicable, authorization-provider checks, composite tenant referential integrity, tenant-scoped Qdrant filters, per-tenant document keys, short-lived authorized asset access, tenant-specific cache namespaces and query-context checks. Deduplication across tenants must not reveal content presence or accidentally cross-link proprietary originals. Stronger encryption isolation, customer-managed keys and regional storage/data residency can be optional plan/deployment capabilities.

### 23.3 Resource ACL and lifecycle

Scopes: tenant, workspace, collection and document, inherited down to versions, figures, chunks, table cells and evidence. An explicit document deny or classification restriction must not be widened by parent grants; resource permissions and session policy re-check on **every** retrieval and asset delivery. Tenant suspension denies new access except approved recovery/export workflows; it must not physically delete data without retention/deletion policy. Tenant termination is a tracked multi-stage process: export window, revoke sessions/tokens, stop queued work, tombstone assets, delete indexes and data after legal holds/retention, then confirm audit-safe completion.

Backup/restore and portability include tenant ownership mapping, schema versions, manifest checksums, data residency constraints, secret exclusions and reindexing capability. Test one-tenant restore from a shared installation without leaking other tenants' data.

## 24. RBAC, resource policies and authorization matrix (v1.3)

### 24.1 Policy evaluation

**Deny by default.** For each user or service principal evaluate: valid authentication -> active tenant membership -> assigned role/permission within a permitted scope -> resource ACL with inheritance and explicit deny -> classification/data-egress/step-up-MFA/time-bound approval constraints -> active document/index version -> subscription feature entitlement and quota, when applicable. **Entitlements never add privileges**; they can only restrict or enable functionality already authorized. A role assignment does not imply blanket access to all documents if collections/documents are restricted.

Use a machine-readable versioned **permission catalog** shared with .NET policy handlers, OpenAPI permission metadata, integration tests and typed React navigation. Server is always authoritative; frontend-hidden controls do not secure an API. Custom role creation and assignment can only grant permissions the acting principal is permitted to delegate at that scope; prohibit self-escalation and cross-tenant assignments. Maintain audit history, administrative step-up authentication and approval for ownership transfer, bulk purge, IdP trust changes and exceptional support access.

Platform roles (outside normal tenant content): **Platform Owner** (operator governance), **Platform Operator** (infra operations), **Platform Security Administrator** (deployment security), **Platform Support** (bounded metadata diagnostics). Operator staff do **not** automatically acquire tenant document read/export rights. Tenant content break-glass, if explicitly supported, must require time-limited customer approval and auditable scope; self-hosted deployment owner remains distinct from a tenant owner.

### 24.2 Tenant role templates

| Code | Built-in role | Scope / intent |
| --- | --- | --- |
| TO | Tenant Owner | Ownership and delegated tenant governance; protected ownership transfer |
| TA | Tenant Administrator | Membership, tenant operations and approved administration |
| SA | Security Administrator | Tenant SSO/MFA, policy and access administration |
| BA | Billing Administrator | Plans, invoices, provider usage and quotas |
| KA | Knowledge Administrator | Workspaces, collections, ingestion and knowledge governance |
| CO | Contributor | Upload, edit metadata and reprocess within authorized resources |
| RV | Content Reviewer | Validate and approve extracted document content |
| AN | Analyst | Authorized queries, comparisons, calculations and exports |
| RE | Reader | View and query permitted evidence; not unrestricted source export |
| AU | Auditor | Authorized security and audit viewing without routine content access |

Role templates are immutable versioned defaults; tenant-defined custom roles are separate instances with validated permission sets. Collection/workspace roles can be assigned at narrow scopes. **TO and TA are not automatically allowed to bypass content classification, legal holds or explicit resource denies**.

### 24.3 Initial default role matrix

**Legend:** Y = built-in role template grants action *subject to resource scope and policy*; - = no default grant. The matrix is a suggested initial configuration to test and refine, **not an automatic universal tenant-owner bypass**.

| Action | TO | TA | SA | BA | KA | CO | RV | AN | RE | AU |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Manage tenant settings | Y | Y | - | - | - | - | - | - | - | - |
| Invite/remove members | Y | Y | - | - | - | - | - | - | - | - |
| Manage tenant role assignments | Y | Y | Y | - | - | - | - | - | - | - |
| Configure SSO/MFA | Y | - | Y | - | - | - | - | - | - | - |
| Manage tenant AI provider profiles | Y | Y | - | - | Y | - | - | - | - | - |
| Manage billing and quotas | Y | - | - | Y | - | - | - | - | - | - |
| Create workspaces/collections | Y | Y | - | - | Y | - | - | - | - | - |
| Manage collection access policies | Y | Y | Y | - | Y | - | - | - | - | - |
| Upload documents | Y | Y | - | - | Y | Y | - | - | - | - |
| Edit document metadata | Y | Y | - | - | Y | Y | - | - | - | - |
| Reprocess documents | Y | Y | - | - | Y | Y | - | - | - | - |
| Approve extracted content | Y | Y | - | - | Y | - | Y | - | - | - |
| Delete documents (recoverable) | Y | Y | - | - | Y | - | - | - | - | - |
| Execute scoped search/Q&A | Y | Y | - | - | Y | Y | Y | Y | Y | - |
| Compare permitted documents | Y | Y | - | - | Y | Y | Y | Y | - | - |
| Execute verified calculations | Y | Y | - | - | Y | - | Y | Y | - | - |
| Download original permitted sources | Y | Y | - | - | Y | Y | Y | Y | - | - |
| Export answers/analysis results | Y | Y | - | - | Y | Y | Y | Y | - | - |
| Read authorized access/audit logs | Y | Y | Y | - | - | - | - | - | - | Y |
| Manage tenant service accounts | Y | Y | Y | - | - | - | - | - | - | - |
| Manage governance alerts / revalidation | Y | Y | - | - | Y | - | Y | - | - | - |
| Create and edit permitted Evidence Briefs | Y | Y | - | - | Y | - | Y | Y | - | - |
| Publish/export permitted Evidence Briefs | Y | Y | - | - | Y | - | - | Y | - | - |
| Submit source-bound correction proposal | Y | Y | - | - | Y | Y | Y | - | - | - |
| Approve reviewer corrections | Y | Y | - | - | Y | - | Y | - | - | - |
| Manage sensitivity/redaction policies | Y | - | Y | - | - | - | - | - | - | - |
| View aggregated knowledge-health dashboard | Y | Y | - | - | Y | - | - | - | - | - |
| Use MCP integration (enabled and delegated) | Y | Y | - | - | Y | - | - | Y | - | - |
| View unmasked confidential content | - | - | - | - | - | - | - | - | - | - |

**v1.4 caveats:** Review submitters cannot approve their own high-impact proposal when dual control is required; approval and publication are distinct audited steps. The MCP row enables *using* the integration only, never additional content privileges. No built-in role receives `confidential.unmasked.read` by default: grant it explicitly at an authorized narrow resource scope with classification policy and step-up requirements. Governance/brief roles do not bypass source access checks.

**Important separations:** security administrators do not receive document reading permission by virtue of security administration. Readers may see evidence excerpts and rendered figures when authorized but not necessarily download original complete files. Source download, export, permanent purge, delegated user impersonation, support access and ownership transfer are distinct sensitive permissions. Document rights from this table are always intersected with resource ACLs, confidentiality and data export policy. A tenant-scoped admin's role does not enable platform-operator APIs.

### 24.4 Initial machine-readable permission namespaces

~~~text
platform.deployments.read
platform.deployments.manage
platform.security.manage
platform.support.diagnostics

tenant.settings.read
tenant.settings.update
tenant.members.invite
tenant.members.remove
tenant.roles.manage
tenant.roles.assign
tenant.identity_provider.manage
tenant.domains.verify
tenant.security.policy.manage
tenant.audit.read
tenant.billing.read
tenant.billing.manage
tenant.subscription.manage

workspace.create
workspace.read
workspace.manage
workspace.permissions.manage

collection.create
collection.read
collection.manage
collection.permissions.manage

document.upload
document.read
document.original.download
document.metadata.update
document.reprocess
document.extraction.review
document.publish
document.delete
document.purge

query.execute
query.visual_search
query.compare
query.analytics.execute
query.export

model_config.manage
service_account.manage
service_account.credential.rotate
tenant.data.export
tenant.data.delete

evidence_brief.create
evidence_brief.read
evidence_brief.edit
evidence_brief.publish
evidence_brief.export
evidence_brief.verify

governance.alert.read
governance.alert.manage
governance.conflict.review
governance.subscription.manage

document.correction.propose
document.correction.review
document.correction.approve
document.correction.publish
document.sensitivity.manage
confidential.unmasked.read
confidential.redaction.policy.manage

integration.mcp.use
integration.mcp.client.manage

knowledge_health.read
knowledge_health.manage
~~~

Permission metadata should define allowed scope types, delegation level, risk classification, prerequisite step-up MFA, audit category and whether it controls **operation** versus **content visibility**. Store assignment decisions and effective permission evaluation logs without dumping secret content.

### 24.5 Security requirements for access changes

- IdP login **does not** auto-join a tenant via unverified email domain; use verified domain policy plus invite/approved JIT.
- SCIM deprovisioning, role removal, collection ACL revocation and tenant suspension invalidate sessions/effective-scope caches and suppress in-flight RAG evidence before disclosure.
- Superusers cannot directly grant themselves privileges outside their authority; privileged updates require authorization and audit.
- Test both horizontal (Tenant A vs B) and vertical (Reader vs TA vs operator) privilege escalation against REST APIs, SSE, asset URLs, exports, vector retrieval and analytics.
- Service-account scopes and rate limits never exceed the grants explicitly configured for that account.
- Security and audit roles have access to metadata only unless an independent explicit document permission exists.

## 25. SaaS control plane and enterprise operations (v1.3)

### 25.1 Separation of core and operator plane

The **core data plane** serves documents, authentication, authorization, retrieval and source citations. The **SaaS control plane** provisions tenants, selects placements/regions, manages subscriptions, entitlements, usage, quotas and service operator workflows. Its credentials, APIs, datastore privileges and audit scope must be distinct from end-user document access. Self-hosted runs its local admin and entitlements/capability configuration **without** connecting to the SaaS control plane.

### 25.2 SaaS features and delivery priority

| Function | Priority | Required behavior |
| --- | --- | --- |
| Tenant registration/onboarding | P0 | Verified bootstrap, safe first owner, lifecycle state machine |
| Tenant suspend/reactivate/terminate | P0 | Gate new jobs/query access, preserve retention/export rights according to policy, auditable cleanup |
| Entitlement and feature policy | P0 | Plans cap product features, but never grant document permissions |
| OCR/model/embedding/storage usage meter | P0 | Tenant-tagged idempotent records from completed provider outcomes, late adjustments/reconciliation |
| Per-tenant quotas and rate limits | P0 | Upload limits, pages, bytes, users, concurrent jobs, search/LLM tokens; fair queue controls |
| Operator/tenant billing separation | P0 | Invoice/subscription webhooks verified; billing admin cannot browse tenant knowledge |
| Backup/restore and tenant export | P0 | Document/evidence integrity and isolated recovery |
| BYO Azure Foundry/other AI credentials | P1 | Tenant secrets in isolated vault; provider egress/billing ownership and quotas |
| SCIM / SAML / advanced enterprise SSO | P1 | As described in §22 |
| Tenant-specific audit export and webhooks | P1 | Signed/idempotent delivery with tenant scope |
| Dedicated data/deployment isolation | P1/P2 | Provisioning, migrations, monitoring and contracted data separation |
| Custom domains and branding | P2 | DNS/domain validation, cert rotation and secure redirect handling |
| Customer-managed keys, region residency | P1/P2 | Per contract; immutable retention and encryption governance |
| Administrative approval workflows | P2 | Sensitive ownership, deletion, exceptional access |

Define tenants states such as **PROVISIONING, ACTIVE, SUSPENDED, EXPORT_PENDING, TERMINATING, TERMINATED, FAILED** with explicit idempotent lifecycle operations. Never bypass security to accommodate plan downgrade or unpaid billing. Suspended tenants keep auditable provenance until retention/erasure actions complete.

### 25.3 Plan entitlements, billing and quotas

A permission answers **may this principal perform action X on resource Y?** An entitlement answers **is feature X included and within available usage budget for Tenant T?** Both must pass, but an entitlement cannot elevate access. Meter physical usage dimensions separately (OCR pages by actual model/region, OCR retries, embedding tokens, LLM input/output tokens, bytes stored, retrieval operations, concurrent ingestion). Bill only according to the explicitly published commercial policy, with corrections/idempotency for duplicates and provider-billed timeout cases. Plans, pricing, free/open-source entitlements and payment processors are business configuration—not baked into the domain's authorization semantics.

Quota enforcement spans admission (before billable work), fair scheduling, provider budgets, metered completion and reconciliation. Reserve/commit/release budgets where possible; warn about nearing limits without leaking other tenants' usage. Provide tenant-facing and operator-facing dashboards with distinct scopes.

### 25.4 Security, migration and operations

Provide retention/hold/export/delete workflows, optional tenant-managed encryption and data-region policy, tenant scoped backups, anonymized service telemetry, signed exports, and independently audited platform support operations. Support workload isolation and resource-placement abstraction before promising dedicated hosting. Feature gates must be enforced on APIs and workers, not solely via UI.

A tenant moving between hosted and self-hosted instances exports original bytes, immutable document versions, CDM elements, evidence locators and compatible audited configuration **without credentials or active sessions**. The importer verifies owner consent, tenant mapping, hashes, version and schema compatibility, RBAC/ACL mapping, then reindexes. Export/import is separate from the data-erasure request: no hidden forced retention or dependence on our hosted SaaS after customer migration.

### 25.5 Release and enterprise acceptance tests

- Self-hosted install/login/upgrade/backup/restore with *zero* vendor SaaS dependencies.
- Built-in login, OIDC/Entra federation, session rotation/revocation, cross-provider verified identity linking, issuer/subject uniqueness, MFA and emergency recovery.
- Multiple tenants per user, tenant switching, invited membership, deny-by-default RBAC and document-scoped ACLs; all image/figure/citation/SQL paths enforce same access policy.
- JIT, domain verification, SCIM and group mapping when those capabilities ship; deprovisioned identities have no continuing effective access.
- API clients, token expiration/rotation, scope validation and service-to-service authentication without human refresh tokens.
- SaaS plan downgrade/suspension/usage exhaustion cannot leak data, bypass quotas or corrupt indexes.
- Shared and dedicated tenant placement tested independently before a stronger isolation tier is offered commercially.
- Tenant export/import replays source references accurately and does not export secrets; tenant restore never exposes other organizations.
- Operator accounts cannot read tenant content without explicit, time-bound and audited customer-approved access.

## 26. Proactive evidence freshness and contradiction governance (v1.4)

### 26.1 Scope, invariants and source authority

A saved/pinned answer may cite a perfectly valid immutable v1 element even after v2 becomes READY. **Citation integrity remains valid for the historic source**; the saved **answer's current applicability** needs separate evidence-freshness state. Never rewrite saved answers or historical citations in place. The status is evaluated against the relevant source baseline (as-of date, version, collection, applicability, approval/effective dates and policy), not simply the highest version number.

| Freshness state | Precise interpretation |
| --- | --- |
| CURRENT | Saved evidence is still applicable for its explicitly recorded baseline |
| CITATION_SUPERSEDED | Cited source element was modified, removed or formally superseded in the applicable newer baseline; **not proof the old claim is false** |
| REVIEW_REQUIRED | Change may alter a claim, calculation or safety/compliance consequence and needs authorized assessment |
| CONTRADICTION_CANDIDATE | Competing applicable statements appear inconsistent; no final conflict yet |
| CONTRADICTED | Two applicable, verified claims conflict under an established, reviewable authority/effective-date policy |
| REVALIDATED | Saved artifact has an audited revalidation decision against specified sources and time |
| SOURCE_UNAVAILABLE | Retention/erasure or revoked access prevents citation inspection; do not falsely mark prior hashes invalid |

Reference hashes and source-version identity are immutable; freshness transitions form an append-only state/event history. Do not automatically treat a draft SOP as overriding an approved SOP, an old revision as invalid for legacy equipment, or a language-model similarity judgment as formal contradiction.

### 26.2 Event-driven impact pipeline

When an authorized document version reaches READY, its PostgreSQL transaction records a claim-check-only outbox event including tenantId, documentId, old/new ready version IDs, index generations, profile hash, run ID, effective-date/approval metadata and eventId. A tenant-scoped background worker consumes idempotently and:

1. Uses §12.1 structural CDM/AST alignment to identify ADDED, REMOVED, MODIFIED, MOVED and ambiguous elements and relationships. Unchanged/moved-with-identical-content is **not** automatically stale.
2. Traverses reverse **evidence_dependencies** from cited document elements/table cells/calculation inputs to saved answers, published briefs, workspace FAQs, reports and notification subscriptions; do not rescan all historic answers.
3. Marks impacted artifacts as CITATION_SUPERSEDED or REVIEW_REQUIRED **for a named comparison baseline**. Verify whether a changed value materially affects calculations; do not invent a newly calculated result.
4. Performs selective, permission-gated comparison of competing active documents using authority order, document family, effective interval, model/version and verified citations. Produce **CONTRADICTION_CANDIDATE** with bilateral source evidence and explicit uncertainty.
5. Requires designated reviewer or policy-approved deterministic evidence to transition to CONTRADICTED or REVALIDATED. No unaudited model output may silently mark a regulatory conclusion definitive.
6. Writes deduplicated notification events with severity, affected object IDs and **no confidential excerpts** by default. At delivery time, reauthorize recipient/subscriber against current tenant and object permissions. Failed or rate-limited notifications retry through the transactional outbox; revocations suppress disclosure.

Use capped fan-out, checkpoints, fairness quotas and notification idempotency. Alert latency is measured from READY to state/notification creation, not promised as instantaneous. Revalidating an answer never alters the original v1 citation; it produces a new evaluated revision or tracked annotation.

### 26.3 Contracts and dependencies

- **evidence_dependencies:** tenantId, consuming artifact kind/ID/revision, source documentVersionId, elementId and/or tableCellId, evidenceId, claimId, dependency kind, calculationRunId (if any), source hash and active-baseline constraint. Unique composite indexes enable reverse lookup. Entries are created when verified claims/briefs/FAQ revisions are saved, not retroactively assumed for unsaved transient chat.
- **answer_freshness_states:** artifact revision, baseline, state, triggering event ID, reviewer, reason, evidence and timestamps. State history is append-only; current status is a projection.
- **evidence_governance_events / contradiction_candidates:** source-pair references, authority policy, applicability periods, confidence/diagnostics, review state, decision, audit events.
- **notification_subscriptions / notification_outbox:** tenant, workspace, principal/subscriber, authorized scope, channel and deliverability/retry settings. Webhooks and emails contain only allowed metadata.
- **Permissions:** governance.alert.read/manage, governance.conflict.review and governance.subscription.manage; status changes to confidential artifacts require independent document.read privilege.
- **API/UI:** governance alerts feed, workspace freshness dashboard, review/revalidate workflow, subscriptions and saved answer status chips; old citations still open the correct retained source when allowed.

## 27. Saved Evidence Briefs and offline-verifiable audit bundles (v1.4)

### 27.1 Brief authoring and immutable publication

A user with evidence_brief.create plus underlying document access can pin **already verified** answer blocks (paragraph, table, calculation, retrieved original/crop, comparison) from multiple authorized queries into an editable brief. Brief working drafts carry an explicit tenant/workspace owner, collaborators, permission scope, section ordering, annotations, and block IDs. Each block retains query ID, verified claim/support verdict, original document version(s), cited element IDs and locators, extraction/profile version, and for arithmetic the exact typed plan, expression and calculation inputs. **A pasted unverified paragraph is draft commentary, not verified evidence.**

Publication creates an **immutable evidence_brief_revision** with a provenance closure over every referenced source, the currently assessed freshness states, policy/sensitivity classification, actor, timestamp, approval and stable hashes. Source changes do **not** rewrite published revisions: they generate a stale/review alert (§26) and may prompt publication of a new revision. Publishing may require step-up MFA or second-person approval by tenant policy.

### 27.2 Formats and manifest

Support controlled outputs: HTML report + manifest, PDF report + manifest, and a portable ZIP **verification bundle** containing approved evidence material, transformations, typed calculation metadata, hashes, signature and a standalone/offline verifier (script or executable as documented). The signed canonical manifest (versioned schema with reproducible serialization and deterministic ordered entries) contains:

~~~json
{
  "schemaVersion": "evidence-bundle/v1",
  "bundleId": "bundle-id",
  "tenantId": "tenant-id",
  "briefRevisionId": "immutable-revision-id",
  "createdAt": "ISO-8601 timestamp",
  "publisher": "signed publisher identity/pseudonymous audit ID",
  "policyVersion": "policy-revision",
  "artifacts": [
    {"path": "evidence/page-crop-01.png", "sha256": "hex", "sourceVersionSha256": "hex", "transform": {"kind": "crop", "sourceLocator": {"page": 5}}},
    {"path": "calculations/calc-01.json", "sha256": "hex", "calculationRunId": "run-id"}
  ],
  "claims": [{"blockId": "block-id", "evidenceIds": ["ev-id"], "supportStatus": "verified"}],
  "signature": {"algorithm": "policy-allowlisted-signature", "keyId": "key-id", "value": "signature-value"}
}
~~~

For cryptographic signing, sign **canonical unsigned manifest bytes** (or a canonical digest) and attach a detached signature or a clearly excluded signature field to avoid self-referential hashing. Use an allowlisted signature algorithm (for example Ed25519 or ECDSA with validated canonical encoding), a tenant/deployment scoped signing key in Key Vault/HSM where configured, key ID, public-key/trust-chain distribution and rotation/revocation policy. A stand-alone offline verifier must validate the signature against an **independently trusted** public key, each included artifact hash, transformation lineage as far as reproducible, calculation-plan/input file integrity, schema completeness and allowed algorithms.

**Limits of proof:** Hashes and signatures establish tamper evidence and signer identity under a trust policy, *not* that an LLM claim is factually correct. A source-hash reference alone does not let an offline auditor inspect the source unless authorized content is actually included in the bundle. Explicitly label missing source material as **externally referenced / not independently verifiable offline**; source link rot or revoked access is not hidden by a green signature. Derived crop/HTML/PDF representations are not interchangeable with the original bytes; record their transforms and original hashes.

### 27.3 Export policy, safety and audit

Run a fresh tenant/resource/field-level authorization and data-classification check at **export time**, and again before a delayed download; opening a past brief never grants permanent access to its protected sources. Redact permitted renditions (§30) or refuse export when a safe transformation cannot be proven. Original asset inclusion requires document.original.download or a separate export-specific grant; an answer export permission alone does not grant all source bytes. Package signing keys and confidential data never leave the authorized deployment.

Store immutable signed manifest, export run, verifier result and download audit. Support offline validation on self-hosted deployments with **no vendor SaaS dependency**. Exports subject to erasure, legal holds and retention use explicit lifecycle policies; signed historical artifacts may later be **cryptographically intact but authorization-inaccessible**. Prevent path traversal, active HTML scripts and untrusted embedded content in portable packages.

### 27.4 Brief lifecycle, API and permissions

- **Workflow:** DRAFT → IN_REVIEW (optional) → PUBLISHED revision → SUPERSEDED_BY_NEW_REVISION or ARCHIVED; a stale source flags status without editing the revision.
- **Entities:** evidence_briefs, brief_blocks, evidence_brief_revisions, verification_manifests, export_runs, export_artifacts, and evidence_dependencies.
- **Permissions:** evidence_brief.create/read/edit/publish/export/verify and underlying source/asset permissions; approval/release may require distinct actors by policy.
- **API/UI:** brief list/editor, pin verified block, ordered composition, freshness marker, publish, export ZIP/PDF/HTML, audit, and local verifier instructions.

## 28. Tenant-scoped Model Context Protocol (MCP) integration (v1.4)

### 28.1 Protocol and transport boundary

Offer optional **/api/v1/mcp** as a first-class remote MCP server using the transport and authorization specifications for the implemented MCP release (Streamable HTTP for the targeted release, appropriate session/Origin validation and resource metadata). Use the standards-defined protected-resource authorization discovery for OAuth clients; validate access-token issuer, audience/resource indicator, expiry, tenant, scopes and client identity. **Do not accept an access token issued for some other API just because its signature is valid; no token passthrough.** CORS/Origin and local host binding controls are part of the threat model.

For human IDE assistants/bots, use **delegated per-user authorization** and effective active-tenant membership. For scheduled/headless agents, use tenant-scoped service_account/API-client grants per §22.4. Tokens must not carry unrestricted platform operator rights, and API keys must not be used as anonymous substitute for OAuth where the selected MCP client/server contract requires OAuth. Machine and delegated principals both undergo the §24 authorization policy and §30 redaction policy **for every tool call**.

### 28.2 Proposed read-only tools

| Tool | Inputs | Authorization | Output |
| --- | --- | --- | --- |
| search_verified_evidence | Query, permitted workspace/collection, language, filters, paging | integration.mcp.use AND query.execute AND resource ACL | Pre-authorized typed CDM evidence with deep links and support metadata |
| get_evidence | Short-lived evidence ID, requested safe format | integration.mcp.use AND document.read AND field policy | Server-sanitized excerpt/crop locator or policy-safe denial |
| compare_document_versions | Allowed immutable version IDs, comparison scope | integration.mcp.use AND query.compare AND both-side ACL | Structural differences and verified bilateral citations |
| execute_table_calculation | Typed allowed plan and bounded filters | integration.mcp.use AND query.analytics.execute AND cell permissions | Deterministic result, excluded inputs and calculation provenance |
| get_document_outline | Document/version ID | integration.mcp.use AND document.read | Allowed structural outline with no inaccessible node leaks |
| get_processing_status | Document/job ID | integration.mcp.use AND scoped document-management capability | Authorized status/diagnostic summary |

No arbitrary SQL, raw Qdrant access, unrestricted vector chunks, document deletion, ACL modification, unreviewed writes or free-form model-executed commands in the initial MCP surface. MCP descriptions/results are **untrusted external text** to downstream assistants; source document prompt injection cannot expand tool authority. Maintain strict output size/pagination/resource limits, tenant quotas, per-tool audit/correlation and timeout/cancellation.

A **verified evidence tool result** means referentially valid, authorized and provenance-linked; do not label subjective inference as certain solely because the source exists. Provide status/freshness and applicable disclaimers. Viewer deep links are only pointers; the UI rechecks current authorization instead of exposing bearer tokens or long-lived public asset links. Resources/prompts, if later exposed via MCP, must have independent explicit policy and safety checks.

### 28.3 Deployment and rollout

MCP is an optional connector for OSS/on-prem and SaaS, not a requirement for core RAG bootstrapping. Advertise capability only where enabled; document transport/version compatibility, authorization-server metadata, scopes, onboarding and per-tenant client registration. Host externally accessible MCP behind HTTPS reverse proxy with origin/CSRF/session protections. Keep service accounts and per-user delegation separate. Test confused deputy, cross-tenant, token audience, SSRF, CSRF, prompt injection, streaming cancellation, revoked membership and sensitive-source redaction. Ship the external endpoint only after the core authorization and evidence policy gates pass.

## 29. Human-in-the-loop extraction and evidence review (v1.4)

### 29.1 Review triggers, inbox and authority

The Extraction Inspector becomes a tenant/workspace-scoped **Review Inbox** for unresolved PARTIAL versions, UNVERIFIED_SCALE, UNVERIFIED_HEADER, missing table-cell geometry, OCR corruption, uncertain source locators, contradictory extraction runs and sensitivity/redaction uncertainty. A reviewer assigned a task sees an original PDF/page crop **only when the reviewer is authorized for the source**; a redacted safe rendition otherwise applies. Do not expose a restricted document simply because the user has the RV role.

Task state machine: PENDING → ASSIGNED → IN_REVIEW → PROPOSED → APPROVED / REJECTED → PUBLISHED; also CANCELLED, EXPIRED, SUPERSEDED for source changes. Enforce tenant, object owner, reviewer scope, stable source version, optimistic concurrency/fencing, task lease and review deadline; prevent old review decisions being committed to a newly replaced extraction generation.

### 29.2 Versioned correction overlay, not source mutation

The reviewer proposes a typed correction (original element/cell ID, page/byte/box location, old parser result and hash, new text/value/unit/scale/header relation/figure link, reason, signer, source evidence and confidence/decision), preserving the original bytes and original extracted run. A proposed correction is **not** automatically authoritative.

Validated proposals write append-only, human-verified overlays associated with the document version and a correction generation. If financial/regulatory/safety impact or tenant policy requires dual control, an independent authorized approver must approve; the proposer cannot self-approve. A signed/audited publication step advances the **derived/CDM/index generation** using the existing fenced READY cutover (§§15.1–15.2), or creates a new processing revision as needed; **no OCR rerun** if the input asset/recognition text remains unchanged.

Derived retrieval units and index projections update atomically with generation pinning. Invalidate or version-tag affected cached retrieval results, field-specific sanitized renditions, calculation outputs, saved answer dependency records and brief freshness checks. A past calculation result is not silently rewritten; it is re-run or marked for revalidation with the new correction. Partial input data remains excluded until the corrected value passes validation and publication.

### 29.3 UI and permission contract

- Prioritized review inbox with task type, impact, confidence, severity, assigned reviewer, source and dependency count.
- Side-by-side crop/text/table diff, unit/scale picker, header ancestry, typed numeric preview, required justification, optional second approval, conflict resolution and status history.
- **Permissions:** document.extraction.review for read workflow, document.correction.propose, document.correction.approve and document.correction.publish; classification/source access still required. Sensitive source reviewer grants are explicit, time-scoped where necessary.
- **Acceptance:** duplicated reviewer clicks, stale task revisions, concurrent approval, permissions revoked mid-review, rejected scale, cross-window table correction, and OCR replay cannot turn an unapproved value into an active verified input.

## 30. PII/sensitivity classification and secure redaction (v1.4)

### 30.1 Threat model and policy-first classification

Sensitive data includes bank accounts, national identifiers (PAN, SSN, etc.), financial compensation, personal information, medical/incident records, confidential engineering details and tenant-defined entities. During ingestion/NORMALIZING, run an **optional policy-governed detection pass** with exact CDM text spans, PDF/page polygons and image regions, supported by confidence and human review. Detection is fallible; boundaries may be approximate in OCR and diagrams. Keep source confidentiality labels, element/cell classifications, detectors/model/run version and a source-linked **sensitivity_annotations** ledger.

**Egress comes first:** A tenant policy must decide whether the unredacted original may be sent to Azure Foundry/Mistral, Document Intelligence or other cloud processors **before** making that call. Local preflight classification may be incomplete; if policy disallows sending uninspected content, process only with an approved local path or reject/hold for review. Do not claim sensitive data can always be detected before an OCR pass.

Permission to query a collection does not imply permission to view its protected spans. Add confidential.unmasked.read only through explicit resource-scoped grants and required conditions, never automatically for TO/TA/SA/KA roles. Tenant policy can require full-document denial rather than partial sharing for restricted content.

### 30.2 Safe delivery and derivative design

The authoritative original remains immutable and **restricted**. A lower-privilege user receives only a **server-created, policy-versioned sanitized derivative**, never the original with CSS, SVG or black UI rectangles drawn over it. Policy-specific extraction/generation must remove or irreversibly sanitize:

- Text content, extracted OCR strings, PDF selectable text, hidden layers, annotations, metadata and alternate descriptions.
- Raster images, cached thumbnails, previews, diagrams and crops; raster masks must be irreversible in delivered pixels and checked against underlying text/text-box leakage.
- HTML/Markdown text/attributes/links, alt text, OCR figure captions, table cells, calculation inputs and exported spreadsheets/PDFs/brief bundles.
- Query suggestions, lexical index/snippets, text/image embeddings, LLM evidence context, generated summaries, cached responses, MCP outputs, logs and diagnostics.

**Indexing boundary:** An unrestricted embedding created from secret text is not approved for broader search solely because the final answer would be masked. Either build and validate separately sanctioned sanitized retrieval units/index entries for low-privilege scopes, keep restricted content entirely outside that audience's index/candidate set, or deny search at document level. Respect policy revision and tenant/principal scope in cache keys; ACL/redaction revocation requires active invalidation and recheck at final disclosure, not just cache expiry. Apply privacy-aware table aggregation safeguards to prevent deriving a protected single individual's value from small cells or arithmetic differencing.

When reliable sanitization is not possible (ambiguous OCR location, uncropped hidden PDF layer, inaccessible image text, detector low confidence), **fail closed**: withhold that region or the entire document for low-privilege audiences. No UI-only mask or arbitrary inference is acceptable as compliance redaction.

### 30.3 Review, auditing, classification, retention

Sensitivity false positives/negatives route to authorized review tasks with original evidence only for privileged reviewers. Permit tenant administrators/security staff to manage policy *metadata* without acquiring confidential document read. Maintain immutable original and annotation hashes, transformation versions, sanitized asset hashes, approval, effective policy version, lineage and access logs. Recompute derivatives on classification updates, propagate alert/citation dependencies and ensure exports and in-flight SSE stop disclosing newly forbidden material.

Apply retention/legal hold and right-to-erasure policy to originals **and** sanitizer outputs, indexes, caches, query history and verification packages. Track detector quality per language and document type. Security release tests inspect both visual raster and underlying PDF text/extraction layers, prompt injections, raw asset endpoints, image crops, caches, generation context, tool responses, model input, cross-user inference and redaction revocation.

## 31. Collection-level knowledge gaps and abstention analytics (v1.4)

### 31.1 Structured quality outcomes

Emit tenant/workspace/collection-authorized knowledge_quality_events when queries end in abstention, insufficient_verified_data, low_semantic_support, failed source resolution, citation supersession, extraction PARTIAL, retrieval deficiency or permission filtering. Separate these categories:

| Outcome | Interpretation / investigative action |
| --- | --- |
| NO_APPLICABLE_EVIDENCE | Candidate corpus might not contain requested material, **not proof a manual is absent** |
| RETRIEVAL_LOW_RECALL | Documents may exist but indexing/rerank/filters failed |
| EXTRACTION_INCOMPLETE | Relevant document exists but some OCR/table content is PARTIAL |
| UNVERIFIED_FIELD | Scale/header/numeric input needs reviewer |
| STALE_EVIDENCE | Existing cited evidence has a newer potentially applicable version |
| LOW_CLAIM_SUPPORT | Retrieved evidence does not substantiate answer claim |
| ACCESS_RESTRICTED | Authorization blocks access; **do not reveal existence, count or topic of inaccessible documents** |
| AMBIGUOUS_QUERY | Intent/period/version ambiguous, not necessarily missing knowledge |

The collection dashboard clusters recurring **authorized** question topics, per-category trends, review backlog and reprocessing failures, with action suggestions (review extraction, tune search, verify an SOP revision, request an approved upload). Any proposed root cause generated by a model is labeled **hypothesis**, not a confirmed absent document.

### 31.2 Privacy and security boundaries

Aggregate strictly within authorized tenants/workspaces/collections, only from events whose metadata the viewing role can see. Minimize raw prompt retention; redact PII before aggregation, hash/cluster prompts under tenant-specific keys as policy permits and avoid sending user queries to a cross-tenant analytics model. Apply minimum cohort sizes and small-cell suppression, aggregation latency/bucket limits, configured retention and appropriate consent/notice for telemetry in self-hosted installs. Do not leak individual user query content, confidential topics, denied document names or another tenant's usage.

**Permissions:** knowledge_health.read/manage, plus authorized collection scope; KA/TA/TO may view the default tenant-level summarized dashboard where policy allows. Security/audit roles do not automatically receive raw query text. Knowledge-gap alerts are diagnostic and never substitute for verification of document availability.

### 31.3 Dashboard and SLOs

Provide time-series by outcome/collection, top privacy-safe recurring topics, abstention rate, source-quality gaps, reviewer queue lag, stale citation count, extraction coverage, feedback and recommended human actions. Measure trend stability, cluster precision and false "missing document" accusations on a labeled corpus. Link dashboards to *authorized* review or upload workflows; not to anonymous background crawler actions.
## 32. Master Encryption Key lifecycle and pluggable key protection (v1.5)

### 32.1 Decision, scope and threat model

The S0 Integration Setup Center stores cloud OCR/LLM API keys, Azure configuration secrets, SMTP credentials, OAuth/OIDC client secrets and other enabled third-party credentials. **Do not store wizard secrets as plaintext in PostgreSQL, generic appsettings, HTML responses, telemetry, container logs or images.** Use **authenticated envelope encryption** under a versioned key protection service. This is separate from ASP.NET Core Identity password hashing and distinct from ASP.NET Core Data Protection's session/cookie key ring. External refresh tokens, where retained server-side, require similarly protected purpose-specific storage and their own revocation lifecycle.

TPM 2.0 may protect a **root/wrapping key** for a single on-prem host, but is never mandatory, never used to store whole credential blobs, and **never** the only way to restore secrets after motherboard/TPM loss. A stolen encrypted database alone must not reveal plaintext credentials. Conversely, compromise of an authorized running application/worker or an abusive legitimate decrypt permission may still expose secrets; TPM/KMS does **not** eliminate runtime threats. Minimize decrypt principals, scopes and exposure windows.

**Terminology:**
- **DEK:** Fresh randomly generated symmetric key **per stored secret version** (AES-256-GCM key) encrypting that secret payload.
- **Tenant KEK (or tenant MEK):** Wrapping layer for the tenant's DEKs; optionally a unique KMS-managed key or a wrapped tenant key in the application.
- **Deployment root protector:** TPM2, remote KMS/Vault or recovery-enabled software/external vault controlling the tenant KEK envelopes. Configurations may wrap DEKs **directly** with KMS rather than require an additional tenant KEK; the abstraction must represent the actual hierarchy truthfully.
- **Recovery envelope:** Independently protected authorized path to re-establish wrapping capability, never plain key material in a database backup.

No signing-only TPM ECC/ECDSA key can be used as an encryption/decryption key. If a TPM key performs wrapping, choose and **verify supported encryption/wrap operations**, key usage attributes, non-exportability, provider-specific algorithm and attestation requirements; alternatively seal small random root key material to a supported TPM object. Optional boot/PCR policies must not make routine firmware updates irrecoverably lock users out.

### 32.2 Encryption and persisted record contract

~~~mermaid
flowchart TD
  A["Authorized wizard API: credential input"]
  B["Generate random 256-bit DEK per secret version"]
  C["AES-256-GCM encrypt + authenticate tenant/secret context"]
  D["Persist ciphertext, nonce, tag and crypto metadata"]
  E["Wrap DEK using tenant KEK / KMS"]
  F["Pluggable key protector: TPM, Vault/KMS or recovery-enabled software"]
  G["Persist wrapped DEK and versioned key reference"]
  A --> B --> C --> D
  B --> E --> F --> G
~~~

Define an \`ISecretEncryptionService\` above an \`IKeyProtectionProvider\`. Use a vetted crypto library/AEAD; generate a unique nonce for each encryption under a given DEK and enforce nonce/record uniqueness. Include **deploymentId, tenantId, secretId, provider purpose, schema version and immutable secret version** as authenticated additional data (AAD) where supported; bind corresponding metadata to wrapped-key key references and access checks. AES-GCM IV/nonce, authentication tag, ciphertext, wrapped DEK, key version, crypto algorithm and context hash are persisted as a **single immutable secret-version record**. Never use floats, string concatenation or home-grown crypto for key derivation.

Illustrative at-rest record fields (never return these to unprivileged UI):

~~~text
integration_secret_versions:
  tenant_id, secret_id, version_id, provider_kind, purpose
  ciphertext, nonce, authentication_tag, algorithm = AES-256-GCM
  wrapped_dek, kek_key_id, kek_version, protector_id
  aad_schema_version, created_at, activated_at, retired_at
  status = PENDING | ACTIVE | ROTATING | RETIRED | UNRECOVERABLE
~~~

The browser **never** receives plaintext of a previously saved credential; edits replace with a newly encrypted version. Test connection uses the server-side secret immediately/with controlled lifetime and reports **redacted** result. Only the authorized provider adapter or narrowly scoped secret access service can unwrap/decrypt for a specific tenant and permitted purpose. Never log plaintext/key bytes; keep plaintext in memory no longer than operationally required and zero buffers where a safe API supports it. For very sensitive integrations prefer short-lived managed identity/OAuth workload identities to a persistent provider API key when the provider supports them.

### 32.3 Protector selection by distribution

| Installation mode | Default or recommended protector | Constraints |
| --- | --- | --- |
| Windows on-prem, single physical host | Optional TPM2 via Windows CNG / Microsoft Platform Crypto Provider with verified seal/wrapping design | Protect root/wrapping material, not arbitrary document payloads; separate secure recovery required |
| Linux on-prem, single physical host | Optional TPM2-TSS sealed-object/wrapping adapter, with tested device/platform policy | Secure host permissions, no raw TPM device to broad untrusted containers; recovery required |
| Docker on customer-owned host | Narrowly authorized **host-side key protection agent** over local IPC if using TPM | Restrict caller OS identity/socket and allowed key/purpose operations; do not expose generic TPM access to full app container |
| Multi-node on-prem / Kubernetes | Shared external Vault/KMS or key management service (or managed per-node wrap envelopes with quorum recovery) | No dependence on one node's physical TPM for service availability |
| Managed multi-tenant SaaS | Azure Key Vault/Managed HSM or approved cloud KMS, with Managed Identity and tenant-aware key policy | Customer-managed key option for validated enterprise tier; IAM, region and audit rules enforced |
| Self-hosted without TPM or vault | Explicitly selected **recovery-enabled software protector** backed by an operator-supplied high-entropy secret/secret-store or protected recovery material | Document brute-force threat, security trade-off and dedicated key storage; **no hardcoded/repository default MEK** |

A key protector must report capabilities and failure modes (key wrap algorithms, key versions, high-availability/recovery support, hardware-backed status, restore requirements) to S0; unsupported TPMs fall back only after explicit owner action. **Do not silently downgrade** from TPM/KMS to software after a failure. An application startup without usable keys stays administratively accessible (via local sign-in) but disables decrypt-dependent integrations and jobs until recovery or authorized re-entry.

### 32.4 Lifecycle: initialize → backup → activate → rotate → recover → retire

| Lifecycle state/action | Invariant and required control |
| --- | --- |
| **Initialize** | After one-time Superadmin bootstrap and login, before first wizard secret: choose protector, generate cryptographically random tenant wrapping material or KMS key reference; persist non-secret key version metadata atomically with activation safeguards |
| **Protect** | Seal/wrap the relevant key under TPM, Vault/KMS, or explicitly selected software/root provider; provider-specific unwrap never trusts request-supplied tenant/key reference alone |
| **Recoverability check** | Where applicable, create an **independently encrypted offline recovery envelope**, with a high-entropy recovery secret held separately from DB/container backup. Confirm backup was exported; perform a safe read-only recovery drill/challenge before considering recovery READY |
| **Activate** | Mark key protection ACTIVE only when wrapping, unwrapping, authorization and recovery policy checks pass; prohibit first integration-secret save before then |
| **Use** | Permit unwrap/decrypt only to authorized server workloads for a scoped tenant/purpose; rate-limit and audit successful/failed access and redact diagnostics |
| **Rotate KEK/MEK** | Version new wrapping key; **rewrap existing per-secret DEKs** through resumable, idempotent journal/checkpoints and dual-version read window; confirm decryptability across all secret records before retiring older wrapping keys |
| **Rotate DEK / provider secret** | Re-encrypt plaintext under a new fresh DEK and AEAD nonce; independently rotate the actual provider API secret/OAuth credential with third party (rewrapping alone does not revoke compromised credentials) |
| **Recover / migrate** | Restore from tenant/database backup onto replacement hardware through controlled recovery envelope/external vault; verify sample/all secret versions, then rewrap under the new protector and audit actor/time; **never reopen Superadmin bootstrap** |
| **Compromise response** | Revoke affected workload privileges, pause use, assess possible plaintext exposure, rotate actual provider credentials and DEKs when necessary, log alert and establish new key lineage |
| **Retire / destroy** | Block deletion of old key versions while referenced by active/retained secrets, pending work, restore points or legal holds; destruction requires explicit admin authorization and backup/retention review. Crypto-erasure may make remaining encrypted backups undecryptable |

**Key version changes must be crash-safe.** Do not update a key ID on a record unless the corresponding new wrapped DEK is durable and its round-trip test succeeds. Rotation writes a new envelope or staged version then atomically promotes it; old envelopes remain available throughout the verified migration window. Track progress, failures, partial results and per-tenant audit events.

### 32.5 Recovery and disaster-recovery design

- **TPM loss/clear, motherboard replacement, VM move, PCR drift:** TPM-sealed material may be unavailable; use a **separate** recovery wrapping envelope or approved Vault/KMS escrow. A single TPM-bound key + database backup is **not recoverable**.
- Recovery kit should contain a versioned, authenticated and **encrypted** recovery envelope, key IDs/version metadata, verifier instructions and supported restore procedure; **never** include plaintext MEK, ordinary app-session secrets, provider API keys or a raw TPM export. For password-based recovery, use a memory-hard password KDF as supported and a genuinely high-entropy recovery passphrase; guard against offline guessing. Organizational escrow/quorum methods may be configured.
- A malicious actor with just the DB backup must not find the recovery passphrase or unwrap capability alongside it. Support secure detached/offline storage and acknowledge loss scenarios if an operator declines recovery.
- A **replacement-host drill** is a release gate: restore DB + object files + permitted recovery envelope, recover key material on new host, rewrap tenant keys, test integrations without exposing raw credentials in UI, confirm bootstrap still SEALED and former sessions remain appropriately invalidated. Never simulate success by simply re-entering all credentials.
- In environments that prohibit key export or escrow, require the enterprise to accept and document alternative re-entry procedure (and resulting downtime/loss of stored secret versions). Do not pretend recovery of an unrecoverable key is possible.
- Protection of the application database and **ASP.NET Core Data Protection key ring** remains a **separate** operational requirement for authentication sessions; a wizard-key backup does not automatically restore login cookies, and vice versa.

### 32.6 .NET contracts, trust boundaries and key agent

~~~csharp
public interface IKeyProtectionProvider
{
    Task<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> dekOrTenantKey,
        KeyContext context,
        CancellationToken cancellationToken);

    Task<UnwrappedKey> UnwrapAsync(
        WrappedKey wrappedKey,
        KeyContext context,
        CancellationToken cancellationToken);
}

public interface ISecretEncryptionService
{
    Task<SecretReference> StoreVersionAsync(
        TenantContext tenant, SecretPurpose purpose,
        ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken);

    Task<ScopedSecretLease> AcquireForProviderAsync(
        TenantContext tenant, SecretReference reference,
        SecretPurpose purpose, CancellationToken cancellationToken);
}
~~~

These are **illustrative contracts**, not compiled definitions; production interfaces must carry key version, explicit access policy, audit/correlation and safe key-buffer handling. Implementations may include \`WindowsCngTpmKeyProtectionProvider\`, \`LinuxTpm2TssKeyProtectionProvider\`, \`AzureKeyVaultKeyProtectionProvider\`, \`ExternalVaultKeyProtectionProvider\` and \`RecoveryEnabledSoftwareKeyProtectionProvider\`. TPM protection is **not** a mandatory microservice for deployments that use Vault/KMS.

For containerized on-prem TPM, prefer a **small host key agent** with narrowly authenticated local IPC and purpose-bound unwrap. The agent must verify caller identity and tenant/key authorization and refuse arbitrary decrypt/wrap operations; do not grant the entire web server container broad /dev/tpm0 or privileged Docker access by default. Defend against compromised app callers through minimum permissions, rate limits, audit and operational key rotation—but be explicit that runtime compromise can still intercept legitimately decrypted secrets.

### 32.7 Slice 0 product UX and explicit failure states

**Order:** secure one-time owner creation (§22 and [Slicing strategy](Slicing%20strategy.md)) → initial admin login → **Protect your installation** (choose/detect protector; TPM/Vault/software) → create/export protected recovery kit or configure enterprise escrow → verify recovery → activate MEK/KEK protection → third-party integration wizard (Foundry, SMTP, OIDC, embedding/LLM, etc.) → dashboard. App login/administrative recovery remains possible if key protection is temporarily unavailable; do not allow wizard secrets or provider jobs until the protector is healthy.

**UI states:** NOT_INITIALIZED; PROTECTOR_DETECTED; SETUP_IN_PROGRESS; RECOVERY_PENDING; RECOVERY_VERIFIED; ACTIVE; DEGRADED; LOCKED/RECOVERY_REQUIRED; ROTATING; RETIRED. Show the selected protector, security/recovery readiness, key version (non-secret), protected integration count and last tested status; **never display or download raw MEKs**. Users who skip optional TPM must explicitly choose a safe alternative; do not turn encryption off to improve onboarding. Only authorized deployment/tenant administrators may configure/rotate keys according to scope; enforce step-up MFA and second-person approval for destructive enterprise actions when required.

### 32.8 Security, operations and acceptance tests

- **Leakage:** database export, browser dev tools, logs, traces, exception handlers, CLI output and support bundles reveal no plaintext stored credential, KEK or recovery passphrase.
- **Crypto:** authenticated context tampering, altered ciphertext/tag, substituted tenant ID/purpose/key version, duplicate nonce, outdated wrapped DEK and forged key reference fail closed.
- **Isolation:** Tenant A API/session/worker cannot unwrap Tenant B's secrets. Provider adapter accesses only its allowed purpose; SaaS operator is not silently a tenant secret reader.
- **TPM:** Windows/Linux supported operations smoke-tested; TPM missing/cleared, PCR change, VM/hardware replacement and Docker key-agent permission failure are handled with safe diagnostics and independently validated recovery.
- **Lifecycle:** concurrent wizard saves, partial key-rotation crash, old-key retirement while referenced, restoration of stale backups, version replay, compromise response and actual provider credential rotation work under integration/fault tests.
- **SaaS:** Key Vault/KMS 429/outage and managed identity failures suspend secret-dependent work without insecure downgrade; multi-node requests respect tenant keys, version/lease rules and operational quotas.
- **Recovery:** restore to **different hardware** with documented offline kit/external KMS recovery, rewrap/test secrets, preserve the sealed Superadmin bootstrap state and verify audited access; test the denied-recovery condition.
- **Releases:** documented .NET crypto library choices, key service hardening, backup and disposal procedures; operating system TPM features and provider compatibility verified on supported distributions. No arbitrary throughput, cost or absolute anti-compromise guarantees.

The security controls in this section are required for **stored integration secrets in S0**, while TPM-specific and advanced multi-node providers are conditional implementation choices. See [Slicing strategy](Slicing%20strategy.md) for production UI sequence and vertical delivery gates.

## 33. Definition of done for the architecture

### 33.0 v1.5 wizard-secret and key-management gates

- Before saving any third-party integration secret, an encrypted provider-scoped secret store is ACTIVE, authenticated envelope encryption and tenant-bound key wrap checks pass, and authorized account recovery remains independent of optional SMTP/cloud AI.
- A self-hosted operator can select TPM protection where supported or an explicitly acknowledged secure alternative; the app cannot silently downgrade to plaintext or automatically unlock protected material after protector failure.
- An independent restore/recovery procedure allows replacement-host recovery when the tenant elected a recoverable policy; inaccessible key material is reported as such rather than silently reset.
- Key/credential rotation is versioned, journaled and testable without losing historical encrypted secret references; old key destruction respects retained data and backups.
- Unprivileged app users, other tenants and SaaS operators cannot retrieve stored integration secret plaintext or trigger unrestricted unwrap through debug endpoints, logs, IPC or API responses.

### 33.1 v1.4 evidence governance, integration and redaction gates

The following v1.4 capabilities must pass security and correctness tests before they are declared implemented:

- When a new READY document version modifies cited evidence, saved answers and immutable Evidence Brief revisions receive an auditable freshness assessment without corrupting original source citations. Confirmed conflicts require applicable authority/effective-date evidence and review, not mere lexical differences.
- Users can build and publish immutable Evidence Briefs from verified source-backed blocks; an independent offline verifier rejects tampering and labels missing/restricted source artifacts as not fully verifiable.
- The optional remote MCP server uses delegated/user or tenant-scoped service identity, strict OAuth resource/audience validation, existing RBAC and current redaction policy; no cross-tenant or raw-secret tool disclosure.
- PARTIAL/UNVERIFIED source data enters a reviewer queue; approved corrections create immutable human-verified overlays and fenced derived re-publications rather than overwriting originals or silently changing prior calculations.
- Unauthorized users never receive protected content through raw PDF layers, raster crops, metadata, caches, embeddings/search snippets, model context, exports, citations or agent-tool endpoints. Uncertain sanitization defaults to denial.
- Knowledge-health metrics distinguish absent evidence from OCR, retrieval, authorization and query ambiguity; tenant/private prompt content cannot be reconstructed from aggregate dashboards.

### 33.2 v1.3 distribution, identity and SaaS gates

- A self-hosted instance boots, configures its built-in identity, creates its first tenant and performs normal RAG operations without a vendor SaaS control-plane dependency.
- SaaS and self-hosted builds use the same authorization contracts and domain model; only documented deployment capabilities and operator services differ.
- A user with multiple tenant memberships can switch tenants safely; every retrieval, derivative image, citation, export, worker job and audit event is isolated to the active authorized tenant.
- Built-in accounts and generic/Entra OIDC are functional before I1 sign-off; refresh/session revocation and secure browser token handling pass authentication tests.
- Built-in and tenant-custom roles are checked against a machine-readable permission catalog and resource-scoped ACLs. Tenant admins and SaaS operators cannot bypass confidential content controls through their role names.
- SaaS usage plans/quotas and tenant suspension never elevate RBAC or destroy retained evidence without an audited policy-controlled workflow.
- Enterprise federation extensions (SAML/AD, SCIM, extra IdPs), stronger isolation and customer-managed AI provider features are considered done **only when implemented and tested**, rather than assumed from v1.3 design.

### 33.3 v1.0–v1.2 evidence-centric RAG baseline gates

- A PDF, HTML page, image, Markdown file and plain-text file can each be ingested with original bytes retained, observable processing status, typed elements, and supported source locators.
- The same content can be reprocessed and reindexed without losing immutable originals, previous citations or audit history.
- A query can return an original source image, structured table or textual answer with precise clickable evidence.
- Markdown and text citations open the **exact original line/span** even after normalization and multilingual indexing.
- A table computation yields exact values from validated cells, inherited units and scale, and shows its formula, exclusions and sources.
- Users can never retrieve or render documents they lack permission to access, including through caches and stale indexes.
- Uncertain extraction or insufficient evidence produces explicit diagnostics/abstention rather than plausible but unsupported assertions.
- Reliability, security and retrieval quality gates are automated and measured.
- Cloud OCR requests respect verified deployment page/byte limits, resume from page checkpoints, and preserve global source geometry across batches.
- Message redelivery after a worker crash can resolve claim-check artifacts without embedding large document payloads in broker messages.
- Query-visible answer blocks and their dependency groups are citation/grounding-validated before display; progress can stream immediately.
- Expired/stale processing workers cannot mutate committed stage results or publish an active version, even after broker lock redelivery.
- Authorization-aware search handles both broad collection access and narrow document overrides without leaking evidence or silently truncating filters.
- Table queries understand hierarchical headers, scale and units, and are executed inside bounded read-only database resource envelopes.
- Search is pinned to a validated active document/index generation and retains in-flight source integrity through cutover.

**Implementation order:** establish immutable storage, the CDM, provenance/locator fidelity, and extraction quality **before** optimizing the conversational UI. This is the foundation that makes future multimodal models and formats safely replaceable.
