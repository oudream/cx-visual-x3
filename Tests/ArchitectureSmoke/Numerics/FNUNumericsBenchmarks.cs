using System;
using System.Diagnostics;
using CxVisual.Foundation.Numerics;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>Reproducible diagnostics, not timing pass/fail gates.</summary>
    internal static class FNUNumericsBenchmarks
    {
        private static double _checksum; // consumed after timing, not a volatile field

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
            var a = matrixA.AsView().Transpose(0,1);
            var b = matrixB.AsView();
            Measure("MatMulViewsInto(reused)", 10, delegate {
                FNUTensorMath.MatMulViewsInto(a,b,matrixOutput);
                return matrixOutput.Get(0,0);
            });
            Measure("MatMulViews(allocating)", 10, delegate {
                return FNUTensorMath.MatMulViews(a,b).Get(0,0);
            });
            var broadcastSamples = new double[height];
            for (int i = 0; i < broadcastSamples.Length; ++i)
                broadcastSamples[i] = (i % 13) / 13.0;
            var broadcastRow = new FNUTensorBuffer(new FNUTensorShape(height),
                broadcastSamples).AsView();
            var elementOutput = new FNUTensorBuffer(new FNUTensorShape(width,height));
            FNUTensorMath.AddViewsInto(view,broadcastRow,elementOutput);
            Measure("AddViewsInto(reused)", iterations, delegate {
                FNUTensorMath.AddViewsInto(view,broadcastRow,elementOutput);
                return elementOutput.Get(0,0);
            });
            Measure("AddViews(allocating)", iterations, delegate {
                return FNUTensorMath.AddViews(view,broadcastRow).Get(0,0);
            });

            var axisOutput = new FNUTensorBuffer(new FNUTensorShape(width));
            FNUTensorMath.ReduceSumViewInto(view,1,axisOutput);
            Measure("ReduceSumViewInto(reused)", iterations, delegate {
                FNUTensorMath.ReduceSumViewInto(view,1,axisOutput);
                return axisOutput.Get(0);
            });
            Measure("ReduceSumView(allocating)", iterations, delegate {
                return FNUTensorMath.ReduceSumView(view,1).Get(0);
            });
            // Compare the contiguous buffer API to the existing strided View
            // API on identical values and output dimensions. Materialization is
            // intentionally outside timed sections.
            var contiguous = view.CopyToBuffer();
            var contiguousRow = broadcastRow.CopyToBuffer();
            var contiguousOutput = new FNUTensorBuffer(new FNUTensorShape(width,height));
            FNUTensorMath.AddInto(contiguous,contiguousRow,contiguousOutput);
            Measure("AddInto(buffer cursor,reused)", iterations, delegate {
                FNUTensorMath.AddInto(contiguous,contiguousRow,contiguousOutput);
                return contiguousOutput.Get(0,0);
            });
            Measure("MultiplyInto(buffer cursor,reused)", iterations, delegate {
                FNUTensorMath.MultiplyInto(contiguous,contiguousRow,contiguousOutput);
                return contiguousOutput.Get(0,0);
            });
            var reduceOutput = new FNUTensorBuffer(new FNUTensorShape(width));
            FNUTensorMath.ReduceSumInto(contiguous,1,reduceOutput);
            Measure("ReduceSumInto(buffer reused)", iterations, delegate {
                FNUTensorMath.ReduceSumInto(contiguous,1,reduceOutput);
                return reduceOutput.Get(0);
            });
            Measure("ReduceSum(buffer allocated)", iterations, delegate {
                return FNUTensorMath.ReduceSum(contiguous,1).Get(0);
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
