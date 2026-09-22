using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WsaHub.Core;

namespace WsaHub.WinUI3;

public sealed partial class MainWindow : Window
{
    public static HubConfig Config { get; private set; } = HubConfig.Load();
    public static WsaStatus Status { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(StatusBar);
        ContentFrame.Navigate(typeof(HomePage));
        _ = InitAsync();
    }

    async Task InitAsync()
    {
        try { Status = await Task.Run(() => WsaProbe.Detect(Config)); }
        catch (Exception ex) { Status = new WsaStatus { Detail = ex.Message }; }
        StatusText.Text = Status.Installed
            ? $"WSA {Status.Version}" + (Status.Running ? " · 运行中" : " · 未运行") + "  ·  " + Status.InstallDir
            : "未检测到 WSA — 请到「更新与安装」全新安装";
        if (!Status.Installed) ContentFrame.Navigate(typeof(UpdatePage));
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
        var sp = new StackPanel { Spacing = 6 };
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
            Child = sp
        };
    }

    public static TextBlock Lbl(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromArgb(255, 210, 210, 216))
    };

    public static Button Btn(string text, Action onClick)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 4, 8, 0) };
        b.Click += (_, __) => { try { onClick(); } catch (Exception ex) { ShowError(ex.Message); } };
        return b;
    }

    public static void ShowError(string msg) => _ = ShowAsync("WsaHub", msg);

    public static async Task ShowAsync(string title, string body)
    {
        try
        {
            var w = new ContentDialog
            {
                Title = title,
                Content = body,
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow?.Content?.XamlRoot
            };
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

    public static ScrollViewer Page(params UIElement[] children)
    {
        var sp = new StackPanel { Spacing = 4, Padding = new Thickness(4) };
        foreach (var c in children) sp.Children.Add(c);
        return new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
