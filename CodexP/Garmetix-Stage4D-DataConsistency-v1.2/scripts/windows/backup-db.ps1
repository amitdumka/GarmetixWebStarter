param(
    [string]$OutputDirectory = "backups",
    [string]$ComposeFile = "docker-compose.yml"
)

. "$PSScriptRoot\common.ps1"

Set-GarmetixRootLocation

$envValues = Read-GarmetixEnv
$dbName = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_DB" -DefaultValue "garmetix"
$dbUser = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_USER" -DefaultValue "garmetix"

$backupRoot = Join-Path (Get-GarmetixRoot) $OutputDirectory
if (-not (Test-Path -LiteralPath $backupRoot)) {
    New-Item -ItemType Directory -Path $backupRoot | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupPath = Join-Path $backupRoot "garmetix-$timestamp.sql"
$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile

Write-Host "Creating PostgreSQL backup: $backupPath"
& docker @composeArgs exec -T postgres pg_dump -U $dbUser -d $dbName | Set-Content -LiteralPath $backupPath -Encoding UTF8

if ($LASTEXITCODE -ne 0) {
    throw "Database backup failed."
}

Write-Host "Backup complete."
Write-Host $backupPath
