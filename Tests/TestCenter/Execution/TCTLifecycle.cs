using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.Execution
{
    public interface ITCTTestLifecycle
    {
        Task SetupAsync(TCTTestContext context);
        Task TeardownAsync(TCTTestContext context);
    }

    public sealed class TCTTestCleanupException : Exception
    {
        public TCTTestCleanupException(Exception inner)
            : base("Teardown failed; this attempt cannot be Passed", inner) { }
    }

    public static class TCTTestLifecycleExecutor
    {
        // Teardown is attempted even after setup/execute failure.
        // Cleanup error always invalidates a previously Passed attempt.
        public static void Execute(ITCTTestCase test, TCTTestContext context)
        {
            ITCTTestLifecycle lifecycle = test as ITCTTestLifecycle;
            Exception primary = null;
            try
            {
                if (lifecycle != null)
                {
                    Task setup = lifecycle.SetupAsync(context);
                    if (setup == null) throw new InvalidOperationException("SetupAsync returned null");
                    setup.GetAwaiter().GetResult();
                }
                context.ThrowIfCancelled();
                test.Execute(context);
            }
            catch (Exception ex) { primary = ex; }
            finally
            {
                if (lifecycle != null)
                {
                    try
                    {
                        Task teardown = lifecycle.TeardownAsync(context);
                        if (teardown == null) throw new InvalidOperationException("TeardownAsync returned null");
                        teardown.GetAwaiter().GetResult();
                    }
                    catch (Exception cleanupError)
                    {
                        context.Diagnostic("TCT.TEARDOWN", Models.TCTTestDiagnosticSeverity.Error,
                            "Teardown", cleanupError.ToString());
                        primary = primary == null ?
                            (Exception)new TCTTestCleanupException(cleanupError) :
                            new AggregateException(primary, new TCTTestCleanupException(cleanupError));
                    }
                }
            }
            if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    // Async test cases share the normal Catalog/Runner via a synchronous execution adapter.
    // The actual execution occurs on the worker's background thread, never the WPF Dispatcher.
    public sealed class TCTAsyncTestCaseAdapter : ITCTTestCase
    {
        private readonly ITCTAsyncTestCase _source;
        public TCTAsyncTestCaseAdapter(ITCTAsyncTestCase source)
        { _source = source ?? throw new ArgumentNullException("source"); }
        public TCTTestCaseDescriptor Descriptor { get { return _source.Descriptor; } }
        public void Execute(TCTTestContext context)
        {
            Task task = _source.ExecuteAsync(context);
            if (task == null) throw new InvalidOperationException("Async TestCase returned null");
            task.GetAwaiter().GetResult();
        }
    }

    // Sorted acquisition prevents deadlocks for multi-resource exclusive fixtures.
    public sealed class TCTResourceGate
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, SemaphoreSlim> _semaphores =
            new Dictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        public async Task<IDisposable> AcquireAsync(IEnumerable<string> keys, CancellationToken token)
        {
            if (keys == null) throw new ArgumentNullException("keys");
            string[] required = keys.Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            List<SemaphoreSlim> acquired = new List<SemaphoreSlim>();
            try
            {
                foreach (string key in required)
                {
                    SemaphoreSlim semaphore;
                    lock (_sync)
                    {
                        if (!_semaphores.TryGetValue(key, out semaphore))
                        {
                            semaphore = new SemaphoreSlim(1, 1);
                            _semaphores.Add(key, semaphore);
                        }
                    }
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                    acquired.Add(semaphore);
                }
                return new TCTResourceGateLease(acquired);
            }
            catch
            {
                for (int i = acquired.Count - 1; i >= 0; i--) acquired[i].Release();
                throw;
            }
        }

        private sealed class TCTResourceGateLease : IDisposable
        {
            private List<SemaphoreSlim> _held;
            public TCTResourceGateLease(List<SemaphoreSlim> held) { _held = held; }
            public void Dispose()
            {
                List<SemaphoreSlim> held = Interlocked.Exchange(ref _held, null);
                if (held == null) return;
                for (int i = held.Count - 1; i >= 0; i--) held[i].Release();
            }
        }
    }
}
