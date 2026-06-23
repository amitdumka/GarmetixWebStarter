# @garmetix/shared-api

Shared API client boundary for the Version5 modular frontends.

This package will gradually receive the reusable parts of `legacy/frontend/garmetix-web/composables/useGarmetixApi.ts`.

Rules:

- Keep the backend as one ASP.NET Core API.
- Keep API base URLs configurable through environment variables.
- Keep auth token lookup injectable so every modular app can use the same client.
- Do not hardcode production domains in this package.

