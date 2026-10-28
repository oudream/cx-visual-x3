using System;

namespace CxVisual.Foundation.Volume
{
    /// <summary>不依赖WPF/GPU的物理毫米坐标；double避免大体数据索引引入float精度损失。</summary>
    public struct FVOPhysicalPoint3D
    {
        public FVOPhysicalPoint3D(double x,double y,double z)
        {
            if(!FVOVolumeSpatialFrame.IsFinite(x) ||
               !FVOVolumeSpatialFrame.IsFinite(y) ||
               !FVOVolumeSpatialFrame.IsFinite(z))
                throw new ArgumentOutOfRangeException("x","物理坐标必须是有限数。");
            X=x;Y=y;Z=z;
        }
        public double X {get;private set;}
        public double Y {get;private set;}
        public double Z {get;private set;}
    }

    /// <summary>连续体素索引坐标；非毫米单位，可为亚体素小数。</summary>
    public struct FVOVoxelCoordinate3D
    {
        public FVOVoxelCoordinate3D(double x,double y,double z)
        {
            if(!FVOVolumeSpatialFrame.IsFinite(x) ||
               !FVOVolumeSpatialFrame.IsFinite(y) ||
               !FVOVolumeSpatialFrame.IsFinite(z))
                throw new ArgumentOutOfRangeException("x","索引坐标必须有限。");
            X=x;Y=y;Z=z;
        }
        public double X {get;private set;}
        public double Y {get;private set;}
        public double Z {get;private set;}
    }

    /// <summary>
    /// FVO原始Volume的VoxelCenter(i,j,k)到Physical(mm)的空间映射。
    /// 列向量约定：physical=Origin+Direction(row-major 3x3)*diag(Spacing)*index。
    /// index=(0,0,0)表示第一个体素中心；非源数据标定时只能用作显示几何。
    /// </summary>
    public sealed class FVOVolumeSpatialFrame
    {
        private readonly double[] _direction;
        public double OriginX {get;private set;}
        public double OriginY {get;private set;}
        public double OriginZ {get;private set;}
        public double SpacingX {get;private set;}
        public double SpacingY {get;private set;}
        public double SpacingZ {get;private set;}
        /// <summary>只有明确提供可信原点/方向/单位的源才可标记true。</summary>
        public bool IsCalibrated {get;private set;}
        public bool IsRightHanded {get;private set;}
        public string Unit {get{return "mm";}}

        public FVOVolumeSpatialFrame(double spacingX,double spacingY,double spacingZ,
            double originX,double originY,double originZ,double[] directionRowMajor,
            bool isCalibrated)
        {
            if(!IsFinite(spacingX)||!IsFinite(spacingY)||!IsFinite(spacingZ)||
               spacingX<=0||spacingY<=0||spacingZ<=0)
                throw new ArgumentOutOfRangeException("spacingX","Spacing(mm)必须有限且为正。");
            if(!IsFinite(originX)||!IsFinite(originY)||!IsFinite(originZ))
                throw new ArgumentOutOfRangeException("originX","物理原点必须为有限数。");
            if(directionRowMajor==null || directionRowMajor.Length!=9)
                throw new ArgumentException("方向矩阵必须是row-major 3x3。","directionRowMajor");
            double[] d=(double[])directionRowMajor.Clone();
            for(int i=0;i<9;i++)
                if(!IsFinite(d[i]))throw new ArgumentOutOfRangeException("directionRowMajor","方向矩阵包含非有限数。");
            const double tolerance=1e-6;
            for(int a=0;a<3;a++)
            {
                for(int b=0;b<3;b++)
                {
                    double dot=d[a]*d[b]+d[a+3]*d[b+3]+d[a+6]*d[b+6];
                    double expected=a==b?1.0:0.0;
                    if(Math.Abs(dot-expected)>tolerance)
                        throw new ArgumentException("Direction必须为正交单位方向矩阵。","directionRowMajor");
                }
            }
            double determinant=d[0]*(d[4]*d[8]-d[5]*d[7])
                -d[1]*(d[3]*d[8]-d[5]*d[6])
                +d[2]*(d[3]*d[7]-d[4]*d[6]);
            if(!IsFinite(determinant)||Math.Abs(Math.Abs(determinant)-1)>tolerance)
                throw new ArgumentException("方向矩阵行列式必须是+1或-1。","directionRowMajor");
            _direction=d;
            OriginX=originX;OriginY=originY;OriginZ=originZ;
            SpacingX=spacingX;SpacingY=spacingY;SpacingZ=spacingZ;
            IsCalibrated=isCalibrated;
            IsRightHanded=determinant>0;
        }

        public static FVOVolumeSpatialFrame UncalibratedAxisAligned(
            double spacingX,double spacingY,double spacingZ)
        {
            return new FVOVolumeSpatialFrame(spacingX,spacingY,spacingZ,
                0,0,0,new double[]{1,0,0,0,1,0,0,0,1},false);
        }

        /// <summary>返回副本，禁止通过外部修改数组破坏已验证的Source Frame。</summary>
        public double[] CopyDirectionRowMajor() {return (double[])_direction.Clone();}

        public FVOPhysicalPoint3D VoxelToPhysical(double x,double y,double z)
        {
            if(!IsFinite(x)||!IsFinite(y)||!IsFinite(z))
                throw new ArgumentOutOfRangeException("x","Voxel索引必须是有限数。");
            x*=SpacingX;y*=SpacingY;z*=SpacingZ;
            return new FVOPhysicalPoint3D(
                OriginX+_direction[0]*x+_direction[1]*y+_direction[2]*z,
                OriginY+_direction[3]*x+_direction[4]*y+_direction[5]*z,
                OriginZ+_direction[6]*x+_direction[7]*y+_direction[8]*z);
        }

        /// <summary>因Direction正交，其逆为转置；返回的浮点Voxel索引可用于亚体素采样。</summary>
        public FVOVoxelCoordinate3D PhysicalToVoxel(double x,double y,double z)
        {
            if(!IsFinite(x)||!IsFinite(y)||!IsFinite(z))
                throw new ArgumentOutOfRangeException("x","物理坐标必须是有限数。");
            double dx=x-OriginX,dy=y-OriginY,dz=z-OriginZ;
            return new FVOVoxelCoordinate3D(
                (_direction[0]*dx+_direction[3]*dy+_direction[6]*dz)/SpacingX,
                (_direction[1]*dx+_direction[4]*dy+_direction[7]*dz)/SpacingY,
                (_direction[2]*dx+_direction[5]*dy+_direction[8]*dz)/SpacingZ);
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value)&&!double.IsInfinity(value);
        }
    }
}
