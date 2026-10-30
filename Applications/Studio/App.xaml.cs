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
            bool roiUi = e.Args.Contains("--studio-roi-dialog-ui-self-test",
                StringComparer.OrdinalIgnoreCase);
            bool testing = cpuOnly || roiUi ||
                e.Args.Contains("--shell-self-test", StringComparer.OrdinalIgnoreCase);
            if (!testing) { window.Show(); return; }
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.Show();
            if(roiUi)
            {
                window.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "StudioRoiDialogUiReport.txt");
                    bool success=await StudioRoiDialogUiSmoke.RunAsync(window,report);
                    window.Close();
                    Shutdown(success?0:1);
                }),DispatcherPriority.ApplicationIdle);
                return;
            }
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StudioShellSelfTest.txt");
                bool success = cpuOnly?
                    StudioShellSelfTests.RunCpuVisionOnly(report):
                    StudioShellSelfTests.Run(window, report);
                if(cpuOnly)
                {
                    // Dedicated CI process has no interactive owner and has already
                    // flushed the complete report. Do not let unrelated WPF shutdown
                    // callbacks keep a finished headless acceptance job alive.
                    Environment.Exit(success?0:1);
                    return;
                }
                window.Close();
                Shutdown(success ? 0 : 1);
            }), DispatcherPriority.ApplicationIdle);
        }
    }
}
