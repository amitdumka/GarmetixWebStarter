# Garmetix Documentation

This folder is the canonical home for project plans, implementation notes, validation logs, operational guides, and reports.

## Start Here

- [Current roadmap](planning/CURRENT-ROADMAP.md)
- [Master feature checklist](planning/MASTER-TODO.md)
- [Open and fixed issues](planning/ISSUES.md)
- [Stage 7 implementation map](stages/stage-7/IMPLEMENTATION-MAP.md)
- [Stage 7M release notes](stages/stage-7/Stage7M-PreStage8-UiNaming-Menu-Cleanup-Notes.md)
- [Stage 8A Package 1 notes](stages/stage-8/Stage8A-Package1-UiAudit-Registers-Notes.md)
- [Stage 8A Package 2 / v4.0.0 notes](stages/stage-8/Stage8A-Package2-Commercial-Customer-v4.0.0-Notes.md)
- [Stage 8A Package 3 / v4.0.1 notes](stages/stage-8/Stage8A-Package3-Party-Voucher-v4.0.1-Notes.md)
- [Stage 8A Package 4 / v4.0.2 notes](stages/stage-8/Stage8A-Package4-Loyalty-PettyCash-v4.0.2-Notes.md)
- [Stage 8A Package 5 / v4.0.3 notes](stages/stage-8/Stage8A-Package5-Billing-SalesReturn-v4.0.3-Notes.md)
- [Stage 8A Package 6 / v4.0.4 notes](stages/stage-8/Stage8A-Package6-Purchase-LocalDate-v4.0.4-Notes.md)
- [Stage 8A Package 7 / v4.0.5 notes](stages/stage-8/Stage8A-Package7-DocumentQr-Print-Inventory-v4.0.5-Notes.md)
- [Stage 8A Package 8 / v4.0.6 notes](stages/stage-8/Stage8A-Package8-Payroll-Payment-v4.0.6-Notes.md)
- [Stage 8A Package 9 / v4.0.7 notes](stages/stage-8/Stage8A-Package9-Accounting-HR-v4.0.7-Notes.md)
- [Stage 8A Package 10 / v4.0.8 notes](stages/stage-8/Stage8A-Package10-Reports-DataOps-v4.0.8-Notes.md)
- [Stage 8A Package 11 / v4.0.9 notes](stages/stage-8/Stage8A-Package11-Product-MessageLogs-v4.0.9-Notes.md)
- [Stage 8A Package 12 / v4.0.10 notes](stages/stage-8/Stage8A-Package12-CashVoucher-v4.0.10-Notes.md)
- [Stage 8A Package 13 / v4.0.11 notes](stages/stage-8/Stage8A-Package13-OffBookGoods-v4.0.11-Notes.md)
- [Stage 8A Package 13 / v4.0.11 validation](stages/stage-8/Stage8A-Package13-OffBookGoods-v4.0.11-Validation.md)
- [Stage 8A Package 14 / v4.0.12 notes](stages/stage-8/Stage8A-Package14-AdminWorkspaces-v4.0.12-Notes.md)
- [Stage 8A Package 14 / v4.0.12 validation](stages/stage-8/Stage8A-Package14-AdminWorkspaces-v4.0.12-Validation.md)
- [Stage 8A Package 15 / v4.0.13 notes](stages/stage-8/Stage8A-Package15-MaintenanceWorkspaces-v4.0.13-Notes.md)
- [Stage 8A Package 15 / v4.0.13 validation](stages/stage-8/Stage8A-Package15-MaintenanceWorkspaces-v4.0.13-Validation.md)

## Categories

- `planning/` - current roadmap, master TODO, issue register, and archived planning transcripts.
- `stages/` - stage-by-stage implementation notes and validation logs.
- `modules/` - durable notes grouped by business or technical module.
- `operations/` - deployment, validation, release, backup, and go-live procedures.
- `guides/` - user and operator manuals.
- `reports/` - generated technical reports and audits.

## Documentation Rules

1. Put new roadmap items in `planning/CURRENT-ROADMAP.md`.
2. Record requested features in `planning/MASTER-TODO.md`.
3. Record reported defects in `planning/ISSUES.md`.
4. Store release notes and validation logs under the applicable `stages/stage-N/` folder.
5. Store long-lived implementation notes under the appropriate `modules/` folder.
6. Do not add new TODO or stage-note files to the repository root.
7. Keep `frontend/garmetix-web/public/docs/` only for documents served directly by the application.
