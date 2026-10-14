# Enterprise Multimodal RAG Platform — Architecture

> **Status:** Proposed architecture (implementation specification, not a claim of completed functionality)  
> **Version:** 1.0  
> **Date:** 2026-10-15  
> **Repository:** sks710931/rag-pipeline  
> **Target:** Enterprise, hybrid deployment, approximately 10,000 initial documents, multilingual, cloud AI models

## 1. Purpose and architectural principles

Build an enterprise-grade multimodal Retrieval-Augmented Generation (RAG) platform that ingests PDF, HTML, images, Markdown, and plain-text files; faithfully preserves their information; and delivers grounded answers with clickable source citations, original images, visual search, document comparison, and exact table calculations.

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
9. **Reprocessing is reproducible.** Extraction, OCR, embeddings, and indexes are versioned with input hashes, software versions, configuration, and processing manifests.
10. **Providers are replaceable.** Parser, OCR, embedding, reranking, generative model, queue, object store, and index have adapters and versioned contracts.

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
- Multi-user access control, collections, audit logs, and enterprise identity integration.
- Multilingual documents and multilingual query/answer support.
- Observability, replay/reindex, cost monitoring, quality gates, and failure recovery.

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
| Document intelligence | Python workers | Docling first, specialized parser/OCR fallback, PyMuPDF for page rendering/crops |
| Advanced layout/OCR | Azure AI Document Intelligence adapter | Optional per-page/per-document fallback for low-confidence or difficult material |
| Vision, generation | OpenAI or Azure OpenAI cloud APIs | Configurable by task/model policy; structured answer contract |
| Text embeddings | Configurable multilingual text embedding adapter | Store profile, dimensions, model/version, preprocessing |
| Image/multimodal embeddings | Provider supporting compatible text/image embedding spaces | Required for genuine text-to-image and image-to-image retrieval; do not substitute text-only embeddings |
| Retrieval | Qdrant + PostgreSQL lexical/structured retrieval | Filtered vector search, fusion and exact data queries; replaceable indexes |
| System of record | PostgreSQL | Document versions, element graph, ACLs, provenance, processing and audit |
| Binary objects | MinIO locally; Azure Blob in cloud | Original and derived immutable assets |
| Messaging | RabbitMQ locally; Azure Service Bus in cloud | Durable queues via shared messaging contract |
| Authentication | OIDC with configurable enterprise IdP | External identity and group mapping |
| Telemetry | OpenTelemetry + centralized metrics/logging/traces | End-to-end per-stage correlation |
| Deployment | Docker Compose locally; Azure Container Apps or Kubernetes | Hybrid, with cloud inference and optional managed infrastructure |

**Deployment rule:** Keep the document system, metadata, object storage, workers, and vector index independently deployable. Cloud AI processing is an explicit data-egress boundary and must comply with per-collection model/data policies.

Do not hardcode one exact external model version into the domain. Pin actual provider model IDs, API versions, parser releases, image embedding dimensions and pricing via deploy configuration and record them in processing runs.

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

1. **Identity & Access:** users, memberships, groups, roles, ACL inheritance, resource authorization, tenant policy.
2. **Document Management:** upload/import, logical identity, immutable versions, hashing/deduplication, document lifecycle.
3. **Ingestion:** stage orchestration, retries, checkpoints, extraction policy, parser routing.
4. **Evidence Catalog:** CDM elements, source assets, relations, locators, corrections, lineage.
5. **Indexing:** retrieval units, embedding profiles, index generations, backfill and publication.
6. **Search:** permission-filtered lexical/vector/visual search, candidate merging, reranking.
7. **Query & Answer:** intent planning, evidence packing, model gateway, grounded structured output and citation verification.
8. **Table Analytics:** typed cells, calculation plans, verified deterministic execution, result provenance.
9. **Operations & Audit:** metrics, traceability, diagnostic events, retention, reprocessing and governance.

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

Use Docling as primary structural parser, PyMuPDF for rendering and geometry, and an optional Azure Document Intelligence fallback by page/region/document. Route based on diagnostics or evaluation, not an assumption one engine is always best.

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
    P["PDF parser"]
    H["HTML parser"]
    I["Image parser"]
    MD["Markdown AST parser"]
    T["Plain-text parser"]
    N["Normalize -> CDM + locators"]
    C["Check quality/coverage and assets"]
    F{"Accepted?"}
    R["Fallback / manual review / partial status"]
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

- Every job has stable job ID, version ID, stage ID, processing profile hash and idempotency key.
- Store stage outputs in versioned, content-addressed objects and manifest records before acknowledging work.
- Use transactional outbox to publish messages tied to metadata commits; assume at-least-once delivery.
- Reprocessing creates a new processing run and index generation; do not mutate past lineage.
- Quarantine malicious/oversized input and apply stage-specific timeouts, concurrency limits, exponential backoff and dead-letter handling.
- Commit publication only when mandatory assets, element graph, ACL projections and retrieval indexes are valid; keep the older ready version visible until cutover.
- Allow explicit reindex without re-upload or reparse if source/CDM remains compatible.

## 8. Persistent data model

PostgreSQL is the system of record; use UUID/ULID identifiers consistently and define all foreign keys, indexes, unique constraints and tenant consistency checks in migrations.

| Table / aggregate | Main data |
| --- | --- |
| tenants | Settings, security policy, model/data egress rules |
| users, groups, group_members, memberships | External identity mapping and tenant/group membership |
| collections, access_policies | Collection scope, ACL inheritance, principals and grants |
| documents | Logical document identity, tenant/collection, active ready version |
| document_versions | Immutable hash, MIME, parser profile, original asset, processing/publication status |
| document_pages_or_views | Page geometry, rendered HTML snapshot, source text viewport metadata |
| document_elements | Typed hierarchy, reading order, source/derived/inferred data, extraction run |
| element_relationships | Typed element-to-element relations |
| evidence_locators | PDF polygons, DOM anchors, image regions, Markdown/text line and offset ranges |
| assets | Immutable binaries, size/hash/type, original/derived classification |
| extracted_tables, table_rows, table_cells | Cell spans, headers, validated typed values, units/currency, evidence locators |
| retrieval_units, retrieval_unit_elements | Search text/metadata and exact underlying element membership |
| embedding_profiles, index_generations, index_entries | Provider/model settings, vector IDs, generation/version and state |
| processing_runs, processing_events, job_outbox | Input profile, outputs, telemetry and durable workflow |
| conversations, queries, answers | Query/answer versions and provenance |
| answer_claims, answer_citations | Evidence-backed claim references and semantic support verification |
| calculation_runs, calculation_inputs | Authorized deterministic expression, contributing input values and result |
| audit_events, corrections | Actor, action, targets, version history and review |

### 8.1 Core schema constraints

- Tenant ownership is mandatory. Validate tenant consistency across every document, element, asset, retrieval unit, calculation, and citation relation; do not rely solely on application joins.
- A document version is immutable once published. A new upload/revision creates a new version ID.
- The logical document points to one active READY version. Never point it to an incomplete index generation.
- Each retrieval unit maps to one or more CDM element IDs; each index entry records its source retrieval unit and generation.
- Store exact-value table cells using NUMERIC/decimal fields plus original literal, locale, unit/currency and verification state; no floating-point arithmetic for money.
- Evidence locators have format-specific validation (PDF page bounds; image pixel bounds; Markdown/text valid source span and encoding; HTML snapshot/anchor existence).
- Answers link claims to authorized, resolvable evidence IDs, never opaque arbitrary URLs.
- Support row-level security as defense in depth, with vetted service roles and explicit tenant context; avoid a privileged application role that bypasses all checks.

### 8.2 Binary object layout

~~~text
tenant/{tenantId}/documents/{documentId}/versions/{versionId}/
  originals/source.ext
  manifests/ingestion.json
  extraction/{runId}/raw-parser-output.json
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

Qdrant and lexical indexes are rebuildable projections. Index entries store tenantId, documentVersionId, retrievalUnitId, modality, indexGenerationId and security filter metadata; ACL changes require correct refresh/invalidation and fail-closed checks. Do not trust stale vector payloads to grant access.

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

## 12. React UI and evidence navigation

Workspaces:

1. **Search/Chat:** multilingual queries, optional image uploads, filters, typed answer blocks, original figures/tables and clickable citations.
2. **Evidence Viewer:** split view for answer/source, PDF.js page overlays, HTML snapshots, image zoom/region overlays, Markdown raw/rendered source spans, text line highlighting.
3. **Document Explorer:** files, collection hierarchy, outlines, extracted elements, tables/figures, metadata, versions and indexed status.
4. **Extraction Inspector:** original vs parsed layout, warnings, missing assets, OCR regions, table reconstruction, corrected annotations.
5. **Admin:** collections, policies, roles, ingestion retries, parser profiles, model configurations, usage/cost, audit and evaluations.

**Markdown/text viewer requirements:** show line numbers, exact original line breaks, Raw/Rendered toggle for Markdown, highlighted citation ranges, copy source selection, and safe rendering that never runs embedded scripts or code.

Use generated OpenAPI TypeScript clients, strict compiler checks, discriminated unions, and runtime validation at external boundaries. Avoid directly exposing database or model-provider DTOs as frontend contracts.

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

API requirements: OIDC bearer authentication, resource authorization, opaque identifiers, problem-details errors, pagination, idempotency keys for mutating requests, cancellation, rate limits, request IDs, and streaming error semantics. Uploaded files use short-lived upload sessions rather than pushing large binaries through query endpoints.

Search supports query text, optional image asset reference, collection/document scope, mode (auto/text/visual/structured), language, filters, and response preferences. Server determines effective authorized scope regardless of caller-supplied identifiers.

## 14. Identity, tenant isolation and security

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

PostgreSQL transactionally records authoritative metadata and outbox messages; workers use idempotent at-least-once handlers. External object/index writes cannot be in the same ACID transaction, so use staging generations, reconciliations and atomic metadata publication.

**Publication invariant:** A document version is READY only if its mandatory original asset, CDM elements, locator integrity, required retrieval/index generation, and ACL projection pass validation. An older READY version remains served until a replacement is healthy.

Deletion must block new reads first (tombstone + authorization), then remove projections/assets asynchronously with auditable completion. Revocations apply immediately to query/evidence/asset authorization even before all search projections refresh.

Record parser/model failures by stage; never claim partial OCR/visual coverage is complete. Provide operational reconciliation tasks for orphan assets, stale index entries, unresolved citations and incomplete processing.

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
| Calculations | Correct outputs for validated inputs | 100% |
| Isolation | Cross-tenant/document unauthorized leaks | 0 |
| Publication consistency | Missing mandatory ready assets/indexes | 0 |
| Markdown/text citations | Correct original line/span resolution | 100% |

These are **targets, not measured performance or universal guarantees**. Evaluate per language, format and document difficulty; publish failure/abstention rates separately. Use golden files for source-offset reconstruction (especially Markdown CRLF, Unicode, BOM, tabs, footnotes, tables, code fences and relative links).

Automated resilience tests: worker crash at each stage; duplicate delivery; same-file concurrent upload; malformed document; unavailable embedding/model provider; revoked permissions mid-query; index cutover; missing original asset; concurrent reprocess; tombstone during active streaming; HTML/Markdown XSS/prompt-injection payloads.

Track ingestion throughput and p95 latency, source coverage, bad/missing asset references, retrieval Recall@K/nDCG, claim support, citation resolution, per-query tokens/cost, queue age, retries and tenant-level errors. Establish latency SLOs only after realistic load tests.

## 17. Capacity and hybrid deployment

Initial planning size: **10,000 documents**; actual compute and storage depend on pages, images, tables, OCR proportion, scanned languages and generated retrieval units. A hypothetical 100 pages/document gives approximately 1 million pages; this is a sizing scenario, not an input guarantee.

**Development:** Docker Compose with ASP.NET API, React app, Python workers, PostgreSQL, Qdrant, MinIO, RabbitMQ, and observability. Provider credentials supplied by secrets/environment injection.

**Production:** Azure Container Apps or Kubernetes for API/workers, managed PostgreSQL, Blob storage, Service Bus and appropriate managed/hosted vector infrastructure. Use horizontal worker queues split by resource class (native extraction, OCR, vision, indexing); separately throttle expensive cloud AI calls.

Run ingestion asynchronously with backpressure, quotas, provider retry budgeting, model routing and per-tenant usage reporting. Keep index/asset formats portable between local and cloud adapters.

## 18. Implementation roadmap

### I1 — Platform foundations
- Repository structure, architecture decision records, contracts, Docker stack and CI.
- OIDC tenant-aware auth, collection/document metadata, upload sessions, immutable originals and versioning.
- Durable queue/outbox, status tracking, audit and basic admin UI.
- **Gate:** immutable uploads, tenant isolation and stage recovery work.

### I2 — Canonical extraction and provenance
- CDM v1 with element types, typed relationships and evidence locators.
- PDF native/OCR/table/figure parsing with coverage manifests.
- **Markdown parser with full AST/raw source preservation** and line/span citation anchors.
- **Plain-text parser with encoding/newline preservation** and line/span citation anchors.
- Original source viewers and extraction inspector.
- **Gate:** real extracted content resolves to precise PDF/Markdown/text source ranges.

### I3 — Grounded hybrid RAG
- Retrieval units, text embeddings, lexical index, Qdrant and fused/reranked retrieval.
- Access-filtered evidence lookup and structured answer contract.
- Citation validator, grounded Q&A and SSE streaming.
- **Gate:** citation/reference integrity and retrieval evaluation thresholds pass.

### I4 — Full multimodal + HTML
- Standalone image parsing, OCR, vision descriptions, true multimodal embeddings and image-image queries.
- HTML parsing, sanitized DOM snapshots and optional isolated browser rendering.
- Exact original-figure/image answers and source overlays.
- **Gate:** visual queries display authorized originals; HTML/Markdown are safe to render.

### I5 — Tables, comparisons, multilingual
- Cell-level typed extraction, verified calculations and arithmetic traceability.
- Cross-document/version comparisons with conflict reporting.
- Language-aware indexing, code and Markdown quality improvements.
- **Gate:** exact validated calculations, cross-document citations and multilingual evaluation pass.

### I6 — Enterprise readiness
- ACL hardening, audit and retention, model egress policy, backups/restore, production Azure deployment.
- Throughput/cost tuning, recovery tests, security tests, operational dashboards and release gates.
- **Gate:** documented SLOs, security invariants, quality targets and disaster recovery verified.

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
│   └── api/                        # ASP.NET Core modular monolith
├── src/
│   ├── BuildingBlocks/
│   ├── IdentityAccess/
│   ├── DocumentManagement/
│   ├── Ingestion/
│   ├── EvidenceCatalog/
│   ├── Search/
│   ├── QueryEngine/
│   └── TableAnalytics/
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

Later extensions can add DOCX/PPTX/XLSX, Git repositories, remote crawlers, incremental sync, human annotation workflows and knowledge-graph features without changing the core provenance contracts.

## 21. Definition of done for the architecture

A working platform is acceptable only when:

- A PDF, HTML page, image, Markdown file and plain-text file can each be ingested with original bytes retained, observable processing status, typed elements, and supported source locators.
- The same content can be reprocessed and reindexed without losing immutable originals, previous citations or audit history.
- A query can return an original source image, structured table or textual answer with precise clickable evidence.
- Markdown and text citations open the **exact original line/span** even after normalization and multilingual indexing.
- A table computation yields exact values from validated cells and shows its formula and sources.
- Users can never retrieve or render documents they lack permission to access, including through caches and stale indexes.
- Uncertain extraction or insufficient evidence produces explicit diagnostics/abstention rather than plausible but unsupported assertions.
- Reliability, security and retrieval quality gates are automated and measured.

**Implementation order:** establish immutable storage, the CDM, provenance/locator fidelity, and extraction quality **before** optimizing the conversational UI. This is the foundation that makes future multimodal models and formats safely replaceable.
