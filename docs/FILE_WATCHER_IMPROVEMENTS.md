# File Watcher Improvements Required Before Building Ingestion

## Purpose

This document defines the improvements required in the **file watcher / document admission** layer before building the next stage of the RAG pipeline such as **document preprocessing, chunking, vectorization, and indexing**.

The goal is simple:

- keep the watcher **thin, fast, and reliable**
- make file admission **idempotent and safe**
- make sure ingestion starts only for **valid, stable, accepted, uniquely identified files**
- prevent bad input from poisoning chunking and embedding later

---

## Current State Summary

Based on the current implementation:

- `FileWatcher.py` watches a folder using `awatch()`
- only `Change.added` events are processed
- the watcher expects filename format: `originalName__{GUID}.ext`
- extension allowlist is used for acceptance
- MIME type is guessed using `mimetypes.guess_type()`
- SHA-256 is computed over raw bytes
- dedupe is based only on `FileMetadata.ContentHash`
- watcher directly creates a `file_ingestions` row with `Status='Pending'`
- upload row status is updated to `Processing`, `Processed`, `Duplicate`, `Rejected`, or `Error`
- unsupported files are deleted from disk

This is a good starting point, but it is still a **basic intake watcher**, not a production-grade preprocessing gateway.

---

# 1. Required Architectural Boundary

## 1.1 The watcher must remain an admission component

The watcher must **not** become the place where heavy parsing, OCR, chunking, or embedding happens.

### Watcher should do

1. detect file arrival
2. verify file is stable and fully written
3. validate file type using extension + content-based detection
4. compute exact binary hash
5. register admission metadata
6. decide exact duplicate vs accepted file
7. enqueue preprocessing / ingestion work
8. exit

### Watcher should not do

- OCR
- PDF parsing
- HTML cleanup
- DOCX text extraction
- chunking
- embedding
- vector DB writes
- LLM calls
- deep content normalization

### Why this boundary is required

If the watcher becomes heavy, it will:

- block file intake under load
- become difficult to retry safely
- mix admission failures with parsing failures
- increase corruption risk for partially written files
- become harder to scale independently

---

# 2. Mandatory Improvements

## 2.1 Add file stability detection before processing

### Problem

The watcher currently sleeps for `0.5` seconds and then processes the file. That is unsafe.

A file may still be:

- uploading
- copying
- being flushed by the backend
- incomplete because of slow disk or network

Hashing or validating a partially written file will create:

- wrong SHA-256 digest
- wrong file size
- corrupt MIME inference
- false duplicate / false unique decision

### Requirement

Before processing, the watcher must verify that the file is **stable**.

### Accepted strategies

Use one of these approaches:

#### Option A: stable size check

- read file size
- wait 1-2 seconds
- read file size again
- repeat for 2 or 3 rounds
- process only if size does not change

#### Option B: temp file then atomic rename

Backend writes to:

- `filename.tmp`

and only after write completion renames to:

- `filename__GUID.ext`

Watcher only watches final committed names.

#### Option C: explicit manifest or queue-based handoff

Upload API writes file fully, then inserts a DB/queue job that watcher/worker consumes.

### Recommended decision

Use **Option B or Option C** if you control the upload API. They are cleaner and more reliable than time-based polling.

---

## 2.2 Do not trust extension alone

### Problem

Current validation checks only file extension.

This is not enough because:

- renamed files can fake extensions
- malformed PDFs may still end with `.pdf`
- non-doc files may be uploaded with doc extensions
- malware or garbage files can pass extension validation

### Requirement

Acceptance must use both:

- **extension allowlist**
- **content-based MIME detection / file signature validation**

### Minimum required checks

- expected extension is supported
- detected MIME type matches extension family
- file is non-empty
- file is not obviously corrupt

### Example rules

- `.pdf` must resolve to `application/pdf`
- `.docx` should resolve to modern Office OpenXML family
- `.html` / `.htm` should look like HTML text, not binary
- `.txt`, `.md`, `.json`, `.csv`, `.tsv` should be decodable text

### Implementation note

`mimetypes.guess_type()` is based mostly on filename and is not enough for acceptance.
Use content-based MIME sniffing or signature checks.

---

## 2.3 Add basic structural validation per file type

### Problem

Even after MIME detection, a file may still be corrupt or unusable.

### Requirement

Add lightweight validators before enqueueing ingestion.

### Minimum validator expectations

#### PDF

- header exists
- file can be opened by a parser without immediate fatal error
- encrypted PDF should be marked separately

#### DOCX

- file is a valid ZIP container
- core Office structure exists

#### Text / Markdown / CSV / TSV / JSON / HTML

- decodable as text
- encoding identified or defaulted safely
- not empty after trimming

### Result handling

If basic structure fails:

- mark upload as `Rejected` or `ValidationFailed`
- store failure reason
- do not create downstream ingestion job
- do not silently delete evidence unless retention policy says so

---

## 2.4 Replace silent deletion with quarantine strategy

### Problem

Current watcher deletes unsupported files from disk immediately.

That is risky because:

- debugging becomes harder
- evidence is lost
- false rejections cannot be inspected
- operational audits become impossible

### Requirement

Rejected files must be moved to a **quarantine / rejected** folder instead of immediate deletion.

### Required behavior

- move file to `quarantine/unsupported/` or `quarantine/invalid/`
- record rejection reason in DB
- optionally retain for N days before cleanup

### Hard requirement

Never hard-delete files in the primary processing path unless there is a separate retention policy and cleanup job.

---

## 2.5 Separate binary identity from document/content identity

### Problem

Current dedupe uses only raw-byte SHA-256.

That is necessary but incomplete.

Two files may:

- contain the same text but have different binary bytes
- be same document exported twice with different metadata
- be OCRed vs native text versions of same content

### Requirement

Introduce two levels of identity.

### Level 1: binary hash

Use SHA-256 of raw bytes for exact physical dedupe.

Purpose:

- same file uploaded multiple times
- same bytes copied under different names

### Level 2: normalized content hash

Generate later during preprocessing from normalized extracted text.

Purpose:

- semantically identical text under different binary files
- same document exported differently

### Immediate action required now

Even if content hash is not implemented yet, schema and naming must anticipate it.

Add fields like:

- `BinaryHash`
- `ContentHashNormalized` or `NormalizedTextHash`

Do **not** overload one column for both concepts.

---

## 2.6 Status model must be expanded

### Problem

Current statuses are too coarse.

`Processed` currently means “watcher finished”, not “document is parsed, chunked, embedded, and searchable”.

That will become confusing fast.

### Requirement

Define status per pipeline stage.

### Recommended upload/admission statuses

- `Pending`
- `Detected`
- `Stabilizing`
- `Validating`
- `Accepted`
- `Rejected`
- `DuplicateBinary`
- `QueuedForPreprocessing`
- `AdmissionError`

### Recommended preprocessing / ingestion statuses

- `Pending`
- `Preprocessing`
- `Parsed`
- `Chunking`
- `Embedding`
- `Indexed`
- `DuplicateContent`
- `Failed`

### Rule

Do not use the same generic `Processed` label for multiple meanings.

---

## 2.7 Add error reason and rejection reason fields

### Problem

Current schema allows only one `ErrorMessage` in `file_ingestions`, but admission failures happen before ingestion and need explanation too.

### Requirement

Store structured failure reasons for every rejected or failed file.

### Minimum fields

For uploads or admission records, add:

- `FailureCode`
- `FailureMessage`
- `FailureStage`
- `LastAttemptAt`
- `RetryCount`

### Example failure codes

- `UNSUPPORTED_EXTENSION`
- `MIME_MISMATCH`
- `FILE_EMPTY`
- `FILE_NOT_STABLE`
- `CORRUPT_PDF`
- `ENCRYPTED_PDF`
- `ZIP_INVALID`
- `HASH_COMPUTE_FAILED`
- `DB_WRITE_FAILED`
- `DUPLICATE_BINARY`

---

## 2.8 Make processing idempotent and race-safe

### Problem

Multiple watcher events, restarts, or concurrent workers can process the same file more than once.

### Risk areas

- duplicate file events from OS
- app restart during `Processing`
- two workers handling same file
- DB race when inserting same hash at same time

### Requirement

Processing must be safe if called multiple times for the same file.

### Minimum requirements

- protect `FileMetadata.BinaryHash` with a DB uniqueness constraint
- protect ingestion job creation from duplicate inserts
- ensure repeated processing of same upload does not create duplicate downstream jobs
- use transaction boundaries correctly
- recover cleanly after restart

### Strong recommendation

Adopt a state transition rule like:

- only move from expected previous states
- do not blindly overwrite statuses

Example:

- `Pending -> Validating`
- `Validating -> Accepted`
- `Accepted -> QueuedForPreprocessing`

Not:

- set status directly without checking prior state

---

## 2.9 Stop doing blocking file I/O inside async flow

### Problem

`get_file_hash()` is declared async but performs blocking file I/O.

That means the async loop is not truly non-blocking.

### Requirement

Hashing and large file reads must be handled properly.

### Accepted approaches

- run blocking hash calculation in a thread pool
- use synchronous worker threads/processes instead of pretending it is async
- batch heavy I/O away from the watch loop

### Rule

The watch loop must stay responsive under large files and multiple arrivals.

---

## 2.10 Support more than only `Change.added`

### Problem

Current watcher reacts only to `Change.added`.

Real systems often produce:

- `added`
- `modified`
- rename/move completion patterns

If the upload path uses temp files and rename, the event pattern may differ.

### Requirement

Watcher logic must handle the actual arrival pattern your upload mechanism uses.

### Recommended behavior

- ignore temp names
- process only committed final names
- optionally react to `modified` when stabilizing
- de-bounce duplicate events per path

---

## 2.11 Add file size and safety limits

### Problem

There is no explicit safety policy for massive files, zip bombs, pathological HTML, or very large JSON/CSV payloads.

### Requirement

Define admission limits before ingestion is built.

### Required controls

- max file size
- max pages for PDFs if needed
- max text length for direct text-based documents
- reject or quarantine encrypted or password-protected files
- reject nested archives if archive support is not planned

### Example decision table

- under limit -> accept
- over limit but supported -> quarantine or special queue
- unsupported archive -> reject
- password-protected PDF -> reject or manual-review queue

---

## 2.12 Add canonical parser routing metadata

### Problem

The current watcher writes only MIME and hash, but downstream preprocessing will need parser decisions.

### Requirement

Admission must store enough metadata so downstream work knows what to do.

### Add fields like

- `DocumentType`
- `DetectedMimeType`
- `Extension`
- `Encoding`
- `ParserHint`
- `NeedsOcr` (nullable until preprocessing decides)
- `AdmissionVersion`

### Why

This avoids downstream ambiguity and supports reprocessing when logic changes.

---

## 2.13 Add versioning for admission/preprocessing logic

### Problem

Your logic will evolve. If you do not version it, you cannot tell which documents were admitted under old rules.

### Requirement

Track processing versions.

### Minimum fields

- `AdmissionVersion`
- `PreprocessingVersion`
- `ChunkingVersion`
- `EmbeddingModel`
- `EmbeddingVersion`

### Rule

Every file admitted into the system must be traceable to the logic version that handled it.

---

## 2.14 Preserve relationship between all uploads and canonical file metadata

### Problem

`FileMetadata.FirstUploadId` stores the first upload only.

That means later duplicate uploads are not explicitly linked to the canonical document except indirectly through the status.

### Requirement

All upload attempts must remain traceable.

### Recommended design

Keep:

- `uploads` = every physical upload occurrence
- `file_metadata` = canonical unique binary file
- mapping from upload to metadata for every upload, not only first

### Options

#### Option A
Add `ContentHash` or `BinaryHash` to `uploads`

#### Option B
Create a separate mapping table:

- `upload_file_metadata_map`
  - `UploadId`
  - `BinaryHash`
  - `RelationshipType` (`Primary`, `DuplicateBinary`)

### Why this matters

You will need this later for:

- UI visibility
- audit trails
- duplicate reporting
- reprocessing
- user-level analytics

---

## 2.15 Add retry design and dead-letter behavior

### Problem

If DB is temporarily down or file is locked, current logic may mark `Error` and stop.

### Requirement

Transient failures must retry; permanent failures must stop clearly.

### Required design

- classify errors into transient vs permanent
- retry transient errors with capped attempts
- move exhausted failures to terminal state
- keep reason and attempt count

### Examples

#### Retryable

- file locked
- temporary DB outage
- network storage hiccup

#### Non-retryable

- unsupported file type
- corrupt file
- GUID parse failure for malformed name
- MIME mismatch

---

## 2.16 Add startup recovery logic

### Problem

If the service crashes after setting `Processing` but before commit completion, records may remain stuck.

### Requirement

On startup, recover stale in-flight items.

### Minimum startup checks

- find uploads stuck in transitional statuses for too long
- inspect whether file still exists
- requeue safely or mark failed with recovery reason

### Example states to reconcile

- `Stabilizing`
- `Validating`
- `Processing`
- `QueuedForPreprocessing` without downstream job

---

## 2.17 Improve logging and observability

### Problem

Current logging is functional but not rich enough for operational debugging.

### Requirement

Every file admission should be traceable end to end.

### Log fields to include

- `UploadId`
- `StoredFileName`
- `OriginalFileName`
- `FilePath`
- `Extension`
- `DetectedMimeType`
- `BinaryHash`
- `FileSize`
- `StatusBefore`
- `StatusAfter`
- `FailureCode`
- `DurationMs`

### Metrics to add

- files detected per minute
- files accepted
- files rejected
- duplicate binary files
- avg validation time
- avg hash time
- admission errors

---

# 3. Database / Schema Changes Required

## 3.1 Uploads table improvements

### Add columns

- `BinaryHash` nullable initially
- `DetectedMimeType`
- `Extension`
- `FailureCode`
- `FailureMessage`
- `FailureStage`
- `RetryCount` default 0
- `LastAttemptAt`
- `AdmissionVersion`
- `QuarantinePath` nullable

### Clarify status semantics

Use upload status only for **admission stage**.
Do not let this table pretend to reflect complete searchability lifecycle.

---

## 3.2 FileMetadata table improvements

### Rename / clarify current fields

Current `ContentHash` is actually behaving as **binary hash**.
Rename it logically to `BinaryHash` if possible.

### Add columns

- `Extension`
- `DetectedMimeType`
- `OriginalMimeTypeSource` (`extension`, `sniffed`, `parser`)
- `IsEncrypted`
- `IsTextBased` nullable
- `PageCount` nullable
- `ContentHashNormalized` nullable
- `ParserHint`
- `CreatedByAdmissionVersion`

---

## 3.3 FileIngestion table improvements

### Add columns

- `Stage`
- `AttemptCount`
- `WorkerId`
- `QueueMessageId` or `JobId`
- `PreprocessingVersion`
- `ChunkingVersion`
- `EmbeddingModel`
- `EmbeddingVersion`

### Clarify purpose

`file_ingestions` should represent downstream processing, not watcher admission.

---

# 4. Required Refactor in Processing Flow

## 4.1 Recommended target flow

### Upload API

1. receive file
2. save to temp location
3. finalize file atomically into watch location
4. insert upload record as `Pending`

### Watcher

1. detect file
2. verify naming and UploadId
3. stabilize file
4. validate extension + MIME + basic structure
5. compute binary hash
6. upsert canonical metadata
7. mark duplicate or accepted
8. enqueue preprocessing job
9. update upload status

### Preprocessing worker

1. parse document
2. decide OCR / text mode
3. normalize text
4. compute normalized content hash
5. write preprocessing manifest
6. enqueue chunking

### Ingestion worker

1. chunk
2. embed
3. persist vectors + chunk metadata
4. mark searchable

---

# 5. Clear Definition of Done Before Building Chunking/Vectorization

You should **not** start chunking/vectorization until all of the following are true.

## 5.1 Functional requirements complete

- [ ] watcher processes only stable files
- [ ] extension validation is combined with content-based MIME detection
- [ ] basic structural validation exists for supported types
- [ ] unsupported or invalid files are quarantined, not silently deleted
- [ ] binary dedupe is idempotent and race-safe
- [ ] upload status model is clearly separated from ingestion status model
- [ ] failure reason is stored for all rejected/failed items
- [ ] every upload remains traceable to canonical metadata
- [ ] retries and startup recovery exist
- [ ] logs include correlation identifiers and failure codes

## 5.2 Operational requirements complete

- [ ] duplicate events do not create duplicate metadata rows
- [ ] service restart does not orphan files in transitional states
- [ ] large file hashing does not block the watcher loop badly
- [ ] transient DB/file lock issues can retry safely
- [ ] permanent failures end in explicit terminal states

---

# 6. Test Scenarios

This section lists the mandatory test scenarios before moving to chunking and embedding.

## 6.1 Happy path tests

### Test HP-1: valid new PDF

**Goal**: confirm new supported file is accepted and queued.

**Input**:
- a valid text-based PDF
- correctly named: `report__{GUID}.pdf`

**Expected**:
- upload status transitions through admission states
- binary hash is computed
- metadata row created once
- preprocessing/ingestion job created once
- no quarantine

---

### Test HP-2: valid new DOCX

**Goal**: confirm DOCX validation works.

**Expected**:
- accepted
- metadata persisted
- downstream job queued

---

### Test HP-3: valid new markdown/text file

**Goal**: confirm text-based formats work and are not treated as binary garbage.

**Expected**:
- accepted
- encoding handled correctly
- no parser failure

---

## 6.2 Duplicate tests

### Test DUP-1: exact same file uploaded twice

**Goal**: verify binary dedupe.

**Steps**:
1. upload file A
2. upload exact same bytes again under different name

**Expected**:
- first upload accepted
- second upload marked `DuplicateBinary`
- canonical metadata row remains single
- second upload still linked to canonical file
- downstream job not duplicated

---

### Test DUP-2: same file event emitted multiple times

**Goal**: verify event de-bounce/idempotency.

**Expected**:
- only one canonical insert
- only one downstream job
- no crash

---

### Test DUP-3: two workers race on same file

**Goal**: verify DB uniqueness and transaction safety.

**Expected**:
- one winner creates metadata/job
- other resolves cleanly as duplicate or no-op
- system remains consistent

---

## 6.3 File stability tests

### Test STAB-1: process partially written file

**Goal**: ensure watcher does not hash early.

**Method**:
- copy a large file slowly into watch directory or write in chunks

**Expected**:
- watcher waits for stability or final rename
- final hash matches completed file
- no premature rejection/duplicate decision

---

### Test STAB-2: temp-to-final rename flow

**Goal**: ensure committed-file workflow works.

**Expected**:
- temp file ignored
- final renamed file processed once

---

## 6.4 Validation and rejection tests

### Test VAL-1: unsupported extension

**Input**:
- `.exe` or unsupported archive disguised in watch folder

**Expected**:
- rejected
- moved to quarantine
- rejection reason stored
- no metadata row
- no downstream job

---

### Test VAL-2: extension says PDF but content is not PDF

**Input**:
- rename a text file to `.pdf`

**Expected**:
- MIME/signature mismatch detected
- rejected with `MIME_MISMATCH`
- quarantined

---

### Test VAL-3: corrupt PDF

**Input**:
- damaged/truncated PDF

**Expected**:
- structural validation fails
- rejected or validation failed
- reason stored

---

### Test VAL-4: empty file

**Input**:
- zero-byte `.txt` or `.pdf`

**Expected**:
- rejected with `FILE_EMPTY`

---

### Test VAL-5: encrypted PDF

**Expected**:
- terminal state according to policy
- clearly marked as encrypted / manual review / rejected

---

## 6.5 Failure and retry tests

### Test RET-1: DB unavailable during metadata insert

**Goal**: verify retry behavior.

**Expected**:
- transient failure logged
- retry attempted according to policy
- file not silently lost

---

### Test RET-2: file locked temporarily

**Expected**:
- retry if lock is transient
- no corrupt partial state

---

### Test RET-3: permanent malformed filename without GUID

**Expected**:
- terminal rejection
- clear failure reason
- no retry loop forever

---

## 6.6 Restart and recovery tests

### Test REC-1: service crashes after setting status to processing

**Method**:
- kill worker mid-processing

**Expected**:
- startup recovery identifies stale item
- item is retried or reconciled safely
- no orphan `Processing` rows forever

---

### Test REC-2: crash after metadata insert but before ingestion job insert

**Expected**:
- reconciliation detects mismatch
- exactly one downstream job exists after recovery

---

## 6.7 Throughput tests

### Test PERF-1: burst of 100 small files

**Expected**:
- no missed files
- acceptable throughput
- no duplicate job creation

---

### Test PERF-2: mix of 20 large and 100 small files

**Expected**:
- watcher loop remains responsive
- large hashing does not starve smaller files badly

---

### Test PERF-3: duplicate-heavy workload

**Expected**:
- duplicates resolve efficiently
- metadata table remains consistent

---

# 7. Clear Steps to Test

This section gives a practical way to test the watcher improvements.

## 7.1 Test environment setup

1. create separate folders:
   - `uploads/incoming`
   - `uploads/quarantine`
   - `uploads/processed` if needed
2. use a dedicated test database
3. enable verbose logs
4. seed clean tables before each test run
5. prepare sample files:
   - valid PDF
   - corrupt PDF
   - encrypted PDF
   - valid DOCX
   - fake PDF
   - zero-byte text file
   - very large text or PDF
   - duplicate copies of same file

---

## 7.2 Manual test steps for each scenario

For every test case:

1. note initial DB row counts in:
   - `uploads`
   - `file_metadata`
   - `file_ingestions`
2. place or upload the file into the system
3. observe logs from watcher
4. inspect final file location:
   - watch folder
   - quarantine folder
5. inspect DB rows and statuses
6. verify expected counts:
   - upload count
   - metadata count
   - ingestion job count
7. verify failure code / message if rejected
8. repeat same test twice to confirm idempotency

---

## 7.3 Automated integration test strategy

Build automated tests around the watcher.

### Recommended automated test layers

#### Layer A: unit tests

Test pure functions / helpers:

- GUID extraction from filename
- extension validation
- MIME validation rule matching
- file stability checker
- state transition guard logic
- failure classification

#### Layer B: integration tests with real DB

Test end-to-end admission against a test database:

- insert upload row
- write file into watched directory
- run watcher cycle
- verify DB state + file movement

#### Layer C: concurrency tests

- same file processed by two parallel tasks
- repeated events on same path
- service restart recovery

---

# 8. Implementation Priority Order

Implement in this order.

## Phase 1 - correctness first

- [ ] add stable file detection
- [ ] replace extension-only check with content-based MIME validation
- [ ] add basic structural validators
- [ ] replace deletion with quarantine
- [ ] clarify status names
- [ ] store failure codes/messages

## Phase 2 - idempotency and safety

- [ ] add DB uniqueness constraints and upsert-safe logic
- [ ] add retry vs terminal failure classification
- [ ] add startup reconciliation for stuck files
- [ ] add mapping between all uploads and canonical file metadata

## Phase 3 - operational readiness

- [ ] improve logs and metrics
- [ ] move blocking hash logic out of async hot path
- [ ] add throughput and concurrency testing
- [ ] add versioning fields

## Phase 4 - enable preprocessing

Only after all above is stable:

- [ ] add parser routing metadata
- [ ] introduce preprocessing worker
- [ ] add normalized content hash
- [ ] then start chunking/vectorization work

---

# 9. Final Recommendation

Do **not** start chunking and vectorization yet.

First make the watcher layer trustworthy.

If admission is weak, every later stage becomes harder:

- bad hashes
- false duplicates
- corrupt files entering chunking
- lost audit trail
- unclear status meanings
- retry/recovery pain

The correct path is:

1. harden watcher admission
2. add clean preprocessing boundary
3. then build chunking and embedding

That order will save rework.
