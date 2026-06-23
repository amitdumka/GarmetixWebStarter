param(
    [string]$TunnelName = "",
    [string]$Url = "http://localhost:3000",
    [switch]$Detached
)

$ErrorActionPreference = "Stop"

$cloudflared = Get-Command cloudflared -ErrorAction SilentlyContinue
if (-not $cloudflared) {
    throw "cloudflared was not found. Install Cloudflare Tunnel first, then run this script again."
}

if ([string]::IsNullOrWhiteSpace($TunnelName)) {
    $args = @("tunnel", "--url", $Url)
    Write-Host "Starting temporary Cloudflare tunnel to $Url"
}
else {
    $args = @("tunnel", "run", $TunnelName)
    Write-Host "Starting named Cloudflare tunnel: $TunnelName"
}

if ($Detached) {
    Start-Process -FilePath $cloudflared.Source -ArgumentList $args -WindowStyle Hidden
    Write-Host "Cloudflare tunnel started in background."
}
else {
    Write-Host "Keep this PowerShell window open while the tunnel is running."
    & $cloudflared.Source @args
}
