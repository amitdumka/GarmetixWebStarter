# Stage 14F.3 - Assistant Frontend Panel

Version: 6.0.45

## Scope

This stage adds the shared Nuxt UI frontend surface for the Garmetix Assistant.

Implemented:

- Added `GarmetixAssistantPanel` in `packages/shared-ui/components`.
- Added a sparkle launcher in the shared `ModularAppShell`.
- Added a right-side `USlideover` for assistant chat.
- Added tool-call badges for transparency.
- Added `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED`, defaulting to `false`.
- Added missing `@garmetix/shared-api` and `@garmetix/shared-auth` shared UI dependencies.

## Safety

- The launcher is hidden unless the frontend build has `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true`.
- The launcher is hidden for anonymous users.
- The backend Assistant remains disabled until the server has `Assistant__Enabled=true` and a rotated provider key.
- The panel uses the existing shared auth token and shared API client.

## Validation

- `npm run modular:assistant:frontend-panel`
- `npm --prefix frontend/modular run check`
- `npm --prefix frontend/modular --workspace @garmetix/main-web run build`
- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release`

## Deploy Note

Deploying this stage with the default environment does not expose the assistant launcher. To live-test later, set both frontend and backend feature flags with a rotated key before building and restarting the API.
