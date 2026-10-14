using System;
using System.Globalization;
using System.Windows.Controls;
using CxVisual.Foundation.Infrastructure;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>系统和进程指标呈现；采样和资源所有权保留在F19。</summary>
    public partial class SystemMonitorControl : UserControl
    {
        private FCRSystemMonitor _monitor;

        public SystemMonitorControl()
        {
            InitializeComponent();
            Unloaded += (sender, args) => Detach();
        }

        public void Attach(FCRSystemMonitor monitor)
        {
            if (monitor == null) throw new ArgumentNullException("monitor");
            if (!Dispatcher.CheckAccess())
                throw new InvalidOperationException("Attach must run on the UI thread.");
            Detach();
            _monitor = monitor;
            _monitor.Sampled += UpdateFromWorker;
            if (_monitor.Latest != null) Show(_monitor.Latest);
        }

        public void Detach()
        {
            if (_monitor != null) _monitor.Sampled -= UpdateFromWorker;
            _monitor = null;
        }

        private void UpdateFromWorker(FCRSystemSnapshot snapshot)
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_monitor != null) Show(snapshot);
            }));
        }

        private void Show(FCRSystemSnapshot snapshot)
        {
            CpuText.Text = snapshot.TotalCpuPercent.HasValue
                ? snapshot.TotalCpuPercent.Value.ToString("F1", CultureInfo.InvariantCulture) + "%"
                : "采样中";
            MemoryText.Text = snapshot.MemoryAvailableBytes.HasValue
                ? (snapshot.MemoryAvailableBytes.Value / 1073741824.0).ToString("F1") + " GB"
                : "不可用";
            DiskText.Text = (snapshot.DiskAvailableBytes / 1073741824.0).ToString("F1") + " GB";
            NetworkText.Text = snapshot.ActiveNetworkInterfaces.ToString();
            StatusText.Text = snapshot.Machine + " | " + snapshot.TimeUtc.ToString("HH:mm:ss")
                + " UTC | " + snapshot.Warning;
            ProcessTable.ItemsSource = snapshot.Processes;
        }
    }
}
