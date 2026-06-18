# Audit UI / Activity Logs Notes

Audit page now supports:

- Module filter
- Action filter
- Actor/user filter
- Entity filter
- From/to date filters
- Keyword search
- Field detail view for a selected entity
- Filtered CSV export

The audit feed is still based mainly on existing entity timestamps and soft-delete flags. For future deep audit requirements, add a dedicated change-log table that records before/after JSON per save operation.
