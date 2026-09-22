$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
$stage = Join-Path $root 'packaging\msix-stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item (Join-Path $dist '*') $stage -Recurse -Force
Copy-Item (Join-Path $root 'packaging\Package.appxmanifest') (Join-Path $stage 'AppxManifest.xml') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'Assets') | Out-Null
$makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { Write-Error 'makeappx.exe not found'; exit 1 }
$out = Join-Path $root 'dist\WsaHub.msix'
& $makeappx.FullName pack /p $out /d $stage /o
Write-Host "MSIX: $out (unsigned)"
