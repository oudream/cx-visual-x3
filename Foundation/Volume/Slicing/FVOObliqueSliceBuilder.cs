using System;
using System.Threading;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Foundation.Volume
{
    /// <summary>
    /// 原始体素坐标的任意切数值采样；与三维空间切面共用FVOObliqueSliceGeometry。
    /// 线性三轴插值、高位深Float32输出与BGR8显示分离，不依赖WPF/OpenGL/CT工艺。
    /// </summary>
    public static class FVOObliqueSliceBuilder
    {
        public static FIMImageBuffer BuildPreview(FVOVolumeData volume,
            FVOObliqueSliceDefinition definition, bool pseudoColor,
            byte windowLow, byte windowHigh, CancellationToken cancellation)
        {
            Validate(volume,definition);
            int w=definition.Width,h=definition.Height;
            byte[] output=new byte[checked(w*h*3)];
            FVOObliqueSliceVector u,v;
            FVOObliqueSliceGeometry.GetAxes(definition,out u,out v);
            double step=definition.PixelSpacingInVoxels;
            FVOObliqueSliceVector topLeft=FVOObliqueSliceGeometry.GetCorner(definition,u,v,-1,-1);
            for(int row=0;row<h;row++)
            {
                cancellation.ThrowIfCancellationRequested();
                double dx=topLeft.X+v.X*row*step;
                double dy=topLeft.Y+v.Y*row*step;
                double dz=topLeft.Z+v.Z*row*step;
                for(int col=0;col<w;col++)
                {
                    double x=dx+u.X*col*step;
                    double y=dy+u.Y*col*step;
                    double z=dz+u.Z*col*step;
                    byte density=(byte)Math.Round(Sample(volume,x,y,z,false));
                    byte gray=FVOSliceImageBuilder.ApplyWindow(density,windowLow,windowHigh);
                    int p=(row*w+col)*3;
                    if(pseudoColor) FVOSliceImageBuilder.WritePseudoBgr(output,p,gray);
                    else output[p]=output[p+1]=output[p+2]=gray;
                }
            }
            return new FIMImageBuffer(w,h,FIMPixelFormat.Bgr24,output);
        }

        /// <summary>不做灰度窗和8bit量化；原16/32位源缓存存在时直接三线性采样原标量。</summary>
        public static FIMImageBuffer BuildScalar(FVOVolumeData volume,
            FVOObliqueSliceDefinition definition,CancellationToken cancellation)
        {
            Validate(volume,definition);
            int w=definition.Width,h=definition.Height;
            byte[] output=new byte[checked(w*h*sizeof(float))];
            FVOObliqueSliceVector u,v;
            FVOObliqueSliceGeometry.GetAxes(definition,out u,out v);
            double step=definition.PixelSpacingInVoxels;
            FVOObliqueSliceVector topLeft=FVOObliqueSliceGeometry.GetCorner(definition,u,v,-1,-1);
            for(int row=0;row<h;row++)
            {
                cancellation.ThrowIfCancellationRequested();
                double dx=topLeft.X+v.X*row*step;
                double dy=topLeft.Y+v.Y*row*step;
                double dz=topLeft.Z+v.Z*row*step;
                for(int col=0;col<w;col++)
                {
                    double scalar=Sample(volume,
                        dx+u.X*col*step,dy+u.Y*col*step,dz+u.Z*col*step,true);
                    byte[] value=BitConverter.GetBytes((float)scalar);
                    Buffer.BlockCopy(value,0,output,(row*w+col)*sizeof(float),sizeof(float));
                }
            }
            return new FIMImageBuffer(w,h,FIMPixelFormat.GrayFloat32,output);
        }

        private static void Validate(FVOVolumeData data,FVOObliqueSliceDefinition plane)
        {
            if(data==null || plane==null) throw new ArgumentNullException("data/plane");
            if(plane.Width<=0 || plane.Height<=0 ||
               plane.Width>8192 || plane.Height>8192 ||
               (long)plane.Width*plane.Height>16L*1024*1024)
                throw new ArgumentOutOfRangeException("plane","任意切像素尺寸非法或超过16M上限。");
            if(!(plane.PixelSpacingInVoxels>0) ||
                double.IsNaN(plane.PixelSpacingInVoxels) ||
                double.IsInfinity(plane.PixelSpacingInVoxels))
                throw new ArgumentOutOfRangeException("plane","任意切像素步长必须是有限正数。");
            double[] numbers={plane.CenterX,plane.CenterY,plane.CenterZ,
                plane.AngleXDegrees,plane.AngleYDegrees,plane.AngleZDegrees};
            foreach(double v in numbers)
                if(double.IsNaN(v)||double.IsInfinity(v))
                    throw new ArgumentOutOfRangeException("plane","任意切位置和角度必须有限。");
        }

        private static double Sample(FVOVolumeData volume,double x,double y,double z,bool original)
        {
            if(x<0 || y<0 || z<0 || x>volume.OriginalWidth-1 ||
                y>volume.OriginalHeight-1 || z>volume.OriginalDepth-1)return 0;
            int x0=(int)x,y0=(int)y,z0=(int)z;
            int x1=Math.Min(x0+1,volume.OriginalWidth-1);
            int y1=Math.Min(y0+1,volume.OriginalHeight-1);
            int z1=Math.Min(z0+1,volume.OriginalDepth-1);
            double fx=x-x0,fy=y-y0,fz=z-z0;
            double v000=Read(volume,x0,y0,z0,original);
            double v100=Read(volume,x1,y0,z0,original);
            double v010=Read(volume,x0,y1,z0,original);
            double v110=Read(volume,x1,y1,z0,original);
            double v001=Read(volume,x0,y0,z1,original);
            double v101=Read(volume,x1,y0,z1,original);
            double v011=Read(volume,x0,y1,z1,original);
            double v111=Read(volume,x1,y1,z1,original);
            double a=v000+(v100-v000)*fx,b=v010+(v110-v010)*fx;
            double c=v001+(v101-v001)*fx,d=v011+(v111-v011)*fx;
            return (a+(b-a)*fy)*(1-fz)+(c+(d-c)*fy)*fz;
        }
        private static double Read(FVOVolumeData volume,int x,int y,int z,bool original)
        {
            return original ? volume.GetScalarByOriginalIndex(x,y,z):
                volume.GetVoxelByOriginalIndex(x,y,z);
        }
    }
}
