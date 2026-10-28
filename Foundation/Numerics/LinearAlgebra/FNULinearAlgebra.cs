using System;
using MathNet.Numerics.LinearAlgebra;
namespace CxVisual.Foundation.Numerics {
    public sealed class FNULeastSquaresResult {
        private readonly double[] _coefficients;
        internal FNULeastSquaresResult(double[] x,int rows,int columns,int rank,double residual,double conditionNumber) {
            _coefficients=(double[])x.Clone(); Rows=rows;Columns=columns;Rank=rank;ResidualNorm=residual;ConditionNumber=conditionNumber;
        }
        public double[] Coefficients{get{return (double[])_coefficients.Clone();}}
        public int Rows{get;private set;}
        public int Columns{get;private set;}
        public int Rank{get;private set;}
        public double ResidualNorm{get;private set;}
        /// <summary>Singular value ratio; infinity denotes extreme conditioning.</summary>
        public double ConditionNumber{get;private set;}
    }
    /// <summary>通用三维点云PCA正交拟合结果，无专业长度单位假设。</summary>
    public sealed class FNUPcaPlaneResult {
        private readonly double[] _centroid,_normal;
        internal FNUPcaPlaneResult(double[] centroid,double[] normal,int count,
            double rms,double maximum,double inPlaneCondition,double planarity) {
            _centroid=(double[])centroid.Clone();_normal=(double[])normal.Clone();
            SampleCount=count;RmsPerpendicularResidual=rms;
            MaxAbsoluteResidual=maximum;InPlaneConditionNumber=inPlaneCondition;
            PlanarityRatio=planarity;
        }
        public double[] Centroid {get{return (double[])_centroid.Clone();}}
        public double[] Normal {get{return (double[])_normal.Clone();}}
        public int SampleCount {get;private set;}
        public double RmsPerpendicularResidual {get;private set;}
        public double MaxAbsoluteResidual {get;private set;}
        /// <summary>较大/较小切向奇异值；共线时明确拒绝而不伪报平面。</summary>
        public double InPlaneConditionNumber {get;private set;}
        /// <summary>法向/次切向奇异值，越小越接近平面。</summary>
        public double PlanarityRatio {get;private set;}
    }

    /// <summary>采用Math.NET 5.0 SVD求满列秩最小二乘；秩亏明确拒绝。</summary>
    public static class FNULinearAlgebra {
        private static bool Finite(double x){return !double.IsNaN(x)&&!double.IsInfinity(x);}
        /// <summary>
        /// Math.NET SVD求最小正交距离平面：输入Nx3任意坐标单位；
        /// 平面数据理论秩为2，不得复用要求满3列秩的LeastSquares。
        /// 源点均先去质心，拒绝零尺度与共线数据，返回方向规范化法向和残差。
        /// </summary>
        public static FNUPcaPlaneResult FitPlaneBySvd(double[,] xyz,
            double maxInPlaneCondition=1000000) {
            if(xyz==null)throw new ArgumentNullException("xyz");
            if(!Finite(maxInPlaneCondition)||maxInPlaneCondition<1)
                throw new ArgumentOutOfRangeException("maxInPlaneCondition");
            int n=xyz.GetLength(0);
            if(xyz.GetLength(1)!=3||n<3)
                throw new ArgumentException("PCA平面拟合必须输入至少3个三维点。","xyz");
            var centroid=new double[3];
            for(int col=0;col<3;col++) {
                double first=xyz[0,col],sum=0;
                if(!Finite(first))throw new ArgumentOutOfRangeException("xyz");
                for(int row=0;row<n;row++) {
                    double point=xyz[row,col];
                    if(!Finite(point))throw new ArgumentOutOfRangeException("xyz");
                    sum+=point-first;
                }
                centroid[col]=first+sum/n;
                if(!Finite(centroid[col]))throw new OverflowException("Plane centroid overflow");
            }
            var centered=new double[n,3];
            for(int row=0;row<n;row++)
                for(int col=0;col<3;col++)
                    centered[row,col]=xyz[row,col]-centroid[col];
            var svd=Matrix<double>.Build.DenseOfArray(centered).Svd(true);
            double large=svd.S[0],tangent=svd.S[1],small=svd.S[2];
            if(!Finite(large)||!Finite(tangent)||!Finite(small)||
               large<=0||tangent<=large*1e-12)
                throw new InvalidOperationException("Plane points degenerate: zero extent or collinear");
            double condition=large/tangent;
            if(!Finite(condition)||condition>maxInPlaneCondition)
                throw new InvalidOperationException("Plane in-plane SVD condition exceeds budget");
            var normal=new[]{svd.VT[2,0],svd.VT[2,1],svd.VT[2,2]};
            double length=Math.Sqrt(normal[0]*normal[0]+normal[1]*normal[1]+normal[2]*normal[2]);
            if(!Finite(length)||length<=0)
                throw new InvalidOperationException("Plane SVD normal invalid");
            for(int axis=0;axis<3;axis++)normal[axis]/=length;
            int principal=0;
            for(int axis=1;axis<3;axis++)
                if(Math.Abs(normal[axis])>Math.Abs(normal[principal]))principal=axis;
            if(normal[principal]<0)
                for(int axis=0;axis<3;axis++)normal[axis]=-normal[axis];
            double sumSq=0,maxAbs=0;
            for(int row=0;row<n;row++) {
                double distance=centered[row,0]*normal[0]+
                    centered[row,1]*normal[1]+centered[row,2]*normal[2];
                if(!Finite(distance))throw new OverflowException("Plane distance overflow");
                sumSq+=distance*distance;
                maxAbs=Math.Max(maxAbs,Math.Abs(distance));
            }
            double rms=Math.Sqrt(sumSq/n),planarity=small/tangent;
            if(!Finite(rms)||!Finite(planarity))
                throw new OverflowException("Plane quality computation overflow");
            return new FNUPcaPlaneResult(centroid,normal,n,rms,maxAbs,condition,planarity);
        }

        /// <summary>Backwards-compatible full-rank SVD least-squares solver without a condition limit.</summary>
        public static FNULeastSquaresResult LeastSquares(double[,] design,double[] observations) {
            return LeastSquares(design,observations,double.PositiveInfinity);
        }

        /// <summary>
        /// Solve a full-column-rank least-squares system by SVD. Set
        /// maxConditionNumber to reject numerically unstable but technically
        /// full-rank systems. Infinity keeps the legacy permissive behavior.
        /// </summary>
        public static FNULeastSquaresResult LeastSquares(double[,] design,double[] observations,
            double maxConditionNumber) {
            if(double.IsNaN(maxConditionNumber)||maxConditionNumber<1||
                double.IsNegativeInfinity(maxConditionNumber))
                throw new ArgumentOutOfRangeException("maxConditionNumber");
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
            double largest=0,smallest=double.PositiveInfinity;
            for(int i=0;i<svd.S.Count;i++){
                double singular=svd.S[i];
                if(!Finite(singular)||singular<=0)
                    throw new InvalidOperationException("SVD returned an invalid singular value.");
                largest=Math.Max(largest,singular);smallest=Math.Min(smallest,singular);
            }
            double condition=largest/smallest;
            if(condition>maxConditionNumber)
                throw new InvalidOperationException("Least-squares matrix exceeds the allowed condition number.");
            return new FNULeastSquaresResult(result,rows,cols,svd.Rank,norm,condition);
        }
        /// <summary>
        /// SVD ridge least squares: minimizes ||A*x-b||^2 + lambda*||x||^2.
        /// Lambda is in the squared units of the unscaled design matrix.
        /// Lambda must be finite and positive. Unlike strict LeastSquares,
        /// rank-deficient and underdetermined matrices are supported without normal equations.
        /// The reported ConditionNumber refers to the original design.
        /// </summary>
        public static FNULeastSquaresResult RidgeLeastSquares(
            double[,] design, double[] observations, double lambda)
        {
            if (!Finite(lambda) || lambda <= 0)
                throw new ArgumentOutOfRangeException("lambda");
            if (design == null) throw new ArgumentNullException("design");
            if (observations == null) throw new ArgumentNullException("observations");
            int rows = design.GetLength(0), cols = design.GetLength(1);
            if (rows == 0 || cols == 0 || observations.Length != rows)
                throw new ArgumentException("Invalid ridge least-squares shape");
            for (int row = 0; row < rows; ++row)
            {
                if (!Finite(observations[row])) throw new ArgumentOutOfRangeException("observations");
                for (int col = 0; col < cols; ++col)
                    if (!Finite(design[row, col])) throw new ArgumentOutOfRangeException("design");
            }
            Matrix<double> a = Matrix<double>.Build.DenseOfArray(design);
            Vector<double> b = Vector<double>.Build.DenseOfArray(observations);
            var svd = a.Svd(true);
            // x = V * diag(s/(s^2 + lambda)) * U^T * b.
            // Compute the filter without forming s^2 or the normal equations.
            var coefficients = new double[cols];
            double largest = 0, smallest = double.PositiveInfinity;
            for (int mode = 0; mode < svd.S.Count; ++mode)
            {
                double singular = svd.S[mode];
                if (!Finite(singular) || singular < 0)
                    throw new InvalidOperationException("SVD returned an invalid singular value.");
                largest = Math.Max(largest, singular);
                smallest = Math.Min(smallest, singular);
                if (singular == 0) continue;

                double filter;
                if (singular >= Math.Sqrt(lambda))
                    filter = 1.0 / (singular + lambda / singular);
                else
                    filter = (singular / lambda) / (1.0 + singular * (singular / lambda));
                double projection = 0;
                for (int row = 0; row < rows; ++row)
                    projection += svd.U[row, mode] * observations[row];
                double scaled = filter * projection;
                for (int col = 0; col < cols; ++col)
                    coefficients[col] += svd.VT[mode, col] * scaled;
            }
            for (int col = 0; col < cols; ++col)
                if (!Finite(coefficients[col])) throw new OverflowException("Ridge coefficient overflow.");
            Vector<double> fitted = a * Vector<double>.Build.DenseOfArray(coefficients);
            double norm = (fitted - b).L2Norm();
            if (!Finite(norm)) throw new OverflowException("Ridge residual overflow.");
            double condition = svd.Rank < cols || smallest == 0
                ? double.PositiveInfinity : largest / smallest;
            return new FNULeastSquaresResult(coefficients, rows, cols,
                svd.Rank, norm, condition);
        }
    }
}
