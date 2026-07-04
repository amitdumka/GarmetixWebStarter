# Stage 14A.7 POS Final Closure

Version: 6.0.7

## Purpose

Stage 14A.7 closes the POS-first modular lane with a repeatable final gate before HR modular parity starts. It does not create live invoices by default.

## What This Stage Adds

- POS final closure gate.
- Deployment and validation cadence so `.127` deploy, public acceptance and database backup are not repeated after every small checkpoint.
- Explicit distinction between code-ready POS closure and production cashier handover.

## Closure Command

```powershell
npm.cmd run modular:pos:final-closure
```

This command checks that POS Stage 14A artifacts are present and that sale, return, exchange, held bill, day open/day close and print recovery surfaces still contain the expected workflow markers.

## Strict Evidence Modes

Require a live smoke token:

```powershell
$env:GARMETIX_SMOKE_AUTH_TOKEN='<token outside source control>'
npm.cmd run modular:pos:final-closure -- --require-token
```

Require manual 14 inch cashier evidence:

```powershell
$env:GARMETIX_POS_MANUAL_ACCEPTANCE='YES'
npm.cmd run modular:pos:final-closure -- --require-manual
```

Require both:

```powershell
$env:GARMETIX_SMOKE_AUTH_TOKEN='<token outside source control>'
$env:GARMETIX_POS_MANUAL_ACCEPTANCE='YES'
npm.cmd run modular:pos:final-closure -- --require-token --require-manual
```

## Go/No-Go Meaning

- `GO`: live-token evidence and manual cashier evidence are both present.
- `CONDITIONAL`: code closure passes, but production cashier handover still needs live-token and/or manual evidence.
- `FAIL`: required files, scripts, workflow markers, version or strict evidence are missing.

## Deployment Decision

No `.127` deployment is required for this stage because Stage 14A.6 was just deployed and Stage 14A.7 adds a local closure gate and documentation only.

Follow `docs/deployment-validation-cadence.md` for future deployment timing.

## Next Stage

Stage 14B starts HR modular parity from the current Version6 base after POS closure is accepted.
