$ErrorActionPreference = "Stop"

function Get-GarmetixRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}

function Set-GarmetixRootLocation {
    Set-Location -LiteralPath (Get-GarmetixRoot)
}

function Read-GarmetixEnv {
    $root = Get-GarmetixRoot
    $envPath = Join-Path $root ".env"
    $values = @{}

    if (-not (Test-Path -LiteralPath $envPath)) {
        return $values
    }

    foreach ($line in Get-Content -LiteralPath $envPath) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
            continue
        }

        $parts = $trimmed.Split("=", 2)
        if ($parts.Count -eq 2) {
            $values[$parts[0].Trim()] = $parts[1].Trim().Trim('"').Trim("'")
        }
    }

    return $values
}

function Get-GarmetixEnvValue {
    param(
        [hashtable]$Values,
        [string]$Name,
        [string]$DefaultValue
    )

    if ($Values.ContainsKey($Name) -and -not [string]::IsNullOrWhiteSpace($Values[$Name])) {
        return $Values[$Name]
    }

    return $DefaultValue
}

function Get-GarmetixComposeArgs {
    param([string]$ComposeFile)

    if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
        return @("compose")
    }

    return @("compose", "-f", $ComposeFile)
}

function Test-GarmetixHttp {
    param(
        [string]$Name,
        [string]$Uri,
        [switch]$Json
    )

    try {
        if ($Json) {
            $result = Invoke-RestMethod -Uri $Uri -TimeoutSec 15
        }
        else {
            $result = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 15
        }

        Write-Host "[OK] $Name - $Uri"
        return $result
    }
    catch {
        Write-Host "[FAIL] $Name - $Uri"
        Write-Host "       $($_.Exception.Message)"
        return $null
    }
}
