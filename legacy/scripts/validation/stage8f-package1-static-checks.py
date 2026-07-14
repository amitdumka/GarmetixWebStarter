from pathlib import Path

root = Path(__file__).resolve().parents[2]
checks = {
    'AuditLogEntry model': root / 'backend/Garmetix.Domain/Generated/Models/Audit/AuditLogEntry.cs',
    'AuditActorContext': root / 'backend/Garmetix.Infrastructure/Audit/AuditActorContext.cs',
    'AuditActorMiddleware': root / 'backend/Garmetix.Api/Audit/AuditActorMiddleware.cs',
    'Audit endpoints': root / 'backend/Garmetix.Api/Audit/AuditEndpoints.cs',
    'DbContext': root / 'backend/Garmetix.Infrastructure/Data/GarmetixDbContext.cs',
    'Audit UI': root / 'frontend/garmetix-web/pages/audit/index.vue',
    'App version': root / 'frontend/garmetix-web/utils/appVersion.ts',
}
missing = [name for name, path in checks.items() if not path.exists()]
if missing:
    raise SystemExit(f'Missing files: {missing}')

ctx = checks['DbContext'].read_text()
required_ctx = [
    'DbSet<AuditLogEntry> AuditLogEntries',
    'AddAuditLogEntries();',
    'BuildAuditLogEntries',
    'IsSensitiveAuditProperty',
    'ResolveAuditModule',
]
for token in required_ctx:
    if token not in ctx:
        raise SystemExit(f'DbContext missing {token}')

program = (root / 'backend/Garmetix.Api/Program.cs').read_text()
if 'UseMiddleware<AuditActorMiddleware>()' not in program:
    raise SystemExit('Program.cs does not register AuditActorMiddleware')

repair = (root / 'backend/Garmetix.Api/Database/DatabaseSchemaRepairService.cs').read_text()
if 'CREATE TABLE IF NOT EXISTS "AuditLogEntries"' not in repair:
    raise SystemExit('Schema repair missing AuditLogEntries table')

api = checks['Audit endpoints'].read_text()
for token in ['MapGet("/events/{auditLogId:guid}"', 'QueryPersistentAuditRowsAsync', 'EventDetailAsync', 'BeforeJson', 'ChangesJson']:
    if token not in api:
        raise SystemExit(f'Audit endpoints missing {token}')

ui = checks['Audit UI'].read_text()
for token in ['audit/events/${auditLogId}', 'changedFieldCount', 'Field Changes', 'before/after field changes']:
    if token not in ui:
        raise SystemExit(f'Audit UI missing {token}')

version = checks['App version'].read_text()
if not ("APP_VERSION = '4." in version) or not ('Stage 8' in version):
    raise SystemExit('Frontend version is not compatible with Stage 8F Package 1 or later')

csproj = (root / 'backend/Garmetix.Api/Garmetix.Api.csproj').read_text()
if '<Version>4.' not in csproj:
    raise SystemExit('Backend version is not compatible with Stage 8F Package 1 or later')

print('Stage 8F Package 1 static validation passed.')
