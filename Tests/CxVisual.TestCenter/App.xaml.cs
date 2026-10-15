using System;
using System.IO;
using System.Threading;
using System.Windows;
using CxVisual.TestCenter.Cases;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public partial class TCTApp : Application
    {
        // --run --report PATH 为无UI回归入口；UI与Runner严格分离。
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (e.Args.Length > 0 && string.Equals(e.Args[0], "--run", StringComparison.OrdinalIgnoreCase))
            {
                string output = Path.Combine(Path.GetTempPath(), "CxVisual-TestCenter-last-run.txt");
                for (int i = 1; i < e.Args.Length - 1; i++)
                    if (string.Equals(e.Args[i], "--report", StringComparison.OrdinalIgnoreCase))
                        output = e.Args[++i];
                try
                {
                    TCTTestCatalog catalog = new TCTTestCatalog();
                    TCTRealCases.Register(catalog);
                    TCTTestRunSummary summary = await new TCTTestRunner().RunAsync(
                        catalog.Cases, CancellationToken.None, null);
                    TCTReportWriter.WriteText(output, summary);
                    Shutdown(summary.Failed == 0 && summary.Cancelled == 0 && summary.Total == catalog.Cases.Count ? 0 : 1);
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(output, "TEST HOST ERROR\r\n" + ex); } catch (IOException) { }
                    Shutdown(2);
                }
                return;
            }
            TCTMainWindow window = new TCTMainWindow();
            MainWindow = window;
            window.Show();
        }
    }
}
