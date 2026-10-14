using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxContent : CxNode
    {
        public CxContent(Guid id, string name) : base(id, name) { }
        public override CxNodeKind Kind { get { return CxNodeKind.Content; } }
    }
}
