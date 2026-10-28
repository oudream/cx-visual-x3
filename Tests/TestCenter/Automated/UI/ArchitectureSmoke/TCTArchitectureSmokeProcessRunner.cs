using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// ArchitectureSmoke is an existing standalone EXE. These are its actual Program.cs
    /// command-line entrypoints, not a second TestCenter Catalog or test runner.
    /// </summary>
    public sealed class TCTArchitectureSmokeProfile
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Arguments { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public TCTArchitectureSmokeProfile(string id, string title, string arguments, int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(id) || title == null || timeoutSeconds <= 0)
                throw new ArgumentException("Invalid ArchitectureSmoke profile");
            Id = id; Title = title; Arguments = arguments ?? "";
            Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
        public override string ToString() { return Title; }
    }

    public static class TCTArchitectureSmokeProfiles
    {
        private static readonly TCTArchitectureSmokeProfile[] _all = {
            new TCTArchitectureSmokeProfile("default", "默认架构回归", "", 600),
            new TCTArchitectureSmokeProfile("foundation", "Foundation", "--foundation", 300),
            new TCTArchitectureSmokeProfile("concurrency", "并发回归", "--concurrency", 300),
            new TCTArchitectureSmokeProfile("channel-transport", "F17 全通道与Connector回环", "--channel-transport", 300),
            new TCTArchitectureSmokeProfile("channel-bulk", "F17 TCP大数据 / 背压 / 租约诊断", "--channel-bulk", 300),
            new TCTArchitectureSmokeProfile("channel-bulk-long", "F17 TCP与跨进程管道双向各2GiB / CPU内存GC", "--channel-bulk-long", 600),
            new TCTArchitectureSmokeProfile("organization", "工程组织", "--organization", 300),
            new TCTArchitectureSmokeProfile("organization-wpf", "工程组织 WPF", "--organization-wpf", 300),
            new TCTArchitectureSmokeProfile("config-foundation", "配置基础", "--config-foundation", 300),
            new TCTArchitectureSmokeProfile("config-wpf", "配置 WPF", "--config-wpf", 300),
            new TCTArchitectureSmokeProfile("config-performance-smoke", "配置性能 Smoke", "--config-performance-smoke", 300),
            new TCTArchitectureSmokeProfile("config-performance-full", "配置性能 Full", "--config-performance-full", 900),
            new TCTArchitectureSmokeProfile("organization-benchmark", "组织 Benchmark", "--organization-benchmark", 900),
            new TCTArchitectureSmokeProfile("organization-stress", "组织 Stress", "--organization-stress", 900)
        };
        public static IReadOnlyList<TCTArchitectureSmokeProfile> All { get { return Array.AsReadOnly(_all); } }
        public static TCTArchitectureSmokeProfile Find(string id)
        {
            return _all.SingleOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
        }
    }

    public sealed class TCTArchitectureSmokeRunResult
    {
        public string ProfileId { get; internal set; }
        public string ExecutablePath { get; internal set; }
        public string Arguments { get; internal set; }
        public DateTime StartedUtc { get; internal set; }
        public TimeSpan Elapsed { get; internal set; }
        public int? ExitCode { get; internal set; }
        public bool Cancelled { get; internal set; }
        public bool TimedOut { get; internal set; }
        public string Failure { get; internal set; }
        public bool Succeeded
        {
            get { return ExitCode == 0 && !Cancelled && !TimedOut &&
                string.IsNullOrEmpty(Failure); }
        }
    }

    /// <summary>
    /// Launches only the verified sibling ArchitectureSmoke.exe using a fixed profile.
    /// No shell, arbitrary command line, or implicit test-case Passed conversion.
    /// </summary>
    public sealed class TCTArchitectureSmokeProcessRunner : IDisposable
    {
        private readonly object _gate = new object();
        private Process _process;
        private bool _disposed;
        public event Action<string, bool> OutputReceived;

        public static string ResolveExecutable(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Empty executable directory");
            return Path.Combine(Path.GetFullPath(directory), "CxVisual.Tests.ArchitectureSmoke.exe");
        }
        public string ExecutablePath { get { return ResolveExecutable(AppDomain.CurrentDomain.BaseDirectory); } }

        public async Task<TCTArchitectureSmokeRunResult> RunAsync(
            TCTArchitectureSmokeProfile profile, CancellationToken cancellationToken)
        {
            if (profile == null || TCTArchitectureSmokeProfiles.Find(profile.Id) != profile)
                throw new ArgumentException("Unregistered ArchitectureSmoke mode", "profile");
            string file = ExecutablePath;
            if (!File.Exists(file))
                throw new FileNotFoundException("ArchitectureSmoke executable not built", file);

            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(file, profile.Arguments) {
                WorkingDirectory = Path.GetDirectoryName(file),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            TCTArchitectureSmokeRunResult result = new TCTArchitectureSmokeRunResult {
                ProfileId = profile.Id, ExecutablePath = file, Arguments = profile.Arguments,
                StartedUtc = DateTime.UtcNow
            };
            Stopwatch stopwatch = Stopwatch.StartNew();
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("TCTArchitectureSmokeProcessRunner");
                if (_process != null) throw new InvalidOperationException("ArchitectureSmoke is already running");
                _process = process;
            }
            try
            {
                if (!process.Start()) throw new InvalidOperationException("ArchitectureSmoke process start returned false");
                process.OutputDataReceived += (sender, args) => Emit(args.Data, false);
                process.ErrorDataReceived += (sender, args) => Emit(args.Data, true);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                Task completed = Task.Run(() => process.WaitForExit());
                Task delay = Task.Delay(profile.Timeout);
                Task cancelled = Task.Delay(Timeout.Infinite, cancellationToken);
                Task first = await Task.WhenAny(completed, delay, cancelled).ConfigureAwait(false);
                if (first != completed)
                {
                    result.Cancelled = cancellationToken.IsCancellationRequested;
                    result.TimedOut = !result.Cancelled;
                    StopProcessTree(process);
                }
                await completed.ConfigureAwait(false);
                result.ExitCode = process.ExitCode;
            }
            catch (Exception ex)
            {
                result.Failure = ex.ToString();
                StopProcessTree(process);
            }
            finally
            {
                stopwatch.Stop();
                result.Elapsed = stopwatch.Elapsed;
                lock (_gate) { if (ReferenceEquals(_process, process)) _process = null; }
                process.Dispose();
            }
            return result;
        }

        private void Emit(string line, bool error)
        {
            if (line == null) return;
            Action<string, bool> handler = OutputReceived;
            if (handler != null)
            {
                try { handler(line, error); }
                catch { /* UI subscriber errors cannot crash an independent test process */ }
            }
        }

        private static void StopProcessTree(Process process)
        {
            try
            {
                if (process.HasExited) return;
                // ArchitectureSmoke can itself start isolated Foundation children.
                // Kill the caller-owned Windows process tree, not only the parent EXE.
                string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
                if (File.Exists(taskkill))
                {
                    using (Process killer = Process.Start(new ProcessStartInfo(taskkill,
                        "/PID " + process.Id + " /T /F") {
                            UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardOutput = false, RedirectStandardError = false
                        }))
                    {
                        if (killer != null && !killer.WaitForExit(5000))
                            try { killer.Kill(); } catch { }
                    }
                }
                if (!process.HasExited) process.Kill();
            }
            catch (InvalidOperationException) { }
            catch
            {
                try { if (!process.HasExited) process.Kill(); }
                catch { }
            }
        }
        public void Stop()
        {
            Process current;
            lock (_gate) { current = _process; }
            if (current != null) StopProcessTree(current);
        }
        public void Dispose()
        {
            lock (_gate) { _disposed = true; }
            Stop();
        }
    }
}
