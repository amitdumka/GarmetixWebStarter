<#
.SYNOPSIS
  Deploy Garmetix from Windows 11 to the Mac mini Ubuntu server using OpenSSH + Docker.

.DESCRIPTION
  This script is the Windows/PowerShell equivalent of deploy/deploy-to-macmini.sh.
  It is designed for Windows 11 PowerShell 7+ or Windows PowerShell 5.1 with OpenSSH Client enabled.

  Recommended authentication: SSH key. Password-based SSH can still work interactively,
  but Windows OpenSSH does not support non-interactive password passing like sshpass.

.USAGE
  1. Copy deploy/macmini.env.example to deploy/macmini.env and edit Cloudflare values.
  2. Set up SSH key login to amit@192.168.11.126.
  3. Run:
       Set-ExecutionPolicy -Scope Process Bypass -Force
       .\deploy\deploy-to-macmini-windows.ps1
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = "deploy\macmini.env"
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host "`n[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message" -ForegroundColor Cyan
}

function Require-Command($Name) {
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Missing required command: $Name. Install/enable it and run again."
    }
}

function Read-DotEnv($Path) {
    $map = @{}
    if (-not (Test-Path $Path)) {
        throw "Missing $Path. Copy deploy\macmini.env.example to deploy\macmini.env and edit it first."
    }

    Get-Content $Path | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith('#')) { return }
        $idx = $line.IndexOf('=')
        if ($idx -lt 0) { return }
        $key = $line.Substring(0, $idx).Trim()
        $value = $line.Substring($idx + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $map[$key] = $value
    }
    return $map
}

Require-Command ssh
Require-Command scp
Require-Command tar

$RootDir = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ConfigFullPath = if ([System.IO.Path]::IsPathRooted($ConfigPath)) { $ConfigPath } else { Join-Path $RootDir $ConfigPath }
$cfg = Read-DotEnv $ConfigFullPath

$ServerHost = if ($cfg.SERVER_HOST) { $cfg.SERVER_HOST } else { "192.168.11.126" }
$ServerUser = if ($cfg.SERVER_USER) { $cfg.SERVER_USER } else { "amit" }
$SshPort = if ($cfg.SSH_PORT) { $cfg.SSH_PORT } else { "22" }
$RemoteAppDir = if ($cfg.REMOTE_APP_DIR) { $cfg.REMOTE_APP_DIR } else { "/opt/garmetix" }
$Domain = if ($cfg.DOMAIN) { $cfg.DOMAIN } else { "garmetix.aadwikafashion.in" }
$PublicHttpsUrl = if ($cfg.PUBLIC_HTTPS_URL) { $cfg.PUBLIC_HTTPS_URL } else { "https://$Domain" }
$SudoPassword = if ($cfg.SUDO_PASSWORD) { $cfg.SUDO_PASSWORD } elseif ($cfg.SSH_PASSWORD -and -not $cfg.SSH_PASSWORD.StartsWith('CHANGE_ME')) { $cfg.SSH_PASSWORD } else { "" }

if ($cfg.SSH_PASSWORD -and -not $cfg.SSH_PASSWORD.StartsWith('CHANGE_ME')) {
    Write-Warning "Windows OpenSSH cannot use SSH_PASSWORD non-interactively. Use SSH key login, or be ready to type the password when ssh/scp prompts."
}

Write-Step "Testing SSH connection to $ServerUser@$ServerHost"
ssh -p $SshPort -o StrictHostKeyChecking=accept-new "$ServerUser@$ServerHost" "echo SSH_OK" | Out-Host

$Release = "release-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$Archive = Join-Path $env:TEMP "garmetix-$Release.tar.gz"
$RemoteArchive = "/tmp/garmetix-$Release.tar.gz"

Write-Step "Creating deployment archive"
Push-Location $RootDir
try {
    if (Test-Path $Archive) { Remove-Item $Archive -Force }
    tar --exclude="./.git" `
        --exclude="./frontend/garmetix-web/node_modules" `
        --exclude="./frontend/garmetix-web/.nuxt" `
        --exclude="./frontend/garmetix-web/.output" `
        --exclude="./backend/Garmetix.Api/bin" `
        --exclude="./backend/Garmetix.Api/obj" `
        --exclude="./backend/Garmetix.Api.Tests/bin" `
        --exclude="./backend/Garmetix.Api.Tests/obj" `
        --exclude="./backups" `
        -czf $Archive .
}
finally {
    Pop-Location
}

Write-Step "Uploading package to Ubuntu server"
scp -P $SshPort -o StrictHostKeyChecking=accept-new $Archive "${ServerUser}@${ServerHost}:$RemoteArchive"
scp -P $SshPort -o StrictHostKeyChecking=accept-new (Join-Path $RootDir "deploy\install-docker-ubuntu.sh") "${ServerUser}@${ServerHost}:/tmp/install-docker-ubuntu.sh"

$SudoPassB64 = ""
if ($SudoPassword) {
    $SudoPassB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($SudoPassword))
}

$RemoteScript = @"
set -Eeuo pipefail
REMOTE_APP_DIR='$RemoteAppDir'
RELEASE='$Release'
REMOTE_ARCHIVE='$RemoteArchive'
SUDO_PASS_B64='$SudoPassB64'
DOMAIN='$Domain'
PUBLIC_HTTPS_URL='$PublicHttpsUrl'

sudo_run() {
  if [[ -n "\$SUDO_PASS_B64" ]]; then
    local sp
    sp="\$(printf '%s' "\$SUDO_PASS_B64" | base64 -d)"
    printf '%s\n' "\$sp" | sudo -S "\$@"
  else
    sudo "\$@"
  fi
}

sudo_run bash /tmp/install-docker-ubuntu.sh
sudo_run mkdir -p "\${REMOTE_APP_DIR}/releases/\${RELEASE}"
sudo_run chown -R "\$USER:\$USER" "\$REMOTE_APP_DIR"
tar -xzf "\$REMOTE_ARCHIVE" -C "\${REMOTE_APP_DIR}/releases/\${RELEASE}"
ln -sfn "\${REMOTE_APP_DIR}/releases/\${RELEASE}" "\${REMOTE_APP_DIR}/current"
cd "\${REMOTE_APP_DIR}/current"
chmod +x deploy/*.sh
DOMAIN="\$DOMAIN" PUBLIC_HTTPS_URL="\$PUBLIC_HTTPS_URL" ./deploy/create-production-env.sh
if [[ -n "\${CLOUDFLARE_API_TOKEN:-}" || -f deploy/macmini.env ]]; then
  ./deploy/cloudflare-create-or-update-tunnel.sh || echo 'Cloudflare automation skipped/failed. You can set CLOUDFLARE_TUNNEL_TOKEN manually in .env.production.'
fi
./deploy/run-production.sh
rm -f "\$REMOTE_ARCHIVE" /tmp/install-docker-ubuntu.sh
"@

Write-Step "Installing Docker if needed and starting Garmetix stack"
$RemoteScript | ssh -p $SshPort -o StrictHostKeyChecking=accept-new "$ServerUser@$ServerHost" "bash -s"

Remove-Item $Archive -Force -ErrorAction SilentlyContinue
Write-Step "Deployment complete"
Write-Host "Open: https://$Domain" -ForegroundColor Green
