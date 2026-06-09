<#
.SYNOPSIS
  Full local validation helper for Garmetix Web on Windows PowerShell.

.DESCRIPTION
  Runs backend restore/build/publish, frontend npm install/build, Docker compose config/build/up,
  API health checks, database schema repair, and captures logs to validation-results/.

.PARAMETER SkipDocker
  Skip Docker compose validation.

.PARAMETER NoCacheApi
  Rebuild the API Docker image with --no-cache before starting compose.

.PARAMETER KeepRunning
  Leave Docker services running after validation. By default this script keeps them running so you can test the UI.
#>
param(
  [switch]$SkipDocker,
  [switch]$NoCacheApi,
  [switch]$KeepRunning = $true
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$results = Join-Path $root 'validation-results'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$summary = Join-Path $results "validation-$stamp.md"

function Add-Line([string]$line = '') {
  Add-Content -Path $summary -Value $line
}

function Run-Step([string]$name, [scriptblock]$body) {
  Write-Host "\n==> $name" -ForegroundColor Cyan
  Add-Line "## $name"
  try {
    & $body 2>&1 | Tee-Object -FilePath (Join-Path $results (($name -replace '[^a-zA-Z0-9-]', '_') + "-$stamp.log"))
    Add-Line "Status: PASS"
  }
  catch {
    Add-Line "Status: FAIL"
    Add-Line "Error: $($_.Exception.Message)"
    throw
  }
  Add-Line
}

function Test-CommandExists([string]$command) {
  if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
    throw "Required command not found: $command"
  }
}

Set-Location $root
Add-Line "# Garmetix Local Validation - $stamp"
Add-Line
Add-Line "Project root: $root"
Add-Line

Run-Step 'tool-versions' {
  Test-CommandExists 'dotnet'
  Test-CommandExists 'npm'
  if (-not $SkipDocker) { Test-CommandExists 'docker' }
  dotnet --info
  npm --version
  if (-not $SkipDocker) { docker version }
}

Run-Step 'backend-restore' {
  dotnet restore backend/Garmetix.Api/Garmetix.Api.csproj
}

Run-Step 'backend-build-release' {
  dotnet build backend/Garmetix.Api/Garmetix.Api.csproj -c Release --no-restore
}

Run-Step 'backend-publish-release' {
  dotnet publish backend/Garmetix.Api/Garmetix.Api.csproj -c Release -o validation-results/publish-api --no-restore
}

Run-Step 'frontend-npm-ci' {
  Push-Location frontend/garmetix-web
  npm ci
  Pop-Location
}

Run-Step 'frontend-build' {
  Push-Location frontend/garmetix-web
  npm run build
  Pop-Location
}

if (-not $SkipDocker) {
  Run-Step 'docker-compose-config' {
    docker compose config
  }

  Run-Step 'docker-compose-build' {
    if ($NoCacheApi) {
      docker compose build --no-cache api
      docker compose build web
    }
    else {
      docker compose build
    }
  }

  Run-Step 'docker-compose-up' {
    docker compose up -d
    Start-Sleep -Seconds 12
    docker compose ps
  }

  Run-Step 'api-health' {
    $health = Invoke-RestMethod -Uri 'http://localhost:5080/api/health' -TimeoutSec 20
    $health | ConvertTo-Json -Depth 10
  }

  Run-Step 'database-repair-endpoint-note' {
    Write-Host 'Database repair endpoint requires Admin JWT. Use scripts/repair-database.ps1 -Token <JWT> after logging in as admin.'
  }

  Run-Step 'web-health' {
    $response = Invoke-WebRequest -Uri 'http://localhost:3000' -TimeoutSec 20
    "StatusCode: $($response.StatusCode)"
  }

  Run-Step 'docker-logs-tail' {
    docker compose logs api --tail=200 | Tee-Object -FilePath (Join-Path $results "api-tail-$stamp.log")
    docker compose logs web --tail=120 | Tee-Object -FilePath (Join-Path $results "web-tail-$stamp.log")
    docker compose logs postgres --tail=120 | Tee-Object -FilePath (Join-Path $results "postgres-tail-$stamp.log")
  }

  if (-not $KeepRunning) {
    Run-Step 'docker-compose-down' { docker compose down }
  }
}

Add-Line '## Final note'
Add-Line 'If this file shows PASS for backend, frontend, docker, API health, and web health, the build is ready for manual functional testing.'
Write-Host "\nValidation complete. Summary: $summary" -ForegroundColor Green
