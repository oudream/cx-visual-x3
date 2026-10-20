using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace CxVisual.Foundation.Infrastructure.Monitoring
{
    /// <summary>
    /// 基础系统和进程采样。仅提供状态，不负责进程重启、工业安全和运行调度。
    /// Windows x64/.NET Framework 4.8，CPU百分比需要两次采样间隔。
    /// </summary>
    public sealed class FCRSystemMonitor : IDisposable
    {
        private readonly object _gate = new object();
        private readonly AutoResetEvent _stopSignal = new AutoResetEvent(false);
        private readonly int _intervalMs;
        private readonly string[] _processNames;
        private readonly Dictionary<string, CpuHistory> _previousProcesses
            = new Dictionary<string, CpuHistory>(StringComparer.Ordinal);
        private Thread _worker;
        private bool _disposed;
        private bool _stop;
        private long _lastIdle, _lastKernel, _lastUser;
        private bool _haveCpu;
        private DateTime _lastProcessSample;
        private FCRSystemSnapshot _latest;

        public FCRSystemMonitor(int intervalMilliseconds = 1000, params string[] processNames)
        {
            if (intervalMilliseconds < 200) throw new ArgumentOutOfRangeException("intervalMilliseconds");
            _intervalMs = intervalMilliseconds;
            _processNames = processNames ?? new string[0];
        }

        public event Action<FCRSystemSnapshot> Sampled;
        public event Action<Exception> SamplingFailed;
        public FCRSystemSnapshot Latest { get { lock (_gate) { return _latest; } } }

        public void Start()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_worker != null) return;
                _stop = false;
                _worker = new Thread(Loop)
                { IsBackground = true, Name = "CxVisual.SystemMonitor" };
                _worker.Start();
            }
        }

        public void Stop()
        {
            Thread thread;
            lock (_gate)
            {
                thread = _worker;
                if (thread == null) return;
                _stop = true;
                _stopSignal.Set();
            }
            if (Thread.CurrentThread == thread)
                throw new InvalidOperationException("Stop must not be called in sampling callback.");
            thread.Join();
            lock (_gate)
            {
                if (_worker == thread) _worker = null;
            }
        }

        private void Loop()
        {
            while (true)
            {
                lock (_gate) { if (_stop) break; }
                try
                {
                    FCRSystemSnapshot snapshot = SampleNow();
                    Action<FCRSystemSnapshot> handlers = Sampled;
                    if (handlers != null)
                        foreach (Delegate del in handlers.GetInvocationList())
                            try { ((Action<FCRSystemSnapshot>)del)(snapshot); }
                            catch (Exception ex) { RaiseFailed(ex); }
                }
                catch (Exception ex) { RaiseFailed(ex); }
                if (_stopSignal.WaitOne(_intervalMs)) break;
            }
        }

        private void RaiseFailed(Exception exception)
        {
            Action<Exception> handlers = SamplingFailed;
            if (handlers == null) return;
            foreach (Delegate callback in handlers.GetInvocationList())
                try { ((Action<Exception>)callback)(exception); } catch { /* 采集服务没有日志递归 */ }
        }

        public FCRSystemSnapshot SampleNow()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                string warning = string.Empty;
                DateTime now = DateTime.UtcNow;
                double? cpu = ReadCpu();
                ulong? total = null, available = null;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    MemoryStatus memory = new MemoryStatus();
                    memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
                    if (GlobalMemoryStatusEx(ref memory))
                    {
                        total = memory.TotalPhysical;
                        available = memory.AvailablePhysical;
                    }
                    else warning += "GlobalMemoryStatusEx failed. ";
                }
                else warning += "Non-Windows memory counters unavailable. ";

                long diskTotal = 0, diskFree = 0;
                try
                {
                    foreach (DriveInfo d in DriveInfo.GetDrives())
                    {
                        if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                        diskTotal += d.TotalSize;
                        diskFree += d.AvailableFreeSpace;
                    }
                }
                catch (Exception ex) { warning += "Drive: " + ex.GetType().Name + ". "; }

                int activeNics = 0;
                try
                {
                    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                        if (nic.OperationalStatus == OperationalStatus.Up) ++activeNics;
                }
                catch (Exception ex) { warning += "Network: " + ex.GetType().Name + ". "; }

                List<FCRProcessSnapshot> processes = SampleProcesses(now, ref warning);
                _latest = new FCRSystemSnapshot(now, Environment.MachineName,
                    Environment.OSVersion.VersionString, Environment.ProcessorCount,
                    cpu, total, available, diskTotal, diskFree, activeNics, processes, warning);
                return _latest;
            }
        }

        private double? ReadCpu()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return null;
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return null;
            double? result = null;
            if (_haveCpu)
            {
                long all = (kernel - _lastKernel) + (user - _lastUser);
                long idleDelta = idle - _lastIdle;
                if (all > 0) result = Math.Max(0, Math.Min(100, 100.0 * (all - idleDelta) / all));
            }
            _lastIdle = idle; _lastKernel = kernel; _lastUser = user; _haveCpu = true;
            return result;
        }

        private List<FCRProcessSnapshot> SampleProcesses(DateTime now, ref string warning)
        {
            List<FCRProcessSnapshot> result = new List<FCRProcessSnapshot>();
            Dictionary<string, CpuHistory> newHistory
                = new Dictionary<string, CpuHistory>(StringComparer.Ordinal);
            double elapsed = _lastProcessSample == default(DateTime)
                ? 0 : (now - _lastProcessSample).TotalMilliseconds;
            _lastProcessSample = now;
            int currentPid;
            using (Process current = Process.GetCurrentProcess()) currentPid = current.Id;
            Process[] processes = Process.GetProcesses();
            try
            {
                foreach (Process p in processes)
                {
                    try
                    {
                        string name = p.ProcessName;
                        bool isCurrent = p.Id == currentPid;
                        bool selected = _processNames.Length == 0 || isCurrent;
                        foreach (string requested in _processNames)
                            if (string.Equals(name, requested, StringComparison.OrdinalIgnoreCase))
                                selected = true;
                        if (!selected) continue;
                        long? started = null;
                        try { started = p.StartTime.ToUniversalTime().Ticks; } catch { }
                        double cpuMs = p.TotalProcessorTime.TotalMilliseconds;
                        string identity = p.Id + ":" + (started.HasValue ? started.Value.ToString() : "?");
                        double? cpuPct = null;
                        CpuHistory before;
                        if (started.HasValue && elapsed > 0
                            && _previousProcesses.TryGetValue(identity, out before))
                            cpuPct = Math.Max(0, Math.Min(100,
                                (cpuMs - before.CpuMilliseconds) * 100
                                / (elapsed * Environment.ProcessorCount)));
                        newHistory[identity] = new CpuHistory(cpuMs);
                        int? handles = null;
                        try { handles = p.HandleCount; } catch { }
                        result.Add(new FCRProcessSnapshot(p.Id, name, started, cpuPct,
                            p.WorkingSet64, p.Threads.Count, handles));
                    }
                    catch (Exception ex) { warning += "Process " + p.Id + ": " + ex.GetType().Name + ". "; }
                }
            }
            finally { foreach (Process p in processes) p.Dispose(); }
            _previousProcesses.Clear();
            foreach (KeyValuePair<string, CpuHistory> item in newHistory)
                _previousProcesses.Add(item.Key, item.Value);
            return result;
        }

        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; }
            Stop();
            lock (_gate) { _disposed = true; }
            _stopSignal.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("FCRSystemMonitor");
        }

        private sealed class CpuHistory
        {
            public readonly double CpuMilliseconds;
            public CpuHistory(double milliseconds) { CpuMilliseconds = milliseconds; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MemoryStatus
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhysical, AvailablePhysical, TotalPageFile,
                AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    }
}
