using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace CxVisual.Studio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            MainWindow window = new MainWindow();
            MainWindow = window;
            bool cpuOnly = e.Args.Contains("--module-center-cpu-self-test",
                StringComparer.OrdinalIgnoreCase);
            bool testing = cpuOnly ||
                e.Args.Contains("--shell-self-test", StringComparer.OrdinalIgnoreCase);
            if (!testing) { window.Show(); return; }
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.Show();
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StudioShellSelfTest.txt");
                bool success = cpuOnly?
                    StudioShellSelfTests.RunCpuVisionOnly(report):
                    StudioShellSelfTests.Run(window, report);
                window.Close();
                Shutdown(success ? 0 : 1);
            }), DispatcherPriority.ApplicationIdle);
        }
    }
}
