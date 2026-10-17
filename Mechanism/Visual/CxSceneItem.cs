using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public sealed class CxSceneItem
    {
        public CxSceneItem(MRCElement source)
        {
            Id = source.Id; Name = source.Name;
            Source = source;
            double x1,y1,x2,y2,x3,y3,x4,y4;
            source.TransformPoint(0,0,out x1,out y1); source.TransformPoint(source.Width,0,out x2,out y2);
            source.TransformPoint(0,source.Height,out x3,out y3); source.TransformPoint(source.Width,source.Height,out x4,out y4);
            double left=Math.Min(Math.Min(x1,x2),Math.Min(x3,x4)), top=Math.Min(Math.Min(y1,y2),Math.Min(y3,y4));
            Bounds = new CxRect2D(left, top, Math.Max(Math.Max(x1,x2),Math.Max(x3,x4))-left, Math.Max(Math.Max(y1,y2),Math.Max(y3,y4))-top);
        }
        internal MRCElement Source { get; private set; }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public CxRect2D Bounds { get; private set; }
    }
}
