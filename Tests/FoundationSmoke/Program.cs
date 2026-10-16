using System;
using CxVisual.Foundation.Core;
using CxVisual.Foundation.Infrastructure;
using CxVisual.Foundation.Geometry;
namespace CxVisual.Tests.FoundationSmoke
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args != null && args.Length > 0 && args[0] == "--concurrency")
                {
                    FCRWorkerTests.Run();
                FCRSharedLeaseTests.Run();
                FCRSchedulingTests.Run();
                FCRDedicatedExecutorTests.Run();
                    Console.WriteLine("PASS F19 concurrency tests");
                    return 0;
                }
                CxStableId id = CxStableId.NewId();
                if (id.Value == Guid.Empty || !id.Equals(new CxStableId(id.Value)))
                    throw new InvalidOperationException("Identity value semantics failed");
                CxRect2D r = new CxRect2D(10, 20, 20, 30);
                if (!r.Contains(new CxPoint2D(15, 25)) || r.Contains(new CxPoint2D(50, 25)))
                    throw new InvalidOperationException("Hit testing failed");
                if (!r.Intersects(new CxRect2D(20, 25, 20, 20)))
                    throw new InvalidOperationException("Rectangle intersection failed");
                CxPoint2D t = new CxAffine2D(1, 0, 0, 1, 30, 40).Transform(new CxPoint2D(2, 4));
                if (t.X != 32 || t.Y != 44) throw new InvalidOperationException("Transform failed");
                try { CxNumericGuard.RequireFinite(double.NaN, "value");
                    throw new InvalidOperationException("NaN check was not enforced"); }
                catch (ArgumentOutOfRangeException) { }
                FLDMemoryLogSink logs = new FLDMemoryLogSink();
                logs.Write(new FLDLogRecord("Foundation", "Smoke", "Checked"));
                if (logs.Count != 1 || logs.Get(0).Category != "Foundation")
                    throw new InvalidOperationException("Logger category lost");
                LoggingAndMonitoringTests.Run();
                FCRWorkerTests.Run();
                FCRSharedLeaseTests.Run();
                FCRSchedulingTests.Run();
                FCRDedicatedExecutorTests.Run();
                Console.WriteLine("PASS Foundation: Identity, Geometry, Logging, Rolling, Monitoring");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL Foundation: " + ex); return 1; }
        }
    }
}
