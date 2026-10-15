using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Diagnostics
{
    // Bounded, ordered observation collection; overflow is an explicit error, never hidden loss.
    public sealed class TCTTestObserverHub
    {
        private readonly object _gate = new object();
        private readonly List<TCTTestEvent> _events = new List<TCTTestEvent>();
        private readonly int _capacity;
        private long _sequence;
        private bool _sealed;
        public TCTTestObserverHub(int capacity = 10000)
        {
            if (capacity < 1 || capacity > 1000000) throw new ArgumentOutOfRangeException("capacity");
            _capacity = capacity;
        }
        public TCTTestEvent Publish(string attemptId, TCTTestEventKind kind, string message)
        {
            lock (_gate)
            {
                if (_sealed) throw new InvalidOperationException("Observer hub sealed");
                if (_events.Count == _capacity)
                    throw new InvalidOperationException("TCT observation capacity exhausted; evidence cannot be discarded");
                TCTTestEvent value = new TCTTestEvent(++_sequence, attemptId, kind, message);
                _events.Add(value);
                return value;
            }
        }
        public IList<TCTTestEvent> Snapshot()
        {
            lock (_gate) return new ReadOnlyCollection<TCTTestEvent>(new List<TCTTestEvent>(_events));
        }
        public void Seal() { lock (_gate) _sealed = true; }
    }
}
