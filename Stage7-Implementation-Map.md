# Stage 7 Implementation Map

## Frontend

| Area | Files |
|---|---|
| Dashboard shell | `frontend/garmetix-web/components/AppShell.vue` |
| Revert shell | `frontend/garmetix-web/components/AppShellLegacy.vue` |
| Store manager dashboard | `frontend/garmetix-web/pages/dashboard/store-manager/index.vue` |
| Business dashboard | `frontend/garmetix-web/pages/dashboard/business/index.vue` |
| Styling | `frontend/garmetix-web/assets/css/main.css` |
| Runtime switch | `frontend/garmetix-web/nuxt.config.ts` |
| Version | `frontend/garmetix-web/utils/appVersion.ts` |

## Backend

| Area | Files |
|---|---|
| Dashboard DTOs | `backend/Garmetix.Api/Dashboard/DashboardDtos.cs` |
| Dashboard endpoints | `backend/Garmetix.Api/Dashboard/DashboardEndpoints.cs` |
| Endpoint mapping | `backend/Garmetix.Api/Program.cs` |
| Version API | `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs` |

## Routes

| Route | Purpose |
|---|---|
| `/dashboard/store-manager` | Store-scoped dashboard for store-manager style users |
| `/dashboard/business` | Company/store-group dashboard for Owner/Admin/Accountant |
| `/api/dashboard/store-manager` | Store dashboard data endpoint |
| `/api/dashboard/business` | Business dashboard data endpoint |

## Safety / Revert

No existing page was deleted. Use `NUXT_PUBLIC_DASHBOARD_SHELL=legacy` to render the previous shell while keeping the new code available.
