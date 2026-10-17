using System;
using System.Globalization;
namespace CxVisual.Foundation.Core
{
    /// <summary>Nonempty Guid identity; default is invalid and cannot be serialized as an accepted identity.</summary>
    public struct FOIStableId : IEquatable<FOIStableId>
    {
        private readonly Guid _value;
        public FOIStableId(Guid value) { if (value == Guid.Empty) throw new ArgumentException("Empty identity", "value"); _value = value; }
        public Guid Value { get { if (_value == Guid.Empty) throw new InvalidOperationException("Uninitialized identity"); return _value; } }
        public static FOIStableId NewId() { return new FOIStableId(Guid.NewGuid()); }
        public static FOIStableId Parse(string text) { return new FOIStableId(Guid.Parse(text)); }
        public static bool TryParse(string text, out FOIStableId result)
        {
            Guid id; if (Guid.TryParse(text, out id) && id != Guid.Empty) { result = new FOIStableId(id); return true; }
            result = default(FOIStableId); return false;
        }
        public bool Equals(FOIStableId other) { return _value.Equals(other._value); }
        public override bool Equals(object obj) { return obj is FOIStableId && Equals((FOIStableId)obj); }
        public override int GetHashCode() { return _value.GetHashCode(); }
        public override string ToString() { return Value.ToString("N", CultureInfo.InvariantCulture); }
    }
}
