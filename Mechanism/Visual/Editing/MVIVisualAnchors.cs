using System;
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
}
