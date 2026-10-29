using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Static possible-route exploration, NOT MRK execution or
    /// physically verified simulation. Conditional choices are explicit assumptions.</summary>
    public sealed class MDCPathExploration
    {
        internal MDCPathExploration(IList<Guid> possible, IList<Guid> unknown,
            IList<Guid> invalid)
        {
            PossibleBlockIds = new ReadOnlyCollection<Guid>(possible);
            UndecidedBranchBlockIds = new ReadOnlyCollection<Guid>(unknown);
            InvalidAssumptionBlockIds = new ReadOnlyCollection<Guid>(invalid);
        }
        public IReadOnlyList<Guid> PossibleBlockIds { get; private set; }
        public IReadOnlyList<Guid> UndecidedBranchBlockIds { get; private set; }
        public IReadOnlyList<Guid> InvalidAssumptionBlockIds { get; private set; }
        /// <summary>All visited guards were specified; this never proves real devices
        /// will follow the same route.</summary>
        public bool HasCompleteAssumptions
        {
            get { return UndecidedBranchBlockIds.Count == 0 &&
                InvalidAssumptionBlockIds.Count == 0; }
        }
    }

    public sealed class MDCStaticPathExplorer
    {
        /// <summary>
        /// Explore only control edges from the authoritative Core/MOL wiring.
        /// Unspecified branches expand to all possible outgoing alternatives;
        /// a given guard must match one existing ConditionId exactly.
        /// No provider, MRK resolver, unsafe scripts, or real device is touched.
        /// </summary>
        public MDCPathExploration Explore(MOLWiringDefinition wiring,
            IReadOnlyDictionary<Guid, string> assumedGuards = null)
        {
            if (wiring == null) throw new ArgumentNullException("wiring");
            wiring.Validate();
            if (wiring.Blocks.Count == 0)
                throw new InvalidOperationException("decision.path.empty_draft");
            var choices = assumedGuards ?? new Dictionary<Guid,string>();
            if (choices.Count > 4096 || choices.Any(x => x.Key == Guid.Empty ||
                    string.IsNullOrWhiteSpace(x.Value)))
                throw new ArgumentException("decision.path.invalid_assumptions");
            var edges = wiring.Links.Where(x=>x.Kind==MPCPortKind.Control)
                .GroupBy(x=>x.Source.BlockId).ToDictionary(x=>x.Key,
                    x=>x.OrderBy(y=>y.Target.BlockId).ThenBy(y=>y.ConditionId,
                        StringComparer.Ordinal).ToList());
            var reachable = new HashSet<Guid>();
            var undecided = new SortedSet<Guid>();
            var invalid = new SortedSet<Guid>();
            var stack = new Stack<Guid>();
            stack.Push(wiring.EntryBlockId);
            while(stack.Count > 0)
            {
                Guid current=stack.Pop();
                if (!reachable.Add(current)) continue;
                List<MOLLink> outgoing;
                if (!edges.TryGetValue(current,out outgoing)) continue;
                var conditional = outgoing.Where(x=>x.ConditionId.Length!=0).ToList();
                string assumed;
                bool hasChoice=choices.TryGetValue(current,out assumed);
                if (conditional.Count!=0 && !hasChoice)
                    undecided.Add(current);
                else if (conditional.Count==0 && hasChoice)
                    invalid.Add(current);
                List<MOLLink> follow;
                if (hasChoice && conditional.Count!=0)
                {
                    follow = conditional.Where(x=>
                        StringComparer.Ordinal.Equals(x.ConditionId,assumed)).ToList();
                    if (follow.Count==0) invalid.Add(current);
                }
                else follow=outgoing;
                foreach(MOLLink edge in follow)
                    stack.Push(edge.Target.BlockId);
                if(reachable.Count>4096)
                    throw new InvalidOperationException("decision.path.node_budget");
            }
            foreach(var pair in choices)
                if (!reachable.Contains(pair.Key)) invalid.Add(pair.Key);
            return new MDCPathExploration(reachable.OrderBy(x=>x).ToList(),
                undecided.ToList(), invalid.ToList());
        }
    }
}
