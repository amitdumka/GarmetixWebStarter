# Stage 12H.5 - Release Checklist

Version: 5.12.35

## Goal

Add a release-day checklist generator for modular static deployments.

## Commands

Print the full checklist for the primary server:

```powershell
npm.cmd run modular:release:checklist
```

Print the checklist for the desktop target:

```powershell
npm.cmd run modular:release:checklist -- --target=desktop
```

Filter to one app:

```powershell
npm.cmd run modular:release:checklist -- --app=pos
npm.cmd run modular:release:checklist -- --target=desktop --app=hr
```

Write a local generated checklist file:

```powershell
npm.cmd run modular:release:checklist -- --write
```

Generated files are written under `modular/docs/generated/` and ignored by git.

## What The Checklist Covers

- Current Version5 version, stage, branch and commit.
- Target host.
- Included apps.
- Pre-release branch sync and validation commands.
- Local and remote deployment preflight commands.
- Public hostname to static root mapping.
- Build-time public environment variables.
- Deploy command sequence.
- API and frontend verification commands.
- Rollback steps.
- Secret-free operating reminders.

## Supported Options

| Option | Values |
| --- | --- |
| `--target` | `server`, `desktop` |
| `--app` | `all`, `main`, `pos`, `hr`, `ai-sense`, `books`, `admin` |
| `--write` | Writes the checklist to an ignored local markdown file |

## Next Step

Stage 12H.6 should add a concise Stage 12 deployment acceptance document that defines when each modular app can be considered deploy-ready.
