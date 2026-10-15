# OpenAPI Contract Specifications

This directory contains committed OpenAPI contract specifications for `RagPipeline.Api`.

## Specifications

- **`rag-pipeline.v1.json`**: OpenAPI 3.1 specification document for `v1` of the API. It documents all public endpoints including system health probes (`/api/v1/system/health/live` and `/api/v1/system/health/ready`), response statuses (HTTP 200, 503), RFC 9457 Problem Details error responses (`application/problem+json`), and schema extensions including `traceId`.

## Invariants & Design Decisions

- **No Runtime Serving**: OpenAPI document generation is configured for build-time and test-time tooling only via `AddRagPipelineOpenApi()`. The API does not serve OpenAPI documents or Swagger UI at runtime.
- **Stable Formatting**: Specifications are serialized as deterministic OpenAPI 3.1 JSON with normalized LF newlines and a single trailing newline.
- **CI Drift Detection**: Integration tests in `tests/Integration/RagPipeline.IntegrationTests/OpenApiContractTests.cs` compare runtime-generated OpenAPI documents against `rag-pipeline.v1.json`. Any route modifications or schema drift without updating the committed contract file cause CI to fail.

## Regenerating the Specification

To regenerate and update the committed OpenAPI specification after modifying endpoints or contracts, run:

```bash
UPDATE_OPENAPI_SPEC=1 dotnet test tests/Integration/RagPipeline.IntegrationTests --filter "FullyQualifiedName~OpenApiContractTests.OpenApiSpec_MatchesCommittedContract"
```

Commit the updated `rag-pipeline.v1.json` alongside the corresponding code changes.
