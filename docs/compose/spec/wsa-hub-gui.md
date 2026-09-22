---
feature: wsa-hub-gui
status: designed
updated: 2026-09-22
branch: main
commits: 
---

# WsaHub — WSA 管理 GUI（WinUI 3）

## Report

## [S1] Problem

需要一款 **现代化 GUI** 管理 Windows 上的 WSA（WSABuilds）：

1. 启动时检测是否已装 WSA；**有则检查更新，无则下载并安装最新版**；
2. **ADB 侧载 APK** 及常用调试能力；
3. 其它管理功能（应用列表、备份、计划任务、设置等）尽可能丰富；
4. **WinUI 3** 风格界面；分发形态：**无 MSIX 自包含 exe + MSIX 包**。

约束：可完全重写，不强制复用旧 CLI；仓库独立（本 `wsa-hub` 仓库）。

### 本机工具链备注

- 系统 `dotnet restore` / `dotnet nuget locals` 因 `NuGet.Common.NuGetEnvironment` 对 `MachineWideSettingsBaseDirectory` 抛 `Path.Combine(null)` **全部失败**（环境探测：SpecialFolder 均正常，属 NuGet 实现缺陷）。
- 因此交付双轨：
  - **WsaHub.App**：WPF Fluent 深色壳，用 Roslyn `csc` + WindowsDesktop 共享框架 **无 NuGet 编译**，立即可用；
  - **WsaHub.WinUI3**：完整 WinUI 3 / Windows App SDK 工程源码 + MSIX 清单，NuGet 可用后 `dotnet build` 即得 WinUI3 版；
  - 视觉与业务层（WsaHub.Core）共用。

## [S2] Design

### 2.1 架构

```
WsaHub.Core     服务层：WSA 检测、GitHub WSABuilds、下载/安装、ADB、配置、任务
WsaHub.App      WPF 演示/生产壳（无 NuGet 构建）
WsaHub.WinUI3   WinUI 3 壳（XAML NavigationView）+ MSIX
packaging/      Package.appxmanifest、构建脚本
scripts/        build-wpf.ps1 / build-winui3.ps1 / build-msix.ps1 / fix-nuget-notes.md
```

### 2.2 启动流程

1. 读配置（`%APPDATA%\WsaHub\config.json`）；
2. `WsaProbe.Detect()` → 未安装 → UI 进入 **安装向导**（下载 GApps-NoAmazon LTS 并可确认安装）；已安装 → **仪表盘 + 检查更新**（后台）；
3. 发现更新 → 信息条 / 对话框 → **下载** → **确认后安装**（UAC）。

### 2.3 功能清单（MVP+）

| 模块 | 能力 |
|------|------|
| 首页 | 版本、安装路径、GApps/Magisk 探测、VM/进程状态、磁盘 |
| 更新 | 检查 WSABuilds LTS、资产匹配 GApps-NoAmazon x64、下载、确认安装 |
| 全新安装 | 无 WSA 时一键下载+确认安装 |
| APK/ADB | 选 APK 安装、连接 58526、设备列表、启动包名、卸载、shell 命令、截图、logcat |
| 应用管理 | `pm list packages`、搜索、启动、卸载 |
| 备份 | 备份/打开 userdata.vhdx 目录 |
| 任务 | 注册/移除登录检查任务 |
| 设置 | 安装/下载目录、资产正则、GitHub Token、开机检查、主题说明 |
| 日志 | 显示应用/安装日志尾部 |
| 外链 | WSABuilds Releases、本机 WSA 设置 |

### 2.4 核心契约

- 更新源 `MustardChef/WSABuilds`；优先 LTS；资产默认 `(?i)WSA_.*_x64_.*GApps.*NoAmazon.*\.7z$`（排除 canary）。
- 安装：备份 userdata → 停 WSA → 解压合并 → 补 `*_APP.dll` → `Add-AppxPackage -Register`（UAC）。
- **永不静默安装**；必须 UI 确认。
- Adb 路径可配置，探测顺序：`wsa_install_dir\platform-tools\adb.exe` → PATH → `%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe`。

### 2.5 分发

- **Unpackaged**：`WsaHub.exe`（csc 输出，可自包含拷贝）。
- **MSIX**：`packaging/Package.appxmanifest` + `scripts/build-msix.ps1`（makeappx/makensuv）；WinUI3 工程含 `WindowsPackageType` 可切换。

## [S3] Out of Scope

- 不做应用商店/爬虫 APK 源
- 不做 Root/模块市场
- 不保证在 NuGet 损坏环境编出 WinUI3（附源与修复说明）
- 不做 macOS/Linux

## Tasks

- [ ] T1: 仓库结构 + Core 服务层 — acceptance: 源文件齐全，接口覆盖 S2.4（covers: S2.1; S2.4）
- [ ] T2: WPF UI 全模块 — acceptance: 首页/更新/安装/APK/应用/设置/日志可见且绑定服务（covers: S2.2; S2.3）
- [ ] T3: 无 NuGet 构建脚本 — acceptance: `build-wpf.ps1` 产出 WsaHub.exe（covers: S2.5）
- [ ] T4: WinUI3 工程 + MSIX 打包资料 — acceptance: csproj/xaml/manifest/脚本存在（covers: S2.1; S2.5）
- [ ] T5: 运行验证 — acceptance: exe 启动显示 UI；检测已装 WSA 显示 up_to_date 或更新（covers: S2.2）
- [ ] T6: 独立复核 — acceptance: 无 critical（covers: S2）
- [ ] T7: GitHub 公开推送 — acceptance: zhang-astronaut/wsa-hub 可访问（covers: S2.5）
