using System;
using System.Collections.Generic;
using System.IO;

namespace CxVisual.Platform.Protocols
{
    /// <summary>Incremental bounded parser. Incomplete input consumes nothing; malformed input faults until Reset.</summary>
    public abstract class CxFrameParser : IFrameParser
    {
        private readonly object _gate = new object();
        private readonly List<byte> _buffer = new List<byte>();
        private readonly int _capacity;
        private Exception _fault;
        protected CxFrameParser(int capacity = 65536) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }
        public int BufferedBytes { get { lock (_gate) return _buffer.Count; } }
        public abstract bool TryParse(IList<byte> buffer, out object frame, out int consumed);
        public void PushBytes(byte[] bytes, int offset, int count)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
            lock (_gate)
            {
                if (_fault != null) throw new InvalidOperationException("PROTOCOL_PARSER_REQUIRES_RESET", _fault);
                if (count > _capacity - _buffer.Count) throw new InvalidOperationException("PROTOCOL_PARSER_CAPACITY_REACHED");
                for (int i = 0; i < count; i++) _buffer.Add(bytes[offset + i]);
            }
        }
        public bool TryReadFrame(out object frame)
        {
            lock (_gate)
            {
                if (_fault != null) throw new InvalidOperationException("PROTOCOL_PARSER_REQUIRES_RESET", _fault);
                try
                {
                    int consumed; bool parsed = TryParse(_buffer.AsReadOnly(), out frame, out consumed);
                    if (!parsed) { if (consumed != 0 || frame != null) throw new InvalidDataException("PROTOCOL_PARSER_INVALID_INCOMPLETE_RESULT"); return false; }
                    if (frame == null || consumed <= 0 || consumed > _buffer.Count) throw new InvalidDataException("PROTOCOL_PARSER_INVALID_CONSUMPTION");
                    _buffer.RemoveRange(0, consumed); return true;
                }
                catch (Exception error) { _fault = error; throw; }
            }
        }
        public void Reset() { lock (_gate) { _buffer.Clear(); _fault = null; } }
    }
}
