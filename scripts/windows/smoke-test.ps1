param(
    [string]$EnvFile = ".env.production",
    [string]$ApiBase = $env:API_BASE_URL
)

$ErrorActionPreference = "Stop"

if (Test-Path $EnvFile) {
    Get-Content $EnvFile | ForEach-Object {
        if ($_ -match '^\s*#' -or $_ -notmatch '=') { return }
        $parts = $_ -split '=', 2
        $name = $parts[0].Trim()
        $value = $parts[1].Trim().Trim('"')
        if ($name) { Set-Item -Path "Env:$name" -Value $value }
    }
}

if (-not $ApiBase) { $ApiBase = $env:PUBLIC_API_BASE_URL }
if (-not $ApiBase) { $ApiBase = "http://localhost:8080/api" }
$ApiBase = $ApiBase.TrimEnd('/')

Write-Host "Garmetix smoke test"
Write-Host "API: $ApiBase"

Write-Host "`n[1/4] API health..."
Invoke-RestMethod -Method Get -Uri "$ApiBase/health" | ConvertTo-Json -Depth 8
Write-Host "Health OK"

if ($env:GARMETIX_SMOKE_USER -and $env:GARMETIX_SMOKE_PASSWORD) {
    Write-Host "`n[2/4] Admin login..."
    $loginBody = @{ userName = $env:GARMETIX_SMOKE_USER; password = $env:GARMETIX_SMOKE_PASSWORD } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$ApiBase/auth/login" -ContentType "application/json" -Body $loginBody
    $token = $login.token
    if (-not $token) { $token = $login.accessToken }
    if (-not $token) { throw "Login did not return a token." }
    $headers = @{ Authorization = "Bearer $token" }
    Write-Host "Login OK"

    Write-Host "`n[3/4] Release smoke checks..."
    Invoke-RestMethod -Method Get -Uri "$ApiBase/release-stabilization/smoke-checks" -Headers $headers | ConvertTo-Json -Depth 10

    Write-Host "`n[4/4] Production readiness summary..."
    Invoke-RestMethod -Method Get -Uri "$ApiBase/production-readiness/summary" -Headers $headers | ConvertTo-Json -Depth 10
} else {
    Write-Host "`n[2/4] Authenticated checks skipped. Set GARMETIX_SMOKE_USER and GARMETIX_SMOKE_PASSWORD to enable them."
}

Write-Host "`nSmoke test completed."
