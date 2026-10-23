using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 正交障碍避让：世界空间AABB，固定世界间距，有限Hanan网格。
    /// 只计算场景投影，不产生第二套工程连接或持久化路径。
    /// </summary>
    internal static class MVIOrthogonalRouter
    {
        // 世界空间安全边距，和缩放比例无关；UI可以另行提供屏幕像素缩放。
        private const double Clearance=8.0;
        // 每增加一次水平/竖直转弯的代价，优先减少过密折角。
        private const double BendCost=12.0;
        private const double Epsilon=1e-7;
        private const int MaxRoutingObstacles=32;
        // 防止在鼠标拖动/场景重投影时构造无限大的搜索空间。
        private const int MaxGridNodes=20000;

        /// <summary>稳定优先队列元素；序列号保证等代价情况下选路可重复。</summary>
        private sealed class QueueItem : IComparable<QueueItem>
        {
            internal double Cost;
            internal int State;
            internal long Sequence;
            public int CompareTo(QueueItem other)
            {
                int compare=Cost.CompareTo(other.Cost);
                if(compare!=0)return compare;
                compare=State.CompareTo(other.State);
                return compare!=0?compare:Sequence.CompareTo(other.Sequence);
            }
        }

        internal static IReadOnlyList<FGEPoint2D> Route(
            FGEPoint2D start,FGEPoint2D end,IEnumerable<FGERect2D> obstacles)
        {
            if(obstacles==null)throw new ArgumentNullException("obstacles");
            var rects=new List<FGERect2D>();
            foreach(FGERect2D bounds in obstacles)
            {
                if(bounds.Width<=Epsilon||bounds.Height<=Epsilon)continue;
                FGERect2D rect=bounds.Inflate(Clearance);
                // 端点已经落在别的图元内部时，无法保证外部避障；不把自身封死。
                if(Inside(rect,start)||Inside(rect,end))continue;
                rects.Add(rect);
            }
            if(Math.Abs(start.X-end.X)<=Epsilon||
                Math.Abs(start.Y-end.Y)<=Epsilon)
            {
                var direct=new[]{start,end};
                if(Clear(direct,rects))return direct;
            }
            // 先用原三段路径，避免未受阻的工程打开后视觉路径无故变化。
            double midX=(start.X+end.X)/2;
            var center=Compact(new[]{
                start,new FGEPoint2D(midX,start.Y),
                new FGEPoint2D(midX,end.Y),end});
            if(Clear(center,rects))return center;
            // 如果原中心通道被挡住，再求低转弯数、短距离的正交路径。
            IReadOnlyList<FGEPoint2D> detour=Find(start,end,rects);
            return detour??center;
        }

        private static bool Inside(FGERect2D r,FGEPoint2D p)
        {return p.X>r.Left+Epsilon&&p.X<r.Right-Epsilon&&
                p.Y>r.Top+Epsilon&&p.Y<r.Bottom-Epsilon;}

        /// <summary>仅接受正交线段；障碍膨胀后的边界允许贴边通过。</summary>
        private static bool SegmentClear(FGEPoint2D a,FGEPoint2D b,
            IList<FGERect2D> obstacles)
        {
            if(Math.Abs(a.Y-b.Y)<=Epsilon)
            {
                double x0=Math.Min(a.X,b.X),x1=Math.Max(a.X,b.X);
                foreach(FGERect2D r in obstacles)
                    if(a.Y>r.Top+Epsilon&&a.Y<r.Bottom-Epsilon&&
                        x0<r.Right-Epsilon&&x1>r.Left+Epsilon)return false;
                return true;
            }
            if(Math.Abs(a.X-b.X)<=Epsilon)
            {
                double y0=Math.Min(a.Y,b.Y),y1=Math.Max(a.Y,b.Y);
                foreach(FGERect2D r in obstacles)
                    if(a.X>r.Left+Epsilon&&a.X<r.Right-Epsilon&&
                        y0<r.Bottom-Epsilon&&y1>r.Top+Epsilon)return false;
                return true;
            }
            return false;
        }

        private static bool Clear(IReadOnlyList<FGEPoint2D> points,
            IList<FGERect2D> obstacles)
        {
            for(int i=1;i<points.Count;i++)
                if(!SegmentClear(points[i-1],points[i],obstacles))return false;
            return true;
        }

        /// <summary>删除重复顶点及共线中间点，不改变几何轨迹。</summary>
        private static IReadOnlyList<FGEPoint2D> Compact(IEnumerable<FGEPoint2D> points)
        {
            var result=new List<FGEPoint2D>();
            foreach(FGEPoint2D p in points)
            {
                if(result.Count>0&&p.DistanceTo(result[result.Count-1])<=Epsilon)continue;
                while(result.Count>=2)
                {
                    FGEPoint2D a=result[result.Count-2],b=result[result.Count-1];
                    bool horizontal=Math.Abs(a.Y-b.Y)<=Epsilon&&
                        Math.Abs(b.Y-p.Y)<=Epsilon;
                    bool vertical=Math.Abs(a.X-b.X)<=Epsilon&&
                        Math.Abs(b.X-p.X)<=Epsilon;
                    if(!horizontal&&!vertical)break;
                    result.RemoveAt(result.Count-1);
                }
                result.Add(p);
            }
            if(result.Count<2)result.Add(result[0]);
            return result.AsReadOnly();
        }

        private static double DistanceToCorridor(FGERect2D r,
            FGEPoint2D a,FGEPoint2D b)
        {
            double left=Math.Min(a.X,b.X),right=Math.Max(a.X,b.X);
            double top=Math.Min(a.Y,b.Y),bottom=Math.Max(a.Y,b.Y);
            double dx=Math.Max(0,Math.Max(left-r.Right,r.Left-right));
            double dy=Math.Max(0,Math.Max(top-r.Bottom,r.Top-bottom));
            return dx+dy;
        }

        private static IReadOnlyList<FGEPoint2D> Find(FGEPoint2D start,
            FGEPoint2D end,List<FGERect2D> all)
        {
            // 明确有界：避免复杂工程中每一帧因过量网格路由而冻结UI。
            var sorted=all.OrderBy(r=>DistanceToCorridor(r,start,end))
                .ThenBy(r=>r.Left).ThenBy(r=>r.Top).ToList();
            var active=sorted.Take(MaxRoutingObstacles).ToList();
            var xs=new SortedSet<double>{start.X,end.X,(start.X+end.X)/2};
            var ys=new SortedSet<double>{start.Y,end.Y,(start.Y+end.Y)/2};
            foreach(FGERect2D r in active)
            {
                xs.Add(r.Left);xs.Add(r.Right);
                ys.Add(r.Top);ys.Add(r.Bottom);
            }
            double[] x=xs.ToArray(),y=ys.ToArray();
            int nx=x.Length,ny=y.Length;
            if(nx*ny>MaxGridNodes)return null;
            int startX=Array.BinarySearch(x,start.X),startY=Array.BinarySearch(y,start.Y);
            int endX=Array.BinarySearch(x,end.X),endY=Array.BinarySearch(y,end.Y);
            if(startX<0||startY<0||endX<0||endY<0)return null;
            int startNode=startY*nx+startX,endNode=endY*nx+endX;
            int size=nx*ny*2;
            var costs=new double[size];
            var parents=new int[size];
            for(int i=0;i<size;i++){costs[i]=double.PositiveInfinity;parents[i]=-1;}
            var queue=new SortedSet<QueueItem>();
            long sequence=0;
            for(int direction=0;direction<2;direction++)
            {
                int state=startNode*2+direction;
                costs[state]=0;
                queue.Add(new QueueItem{Cost=0,State=state,Sequence=sequence++});
            }
            int reached=-1;
            while(queue.Count>0)
            {
                QueueItem step=queue.Min;queue.Remove(step);
                int state=step.State,node=state/2,previousDirection=state%2;
                if(step.Cost>costs[state]+Epsilon)continue;
                if(node==endNode){reached=state;break;}
                int ix=node%nx,iy=node/nx;
                for(int i=0;i<4;i++)
                {
                    int xx=ix+(i==0?-1:i==1?1:0);
                    int yy=iy+(i==2?-1:i==3?1:0);
                    if(xx<0||xx>=nx||yy<0||yy>=ny)continue;
                    int direction=i<2?0:1;
                    FGEPoint2D a=new FGEPoint2D(x[ix],y[iy]);
                    FGEPoint2D b=new FGEPoint2D(x[xx],y[yy]);
                    if(!SegmentClear(a,b,active))continue;
                    int next=(yy*nx+xx)*2+direction;
                    double delta=Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y);
                    double cost=step.Cost+delta+
                        (previousDirection==direction?0:BendCost);
                    if(cost>=costs[next]-Epsilon)continue;
                    costs[next]=cost;parents[next]=state;
                    queue.Add(new QueueItem{Cost=cost,State=next,
                        Sequence=sequence++});
                }
            }
            if(reached<0)return null;
            var reverse=new List<FGEPoint2D>();
            for(int state=reached;state>=0;state=parents[state])
            {
                int node=state/2;
                reverse.Add(new FGEPoint2D(x[node%nx],y[node/nx]));
            }
            reverse.Reverse();
            IReadOnlyList<FGEPoint2D> answer=Compact(reverse);
            // 即使启用了有界候选集，也不得穿过被截去的真实障碍物。
            return Clear(answer,all)?answer:null;
        }
    }
}
