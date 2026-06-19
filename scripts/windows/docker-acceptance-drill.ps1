param(
    [string]$EnvFile = ".env.production",
    [string]$ComposeFile = "docker-compose.prod.yml",
    [string]$ExpectedVersion = "4.9.24",
    [string]$ExpectedBuildCode = "GARMETIX-8I-20260619-49240"
)

$ErrorActionPreference = "Stop"

function Import-GarmetixEnv([string]$Path) {
    if (-not (Test-Path $Path)) { throw "Environment file was not found: $Path" }
    $resolved = Resolve-Path $Path
    $content = [System.IO.File]::ReadAllText($resolved) -replace "`r`n", "`n" -replace "`r", "`n"
    [System.IO.File]::WriteAllText($resolved, $content)
    Get-Content $resolved | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $parts = $_ -split '=', 2
        $name = $parts[0].Trim()
        $value = $parts[1].Trim().Trim('"')
        if ($name) { Set-Item -Path "Env:$name" -Value $value }
    }
}

Write-Host "Garmetix Docker acceptance drill"
Write-Host "Env file: $EnvFile"
Write-Host "Expected: $ExpectedVersion / $ExpectedBuildCode"

Import-GarmetixEnv $EnvFile

$apiPort = if ($env:API_PORT) { $env:API_PORT } else { "5080" }
$webPort = if ($env:WEB_PORT) { $env:WEB_PORT } else { "3000" }
$apiBase = if ($env:API_BASE_URL) { $env:API_BASE_URL } elseif ($env:PUBLIC_API_BASE_URL) { $env:PUBLIC_API_BASE_URL } else { "http://localhost:$apiPort/api" }
$apiBase = $apiBase.TrimEnd('/')
$webBase = if ($env:GARMETIX_WEB_BASE_URL) { $env:GARMETIX_WEB_BASE_URL } elseif ($env:NUXT_PUBLIC_SITE_URL) { $env:NUXT_PUBLIC_SITE_URL } else { "http://localhost:$webPort" }
$webBase = $webBase.TrimEnd('/')

Write-Host "`n[1/8] Static route-access audit..."
python scripts/validation/frontend-route-access-check.py

Write-Host "`n[2/8] Build production Docker images..."
docker compose --env-file $EnvFile -f $ComposeFile build

Write-Host "`n[3/8] Start production containers..."
docker compose --env-file $EnvFile -f $ComposeFile up -d

Write-Host "`n[4/8] Wait for API health..."
$ready = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        Invoke-RestMethod -Method Get -Uri "$apiBase/health" -TimeoutSec 5 | Out-Null
        Write-Host "API health OK after $i attempt(s)."
        $ready = $true
        break
    } catch {
        Start-Sleep -Seconds 3
    }
}
if (-not $ready) {
    docker compose --env-file $EnvFile -f $ComposeFile ps
    docker compose --env-file $EnvFile -f $ComposeFile logs --tail=150 api
    throw "API health did not become ready at $apiBase/health"
}

Write-Host "`n[5/8] Web root and proxy checks..."
Invoke-WebRequest -Method Get -Uri "$webBase/" -TimeoutSec 20 | Out-Null
$appInfo = Invoke-RestMethod -Method Get -Uri "$webBase/api/app-info/version" -TimeoutSec 20
if ($appInfo.version -ne $ExpectedVersion -or $appInfo.buildCode -ne $ExpectedBuildCode) {
    throw "Web proxy app-info mismatch: $($appInfo.version) / $($appInfo.buildCode)"
}
Write-Host "Web proxy app-info OK: $($appInfo.version) / $($appInfo.buildCode)"

Write-Host "`n[6/8] API smoke test..."
$env:GARMETIX_EXPECTED_VERSION = $ExpectedVersion
$env:GARMETIX_EXPECTED_BUILD_CODE = $ExpectedBuildCode
$env:API_BASE_URL = $apiBase
& scripts/windows/smoke-test.ps1 -EnvFile $EnvFile -ApiBase $apiBase -ExpectedVersion $ExpectedVersion -ExpectedBuildCode $ExpectedBuildCode

if ($env:GARMETIX_SMOKE_USER -and $env:GARMETIX_SMOKE_PASSWORD) {
    Write-Host "`n[7/8] Authenticated workspace/navigation acceptance..."
    $loginBody = @{ userName = $env:GARMETIX_SMOKE_USER; password = $env:GARMETIX_SMOKE_PASSWORD } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$apiBase/auth/login" -ContentType "application/json" -Body $loginBody -TimeoutSec 30
    $token = if ($login.token) { $login.token } else { $login.accessToken }
    if (-not $token) { throw "Login did not return a bearer token." }
    $headers = @{ Authorization = "Bearer $token" }
    $dashboardHome = Invoke-RestMethod -Method Get -Uri "$apiBase/dashboard/home" -Headers $headers -TimeoutSec 30
    $workspace = Invoke-RestMethod -Method Get -Uri "$apiBase/workspace/options" -Headers $headers -TimeoutSec 30
    $setup = Invoke-RestMethod -Method Get -Uri "$apiBase/setup/status" -Headers $headers -TimeoutSec 30
    $release = Invoke-RestMethod -Method Get -Uri "$apiBase/release-stabilization/smoke-checks" -Headers $headers -TimeoutSec 30
    $readiness = Invoke-RestMethod -Method Get -Uri "$apiBase/production-readiness/summary" -Headers $headers -TimeoutSec 30
    if (-not $dashboardHome.route -or -not $dashboardHome.dashboardType) { throw "Dashboard home body is invalid." }
    if ($null -eq $workspace.companies -or $null -eq $workspace.storeGroups -or $null -eq $workspace.stores) { throw "Workspace options body is invalid." }
    if ($null -eq $setup.hasCompany -or $null -eq $setup.hasStore) { throw "Setup status body is invalid." }
    if (-not $release.checks) { throw "Release smoke checks returned no checks." }
    if (-not $readiness.checks) { throw "Production readiness returned no checks." }
    Write-Host "Dashboard route: $($dashboardHome.route) ($($dashboardHome.dashboardType))"
    Write-Host "Workspace options: $($workspace.companies.Count) companies, $($workspace.stores.Count) stores"
    Write-Host "Release/readiness checks OK"
} else {
    Write-Host "`n[7/8] Authenticated workspace/navigation acceptance skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD."
}

Write-Host "`n[8/8] Container status..."
docker compose --env-file $EnvFile -f $ComposeFile ps
Write-Host "`nDocker acceptance drill completed."
