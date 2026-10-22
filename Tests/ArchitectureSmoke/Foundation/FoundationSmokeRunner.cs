using System;
using CxVisual.Foundation.Core;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Logging.Sinks;
using CxVisual.Foundation.Geometry;
namespace CxVisual.Tests.FoundationSmoke
{
    internal static class FoundationSmokeRunner
    {
        internal static int Run(string[] args)
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
                FOIStableId id = FOIStableId.NewId();
                FOIStableId parsed;
                if (id.Value == Guid.Empty ||
                    !id.Equals(new FOIStableId(id.Value)) ||
                    !FOIStableId.TryParse(id.ToString(), out parsed) ||
                    !parsed.Equals(id))
                    throw new InvalidOperationException("Identity value semantics failed");
                if (FOIStableId.TryParse(Guid.Empty.ToString(), out parsed))
                    throw new InvalidOperationException("Empty identity was accepted");
                try { new FOIStableId(Guid.Empty); throw new InvalidOperationException("Empty identity was accepted"); }
                catch (ArgumentException) { }
                try { var invalidId = default(FOIStableId).Value; throw new InvalidOperationException("Uninitialized identity was accepted"); }
                catch (InvalidOperationException ex) { if (ex.Message != "Uninitialized identity") throw; }
                FGERect2D r = new FGERect2D(10, 20, 20, 30);
                if (!r.Contains(new FGEPoint2D(15, 25)) || r.Contains(new FGEPoint2D(50, 25)))
                    throw new InvalidOperationException("Hit testing failed");
                if (!r.Intersects(new FGERect2D(20, 25, 20, 20)))
                    throw new InvalidOperationException("Rectangle intersection failed");
                FGEPoint2D t = new FGETransform2D(1, 0, 0, 1, 30, 40).Transform(new FGEPoint2D(2, 4));
                if (t.X != 32 || t.Y != 44) throw new InvalidOperationException("Transform failed");
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
