from pathlib import Path

root = Path(__file__).resolve().parents[2]
endpoints = (root / 'backend/Garmetix.Api/Backup/BackupEndpoints.cs').read_text()
for token in [
    'private static IResult MaintenanceStatusAsync',
    'service.GetMaintenanceStatus()',
    'private static async Task<IResult> MaintenanceCleanupAsync',
    'service.CleanupBackupDirectoryAsync(cancellationToken)',
    'private static IResult VerifyAllAsync',
    'service.VerifyAllBackups()'
]:
    if token not in endpoints:
        raise SystemExit(f'Missing backup maintenance compile hotfix token: {token}')
print('Stage 8G Package 1 compile hotfix static validation passed.')
