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
            FVOVolumeSpatialFrame frame, bool touchesBoundary,string sourceRef,long sourceRevision,
            int sampleStep=1,int maximumPoints=10000,Guid? resultId=null)
        {
            ResultId=resultId??Guid.NewGuid();
            Points=new ReadOnlyCollection<FVOPhysicalPoint3D>(points);
            Threshold=threshold;
            SourceRef=sourceRef;SourceRevision=sourceRevision;
            SpatialFrame=frame;
            TouchesVolumeBoundary=touchesBoundary;
            SampleStep=sampleStep;MaximumPoints=maximumPoints;
        }
        public Guid ResultId {get;private set;}
        public int SampleStep {get;private set;}
        public int MaximumPoints {get;private set;}
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
            string sourceRef,long sourceRevision,FVOVolumeSpatialFrame frame,
            Guid surfaceResultId,double maximumConditionNumber,Guid? resultId=null)
        {
            ResultId=resultId??Guid.NewGuid();SourceRef=sourceRef;SourceRevision=sourceRevision;
            Center=center;RadiusMm=radius;RmsResidualMm=rms;
            MaxAbsoluteResidualMm=maximum;ConditionNumber=condition;SampleCount=count;
            SpatialFrame=frame;SourceSurfaceResultId=surfaceResultId;MaximumConditionNumber=maximumConditionNumber;
        }
        /// <summary>算法结果稳定身份；工程重开保留，主动复算生成新身份。</summary>
        public Guid ResultId {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public Guid SourceSurfaceResultId {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public double MaximumConditionNumber {get;private set;}
        public string Unit {get{return "mm";}}
        public FVOPhysicalPoint3D Center {get;private set;}
        public double RadiusMm {get;private set;}
        public double DiameterMm {get{return RadiusMm*2;}}
        public double RmsResidualMm {get;private set;}
        public double MaxAbsoluteResidualMm {get;private set;}
        public double ConditionNumber {get;private set;}
        public int SampleCount {get;private set;}
        public string FitMethod {get{return "centered-algebraic-sphere-SVD";}}
    }

    /// <summary>已知物理轴向的圆柱侧壁拟合；轴方向是显式工装/ROI约束，不推断未知轴。</summary>
    public sealed class CT3DCylinderFitResult
    {
        internal CT3DCylinderFitResult(FVOPhysicalPoint3D axisPoint,FVOPhysicalPoint3D axisDirection,
            double radius,double rms,double maxResidual,double condition,int count,
            CT3DSurfaceSamples source,double limit,IReadOnlyList<int> selectedIndices=null,Guid? resultId=null)
        {
            ResultId=resultId??Guid.NewGuid();AxisPoint=axisPoint;AxisDirection=axisDirection;
            RadiusMm=radius;RmsResidualMm=rms;MaxAbsoluteResidualMm=maxResidual;
            ConditionNumber=condition;SampleCount=count;SourceSurfaceResultId=source.ResultId;
            SourceRef=source.SourceRef;SourceRevision=source.SourceRevision;
            SpatialFrame=source.SpatialFrame;MaximumConditionNumber=limit;
            var indices=new int[selectedIndices==null?0:selectedIndices.Count];
            for(int i=0;i<indices.Length;i++)indices[i]=selectedIndices[i];
            SelectedPointIndices=Array.AsReadOnly(indices);
        }
        public Guid ResultId {get;private set;}
        public Guid SourceSurfaceResultId {get;private set;}
        public string SourceRef {get;private set;}
        public long SourceRevision {get;private set;}
        public FVOVolumeSpatialFrame SpatialFrame {get;private set;}
        public FVOPhysicalPoint3D AxisPoint {get;private set;}
        public FVOPhysicalPoint3D AxisDirection {get;private set;}
        public double RadiusMm {get;private set;}
        public double DiameterMm {get{return RadiusMm*2;}}
        public double RmsResidualMm {get;private set;}
        public double MaxAbsoluteResidualMm {get;private set;}
        public double ConditionNumber {get;private set;}
        public double MaximumConditionNumber {get;private set;}
        public int SampleCount {get;private set;}
        public ReadOnlyCollection<int> SelectedPointIndices {get;private set;}
        public bool HasExplicitSidewallRoi {get{return SelectedPointIndices.Count!=0;}}
        public string Unit {get{return "mm";}}
        public string FitMethod {get{return "constrained-axis-circle-SVD";}}
        public bool HasEvaluatedUncertainty {get{return false;}}
    }

    /// <summary>固定轴向侧壁圆柱拟合；调用方必须排除端盖，避免其污染半径。</summary>
    public static class CT3DSurfaceCylinderAnalysis
    {
        public static CT3DCylinderFitResult FitCylinder(CT3DSurfaceSamples surface,
            FVOPhysicalPoint3D axisDirection,double maxConditionNumber=10000000,
            CancellationToken cancellation=default(CancellationToken))
        {
            return FitCylinder(surface,axisDirection,null,maxConditionNumber,cancellation);
        }

        /// <summary>显式ROI只包含圆柱侧壁点；输入索引按原Surface固定顺序保存重开。</summary>
        public static CT3DCylinderFitResult FitCylinder(CT3DSurfaceSamples surface,
            FVOPhysicalPoint3D axisDirection,IReadOnlyList<int> selectedPointIndices,
            double maxConditionNumber=10000000,CancellationToken cancellation=default(CancellationToken))
        {
            if(surface==null)throw new ArgumentNullException("surface");
            if(!surface.SpatialFrame.IsCalibrated||surface.TouchesVolumeBoundary)
                throw new InvalidOperationException("圆柱拟合要求已标定且未截断的侧壁采样。");
            if(surface.Count<6)throw new ArgumentException("圆柱侧壁采样不足。","surface");
            cancellation.ThrowIfCancellationRequested();
            double ax=axisDirection.X,ay=axisDirection.Y,az=axisDirection.Z;
            double length=Math.Sqrt(ax*ax+ay*ay+az*az);
            if(length<=1e-12||double.IsNaN(length)||double.IsInfinity(length))
                throw new ArgumentOutOfRangeException("axisDirection");
            ax/=length;ay/=length;az/=length;
            double tx=Math.Abs(ax)<0.8?1:0,ty=Math.Abs(ax)<0.8?0:1;
            double projection=tx*ax+ty*ay;
            tx-=projection*ax;ty-=projection*ay;
            double tlen=Math.Sqrt(tx*tx+ty*ty);
            tx/=tlen;ty/=tlen;
            double tz=0,ux=ay*tz-az*ty,uy=az*tx-ax*tz,uz=ax*ty-ay*tx;
            var selected=new List<int>();
            if(selectedPointIndices==null)
                for(int i=0;i<surface.Count;i++)selected.Add(i);
            else
            {
                if(selectedPointIndices.Count<6||selectedPointIndices.Count>surface.Count)
                    throw new ArgumentOutOfRangeException("selectedPointIndices");
                var unique=new HashSet<int>();
                foreach(int index in selectedPointIndices)
                {
                    if(index<0||index>=surface.Count)
                        throw new ArgumentOutOfRangeException("selectedPointIndices");
                    if(!unique.Add(index))
                        throw new ArgumentException("圆柱侧壁ROI点索引必须互异。","selectedPointIndices");
                    selected.Add(index);
                }
            }
            int n=selected.Count;
            double ox=0,oy=0,oz=0;
            foreach(int index in selected)
            {FVOPhysicalPoint3D p=surface.Points[index];ox+=p.X;oy+=p.Y;oz+=p.Z;}
            ox/=n;oy/=n;oz/=n;
            double[,] design=new double[n,3];double[] values=new double[n];
            for(int i=0;i<n;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                FVOPhysicalPoint3D p=surface.Points[selected[i]];
                double dx=p.X-ox,dy=p.Y-oy,dz=p.Z-oz;
                double x=dx*tx+dy*ty+dz*tz,y=dx*ux+dy*uy+dz*uz;
                design[i,0]=2*x;design[i,1]=2*y;design[i,2]=1;
                values[i]=x*x+y*y;
            }
            FNULeastSquaresResult fit=FNULinearAlgebra.LeastSquares(design,values,maxConditionNumber);
            double cx=fit.Coefficients[0],cy=fit.Coefficients[1];
            double rr=fit.Coefficients[2]+cx*cx+cy*cy;
            if(!(rr>0)||double.IsInfinity(rr)||double.IsNaN(rr))
                throw new InvalidOperationException("圆柱半径退化。");
            double radius=Math.Sqrt(rr),sum=0,max=0;
            for(int i=0;i<n;i++)
            {
                if((i&255)==0)cancellation.ThrowIfCancellationRequested();
                double x=design[i,0]*0.5-cx,y=design[i,1]*0.5-cy;
                double residual=Math.Sqrt(x*x+y*y)-radius;
                sum+=residual*residual;max=Math.Max(max,Math.Abs(residual));
            }
            cancellation.ThrowIfCancellationRequested();
            return new CT3DCylinderFitResult(
                new FVOPhysicalPoint3D(ox+cx*tx+cy*ux,oy+cx*ty+cy*uy,oz+cx*tz+cy*uz),
                new FVOPhysicalPoint3D(ax,ay,az),radius,Math.Sqrt(sum/n),max,
                fit.ConditionNumber,n,surface,maxConditionNumber,selectedPointIndices);
        }
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
                        if(CT3DRawScalarGuard.ReadFinite(source,x,y,z)<threshold)continue;
                        bool outer=x==0||y==0||z==0||
                            x==source.Width-1||y==source.Height-1||z==source.Depth-1;
                        bool edge=outer ||
                            CT3DRawScalarGuard.ReadFinite(source,x-1,y,z)<threshold ||
                            CT3DRawScalarGuard.ReadFinite(source,x+1,y,z)<threshold ||
                            CT3DRawScalarGuard.ReadFinite(source,x,y-1,z)<threshold ||
                            CT3DRawScalarGuard.ReadFinite(source,x,y+1,z)<threshold ||
                            CT3DRawScalarGuard.ReadFinite(source,x,y,z-1)<threshold ||
                            CT3DRawScalarGuard.ReadFinite(source,x,y,z+1)<threshold;
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
                sourceRef,sourceRevision,sampleStep,maxPoints);
        }

        /// <summary>
        /// 对经源标定的完整物体边界使用FNU SVD拟合球，拒绝退化/超条件数的结果。
        /// 只提供离散边界拟合结果，工业精度需标准件、亚体素策略及不确定度独立验证。
        /// </summary>
        public static CT3DSphereFitResult FitSphere(
            CT3DSurfaceSamples surface,double maxConditionNumber=10000000)
        {
            return FitSphere(surface,maxConditionNumber,null);
        }

        internal static CT3DSphereFitResult FitSphere(CT3DSurfaceSamples surface,
            double maxConditionNumber,Guid? resultId)
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
                surface.SourceRef,surface.SourceRevision,surface.SpatialFrame,
                surface.ResultId,maxConditionNumber,resultId);
        }
    }
}
