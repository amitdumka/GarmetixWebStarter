<#
.SYNOPSIS
  Calls the authenticated database repair endpoint after admin login.

.EXAMPLE
  .\scripts\repair-database.ps1 -Token "eyJ..."
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$Token,
  [string]$ApiBase = 'http://localhost:5080/api'
)

$headers = @{ Authorization = "Bearer $Token" }
Invoke-RestMethod -Method Post -Uri "$ApiBase/database/repair" -Headers $headers | ConvertTo-Json -Depth 10
