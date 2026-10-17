using System;
namespace CxVisual.Mechanism.Core
{
    /// <summary>Compatibility name for the same authoritative model, not a copied wrapper.</summary>
    public sealed class CxContent : MEMContent
    {
        public CxContent(Guid id, string name) : base(id, name) { }
        public new System.Collections.ObjectModel.ReadOnlyCollection<CxNode> Children { get { return ((CxNode)this).Children; } }
        public new CxNode Parent { get { return ((CxNode)this).Parent; } }
        public new CxNode Find(Guid id) { return ((CxNode)this).Find(id); }
        public new CxNodeKind Kind { get { return CxNodeKind.Content; } }
    }
}
