param(
    [ValidateSet("all", "web", "api", "postgres")]
    [string]$Service = "all",
    [switch]$Build,
    [string]$PublicUrl = "",
    [string]$ComposeFile = "docker-compose.yml"
)

. "$PSScriptRoot\common.ps1"

Set-GarmetixRootLocation

$composeArgs = Get-GarmetixComposeArgs -ComposeFile $ComposeFile

if ($Build) {
    Write-Host "Rebuilding and starting Garmetix..."
    if ($Service -eq "all") {
        & docker @composeArgs up --build -d
    }
    else {
        & docker @composeArgs up --build -d $Service
    }
}
elseif ($Service -eq "all") {
    Write-Host "Restarting all Garmetix services..."
    & docker @composeArgs restart
}
else {
    Write-Host "Restarting Garmetix service: $Service"
    & docker @composeArgs restart $Service
}

if ($LASTEXITCODE -ne 0) {
    throw "Docker compose restart failed."
}

Write-Host ""
& "$PSScriptRoot\health-check.ps1" -PublicUrl $PublicUrl -ComposeFile $ComposeFile
