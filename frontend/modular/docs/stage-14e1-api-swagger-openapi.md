# Stage 14E.1 API Swagger OpenAPI Documentation

Version: `6.0.37`

## Goal

Expose a browsable API route catalogue for the shared Garmetix ASP.NET Core API without splitting the backend or changing database behavior.

## Added

- Swagger/OpenAPI generation through `Swashbuckle.AspNetCore`.
- API explorer metadata for minimal API endpoints.
- Swagger UI at `/api/docs`.
- OpenAPI JSON at `/api/openapi/v1/swagger.json`.
- JWT Bearer security support so protected endpoints can be tested from the docs page after login.
- `ApiDocs:Enabled` configuration flag in API appsettings.

## Public URLs

- SRP docs UI: `https://srp.aadwikafashion.in/api/docs`
- SRP OpenAPI JSON: `https://srp.aadwikafashion.in/api/openapi/v1/swagger.json`

## Auth Use

1. Login through the app or call `/api/auth/login`.
2. Copy the returned JWT access token.
3. Open `/api/docs`.
4. Click `Authorize` and paste the token value.

## Notes

- The docs live under `/api/...` because SRP nginx and Cloudflare proxy the API under that prefix.
- This stage is read-only for database/data behavior; it only exposes endpoint documentation and interactive API testing.
- If production needs to hide docs later, set `ApiDocs:Enabled=false` in the API configuration.
