$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
Write-Host '=== DRONE PLANNER: BUILD + ALL 36 TESTS ==='
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Host 'Install .NET 10 SDK first: https://dotnet.microsoft.com/download/dotnet/10.0' -ForegroundColor Red; exit 1 }
$dll = Join-Path $PSScriptRoot 'runtime/win-x64/drone_native.dll'
if (Get-Command gcc -ErrorAction SilentlyContinue) {
    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'native/build-windows.ps1')
    if ($LASTEXITCODE -ne 0) { Write-Host 'C library failed to build.' -ForegroundColor Red; exit 1 }
} elseif (Test-Path $dll) {
    Write-Host 'gcc not found: using the prebuilt runtime/win-x64/drone_native.dll.' -ForegroundColor Yellow
} else { Write-Host 'Install a 64-bit MinGW-w64 GCC and add gcc.exe to PATH first.' -ForegroundColor Red; exit 1 }
& dotnet restore DroneDeliveryFinal.slnx
if ($LASTEXITCODE -ne 0) { exit 1 }
& dotnet build DroneDeliveryFinal.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host 'C# build failed.' -ForegroundColor Red; exit 1 }
& dotnet run --project tests/DronePlanner.Tests -c Release --no-build
if ($LASTEXITCODE -ne 0) { Write-Host 'Some tests FAILED. Read FAIL lines above.' -ForegroundColor Red; exit 1 }
Write-Host 'Build complete. Check the 36/36 summary above.' -ForegroundColor Green
