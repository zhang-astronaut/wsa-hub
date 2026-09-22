using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WsaHub.Core;

namespace WsaHub.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Startup += (_, __) =>
        {
            var win = new MainWindow();
            app.MainWindow = win;
            win.Show();
        };
        app.Run();
    }
}

public sealed class MainWindow : Window
{
    readonly HubConfig _cfg = HubConfig.Load();
    readonly TextBlock _status = new();
    readonly ContentControl _page = new();
    readonly ListBox _nav = new();
    WsaStatus _wsa = new();

    public MainWindow()
    {
        Title = "WsaHub — WSA 管理中心";
        Width = 1180; Height = 760; MinWidth = 960; MinHeight = 640;
        Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x24));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildChrome();
        Loaded += async (_, __) => await BootstrapAsync();
    }

    UIElement BuildChrome()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var side = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1C)),
            Padding = new Thickness(12)
        };
        var sideStack = new StackPanel();
        sideStack.Children.Add(TitleBlock("WsaHub", 22));
        sideStack.Children.Add(TitleBlock("WSA · WSABuilds · ADB", 11, opacity: 0.55));
        sideStack.Children.Add(Space(18));
        _nav.Background = Brushes.Transparent;
        _nav.BorderThickness = new Thickness(0);
        _nav.Foreground = Brushes.White;
        _nav.FontSize = 14;
        foreach (var item in new[] { "首页", "更新与安装", "APK / ADB", "应用管理", "备份", "任务", "设置", "日志" })
            _nav.Items.Add(item);
        _nav.SelectedIndex = 0;
        _nav.SelectionChanged += (_, __) => Navigate(_nav.SelectedItem?.ToString() ?? "首页");
        sideStack.Children.Add(_nav);
        side.Child = sideStack;
        Grid.SetColumn(side, 0);
        root.Children.Add(side);

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x2A)),
            Padding = new Thickness(20, 14, 20, 14)
        };
        var hstack = new DockPanel { LastChildFill = true };
        _status.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xD0));
        _status.Text = "正在检测 WSA…";
        DockPanel.SetDock(_status, Dock.Right);
        hstack.Children.Add(_status);
        hstack.Children.Add(TitleBlock("WSA Control Center", 18));
        header.Child = hstack;
        Grid.SetRow(header, 0);
        right.Children.Add(header);

        _page.Margin = new Thickness(16);
        Grid.SetRow(_page, 1);
        right.Children.Add(_page);

        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x1C)),
            Padding = new Thickness(16, 8, 16, 8)
        };
        footer.Child = new TextBlock
        {
            Text = "确认后才会安装 · 默认 GApps-NoAmazon · github.com/zhang-astronaut/wsa-hub",
            Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x88)),
            FontSize = 11
        };
        Grid.SetRow(footer, 2);
        right.Children.Add(footer);

        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        return root;
    }

    static TextBlock TitleBlock(string text, double size, double opacity = 1)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb((byte)(255 * opacity), 0xF2, 0xF2, 0xF7)),
            Margin = new Thickness(2, 4, 2, 4)
        };
    }

    static UIElement Space(double h) => new Border { Height = h };

    async Task BootstrapAsync()
    {
        try { _wsa = await Task.Run(() => WsaProbe.Detect(_cfg)); }
        catch (Exception ex) { _wsa = new WsaStatus { Detail = ex.Message }; }
        if (!_wsa.Installed)
        {
            _status.Text = "未检测到 WSA";
            Navigate("更新与安装");
            return;
        }
        _status.Text = $"WSA {_wsa.Version}" + (_wsa.Running ? " · 运行中" : " · 未运行");
        Navigate("首页");
        _ = CheckUpdateSilentAsync();
    }

    async Task CheckUpdateSilentAsync()
    {
        try
        {
            var r = await GithubWsabuilds.CheckAsync(_cfg);
            if (r.Ok && r.HasUpdate)
            {
                _status.Text = $"发现更新 {r.ReleaseTag}";
                Dispatcher.Invoke(() =>
                    MessageBox.Show(this, $"发现新构建：{r.ReleaseTag}\n{r.AssetName}\n\n请到「更新与安装」下载。", "WsaHub", MessageBoxButton.OK, MessageBoxImage.Information));
            }
        }
        catch { }
    }

    async Task ReDetectAsync()
    {
        _wsa = await Task.Run(() => WsaProbe.Detect(_cfg));
        _status.Text = _wsa.Installed ? $"WSA {_wsa.Version}" : "未检测到 WSA";
        Navigate("首页");
    }

    void Navigate(string page)
    {
        _page.Content = page switch
        {
            "更新与安装" => BuildUpdatePage(),
            "APK / ADB" => BuildAdbPage(),
            "应用管理" => BuildAppsPage(),
            "备份" => BuildBackupPage(),
            "任务" => BuildTaskPage(),
            "设置" => BuildSettingsPage(),
            "日志" => BuildLogPage(),
            _ => BuildHome()
        };
    }

    UIElement Card(string title, params UIElement[] children)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        sp.Children.Add(TitleBlock(title, 15));
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x30)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Child = new StackPanel()
        };
        foreach (var c in children) ((StackPanel)border.Child).Children.Add(c);
        sp.Children.Add(border);
        return sp;
    }

    static Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(0, 6, 8, 0),
            Padding = new Thickness(14, 8, 14, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xED)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        b.Click += (_, __) => onClick();
        return b;
    }

    static TextBlock Lbl(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD8)),
        Margin = new Thickness(0, 2, 0, 2),
        TextWrapping = TextWrapping.Wrap
    };

    UIElement BuildHome()
    {
        var sp = new StackPanel();
        sp.Children.Add(Card("WSA 状态",
            Lbl($"已安装：{(_wsa.Installed ? "是" : "否")}"),
            Lbl($"版本：{_wsa.Version}"),
            Lbl($"检测来源：{_wsa.Source}"),
            Lbl($"安装目录：{_wsa.InstallDir}"),
            Lbl($"运行中：{(_wsa.Running ? "是" : "否")}"),
            Lbl($"Google 服务：{(_wsa.GApps ? "有" : "无/未知")}"),
            Lbl($"Magisk：{(_wsa.Magisk ? "有" : "无/未知")}"),
            Btn("重新检测", () => _ = ReDetectAsync()),
            Btn("启动 WSA 设置", () => RunUri("shell:AppsFolder\\MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe!SettingsApp"))
        ));
        if (!_wsa.Installed)
            sp.Children.Add(Lbl("提示：未检测到 WSA，请到「更新与安装」执行全新安装。"));
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement BuildUpdatePage()
    {
        var log = new TextBox
        {
            Height = 160,
            Margin = new Thickness(0, 8, 0, 8),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xFF, 0xB0)),
            FontFamily = new FontFamily("Consolas"),
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var info = new TextBlock
        {
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            Text = "检测 WSABuilds LTS，匹配 GApps-NoAmazon x64 包。安装前会再次确认。"
        };
        var bar = new ProgressBar { Height = 8, Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        void Append(string s) => Dispatcher.Invoke(() => log.Text += s + Environment.NewLine);

        var sp = new StackPanel();
        if (!_wsa.Installed)
        {
            sp.Children.Add(Card("全新安装",
                Lbl("未检测到 WSA。将下载最新 LTS（GApps-NoAmazon）并在确认后安装。"),
                Btn("检查并下载最新版", async () =>
                {
                    try
                    {
                        var r = await GithubWsabuilds.CheckAsync(_cfg);
                        if (!r.Ok) { Append("检查失败: " + r.Error); return; }
                        info.Text = $"{r.ReleaseTag}\n{r.AssetName}";
                        bar.Visibility = Visibility.Visible;
                        var path = await GithubWsabuilds.DownloadAsync(_cfg, r, new Progress<double>(p => bar.Value = p * 100));
                        Append("已下载: " + path);
                        if (MessageBox.Show(this, "下载完成。现在安装？", "WsaHub", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            WsaInstaller.StopWsa();
                            WsaInstaller.BackupUserdata(_cfg);
                            WsaInstaller.ExtractAndMerge(path, _cfg, Append);
                            Append("Register exit=" + WsaInstaller.RegisterElevated(_cfg, Append));
                            await ReDetectAsync();
                        }
                    }
                    catch (Exception ex) { Append("错误: " + ex.Message); }
                })
            ));
        }

        sp.Children.Add(Card("检查更新",
            info,
            Btn("立即检查", async () =>
            {
                try
                {
                    var r = await GithubWsabuilds.CheckAsync(_cfg);
                    if (!r.Ok) { info.Text = "失败: " + r.Error; return; }
                    info.Text = $"本地: {r.InstalledVersion}\n远端: {r.ReleaseTag}\n包: {r.AssetName} ({r.AssetVersion})\n状态: {r.Reason} / 更新={r.HasUpdate}";
                    _cfg.LastReleaseTag = r.ReleaseTag;
                    _cfg.LastAssetName = r.AssetName;
                    _cfg.LastCheckUtc = DateTime.UtcNow.ToString("o");
                    _cfg.Save();
                }
                catch (Exception ex) { info.Text = ex.Message; }
            }),
            Btn("下载更新", async () =>
            {
                try
                {
                    var r = await GithubWsabuilds.CheckAsync(_cfg);
                    if (!r.Ok) { Append(r.Error); return; }
                    if (!r.HasUpdate && MessageBox.Show(this, "已是最新。仍要下载该包？", "WsaHub", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                    bar.Visibility = Visibility.Visible;
                    var path = await GithubWsabuilds.DownloadAsync(_cfg, r, new Progress<double>(p => bar.Value = p * 100));
                    Append("已下载: " + path);
                }
                catch (Exception ex) { Append(ex.Message); }
            }),
            Btn("下载并确认安装", async () =>
            {
                try
                {
                    var r = await GithubWsabuilds.CheckAsync(_cfg);
                    if (!r.Ok) { Append(r.Error); return; }
                    bar.Visibility = Visibility.Visible;
                    var path = await GithubWsabuilds.DownloadAsync(_cfg, r, new Progress<double>(p => bar.Value = p * 100));
                    Append("已下载: " + path);
                    if (MessageBox.Show(this, $"安装 {r.AssetVersion} 到 {_cfg.WsaInstallDir}？", "WsaHub",
                            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                    WsaInstaller.StopWsa();
                    WsaInstaller.BackupUserdata(_cfg);
                    WsaInstaller.ExtractAndMerge(path, _cfg, Append);
                    WsaInstaller.RegisterElevated(_cfg, Append);
                    await ReDetectAsync();
                }
                catch (Exception ex) { Append(ex.Message); }
            }),
            bar, log
        ));
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement BuildAdbPage()
    {
        var output = new TextBox
        {
            Height = 220,
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xC0, 0xE0, 0xC0)),
            FontFamily = new FontFamily("Consolas"),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        var apkPath = new TextBox { Margin = new Thickness(0, 4, 0, 4), Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var pkgBox = new TextBox { Margin = new Thickness(0, 4, 0, 4), Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        var shellBox = new TextBox { Margin = new Thickness(0, 4, 0, 4), Width = 480, HorizontalAlignment = HorizontalAlignment.Left };

        var sp = new StackPanel();
        sp.Children.Add(Card("ADB 连接",
            Lbl("adb: " + (AdbTool.ResolveAdb(_cfg) ?? "未找到")),
            Btn("连接 127.0.0.1:58526", () =>
            {
                try { AdbTool.EnsureConnected(_cfg); output.Text += AdbTool.Run(AdbTool.ResolveAdb(_cfg)!, "devices") + "\n"; }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            }),
            Btn("设备列表", () =>
            {
                var adb = AdbTool.ResolveAdb(_cfg);
                output.Text = adb == null ? "adb missing" : AdbTool.Run(adb, "devices -l");
            })
        ));
        sp.Children.Add(Card("安装 APK",
            Lbl("选择 APK 后安装到 WSA"),
            apkPath,
            Btn("选择 APK…", () =>
            {
                var ofd = new OpenFileDialog { Filter = "Android APK|*.apk;*.apks;*.xapk|All|*.*" };
                if (ofd.ShowDialog() == true) apkPath.Text = ofd.FileName;
            }),
            Btn("安装", async () =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(apkPath.Text) || !File.Exists(apkPath.Text)) { output.Text += "请选择 APK\n"; return; }
                    output.Text += await Task.Run(() => AdbTool.InstallApk(_cfg, apkPath.Text)) + "\n";
                }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            })
        ));
        sp.Children.Add(Card("包操作",
            Lbl("包名示例：com.android.vending"),
            pkgBox,
            Btn("启动", () =>
            {
                try { output.Text += WsaProbe.AdbShell(_cfg, $"shell monkey -p {pkgBox.Text.Trim()} -c android.intent.category.LAUNCHER 1") + "\n"; }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            }),
            Btn("卸载", () =>
            {
                if (MessageBox.Show(this, "卸载 " + pkgBox.Text + "？", "WsaHub", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                try { output.Text += WsaProbe.AdbShell(_cfg, "shell pm uninstall " + pkgBox.Text.Trim()) + "\n"; }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            }),
            Btn("截图到下载目录", () =>
            {
                try
                {
                    var adb = AdbTool.ResolveAdb(_cfg)!;
                    AdbTool.Run(adb, "-s 127.0.0.1:58526 shell screencap -p /sdcard/wsahub.png");
                    Directory.CreateDirectory(_cfg.DownloadDir);
                    var dest = Path.Combine(_cfg.DownloadDir, "wsahub_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    AdbTool.Run(adb, "-s 127.0.0.1:58526 pull /sdcard/wsahub.png \"" + dest + "\"");
                    output.Text += "Saved " + dest + "\n";
                }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            })
        ));
        sp.Children.Add(Card("Shell",
            shellBox,
            Btn("执行", () =>
            {
                try { output.Text += WsaProbe.AdbShell(_cfg, "shell " + shellBox.Text.Trim()) + "\n"; }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            }),
            Btn("logcat -d 尾部", () =>
            {
                try { output.Text += WsaProbe.AdbShell(_cfg, "logcat -d -t 80") + "\n"; }
                catch (Exception ex) { output.Text += ex.Message + "\n"; }
            }),
            output
        ));
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement BuildAppsPage()
    {
        var list = new ListView { Height = 360, Margin = new Thickness(0, 8, 0, 0) };
        var filter = new TextBox { Width = 320, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
        void Reload()
        {
            try
            {
                AdbTool.EnsureConnected(_cfg);
                var text = WsaProbe.AdbShell(_cfg, "shell pm list packages -3");
                var pkgs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Replace("package:", "").Trim())
                    .Where(p => p.Length > 0)
                    .Where(p => string.IsNullOrWhiteSpace(filter.Text) || p.Contains(filter.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                list.ItemsSource = pkgs;
            }
            catch (Exception ex) { list.ItemsSource = new[] { ex.Message }; }
        }
        var sp = new StackPanel();
        sp.Children.Add(Card("第三方应用",
            Btn("刷新", Reload), filter, list,
            Btn("启动选中", () =>
            {
                if (list.SelectedItem is string pkg)
                    WsaProbe.AdbShell(_cfg, $"shell monkey -p {pkg} -c android.intent.category.LAUNCHER 1");
            }),
            Btn("卸载选中", () =>
            {
                if (list.SelectedItem is not string pkg) return;
                if (MessageBox.Show(this, "卸载 " + pkg + "？", "WsaHub", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                WsaProbe.AdbShell(_cfg, "shell pm uninstall " + pkg);
                Reload();
            })
        ));
        Reload();
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement BuildBackupPage()
    {
        var sp = new StackPanel();
        sp.Children.Add(Card("userdata 备份",
            Btn("立即备份", () =>
            {
                var f = WsaInstaller.BackupUserdata(_cfg);
                MessageBox.Show(this, string.IsNullOrEmpty(f) ? "未找到 userdata" : "已备份: " + f, "WsaHub");
            }),
            Btn("打开备份文件夹", () =>
            {
                var d = Path.Combine(_cfg.WsaInstallDir, "_backup");
                Directory.CreateDirectory(d);
                RunUri(d);
            }),
            Btn("打开安装目录", () => RunUri(_cfg.WsaInstallDir))
        ));
        return sp;
    }

    UIElement BuildTaskPage()
    {
        var sp = new StackPanel();
        var state = Lbl("状态：未知");
        void Refresh() { state.Text = "登录检查任务：" + (LogonTask.IsRegistered() ? "已注册" : "未注册"); }
        sp.Children.Add(Card("开机 / 登录检查",
            state, Btn("刷新", Refresh),
            Btn("注册任务", () =>
            {
                var script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WsaHub", "Check-WsaUpdate.ps1");
                Directory.CreateDirectory(Path.GetDirectoryName(script)!);
                if (!File.Exists(script))
                    File.WriteAllText(script, "# WsaHub logon check\nWrite-Host 'Use WsaHub GUI to check updates'");
                LogonTask.Install(script);
                Refresh();
            }),
            Btn("移除任务", () => { LogonTask.Uninstall(); Refresh(); })
        ));
        Refresh();
        return sp;
    }

    UIElement BuildSettingsPage()
    {
        var install = new TextBox { Text = _cfg.WsaInstallDir, Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var download = new TextBox { Text = _cfg.DownloadDir, Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var adb = new TextBox { Text = _cfg.AdbPath, Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var pattern = new TextBox { Text = _cfg.AssetPattern, Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var token = new PasswordBox { Width = 480, HorizontalAlignment = HorizontalAlignment.Left };
        var sp = new StackPanel();
        sp.Children.Add(Card("路径",
            Lbl("WSA 安装目录"), install,
            Lbl("下载目录"), download,
            Lbl("adb.exe（可选）"), adb
        ));
        sp.Children.Add(Card("更新策略",
            Lbl("资产正则（默认 GApps-NoAmazon x64）"), pattern,
            Lbl("GitHub Token（可选）"), token
        ));
        sp.Children.Add(Card("保存",
            Btn("保存设置", () =>
            {
                _cfg.WsaInstallDir = install.Text.Trim();
                _cfg.DownloadDir = download.Text.Trim();
                _cfg.AdbPath = adb.Text.Trim();
                _cfg.AssetPattern = pattern.Text.Trim();
                if (!string.IsNullOrWhiteSpace(token.Password)) _cfg.GithubToken = token.Password.Trim();
                _cfg.Save();
                MessageBox.Show(this, "已保存", "WsaHub");
            }),
            Btn("打开 GitHub Releases", () => RunUri("https://github.com/MustardChef/WSABuilds/releases"))
        ));
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    UIElement BuildLogPage()
    {
        var box = new TextBox
        {
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1E)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
            Height = 480
        };
        void LoadLog(string path)
        {
            try { box.Text = File.Exists(path) ? File.ReadAllText(path) : "(empty) " + path; }
            catch (Exception ex) { box.Text = ex.Message; }
        }
        var sp = new StackPanel();
        var logPath = Path.Combine(_cfg.WsaInstallDir, "_install_apply_log.txt");
        sp.Children.Add(Card("日志",
            Btn("安装日志", () => LoadLog(logPath)),
            Btn("config.json", () => LoadLog(HubConfig.DefaultPath())),
            box
        ));
        LoadLog(logPath);
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    static void RunUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true }); }
        catch { }
    }
}
