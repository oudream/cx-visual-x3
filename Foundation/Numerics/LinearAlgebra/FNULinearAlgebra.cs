using System;
using MathNet.Numerics.LinearAlgebra;
namespace CxVisual.Foundation.Numerics {
    public sealed class FNULeastSquaresResult {
        private readonly double[] _coefficients;
        internal FNULeastSquaresResult(double[] x,int rows,int columns,int rank,double residual) {
            _coefficients=(double[])x.Clone(); Rows=rows;Columns=columns;Rank=rank;ResidualNorm=residual;
        }
        public double[] Coefficients{get{return (double[])_coefficients.Clone();}}
        public int Rows{get;private set;}
        public int Columns{get;private set;}
        public int Rank{get;private set;}
        public double ResidualNorm{get;private set;}
    }
    /// <summary>采用Math.NET 5.0 SVD求满列秩最小二乘；秩亏明确拒绝。</summary>
    public static class FNULinearAlgebra {
        private static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
        public static FNULeastSquaresResult LeastSquares(double[,] design,double[] observations) {
            if(design==null)throw new ArgumentNullException("design");
            if(observations==null)throw new ArgumentNullException("observations");
            int rows=design.GetLength(0),cols=design.GetLength(1);
            if(rows==0||cols==0||rows<cols||observations.Length!=rows)
                throw new ArgumentException("Invalid least-squares shape");
            for(int i=0;i<rows;i++){
                if(!Finite(observations[i]))throw new ArgumentOutOfRangeException("observations");
                for(int j=0;j<cols;j++)
                    if(!Finite(design[i,j]))throw new ArgumentOutOfRangeException("design");
            }
            Matrix<double> a=Matrix<double>.Build.DenseOfArray(design);
            Vector<double> b=Vector<double>.Build.DenseOfArray(observations);
            var svd=a.Svd(true);
            if(svd.Rank<cols)throw new InvalidOperationException("Rank-deficient matrix");
            Vector<double> x=svd.Solve(b);
            double norm=(a*x-b).L2Norm();
            if(!Finite(norm))throw new OverflowException("Residual overflow");
            double[] result=x.ToArray();
            foreach(double v in result)if(!Finite(v))throw new OverflowException("Coefficient overflow");
            return new FNULeastSquaresResult(result,rows,cols,svd.Rank,norm);
        }
    }
}
