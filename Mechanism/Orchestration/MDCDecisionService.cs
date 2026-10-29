using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>一次静态判断：未知条件不可自动当作允许。运行授权仍属于Host/MRK。</summary>
    public enum MDCVerdict { Allowed = 1, Rejected = 2, Unknown = 3 }

    /// <summary>已由权威规则提供的单项结果。不会执行自定义代码、设备或Provider。</summary>
    public sealed class MDCConstraintOutcome
    {
        public MDCConstraintOutcome(string id, MDCVerdict verdict, string reason)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("decision.constraint.id", "id");
            if (!Enum.IsDefined(typeof(MDCVerdict), verdict))
                throw new ArgumentOutOfRangeException("verdict");
            Id = id.Trim();
            Verdict = verdict;
            Reason = reason ?? "";
        }
        public string Id { get; private set; }
        public MDCVerdict Verdict { get; private set; }
        public string Reason { get; private set; }
    }

    /// <summary>不可变结果；结论仅涵盖已评估的静态条件，并非生产许可。</summary>
    public sealed class MDCAssessment
    {
        public MDCAssessment(IEnumerable<MDCConstraintOutcome> outcomes)
        {
            if (outcomes == null) throw new ArgumentNullException("outcomes");
            var copy = outcomes.ToList();
            if (copy.Any(x => x == null) ||
                copy.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() != 1))
                throw new ArgumentException("decision.constraint.duplicate_or_null", "outcomes");
            Outcomes = new ReadOnlyCollection<MDCConstraintOutcome>(copy);
            Verdict = copy.Any(x => x.Verdict == MDCVerdict.Rejected)
                ? MDCVerdict.Rejected
                : copy.Any(x => x.Verdict == MDCVerdict.Unknown)
                    ? MDCVerdict.Unknown : MDCVerdict.Allowed;
        }
        public MDCVerdict Verdict { get; private set; }
        public IReadOnlyList<MDCConstraintOutcome> Outcomes { get; private set; }
        public IReadOnlyList<string> ReasonCodes
        {
            get
            {
                return new ReadOnlyCollection<string>(Outcomes
                    .Where(x => x.Verdict != MDCVerdict.Allowed)
                    .Select(x => x.Id).ToList());
            }
        }
    }

    public sealed class MDCCandidate
    {
        internal MDCCandidate(string capabilityId, MPCPortDescriptor target, MDCAssessment assessment)
        {
            CapabilityId = capabilityId;
            TargetPort = target;
            Assessment = assessment;
        }
        public string CapabilityId { get; private set; }
        public MPCPortDescriptor TargetPort { get; private set; }
        public MDCAssessment Assessment { get; private set; }
    }

    /// <summary>纯静态端口评估与候选发现，渐进迁移至Mechanism.Decision。
    /// 复用MPC端口和现有MOC目录，既不创建第二Registry，也不调用MRK Resolver。
    /// </summary>
    public sealed class MDCDecisionService
    {
        public MDCAssessment AssessConnection(MPCPortDescriptor source, MPCPortDescriptor target)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (target == null) throw new ArgumentNullException("target");
            var checks = new List<MDCConstraintOutcome>();
            bool fromOutput = source.Direction == MPCPortDirection.Output ||
                              source.Direction == MPCPortDirection.InOut;
            bool toInput = target.Direction == MPCPortDirection.Input ||
                           target.Direction == MPCPortDirection.InOut;
            checks.Add(Check("MDC_PORT_DIRECTION", fromOutput && toInput,
                "source must output and target must input"));
            checks.Add(Check("MDC_PORT_KIND", source.Kind == target.Kind,
                "port kinds must match; conversions require an explicit adapter"));
            checks.Add(Check("MDC_PORT_TYPE", string.Equals(source.TypeId, target.TypeId,
                StringComparison.Ordinal), "TypeId must match; implicit casts are not assumed"));

            // MTU contracts belong to MPC; never infer physical unit/coordinate conversions.
            MTUContractReference a = source.Contract;
            MTUContractReference b = target.Contract;
            CompareDimension(checks, "MDC_UNIT", a == null ? "" : a.UnitId,
                b == null ? "" : b.UnitId);
            CompareDimension(checks, "MDC_COORDINATE", a == null ? "" : a.CoordinateFrameId,
                b == null ? "" : b.CoordinateFrameId);
            CompareDimension(checks, "MDC_TIMEBASE", a == null ? "" : a.TimeBaseId,
                b == null ? "" : b.TimeBaseId);
            if ((a != null && a.Calibration != null) ||
                (b != null && b.Calibration != null))
            {
                checks.Add(new MDCConstraintOutcome("MDC_CALIBRATION",
                    a != null && b != null && object.Equals(a.Calibration, b.Calibration)
                        ? MDCVerdict.Allowed : MDCVerdict.Unknown,
                    "calibration identity must be confirmed by its professional owner"));
            }
            return new MDCAssessment(checks);
        }

        /// <summary>只合并已经求出的纯规则结论；Hard Fail优先于Unknown与软目标。</summary>
        public MDCAssessment AssessHardConstraints(IEnumerable<MDCConstraintOutcome> checks)
        { return new MDCAssessment(checks); }

        /// <summary>仅按当前输出端口作确定性的静态候选筛选，不评分、不绑定Runtime。</summary>
        public IReadOnlyList<MDCCandidate> FindCandidates(MPCPortDescriptor source,
            CxVisual.Mechanism.Orchestration.MOCCapabilityCatalog catalog,
            string query = null, int limit = 50)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (catalog == null) throw new ArgumentNullException("catalog");
            var results = new List<MDCCandidate>();
            foreach (var item in catalog.Search(query, limit))
            {
                var inputs = item.Definition.Ports.Where(p =>
                    p.Direction == MPCPortDirection.Input ||
                    p.Direction == MPCPortDirection.InOut).ToList();
                if (inputs.Count == 0)
                {
                    results.Add(new MDCCandidate(item.Definition.Id, null,
                        new MDCAssessment(new [] { new MDCConstraintOutcome(
                            "MDC_NO_INPUT", MDCVerdict.Rejected,
                            "capability declares no input ports") })));
                    continue;
                }
                MDCAssessment selected = null;
                MPCPortDescriptor selectedPort = null;
                foreach (var input in inputs)
                {
                    MDCAssessment assessment = AssessConnection(source, input);
                    if (selected == null ||
                        Rank(assessment.Verdict) > Rank(selected.Verdict))
                    {
                        selected = assessment;
                        selectedPort = input;
                    }
                    if (assessment.Verdict == MDCVerdict.Allowed) break;
                }
                results.Add(new MDCCandidate(item.Definition.Id, selectedPort, selected));
            }
            return new ReadOnlyCollection<MDCCandidate>(results);
        }

        private static int Rank(MDCVerdict verdict)
        {
            return verdict == MDCVerdict.Allowed ? 3 :
                verdict == MDCVerdict.Unknown ? 2 : 1;
        }

        private static MDCConstraintOutcome Check(string id, bool ok, string reason)
        { return new MDCConstraintOutcome(id, ok ? MDCVerdict.Allowed : MDCVerdict.Rejected, reason); }

        private static void CompareDimension(ICollection<MDCConstraintOutcome> checks,
            string id, string left, string right)
        {
            left = left ?? ""; right = right ?? "";
            MDCVerdict verdict = left.Length == 0 && right.Length == 0
                ? MDCVerdict.Allowed
                : left.Length == 0 || right.Length == 0
                    ? MDCVerdict.Unknown
                    : string.Equals(left, right, StringComparison.Ordinal)
                        ? MDCVerdict.Allowed : MDCVerdict.Rejected;
            checks.Add(new MDCConstraintOutcome(id, verdict,
                verdict == MDCVerdict.Unknown ? "unit/frame declaration missing" :
                verdict == MDCVerdict.Rejected ? "unit/frame mismatch; explicit conversion required" : ""));
        }
    }
}
