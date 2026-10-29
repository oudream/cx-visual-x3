using System;

namespace CxVisual.Professional.Simulation.Modules
{
    /// <summary>
    /// Deterministic virtual axis state model, not a physical driver or hardware safety
    /// controller. PSMSimulationModule owns model/clock and MRK decides when to advance.
    /// </summary>
    public sealed class PSMAxisLinearModel : IPSMSimulationModel
    {
        public const string ProviderId="simulation.axis.linear";
        public const string StepCapabilityId="simulation.axis.step";
        private readonly object _gate=new object();
        private readonly double _velocity;
        private readonly double _limit;
        private double _position;
        private bool _initialized;
        private bool _faulted;
        private int _seed;
        public PSMAxisLinearModel(double velocityMillimetersPerSecond,
            double positiveLimitMillimeters)
        {
            if(double.IsNaN(velocityMillimetersPerSecond) ||
                double.IsInfinity(velocityMillimetersPerSecond) ||
                velocityMillimetersPerSecond<=0 ||
                double.IsNaN(positiveLimitMillimeters) ||
                double.IsInfinity(positiveLimitMillimeters) ||
                positiveLimitMillimeters<=0)
                throw new ArgumentOutOfRangeException("axis.model","Invalid positive velocity/limit");
            _velocity=velocityMillimetersPerSecond;
            _limit=positiveLimitMillimeters;
        }
        public string ModelId {get{return "axis";}}
        public double PositionMillimeters {get{lock(_gate)return _position;}}
        public bool Faulted {get{lock(_gate)return _faulted;}}
        public bool Initialized {get{lock(_gate)return _initialized;}}
        public int Seed {get{lock(_gate)return _seed;}}
        public void Initialize(int seed)
        {
            lock(_gate){_position=0;_faulted=false;_initialized=true;_seed=seed;}
        }
        public void Advance(TimeSpan delta)
        {
            if(delta<=TimeSpan.Zero)throw new ArgumentOutOfRangeException("delta");
            lock(_gate)
            {
                if(!_initialized)throw new InvalidOperationException("Virtual axis has not initialized");
                if(_faulted)throw new InvalidOperationException("Injected virtual axis fault");
                double next=_position+_velocity*delta.TotalSeconds;
                if(double.IsInfinity(next)||double.IsNaN(next)||next>_limit)
                    throw new InvalidOperationException("Virtual positive travel limit reached");
                _position=next; // No partial update when advance is rejected.
            }
        }
        public void InjectFault(){lock(_gate){if(!_initialized)throw new InvalidOperationException("Initialize model first");_faulted=true;}}
        public void Reset(){lock(_gate){_position=0;_faulted=false;_initialized=false;}}
    }
}
