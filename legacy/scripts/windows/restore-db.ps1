param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [switch]$ConfirmRestore,
    [string]$ComposeFile = "docker-compose.yml",
    [string]$DatabaseService = "postgres"
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

if (Test-Path -LiteralPath "$($backupPath.Path).sha256") {
    $expected = (Get-Content -LiteralPath "$($backupPath.Path).sha256" -First 1).Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)[0]
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath.Path).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        throw "Checksum verification failed. Expected $expected but found $actual."
    }
    Write-Host "Checksum verified: $actual"
}

$header = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($backupPath.Path)[0..4])
if ($header -ne "PGDMP") {
    throw "This is not a PostgreSQL custom-format dump. Expected PGDMP header."
}

$envValues = Read-GarmetixEnv
$dbName = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_DB" -DefaultValue "garmetix"
$dbUser = Get-GarmetixEnvValue -Values $envValues -Name "POSTGRES_USER" -DefaultValue "garmetix"
$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile
$containerPath = "/tmp/garmetix-restore.dump"

Write-Host "Copying backup to database container..."
& docker @composeArgs cp $backupPath.Path "$DatabaseService`:$containerPath"

Write-Host "Running pg_restore --list preflight..."
& docker @composeArgs exec -T $DatabaseService pg_restore --list $containerPath | Select-Object -First 30
if ($LASTEXITCODE -ne 0) {
    throw "pg_restore preflight failed."
}

$typed = Read-Host "Type RESTORE to replace database '$dbName'"
if ($typed -ne "RESTORE") {
    & docker @composeArgs exec -T $DatabaseService rm -f $containerPath | Out-Null
    throw "Restore cancelled."
}

Write-Host "Creating safety backup before restore..."
& "$PSScriptRoot\backup-db.ps1" -OutputDirectory "backups" -ComposeFile $ComposeFile -DatabaseService $DatabaseService

Write-Host "Restoring PostgreSQL backup into database '$dbName' as user '$dbUser'."
& docker @composeArgs exec -T $DatabaseService pg_restore --clean --if-exists --no-owner --no-privileges --exit-on-error --single-transaction --username $dbUser --dbname $dbName $containerPath
if ($LASTEXITCODE -ne 0) {
    throw "Database restore failed."
}

& docker @composeArgs exec -T $DatabaseService rm -f $containerPath | Out-Null
Write-Host "Restore complete. Restart application services if needed."
