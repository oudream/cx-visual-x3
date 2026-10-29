using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    public sealed class MDCImpactedBlock
    {
        internal MDCImpactedBlock(Guid contentId, Guid blockId, string capabilityId,
            MDCCatalogChangeKind change)
        { ContentId = contentId; BlockId = blockId; CapabilityId = capabilityId; Change = change; }
        public Guid ContentId { get; private set; }
        public Guid BlockId { get; private set; }
        public string CapabilityId { get; private set; }
        public MDCCatalogChangeKind Change { get; private set; }
    }

    /// <summary>Pure snapshot analysis. Incomplete analysis is never a proof
    /// that an engineering revision is unaffected.</summary>
    public sealed class MDCProjectImpactResult
    {
        internal MDCProjectImpactResult(IList<MDCImpactedBlock> direct,
            IList<Guid> affected, IList<Guid> unknown)
        {
            DirectBlocks = new ReadOnlyCollection<MDCImpactedBlock>(direct);
            AffectedContentIds = new ReadOnlyCollection<Guid>(affected);
            UnknownContentIds = new ReadOnlyCollection<Guid>(unknown);
        }
        public IReadOnlyList<MDCImpactedBlock> DirectBlocks { get; private set; }
        public IReadOnlyList<Guid> AffectedContentIds { get; private set; }
        public IReadOnlyList<Guid> UnknownContentIds { get; private set; }
        public bool IsComplete { get { return UnknownContentIds.Count == 0; } }
    }

    /// <summary>
    /// D10: map a capability catalog delta onto the existing Core/MOL definitions.
    /// No editable graph, provider invocation, scheduler, or MRK trace is constructed.
    /// </summary>
    public static class MDCProjectImpactAnalyzer
    {
        public static MDCProjectImpactResult Analyze(MEMProjectSnapshot project,
            IEnumerable<MDCCatalogChange> changes)
        {
            if (project == null) throw new ArgumentNullException("project");
            if (changes == null) throw new ArgumentNullException("changes");
            var changed = changes.ToList();
            if (changed.Count > 4096 || changed.Any(x => x == null) ||
                changed.GroupBy(x => x.CapabilityId, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new ArgumentException("decision.impact.invalid_changes");
            var byId = changed.ToDictionary(x => x.CapabilityId, StringComparer.Ordinal);
            var direct = new List<MDCImpactedBlock>();
            var reverse = new Dictionary<Guid, HashSet<Guid>>();
            var unknown = new HashSet<Guid>();
            var contents = new HashSet<Guid>(project.Nodes.Where(x =>
                x.Kind == MEMNodeKind.Content).Select(x => x.Id));
            int totalSteps = 0;
            foreach (MEMNodeSnapshot node in project.Nodes.Where(x =>
                x.Kind == MEMNodeKind.Content))
            {
                try
                {
                    MOLWiringDefinition wired = MOLWiringCodec.Get(node);
                    if (wired != null)
                    {
                        foreach (MOLControlBlock block in wired.Blocks)
                        {
                            if (++totalSteps > 131072)
                                throw new InvalidOperationException("decision.impact.step_budget");
                            Record(direct,byId,node.Id,block.Id,block.CapabilityId);
                            Guid target;
                            if (block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                            {
                                if (!Guid.TryParseExact(block.CapabilityId.Substring(11),"N",out target))
                                    throw new InvalidOperationException("decision.impact.invalid_invoke");
                                AddReference(reverse,node.Id,target);
                                if (!contents.Contains(target)) unknown.Add(node.Id);
                            }
                        }
                        foreach (var machine in wired.StateMachines)
                            foreach (MOLStateDescriptor state in machine.Value.States)
                            {
                                Record(direct,byId,node.Id,machine.Key,state.EntryCapability);
                                Record(direct,byId,node.Id,machine.Key,state.ExitCapability);
                            }
                    }
                    else
                    {
                        MOLLogicDefinition legacy = MOLLogicCodec.Get(node);
                        if (legacy == null) continue;
                        foreach (MOLControlBlock block in MDCLogicTraversal.Flatten(legacy.Root))
                        {
                            if (++totalSteps > 131072)
                                throw new InvalidOperationException("decision.impact.step_budget");
                            Record(direct,byId,node.Id,block.Id,block.CapabilityId);
                            if (block.Kind == MOLControlKind.Invoke)
                            {
                                Guid target;
                                if (!Guid.TryParseExact(block.TargetFlow,"N",out target))
                                    throw new InvalidOperationException("decision.impact.invalid_invoke");
                                AddReference(reverse,node.Id,target);
                                if (!contents.Contains(target)) unknown.Add(node.Id);
                            }
                        }
                    }
                }
                catch (InvalidDataException) { unknown.Add(node.Id); }
                catch (InvalidOperationException) { unknown.Add(node.Id); }
                catch (ArgumentException) { unknown.Add(node.Id); }
            }
            var affected = new HashSet<Guid>(direct.Select(x => x.ContentId));
            var queue = new Queue<Guid>(affected);
            while (queue.Count != 0)
            {
                Guid child = queue.Dequeue();
                HashSet<Guid> parents;
                if (!reverse.TryGetValue(child,out parents)) continue;
                foreach (Guid parent in parents)
                    if (affected.Add(parent)) queue.Enqueue(parent);
            }
            var sortedDirect = direct.GroupBy(x => x.ContentId.ToString("N") + ":" +
                x.BlockId.ToString("N") + ":" + x.CapabilityId, StringComparer.Ordinal)
                .Select(x => x.First())
                .OrderBy(x => x.ContentId).ThenBy(x => x.BlockId)
                .ThenBy(x => x.CapabilityId, StringComparer.Ordinal).ToList();
            return new MDCProjectImpactResult(sortedDirect,
                affected.OrderBy(x => x).ToList(), unknown.OrderBy(x => x).ToList());
        }

        private static void Record(ICollection<MDCImpactedBlock> results,
            IDictionary<string, MDCCatalogChange> changes, Guid content, Guid block,
            string capability)
        {
            MDCCatalogChange changed;
            if (!string.IsNullOrEmpty(capability) &&
                changes.TryGetValue(capability,out changed))
                results.Add(new MDCImpactedBlock(content,block,capability,changed.Kind));
        }

        private static void AddReference(Dictionary<Guid,HashSet<Guid>> reverse,
            Guid parent,Guid child)
        {
            HashSet<Guid> parents;
            if (!reverse.TryGetValue(child,out parents))
            { parents = new HashSet<Guid>(); reverse.Add(child,parents); }
            parents.Add(parent);
        }


    }
}
