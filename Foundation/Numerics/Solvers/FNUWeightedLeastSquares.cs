using System;
namespace CxVisual.Foundation.Numerics
{
    /// <summary>对每一行应用 sqrt(weight) 的加权最小二乘，复用 SVD；权重不允许负数。</summary>
    public static class FNUWeightedLeastSquares
    {
        public static FNULeastSquaresResult Solve(double[,] design, double[] observations, double[] weights)
        {
            if (design == null) throw new ArgumentNullException("design");
            if (observations == null) throw new ArgumentNullException("observations");
            if (weights == null) throw new ArgumentNullException("weights");
            int rows = design.GetLength(0), cols = design.GetLength(1);
            if (rows < cols || cols <= 0 || observations.Length != rows || weights.Length != rows)
                throw new ArgumentException("Weighted least squares input shape mismatch.");
            var weighted = new double[rows, cols];
            var right = new double[rows];
            for (int row = 0; row < rows; row++)
            {
                double weight = weights[row];
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                    throw new ArgumentOutOfRangeException("weights");
                double scale = Math.Sqrt(weight);
                right[row] = scale * observations[row];
                for (int col = 0; col < cols; col++)
                    weighted[row, col] = scale * design[row, col];
            }
            // Validation of finite transformed values and rank is delegated to the
            // same SVD implementation as unweighted least squares.
            return FNULinearAlgebra.LeastSquares(weighted, right);
        }
    }
}
