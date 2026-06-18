param(
    [string]$EnvFile = ".env.production",
    [string]$ApiBase = $env:API_BASE_URL,
    [string]$ExpectedVersion = "4.6.0",
    [string]$ExpectedBuildCode = "GARMETIX-8G-20260617-4600"
)

$ErrorActionPreference = "Stop"

if (Test-Path $EnvFile) {
    $content = [System.IO.File]::ReadAllText((Resolve-Path $EnvFile)) -replace "`r`n", "`n" -replace "`r", "`n"
    [System.IO.File]::WriteAllText((Resolve-Path $EnvFile), $content)
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $parts = $_ -split '=', 2
        $name = $parts[0].Trim()
        $value = $parts[1].Trim().Trim('"')
        if ($name) { Set-Item -Path "Env:$name" -Value $value }
    }
}

if (-not $ApiBase) { $ApiBase = $env:PUBLIC_API_BASE_URL }
if (-not $ApiBase) { $ApiBase = "http://localhost:5080/api" }
$ApiBase = $ApiBase.TrimEnd('/')

Write-Host "Garmetix API smoke test"
Write-Host "API: $ApiBase"

Write-Host "`n[1/5] API health..."
Invoke-RestMethod -Method Get -Uri "$ApiBase/health" | ConvertTo-Json -Depth 8
Write-Host "Health OK"

Write-Host "`n[2/5] App version..."
$appInfo = Invoke-RestMethod -Method Get -Uri "$ApiBase/app-info/version"
$appInfo | ConvertTo-Json -Depth 8
if ($appInfo.version -ne $ExpectedVersion -or $appInfo.buildCode -ne $ExpectedBuildCode) {
    throw "Expected $ExpectedVersion / $ExpectedBuildCode but got $($appInfo.version) / $($appInfo.buildCode)"
}

Write-Host "`n[3/5] Test automation manifest..."
$manifest = Invoke-RestMethod -Method Get -Uri "$ApiBase/test-automation/manifest"
$codes = @($manifest.checks | ForEach-Object { $_.code })
$required = @('BACKEND_UNIT_TESTS','FRONTEND_BUILD','FRONTEND_SMOKE','DOCKER_COMPOSE_BUILD','DOCKER_HEALTH','AUTHENTICATED_API_SMOKE')
$missing = @($required | Where-Object { $codes -notcontains $_ })
if ($missing.Count -gt 0) { throw "Missing manifest codes: $($missing -join ', ')" }
Write-Host "Manifest OK: $($manifest.checks.Count) checks"

Write-Host "`n[4/5] Runtime smoke endpoint..."
Invoke-RestMethod -Method Get -Uri "$ApiBase/test-automation/runtime-smoke" | ConvertTo-Json -Depth 10

if ($env:GARMETIX_SMOKE_USER -and $env:GARMETIX_SMOKE_PASSWORD) {
    Write-Host "`n[5/5] Admin login and authenticated checks..."
    $loginBody = @{ userName = $env:GARMETIX_SMOKE_USER; password = $env:GARMETIX_SMOKE_PASSWORD } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$ApiBase/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $login.token
    if (-not $token) { $token = $login.accessToken }
    if (-not $token) { throw "Login did not return a token." }
    $headers = @{ Authorization = "Bearer $token" }
    Invoke-RestMethod -Method Get -Uri "$ApiBase/release-stabilization/smoke-checks" -Headers $headers | ConvertTo-Json -Depth 10
    Invoke-RestMethod -Method Get -Uri "$ApiBase/production-readiness/summary" -Headers $headers | ConvertTo-Json -Depth 10
} else {
    Write-Host "`n[5/5] Authenticated checks skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD to enable them."
}

Write-Host "`nSmoke test completed."
