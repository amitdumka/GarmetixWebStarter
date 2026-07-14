# Garmetix Production Release Checklist

Use this checklist before switching a real store to the web app.

## 1. Build verification

```bash
cd backend
dotnet restore
dotnet build --configuration Release

cd ../frontend/garmetix-web
npm ci
npm run build
```

## 2. Docker verification

```bash
docker compose config
docker compose up --build
```

Confirm:

- API is reachable at `/api/health`.
- Frontend opens.
- Login works.
- Admin user exists.
- Workspace company/store selection works.

## 3. Backup verification

From the app:

1. Open **Admin → System Health**.
2. Create a manual backup.
3. Click **Verify**.
4. Click **Preflight**.
5. Download the `.dump`, `.sha256`, and `.manifest.json` sidecars if using file-based archive storage.

From Linux/Mac:

```bash
scripts/linux/backup-db.sh
sha256sum -c backups/<file>.dump.sha256
```

From Windows PowerShell:

```powershell
scripts\windows\backup-db.ps1
```

## 4. Restore drill on test copy

Never make first restore attempt on production.

1. Copy the backup to a test machine or temporary database.
2. Run restore preflight.
3. Restore.
4. Open the app.
5. Run **Admin → Consistency & Repair** summary.
6. Confirm stock, invoice, GST, purchase, payment, and user records are visible.

## 5. Data consistency check

Open:

```text
/data-consistency
```

Resolve or document:

- negative stock;
- duplicate document numbers;
- GST header/item mismatch;
- payment mismatch;
- purchase overpayment;
- missing item HSN/name/unit snapshots;
- journal debit/credit imbalance.

Only use repair tools after previewing each action.

## 6. Security readiness

Confirm:

- default/dev passwords changed;
- JWT secret is configured outside source code;
- `.env` and `secrets/` are not committed;
- only Owner/Admin users can access repair/backup pages;
- Google Drive service account JSON is stored in `secrets/` and mounted read-only;
- public tunnel/domain is HTTPS-only.

## 7. Store workflow smoke test

Test one complete cycle:

1. Create or update product master with HSN/unit/tax/product group.
2. Purchase inward with vendor, supplier invoice date, due date, HSN, unit.
3. Billing sale with customer picker, salesman, split payment.
4. Sales return.
5. Partial purchase return.
6. Stock adjustment and movement ledger.
7. GST HSN summary and invoice register export.
8. Data consistency summary.
9. Backup create + verify + preflight.

## 8. Go-live rule

Proceed only when:

- build passes;
- Docker runs cleanly;
- backup verify/preflight passes;
- restore drill passes on test copy;
- data consistency page is clean or known issues are documented;
- store operator has tested billing and purchase flows.

## Stage 5B production hardening checklist

- [ ] Copy `.env.production.example` to `.env.production` on the production host.
- [ ] Run `scripts/linux/generate-secrets.sh .env.production`.
- [ ] Replace public domain, CORS origin, SMTP, backup, and optional cloud backup settings.
- [ ] Run `scripts/linux/production-preflight.sh .env.production`.
- [ ] Start with `docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build`.
- [ ] Verify `/api/health`, `/system-health`, and `/production-readiness`.
- [ ] Configure HTTPS through Cloudflare Tunnel, Caddy, Nginx, or equivalent.
- [ ] Enable systemd auto-start if running on Linux/Mac mini host.
- [ ] Confirm Docker log rotation is active.
- [ ] Run backup creation, backup verification, and restore preflight.
- [ ] Run `/data-consistency` and repair only reviewed issues.
- [ ] Keep the previous release ZIP and `.env.production` backup for rollback.
