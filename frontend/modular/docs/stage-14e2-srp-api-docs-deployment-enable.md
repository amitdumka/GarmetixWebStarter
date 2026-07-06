# Stage 14E.2 SRP API Docs Deployment Enablement

Version: `6.0.38`

## Goal

Keep Swagger/OpenAPI enabled on the live SRP `.127` deployment after systemd service regeneration and API restart.

## Added

- `ApiDocs__Enabled=true` in the SRP API systemd service template.
- `ApiDocs__Enabled=true` in the SRP API env template.
- `ApiDocs__Enabled=true` in the SRP runtime bootstrap env content for fresh hosts.

## Expected Live Routes

- Docs UI: `https://srp.aadwikafashion.in/api/docs`
- OpenAPI JSON: `https://srp.aadwikafashion.in/api/openapi/v1/swagger.json`
- API health: `https://srp.aadwikafashion.in/api/health`

## Notes

- The public `/swagger/...` path is intentionally not used for API docs because SRP nginx routes backend traffic under `/api`.
- Production can still disable docs later by changing the service/env value to `ApiDocs__Enabled=false`.
