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
- `docs/ARCHITECTURE.md`
- `docs/SUPPORTED_FILES.md`

## Executive Summary

The file watcher has been significantly hardened compared with the "current state" described in `FILE_WATCHER_IMPROVEMENTS.md`, but the full improvement set is not complete.

Overall status:

- Implemented: 6 items
- Partially implemented: 10 items
- Not implemented: 3 items

The most important remaining risks are:

1. Concurrent workers can still race when creating metadata and ingestion jobs because the dedupe path is check-then-insert and `file_ingestions.BinaryHash` is not unique.
2. Retry/dead-letter behavior is not implemented beyond a startup reset of some statuses.
3. The ingestion controller still references old model fields (`ContentHash`, `MimeType`) that no longer exist.
4. Automated coverage now exists for file stability, MIME rules, and structural validators, but duplicate races, recovery, and throughput scenarios are still mostly untested.

## High-Risk Findings

### 1. Concurrent metadata/job creation is not fully race-safe

Status: Not fully implemented

Evidence:

- `worker/FileWatcher.py` now skips uploads that are no longer `Pending` or `Stabilizing`, which prevents repeated events from changing an already accepted upload into `DuplicateBinary`.
- The remaining dedupe path still queries for `FileMetadata.BinaryHash` and then inserts if no row is found.
- `file_metadata.BinaryHash` is a primary key, but there is no explicit retry/upsert handling around a concurrent insert collision.
- `file_ingestions.BinaryHash` has no unique constraint, so duplicate downstream jobs remain possible under races.

Impact:

Two concurrent workers can still race on the same binary hash. One may create metadata and the other may fail or create duplicate downstream state unless the insert path is made explicitly idempotent.

Recommended fix:

- Use a transaction-safe upsert or handle the primary-key conflict as a duplicate outcome.
- Add a unique job guard for `file_ingestions.BinaryHash` or an equivalent idempotent queue key.

### 2. Ingestion API still uses removed field names

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
| 2.7 | Error/rejection reason fields | Partial | Upload model has `FailureCode`, `FailureMessage`, `FailureStage`, `LastAttemptAt`, and `RetryCount` (`upload.py:23-33`). Watcher sets `FailureCode` for quarantine and internal errors, but usually does not set `FailureMessage` or `FailureStage`. |
| 2.8 | Idempotent and race-safe processing | Partial | `FileMetadata.BinaryHash` is the primary key, so binary metadata uniqueness exists. Repeated events for already terminal uploads are now skipped. However the check-then-insert dedupe path is not transactionally race-safe, and ingestion jobs are not protected by a unique constraint. |
| 2.9 | Avoid blocking file I/O in async flow | Partial | Hashing, MIME sniffing, and structural validation run via `asyncio.to_thread()`. But DB calls and `shutil.move()` still run synchronously inside the async flow, and files are processed serially inside the watch event loop. |
| 2.10 | Support more than `Change.added` | Partial | The watcher handles `Change.added` and `Change.modified` (`FileWatcher.py:299`) and ignores `.tmp` files (`153`). There is no debounce or per-path in-flight guard. |
| 2.11 | File size and safety limits | Partial | `MAX_FILE_SIZE` exists (`FileWatcher.py:32`) and oversized/encrypted PDFs are rejected (`105-117`). Missing controls include max PDF pages, max text length, nested archive policy, and special/manual queues. |
| 2.12 | Canonical parser routing metadata | Partial | `Extension`, `DetectedMimeType`, `OriginalMimeTypeSource`, `ParserHint`, and admission version fields exist. `ParserHint` is not populated, and there is no `DocumentType`, `Encoding`, or `NeedsOcr` field. |
| 2.13 | Version admission/preprocessing/chunking/embedding logic | Partial | `AdmissionVersion` and `CreatedByAdmissionVersion` exist and are set. `PreprocessingVersion`, `ChunkingVersion`, `EmbeddingModel`, and `EmbeddingVersion` exist on ingestion rows but are not populated because downstream stages are not implemented. |
| 2.14 | Trace all uploads to canonical metadata | Partial | `uploads.BinaryHash` links every accepted/duplicate upload to `file_metadata.BinaryHash`, satisfying the simplest option in the improvement doc. There is no relationship type (`Primary`, `DuplicateBinary`) and no separate mapping table. |
| 2.15 | Retry design and dead-letter behavior | Not implemented | `RetryCount` exists and startup recovery increments it, but there is no transient/permanent classification, capped retry loop, backoff, dead-letter terminal state, or retry handling for DB/file lock failures. |
| 2.16 | Startup recovery logic | Partial | `recover_stuck_uploads()` resets `Validating` and `Stabilizing` to `Pending`. It does not check staleness age, file existence, `Processing`, `QueuedForPreprocessing`, or metadata/job mismatches. |
| 2.17 | Logging and observability | Partial | Basic logs exist for quarantine, duplicates, admission, and loop errors. Logs are not structured and do not consistently include `UploadId`, status before/after, failure stage, duration, file size, or metrics. |
| 3.1 | Uploads schema improvements | Partial | Required columns mostly exist. Missing: stronger status semantics enforcement and consistent population of `DetectedMimeType`, `FailureMessage`, `FailureStage`, and retry fields. |
| 3.2 | FileMetadata schema improvements | Partial | `BinaryHash`, `Extension`, `DetectedMimeType`, `OriginalMimeTypeSource`, `IsEncrypted`, `IsTextBased`, `PageCount`, `ContentHashNormalized`, `ParserHint`, and admission version exist. Missing: populated `ParserHint` and possibly `DocumentType`/`NeedsOcr` depending on target design. |
| 3.3 | FileIngestion schema improvements | Partial | `Stage`, `AttemptCount`, `WorkerId`, and processing version fields exist. Missing: `QueueMessageId`/`JobId`, unique job constraint, and actual downstream processing semantics. |
| 4.1 | Recommended target flow | Partial | Upload API uses DB-record-first temp-to-final rename, and watcher stabilizes files before validation. The watcher validates extension plus MIME plus structure, hashes, dedupes, and creates a pending ingestion row. The missing preprocessing worker means the full target flow is not complete. |
| 5.1 | Functional definition of done | Partial | Several functional items are present, but idempotency, failure details, retry/recovery, and logging are incomplete. |
| 5.2 | Operational definition of done | Not implemented | Duplicate events, restart reconciliation, transient retries, and throughput behavior are not proven by code or tests. |
| 6-7 | Mandatory test scenarios and test strategy | Not implemented | Focused unit tests now cover the file-stability handoff, MIME rule matching, and structural validators. The full mandatory test set is still missing for duplicate races, recovery, and throughput. |

## Schema Review

Implemented schema improvements:

- `uploads.BinaryHash`, `DetectedMimeType`, `Extension`, `QuarantinePath`, `FailureCode`, `FailureMessage`, `FailureStage`, `RetryCount`, `LastAttemptAt`, and `AdmissionVersion` exist.
- `file_metadata.BinaryHash` is the primary key.
- `file_metadata.ContentHashNormalized` exists for later normalized content dedupe.
- `file_ingestions.Stage`, worker fields, attempt count, and version fields exist.

Schema gaps:

- `file_ingestions` has no uniqueness constraint on `BinaryHash`, so duplicate jobs can be created by races.
- `file_ingestions` does not include `QueueMessageId` or `JobId`.
- No mapping table exists for upload-to-metadata relationships with relationship type.
- `uploads.BinaryHash` is indexed but not constrained by status transition rules.

## Documentation Drift

`docs/ARCHITECTURE.md` mostly reflects the newer design, but it still overstates one detail:

- It says production-grade admission and preprocessing, but preprocessing is not implemented yet.

`docs/SUPPORTED_FILES.md` appears stale:

- It still says unsupported files are physically deleted from disk, while the watcher now quarantines them.

## Verification Performed

Commands run:

```powershell
.\.venv\Scripts\python.exe -m compileall backend worker tests
git diff --check -- worker/FileWatcher.py tests/test_structural_validation.py docs/FILE_WATCHER_IMPROVEMENTS_REVIEW.md
```

Result:

- Python files under `backend`, `worker`, and `tests` compiled successfully.
- No whitespace errors were reported for the files changed by this implementation.

Additional verification:

- Searched source, SQL, and docs for watcher, schema, status, retry, MIME, quarantine, deletion, and old `ContentHash`/`MimeType` references.
- Added and ran focused file-stability, MIME validation, and structural validation tests:

```powershell
.\.venv\Scripts\python.exe -m unittest tests.test_structural_validation tests.test_mime_validation tests.test_file_stability
```

Result:

- 19 tests passed.

Note:

- Compile verification does not catch runtime ORM attribute errors such as `FileIngestion.ContentHash` in `ingestion_controller.py`.

## Recommended Next Steps

Priority 1:

1. Fix `backend/src/controllers/ingestion_controller.py` to use `BinaryHash` and `DetectedMimeType`.
2. Make concurrent metadata/job creation transactionally idempotent.

Priority 2:

1. Add a unique job guard for `file_ingestions.BinaryHash` or an idempotent queue key.
2. Populate `FailureMessage`, `FailureStage`, `DetectedMimeType`, and parser routing fields consistently.
3. Add retry classification, capped attempts, and terminal dead-letter behavior.

Priority 3:

1. Add unit tests for filename parsing, state transitions, MIME rules, structural validators, and quarantine handling.
2. Add integration tests for duplicate events, temp-to-final rename, corrupt/fake files, startup recovery, and concurrent workers.
3. Add structured admission logs with `UploadId`, `BinaryHash`, `StatusBefore`, `StatusAfter`, `FailureCode`, and `DurationMs`.
