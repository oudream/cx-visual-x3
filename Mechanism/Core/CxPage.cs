using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxPage : CxNode
    {
        public CxPage(Guid id, string name) : base(id, name) { }
        public override CxNodeKind Kind { get { return CxNodeKind.Page; } }
    }
}
