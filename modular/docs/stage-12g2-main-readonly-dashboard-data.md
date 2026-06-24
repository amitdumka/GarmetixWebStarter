# Stage 12G.2 - Main Read-Only Dashboard Data

Version: 5.12.28

## Scope

This stage connects the modular Main Back Office dashboard pages to existing read-only dashboard API endpoints.

## Connected Endpoints

- `GET /api/dashboard/business`
- `GET /api/dashboard/todays`
- `GET /api/dashboard/store-manager`

## Added

- `modular/apps/main/utils/main-api.ts`
- `modular/apps/main/components/MainDashboardReadModel.vue`
- Back Office home and dashboard pages now render metric cards, primary rows, secondary rows, health signals and trend preview.

## Safety Notes

This stage does not add write actions. It reuses existing authenticated GET endpoints and does not change backend behavior, database schema, or legacy routes.

## Validation

Run:

```powershell
npm.cmd run modular:check
npm.cmd --prefix modular run build:main
npm.cmd run legacy:api:build
```

## Next Step

Stage 12G.3 should connect the next safe Main read-only screens, likely sale invoice review, purchase review, inventory summary, customer preview and operational reports. Writable actions should stay deferred until each legacy workflow contract is reviewed.
