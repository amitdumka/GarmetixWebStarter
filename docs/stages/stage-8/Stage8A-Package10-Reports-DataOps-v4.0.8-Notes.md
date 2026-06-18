# Stage 8A Package 10 - v4.0.8

Date: 2026-06-14

## Delivered

- Standardized Reports Center, GST Reports, Import/Export, Audit Trail, and Message Logs with shared loading, retryable error, empty, and responsive register states.
- Replaced UTC-based Reports Center date defaults with local calendar values.
- Kept load failures visible in the affected register until a successful retry.
- Preserved existing report exports, report cache, GST CSV downloads, import validation, audit export, and log detail workflows.
- Reworked Message Logs into compact operational panels with expandable diagnostic details.
- Replaced invalid empty Nuxt UI select values in Message Log filters with explicit all-value sentinels.
- Added the active workspace company to GST report and CSV requests so valid periods no longer fail company-scope validation.
- Sanitized retained register errors so server URLs stay in technical logs instead of business-facing messages.
- Marked all five Package 10 routes reviewed in the persistent Stage 8A audit queue.

## Validation

```powershell
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj --no-restore
cd frontend/garmetix-web
npm.cmd run build
python scripts/validation/current-release-checks.py
docker compose up -d --build api web
```
