# Stage 10L Production Support - v4.10.30

Build: `GARMETIX-10L-20260620-4130`

Stage 10L adds a support console at `/production-support` for Admin/Owner.

## Support Drills

- Save failure drill: visible save message, Message Logs, Runtime Diagnostics and duplicate-save check.
- Print or PDF failure drill: Download PDF, Document Scanner, print log evidence and Print Final Acceptance.
- Backup warning drill: local backup, restore drill, Production Readiness and Google Drive backup review.
- Email or share failure drill: SMTP diagnostics, test email, Message Logs and PDF fallback evidence.
- Tunnel or API mismatch drill: public origin, proxy headers, Runtime Diagnostics, API Health and hosted URL confirmation.

## API Contract

- `GET /api/stage10l/production-support`
- `GET /api/stage10l/production-support/drills`

Both endpoints require Admin authorization and return safe degraded payloads if an unexpected backend error occurs.

## Operator Evidence

For every production issue, capture:

- Page name and operation performed.
- Visible message without server URL.
- Version and build code.
- Message Log id when available.
- Document number, store, date and user.
- Public URL used by the operator when the issue happened.
