# Stage 13G.6 SRP Public Acceptance

Version: 5.13.46

## Scope

This stage adds a repeatable acceptance runner for `srp.aadwikafashion.in` without storing Cloudflare credentials in the repository.

## Command

Dry-run:

```bash
npm run modular:deploy:srp:acceptance
```

Live, warning-only:

```bash
npm run modular:deploy:srp:acceptance -- --live
```

Live, strict after Cloudflare is configured:

```bash
npm run modular:deploy:srp:acceptance -- --live --strict
```

## Checks

The runner checks:

- DNS resolution for `srp.aadwikafashion.in`
- public Main Back Office route
- public POS route
- public HR route
- public AI Sense route
- public Books route
- public Admin/SaaS route
- public API health route
- LAN fallback routes on `http://192.168.11.127:8088`

## Why Warning-Only Live Mode Exists

At this stage the SRP host origin is ready, but Cloudflare credentials are still outside git and not present in the private secrets file. The live acceptance command therefore reports Cloudflare/DNS issues as warnings unless `--strict` is supplied.

This makes the current state easy to see:

- LAN passes: SRP host origin is working.
- Public warns: Cloudflare DNS/tunnel is not active yet.
- Strict fails: final production-public acceptance is not complete.

## Remaining Work

Add one Cloudflare credential option to the private secrets file and run:

```bash
bash modular/deploy/srp-cloudflare-activate.sh --install
npm run modular:deploy:srp:acceptance -- --live --strict
```

Then complete browser login, app switching and API verification from the public SRP hostname.
