using System;
using System.Globalization;
namespace CxVisual.Foundation.Core
{
    public struct CxStableId : IEquatable<CxStableId>
    {
        public CxStableId(Guid value)
        {
            if (value == Guid.Empty) throw new ArgumentException("Id cannot be empty", "value");
            Value = value;
        }
        public Guid Value { get; private set; }
        public static CxStableId NewId() { return new CxStableId(Guid.NewGuid()); }
        public bool Equals(CxStableId other) { return Value.Equals(other.Value); }
        public override bool Equals(object obj) { return obj is CxStableId && Equals((CxStableId)obj); }
        public override int GetHashCode() { return Value.GetHashCode(); }
        public override string ToString() { return Value.ToString("N", CultureInfo.InvariantCulture); }
    }
}
