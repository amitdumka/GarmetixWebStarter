# Stage 7L v3.11.1 — Nuxt Docker Build Memory Fix

Version: 3.11.1  
Stage: Stage 7L  
Build Code: GARMETIX-7L-20260611-3111

## Problem fixed

The local Docker build reached the Nuxt/Nitro production server build step and then failed with:

```text
FATAL ERROR: Reached heap limit Allocation failed - JavaScript heap out of memory
```

The API image built successfully. The failure was in the web image during:

```text
RUN npm run build
```

## Root cause

After Stage 7 dashboard pages/components, the Nuxt/Nitro production build needs more heap than Node's default build-time memory limit inside Docker.

## Fix applied

Updated:

```text
frontend/garmetix-web/Dockerfile
```

Added build-stage memory and telemetry settings:

```dockerfile
ENV NODE_OPTIONS=--max-old-space-size=4096
ENV NUXT_TELEMETRY_DISABLED=1
```

Added a smaller runtime heap setting for the final image:

```dockerfile
ENV NODE_OPTIONS=--max-old-space-size=1024
```

## Notes

- No business logic changed.
- No API code changed except version identity.
- No frontend page behavior changed except Docker build reliability.
- If Docker Desktop is limited to less than 4GB RAM, increase Docker Desktop memory to 6GB or 8GB for production builds.

## Test command

```bash
docker compose build --no-cache web
```

Then:

```bash
docker compose up --build
```
