using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class UpdatePage : Page
{
    List<ReleaseInfo> _releases = new();
    readonly ComboBox _releaseBox = new() { HorizontalAlignment = HorizontalAlignment.Stretch, PlaceholderText = "选择 Release" };
    readonly ComboBox _filter = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ListView _assets = new() { SelectionMode = ListViewSelectionMode.Single, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 190, 190, 200)) };
    readonly TextBox _log = new() { Height = 110, IsReadOnly = true, AcceptsReturn = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly TextBlock _info = new() { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 208)) };
    readonly ProgressBar _bar = new()
    {
        Minimum = 0,
        Maximum = 100,
        Height = 10,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };

    public UpdatePage()
    {
        InitializeComponent();
        Machine.Probe();
        _filter.Items.Add("全部变体");
        _filter.Items.Add("GApps + 无Amazon");
        _filter.Items.Add("NoGApps + 无Amazon");
        _filter.Items.Add("含 Magisk/KernelSU");
        _filter.Items.Add("无 Root");
        _filter.SelectedIndex = 0;
        _filter.SelectionChanged += (_, __) => ApplyFilter();
        _releaseBox.SelectionChanged += (_, __) => ApplyFilter();
        _assets.SelectionChanged += (_, __) => ShowDetail();

        // Responsive body: fills window height; list takes star space
        var root = new Grid { Padding = new Thickness(16, 12, 16, 12), RowSpacing = 10 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // header + machine recommendation + legend
        var header = new StackPanel { Spacing = 6 };
        header.Children.Add(_info);
        header.Children.Add(MachineCard());
        header.Children.Add(LegendCard());
        header.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 } .Also(p =>
        {
            p.Children.Add(Ui.BusyBtn("加载 Release / 本机推荐", LoadAsync));
            var fr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            fr.Children.Add(Ui.Lbl("过滤"));
            _filter.Width = 200;
            _filter.HorizontalAlignment = HorizontalAlignment.Left;
            fr.Children.Add(_filter);
            header.Children.Add(fr);
        }));
        header.Children.Add(_releaseBox);
        header.SetValue(Grid.RowProperty, 0);
        root.Children.Add(header);

        _assets.SetValue(Grid.RowProperty, 1);
        root.Children.Add(_assets);

        var actions = new StackPanel { Spacing = 8, Padding = new Thickness(0, 8, 0, 0) };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnRow.Children.Add(Ui.BusyBtn("下载并切换到选中版本", SwitchAsync));
        btnRow.Children.Add(Ui.BusyBtn("仅下载选中包", DownloadOnlyAsync));
        actions.Children.Add(_bar);
        actions.Children.Add(btnRow);
        actions.SetValue(Grid.RowProperty, 2);
        root.Children.Add(actions);

        var bottom = new StackPanel { Spacing = 6 };
        bottom.Children.Add(_detail);
        bottom.Children.Add(_log);
        bottom.SetValue(Grid.RowProperty, 3);
        root.Children.Add(bottom);

        Root.Content = root;
        _ = LoadAsync();
    }

    UIElement MachineCard()
    {
        var arch = Machine.IsArm64Host ? "ARM64" : "x64";
        return Ui.Card("本机检测与推荐",
            Ui.Lbl("CPU：" + (string.IsNullOrWhiteSpace(Machine.Cpu) ? "未知" : Machine.Cpu)),
            Ui.Lbl("架构：" + arch + " · 已装 WSA：" + (MainWindow.Status.Installed ? MainWindow.Status.Version : "无")),
            Ui.Dim("默认优先推荐：x64 + GApps + 无Amazon + 无Root（日常应用/手游友好，少折腾）。列表中「推荐」分数最高者即适配本机。")
        );
    }

    static UIElement LegendCard()
    {
        return Ui.Card("包名含义",
            Ui.Dim("WSA_<版本>_x64_<通道>-GApps|NoGApps|with-magisk|kernelsu-…-NoAmazon.7z"),
            Ui.Dim("• WSA_2407.40000.4.0：WSA 构建版本号"),
            Ui.Dim("• x64 / arm64：CPU 架构（本机请选 x64）"),
            Ui.Dim("• Release-Nightly / Release：构建通道"),
            Ui.Dim("• GApps / MindTheGapps：含 Google 服务与 Play 商店"),
            Ui.Dim("• NoGApps：无 Google，需自行侧载 APK"),
            Ui.Dim("• with-magisk / kernelsu：Root 方案（Magisk / KernelSU）"),
            Ui.Dim("• NoAmazon / RemovedAmazon：去掉 Amazon Appstore（推荐）"),
            Ui.Dim("文件名不含 NoAmazon 时通常仍带 Amazon 组件。")
        );
    }

    void Append(string s) => _log.Text += s + "\n";

    ReleaseInfo? SelectedRelease =>
        _releaseBox.SelectedItem is string tag ? _releases.FirstOrDefault(r => r.Tag == tag) : null;

    ReleaseAssetInfo? SelectedAsset => _assets.SelectedItem as ReleaseAssetInfo;

    async Task LoadAsync()
    {
        _info.Text = "正在拉取 GitHub Releases，并按本机推荐…";
        try
        {
            _releases = await ReleaseCatalog.ListReleasesAsync(MainWindow.Config);
            _releaseBox.Items.Clear();
            var idx = _releases.FindIndex(r => r.IsLts);
            foreach (var r in _releases)
                _releaseBox.Items.Add(r.Tag + (r.IsLts ? "  [LTS]" : ""));
            _releaseBox.SelectedIndex = idx >= 0 ? idx : 0;
            // keep Tag mapping via same index
            _info.Text = $"共 {_releases.Count} 个 Release。本机推荐带「推荐」标记的包。";
            ApplyFilter();
        }
        catch (Exception ex) { _info.Text = ex.Message; }
    }

    void ApplyFilter()
    {
        var i = _releaseBox.SelectedIndex;
        var rel = i >= 0 && i < _releases.Count ? _releases[i] : null;
        _assets.Items.Clear();
        if (rel == null) return;
        var f = _filter.SelectedItem as string ?? "全部变体";
        IEnumerable<ReleaseAssetInfo> q = rel.Assets;
        q = f switch
        {
            "GApps + 无Amazon" => q.Where(a => a.GApps && !a.Amazon),
            "NoGApps + 无Amazon" => q.Where(a => !a.GApps && !a.Amazon),
            "含 Magisk/KernelSU" => q.Where(a => a.Magisk || a.KernelSu),
            "无 Root" => q.Where(a => !a.Magisk && !a.KernelSu),
            _ => q
        };
        foreach (var a in q.OrderByDescending(a => Machine.Score(a, MainWindow.Config)))
        {
            var score = Machine.Score(a, MainWindow.Config);
            var mark = score >= 70 ? "★推荐  " : "";
            _assets.Items.Add(new AssetRow(a, mark, score));
        }
        ShowDetail();
    }

    void ShowDetail()
    {
        if (_assets.SelectedItem is AssetRow row)
        {
            _detail.Text = row.Asset.Name + "\n" + Machine.RecommendReason(row.Asset) +
                           "\n大小 " + (row.Asset.Size / 1024.0 / 1024.0).ToString("F1") + " MB  ·  推荐分 " + row.Score +
                           "\n切换将：备份 userdata → 停 WSA → 注销保留数据 → 干净合并 → 注册 Appx";
        }
        else
            _detail.Text = "选择上方包可查看含义与推荐依据。";
    }

    async Task DownloadOnlyAsync()
    {
        if (_assets.SelectedItem is not AssetRow row) { Append("请先选择一个包"); return; }
        await InstallCoreAsync(row.Asset, apply: false);
    }

    async Task SwitchAsync()
    {
        if (_assets.SelectedItem is not AssetRow row) { Append("请先选择一个包"); return; }
        var a = row.Asset;
        var ok = await Ui.ConfirmAsync("切换 / 安装版本",
            a.Name + "\n" + Machine.RecommendReason(a) + "\n\n干净替换系统文件，保留 userdata 备份与下载目录。继续？");
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
                AssetVersion = asset.WsaVersion
            };
            Append("下载 " + asset.Name + " …");
            var path = await GithubWsabuilds.DownloadAsync(cfg, r, new Progress<double>(p =>
            {
                _bar.IsIndeterminate = false;
                _bar.Visibility = Visibility.Visible;
                _bar.Value = p * 40; // 0-40 download
            }));
            Append("已下载: " + path);
            if (!apply) { _bar.Visibility = Visibility.Collapsed; Append("仅下载完成"); return; }

            _bar.IsIndeterminate = false;
            Append("1/5 备份 userdata…");
            var bak = WsaInstaller.BackupUserdata(cfg);
            if (!string.IsNullOrEmpty(bak)) Append("备份: " + bak);

            Append("2/5 停止 WSA 并释放文件锁…");
            WsaInstaller.StopWsa();

            Append("3/5 干净合并到 " + cfg.WsaInstallDir + " …");
            WsaInstaller.ExtractAndMerge(path, cfg, Append, new Progress<double>(p =>
            {
                _bar.Visibility = Visibility.Visible;
                _bar.Value = 40 + p * 50; // 40-90 merge
            }));

            Append("4-5/5 注册 Appx（UAC）…");
            _bar.IsIndeterminate = true;
            _bar.Visibility = Visibility.Visible;
            Append("Register exit=" + WsaInstaller.RegisterElevated(cfg, Append));
            _bar.Visibility = Visibility.Collapsed;
            await Ui.ShowAsync("完成", "已切换到 " + asset.WsaVersion + "（" + asset.Summary + "）");
        }
        catch (Exception ex)
        {
            _bar.Visibility = Visibility.Collapsed;
            Append("错误: " + ex.Message);
        }
    }

    sealed class AssetRow
    {
        public ReleaseAssetInfo Asset { get; }
        public string Mark { get; }
        public int Score { get; }
        public AssetRow(ReleaseAssetInfo a, string mark, int score) { Asset = a; Mark = mark; Score = score; }
        public override string ToString() => Mark + Asset.WsaVersion + " | " + Asset.Summary + " | " + Asset.Name;
    }
}

static class PanelExtensions
{
    public static T Also<T>(this T obj, Action<T> action) { action(obj); return obj; }
}
