using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxGroup : CxNode
    {
        public CxGroup(Guid id, string name) : base(id, name) { }
        public override CxNodeKind Kind { get { return CxNodeKind.Group; } }
    }
}
