using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public static class Machine
{
    public static string Cpu { get; private set; } = "";
    public static bool IsArm64Host { get; private set; }

    public static void Probe()
    {
        try
        {
            var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
            IsArm64Host = arch.Contains("Arm64", StringComparison.OrdinalIgnoreCase);
        }
        catch { IsArm64Host = false; }
        try
        {
            Cpu = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0")?
                .GetValue("ProcessorNameString") as string ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "";
        }
        catch { Cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? ""; }
    }

    /// <summary>Score an asset for this PC (higher = better).</summary>
    public static int Score(ReleaseAssetInfo a, HubConfig cfg)
    {
        int s = 0;
        var name = a.Name;
        // arch: prefer x64 on x64 host
        bool x64 = name.Contains("_x64_", StringComparison.OrdinalIgnoreCase) || name.Contains("x64", StringComparison.OrdinalIgnoreCase);
        bool arm64 = name.Contains("arm64", StringComparison.OrdinalIgnoreCase);
        if (!IsArm64Host && x64) s += 40;
        if (!IsArm64Host && arm64) s -= 50;
        if (IsArm64Host && arm64) s += 40;
        if (IsArm64Host && x64) s += 5; // x64 emu ok

        // user prefs: GApps, NoAmazon
        if (a.GApps) s += 25; else s += 5;
        if (!a.Amazon) s += 15; else s -= 5;
        // root: slight penalty (complexity) unless name suggests needed
        if (!a.Magisk && !a.KernelSu) s += 8;
        // LTS / newest already ordered by list
        return s;
    }

    public static string RecommendReason(ReleaseAssetInfo a)
    {
        var arch = IsArm64Host ? "ARM64 主机" : "x64 主机";
        var bits = new List<string> { arch, a.GApps ? "含 GApps" : "无 GApps", a.Amazon ? "含 Amazon" : "无 Amazon", a.RootLabel };
        return "推荐依据：" + string.Join(" · ", bits);
    }
}

public sealed partial class MainWindow : Window
{
    public static HubConfig Config { get; private set; } = HubConfig.Load();
    public static WsaStatus Status { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Machine.Probe();
        ContentFrame.Navigate(typeof(HomePage));
        _ = InitAsync();
    }

    async Task InitAsync()
    {
        try { Status = await Task.Run(() => WsaProbe.Detect(Config)); }
        catch (Exception ex) { Status = new WsaStatus { Detail = ex.Message }; }
        if (Status.Installed)
            StatusText.Text = "WSA " + Status.Version + (Status.Running ? " · 运行中" : " · 未运行") + " · " + Status.InstallDir;
        else
            StatusText.Text = "未检测到 WSA — 请到「更新安装」";
    }

    async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "检测中…";
        await InitAsync();
    }

    void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        var tag = (args.InvokedItemContainer as NavigationViewItem)?.Tag as string ?? "home";
        Type page = tag switch
        {
            "update" => typeof(UpdatePage),
            "adb" => typeof(AdbPage),
            "apps" => typeof(AppsPage),
            "backup" => typeof(BackupPage),
            "settings" => typeof(SettingsPage),
            "logs" => typeof(LogsPage),
            _ => typeof(HomePage)
        };
        ContentFrame.Navigate(page);
    }
}

public static class Ui
{
    public static Border Card(string title, params UIElement[] children)
    {
        var sp = new StackPanel { Spacing = 8 };
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 242, 242, 247))
        });
        foreach (var c in children) sp.Children.Add(c);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 48)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = sp
        };
    }

    public static TextBlock Lbl(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 200, 200, 208)),
        Margin = new Thickness(0, 2, 0, 2)
    };

    public static TextBlock Dim(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 150, 150, 158)),
        FontSize = 12,
        Margin = new Thickness(0, 2, 0, 2)
    };

    public static Button BusyBtn(string text, Func<Task> onClick)
    {
        var ring = new ProgressRing { Width = 16, Height = 16, IsActive = false, Visibility = Visibility.Collapsed };
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(ring);
        panel.Children.Add(label);
        var b = new Button
        {
            Content = panel,
            Margin = new Thickness(0, 6, 8, 0),
            Padding = new Thickness(14, 8, 14, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        b.Click += async (_, __) =>
        {
            if (!b.IsEnabled) return;
            b.IsEnabled = false;
            ring.IsActive = true;
            ring.Visibility = Visibility.Visible;
            label.Text = "请稍候…";
            try { await onClick(); }
            catch (Exception ex) { ShowError(ex.Message); }
            finally
            {
                ring.IsActive = false;
                ring.Visibility = Visibility.Collapsed;
                label.Text = text;
                b.IsEnabled = true;
            }
        };
        return b;
    }

    public static Button BusyBtn(string text, Action onClick) => BusyBtn(text, () => { onClick(); return Task.CompletedTask; });
    public static Button Btn(string text, Action onClick) => BusyBtn(text, onClick);
    public static Button Btn(string text, Func<Task> onClick) => BusyBtn(text, onClick);

    public static void ShowError(string msg) => _ = ShowAsync("提示", msg);

    public static async Task ShowAsync(string title, string body)
    {
        try
        {
            var w = new ContentDialog { Title = title, Content = body, CloseButtonText = "确定", XamlRoot = App.MainWindow?.Content?.XamlRoot };
            await w.ShowAsync();
        }
        catch { }
    }

    public static async Task<bool> ConfirmAsync(string title, string body)
    {
        try
        {
            var w = new ContentDialog
            {
                Title = title,
                Content = body,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainWindow?.Content?.XamlRoot
            };
            return await w.ShowAsync() == ContentDialogResult.Primary;
        }
        catch { return false; }
    }

    /// <summary>Full-width fluid page body (resizes with window, no wasted margin).</summary>
    public static FrameworkElement Page(params UIElement[] children)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var sp = new StackPanel { Spacing = 4, Padding = new Thickness(16, 12, 16, 8), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var c in children) sp.Children.Add(c);
        sp.SetValue(Grid.RowProperty, 0);
        grid.Children.Add(sp);
        return new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }
}
