using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Persistent binding declaration only; evaluation belongs to the existing data/runtime owners.</summary>
    public sealed class MPMBinding
    {
        public MPMBinding(string sourceId, string memberId)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("Binding source/member required");
            SourceId = sourceId; MemberId = memberId;
        }
        public string SourceId { get; private set; }
        public string MemberId { get; private set; }
    }
    public abstract class MRCVisualItem : MEMNode
    {
        internal Dictionary<string, MPMBinding> BindingItems;
        private double _x, _y, _width, _height, _rotation, _scaleX = 1, _scaleY = 1;
        protected MRCVisualItem(Guid id, string name, string typeId, MEMNodeKind kind, double x, double y, double width, double height) : base(id, name, typeId, kind)
        { SetGeometry(x, y, width, height, 0, 1, 1); }
        public double X { get { return _x; } }
        public double Y { get { return _y; } }
        public double Width { get { return _width; } }
        public double Height { get { return _height; } }
        public double Rotation { get { return _rotation; } }
        public double ScaleX { get { return _scaleX; } }
        public double ScaleY { get { return _scaleY; } }
        public bool IsVisible { get; internal set; } = true;
        public bool IsSelectable { get; internal set; } = true;
        internal static void Finite(double value) { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException("value"); }
        internal void SetGeometry(double x, double y, double width, double height, double rotation, double sx, double sy)
        {
            Finite(x); Finite(y); Finite(width); Finite(height); Finite(rotation); Finite(sx); Finite(sy);
            if (width <= 0 || height <= 0 || sx == 0 || sy == 0) throw new ArgumentOutOfRangeException("geometry");
            _x = x; _y = y; _width = width; _height = height; _rotation = rotation % 360; _scaleX = sx; _scaleY = sy;
        }
        public MPMBinding GetBinding(string property)
        { MPMBinding binding; return BindingItems != null && BindingItems.TryGetValue(property, out binding) ? binding : null; }
        internal void LocalToParent(double x, double y, out double px, out double py)
        {
            double rad = Rotation * Math.PI / 180, cos = Math.Cos(rad), sin = Math.Sin(rad);
            px = X + cos * x * ScaleX - sin * y * ScaleY; py = Y + sin * x * ScaleX + cos * y * ScaleY;
        }
        public void TransformPoint(double localX, double localY, out double worldX, out double worldY)
        {
            worldX = localX; worldY = localY;
            for (MEMNode n = this; n != null; n = n.Parent)
            {
                var v = n as MRCVisualItem;
                if (v != null) v.LocalToParent(worldX, worldY, out worldX, out worldY);
            }
        }
        /// <summary>Translate a canvas drag to this object's local position without rewriting children.</summary>
        public void PositionAfterWorldDelta(double deltaX, double deltaY, out double x, out double y)
        {
            Finite(deltaX); Finite(deltaY);
            double wx, wy; TransformPoint(0, 0, out wx, out wy); wx += deltaX; wy += deltaY;
            var chain = new Stack<MRCVisualItem>();
            for (var n = Parent; n != null; n = n.Parent) { var v = n as MRCVisualItem; if (v != null) chain.Push(v); }
            while (chain.Count != 0) InversePoint(chain.Pop(), ref wx, ref wy);
            Finite(wx); Finite(wy); x = wx; y = wy;
        }
        private static void InversePoint(MRCVisualItem v, ref double wx, ref double wy)
        {
            double x = wx - v.X, y = wy - v.Y, rad = v.Rotation * Math.PI / 180;
            wx = (Math.Cos(rad) * x + Math.Sin(rad) * y) / v.ScaleX;
            wy = (-Math.Sin(rad) * x + Math.Cos(rad) * y) / v.ScaleY;
        }
        public bool HitTest(double worldX, double worldY)
        {
            Finite(worldX); Finite(worldY);
            var chain = new Stack<MRCVisualItem>();
            for (MEMNode n = this; n != null; n = n.Parent)
            { var v = n as MRCVisualItem; if (v != null) { if (!v.IsVisible) return false; chain.Push(v); } }
            if (!IsSelectable) return false;
            while (chain.Count != 0)
            {
                InversePoint(chain.Pop(), ref worldX, ref worldY);
            }
            return worldX >= 0 && worldY >= 0 && worldX <= Width && worldY <= Height;
        }
    }
    public abstract class MRCElement : MRCVisualItem
    {
        protected MRCElement(Guid id, string name, string typeId, double x, double y, double width, double height) : base(id, name, typeId, MEMNodeKind.Element, x, y, width, height) { }
    }
    public class MRCRectangle : MRCElement
    {
        public MRCRectangle(Guid id, string name, double x, double y, double width = 120, double height = 80) : base(id, name, "core.rectangle", x, y, width, height) { }
    }
    public class MRCGroup : MRCVisualItem
    {
        private static readonly MRCVisualItem[] NoItems = new MRCVisualItem[0];
        private MEMChildList<MRCVisualItem> _items;
        public MRCGroup(Guid id, string name) : base(id, name, "core.group", MEMNodeKind.Group, 0, 0, 120, 80) { }
        public new IReadOnlyList<MRCVisualItem> Children { get { return _items == null ? (IReadOnlyList<MRCVisualItem>)NoItems : _items; } }
        internal override IReadOnlyList<MEMNode> Owned { get { return Children; } }
        internal override bool Accepts(MEMNode child) { return child is MRCVisualItem; }
        internal override void InsertCore(int index, MEMNode child) { if (_items == null) _items = new MEMChildList<MRCVisualItem>(); _items.Insert(index, (MRCVisualItem)child); }
        internal override void RemoveCore(MEMNode child) { _items.Remove((MRCVisualItem)child); }
    }
}
