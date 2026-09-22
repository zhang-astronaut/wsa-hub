$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet build (Join-Path $root 'src\WsaHub.WinUI3\WsaHub.WinUI3.csproj') -c Release
