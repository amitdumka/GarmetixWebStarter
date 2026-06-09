param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [switch]$ConfirmRestore,
    [string]$ComposeFile = "docker-compose.yml"
)

. "$PSScriptRoot\common.ps1"

Set-GarmetixRootLocation

if (-not $ConfirmRestore) {
    throw "Restore was not run. Add -ConfirmRestore after checking the backup file and target database."
}

$backupPath = Resolve-Path -LiteralPath $BackupFile
if (-not (Test-Path -LiteralPath $backupPath.Path)) {
    throw "Backup file not found: $BackupFile"
}

$envValues = Read-GarmetixEnv
$dbName = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_DB" -DefaultValue "garmetix"
$dbUser = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_USER" -DefaultValue "garmetix"
$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile

Write-Host "Restoring PostgreSQL backup into database '$dbName' as user '$dbUser'."
Write-Host "Backup: $($backupPath.Path)"

Get-Content -LiteralPath $backupPath.Path | & docker @composeArgs exec -T postgres psql -U $dbUser -d $dbName

if ($LASTEXITCODE -ne 0) {
    throw "Database restore failed."
}

Write-Host "Restore complete."
