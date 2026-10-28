using System;
using System.Diagnostics;
using CxVisual.Foundation.Numerics;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>Reproducible diagnostics, not timing pass/fail gates.</summary>
    internal static class FNUNumericsBenchmarks
    {
        private static volatile double _checksum;

        public static void Run()
        {
            const int width = 256, height = 256, iterations = 30;
            var values = new double[width * height];
            for (int i = 0; i < values.Length; ++i) values[i] = (i % 97) / 97.0;
            var input = new FNUTensorBuffer(new FNUTensorShape(height, width), values);
            var view = input.AsView().Transpose(0,1).Slice(0,0,width);
            for (int i = 0; i < 3; ++i) {
                _checksum = FNUTensorMath.SumView(view);
                _checksum = FlatReference(view);
            }
            Measure("SumView(cursor)", iterations, delegate { return FNUTensorMath.SumView(view); });
            Measure("SumView(GetFlat reference)", iterations, delegate { return FlatReference(view); });

            var matrixA = new FNUTensorBuffer(new FNUTensorShape(64,64));
            var matrixB = new FNUTensorBuffer(new FNUTensorShape(64,64));
            for (int row = 0; row < 64; ++row) {
                matrixA.Set(1, row, row); matrixB.Set(1, row, row);
            }
            var matrixOutput = new FNUTensorBuffer(new FNUTensorShape(64,64));
            var a = matrixA.AsView().Transpose(0,1), b = matrixB.AsView();
            Measure("MatMulViewsInto(reused)", 10, delegate {
                FNUTensorMath.MatMulViewsInto(a,b,matrixOutput);
                return matrixOutput.Get(0,0);
            });
            Measure("MatMulViews(allocating)", 10, delegate {
                return FNUTensorMath.MatMulViews(a,b).Get(0,0);
            });
            Console.WriteLine("PASS FNU benchmark diagnostics (timings are observations, not CI thresholds)");
        }

        private static double FlatReference(FNUTensorView view)
        {
            double total = 0, correction = 0;
            for (int i = 0; i < view.ElementCount; ++i) {
                double v = view.GetFlat(i), next = total + v;
                correction += Math.Abs(total) >= Math.Abs(v)
                    ? (total - next) + v : (v - next) + total;
                total = next;
            }
            return total + correction;
        }

        private static void Measure(string name, int iterations, Func<double> action)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            double last = 0;
            for (int i = 0; i < iterations; ++i) last = action();
            long ticks = Stopwatch.GetTimestamp() - start;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
            _checksum = last;
            double milliseconds = ticks * 1000.0 / Stopwatch.Frequency / iterations;
            Console.WriteLine("BENCH FNU " + name + " | ms/op=" +
                milliseconds.ToString("F4",System.Globalization.CultureInfo.InvariantCulture) +
                " | bytes/op=" + (bytes / (double)iterations).ToString("F1",
                    System.Globalization.CultureInfo.InvariantCulture) +
                " | checksum=" + last.ToString("G17",System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
