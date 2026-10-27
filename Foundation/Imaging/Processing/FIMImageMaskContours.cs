using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// Gray8二值掩码的像素边界闭环，坐标在原图像素格点边缘。
    /// 返回所有独立闭环（含孔洞），正向为外环、负向为孔洞（Y向下坐标）。
    /// 只描述真实边界，不以组件外包框或有孔多边形冒充面积ROI。
    /// </summary>
    public static class FIMImageMaskContours
    {
        public static IReadOnlyList<IReadOnlyList<FGEPoint2D>> Trace(
            FIMImageBuffer mask,int maxBoundaryEdges=100000,
            CancellationToken cancellationToken=default(CancellationToken))
        {
            if(mask==null)throw new ArgumentNullException("mask");
            if(mask.PixelFormat!=FIMPixelFormat.Gray8)
                throw new ArgumentException("轮廓追踪仅接受Gray8二值掩码。","mask");
            if(maxBoundaryEdges<4 || maxBoundaryEdges>2000000)
                throw new ArgumentOutOfRangeException("maxBoundaryEdges");
            cancellationToken.ThrowIfCancellationRequested();
            int w=mask.Width,h=mask.Height,stride=w+1;
            HashSet<long> edges=new HashSet<long>();
            for(int y=0;y<h;y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for(int x=0;x<w;x++)
                {
                    if(mask.GetGrayValue(x,y)==0)continue;
                    if(y==0 || mask.GetGrayValue(x,y-1)==0)
                        edges.Add(Key(x,y,0,stride));
                    if(x==w-1 || mask.GetGrayValue(x+1,y)==0)
                        edges.Add(Key(x+1,y,1,stride));
                    if(y==h-1 || mask.GetGrayValue(x,y+1)==0)
                        edges.Add(Key(x+1,y+1,2,stride));
                    if(x==0 || mask.GetGrayValue(x-1,y)==0)
                        edges.Add(Key(x,y+1,3,stride));
                    if(edges.Count>maxBoundaryEdges)
                        throw new InvalidOperationException(
                            "掩码边界超过可编辑图元上限，请保留掩码并缩小ROI或调整阈值。");
                }
            }
            List<IReadOnlyList<FGEPoint2D>> contours=new List<IReadOnlyList<FGEPoint2D>>();
            while(edges.Count>0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long first=edges.First();
                long edge=first;
                List<FGEPoint2D> raw=new List<FGEPoint2D>();
                do
                {
                    if(!edges.Remove(edge))
                        throw new InvalidOperationException("像素边界存在断裂或重边。");
                    long vertex=edge/4;
                    int dir=(int)(edge%4);
                    int x=(int)(vertex%stride),y=(int)(vertex/stride);
                    raw.Add(new FGEPoint2D(x,y));
                    if(dir==0)x++;
                    else if(dir==1)y++;
                    else if(dir==2)x--;
                    else y--;
                    // 在仅角接触处优先右转，分别追踪两块区域，不误合并为自交多边形。
                    int[] order={(dir+1)%4,dir,(dir+3)%4,(dir+2)%4};
                    long next=-1;
                    foreach(int candidate in order)
                    {
                        long key=Key(x,y,candidate,stride);
                        if(key==first || edges.Contains(key))
                        { next=key;break; }
                    }
                    if(next<0)
                        throw new InvalidOperationException("掩码轮廓没有可闭合的后继边。");
                    edge=next;
                    if((raw.Count&1023)==0)
                        cancellationToken.ThrowIfCancellationRequested();
                    if(raw.Count>maxBoundaryEdges)
                        throw new InvalidOperationException("单一轮廓过大，不能输出可编辑多边形。");
                }while(edge!=first);

                List<FGEPoint2D> corners=new List<FGEPoint2D>();
                for(int i=0;i<raw.Count;i++)
                {
                    FGEPoint2D a=raw[(i+raw.Count-1)%raw.Count];
                    FGEPoint2D b=raw[i];
                    FGEPoint2D c=raw[(i+1)%raw.Count];
                    double turn=(b.X-a.X)*(c.Y-b.Y)-(b.Y-a.Y)*(c.X-b.X);
                    if(turn!=0)corners.Add(b);
                }
                if(corners.Count<3)
                    throw new InvalidOperationException("掩码轮廓不能构成闭合区域。");
                contours.Add(corners.AsReadOnly());
            }
            return contours.AsReadOnly();
        }

        public static double SignedArea(IReadOnlyList<FGEPoint2D> points)
        {
            if(points==null)throw new ArgumentNullException("points");
            if(points.Count<3)throw new ArgumentException("轮廓点数不足。","points");
            double twice=0;
            for(int i=0;i<points.Count;i++)
            {
                FGEPoint2D a=points[i],b=points[(i+1)%points.Count];
                twice+=a.X*b.Y-b.X*a.Y;
            }
            return twice/2;
        }

        private static long Key(int x,int y,int dir,int stride)
        { return ((long)y*stride+x)*4+dir; }
    }
}
