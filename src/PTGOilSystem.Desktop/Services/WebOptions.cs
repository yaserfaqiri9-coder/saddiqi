namespace PTGOilSystem.Desktop.Services;

/// <summary>
/// Strongly-typed view of the "Web" section in appsettings.json.
/// </summary>
public sealed class WebOptions
{
    public string ConnectionString { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; }
    public string Environment { get; set; } = "Production";
    public bool AutoMigrate { get; set; }
    public int StartupTimeoutSeconds { get; set; } = 60;
    public string ExecutablePath { get; set; } = "";
    public string DllPath { get; set; } = "";
    public string DevProjectPath { get; set; } = "";
}
