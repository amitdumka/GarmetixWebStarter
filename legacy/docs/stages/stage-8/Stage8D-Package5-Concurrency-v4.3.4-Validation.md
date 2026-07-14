# Stage 8D Package 5 Validation

Version: 4.3.4

- [x] Standard unit suite passes with 64 tests and 3 PostgreSQL tests skipped when no test connection is configured.
- [x] Live PostgreSQL suite passes all 67 tests, including concurrent sequence and stock-lock checks.
- [x] Concurrent sequence test returns unique counters 1 through 8 and leaves one active sequence row.
- [x] Competing stock-lock test remains blocked until the owning transaction commits.
- [x] Sequence and stock locking reject calls made outside an active transaction.
- [x] Backend and Nuxt production builds complete.
- [x] Docker applies the sequence-hardening migration and API, web, and PostgreSQL services are healthy.
- [x] App Info reports build code `GARMETIX-8D-20260615-4340`.
