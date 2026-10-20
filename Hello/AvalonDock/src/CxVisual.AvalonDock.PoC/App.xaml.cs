using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace CxVisual.DockingPoC
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            bool selfTest = e.Args.Length > 0 &&
                string.Equals(e.Args[0], "--self-test", StringComparison.OrdinalIgnoreCase);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!selfTest)
            {
                MainWindow window = new MainWindow(true);
                MainWindow = window;
                window.Show();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                return;
            }

            string report = e.Args.Length > 1 ? e.Args[1] :
                Path.Combine(Path.GetTempPath(), "CxVisual-AvalonDock-PoC-selftest.txt");
            // 安排到 Dispatcher 正常运行后测试，确保 WPF Loaded/布局初始化已经完成。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool passed = false;
                MainWindow testWindow = null;
                try
                {
                    testWindow = new MainWindow(false);
                    MainWindow = testWindow;
                    testWindow.Show();
                    // 再进入一个 Dispatcher 周期，使 WPF Layout/Loaded/窗口句柄稳定。
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { passed = DockingSelfTests.Run(testWindow, report); }
                        catch (Exception exception)
                        {
                            File.WriteAllText(report, "FAIL: " + exception);
                        }
                        finally
                        {
                            testWindow.Close();
                            Shutdown(passed ? 0 : 1);
                        }
                    }), DispatcherPriority.ApplicationIdle);
                }
                catch (Exception exception)
                {
                    File.WriteAllText(report, "FAIL: " + exception);
                    if (testWindow != null) testWindow.Close();
                    Shutdown(1);
                }
            }), DispatcherPriority.ApplicationIdle);
        }
    }
}
