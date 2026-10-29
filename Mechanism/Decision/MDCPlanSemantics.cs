using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Immutable, bounded declarative expression tree. Evaluation is
    /// intentionally NOT provided; MRK/Host retain execution and input ownership.</summary>
    public sealed class MDCPlanExpression
    {
        internal MDCPlanExpression(MOLExpression source,IReadOnlyList<MDCPlanExpression> terms)
        {
            Operator=source.Operator;Source=source.Source;Name=source.Name;
            Comparison=source.Comparison;Expected=source.Expected;
            Terms=new ReadOnlyCollection<MDCPlanExpression>(terms.ToArray());
        }
        public MOLLogicalOperator Operator {get;private set;}
        public MOLExpressionSource Source {get;private set;}
        public string Name {get;private set;}
        public MOLComparison Comparison {get;private set;}
        public string Expected {get;private set;}
        public IReadOnlyList<MDCPlanExpression> Terms {get;private set;}
    }

    public sealed class MDCPlanBranchArm
    {
        internal MDCPlanBranchArm(Guid block,int index,MOLBranchCase item,
            MDCPlanExpression condition)
        {
            BranchBlockId=block;Ordinal=index;BodyBlockId=item.Body.Id;
            Label=item.Label;Condition=condition;
        }
        public Guid BranchBlockId {get;private set;}
        public int Ordinal {get;private set;}
        public Guid BodyBlockId {get;private set;}
        public string Label {get;private set;}
        /// <summary>null = else/default; no implicit predicate is invented.</summary>
        public MDCPlanExpression Condition {get;private set;}
    }

    public sealed class MDCPlanLoopRule
    {
        internal MDCPlanLoopRule(Guid block,MOLLoopDefinition loop,MDCPlanExpression guard)
        {
            BlockId=block;BodyBlockId=loop.Body.Id;Mode=loop.Mode;
            MaxIterations=loop.MaxIterations;CollectionInput=loop.CollectionInput;
            Condition=guard;
        }
        public Guid BlockId {get;private set;}
        public Guid BodyBlockId {get;private set;}
        public MOLLoopMode Mode {get;private set;}
        public int MaxIterations {get;private set;}
        public string CollectionInput {get;private set;}
        public MDCPlanExpression Condition {get;private set;}
    }

    public sealed class MDCPlanCatchRule
    {
        internal MDCPlanCatchRule(Guid block,Guid handler,string code,bool isFallback)
        {TryBlockId=block;HandlerBlockId=handler;ErrorCode=code;Fallback=isFallback;}
        public Guid TryBlockId {get;private set;}
        public Guid HandlerBlockId {get;private set;}
        public string ErrorCode {get;private set;}
        public bool Fallback {get;private set;}
    }

    public sealed class MDCPlanWaitRule
    {
        internal MDCPlanWaitRule(Guid block,MOLWaitDefinition wait)
        {
            BlockId=block;DurationOrTimeoutMs=wait.DurationMilliseconds;
            EventId=wait.EventInput;
            EventSource=wait.Event==null?MOLEventSource.FlowInput:wait.Event.Source;
            PayloadTypeId=wait.Event==null?"":wait.Event.PayloadTypeId;
            CorrelationInput=wait.Event==null?"":wait.Event.CorrelationInput;
        }
        public Guid BlockId {get;private set;}
        public int DurationOrTimeoutMs {get;private set;}
        public string EventId {get;private set;}
        public MOLEventSource EventSource {get;private set;}
        public string PayloadTypeId {get;private set;}
        public string CorrelationInput {get;private set;}
    }

    public sealed class MDCPlanExitRule
    {
        internal MDCPlanExitRule(Guid block,MOLControlBlock original)
        {BlockId=block;Kind=original.ExitKind;ErrorCode=original.ErrorCode;}
        public Guid BlockId {get;private set;}
        public MOLExitKind Kind {get;private set;}
        public string ErrorCode {get;private set;}
    }

    public sealed class MDCPlanStateRule
    {
        internal MDCPlanStateRule(Guid block,MOLStateDescriptor state)
        {
            MachineBlockId=block;StateId=state.Id;Region=state.Region;
            ParentStateId=state.ParentId;Initial=state.Initial;
            EntryCapabilityId=state.EntryCapability;ExitCapabilityId=state.ExitCapability;
        }
        public Guid MachineBlockId {get;private set;}
        public string StateId {get;private set;}
        public string Region {get;private set;}
        public string ParentStateId {get;private set;}
        public bool Initial {get;private set;}
        public string EntryCapabilityId {get;private set;}
        public string ExitCapabilityId {get;private set;}
    }

    public sealed class MDCPlanTransitionRule
    {
        internal MDCPlanTransitionRule(Guid block,MOLStateTransition transition,
            MDCPlanExpression guard)
        {
            MachineBlockId=block;SourceStateId=transition.Source;
            EventId=transition.EventId;TargetStateId=transition.Target;
            Priority=transition.Priority;Guard=guard;
        }
        public Guid MachineBlockId {get;private set;}
        public string SourceStateId {get;private set;}
        public string EventId {get;private set;}
        public string TargetStateId {get;private set;}
        public int Priority {get;private set;}
        public MDCPlanExpression Guard {get;private set;}
    }

    public sealed class MDCPlanBlockGuard
    {
        internal MDCPlanBlockGuard(Guid block,MDCPlanExpression guard)
        {BlockId=block;Guard=guard;}
        public Guid BlockId {get;private set;}
        public MDCPlanExpression Guard {get;private set;}
    }

    /// <summary>Describes the original Core behavior without delegating execution,
    /// evaluating scripts or copying mutable engineering nodes.</summary>
    public sealed class MDCPlanSemanticIndex
    {
        internal MDCPlanSemanticIndex(IEnumerable<MDCPlanBranchArm> branches,
            IEnumerable<MDCPlanLoopRule> loops,IEnumerable<MDCPlanCatchRule> catches,
            IEnumerable<MDCPlanWaitRule> waits,IEnumerable<MDCPlanExitRule> exits,
            IEnumerable<MDCPlanStateRule> states,
            IEnumerable<MDCPlanTransitionRule> transitions,
            IEnumerable<MDCPlanBlockGuard> conditions)
        {
            Branches=new ReadOnlyCollection<MDCPlanBranchArm>(branches.ToArray());
            Loops=new ReadOnlyCollection<MDCPlanLoopRule>(loops.ToArray());
            Catches=new ReadOnlyCollection<MDCPlanCatchRule>(catches.ToArray());
            Waits=new ReadOnlyCollection<MDCPlanWaitRule>(waits.ToArray());
            Exits=new ReadOnlyCollection<MDCPlanExitRule>(exits.ToArray());
            States=new ReadOnlyCollection<MDCPlanStateRule>(states.ToArray());
            Transitions=new ReadOnlyCollection<MDCPlanTransitionRule>(transitions.ToArray());
            BlockGuards=new ReadOnlyCollection<MDCPlanBlockGuard>(conditions.ToArray());
        }
        public IReadOnlyList<MDCPlanBranchArm> Branches {get;private set;}
        public IReadOnlyList<MDCPlanLoopRule> Loops {get;private set;}
        public IReadOnlyList<MDCPlanCatchRule> Catches {get;private set;}
        public IReadOnlyList<MDCPlanWaitRule> Waits {get;private set;}
        public IReadOnlyList<MDCPlanExitRule> Exits {get;private set;}
        public IReadOnlyList<MDCPlanStateRule> States {get;private set;}
        public IReadOnlyList<MDCPlanTransitionRule> Transitions {get;private set;}
        public IReadOnlyList<MDCPlanBlockGuard> BlockGuards {get;private set;}
    }

    /// <summary>Pure compiler pass over the unique MOL Core definition.</summary>
    internal static class MDCPlanSemanticCompiler
    {
        private sealed class Work
        {
            internal int Count;
        }
        private static MDCPlanExpression Copy(MOLExpression expression, int depth,
            Work budget)
        {
            if(expression==null)return null;
            if(depth>16||++budget.Count>128)
                throw new InvalidOperationException("decision.plan.expression_budget");
            return new MDCPlanExpression(expression,
                expression.Terms.Select(x=>Copy(x,depth+1,budget)).ToArray());
        }
        private static MDCPlanExpression Copy(MOLExpression expression)
        {return Copy(expression,0,new Work());}

        internal static MDCPlanSemanticIndex FromStructured(MOLLogicDefinition original)
        {
            var branches=new List<MDCPlanBranchArm>();
            var loops=new List<MDCPlanLoopRule>();
            var catches=new List<MDCPlanCatchRule>();
            var waits=new List<MDCPlanWaitRule>();
            var exits=new List<MDCPlanExitRule>();
            var guards=new List<MDCPlanBlockGuard>();
            foreach(var b in MDCLogicTraversal.Flatten(original.Root))
            {
                if(b.Condition!=null)guards.Add(new MDCPlanBlockGuard(b.Id,Copy(b.Condition)));
                for(int i=0;i<b.Cases.Count;i++)
                    branches.Add(new MDCPlanBranchArm(b.Id,i,b.Cases[i],
                        Copy(b.Cases[i].Condition)));
                if(b.Loop!=null)loops.Add(new MDCPlanLoopRule(b.Id,b.Loop,
                    Copy(b.Loop.Condition)));
                if(b.TryRegion!=null)
                {
                    if(b.TryRegion.CatchBody!=null)
                        catches.Add(new MDCPlanCatchRule(b.Id,
                            b.TryRegion.CatchBody.Id,"*",true));
                    foreach(var c in b.TryRegion.Catches)
                        catches.Add(new MDCPlanCatchRule(b.Id,c.Body.Id,c.ErrorCode,false));
                }
                if(b.Wait!=null)waits.Add(new MDCPlanWaitRule(b.Id,b.Wait));
                if(b.Kind==MOLControlKind.Exit)exits.Add(new MDCPlanExitRule(b.Id,b));
            }
            return new MDCPlanSemanticIndex(branches,loops,catches,waits,exits,
                new MDCPlanStateRule[0],new MDCPlanTransitionRule[0],guards);
        }

        internal static MDCPlanSemanticIndex FromWiring(MOLWiringDefinition wiring)
        {
            var states=new List<MDCPlanStateRule>();
            var transitions=new List<MDCPlanTransitionRule>();
            foreach(var machine in wiring.StateMachines.OrderBy(x=>x.Key))
            {
                foreach(var state in machine.Value.States)
                    states.Add(new MDCPlanStateRule(machine.Key,state));
                foreach(var transition in machine.Value.Transitions)
                    transitions.Add(new MDCPlanTransitionRule(machine.Key,transition,
                        Copy(transition.Guard)));
            }
            return new MDCPlanSemanticIndex(new MDCPlanBranchArm[0],
                new MDCPlanLoopRule[0],new MDCPlanCatchRule[0],
                new MDCPlanWaitRule[0],new MDCPlanExitRule[0],states,transitions,
                new MDCPlanBlockGuard[0]);
        }
    }
}
