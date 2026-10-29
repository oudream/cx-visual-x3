using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>
    /// A professional Owner's explicitly supplied requirements, evidence and hard rules.
    /// Empty lists mean the Owner explicitly declares that category empty; absence
    /// of a whole evidence record is Unknown, never an automatic approval.
    /// </summary>
    public sealed class MDCProfessionalCandidateEvidence
    {
        public MDCProfessionalCandidateEvidence(string capabilityId,
            IEnumerable<MDCResourceRequirement> resources,
            IEnumerable<MDCMeasuredMetric> metrics,
            IEnumerable<MDCConstraintOutcome> hardConstraints)
        {
            if (string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("decision.profile.capability_id", "capabilityId");
            if (resources == null) throw new ArgumentNullException("resources");
            if (metrics == null) throw new ArgumentNullException("metrics");
            if (hardConstraints == null) throw new ArgumentNullException("hardConstraints");
            var needs = resources.ToList();
            var scores = metrics.ToList();
            var rules = hardConstraints.ToList();
            if (needs.Count > 128 || scores.Count > 128 || rules.Count > 128 ||
                needs.Any(x => x == null) || scores.Any(x => x == null) ||
                rules.Any(x => x == null))
                throw new ArgumentException("decision.profile.invalid_evidence");
            CapabilityId = capabilityId.Trim();
            Resources = new ReadOnlyCollection<MDCResourceRequirement>(needs);
            Metrics = new ReadOnlyCollection<MDCMeasuredMetric>(scores);
            HardConstraints = new ReadOnlyCollection<MDCConstraintOutcome>(rules);
        }
        public string CapabilityId { get; private set; }
        public IReadOnlyList<MDCResourceRequirement> Resources { get; private set; }
        public IReadOnlyList<MDCMeasuredMetric> Metrics { get; private set; }
        public IReadOnlyList<MDCConstraintOutcome> HardConstraints { get; private set; }
    }

    public sealed class MDCRecommendationResult
    {
        internal MDCRecommendationResult(MDCStampedCandidates candidates,
            MDCComparisonResult comparison, string inputsFingerprint)
        { Candidates = candidates; Comparison = comparison; InputsFingerprint = inputsFingerprint; }
        public MDCStampedCandidates Candidates { get; private set; }
        public MDCComparisonResult Comparison { get; private set; }
        public string InputsFingerprint { get; private set; }
        /// <summary>Require both version stamp and actual selected input/evidence fingerprint.</summary>
        public void RequireCurrent(MDCDecisionVersionStamp current,
            string currentInputsFingerprint)
        {
            Candidates.RequireCurrent(current);
            if (!StringComparer.Ordinal.Equals(InputsFingerprint, currentInputsFingerprint))
                throw new InvalidOperationException("decision.recommend.stale_inputs_or_evidence");
        }
    }

    /// <summary>
    /// D02+D03+D04+D07 facade: no device invocation, no second catalog and no
    /// rules/optimization engine. Host remains the authorization/resource Owner.
    /// </summary>
    public sealed class MDCRecommendationService
    {
        public MDCRecommendationResult Recommend(MPCPortDescriptor source,
            MOCCapabilityCatalog catalog, MDCDecisionVersionStamp stamp,
            IEnumerable<MDCObjective> objectives, IEnumerable<MDCResourceBudget> budgets,
            IEnumerable<MDCProfessionalCandidateEvidence> professionalEvidence,
            string query = null, int limit = 50)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (stamp == null) throw new ArgumentNullException("stamp");
            if (objectives == null) throw new ArgumentNullException("objectives");
            if (budgets == null) throw new ArgumentNullException("budgets");
            if (professionalEvidence == null)
                throw new ArgumentNullException("professionalEvidence");
            if (limit > 256 || limit < 1)
                throw new ArgumentOutOfRangeException("limit");

            var profiles = professionalEvidence.ToList();
            if (profiles.Count > 256 || profiles.Any(x => x == null) ||
                profiles.GroupBy(x => x.CapabilityId, StringComparer.Ordinal)
                    .Any(x => x.Count() != 1))
                throw new ArgumentException("decision.recommend.duplicate_or_invalid_evidence");

            var byCapability = profiles.ToDictionary(x => x.CapabilityId, StringComparer.Ordinal);
            foreach (var record in profiles)
                if (catalog.Find(record.CapabilityId) == null)
                    throw new ArgumentException(
                        "decision.recommend.evidence_for_unknown_capability:" + record.CapabilityId);
            var budgetSnapshot = budgets.ToList();
            var goals = objectives.ToList();
            var matched = new MDCDecisionService().FindCandidatesStamped(
                source, catalog, stamp, query, limit);
            var planner = new MDCResourceBudgetPlanner();
            var options = new List<MDCOption>();
            foreach (var candidate in matched.Candidates)
            {
                MDCProfessionalCandidateEvidence profile;
                if (!byCapability.TryGetValue(candidate.CapabilityId, out profile))
                {
                    var missing = candidate.Assessment.Outcomes.Concat(new [] {
                        new MDCConstraintOutcome("MDC_PROFESSIONAL_EVIDENCE_MISSING",
                            MDCVerdict.Unknown,"professional requirements were not supplied")
                    });
                    options.Add(new MDCOption(candidate.CapabilityId,
                        new MDCAssessment(missing), new MDCMeasuredMetric[0]));
                    continue;
                }
                var resourceAssessment = planner.Assess(budgetSnapshot, profile.Resources);
                var combined = candidate.Assessment.Outcomes.Concat(
                    resourceAssessment.Outcomes).Concat(profile.HardConstraints);
                options.Add(new MDCOption(candidate.CapabilityId,
                    new MDCAssessment(combined), profile.Metrics));
            }
            var compared = new MDCChoiceService().Compare(options, goals);
            string inputsFingerprint = ComputeInputsFingerprint(source, goals,
                budgetSnapshot, profiles, query, limit);
            return new MDCRecommendationResult(matched, compared, inputsFingerprint);
        }

        /// <summary>
        /// Recompute just before applying a recommendation to Core. The digest includes
        /// selected source, goals, resource snapshots and professional evidence, so a
        /// changed measurement/requirement invalidates even an unchanged project revision.
        /// Does not validate trust in an EvidenceId: the supplying Owner remains responsible.
        /// </summary>
        public static string ComputeInputsFingerprint(MPCPortDescriptor source,
            IEnumerable<MDCObjective> objectives, IEnumerable<MDCResourceBudget> budgets,
            IEnumerable<MDCProfessionalCandidateEvidence> evidence,
            string query = null, int limit = 50)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (objectives == null) throw new ArgumentNullException("objectives");
            if (budgets == null) throw new ArgumentNullException("budgets");
            if (evidence == null) throw new ArgumentNullException("evidence");
            var text = new StringBuilder();
            Add(text, source.MemberId); Add(text, source.TypeId);
            Add(text, ((int)source.Direction).ToString(CultureInfo.InvariantCulture));
            Add(text, ((int)source.Kind).ToString(CultureInfo.InvariantCulture));
            Add(text, ((int)source.Multiplicity).ToString(CultureInfo.InvariantCulture));
            Add(text, source.Required ? "1" : "0");
            Add(text, source.IsPublic ? "1" : "0");
            var contract = source.Contract;
            Add(text, contract == null ? "" : contract.UnitId);
            Add(text, contract == null ? "" : contract.CoordinateFrameId);
            Add(text, contract == null ? "" : contract.TimeBaseId);
            Add(text, query); Add(text, limit.ToString(CultureInfo.InvariantCulture));
            var goalList = objectives.ToList();
            var budgetList = budgets.ToList();
            var profileList = evidence.ToList();
            if (goalList.Count > 16 || budgetList.Count > 128 || profileList.Count > 256 ||
                goalList.Any(x => x == null) || budgetList.Any(x => x == null) ||
                profileList.Any(x => x == null))
                throw new ArgumentException("decision.recommend.fingerprint_input_budget");
            Add(text, "goals"); Add(text, goalList.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var goal in goalList.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                Add(text, goal.Id); Add(text, goal.Unit);
                Add(text, ((int)goal.Direction).ToString(CultureInfo.InvariantCulture));
                Add(text, goal.Priority.ToString(CultureInfo.InvariantCulture));
            }
            Add(text, "budgets"); Add(text, budgetList.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var resource in budgetList.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                Add(text, resource.Id); Add(text, resource.Unit);
                Add(text, resource.Capacity.ToString(CultureInfo.InvariantCulture));
                Add(text, resource.Reserved.ToString(CultureInfo.InvariantCulture));
                Add(text, resource.SnapshotId);
            }
            Add(text, "profiles"); Add(text, profileList.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var profile in profileList.OrderBy(x => x.CapabilityId, StringComparer.Ordinal))
            {
                Add(text, profile.CapabilityId);
                Add(text, "needs"); Add(text, profile.Resources.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var need in profile.Resources.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    Add(text, need.Id); Add(text, need.Unit);
                    Add(text, need.LowerBound.ToString(CultureInfo.InvariantCulture));
                    Add(text, need.UpperBound.ToString(CultureInfo.InvariantCulture));
                    Add(text, need.EvidenceId);
                }
                Add(text, "metrics"); Add(text, profile.Metrics.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var metric in profile.Metrics.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    Add(text, metric.Id); Add(text, metric.Unit);
                    Add(text, metric.Value.ToString("R", CultureInfo.InvariantCulture));
                    Add(text, ((int)metric.Evidence).ToString(CultureInfo.InvariantCulture));
                    Add(text, metric.EvidenceId);
                }
                Add(text, "rules"); Add(text, profile.HardConstraints.Count.ToString(CultureInfo.InvariantCulture));
                foreach (var rule in profile.HardConstraints.OrderBy(x => x.Id, StringComparer.Ordinal))
                {
                    Add(text, rule.Id); Add(text, ((int)rule.Verdict).ToString(
                        CultureInfo.InvariantCulture)); Add(text, rule.Reason);
                }
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static void Add(StringBuilder text, string value)
        {
            if (value == null) value = "";
            text.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            text.Append(':').Append(value);
        }

    }
}
