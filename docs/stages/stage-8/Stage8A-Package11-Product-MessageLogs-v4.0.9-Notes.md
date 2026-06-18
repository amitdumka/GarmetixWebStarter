# Stage 8A Package 11 - v4.0.9

## Scope

Repair Product Master saving and make Message Logs the persistent operational record for API failures, exceptions, write events, frontend messages, and unhandled browser errors.

## Product Master

- Reproduced Product create returning HTTP 500 before the first scope query.
- Root cause: `NpgsqlRetryingExecutionStrategy` does not permit a manually opened transaction outside its execution delegate.
- Wrapped Product create and edit transaction units in `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- Preserved the existing product, stock, opening movement, classification, tax, vendor, and workspace behavior.

## Persistent Message Logs

- Added API middleware that records:
  - unhandled exceptions with trace, stack, route, user, and timing details;
  - all HTTP 4xx/5xx API responses;
  - successful POST, PUT, PATCH, and DELETE operations.
- Unhandled exceptions now return a safe message with an operation ID instead of exposing server stack and authorization headers.
- Added an authenticated write-only frontend logging endpoint while retaining admin-only Message Log search and diagnostics.
- Frontend toast messages, action failures, Vue errors, browser errors, and unhandled promise rejections are persisted.
- Garmetix application and background-service `ILogger` messages at Information or higher are queued and persisted without blocking the originating service.
- Microsoft/EF framework chatter is excluded, the queue is bounded, and the logging subsystem excludes itself to prevent recursive failures.
- Password, token, authorization, API-key, and secret fields are redacted from frontend diagnostics.
- Server-side messages and details pass through the same sensitive-value redaction before storage.
- Message field sizes are bounded to protect the log table from oversized browser or exception payloads.

## Version

- Frontend, backend, npm package, roadmap, and documentation identify `v4.0.9`.
- Build code: `GARMETIX-8A-20260614-4009`.
