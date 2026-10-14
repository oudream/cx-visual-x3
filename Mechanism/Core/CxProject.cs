using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxProject : CxNode
    {
        public CxProject(Guid id, string name) : base(id, name) { }
        public override CxNodeKind Kind { get { return CxNodeKind.Project; } }
    }
}
