# File Watcher Improvements Review

Date: 2026-04-19

Source checklist: `docs/FILE_WATCHER_IMPROVEMENTS.md`

Reviewed areas:

- `worker/FileWatcher.py`
- `worker/models.py`
- `backend/src/services/file_service.py`
- `backend/src/models/*.py`
- `backend/src/controllers/ingestion_controller.py`
- `sql/*.sql`
- `tests/test_file_stability.py`
- `tests/test_mime_validation.py`
- `tests/test_structural_validation.py`
- `tests/test_admission_error_and_idempotency.py`
- `tests/test_watcher_async_flow_and_safety.py`
- `docs/ARCHITECTURE.md`
- `docs/SUPPORTED_FILES.md`

## Executive Summary

The file watcher has been significantly hardened compared with the "current state" described in `FILE_WATCHER_IMPROVEMENTS.md`, but the full improvement set is not complete.

Overall status:

- Implemented: 11 items
- Partially implemented: 6 items
- Not implemented: 2 items

The most important remaining risks are:

1. Retry/dead-letter behavior is not implemented beyond a startup reset of some statuses.
2. The ingestion controller still references old model fields (`ContentHash`, `MimeType`) that no longer exist.
3. Automated coverage now exists for file stability, MIME rules, structural validators, admission idempotency, async event scheduling, and safety limits, but restart recovery and end-to-end throughput scenarios are still mostly untested.
4. Parser routing, startup reconciliation, retry/dead-letter behavior, and structured observability remain incomplete.

## High-Risk Findings

### 1. Ingestion API still uses removed field names

Status: Not implemented for compatibility

Evidence:

- `backend/src/controllers/ingestion_controller.py:12` joins `FileIngestion.ContentHash == FileMetadata.ContentHash`.
- Current models define `BinaryHash`, not `ContentHash`, in `backend/src/models/file_ingestion.py:11` and `backend/src/models/file_metadata.py:9`.
- `backend/src/controllers/ingestion_controller.py:25` reads `meta.MimeType`, but current metadata uses `DetectedMimeType` at `backend/src/models/file_metadata.py:11`.

Impact:

The ingestion listing endpoint will fail at runtime once exercised. This also means the downstream lifecycle visibility is not aligned with the watcher/schema refactor.

Recommended fix:

- Join on `FileIngestion.BinaryHash == FileMetadata.BinaryHash`.
- Return `meta.DetectedMimeType`.
- Include `Stage` in the response so upload/admission and ingestion stage status remain distinct.

## Requirement-by-Requirement Status

| ID | Requirement | Status | Evidence / notes |
| --- | --- | --- | --- |
| 1.1 | Keep watcher as admission component only | Implemented | The watcher validates, hashes, dedupes, creates metadata, and queues a pending ingestion row. It does not do OCR, chunking, embedding, vector DB writes, or LLM calls. |
| 2.1 | File stability detection before processing | Implemented | Backend writes to `.tmp`, commits the upload DB row, and only then atomically renames to the final watched name. The watcher ignores `.tmp` and quarantine files, defers if the upload row is not visible, marks visible uploads as `Stabilizing`, waits for repeated stable size/mtime checks, resets unstable files to `Pending`, and only proceeds to `Validating` after stability is confirmed. Focused tests cover DB-before-rename ordering, changing-size rejection, and missing-row deferral. |
| 2.2 | Extension plus content-based MIME validation | Implemented | The watcher now combines the extension allowlist with explicit content validation. Binary document extensions are checked by signature/family rules (`.pdf`, `.doc`, `.docx`, `.odt`, `.rtf`), text-like extensions must be decodable text, HTML must look like HTML, binary signatures are rejected for text extensions, and mismatches are quarantined with `MIME_MISMATCH`. Accepted metadata stores the content-derived `DetectedMimeType` and source. Focused tests cover spoofed PDF text, valid PDF signature, non-HTML text renamed to `.html`, and binary content renamed to `.txt`. |
| 2.3 | Basic structural validation per file type | Implemented | The watcher now performs lightweight per-type structural validation after MIME acceptance: PDF header/parser/encryption checks, DOCX ZIP/core Office XML checks, ODT ZIP/mimetype/content XML checks, DOC OLE header checks, RTF header/body checks, JSON parsing, CSV/TSV row consistency checks, HTML tag checks, text decoding with fallback encodings, and non-empty-after-trim checks. Focused tests cover corrupt PDF, DOCX valid/invalid, ODT valid/invalid, invalid DOC/RTF, invalid/valid JSON, inconsistent CSV, valid HTML, and whitespace-only text. |
| 2.4 | Quarantine instead of silent deletion | Implemented | Rejected files are moved with `shutil.move()` to `uploads/quarantine` and `FailureCode`/`QuarantinePath` are saved (`FileWatcher.py:129-147`). No hard delete was found in the watcher. |
| 2.5 | Separate binary identity from normalized content identity | Implemented | Models and SQL use `BinaryHash` as the primary binary identity and include `ContentHashNormalized` for later normalized text identity (`file_metadata.py:9,17`; `recreate_schema.sql:36,44`). |
| 2.6 | Expanded status model | Partial | Upload statuses now include `Pending`, `Stabilizing`, `Validating`, `Accepted`, `Rejected`, `DuplicateBinary`, and `AdmissionError`. Missing or unused recommended states include `Detected` and `QueuedForPreprocessing`. Ingestion stage fields exist but no downstream lifecycle is implemented yet. |
| 2.7 | Error/rejection reason fields | Implemented | Upload model has `FailureCode`, `FailureMessage`, `FailureStage`, `LastAttemptAt`, and `RetryCount`. The watcher now uses a shared failure update helper for unsupported extensions, MIME mismatches, structural failures, unstable files, hash failures, DB write failures, duplicate binaries, quarantine failures, and internal errors. Rejected/failed/duplicate uploads receive structured code, message, stage, and attempt timestamp fields. |
| 2.8 | Idempotent and race-safe processing | Implemented | Upload processing is now claimed with guarded `Pending -> Stabilizing -> Validating` updates, so duplicate events or multiple workers cannot blindly overwrite terminal statuses. `FileMetadata.BinaryHash` remains the canonical primary key, metadata insert `IntegrityError` collisions are resolved as duplicate-binary outcomes, and `file_ingestions.BinaryHash` is protected by `UQ_FileIngestions_BinaryHash` in ORM models and SQL schema scripts. |
| 2.9 | Avoid blocking file I/O in async flow | Implemented | The async watcher now acts as a coordinator: candidate checks run in `asyncio.to_thread()`, the full DB/file admission path runs in `_process_file_sync()` through `asyncio.to_thread()`, startup recovery is offloaded to a worker thread, and watch events create independent admission tasks instead of serially awaiting each file in the watch loop. Blocking hash, MIME, structural validation, quarantine moves, DB writes, and file stats now happen outside the event loop. |
| 2.10 | Support more than `Change.added` | Implemented | The watcher handles both `Change.added` and `Change.modified`, schedules each eligible event through `handle_watch_changes()`, ignores temp/quarantine paths, and uses a per-path `_inflight_paths` guard plus configurable debounce to coalesce duplicate events while a file is already being admitted. Focused tests cover added/modified scheduling and same-path duplicate event coalescing. |
| 2.11 | File size and safety limits | Implemented | Admission now enforces `MAX_FILE_SIZE`, `MAX_TEXT_FILE_SIZE`, `MAX_PDF_PAGES`, encrypted PDF rejection, ZIP entry count limits, ZIP total uncompressed size limits, and nested archive rejection for ZIP-based document formats. Focused tests cover oversized direct text, over-page-limit PDFs, and nested archive rejection inside DOCX. |
| 2.12 | Canonical parser routing metadata | Partial | `Extension`, `DetectedMimeType`, `OriginalMimeTypeSource`, `ParserHint`, and admission version fields exist. `ParserHint` is not populated, and there is no `DocumentType`, `Encoding`, or `NeedsOcr` field. |
| 2.13 | Version admission/preprocessing/chunking/embedding logic | Partial | `AdmissionVersion` and `CreatedByAdmissionVersion` exist and are set. `PreprocessingVersion`, `ChunkingVersion`, `EmbeddingModel`, and `EmbeddingVersion` exist on ingestion rows but are not populated because downstream stages are not implemented. |
| 2.14 | Trace all uploads to canonical metadata | Partial | `uploads.BinaryHash` links every accepted/duplicate upload to `file_metadata.BinaryHash`, satisfying the simplest option in the improvement doc. There is no relationship type (`Primary`, `DuplicateBinary`) and no separate mapping table. |
| 2.15 | Retry design and dead-letter behavior | Not implemented | `RetryCount` exists and startup recovery increments it, but there is no transient/permanent classification, capped retry loop, backoff, dead-letter terminal state, or retry handling for DB/file lock failures. |
| 2.16 | Startup recovery logic | Partial | `recover_stuck_uploads()` resets `Validating` and `Stabilizing` to `Pending`. It does not check staleness age, file existence, `Processing`, `QueuedForPreprocessing`, or metadata/job mismatches. |
| 2.17 | Logging and observability | Partial | Basic logs exist for quarantine, duplicates, admission, and loop errors. Logs are not structured and do not consistently include `UploadId`, status before/after, failure stage, duration, file size, or metrics. |
| 3.1 | Uploads schema improvements | Partial | Required columns mostly exist and the watcher now consistently populates `DetectedMimeType`, `Extension`, `FailureCode`, `FailureMessage`, `FailureStage`, and `LastAttemptAt` during admission outcomes. Missing: database-level status-transition constraints and full retry/dead-letter semantics. |
| 3.2 | FileMetadata schema improvements | Partial | `BinaryHash`, `Extension`, `DetectedMimeType`, `OriginalMimeTypeSource`, `IsEncrypted`, `IsTextBased`, `PageCount`, `ContentHashNormalized`, `ParserHint`, and admission version exist. Missing: populated `ParserHint` and possibly `DocumentType`/`NeedsOcr` depending on target design. |
| 3.3 | FileIngestion schema improvements | Partial | `Stage`, `AttemptCount`, `WorkerId`, processing version fields, and a unique `BinaryHash` job guard now exist. Missing: `QueueMessageId`/`JobId` and actual downstream processing semantics. |
| 4.1 | Recommended target flow | Partial | Upload API uses DB-record-first temp-to-final rename, and watcher stabilizes files before validation. The watcher validates extension plus MIME plus structure, hashes, dedupes, and creates a pending ingestion row. The missing preprocessing worker means the full target flow is not complete. |
| 5.1 | Functional definition of done | Partial | Several functional items are present, including idempotent binary admission and structured failure details. Retry/recovery, richer parser routing, and logging are still incomplete. |
| 5.2 | Operational definition of done | Partial | Duplicate event protections, duplicate-job protections, and non-blocking watcher scheduling are now present in code, but restart reconciliation, transient retries, and throughput behavior are not fully proven by integration tests. |
| 6-7 | Mandatory test scenarios and test strategy | Not implemented | Focused unit tests now cover the file-stability handoff, MIME rule matching, structural validators, failure-field helpers, guarded state transitions, the unique ingestion-job guard, added/modified event scheduling, per-path debounce, and safety limits. The full mandatory integration test set is still missing for recovery and throughput. |

## Schema Review

Implemented schema improvements:

- `uploads.BinaryHash`, `DetectedMimeType`, `Extension`, `QuarantinePath`, `FailureCode`, `FailureMessage`, `FailureStage`, `RetryCount`, `LastAttemptAt`, and `AdmissionVersion` exist.
- `file_metadata.BinaryHash` is the primary key.
- `file_metadata.ContentHashNormalized` exists for later normalized content dedupe.
- `file_ingestions.Stage`, worker fields, attempt count, and version fields exist.
- `file_ingestions.BinaryHash` now has `UQ_FileIngestions_BinaryHash` to prevent duplicate downstream admission jobs for the same canonical binary.

Schema gaps:

- `file_ingestions` does not include `QueueMessageId` or `JobId`.
- No mapping table exists for upload-to-metadata relationships with relationship type.
- `uploads.BinaryHash` is indexed, and watcher code enforces expected-state transitions, but the database itself does not enforce the upload status state machine.

## Documentation Drift

`docs/ARCHITECTURE.md` mostly reflects the newer design, but it still overstates one detail:

- It says production-grade admission and preprocessing, but preprocessing is not implemented yet.

`docs/SUPPORTED_FILES.md` appears stale:

- It still says unsupported files are physically deleted from disk, while the watcher now quarantines them.

## Verification Performed

Commands run:

```powershell
.\.venv\Scripts\python.exe -m compileall backend worker tests
git diff --check -- worker/FileWatcher.py worker/models.py backend/src/models/file_ingestion.py sql/create_file_ingestions_table.sql sql/recreate_schema.sql tests/test_admission_error_and_idempotency.py tests/test_watcher_async_flow_and_safety.py docs/FILE_WATCHER_IMPROVEMENTS_REVIEW.md
```

Result:

- Python files under `backend`, `worker`, and `tests` compiled successfully.
- No whitespace errors were reported for the files changed by this implementation.

Additional verification:

- Searched source, SQL, and docs for watcher, schema, status, retry, MIME, quarantine, deletion, and old `ContentHash`/`MimeType` references.
- Added and ran focused file-stability, MIME validation, structural validation, admission idempotency/failure-field, async watcher scheduling, debounce, and safety-limit tests:

```powershell
.\.venv\Scripts\python.exe -m unittest tests.test_file_stability tests.test_mime_validation tests.test_structural_validation tests.test_admission_error_and_idempotency tests.test_watcher_async_flow_and_safety
```

Result:

- 27 tests passed.

Note:

- Compile verification does not catch runtime ORM attribute errors such as `FileIngestion.ContentHash` in `ingestion_controller.py`.

## Recommended Next Steps

Priority 1:

1. Fix `backend/src/controllers/ingestion_controller.py` to use `BinaryHash` and `DetectedMimeType`.
2. Add retry classification, capped attempts, and terminal dead-letter behavior.

Priority 2:

1. Add integration tests for duplicate events, concurrent workers, and metadata/job collision recovery against a real test database.
2. Populate parser routing fields consistently.
3. Strengthen startup recovery for stale validating/stabilizing rows, missing files, and metadata/job mismatches.

Priority 3:

1. Add remaining unit tests for filename parsing and quarantine handling.
2. Add integration tests for temp-to-final rename, corrupt/fake files, startup recovery, and throughput.
3. Add structured admission logs with `UploadId`, `BinaryHash`, `StatusBefore`, `StatusAfter`, `FailureCode`, and `DurationMs`.
