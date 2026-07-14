param(
    [switch]$Build,
    [switch]$StartTunnel,
    [switch]$DetachedTunnel,
    [string]$TunnelName = "",
    [string]$TunnelUrl = "http://localhost:3000",
    [string]$PublicUrl = "https://garmetix.aadwikafashion.in",
    [string]$ComposeFile = "docker-compose.yml"
)

. "$PSScriptRoot\common.ps1"

Set-GarmetixRootLocation

$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile
if ($Build) {
    Write-Host "Starting Garmetix with Docker rebuild..."
    & docker @composeArgs up --build -d
}
else {
    Write-Host "Starting Garmetix..."
    & docker @composeArgs up -d
}

if ($LASTEXITCODE -ne 0) {
    throw "Docker compose start failed."
}

Write-Host ""
& "$PSScriptRoot\health-check.ps1" -PublicUrl $PublicUrl -ComposeFile $ComposeFile

if ($StartTunnel) {
    Write-Host ""
    & "$PSScriptRoot\start-cloudflare-tunnel.ps1" -TunnelName $TunnelName -Url $TunnelUrl -Detached:$DetachedTunnel
}
