# License / SaaS Activation Operation Guide v4.9.20

## Production environment keys

```bash
LICENSE_ENFORCEMENT_ENABLED=true
LICENSE_REQUIRE_OPERATIONAL_APIS=true
LICENSE_PRODUCT_CODE=GARMETIX-WEB
LICENSE_MASTER_SECRET=your-48-plus-character-private-master-secret
LICENSE_DEFAULT_PLAN=Trial
LICENSE_DEFAULT_VALIDITY_DAYS=365
LICENSE_DEFAULT_MAX_STORES=1
LICENSE_DEFAULT_MAX_USERS=10
LICENSE_REQUIRED_MODULES=Billing,Inventory,Purchase,Accounting,GST,HR,Payroll
```

Keep `LICENSE_MASTER_SECRET` only in the real production `.env.production` file. Do not upload or commit it.

## Activation steps

1. Login as Admin or Owner.
2. Open `/license-activation`.
3. Confirm status and master-secret readiness.
4. Generate a client license or paste a license issued from your master installation.
5. Activate the key.
6. Refresh and confirm `State = Valid`.
7. Turn on `LICENSE_ENFORCEMENT_ENABLED=true` and restart the API.

## Host acceptance drill

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/license-acceptance-drill.sh .env.production
```

Optional generation test:

```bash
export GARMETIX_LICENSE_GENERATE_TEST=true
./scripts/linux/license-acceptance-drill.sh .env.production
```

Optional generation plus activation test:

```bash
export GARMETIX_LICENSE_GENERATE_TEST=true
export GARMETIX_LICENSE_ACTIVATE_GENERATED=true
./scripts/linux/license-acceptance-drill.sh .env.production
```

Use activation carefully on production hosts because it writes the live activation file.
