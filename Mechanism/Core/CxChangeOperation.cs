using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace CxVisual.Mechanism.Core
{
    public sealed class CxChangeOperation
    {
        private CxChangeOperation(CxChangeKind kind, Guid id, Guid parentId, CxElement element, double x, double y)
        {
            Kind = kind; Id = id; ParentId = parentId; Element = element; X = x; Y = y;
        }
        public CxChangeKind Kind { get; private set; }
        public Guid Id { get; private set; }
        public Guid ParentId { get; private set; }
        public CxElement Element { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public static CxChangeOperation Add(Guid parentId, CxElement element)
        {
            if (element == null) throw new ArgumentNullException("element");
            return new CxChangeOperation(CxChangeKind.AddElement, element.Id, parentId, element, 0, 0);
        }
        public static CxChangeOperation Remove(Guid id)
        {
            return new CxChangeOperation(CxChangeKind.RemoveElement, id, Guid.Empty, null, 0, 0);
        }
        public static CxChangeOperation Move(Guid id, double x, double y)
        {
            return new CxChangeOperation(CxChangeKind.MoveElement, id, Guid.Empty, null, x, y);
        }
    }
}
