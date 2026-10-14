using System;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxElement : CxNode
    {
        public CxElement(Guid id, string name, double x, double y, double width, double height) : base(id, name)
        {
            CheckGeometry(x, y, width, height);
            X = x; Y = y; Width = width; Height = height;
        }
        public override CxNodeKind Kind { get { return CxNodeKind.Element; } }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        internal void SetPosition(double x, double y)
        {
            CheckGeometry(x, y, Width, Height);
            X = x; Y = y;
        }
        private static void CheckGeometry(double x, double y, double width, double height)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y)
                || double.IsNaN(width) || double.IsInfinity(width)
                || double.IsNaN(height) || double.IsInfinity(height)
                || width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("Invalid rectangle geometry");
        }
    }
}
