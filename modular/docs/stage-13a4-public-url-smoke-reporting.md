# Stage 13A.4 - Public URL Smoke Reporting

Version: 5.13.4

## Goal

Add a Cloudflare-facing public URL smoke report for the modular apps and shared API without changing deployment, backend routing, database state or credentials.

## Added Commands

From the repository root:

```powershell
npm.cmd run modular:smoke:public
npm.cmd run modular:smoke:public -- --app=pos
npm.cmd run modular:smoke:public -- --write
npm.cmd run modular:smoke:public -- --live
```

From the `modular/` folder:

```powershell
npm.cmd run smoke:public
```

## Dry Mode

Dry mode is the default. It prints the expected public endpoint matrix for:

- `api.garmetix.aadwikafashion.in`
- `garmetix.aadwikafashion.in`
- `pos.garmetix.aadwikafashion.in`
- `hr.garmetix.aadwikafashion.in`
- `ai-sense.garmetix.aadwikafashion.in`
- `books.garmetix.aadwikafashion.in`
- `admin.garmetix.aadwikafashion.in`

The dry public smoke report is included in `npm.cmd run modular:validate`.

## Live Mode

Live mode sends safe anonymous `HEAD` requests, falling back to `GET` when needed. It expects:

- API health to respond with `200`.
- App roots to respond with any non-server-error status below `500`, because deployed static apps may redirect to login or show protected-shell fallback.

No usernames, passwords, tunnel tokens, SSH keys or bearer tokens are used.

## Generated Reports

Use `--write` to create a local markdown report under:

```text
modular/docs/generated/
```

That folder is intentionally ignored by git.

## Acceptance

- Public URL smoke report works in dry mode.
- App filtering works with `--app=main|pos|hr|ai-sense|books|admin`.
- Optional live mode can test Cloudflare-facing reachability.
- `modular:validate` includes the dry public URL report.

## Next Step

Stage 13A.5 should add visual smoke notes for login, shell layout, app switching and access-denied pages, then close the Stage 13A hardening lane.
