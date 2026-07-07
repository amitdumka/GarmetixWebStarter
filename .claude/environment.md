# Environment Map

## Workspaces

| Workspace | Path | Agent | Branch | Status |
|---|---|---|---|---|
| Active Codex/Claude workspace | `C:\AIArea\Codex\GarmetixWebStarter` | Codex + Claude (this one) | `version6` | Active |
| Antigravity workspace | `D:\AIArea\GarmetixWebStarter` | Antigravity | `Version6.A` | Reference only, boundary enforced |
| Old C workspace | `C:\Users\amitn\Documents\Codex\2026-06-04\...\outputs\GarmetixWebStarter` | - | - | Backup/reference only |
| Previous D workspace | `D:\AIArea\Codex\GarmetixWebStarter` | - | - | Backup/reference only |

## Repo Layout

- `backend/` - shared ASP.NET Core API (`Garmetix.Api`), domain (`Garmetix.Domain`), infra/EF Core (`Garmetix.Infrastructure`), tests (`Garmetix.Api.Tests`). One PostgreSQL database. Serves both frontends.
- `frontend/legacy/garmetix-web/` - legacy Nuxt monolith UI, deployed at `garmetix.aadwikafashion.in`. Frozen unless a task explicitly asks for a legacy change.
- `frontend/modular/` - active modular Nuxt workspace, npm workspaces under `apps/*` and `packages/*`.
  - Apps: `main` (back office), `pos`, `hr`, `books`, `ai-sense`, `admin` (SaaS/owner), `crm`.
  - Shared packages: `shared-api`, `shared-auth`, `shared-types`, `shared-ui`, `shared-utils`.
  - `config/routes.ts` - route ownership registry across apps (source of truth for who owns which path).
  - `config/version.ts` - current modular version/stage marker (`6.0.51` as of 2026-07-07).
  - `scripts/` - ~80 readiness/parity/acceptance/smoke scripts, one family per module/stage. Mostly non-mutating dry-run gates; a few have opt-in live modes.
  - `deploy/` - per-app static deploy scripts plus SRP whole-site deploy tooling.
- `legacy/` (repo root, distinct from `frontend/legacy/`) - legacy deployment notes, release notes (`RELEASE-v4.12.*.txt`), device-bridge apps (`legacy/apps/Garmetix.AttendanceKiosk`, `Garmetix.FingerprintBridge`, `Garmetix.MantraMockService`), Docker/Cloudflare/Ubuntu deploy scripts, and Excel exports. Reference/deploy material, not actively developed.
- `docs/` - cross-cutting Version6 planning docs (legacy baseline migration roadmap, live-data-copy notes, codex workspace instructions).
- `.codex/` - Codex's own working TODOs and file backups (do not overwrite; read for context).
- `outputs/` - deploy staging output (SRP release artifacts), not source.

## Deploy Targets

- SRP host: `amitkumar@192.168.11.127`, public URL `https://srp.aadwikafashion.in`, remote base `/opt/garmetix-srp`. This hosts the modular apps.
- Legacy Ubuntu server host: `amit@192.168.11.126` (per `frontend/modular/docs` deployment split guide).
- Legacy production domain: `garmetix.aadwikafashion.in`.
- Cloudflare Tunnel fronts both; per-app subdomains planned (`pos.`, `hr.`, `books.`, `ai-sense.`, `admin.` + `garmetix.aadwikafashion.in` for main, `api.` for the shared API).

## Validation Commands

```powershell
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release
npm --prefix frontend/legacy/garmetix-web run build
node frontend/modular/scripts/validate-structure.mjs   # confirmed passing 2026-07-07
node frontend/modular/scripts/validate-all.mjs
```

## Notable Environment Quirk

Running `git status`/`git diff` from the Linux Cowork sandbox against this Windows-checked-out repo shows ~1,689 files as modified. Verified 2026-07-07: this is a CRLF (Windows working tree) vs LF (committed) line-ending artifact - `git diff --ignore-space-at-eol` shows zero real changes on sampled files. Not a real uncommitted-change problem; just don't trust a raw `git status` file count from this sandbox. Real changes should be confirmed with `git diff --ignore-space-at-eol` or checked from an actual Windows shell.
