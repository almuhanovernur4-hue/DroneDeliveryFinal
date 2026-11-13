$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Host 'Install .NET 10 SDK first.'; exit 1 }
& dotnet run --project src/DronePlanner.App -c Release
exit $LASTEXITCODE
