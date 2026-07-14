using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Web.WebView2.Core;
using PTGOilSystem.Desktop.Services;

namespace PTGOilSystem.Desktop;

public partial class MainWindow : Window
{
    private readonly WebServerLauncher _launcher;
    private readonly WebOptions _options;
    private readonly CancellationTokenSource _cts = new();

    public MainWindow()
    {
        InitializeComponent();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        _options = config.GetSection("Web").Get<WebOptions>() ?? new WebOptions();
        _launcher = new WebServerLauncher(_options);

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var progress = new Progress<string>(msg => StatusText.Text = msg);
        try
        {
            var url = await _launcher.StartAsync(progress, _cts.Token);
            await InitializeWebViewAsync(url);

            WebView.Visibility = Visibility.Visible;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async Task InitializeWebViewAsync(string url)
    {
        // Keep the WebView2 user-data folder per-user and out of Program Files.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PTGOilSystem", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        // Office desktop: no dev tools, no default context menu noise.
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

        WebView.Source = new Uri(url);
    }

    private void ShowError(string message)
    {
        StatusText.Text = "راه‌اندازی ناموفق بود.";
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        // Req #6 — stop the web process when the desktop app exits.
        _cts.Cancel();
        _launcher.Dispose();
        _cts.Dispose();
    }
}
