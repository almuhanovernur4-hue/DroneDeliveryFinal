# Requires 64-bit Windows and GCC (MinGW-w64) available on PATH.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Join-Path $PSScriptRoot 'drone_native.c'
$outputDir = Join-Path $root 'runtime/win-x64'
New-Item -ItemType Directory -Force $outputDir | Out-Null
$output = Join-Path $outputDir 'drone_native.dll'
& gcc -Wall -Wextra -Werror -shared -O2 -o $output $native
if ($LASTEXITCODE -ne 0) { throw 'Native GCC compiler failed' }
Write-Host "Built $output"
