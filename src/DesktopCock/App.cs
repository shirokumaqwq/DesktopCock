using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace DesktopCock;
public sealed class App : Application
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var single = new Mutex(true, "Local\\DesktopCock.Pet", out var first);
        if (!first) return;
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Directory.CreateDirectory(PetWindow.DataFolder);
            File.AppendAllText(Path.Combine(PetWindow.DataFolder, "error.log"), $"{DateTime.Now:O} {e.Exception}\n");
        };
        var pet = new PetWindow();
        app.MainWindow = pet;
        pet.Show();
#if DEBUG
        if (Array.IndexOf(args, "--preview") >= 0) pet.OpenPreview();
        if (Array.IndexOf(args, "--flight-debug") >= 0) pet.SetFlightDebug(true);
#endif
        app.Run();
    }
}
