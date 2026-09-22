using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class UpdatePage : Page
{
    UpdateCheckResult _last = new();
    readonly TextBox _log = new() { Height = 180, IsReadOnly = true, AcceptsReturn = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    readonly TextBlock _info = Ui.Lbl("检测 WSABuilds LTS，匹配 GApps-NoAmazon x64。安装需确认。");
    readonly ProgressBar _bar = new() { Height = 8, Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };

    public UpdatePage()
    {
        InitializeComponent();
        Root.Content = Ui.Page(
            Ui.Card("检查更新",
                _info,
                Ui.Btn("立即检查", async () => await CheckAsync()),
                Ui.Btn("下载更新", async () => await DownloadAsync()),
                Ui.Btn("下载并确认安装", async () => await DownloadInstallAsync()),
                _bar,
                _log
            )
        );
    }

    void Append(string s) => _log.Text += s + "\n";

    async Task CheckAsync()
    {
        try
        {
            _last = await GithubWsabuilds.CheckAsync(MainWindow.Config);
            if (!_last.Ok) { _info.Text = "失败: " + _last.Error; return; }
            _info.Text = $"本地: {_last.InstalledVersion}\n远端: {_last.ReleaseTag}\n包: {_last.AssetName} ({_last.AssetVersion})\n状态: {_last.Reason} / 更新={_last.HasUpdate}";
            var cfg = MainWindow.Config;
            cfg.LastReleaseTag = _last.ReleaseTag;
            cfg.LastAssetName = _last.AssetName;
            cfg.LastCheckUtc = DateTime.UtcNow.ToString("o");
            cfg.Save();
        }
        catch (Exception ex) { _info.Text = ex.Message; }
    }

    async Task DownloadAsync()
    {
        if (string.IsNullOrEmpty(_last.AssetUrl)) await CheckAsync();
        if (string.IsNullOrEmpty(_last.AssetUrl)) { Append("无下载地址"); return; }
        if (!_last.HasUpdate && !await Ui.ConfirmAsync("WsaHub", "已是最新。仍要下载该包？")) return;
        await DownloadCoreAsync();
    }

    async Task DownloadInstallAsync()
    {
        await CheckAsync();
        if (string.IsNullOrEmpty(_last.AssetUrl)) { Append("无下载地址"); return; }
        var path = await DownloadCoreAsync();
        if (path == null) return;
        var ok = await Ui.ConfirmAsync("确认安装",
            $"安装 {_last.AssetVersion} 到 {MainWindow.Config.WsaInstallDir}？\n备份 userdata → 停止 WSA → 合并 → 注册 Appx（UAC）");
        if (!ok) { Append("用户取消安装"); return; }
        try
        {
            Append("备份 userdata...");
            WsaInstaller.BackupUserdata(MainWindow.Config);
            Append("停止 WSA...");
            WsaInstaller.StopWsa();
            Append("解压合并...");
            WsaInstaller.ExtractAndMerge(path, MainWindow.Config, Append);
            Append("注册 Appx（UAC）...");
            Append("Register exit=" + WsaInstaller.RegisterElevated(MainWindow.Config, Append));
            await Ui.ShowAsync("WsaHub", "安装流程结束，请检查开始菜单。");
        }
        catch (Exception ex) { Append("错误: " + ex.Message); }
    }

    async Task<string?> DownloadCoreAsync()
    {
        try
        {
            _bar.Visibility = Visibility.Visible;
            var path = await GithubWsabuilds.DownloadAsync(MainWindow.Config, _last, new Progress<double>(p => _bar.Value = p * 100));
            Append("已下载: " + path);
            return path;
        }
        catch (Exception ex) { Append(ex.Message); return null; }
        finally { _bar.Visibility = Visibility.Collapsed; }
    }
}
