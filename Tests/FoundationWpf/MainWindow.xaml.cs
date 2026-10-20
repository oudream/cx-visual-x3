using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Logging.Dedicated;
using CxVisual.Foundation.Infrastructure.Monitoring;

namespace CxVisual.Tests.FoundationWpf
{
    public partial class MainWindow : Window
    {
        private readonly FLDLogService _log;
        private readonly FCRSystemMonitor _monitor;
        private readonly string _testRoot;

        public MainWindow()
        {
            InitializeComponent();
            _testRoot = Path.Combine(Path.GetTempPath(), "CxVisual-FoundationWpf",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            _log = new FLDLogService(Path.Combine(_testRoot, "Logs"), 4096, 1500);
            using (Process current = Process.GetCurrentProcess())
                _monitor = new FCRSystemMonitor(1000, current.ProcessName);
            FLDLogHelper.Attach(_log);
            FLDLogHelper.RegisterModuleRule("MainWindow", "Foundation.WpfTest");
            FLDEventLogHelper.Init(Path.Combine(_testRoot, "Synchronous"));
            Viewer.Attach(_log);
            MonitorView.Attach(_monitor);
            _monitor.SamplingFailed += MonitorFailed;
            _monitor.Start();
            StatusText.Text = "测试日志目录：" + _testRoot;
        }

        private void WriteInfo(object sender, RoutedEventArgs e)
        {
            FLDLogHelper.Info("普通日志测试", module: ModuleText.Text);
        }

        private void WriteDedicated(object sender, RoutedEventArgs e)
        {
            try
            {
                FLDLogHelper.ForDedicated(ModuleText.Text, DedicatedText.Text)
                    .Information("专有文件日志测试，Channel/ThirdParty可按需要创建");
                StatusText.Text = "专有文件：" + DedicatedText.Text;
            }
            catch (Exception ex) { StatusText.Text = "参数错误：" + ex.Message; }
        }

        private void WriteSynchronousMes(object sender, RoutedEventArgs e)
        {
            try
            {
                FLDEventLogHelper.Information(FLDLOG_TYPE.Mes, "ManualTest",
                    "FoundationWpf", "MES直接同步写入");
                StatusText.Text = "同步MES已Flush到独立文件（不推送LogViewer）：" +
                    Path.Combine(_testRoot, "Synchronous");
            }
            catch (Exception ex) { StatusText.Text = "同步MES失败：" + ex.Message; }
        }

        private void WriteSynchronousPanda(object sender, RoutedEventArgs e)
        {
            try
            {
                FLDEventLogHelper.Error(FLDLOG_TYPE.Panda, "ManualTest",
                    "FoundationWpf", "Panda直接同步写入");
                StatusText.Text = "同步Panda已Flush到独立文件（不推送LogViewer）：" +
                    Path.Combine(_testRoot, "Synchronous");
            }
            catch (Exception ex) { StatusText.Text = "同步Panda失败：" + ex.Message; }
        }

        private void WriteImportant(object sender, RoutedEventArgs e)
        {
            _log.Log(FLDLogLevel.Critical, ModuleText.Text, "重要事件测试",
                DedicatedText.Text, "ImportantEvent", true);
        }

        private async void FlushLogs(object sender, RoutedEventArgs e)
        {
            try
            {
                await Task.Run(() => _log.Flush());
                StatusText.Text = "Flush成功：" + _testRoot;
            }
            catch (Exception ex) { StatusText.Text = "Flush失败：" + ex.Message; }
        }

        private void SampleOnce(object sender, RoutedEventArgs e)
        {
            try { StatusText.Text = "采样：" + _monitor.SampleNow().TimeUtc.ToString("o"); }
            catch (Exception ex) { StatusText.Text = "采样失败：" + ex.Message; }
        }

        private void Stress1000(object sender, RoutedEventArgs e) { BeginStress(1000); }
        private void Stress10000(object sender, RoutedEventArgs e) { BeginStress(10000); }

        private async void BeginStress(int count)
        {
            StressResult.Text = "正在写入 " + count + " 条...";
            try
            {
                long before = _log.DroppedCount;
                Stopwatch timer = Stopwatch.StartNew();
                int queued = await Task.Run(() =>
                {
                    int accepted = 0;
                    for (int i = 0; i < count; ++i)
                        if (_log.Log(FLDLogLevel.Debug, "F16.Stress", "stress " + i.ToString(),
                            "Performance")) ++accepted;
                    _log.Flush(60000);
                    return accepted;
                });
                timer.Stop();
                StressResult.Text = "提交：" + queued + "/" + count + "，耗时："
                    + timer.ElapsedMilliseconds + " ms；丢弃增量：" + (_log.DroppedCount - before)
                    + "，测试路径：" + _testRoot;
            }
            catch (Exception ex) { StressResult.Text = "压力测试失败：" + ex; }
        }

        private void TestInvalidName(object sender, RoutedEventArgs e)
        {
            try
            {
                _log.ForDedicated("F16", "../escape");
                StressResult.Text = "FAIL：非法名称未拒绝";
            }
            catch (ArgumentException) { StressResult.Text = "PASS：非法路径名称被拒绝"; }
        }

        private void MonitorFailed(Exception ex)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() => StatusText.Text = "监控警告：" + ex.Message));
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            _monitor.SamplingFailed -= MonitorFailed;
            MonitorView.Detach();
            Viewer.Detach();
            _monitor.Dispose();
            try { _log.Flush(15000); }
            catch (Exception ex) { StatusText.Text = "日志Flush失败：" + ex.Message; }
            FLDEventLogHelper.Release();
            FLDLogHelper.Stop();
            _log.Dispose();
        }
    }
}
