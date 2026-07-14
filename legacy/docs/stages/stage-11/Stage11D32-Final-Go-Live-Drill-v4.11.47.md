# Stage 11D-32 — Final Go-Live Drill

Version: **4.11.47**

This stage adds a single final drill script that runs Docker/API checks plus Stage 11D-30 GST/import validation and Stage 11D-31 payroll readiness validation.

Run:

```bash
cd /opt/garmetix/current
chmod +x scripts/production/*.sh
./scripts/production/stage11d32-final-go-live-drill.sh
```

Final report is saved under:

```txt
reports/stage11d32-final-go-live-drill/
```

Treat this as the final production rehearsal. Go live only after API health, app info, import/GST checks, payroll readiness, PDF checks, and attendance punch checks pass.
