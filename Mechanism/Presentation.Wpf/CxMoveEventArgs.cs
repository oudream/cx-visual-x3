using System;
namespace CxVisual.Mechanism.Presentation.Wpf
{
    public sealed class CxMoveEventArgs : EventArgs
    {
        public CxMoveEventArgs(Guid id, double x, double y) { Id = id; X = x; Y = y; }
        public Guid Id { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
    }
}
