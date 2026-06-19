# Stage 8I Package 22 - HR Employee Master Upgrade v4.9.21

## Scope

Package 22 upgrades the HR Employee Master so it can support ID cards, future face/fingerprint attendance, payroll, leave and statutory workflows.

## Added

- Auto employee code support when the field is left blank.
- Employee profile fields: father/husband name, department, designation, salary type, monthly salary, daily wage, blood group and lifecycle status.
- Document/bank fields: Aadhaar, PAN, bank account name/number, IFSC, ESI number, PF number and emergency contact.
- Employee photo data URL storage for preview, ID-card print and future face attendance seed image.
- Employee lifecycle: Active, On Leave, Resigned, Terminated and Inactive with exit date/reason.
- HR summary endpoint: `/api/hr/employee-master/summary`.
- ID-card endpoint: `/api/hr/employees/{id}/id-card`.
- Frontend HR page now shows readiness warnings for missing photo, bank, Aadhaar and salary structure.

## Validation

Run:

```bash
python3 scripts/validation/hr-employee-master-check.py
```

Host acceptance:

```bash
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/hr-employee-master-acceptance-drill.sh .env.production
```
