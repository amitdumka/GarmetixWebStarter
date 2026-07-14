# Stage 14F.4 - AI Sense Tool Catalog Expansion

Version: 6.0.46

## What Changed

- Added assistant read-only tools for `get_business_snapshot` and `get_today_snapshot`.
- Reused the existing dashboard aggregations from `DashboardEndpoints.BusinessAsync` and `DashboardEndpoints.TodaysAsync` instead of duplicating large reporting logic.
- Kept all assistant tool calls behind the existing authenticated assistant endpoint and existing workspace scoping.
- Enabled the modular frontend assistant launcher flag with `NUXT_PUBLIC_GARMETIX_ASSISTANT_ENABLED=true`.

## Safety Rules

- No tool writes or mutates data.
- No provider key is stored in source-controlled files.
- Backend provider enablement still requires server-side `Assistant__Enabled=true` and a rotated server-side provider key.
- Tool result payloads remain capped by existing dashboard/top-row limits or explicit `Take(...)` limits.

## Validation

- `npm run modular:assistant:tool-catalog`
- `dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release`
- `npm --prefix frontend/modular run check`
- modular build/deploy validation before live rollout
