# Dashboard Home Smoke Hardening v4.9.13

This package closes the dashboard-home release blocker and adds smoke-test coverage so the issue does not return silently.

## Dashboard home endpoint fix

`GET /api/dashboard/home` is now mapped to an explicit handler named `HomeAsync` that returns `Results.Ok(DashboardHomeDto)`.

This keeps the live endpoint from returning HTTP 200 with an empty or unusable body. The frontend dashboard redirect page still keeps its local fallback, but the normal server contract is now explicit.

## Testable routing contract

Dashboard role routing is now centralized in:

```csharp
DashboardEndpoints.ResolveHome(ClaimsPrincipal user)
```

Covered routes:

- Admin / Owner / Accountant / Power User -> `/dashboard/business`
- Store Manager / Salesman -> `/dashboard/store-manager`
- HR -> `/hr`
- Payroll -> `/payroll`

## Backend unit tests

Added:

```text
backend/Garmetix.Api.Tests/Dashboard/DashboardHomeRoutingTests.cs
```

The tests verify dashboard routing for Admin, HR, Payroll and Salesman users.

## Release smoke checks

The admin-only Release Stabilization smoke endpoint now includes:

```text
DASHBOARD_HOME_CONTRACT
```

This check validates the same dashboard routing contract during production acceptance.

## Test automation manifest

The test automation manifest now includes a dedicated dashboard-home contract entry:

```text
DASHBOARD_HOME_CONTRACT
```

Suggested command:

```bash
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release --filter FullyQualifiedName~DashboardHomeRoutingTests
```

## Linux and Windows smoke scripts

Both production smoke scripts now:

1. Expect version `4.9.13` and build code `GARMETIX-8I-20260619-49130` by default.
2. Require `DASHBOARD_HOME_CONTRACT` in the test manifest.
3. Call authenticated `/api/dashboard/home` and fail if `route` or `dashboardType` is missing.

## Validation

Run:

```bash
python3 scripts/validation/stage8i-package14-static-checks.py
python3 scripts/validation/current-release-checks.py
```

Then on a machine with .NET and Docker:

```bash
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
cd frontend/garmetix-web && npm ci && npm run build
cd ../..
docker compose --env-file .env.production -f docker-compose.prod.yml build
```
