using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MDCDecisionTests
    {
        private sealed class Provider : IMOCCapabilityProvider
        {
            private readonly MOCCapabilityDescriptor[] _descriptions;
            internal Provider(params MOCCapabilityDescriptor[] descriptions)
            { _descriptions = descriptions; }
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            { return _descriptions; }
        }
        private sealed class Resolver : IMRKCapabilityResolver
        {
            internal int Calls;
            public bool TryResolve(string id, out MRKResolvedCapability result)
            {
                Calls++;
                result = id == "vision.gray16" ? new MRKResolvedCapability(
                    (ctx, token) => Task.FromResult((object)42)) : null;
                return result != null;
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Decision: " + message);
        }
        private static MPCPortDescriptor Port(string id, MPCPortDirection direction,
            string type, string unit = "", string frame = "")
        {
            return new MPCPortDescriptor(id, direction, type, MPCPortKind.Data,
                MPCPortMultiplicity.One,
                new MTUContractReference(unit, frame));
        }

        internal static void Run()
        {
            OfflineCatalogAndLegacyResolver();
            StaticConnectionIsFailClosed();
            CandidateGroupsAndNoExecution();
            HardConstraintAggregation();
            Console.WriteLine("PASS MDC Decision offline discovery/constraints/candidates");
        }
        private static void OfflineCatalogAndLegacyResolver()
        {
            var descriptions = new Provider(
                new MOCCapabilityDescriptor("vision.gray16", "Vision 16", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Input, "gray16") }),
                new MOCCapabilityDescriptor("vision.gray8", "Vision 8", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Input, "gray8") }));
            var offline = new MOCCapabilityCatalog(new[] { descriptions });
            Check(offline.All.Count == 2 &&
                offline.All.All(x => !x.AvailabilityKnown && !x.RuntimeAvailable),
                "offline metadata must not claim runtime availability");
            Resolver runtime = new Resolver();
            var bound = new MOCCapabilityCatalog(new[] { descriptions }, runtime);
            Check(runtime.Calls == 2 && bound.Find("vision.gray16").AvailabilityKnown &&
                bound.Find("vision.gray16").RuntimeAvailable &&
                bound.Find("vision.gray8").AvailabilityKnown &&
                !bound.Find("vision.gray8").RuntimeAvailable,
                "legacy resolver contract must be retained");
            Check(offline.Find("vision.gray16").Definition.Ports.Count == 1,
                "offline discovery lost normalized MPC ports");
            bool duplicated = false;
            try { new MOCCapabilityCatalog(new[] { descriptions, descriptions }); }
            catch (InvalidOperationException) { duplicated = true; }
            Check(duplicated, "duplicate capability id cannot silently override");
        }
        private static void StaticConnectionIsFailClosed()
        {
            var decision = new MDCDecisionService();
            var source = Port("output", MPCPortDirection.Output, "gray16", "mm", "camera");
            Check(decision.AssessConnection(source,
                Port("input", MPCPortDirection.Input, "gray16", "mm", "camera"))
                .Verdict == MDCVerdict.Allowed, "same type/unit/frame should pass static check");
            var mismatch = decision.AssessConnection(source,
                Port("input", MPCPortDirection.Input, "gray8", "mm", "camera"));
            Check(mismatch.Verdict == MDCVerdict.Rejected &&
                mismatch.ReasonCodes.Contains("MDC_PORT_TYPE"), "pixel type mismatch was accepted");
            var unitUnknown = decision.AssessConnection(source,
                Port("input", MPCPortDirection.Input, "gray16", "", "camera"));
            Check(unitUnknown.Verdict == MDCVerdict.Unknown &&
                unitUnknown.ReasonCodes.Contains("MDC_UNIT"), "missing unit was treated as allowed");
            Check(decision.AssessConnection(source,
                Port("input", MPCPortDirection.Input, "gray16", "cm", "camera"))
                .Verdict == MDCVerdict.Rejected, "unit conversion was silently inferred");
            Check(decision.AssessConnection(
                Port("bad", MPCPortDirection.Input, "gray16"), 
                Port("input", MPCPortDirection.Input, "gray16"))
                .Verdict == MDCVerdict.Rejected, "input-to-input should reject");
        }
        private static void CandidateGroupsAndNoExecution()
        {
            var provider = new Provider(
                new MOCCapabilityDescriptor("vision.gray16", "Vision 16", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Input, "gray16", "mm") }),
                new MOCCapabilityDescriptor("vision.gray8", "Vision 8", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Input, "gray8", "mm") }),
                new MOCCapabilityDescriptor("unknown.unit", "Unspecified", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Input, "gray16") }),
                new MOCCapabilityDescriptor("output.only", "Output", "Vision/Compute",
                    new[] { Port("image", MPCPortDirection.Output, "gray16") }));
            var catalog = new MOCCapabilityCatalog(new[] { provider });
            var service = new MDCDecisionService();
            var results = service.FindCandidates(
                Port("out", MPCPortDirection.Output, "gray16", "mm"), catalog, limit: 50);
            Check(results.Count == 4, "candidate discovery dropped rejected entries");
            Check(results.Single(x => x.CapabilityId == "vision.gray16").Assessment.Verdict ==
                MDCVerdict.Allowed, "compatible candidate was not shown");
            Check(results.Single(x => x.CapabilityId == "vision.gray8").Assessment.Verdict ==
                MDCVerdict.Rejected, "incompatible candidate was not rejected");
            Check(results.Single(x => x.CapabilityId == "unknown.unit").Assessment.Verdict ==
                MDCVerdict.Unknown, "missing professional context was not marked unknown");
            Check(results.Single(x => x.CapabilityId == "output.only").Assessment.ReasonCodes
                .Contains("MDC_NO_INPUT"), "capability lacking inputs should be explained");
        }
        private static void HardConstraintAggregation()
        {
            var service = new MDCDecisionService();
            var rejected = service.AssessHardConstraints(new[] {
                new MDCConstraintOutcome("resource", MDCVerdict.Rejected, "GPU over budget"),
                new MDCConstraintOutcome("permission", MDCVerdict.Unknown, "host check required")
            });
            Check(rejected.Verdict == MDCVerdict.Rejected, "HardFail must precede Unknown");
            var unknown = service.AssessHardConstraints(new[] {
                new MDCConstraintOutcome("permission", MDCVerdict.Unknown, "not authorized")
            });
            Check(unknown.Verdict == MDCVerdict.Unknown, "Unknown cannot become Allowed");
            bool dup = false;
            try { service.AssessHardConstraints(new[] {
                new MDCConstraintOutcome("x", MDCVerdict.Allowed, ""),
                new MDCConstraintOutcome("x", MDCVerdict.Allowed, "")
            }); }
            catch (ArgumentException) { dup = true; }
            Check(dup, "duplicate rule id must not silently override a rule");
        }
    }
}
