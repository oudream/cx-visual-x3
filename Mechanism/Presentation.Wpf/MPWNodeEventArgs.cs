using System;
namespace CxVisual.Mechanism.Presentation.Wpf
{
    public sealed class MPWNodeEventArgs : EventArgs
    {
        public MPWNodeEventArgs(Guid? id) { Id = id; }
        public Guid? Id { get; private set; }
    }
}
