param(
    [string]$ApiBaseUrl = "http://localhost:5080/api",
    [Parameter(Mandatory=$true)][string]$Token,
    [string]$EntityName = "Customer"
)

$headers = @{ Authorization = "Bearer $Token"; "Content-Type" = "application/json" }
$body = @{ entityName = $EntityName; sourceApplication = "ExternalAppSmokeTest"; pullAfterSeed = $true; repairFirst = $true } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/oracle-sync/external-app-test" -Headers $headers -Body $body
