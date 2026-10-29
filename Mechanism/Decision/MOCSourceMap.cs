using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Orchestration
{
    // Original MOC source map and step-origin public API, moved without changes.
    // This static DTO has no MRK execution dependencies; source identities stay Core-owned.
    public sealed class MOCStepOrigin
    {
        internal MOCStepOrigin(string step, MRIObjectAddress address, Guid block,
            IEnumerable<Guid> callPath, int? iteration, string role)
        {
            StepId = step; ContentAddress = address; BlockId = block;
            CallPath = Array.AsReadOnly(callPath.ToArray()); Iteration = iteration; Role = role;
        }
        public string StepId { get; private set; }
        public MRIObjectAddress ContentAddress { get; private set; }
        public Guid BlockId { get; private set; }
        public IReadOnlyList<Guid> CallPath { get; private set; }
        public int? Iteration { get; private set; }
        public string Role { get; private set; }
    }

    public sealed class MOCSourceMap
    {
        private readonly IReadOnlyDictionary<string, MOCStepOrigin> _steps;
        internal MOCSourceMap(IEnumerable<MOCStepOrigin> origins)
        {
            _steps = new ReadOnlyDictionary<string, MOCStepOrigin>(
                origins.ToDictionary(s => s.StepId, s => s, StringComparer.Ordinal));
        }
        public IReadOnlyDictionary<string, MOCStepOrigin> Steps { get { return _steps; } }
        public bool TryResolve(string compiledStepId, out MOCStepOrigin origin)
        { return _steps.TryGetValue(compiledStepId, out origin); }
        public IList<MOCStepOrigin> Find(Guid blockId)
        {
            // 循环同一Action会生成多个Step；按真实迭代序号稳定呈现，
            // 而不是按编译ID的字典序列出（该ID包含运行结构前缀）。
            return _steps.Values.Where(o => o.BlockId == blockId)
                .OrderBy(o => o.Iteration.HasValue ? 0 : 1)
                .ThenBy(o => o.Iteration ?? int.MaxValue)
                .ThenBy(o => o.StepId, StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }

}
