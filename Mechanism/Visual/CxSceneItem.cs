using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public sealed class CxSceneItem
    {
        public CxSceneItem(CxElement source)
        {
            Id = source.Id; Name = source.Name;
            Bounds = new CxRect2D(source.X, source.Y, source.Width, source.Height);
        }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public CxRect2D Bounds { get; private set; }
    }
}
