# API Client Package

Purpose: shared API client primitives for all Garmetix frontends.

Planned extraction source:

- `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts`
- `legacy/frontend/garmetix-web/composables/useServerDocumentPrint.ts`
- `legacy/frontend/garmetix-web/composables/useProductLookup.ts`

Responsibilities:

- Build API URLs from environment config.
- Attach bearer auth headers.
- Normalize request errors without showing server URLs to users.
- Support lightweight caching for lookup resources.
- Keep API contracts shared across main, POS, HR, AI Sense, Books, and Admin/SaaS.

