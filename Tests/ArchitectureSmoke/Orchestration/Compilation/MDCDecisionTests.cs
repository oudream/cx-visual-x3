using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

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
            MultiObjectiveSelection();
            RuntimeBindingContractConsistency();
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

        private static MDCMeasuredMetric Measure(string id, double value, string unit,
            MDCMetricEvidence evidence = MDCMetricEvidence.Measured, string proof = "sample-1")
        { return new MDCMeasuredMetric(id, value, unit, evidence, proof); }

        private static void MultiObjectiveSelection()
        {
            var goals = new[] {
                new MDCObjective("latency", "ms", MDCObjectiveDirection.Minimize, 0),
                new MDCObjective("quality", "score", MDCObjectiveDirection.Maximize, 1)
            };
            var allowed = new MDCAssessment(new MDCConstraintOutcome[0]);
            var denied = new MDCAssessment(new[] {
                new MDCConstraintOutcome("GPU_BUDGET", MDCVerdict.Rejected, "memory exceeded")
            });
            var options = new[] {
                new MDCOption("gpu", allowed, new[] {
                    Measure("latency", 20, "ms"), Measure("quality", 0.95, "score") }),
                new MDCOption("cpu", allowed, new[] {
                    Measure("latency", 50, "ms"), Measure("quality", 0.99, "score") }),
                new MDCOption("unsafe", denied, new[] {
                    Measure("latency", 1, "ms"), Measure("quality", 1, "score") }),
                new MDCOption("estimated", allowed, new[] {
                    Measure("latency", 5, "ms", MDCMetricEvidence.Estimated),
                    Measure("quality", 0.999, "score") })
            };
            var comparison = new MDCChoiceService().Compare(options, goals);
            Check(comparison.Candidates.Count == 4 && comparison.Pareto.Count == 2 &&
                comparison.Selected.Option.Id == "gpu", "Pareto/lexicographic selection wrong");
            Check(comparison.Candidates.Single(x => x.Option.Id == "unsafe").Verdict ==
                MDCVerdict.Rejected, "HardFail must never be rescued by attractive goals");
            Check(comparison.Candidates.Single(x => x.Option.Id == "estimated").Verdict ==
                MDCVerdict.Unknown, "Estimated evidence must not be selected as measured");
            var missing = new MDCChoiceService().Compare(new[] {
                new MDCOption("missing", allowed, new[] { Measure("quality", 1, "score") })
            }, goals);
            Check(missing.Selected == null && missing.Pareto.Count == 0 &&
                missing.Candidates[0].ReasonCodes.Contains("MDC_METRIC_MISSING:latency"),
                "missing evidence should yield no automatic selection");
            var unit = new MDCChoiceService().Compare(new[] {
                new MDCOption("wrongunit", allowed, new[] {
                    Measure("latency", 5, "seconds"), Measure("quality", 0.99, "score") })
            }, goals);
            Check(unit.Selected == null && unit.Candidates[0].Verdict == MDCVerdict.Unknown,
                "incomparable units cannot be ranked");
            var tie = new MDCChoiceService().Compare(new[] {
                new MDCOption("z", allowed, new[] {
                    Measure("latency", 20, "ms"), Measure("quality", 0.95, "score") }),
                new MDCOption("a", allowed, new[] {
                    Measure("latency", 20, "ms"), Measure("quality", 0.95, "score") })
            }, goals);
            Check(tie.Pareto.Count == 2 && tie.Selected.Option.Id == "a",
                "equal metrics need deterministic capability id tie break");
        }
        private static void RuntimeBindingContractConsistency()
        {
            var inspector = new MOCBindingContractInspector();
            var staticPorts = new MOCCapabilityDescriptor("compute.sample", "Sample", "Compute",
                new[] {
                    Port("Value", MPCPortDirection.Input, "int"),
                    Port("Result", MPCPortDirection.Output, "long")
                });
            int executed = 0;
            var match = new MRKResolvedCapability((ctx,ct) => {
                executed++;
                return Task.FromResult((object)5L);
            }, new MRKExecutionOutput(typeof(long)),
                dataOutputs: new Dictionary<string,Type> { { "Result", typeof(long) } },
                inputs: new [] { new MRKCapabilityInput("Value", typeof(int)) });
            // The default static port is optional; the runtime contract is required:
            // this is a real contract mismatch rather than a silent success.
            var requiredMismatch = inspector.Assess(staticPorts, match);
            Check(requiredMismatch.Verdict == MDCVerdict.Rejected &&
                requiredMismatch.ReasonCodes.Contains("MDC_BIND_REQUIRED_MISMATCH:Value"),
                "runtime-only required input should fail closed");
            var declaredRequired = new MOCCapabilityDescriptor("compute.required", "Sample", "Compute",
                new [] {
                    new MPCPortDescriptor("Value", MPCPortDirection.Input, "int",
                        MPCPortKind.Data, required:true),
                    Port("Result", MPCPortDirection.Output, "long")
                });
            Check(inspector.Assess(declaredRequired, match).Verdict == MDCVerdict.Allowed,
                "typed input/output contract should validate without execution");
            var bad = new MRKResolvedCapability((ctx,ct)=>Task.FromResult((object)5L),
                new MRKExecutionOutput(typeof(long)),
                dataOutputs:new Dictionary<string,Type>{{"Result",typeof(int)}},
                inputs:new[]{new MRKCapabilityInput("Value",typeof(double))});
            var mismatch=inspector.Assess(declaredRequired,bad);
            Check(mismatch.Verdict==MDCVerdict.Rejected &&
                mismatch.ReasonCodes.Contains("MDC_BIND_TYPE_MISMATCH:INPUT:Value") &&
                mismatch.ReasonCodes.Contains("MDC_BIND_TYPE_MISMATCH:OUTPUT:Result"),
                "static TypeId / runtime CLR divergence must be reported");
            var outputOnly=new MRKResolvedCapability((ctx,ct)=>Task.FromResult((object)5L),
                new MRKExecutionOutput(typeof(long)),
                inputs:new[]{new MRKCapabilityInput("Value",typeof(int))});
            Check(inspector.Assess(declaredRequired,outputOnly).Verdict==MDCVerdict.Unknown,
                "StepResult does not prove a named Data.Return contract");
            var extra=new MRKResolvedCapability((ctx,ct)=>Task.FromResult((object)5L),
                new MRKExecutionOutput(typeof(long)),
                dataOutputs:new Dictionary<string,Type>{{"Result",typeof(long)}},
                inputs:new[]{new MRKCapabilityInput("Value",typeof(int)),
                    new MRKCapabilityInput("Undeclared",typeof(bool))});
            Check(inspector.Assess(declaredRequired,extra).ReasonCodes.Contains(
                "MDC_BIND_UNDECLARED_INPUT:Undeclared"),"unpublished runtime input must fail");
            var vision = new CxVisionAcceptedRegionMeasureProvider();
            var descriptor = vision.DescribeCapabilities().Single();
            MRKResolvedCapability resolved;
            Check(vision.TryResolve(CxVisionAcceptedRegionMeasureProvider.CapabilityId,out resolved),
                "real Vision provider missing");
            var unmapped=inspector.Assess(descriptor,resolved);
            Check(unmapped.Verdict==MDCVerdict.Unknown &&
                unmapped.ReasonCodes.Contains("MDC_BIND_TYPE_UNKNOWN:INPUT:Inspection"),
                "unregistered professional semantic TypeId must remain Unknown");
            var mapped=inspector.Assess(descriptor,resolved,
                new Dictionary<string,Type> {
                    { CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                        typeof(CxVisionInspectionResult) }
                });
            Check(mapped.Verdict==MDCVerdict.Unknown &&
                mapped.ReasonCodes.Contains("MDC_BIND_OBJECT_UNVERIFIED:INPUT:Inspection"),
                "existing broad object contract needs explicit adapter evidence");

            // A real professional software provider can prove the same MPC/CLR contract
            // without starting MRK or executing pixels. Only domain semantic output needs
            // an explicit trusted type mapping, not a second registration database.
            var threshold = new CxVisionThresholdProvider(new CxVisionInspectionOptions(), 0);
            var thresholdDescription = threshold.DescribeCapabilities().Single();
            MRKResolvedCapability thresholdBinding;
            Check(threshold.TryResolve(CxVisionThresholdProvider.CapabilityId,
                out thresholdBinding), "threshold provider could not resolve");
            var thresholdContract = inspector.Assess(thresholdDescription, thresholdBinding,
                new Dictionary<string,Type> {
                    { CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                        typeof(CxVisionInspectionResult) }
                });
            Check(thresholdContract.Verdict == MDCVerdict.Allowed,
                "real Vision threshold static/CLR contract must agree");
            var catalog = new MOCCapabilityCatalog(new IMOCCapabilityProvider[] { threshold });
            var decisions = new MDCDecisionService().FindCandidates(
                Port("Raw", MPCPortDirection.Output, "byte[]"), catalog);
            Check(decisions.Count == 1 &&
                decisions[0].Assessment.Verdict == MDCVerdict.Allowed &&
                decisions[0].TargetPort.MemberId == "RawPixels",
                "real Vision capability discovery and static candidate mapping failed");

            Check(executed==0,"static contract checks must not execute professional code");
        }

    }
}
