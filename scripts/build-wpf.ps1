# Build WsaHub WPF host with Roslyn csc (no NuGet).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$csc = 'C:\Program Files\dotnet\sdk\8.0.421\Roslyn\bincore\csc.dll'
if (-not (Test-Path $csc)) {
  $csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll' | Sort-Object FullName -Descending | Select-Object -First 1).FullName
}
$wdc = (Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*' | Sort-Object Name -Descending | Select-Object -First 1).FullName
$nc = (Get-ChildItem 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.*' | Sort-Object Name -Descending | Select-Object -First 1).FullName
Write-Host "csc=$csc"
Write-Host "wdc=$wdc"

$rsp = Join-Path $dist 'WsaHub.rsp'
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('-nologo')
[void]$sb.AppendLine('-target:winexe')
[void]$sb.AppendLine('-platform:anycpu')
[void]$sb.AppendLine('-nullable:disable')
[void]$sb.AppendLine('-langversion:latest')
[void]$sb.AppendLine('-out:"' + (Join-Path $dist 'WsaHub.exe') + '"')

function Add-Ref([string]$p) {
  if ($p -and (Test-Path $p)) {
    [void]$script:sb.AppendLine('-r:"' + $p + '"')
  }
}

foreach ($name in @(
  'WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll',
  'System.Windows.Forms.dll','System.Drawing.Common.dll','System.Drawing.dll',
  'UIAutomationProvider.dll','UIAutomationTypes.dll','System.Windows.Extensions.dll'
)) {
  Add-Ref (Join-Path $wdc $name)
}

foreach ($dir in @($wdc, $nc)) {
  Get-ChildItem (Join-Path $dir '*.dll') | ForEach-Object {
    try {
      $null = [System.Reflection.AssemblyName]::GetAssemblyName($_.FullName)
      Add-Ref $_.FullName
    } catch { }
  }
}

[void]$sb.AppendLine('"' + (Join-Path $src 'WsaHub.Core\WsaHubCore.cs') + '"')
[void]$sb.AppendLine('"' + (Join-Path $src 'WsaHub.App\Program.cs') + '"')
Set-Content -Path $rsp -Value $sb.ToString() -Encoding UTF8

Write-Host 'Compiling...'
& 'C:\Program Files\dotnet\dotnet.exe' exec $csc "@$rsp"
if ($LASTEXITCODE -ne 0) { throw "csc failed $LASTEXITCODE" }

@'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "frameworks": [
      { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
      { "name": "Microsoft.WindowsDesktop.App", "version": "8.0.0" }
    ]
  }
}
'@ | Set-Content (Join-Path $dist 'WsaHub.runtimeconfig.json') -Encoding UTF8

Write-Host ('OK ' + (Join-Path $dist 'WsaHub.exe'))
Get-Item (Join-Path $dist 'WsaHub.exe') | Select-Object FullName, Length
