using System;
using System.Collections.Generic;
using CxVisual.Mechanism.Core;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>纯几何的四向视觉连接锚点；端口/连接身份由现有MPC体系持久化。</summary>
    public static class MVIVisualAnchors
    {
        public static bool IsVisualMember(string member)
        {
            return member=="visual.north"||member=="visual.east"||
                member=="visual.south"||member=="visual.west";
        }

        public static string MemberName(string side)
        {
            if(side!="north"&&side!="east"&&side!="south"&&side!="west")
                throw new ArgumentException("visual.anchor.invalid_side","side");
            return "visual."+side;
        }

        public static FGEPoint2D At(MVISceneItem item,string side)
        {
            if(item==null)throw new ArgumentNullException("item");
            MemberName(side);
            double x=side=="east"?item.LocalWidth:
                side=="west"?0:item.LocalWidth/2;
            double y=side=="south"?item.LocalHeight:
                side=="north"?0:item.LocalHeight/2;
            return item.WorldTransform.ToGeometry().TransformPoint(new FGEPoint2D(x,y));
        }
    }

    /// <summary>由已有MPC Visual连接投影路径。正交路由与人工折点共享同一端点身份。</summary>
    public static class MVIVisualRouting
    {
        public static IReadOnlyList<FGEPoint2D> Points(MPCConnection connection,
            FGEPoint2D start,FGEPoint2D end)
        {
            if(connection==null)throw new ArgumentNullException("connection");
            var result=new List<FGEPoint2D>{start};
            if(connection.VisualRouteKind==MPCVisualRouteKind.Orthogonal)
            {
                if(Math.Abs(start.X-end.X)>1e-7&&Math.Abs(start.Y-end.Y)>1e-7)
                {
                    double centerX=(start.X+end.X)/2;
                    result.Add(new FGEPoint2D(centerX,start.Y));
                    result.Add(new FGEPoint2D(centerX,end.Y));
                }
            }
            else if(connection.VisualRouteKind==MPCVisualRouteKind.Manual)
            {
                foreach(MPCVisualWaypoint waypoint in connection.VisualWaypoints)
                {
                    double t=waypoint.Fraction;
                    result.Add(new FGEPoint2D(
                        start.X+(end.X-start.X)*t+waypoint.OffsetX,
                        start.Y+(end.Y-start.Y)*t+waypoint.OffsetY));
                }
            }
            result.Add(end);
            return result.AsReadOnly();
        }

        /// <summary>为自动正交连线计算障碍避让；手动折点/直线完全保留原语义。</summary>
        public static IReadOnlyList<FGEPoint2D> Points(MPCConnection connection,
            FGEPoint2D start,FGEPoint2D end,IEnumerable<FGERect2D> obstacles)
        {
            if(connection==null)throw new ArgumentNullException("connection");
            if(connection.VisualRouteKind==MPCVisualRouteKind.Orthogonal)
                return MVIOrthogonalRouter.Route(start,end,obstacles);
            return Points(connection,start,end);
        }

        public static MPCVisualWaypoint WaypointAt(FGEPoint2D position,
            FGEPoint2D start,FGEPoint2D end,double fraction)
        {
            return new MPCVisualWaypoint(fraction,
                position.X-start.X-(end.X-start.X)*fraction,
                position.Y-start.Y-(end.Y-start.Y)*fraction);
        }
    }
}
