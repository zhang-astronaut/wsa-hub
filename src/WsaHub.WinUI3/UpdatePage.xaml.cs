using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class UpdatePage : Page
{
    List<ReleaseInfo> _releases = new();
    readonly ComboBox _releaseBox = new() { Width = 420, PlaceholderText = "选择 Release" };
    readonly ComboBox _filter = new() { Width = 420 };
    readonly ListView _assets = new() { Height = 280, SelectionMode = ListViewSelectionMode.Single };
    readonly TextBox _log = new() { Height = 140, IsReadOnly = true, AcceptsReturn = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
    readonly TextBlock _info = Ui.Lbl("选择版本与变体（GApps / Amazon / Root），可切换或全新安装。");

    public UpdatePage()
    {
        InitializeComponent();
        _filter.Items.Add("全部变体");
        _filter.Items.Add("GApps + 无Amazon");
        _filter.Items.Add("NoGApps + 无Amazon");
        _filter.Items.Add("含 Magisk Root");
        _filter.Items.Add("无 Root");
        _filter.SelectedIndex = 0;
        _filter.SelectionChanged += (_, __) => ApplyFilter();
        _releaseBox.SelectionChanged += (_, __) => ApplyFilter();

        Root.Content = Ui.Page(
            Ui.Card("选择版本 / 变体",
                _info,
                Ui.BusyBtn("加载 Release 列表", LoadAsync),
                Ui.Lbl("Release"),
                _releaseBox,
                Ui.Lbl("变体过滤"),
                _filter,
                _assets,
                Ui.BusyBtn("下载并切换到选中版本", SwitchAsync),
                Ui.BusyBtn("仅下载选中包", DownloadOnlyAsync),
                _log
            ),
            Ui.Lbl("切换流程：备份 userdata → 停止 WSA → 卸载（保留数据）→ 合并干净包 → 补 DLL → 注册 Appx。目录 _backup / _download / platform-tools 会保留。")
        );
    }

    void Append(string s) => _log.Text += s + "\n";

    ReleaseInfo? SelectedRelease =>
        _releaseBox.SelectedItem is string tag
            ? _releases.FirstOrDefault(r => r.Tag == tag)
            : null;

    ReleaseAssetInfo? SelectedAsset => _assets.SelectedItem as ReleaseAssetInfo;

    async Task LoadAsync()
    {
        _info.Text = "正在拉取 GitHub Releases…";
        try
        {
            _releases = await ReleaseCatalog.ListReleasesAsync(MainWindow.Config);
            _releaseBox.Items.Clear();
            foreach (var r in _releases)
                _releaseBox.Items.Add(r.Tag + (r.IsLts ? "  [LTS]" : ""));
            // select first LTS or first
            var idx = _releases.FindIndex(r => r.IsLts);
            _releaseBox.SelectedIndex = idx >= 0 ? idx : 0;
            // store tag only
            _releaseBox.Items.Clear();
            foreach (var r in _releases)
                _releaseBox.Items.Add(r.Tag);
            _releaseBox.SelectedIndex = idx >= 0 ? idx : 0;
            _info.Text = $"共 {_releases.Count} 个 Release。选择资产后可切换安装。";
            ApplyFilter();
        }
        catch (Exception ex) { _info.Text = ex.Message; }
    }

    void ApplyFilter()
    {
        var rel = SelectedRelease;
        _assets.Items.Clear();
        if (rel == null) return;
        var f = _filter.SelectedItem as string ?? "全部变体";
        IEnumerable<ReleaseAssetInfo> q = rel.Assets;
        q = f switch
        {
            "GApps + 无Amazon" => q.Where(a => a.GApps && !a.Amazon),
            "NoGApps + 无Amazon" => q.Where(a => !a.GApps && !a.Amazon),
            "含 Magisk Root" => q.Where(a => a.Magisk || a.KernelSu),
            "无 Root" => q.Where(a => !a.Magisk && !a.KernelSu),
            _ => q
        };
        foreach (var a in q.OrderByDescending(x => x.GApps).ThenByDescending(x => !x.Amazon))
            _assets.Items.Add(a);
    }

    async Task DownloadOnlyAsync()
    {
        var a = SelectedAsset;
        if (a == null) { Append("请先在列表中选择一个包"); return; }
        await InstallCoreAsync(a, apply: false);
    }

    async Task SwitchAsync()
    {
        var a = SelectedAsset;
        if (a == null) { Append("请先在列表中选择一个包"); return; }
        var ok = await Ui.ConfirmAsync("切换 / 安装版本",
            $"将使用：\n{a.Name}\n{a.Summary}\nWSA {a.WsaVersion}\n\n流程：备份 userdata → 卸载保留数据 → 干净合并 → 注册。\n继续？");
        if (!ok) { Append("已取消"); return; }
        await InstallCoreAsync(a, apply: true);
    }

    async Task InstallCoreAsync(ReleaseAssetInfo asset, bool apply)
    {
        var cfg = MainWindow.Config;
        try
        {
            var r = new UpdateCheckResult
            {
                Ok = true,
                AssetName = asset.Name,
                AssetUrl = asset.Url,
                AssetSize = asset.Size,
                AssetVersion = asset.WsaVersion,
                ReleaseTag = SelectedRelease?.Tag ?? ""
            };
            Append("下载 " + asset.Name + " …");
            var path = await GithubWsabuilds.DownloadAsync(cfg, r, new Progress<double>(p => { }));
            Append("已下载: " + path);
            if (!apply) { Append("仅下载完成"); return; }

            Append("1/5 备份 userdata…");
            var bak = WsaInstaller.BackupUserdata(cfg);
            if (!string.IsNullOrEmpty(bak)) Append("备份: " + bak);

            Append("2/5 停止 WSA…");
            WsaInstaller.StopWsa();

            Append("3/5 注销旧包（保留应用数据）…");
            // RegisterElevated also unregisters; do file merge first then register script handles unregister+register
            Append("4/5 干净合并到 " + cfg.WsaInstallDir + " …");
            WsaInstaller.ExtractAndMerge(path, cfg, Append);

            Append("5/5 注册 Appx（UAC）…");
            var code = WsaInstaller.RegisterElevated(cfg, Append);
            Append("Register exit=" + code);
            await Ui.ShowAsync("完成", "已切换到 " + asset.WsaVersion + "（" + asset.Summary + "）\n请打开开始菜单验证 WSA。");
        }
        catch (Exception ex) { Append("错误: " + ex.Message); }
    }
}
