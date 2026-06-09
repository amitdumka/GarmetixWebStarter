# Developer Validation Automation Notes

This project now includes helper scripts for the remaining Testing / Deployment Hardening checklist. These scripts do not replace manual business testing, but they make build/runtime validation repeatable.

## Windows PowerShell

From the project root:

```powershell
.\scripts\validate-local.ps1 -NoCacheApi
```

Useful variants:

```powershell
.\scripts\validate-local.ps1 -SkipDocker
.\scripts\validate-local.ps1 -NoCacheApi -KeepRunning:$false
```

## Linux / Mac

From the project root:

```bash
./scripts/validate-local.sh --no-cache-api
```

Useful variants:

```bash
./scripts/validate-local.sh --skip-docker
./scripts/validate-local.sh --no-cache-api --down-after
```

## What it checks

- Installed tool versions: `dotnet`, `npm`, and Docker.
- Backend restore/build/publish.
- Frontend `npm ci` and Nuxt build.
- Docker compose config/build/up.
- API health: `http://localhost:5080/api/health`.
- Web health: `http://localhost:3000`.
- Captures API/web/Postgres logs into `validation-results/`.

## Manual database repair

The runtime repair endpoint requires an Admin JWT.

PowerShell:

```powershell
.\scripts\repair-database.ps1 -Token "<admin-jwt-token>"
```

Bash:

```bash
./scripts/repair-database.sh "<admin-jwt-token>"
```

## Validation output

Each run writes a timestamped markdown summary and log files under:

```text
validation-results/
```

Attach the generated `validation-*.md` and related `*-tail-*.log` files when reporting build/runtime issues.
