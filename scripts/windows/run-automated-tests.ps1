param(
    [switch]$SkipBackendTests,
    [switch]$SkipFrontendBuild,
    [switch]$RunFrontendSmoke,
    [switch]$RunDockerSmoke,
    [string]$EnvFile = ".env.production"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $Root

function Require-Command($Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Missing command: $Name"
    }
}

if (-not $SkipBackendTests) {
    Write-Host "== Backend xUnit tests =="
    Require-Command dotnet
    dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
}

if (-not $SkipFrontendBuild) {
    Write-Host "== Frontend Nuxt build =="
    Require-Command npm
    Push-Location frontend/garmetix-web
    npm ci
    npm run build
    Pop-Location
}

if ($RunFrontendSmoke) {
    Write-Host "== Frontend browserless smoke =="
    Require-Command npm
    Push-Location frontend/garmetix-web
    npm run smoke:frontend
    Pop-Location
}

if ($RunDockerSmoke) {
    Write-Host "== Docker/API smoke =="
    & "$PSScriptRoot\smoke-test.ps1" -EnvFile $EnvFile
}

python scripts/validation/stage8f-package2-static-checks.py
python scripts/validation/current-release-checks.py

Write-Host "Automated test runner completed."
