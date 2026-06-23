param(
    [string]$OutputDirectory = "backups",
    [string]$ComposeFile = "docker-compose.yml",
    [string]$DatabaseService = "postgres"
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

$timestamp = Get-Date -AsUTC -Format "yyyyMMdd-HHmmss"
$fileName = "garmetix-manual-$timestamp.dump"
$backupPath = Join-Path $backupRoot $fileName
$containerPath = "/tmp/$fileName"
$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile

Write-Host "Creating PostgreSQL custom-format backup: $backupPath"
& docker @composeArgs exec -T $DatabaseService pg_dump --format=custom --compress=6 --no-owner --no-privileges --username $dbUser --file $containerPath $dbName
if ($LASTEXITCODE -ne 0) {
    throw "Database backup failed."
}

& docker @composeArgs cp "$DatabaseService`:$containerPath" $backupPath
& docker @composeArgs exec -T $DatabaseService rm -f $containerPath

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $backupPath
"$($hash.Hash.ToLowerInvariant())  $fileName" | Set-Content -LiteralPath "$backupPath.sha256" -Encoding UTF8
$manifest = [ordered]@{
    fileName = $fileName
    createdAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    source = "manual-powershell"
    database = $dbName
    service = $DatabaseService
    format = "PostgreSQL custom pg_dump"
    sha256 = $hash.Hash.ToLowerInvariant()
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$backupPath.manifest.json" -Encoding UTF8

Write-Host "Backup complete."
Write-Host $backupPath
Write-Host "Checksum: $($hash.Hash.ToLowerInvariant())"
