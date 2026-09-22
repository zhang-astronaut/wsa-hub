# WsaHub
# .NET restore 本机故障：dotnet restore / nuget locals 报 path1 空引用（NuGet.Common GetFolderPath MachineWide*）。见 scripts/fix-nuget-notes.md
# 生产构建：scripts/build-wpf.ps1（csc + WindowsDesktop，零 NuGet）

## Features
- Detect WSA (install wizard if missing)
- Check WSABuilds LTS updates (GApps-NoAmazon x64)
- Download + confirm install (UAC)
- ADB APK install / packages / shell / screenshot / logcat
- Backup, logon task, settings, logs
- WinUI3 sources + MSIX packaging scripts

## Build (WPF host, works today)
```powershell
cd scripts
.\build-wpf.ps1
..\dist\WsaHub.exe
```

## Build (WinUI3 when NuGet works)
```powershell
cd scripts
.\build-winui3.ps1
```

## MSIX
```powershell
.\scripts\build-wpf.ps1
.\scripts\build-msix.ps1
```
