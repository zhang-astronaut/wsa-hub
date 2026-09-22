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
        // Do NOT extend into title bar — that caused header/pane overlap.
        ContentFrame.Navigate(typeof(HomePage));
        _ = InitAsync();
    }

    async Task InitAsync()
    {
        try { Status = await Task.Run(() => WsaProbe.Detect(Config)); }
        catch (Exception ex) { Status = new WsaStatus { Detail = ex.Message }; }
        StatusText.Text = Status.Installed
            ? $"WSA {Status.Version}" + (Status.Running ? " · 运行中" : " · 未运行") + "  ·  {Status.InstallDir}"
            : "未检测到 WSA — 请到「更新安装」";
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
            Foreground = new SolidColorBrush(Color.FromArgb(255, 242, 242, 247)),
            Margin = new Thickness(0, 0, 0, 4)
        });
        foreach (var c in children) sp.Children.Add(c);
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 42, 42, 48)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 14),
            MaxWidth = 720,
            HorizontalAlignment = HorizontalAlignment.Left,
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

    public static Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(0, 6, 8, 0),
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        b.Click += async (_, __) =>
        {
            var old = b.Content as string;
            try
            {
                b.IsEnabled = false;
                b.Content = "…";
                onClick();
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally
            {
                await Task.Delay(150);
                b.Content = old;
                b.IsEnabled = true;
            }
        };
        return b;
    }

    public static Button BtnAsync(string text, Func<Task> onClick)
    {
        var b = new Button
        {
            Content = text,
            Margin = new Thickness(0, 6, 8, 0),
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        b.Click += async (_, __) =>
        {
            var old = b.Content as string;
            try
            {
                b.IsEnabled = false;
                b.Content = "请稍候…";
                await onClick();
            }
            catch (Exception ex) { ShowError(ex.Message); }
            finally
            {
                b.Content = old;
                b.IsEnabled = true;
            }
        };
        return b;
    }

    public static void ShowError(string msg) => _ = ShowAsync("提示", msg);

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
        var sp = new StackPanel
        {
            Spacing = 4,
            Padding = new Thickness(16, 12, 16, 24),
            MaxWidth = 760,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var c in children) sp.Children.Add(c);
        return new ScrollViewer
        {
            Content = sp,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }
}
