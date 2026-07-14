using System.Threading;
using System.Windows;

namespace PTGOilSystem.Desktop;

public partial class App : Application
{
    // Req #5 — single instance only. The mutex is held for the app lifetime.
    private const string SingleInstanceMutexName = "Global\\PTGOilSystem.Desktop.SingleInstance";
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "نرم‌افزار PTG Oil System هم‌اکنون در حال اجراست.",
                "PTG Oil System",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
