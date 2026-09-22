# Build WinForms host with Roslyn csc against .NET Framework 4.8 (runs via mscoree, no NuGet).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src\WsaHub.WinForms48\App.cs'
$outDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'WsaHub.exe'
$csc = 'C:\Program Files\dotnet\sdk\8.0.421\Roslyn\bincore\csc.dll'
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs = @(
  (Join-Path $fx 'mscorlib.dll'),
  (Join-Path $fx 'System.dll'),
  (Join-Path $fx 'System.Core.dll'),
  (Join-Path $fx 'System.Drawing.dll'),
  (Join-Path $fx 'System.Windows.Forms.dll'),
  (Join-Path $fx 'System.Net.dll'),
  (Join-Path $fx 'System.Xml.dll')
)
$rsp = Join-Path $outDir 'WsaHub-netfx.rsp'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('-nologo')
$lines.Add('-target:winexe')
$lines.Add('-platform:anycpu')
$lines.Add('-langversion:latest')
$lines.Add('-out:"' + $out + '"')
foreach ($r in $refs) { $lines.Add('-r:"' + $r + '"') }
$lines.Add('"' + $src + '"')
Set-Content $rsp $lines -Encoding UTF8
& 'C:\Program Files\dotnet\dotnet.exe' exec $csc ('@' + $rsp)
if ($LASTEXITCODE -ne 0) { throw "csc failed $LASTEXITCODE" }
Get-Item $out | Format-List FullName,Length
