using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Cases;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Execution;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTWorkerFaultCases
    {
        [TCTTest("TCT.M1.WORKER.PROBE", "受控Worker故障探针", TCTTestAspect.Foundation,
            "TestFramework", ObjectId = "TCTTestHostController")]
        public static void WorkerProbe(TCTTestContext ctx)
        {
            // Normal regression remains a real, passing assertion.
            string mode = Environment.GetEnvironmentVariable("TCT_M1_WORKER_FAULT");
            if (mode == "hang") Thread.Sleep(10000);
            if (mode == "tree")
            {
                string pidFile = Environment.GetEnvironmentVariable("TCT_M1_WORKER_PIDFILE");
                if (string.IsNullOrWhiteSpace(pidFile))
                    throw new InvalidOperationException("Tree probe needs output PID file");
                using (Process child = Process.Start(new ProcessStartInfo("cmd.exe",
                    "/c ping -n 45 127.0.0.1 >nul")
                { UseShellExecute = false, CreateNoWindow = true }))
                {
                    File.WriteAllText(pidFile, child.Id.ToString(CultureInfo.InvariantCulture));
                    Thread.Sleep(30000);
                }
            }
            if (mode == "exit") Environment.Exit(23);
            ctx.AssertTrue(true, "Worker normal result", true, true);
        }

        [TCTTest("TCT.M1.WORKER.CANCEL", "运行中的Worker可取消且不能标通过",
            TCTTestAspect.Foundation, "TestFramework", ObjectId = "TCTTestHostController")]
        public static void RunningWorkerCancellation(TCTTestContext ctx)
        {
            ITCTTestCase probe = TCTRegistration.CreateCatalog().Cases.Single(
                x => x.Descriptor.Id == "TCT.M1.WORKER.PROBE");
            TCTTestAttempt attempt = new TCTTestAttempt(
                Guid.NewGuid().ToString("N"), probe.Descriptor.Id, 0, 123);
            string saved = Environment.GetEnvironmentVariable("TCT_M1_WORKER_FAULT");
            Environment.SetEnvironmentVariable("TCT_M1_WORKER_FAULT", "hang");
            try
            {
                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    var started = new TaskCompletionSource<bool>();
                    Task<TCTTestCaseResult> running = new TCTTestHostController().RunIsolatedAsync(
                        probe, TimeSpan.FromSeconds(15), 123, cancel.Token, attempt,
                        e => { if (e.Kind == TCTTestEventKind.Started)
                            started.TrySetResult(true); });
                    Task seen = Task.WhenAny(started.Task, Task.Delay(5000)).GetAwaiter().GetResult();
                    ctx.AssertTrue(seen == started.Task,
                        "收到真实Worker开始事件后再取消", true, seen == started.Task);
                    cancel.Cancel();
                    TCTTestCaseResult result = running.GetAwaiter().GetResult();
                    ctx.AssertEqual(TCTTestStatus.Cancelled, result.Status,
                        "执行中的Worker终态是Cancelled");
                    ctx.AssertTrue(result.Status != TCTTestStatus.Passed,
                        "取消不会假绿", false, false);
                }
            }
            finally { Environment.SetEnvironmentVariable("TCT_M1_WORKER_FAULT", saved); }
        }
    }
}
