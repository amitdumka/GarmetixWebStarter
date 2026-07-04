# Deployment And Validation Cadence

Version: 6.0.7

## Purpose

This cadence keeps Version6 development focused on coding and module parity while still protecting the `.127` SRP deployment and live data.

## Default Rule

Run local checks and commit every completed stage. Do not deploy every stage by default.

Every third checkpoint, run the full deployment loop:

1. Build affected modular app or full SRP bundle.
2. Deploy to `192.168.11.127`.
3. Run public and LAN acceptance.
4. Record the result in the stage summary.

## Database Backup

Database backup is required only when a step may create, update or delete live data, or when backend model/schema/migration behavior changes.

Backup is not required for:

- Documentation-only changes.
- Frontend layout or static route changes.
- Read-only acceptance scripts.
- Dry-run validation scripts.
- Package script wiring.

Backup is required for:

- EF model, migration or schema changes.
- Seed data changes that touch live database behavior.
- Controlled live-write tests for sale, return, exchange, payroll, voucher, petty cash or stock.
- Factory reset, restore, import/export commit, or destructive admin work.

## Live Deploy

Live deploy to `.127` is normally done every third checkpoint.

Deploy earlier only when:

- A production defect is being fixed.
- A route or asset issue can only be confirmed on Cloudflare/Nginx.
- Backend API/service wiring changed.
- The user explicitly asks for immediate live deployment.

## Public/LAN Acceptance

Public/LAN acceptance is normally run after a live deploy, not after every local script or documentation update.

Run strict acceptance earlier only when:

- Nginx, Cloudflare, base URL, API proxy or asset base path changed.
- A prior public route was blank, 404, 500 or loading raw text.
- A module is being handed to the user for live browser testing.

## Local Validation

For normal coding checkpoints, prefer:

- The new or changed module script.
- `npm.cmd run modular:check`.
- The affected app build only when UI/source files changed.

Run full `modular:validate` and all builds before a larger stage closure or third-checkpoint deploy.

## Current POS Rule

Stage 14A.7 closes the POS-first lane locally. It should not trigger another `.127` deploy because Stage 14A.6 was just deployed and no runtime source behavior is being changed.

The next deploy should happen after two more implementation checkpoints, unless a high-risk or user-requested deployment happens earlier.
