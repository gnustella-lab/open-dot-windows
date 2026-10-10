using Microsoft.UI.Xaml;
using OpenDot.App.Services;

namespace OpenDot.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (s, e) =>
        {
            var crashPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(crashPath)!);
            System.IO.File.AppendAllText(crashPath, $"[{DateTime.UtcNow:O}] UNHANDLED XAML EXCEPTION: {e.Message}\n{e.Exception}\n");
        };
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot", "app.log");
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] App OnLaunched started.\n");

            AppServices.Initialize();
            System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] AppServices initialized.\n");

            _window = new MainWindow();
            System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] MainWindow instantiated.\n");

            _window.Activate();
            System.IO.File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] MainWindow activated.\n");
        }
        catch (Exception ex)
        {
            var crashPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenDot", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(crashPath)!);
            System.IO.File.AppendAllText(crashPath, $"[{DateTime.UtcNow:O}] FATAL ONLAUNCHED EXCEPTION:\n{ex}\n");
            throw;
        }
    }
}
