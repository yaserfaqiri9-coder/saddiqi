using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PTGOilSystem.Desktop.Services;

/// <summary>
/// Starts the existing PTGOilSystem.Web app as a child process on a loopback
/// address, waits for its /health probe, and guarantees the process is killed
/// when the desktop app exits. The web app itself is never modified.
/// </summary>
public sealed class WebServerLauncher : IDisposable
{
    private readonly WebOptions _options;
    private readonly string _baseDir;
    private Process? _process;
    private bool _disposed;

    public WebServerLauncher(WebOptions options)
    {
        _options = options;
        _baseDir = AppContext.BaseDirectory;
    }

    /// <summary>The root URL once the server is reachable, e.g. http://127.0.0.1:5187 .</summary>
    public string? BaseUrl { get; private set; }

    /// <summary>
    /// Launch the web server and block until /health returns 200 or the
    /// configured timeout elapses.
    /// </summary>
    public async Task<string> StartAsync(IProgress<string>? progress, CancellationToken ct)
    {
        var host = string.IsNullOrWhiteSpace(_options.Host) ? "127.0.0.1" : _options.Host;
        var port = _options.Port > 0 ? _options.Port : GetFreePort(host);
        var url = $"http://{host}:{port}";

        var startInfo = BuildStartInfo(url);
        progress?.Report("در حال راه‌اندازی سرور محلی…");

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!_process.Start())
            throw new InvalidOperationException("راه‌اندازی فرایند وب ناموفق بود.");

        await WaitForHealthAsync(url, progress, ct);
        BaseUrl = url;
        return url;
    }

    private ProcessStartInfo BuildStartInfo(string url)
    {
        var exePath = ResolvePath(_options.ExecutablePath);
        var dllPath = ResolvePath(_options.DllPath);
        var devProj = ResolvePath(_options.DevProjectPath);

        ProcessStartInfo psi;
        if (exePath is not null && File.Exists(exePath))
        {
            psi = new ProcessStartInfo(exePath);
            psi.WorkingDirectory = Path.GetDirectoryName(exePath)!;
        }
        else if (dllPath is not null && File.Exists(dllPath))
        {
            psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add(dllPath);
            psi.WorkingDirectory = Path.GetDirectoryName(dllPath)!;
        }
        else if (devProj is not null && File.Exists(devProj))
        {
            // Development fallback: run straight from source.
            psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(devProj);
            psi.ArgumentList.Add("--no-launch-profile");
            psi.WorkingDirectory = Path.GetDirectoryName(devProj)!;
        }
        else
        {
            throw new FileNotFoundException(
                "فایل اجرایی وب پیدا نشد. ExecutablePath/DllPath/DevProjectPath را در appsettings.json بررسی کنید.");
        }

        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        // The web app reads ASPNETCORE_URLS to know where to bind.
        psi.Environment["ASPNETCORE_URLS"] = url;
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = _options.Environment;

        // Req #9 — desktop never migrates implicitly unless explicitly enabled.
        psi.Environment["PTG_AUTO_MIGRATE"] = _options.AutoMigrate ? "true" : "false";

        // Req #7 — DB connection string stays configurable from appsettings.json.
        if (!string.IsNullOrWhiteSpace(_options.ConnectionString))
            psi.Environment["ConnectionStrings__DefaultConnection"] = _options.ConnectionString;

        return psi;
    }

    private async Task WaitForHealthAsync(string url, IProgress<string>? progress, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(5, _options.StartupTimeoutSeconds));
        var healthUrl = $"{url}/health";

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (_process is { HasExited: true })
                throw new InvalidOperationException(
                    $"سرور وب پیش از آماده شدن متوقف شد (کد خروج {_process.ExitCode}).");

            try
            {
                using var res = await http.GetAsync(healthUrl, ct);
                if (res.IsSuccessStatusCode)
                {
                    progress?.Report("سرور آماده شد.");
                    return;
                }
            }
            catch
            {
                // Not up yet — keep polling.
            }

            await Task.Delay(400, ct);
        }

        throw new TimeoutException("زمان انتظار برای آماده شدن سرور وب به پایان رسید.");
    }

    private string? ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(_baseDir, path));
    }

    private static int GetFreePort(string host)
    {
        var address = IPAddress.TryParse(host, out var ip) ? ip : IPAddress.Loopback;
        var listener = new TcpListener(address, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Req #6 — kill the whole web process tree on exit.
        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort — process may already be gone.
        }
        finally
        {
            _process?.Dispose();
            _process = null;
        }
    }
}
