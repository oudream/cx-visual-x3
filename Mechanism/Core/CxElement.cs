using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxElement : MRCRectangle
    {
        public CxElement(Guid id, string name, double x, double y, double width, double height) : base(id, name, x, y, width, height) { }
        public new System.Collections.ObjectModel.ReadOnlyCollection<CxNode> Children { get { return ((CxNode)this).Children; } }
        public new CxNode Parent { get { return ((CxNode)this).Parent; } }
        public new CxNode Find(Guid id) { return ((CxNode)this).Find(id); }
        public new CxNodeKind Kind { get { return CxNodeKind.Element; } }
        internal void SetPosition(double x, double y) { SetGeometry(x, y, Width, Height, Rotation, ScaleX, ScaleY); }
    }
}
