using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxSymbol : CxNode
    {
        public CxSymbol(Guid id, string name) : base(id, name) { }
        public override CxNodeKind Kind { get { return CxNodeKind.Symbol; } }
    }
}
