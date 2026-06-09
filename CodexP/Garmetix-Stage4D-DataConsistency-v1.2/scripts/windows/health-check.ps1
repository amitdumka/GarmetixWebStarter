param(
    [string]$PublicUrl = "",
    [string]$ComposeFile = "docker-compose.yml"
)

. "$PSScriptRoot\common.ps1"

Set-GarmetixRootLocation

Write-Host "Garmetix health check"
Write-Host "Root: $(Get-GarmetixRoot)"
Write-Host ""

$localWeb = Test-GarmetixHttp -Name "Frontend" -Uri "http://localhost:3000"
$localApi = Test-GarmetixHttp -Name "API proxy" -Uri "http://localhost:3000/api/health" -Json

if ($localApi -and $localApi.databaseReady -ne $null) {
    Write-Host "Database ready: $($localApi.databaseReady)"
    Write-Host "API checked at UTC: $($localApi.checkedAtUtc)"
}

if (-not [string]::IsNullOrWhiteSpace($PublicUrl)) {
    $baseUrl = $PublicUrl.TrimEnd("/")
    Write-Host ""
    Test-GarmetixHttp -Name "Public frontend" -Uri $baseUrl | Out-Null
    Test-GarmetixHttp -Name "Public API proxy" -Uri "$baseUrl/api/health" -Json | Out-Null
}

Write-Host ""
Write-Host "Docker services"
$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile
& docker @composeArgs ps
