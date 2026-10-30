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
            bool hmiUi = e.Args.Contains("--hmi-studio-smoke",
                StringComparer.OrdinalIgnoreCase);
            bool testing = cpuOnly || roiUi || hmiUi ||
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
                    // 独立GUI验收是有明确PASS/FAIL报告的一次性Windows进程。
                    // 模态UIA、外部MHS/Host服务或WPF布局退出回调不能让
                    // 已落盘的真实FAIL被120秒进程超时覆盖；正式Studio不受影响。
                    Environment.Exit(success?0:1);
                }),DispatcherPriority.ApplicationIdle);
                return;
            }
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                string report = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StudioShellSelfTest.txt");
                bool success = cpuOnly?
                    StudioShellSelfTests.RunCpuVisionOnly(report):
                    hmiUi ? StudioShellSelfTests.RunHmiOnly(window,report) :
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
