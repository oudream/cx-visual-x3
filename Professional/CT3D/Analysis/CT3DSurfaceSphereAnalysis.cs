using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using CxVisual.Foundation.Volume;
using CxVisual.Foundation.Numerics;

namespace CxVisual.Professional.CT3D
{
    /// <summary>
    /// 离散体素阈值边界采样结果（并非亚体素表面、Mesh或已验收CT计量面）。
    /// 原始空间坐标只由FVO给出，包含输入标定及边缘截断状态。
    /// </summary>
    public sealed class CT3DSurfaceSamples
    {
        internal CT3DSurfaceSamples(List<FVOPhysicalPoint3D> points, float threshold,
            FVOVolumeSpatialFrame frame, bool touchesBoundary,string sourceRef,long sourceRevision)
        {
            Points=new ReadOnlyCollection<FVOPhysicalPoint3D>(points);
            Threshold=threshold;
            SourceRef=sourceRef;SourceRevision=sourceRevision;
            SpatialFrame=frame;
            TouchesVolumeBoundary=touchesBoundary;
        }
        public ReadOnlyCollection<FVOPhysicalPoint3D> Points {get;private set;}
        public int Count {get{return Points.Count;}}
        public float Threshold {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public bool TouchesVolumeBoundary {get;private set;}
        public string SurfaceMethod {get{return "binary-voxel-center-6-neighbor";}}
    }

    /// <summary>拟合的球几何与质量证据；RMS是离散边界点到球面的毫米残差。</summary>
    public sealed class CT3DSphereFitResult
    {
        internal CT3DSphereFitResult(FVOPhysicalPoint3D center,double radius,
            double rms,double maximum,double condition,int count,
            string sourceRef,long sourceRevision)
        {
            ResultId=Guid.NewGuid();SourceRef=sourceRef;SourceRevision=sourceRevision;
            Center=center;RadiusMm=radius;RmsResidualMm=rms;
            MaxAbsoluteResidualMm=maximum;ConditionNumber=condition;SampleCount=count;
        }
        /// <summary>本次算法结果临时身份；保存到工程时由MEM统一登记资源。</summary>
        public Guid ResultId {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public FVOPhysicalPoint3D Center {get;private set;}
        public double RadiusMm {get;private set;}
        public double DiameterMm {get{return RadiusMm*2;}}
        public double RmsResidualMm {get;private set;}
        public double MaxAbsoluteResidualMm {get;private set;}
        public double ConditionNumber {get;private set;}
        public int SampleCount {get;private set;}
        public string FitMethod {get{return "centered-algebraic-sphere-SVD";}}
    }

    /// <summary>
    /// CT3D首个Headless专业纵切片：原始RAW→阈值边界点→数值球拟合。
    /// 不访问R8预览、WPF、OpenGL或私有Runtime，不自行实现第二套线性代数。
    /// </summary>
    public static class CT3DSurfaceSphereAnalysis
    {
        /// <summary>
        /// 每隔sampleStep个原始体素检测一次6邻域边界；达到预算会明确失败而非静默截断。
        /// 非法/不完整RAW、取消、数据边缘截断均可从结果或异常辨识。
        /// </summary>
        public static CT3DSurfaceSamples SampleBoundary(
            FVOOriginalScalarLease source,string sourceRef,long sourceRevision,
            float threshold,int sampleStep,int maxPoints,CancellationToken cancellationToken)
        {
            if(source==null)throw new ArgumentNullException("source");
            if(string.IsNullOrWhiteSpace(sourceRef))
                throw new ArgumentException("专业结果必须带源资产引用。","sourceRef");
            if(sourceRevision<0)throw new ArgumentOutOfRangeException("sourceRevision");
            if(float.IsNaN(threshold)||float.IsInfinity(threshold))
                throw new ArgumentOutOfRangeException("threshold");
            if(sampleStep<1)throw new ArgumentOutOfRangeException("sampleStep");
            if(maxPoints<6||maxPoints>10000)throw new ArgumentOutOfRangeException("maxPoints");
            List<FVOPhysicalPoint3D> points=new List<FVOPhysicalPoint3D>();
            bool touchesBoundary=false;
            for(int z=0;z<source.Depth;z+=sampleStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for(int y=0;y<source.Height;y+=sampleStep)
                {
                    if((y&31)==0)cancellationToken.ThrowIfCancellationRequested();
                    for(int x=0;x<source.Width;x+=sampleStep)
                    {
                        if(source.ReadScalar(x,y,z)<threshold)continue;
                        bool outer=x==0||y==0||z==0||
                            x==source.Width-1||y==source.Height-1||z==source.Depth-1;
                        bool edge=outer ||
                            source.ReadScalar(x-1,y,z)<threshold ||
                            source.ReadScalar(x+1,y,z)<threshold ||
                            source.ReadScalar(x,y-1,z)<threshold ||
                            source.ReadScalar(x,y+1,z)<threshold ||
                            source.ReadScalar(x,y,z-1)<threshold ||
                            source.ReadScalar(x,y,z+1)<threshold;
                        if(!edge)continue;
                        if(points.Count==maxPoints)
                            throw new InvalidOperationException(
                                "边界点数量超过预算；请提高采样步长或分区分析，禁止静默截断。");
                        points.Add(source.SpatialFrame.VoxelToPhysical(x,y,z));
                        if(outer)touchesBoundary=true;
                    }
                }
            }
            if(points.Count<6)
                throw new InvalidOperationException("有效表面边界点不足，无法进行可靠三维拟合。");
            return new CT3DSurfaceSamples(points,threshold,source.SpatialFrame,touchesBoundary,
                sourceRef,sourceRevision);
        }

        /// <summary>
        /// 对经源标定的完整物体边界使用FNU SVD拟合球，拒绝退化/超条件数的结果。
        /// 只提供离散边界拟合结果，工业精度需标准件、亚体素策略及不确定度独立验证。
        /// </summary>
        public static CT3DSphereFitResult FitSphere(
            CT3DSurfaceSamples surface,double maxConditionNumber=10000000)
        {
            if(surface==null)throw new ArgumentNullException("surface");
            if(!surface.SpatialFrame.IsCalibrated)
                throw new InvalidOperationException("源空间尚未标定，不能提交专业球体计量结果。");
            if(surface.TouchesVolumeBoundary)
                throw new InvalidOperationException("物体接触体数据边界，可能截断，禁止拟合成功。");
            if(surface.Count<6)throw new ArgumentException("球拟合至少需要6个边界点。","surface");
            int n=surface.Count;
            double ox=0,oy=0,oz=0;
            foreach(FVOPhysicalPoint3D p in surface.Points)
            {
                ox+=p.X;oy+=p.Y;oz+=p.Z;
            }
            ox/=n;oy/=n;oz/=n;
            double[,] design=new double[n,4];
            double[] values=new double[n];
            for(int i=0;i<n;i++)
            {
                FVOPhysicalPoint3D p=surface.Points[i];
                double x=p.X-ox,y=p.Y-oy,z=p.Z-oz;
                design[i,0]=2*x;design[i,1]=2*y;design[i,2]=2*z;design[i,3]=1;
                values[i]=x*x+y*y+z*z;
            }
            FNULeastSquaresResult fit=FNULinearAlgebra.LeastSquares(
                design,values,maxConditionNumber);
            double[] c=fit.Coefficients;
            double radiusSquared=c[3]+c[0]*c[0]+c[1]*c[1]+c[2]*c[2];
            if(radiusSquared<=0||double.IsNaN(radiusSquared)||double.IsInfinity(radiusSquared))
                throw new InvalidOperationException("球体拟合退化：半径平方不为有效正数。");
            double radius=Math.Sqrt(radiusSquared),sumSquared=0,max=0;
            for(int i=0;i<n;i++)
            {
                FVOPhysicalPoint3D p=surface.Points[i];
                double x=p.X-ox-c[0],y=p.Y-oy-c[1],z=p.Z-oz-c[2];
                double residual=Math.Sqrt(x*x+y*y+z*z)-radius;
                sumSquared+=residual*residual;
                max=Math.Max(max,Math.Abs(residual));
            }
            double rms=Math.Sqrt(sumSquared/n);
            return new CT3DSphereFitResult(
                new FVOPhysicalPoint3D(ox+c[0],oy+c[1],oz+c[2]),
                radius,rms,max,fit.ConditionNumber,n,
                surface.SourceRef,surface.SourceRevision);
        }
    }
}
