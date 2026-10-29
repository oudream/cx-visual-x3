using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Host snapshot of capacity and existing reservations.
    /// This is evidence for static planning; it is NOT an F19 resource lease.</summary>
    public sealed class MDCResourceBudget
    {
        public MDCResourceBudget(string id,string unit,decimal capacity,
            decimal reserved,string snapshotId)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("decision.resource.identity");
            if(capacity<0||reserved<0||reserved>capacity)
                throw new ArgumentOutOfRangeException("reserved");
            Id=id.Trim();Unit=unit.Trim();Capacity=capacity;Reserved=reserved;
            SnapshotId=snapshotId??"";
        }
        public string Id{get;private set;}
        public string Unit{get;private set;}
        public decimal Capacity{get;private set;}
        public decimal Reserved{get;private set;}
        public string SnapshotId{get;private set;}
    }

    /// <summary>A professional model supplies certified lower/upper bounds,
    /// rather than inventing an exact future CPU/GPU/memory consumption.</summary>
    public sealed class MDCResourceRequirement
    {
        public MDCResourceRequirement(string id,string unit,
            decimal lowerBound,decimal upperBound,string evidenceId)
        {
            if(string.IsNullOrWhiteSpace(id)||string.IsNullOrWhiteSpace(unit))
                throw new ArgumentException("decision.resource.identity");
            if(lowerBound<0||upperBound<lowerBound)
                throw new ArgumentOutOfRangeException("lowerBound");
            Id=id.Trim();Unit=unit.Trim();LowerBound=lowerBound;
            UpperBound=upperBound;EvidenceId=evidenceId??"";
        }
        public string Id{get;private set;}
        public string Unit{get;private set;}
        public decimal LowerBound{get;private set;}
        public decimal UpperBound{get;private set;}
        public string EvidenceId{get;private set;}
    }

    /// <summary>
    /// Pure budget reasoning: upperBound fits => Allowed, lowerBound exceeds => Rejected;
    /// overlapping intervals, missing evidence/snapshot or units => Unknown.
    /// No device probing, no resource allocation, no hidden policy or side effects.
    /// </summary>
    public sealed class MDCResourceBudgetPlanner
    {
        public MDCAssessment Assess(IEnumerable<MDCResourceBudget> budgets,
            IEnumerable<MDCResourceRequirement> requirements)
        {
            if(budgets==null)throw new ArgumentNullException("budgets");
            if(requirements==null)throw new ArgumentNullException("requirements");
            var available=budgets.ToList();
            var needed=requirements.ToList();
            if(available.Count>128||needed.Count>128||
                available.Any(x=>x==null)||needed.Any(x=>x==null)||
                available.GroupBy(x=>x.Id,StringComparer.Ordinal).Any(x=>x.Count()!=1)||
                needed.GroupBy(x=>x.Id,StringComparer.Ordinal).Any(x=>x.Count()!=1))
                throw new ArgumentException("decision.resource.invalid_set");
            var map=available.ToDictionary(x=>x.Id,StringComparer.Ordinal);
            var outcomes=new List<MDCConstraintOutcome>();
            foreach(var demand in needed.OrderBy(x=>x.Id,StringComparer.Ordinal))
            {
                MDCResourceBudget budget;
                if(!map.TryGetValue(demand.Id,out budget))
                {
                    outcomes.Add(new MDCConstraintOutcome(
                        "MDC_RESOURCE_MISSING:"+demand.Id,MDCVerdict.Unknown,
                        "Host provided no capacity for this resource"));
                    continue;
                }
                if(string.IsNullOrWhiteSpace(budget.SnapshotId)||
                   string.IsNullOrWhiteSpace(demand.EvidenceId))
                {
                    outcomes.Add(new MDCConstraintOutcome(
                        "MDC_RESOURCE_EVIDENCE:"+demand.Id,MDCVerdict.Unknown,
                        "budget snapshot or requirement bound has no evidence identity"));
                    continue;
                }
                if(!string.Equals(budget.Unit,demand.Unit,StringComparison.Ordinal))
                {
                    outcomes.Add(new MDCConstraintOutcome(
                        "MDC_RESOURCE_UNIT:"+demand.Id,MDCVerdict.Unknown,
                        "unit conversion must be supplied and verified by the Owner"));
                    continue;
                }
                decimal remaining=budget.Capacity-budget.Reserved;
                MDCVerdict result=demand.LowerBound>remaining
                    ? MDCVerdict.Rejected
                    : demand.UpperBound<=remaining
                        ? MDCVerdict.Allowed : MDCVerdict.Unknown;
                string reason=result==MDCVerdict.Rejected
                    ? "certified minimum requirement exceeds capacity"
                    : result==MDCVerdict.Unknown
                        ? "certified demand interval overlaps remaining capacity"
                        : "certified upper bound fits snapshot capacity";
                outcomes.Add(new MDCConstraintOutcome(
                    "MDC_RESOURCE_BUDGET:"+demand.Id,result,reason));
            }
            return new MDCAssessment(outcomes);
        }
    }
}
