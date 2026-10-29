using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CxVisual.Mechanism.Decision
{
    public enum MDCObjectiveDirection { Minimize = 1, Maximize = 2 }
    public enum MDCMetricEvidence { Measured = 1, Estimated = 2, Virtual = 3 }

    /// <summary>指标目标的单位、优化方向和优先级由业务提供，Decision不猜测目标。</summary>
    public sealed class MDCObjective
    {
        public MDCObjective(string id, string unit, MDCObjectiveDirection direction, int priority = 0)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("decision.objective.id", "id");
            if (!Enum.IsDefined(typeof(MDCObjectiveDirection), direction))
                throw new ArgumentOutOfRangeException("direction");
            Id = id.Trim(); Unit = unit ?? ""; Direction = direction; Priority = priority;
        }
        public string Id { get; private set; }
        public string Unit { get; private set; }
        public MDCObjectiveDirection Direction { get; private set; }
        public int Priority { get; private set; }
    }

    /// <summary>数值不脱离单位、来源和证据级别；估计/虚拟证据不自动当作实测。</summary>
    public sealed class MDCMeasuredMetric
    {
        public MDCMeasuredMetric(string id, double value, string unit,
            MDCMetricEvidence evidence, string evidenceId)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("decision.metric.id", "id");
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException("value");
            if (!Enum.IsDefined(typeof(MDCMetricEvidence), evidence))
                throw new ArgumentOutOfRangeException("evidence");
            Id = id.Trim(); Value = value; Unit = unit ?? "";
            Evidence = evidence; EvidenceId = evidenceId ?? "";
        }
        public string Id { get; private set; }
        public double Value { get; private set; }
        public string Unit { get; private set; }
        public MDCMetricEvidence Evidence { get; private set; }
        public string EvidenceId { get; private set; }
    }

    public sealed class MDCOption
    {
        public MDCOption(string id, MDCAssessment hardConstraints,
            IEnumerable<MDCMeasuredMetric> measurements)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("decision.option.id", "id");
            if (hardConstraints == null) throw new ArgumentNullException("hardConstraints");
            if (measurements == null) throw new ArgumentNullException("measurements");
            var all = measurements.ToList();
            if (all.Any(x => x == null) ||
                all.GroupBy(x => x.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new ArgumentException("decision.metric.duplicate_or_null", "measurements");
            if (all.Count > 128) throw new ArgumentException("decision.metric.capacity");
            Id = id.Trim(); HardConstraints = hardConstraints;
            Measurements = new ReadOnlyCollection<MDCMeasuredMetric>(all);
        }
        public string Id { get; private set; }
        public MDCAssessment HardConstraints { get; private set; }
        public IReadOnlyList<MDCMeasuredMetric> Measurements { get; private set; }
    }

    public sealed class MDCOptionEvaluation
    {
        internal MDCOptionEvaluation(MDCOption option, MDCVerdict verdict, IEnumerable<string> reasons)
        {
            Option = option; Verdict = verdict;
            ReasonCodes = new ReadOnlyCollection<string>(reasons.ToList());
        }
        public MDCOption Option { get; private set; }
        public MDCVerdict Verdict { get; private set; }
        public IReadOnlyList<string> ReasonCodes { get; private set; }
    }

    public sealed class MDCComparisonResult
    {
        internal MDCComparisonResult(IList<MDCOptionEvaluation> candidates,
            IList<MDCOptionEvaluation> pareto, MDCOptionEvaluation selected)
        {
            Candidates = new ReadOnlyCollection<MDCOptionEvaluation>(candidates);
            Pareto = new ReadOnlyCollection<MDCOptionEvaluation>(pareto);
            Selected = selected;
        }
        public IReadOnlyList<MDCOptionEvaluation> Candidates { get; private set; }
        public IReadOnlyList<MDCOptionEvaluation> Pareto { get; private set; }
        /// <summary>仅来自实测且通过已声明硬约束的可比Pareto集；无可行项则null。</summary>
        public MDCOptionEvaluation Selected { get; private set; }
    }

    /// <summary>
    /// 有界、确定性的多目标比较：Hard先过滤；仅已实测的同单位指标进入Pareto和选中。
    /// Lexicographic优先级只用来在非支配集合中选一个明确的用户目标首选，不宣称全局最优。
    /// </summary>
    public sealed class MDCChoiceService
    {
        public MDCComparisonResult Compare(IEnumerable<MDCOption> options,
            IEnumerable<MDCObjective> objectives)
        {
            if (options == null) throw new ArgumentNullException("options");
            if (objectives == null) throw new ArgumentNullException("objectives");
            var all = options.ToList();
            var goals = objectives.ToList();
            if (all.Count > 256 || goals.Count == 0 || goals.Count > 16 ||
                all.Any(x => x == null) || goals.Any(x => x == null) ||
                all.GroupBy(x => x.Id, StringComparer.Ordinal).Any(g => g.Count() != 1) ||
                goals.GroupBy(x => x.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new ArgumentException("decision.compare.invalid_inputs");
            goals = goals.OrderBy(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToList();
            var evaluated = new List<MDCOptionEvaluation>();
            foreach (var option in all.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (option.HardConstraints.Verdict == MDCVerdict.Rejected)
                {
                    evaluated.Add(new MDCOptionEvaluation(option, MDCVerdict.Rejected,
                        option.HardConstraints.ReasonCodes));
                    continue;
                }
                var problems = new List<string>();
                if (option.HardConstraints.Verdict == MDCVerdict.Unknown)
                    problems.AddRange(option.HardConstraints.ReasonCodes);
                var metrics = option.Measurements.ToDictionary(x => x.Id, StringComparer.Ordinal);
                foreach (var goal in goals)
                {
                    MDCMeasuredMetric metric;
                    if (!metrics.TryGetValue(goal.Id, out metric))
                        problems.Add("MDC_METRIC_MISSING:" + goal.Id);
                    else if (!string.Equals(metric.Unit, goal.Unit, StringComparison.Ordinal))
                        problems.Add("MDC_METRIC_UNIT_MISMATCH:" + goal.Id);
                    else if (metric.Evidence != MDCMetricEvidence.Measured ||
                             string.IsNullOrWhiteSpace(metric.EvidenceId))
                        problems.Add("MDC_METRIC_UNVERIFIED:" + goal.Id);
                }
                evaluated.Add(new MDCOptionEvaluation(option,
                    problems.Count == 0 ? MDCVerdict.Allowed : MDCVerdict.Unknown, problems));
            }
            var feasible = evaluated.Where(x => x.Verdict == MDCVerdict.Allowed).ToList();
            var metricsByOption = feasible.ToDictionary(x => x.Option.Id,
                x => x.Option.Measurements.ToDictionary(m => m.Id, StringComparer.Ordinal),
                StringComparer.Ordinal);
            var frontier = feasible.Where(candidate => !feasible.Any(other =>
                other != candidate && Dominates(metricsByOption[other.Option.Id],
                    metricsByOption[candidate.Option.Id], goals))).ToList();
            frontier.Sort((a, b) => CompareLexicographic(
                metricsByOption[a.Option.Id], metricsByOption[b.Option.Id], goals,
                a.Option.Id, b.Option.Id));
            return new MDCComparisonResult(evaluated, frontier,
                frontier.Count == 0 ? null : frontier[0]);
        }

        private static bool Dominates(IDictionary<string, MDCMeasuredMetric> a,
            IDictionary<string, MDCMeasuredMetric> b, IList<MDCObjective> goals)
        {
            bool strictlyBetter = false;
            foreach (var goal in goals)
            {
                int cmp = a[goal.Id].Value.CompareTo(b[goal.Id].Value);
                if (goal.Direction == MDCObjectiveDirection.Maximize) cmp = -cmp;
                if (cmp > 0) return false;
                if (cmp < 0) strictlyBetter = true;
            }
            return strictlyBetter;
        }
        private static int CompareLexicographic(IDictionary<string, MDCMeasuredMetric> a,
            IDictionary<string, MDCMeasuredMetric> b, IList<MDCObjective> goals,
            string aid, string bid)
        {
            foreach (var goal in goals)
            {
                int cmp = a[goal.Id].Value.CompareTo(b[goal.Id].Value);
                if (goal.Direction == MDCObjectiveDirection.Maximize) cmp = -cmp;
                if (cmp != 0) return cmp;
            }
            return StringComparer.Ordinal.Compare(aid, bid);
        }
    }
}
