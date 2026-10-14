using System;
namespace CxVisual.Mechanism.Presentation.Wpf
{
    public sealed class CxNodeEventArgs : EventArgs
    {
        public CxNodeEventArgs(Guid? id) { Id = id; }
        public Guid? Id { get; private set; }
    }
}
