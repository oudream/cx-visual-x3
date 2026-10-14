using System;
using CxVisual.Foundation.Core;
namespace CxVisual.Foundation.Geometry
{
    public struct CxPoint2D
    {
        public CxPoint2D(double x, double y) { X = x; Y = y; }
        public double X { get; private set; }
        public double Y { get; private set; }
    }
}
