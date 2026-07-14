param(
    [int]$MockPort = 8788,
    [int]$BridgePort = 8787,
    [int]$UnsafeBridgePort = 8789
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..\..")
$mockProject = Join-Path $repoRoot "apps\Garmetix.MantraMockService\Garmetix.MantraMockService.csproj"
$bridgeProject = Join-Path $repoRoot "apps\Garmetix.FingerprintBridge\Garmetix.FingerprintBridge.csproj"
$mockDll = Join-Path $repoRoot "apps\Garmetix.MantraMockService\bin\Release\net10.0\Garmetix.MantraMockService.dll"
$bridgeDll = Join-Path $repoRoot "apps\Garmetix.FingerprintBridge\bin\Release\net10.0\Garmetix.FingerprintBridge.dll"
$runId = [Guid]::NewGuid().ToString("N")
$logDir = Join-Path ([System.IO.Path]::GetTempPath()) "garmetix-stage11b-mantra-$runId"
$processes = New-Object System.Collections.Generic.List[System.Diagnostics.Process]

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Wait-Json {
    param([string]$Uri, [int]$TimeoutSeconds = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            return Invoke-RestMethod -Method Get -Uri $Uri -TimeoutSec 3
        } catch {
            Start-Sleep -Milliseconds 500
        }
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for $Uri"
}

function Start-DotnetApp {
    param([string]$Name, [string]$DllPath, [string[]]$AppArgs)

    $stdout = Join-Path $logDir "$Name.out.log"
    $stderr = Join-Path $logDir "$Name.err.log"
    $args = @($DllPath) + $AppArgs
    $process = Start-Process -FilePath "dotnet" -ArgumentList $args -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $processes.Add($process)
    return $process
}

function Stop-StartedProcesses {
    for ($index = $processes.Count - 1; $index -ge 0; $index--) {
        $process = $processes[$index]
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

try {
    Write-Host "Building Mantra mock service and fingerprint bridge..."
    & dotnet build $mockProject -c Release --nologo
    & dotnet build $bridgeProject -c Release --nologo

    Start-DotnetApp "mantra-mock" $mockDll @("--MockMantra:Urls=http://127.0.0.1:$MockPort") | Out-Null
    Wait-Json "http://127.0.0.1:$MockPort/health" | Out-Null
    Write-Host "PASS: Mantra mock service health is available."

    Start-DotnetApp "fingerprint-bridge-safe" $bridgeDll @(
        "--Bridge:Urls=http://127.0.0.1:$BridgePort",
        "--Bridge:Adapter=Mantra",
        "--Bridge:MantraServiceUrl=http://127.0.0.1:$MockPort/",
        "--Bridge:MantraEnrollPath=/enroll"
    ) | Out-Null
    Wait-Json "http://127.0.0.1:$BridgePort/garmetix-fingerprint/health" | Out-Null

    $payload = @{
        employeeCode = "MGR-REHEARSAL"
        employeeName = "Manager Rehearsal"
        rawPayloadAllowed = $false
    } | ConvertTo-Json -Depth 8
    $safeEnroll = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$BridgePort/garmetix-fingerprint/enroll" -ContentType "application/json" -Body $payload -TimeoutSec 15
    Assert-True $safeEnroll.success "Expected safe enroll success."
    Assert-True ($safeEnroll.matchStatus -eq "Enrolled") "Expected safe enroll matchStatus Enrolled."
    Assert-True (-not $safeEnroll.rawPayloadStored) "Expected safe enroll rawPayloadStored=false."
    Assert-True (-not [string]::IsNullOrWhiteSpace($safeEnroll.templateRef)) "Expected safe enroll templateRef."
    Write-Host "PASS: Safe Mantra enroll returns template reference without raw payload."

    Stop-Process -Id $processes[$processes.Count - 1].Id -Force -ErrorAction SilentlyContinue

    Start-DotnetApp "fingerprint-bridge-raw-block" $bridgeDll @(
        "--Bridge:Urls=http://127.0.0.1:$UnsafeBridgePort",
        "--Bridge:Adapter=Mantra",
        "--Bridge:MantraServiceUrl=http://127.0.0.1:$MockPort/",
        "--Bridge:MantraEnrollPath=/unsafe/enroll-with-raw"
    ) | Out-Null
    Wait-Json "http://127.0.0.1:$UnsafeBridgePort/garmetix-fingerprint/health" | Out-Null

    $blockedEnroll = Invoke-RestMethod -Method Post -Uri "http://127.0.0.1:$UnsafeBridgePort/garmetix-fingerprint/enroll" -ContentType "application/json" -Body $payload -TimeoutSec 15
    Assert-True (-not $blockedEnroll.success) "Expected unsafe enroll to be blocked."
    Assert-True ($blockedEnroll.matchStatus -eq "RawPayloadBlocked") "Expected RawPayloadBlocked."
    Assert-True (-not $blockedEnroll.rawPayloadStored) "Expected blocked enroll rawPayloadStored=false."
    Assert-True ([string]::IsNullOrWhiteSpace($blockedEnroll.templateRef)) "Expected blocked enroll templateRef to be empty."
    Write-Host "PASS: Raw biometric-looking Mantra response is blocked."

    Write-Host "Stage 11B Mantra contract rehearsal passed."
} finally {
    Stop-StartedProcesses
    Write-Host "Logs: $logDir"
}
