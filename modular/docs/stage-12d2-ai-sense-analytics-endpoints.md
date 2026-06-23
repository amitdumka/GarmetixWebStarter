# Stage 12D.2 AI Sense Analytics Endpoints

Version: 5.12.13

## What Changed

- Added read-only AI Sense API endpoints under `/api/ai-sense`.
- Connected the modular AI Sense analysis pages to those endpoints.
- Added a reusable `AiConnectedAnalysis` component for metric, row, signal, and trend display.

## API Endpoints

- `GET /api/ai-sense/sales-analysis`
- `GET /api/ai-sense/purchase-analysis`
- `GET /api/ai-sense/profit-analysis`
- `GET /api/ai-sense/stock-risk`
- `GET /api/ai-sense/vendor-analysis`
- `GET /api/ai-sense/customer-analysis`
- `GET /api/ai-sense/daily-summary`
- `GET /api/ai-sense/monthly-summary`

These endpoints project existing dashboard data and do not introduce a new backend service, database, AI provider, or write action.

## Frontend Pages Connected

- `/ai-sense/sales-analysis`
- `/ai-sense/purchase-analysis`
- `/ai-sense/profit-analysis`
- `/ai-sense/stock-risk`
- `/ai-sense/vendor-analysis`
- `/ai-sense/customer-analysis`
- `/ai-sense/daily-summary`
- `/ai-sense/monthly-summary`

## Safety Notes

- Backend remains one ASP.NET Core API.
- PostgreSQL remains one database.
- No schema change.
- No write action.
- No external AI API integration.

## How To Test

```powershell
dotnet build legacy/backend/Garmetix.Api/Garmetix.Api.csproj -c Release
npm run modular:check
npm --prefix modular run build:ai-sense
```

Then run the API and AI Sense app, login, and open the connected analysis routes.

## Next Step

Stage 12D.3 should add the AI Sense static deploy script and deployment notes for `ai-sense.garmetix.aadwikafashion.in`.
