param(
    [string]$EnvFile = ".env.production",
    [string]$ComposeFile = "docker-compose.prod.yml",
    [string]$PostgresService = "postgres",
    [string]$BackupDir = "./backups"
)

$ErrorActionPreference = "Stop"
Write-Host "Garmetix backup/restore drill"
Write-Host "Env file: $EnvFile"
Write-Host "Compose file: $ComposeFile"
Write-Host ""
Write-Host "For Windows hosts, run the Linux drill through WSL for the safest stream/pipe behavior:"
Write-Host "  wsl bash scripts/linux/backup-restore-drill.sh $EnvFile"
Write-Host ""
Write-Host "This wrapper is intentionally non-destructive and delegates to the Linux drill."
