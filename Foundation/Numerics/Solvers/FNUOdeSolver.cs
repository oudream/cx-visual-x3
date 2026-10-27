using System;
using System.Threading;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>标量ODE采样轨迹；输出数组防御性拷贝。</summary>
    public sealed class FNUOdeTrajectory
    {
        private readonly double[] _times, _values;
        internal FNUOdeTrajectory(double[] times, double[] values)
        {
            _times = times; _values = values;
        }
        public int Count { get { return _times.Length; } }
        public double TimeAt(int index) { return _times[index]; }
        public double ValueAt(int index) { return _values[index]; }
        public double[] CopyValues() { return (double[])_values.Clone(); }
        public double[] CopyTimes() { return (double[])_times.Clone(); }
    }

    public static class FNUOdeSolver
    {
        /// <summary>定步长经典RK4求解 y'(t)=f(t,y)。步长可正可负，不是自适应刚性ODE求解器。</summary>
        public static FNUOdeTrajectory RungeKutta4(Func<double, double, double> derivative,
            double startTime, double initialValue, double step, int steps,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (derivative == null) throw new ArgumentNullException("derivative");
            if (!Finite(startTime) || !Finite(initialValue) || !Finite(step) || step == 0)
                throw new ArgumentOutOfRangeException("step");
            if (steps < 1 || steps > 1000000) throw new ArgumentOutOfRangeException("steps");
            var times = new double[steps + 1];
            var values = new double[steps + 1];
            times[0] = startTime; values[0] = initialValue;
            for (int i = 0; i < steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double t = times[i], y = values[i];
                double midpoint = Checked(t + step / 2);
                double end = Checked(t + step);
                double k1 = Checked(derivative(t, y));
                double k2 = Checked(derivative(midpoint, Checked(y + step * k1 / 2)));
                double k3 = Checked(derivative(midpoint, Checked(y + step * k2 / 2)));
                double k4 = Checked(derivative(end, Checked(y + step * k3)));
                times[i + 1] = end;
                values[i + 1] = Checked(y + step * (k1 + 2*k2 + 2*k3 + k4) / 6);
            }
            return new FNUOdeTrajectory(times, values);
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static double Checked(double x)
        {
            if (!Finite(x)) throw new OverflowException("ODE produced a non-finite intermediate value.");
            return x;
        }
    }
}
