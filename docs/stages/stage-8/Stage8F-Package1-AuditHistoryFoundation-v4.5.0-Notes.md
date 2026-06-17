# Stage 8F Package 1 - Audit History Foundation (v4.5.0)

Build code: `GARMETIX-8F-20260617-4500`

This package starts Stage 8F by replacing the earlier derived audit view with persistent audit-log capture at the DbContext level.

## Implemented

- Added `AuditLogEntry` persistent model and `AuditLogEntries` table.
- Added audit actor middleware that captures authenticated user, workspace scope, request path, IP address and trace id.
- Added DbContext `SaveChanges` audit capture for create, update, soft-delete and physical-delete operations.
- Added before/after JSON snapshots and field-level change list.
- Excluded sensitive fields such as password, token, secret, signing key and API key values.
- Added `/api/audit/events/{auditLogId}` for event-level detail.
- Updated `/api/audit/recent` to read persistent audit entries first and fall back to legacy CreatedAt/UpdatedAt rows before new activity exists.
- Updated Audit UI to show changed-field counts and event detail context.
- Added idempotent runtime schema repair for `AuditLogEntries` so existing Docker volumes can upgrade without replaying historical migrations.

## Notes

- Audit history is not a replacement for message logs. Message logs show request/runtime outcomes; audit logs show persisted business data changes.
- Existing historical rows before this package remain visible through legacy fallback, but only new saves after v4.5.0 contain field-level before/after values.
