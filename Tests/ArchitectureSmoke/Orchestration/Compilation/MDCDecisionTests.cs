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
            StaticRuntimeAssemblyBoundary();
            OfflineCatalogAndLegacyResolver();
            StaticConnectionIsFailClosed();
            CandidateGroupsAndNoExecution();
            HardConstraintAggregation();
            MultiObjectiveSelection();
            ResourcePlanningFeedsSelection();
            CatalogFingerprintVersionAndImpact();
            EndToEndStaticRecommendation();
            ProjectCapabilityImpactUsesCoreSnapshot();
            RetrySafetyRequiresRealAcknowledgment();
            StaticWiringPreflightWithoutRuntime();
            StaticBranchScenarioExploration();
            StaticSourceManifestPreservesCoreRevisionAndClosure();
            ManifestRuntimeBindingHandoffAndSafety();
            VersionPinnedRuntimeHandoffRejectsDrift();
            DeclarativePlanPreservesV1V2Semantics();
            RuntimeBindingContractConsistency();
            Console.WriteLine("PASS MDC Decision offline discovery/constraints/candidates");
        }

        private static void StaticRuntimeAssemblyBoundary()
        {
            string decisionName = typeof(MDCDeclarativePlan).Assembly.GetName().Name;
            string runtimeName = typeof(MOCEngineeringCompiler).Assembly.GetName().Name;
            Check(decisionName == "CxVisual.Mechanism.Decision" &&
                runtimeName == "CxVisual.Mechanism.Runtime",
                "Decision static and MOC/MRK executable compiler must have distinct assembly owners");
            Check(typeof(MOCFlowBuilder).Assembly == typeof(MDCDeclarativePlan).Assembly &&
                typeof(MOCCapabilityCatalog).Assembly == typeof(MDCDeclarativePlan).Assembly,
                "Core editing facade and static catalog must stay with Decision");
            Check(typeof(MOCBindingContractInspector).Assembly ==
                typeof(MOCEngineeringCompiler).Assembly &&
                typeof(MOCProjectRelease).Assembly == typeof(MOCEngineeringCompiler).Assembly &&
                typeof(MOCCapabilityCatalogRuntimeFactory).Assembly ==
                typeof(MOCEngineeringCompiler).Assembly,
                "resolver/binder/release adapter must stay on Runtime-facing side");
            Check(!typeof(MDCDeclarativePlan).Assembly.GetReferencedAssemblies()
                .Any(x => x.Name == runtimeName),
                "Decision project must not reference the Runtime assembly");
            Check(typeof(MOCEngineeringCompiler).Assembly.GetReferencedAssemblies()
                .Any(x => x.Name == decisionName),
                "Runtime binder must reference the static Decision contract");
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
            var bound = MOCCapabilityCatalogRuntimeFactory.Create(new[] { descriptions }, runtime);
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

        private static void ResourcePlanningFeedsSelection()
        {
            var planner = new MDCResourceBudgetPlanner();
            var snapshot = new [] {
                new MDCResourceBudget("GPU_MEMORY","MiB",4096m,1024m,"gpu-capacity-v1")
            };
            MDCAssessment fits = planner.Assess(snapshot,new [] {
                new MDCResourceRequirement("GPU_MEMORY","MiB",1024m,3000m,"ct-upper-v1")
            });
            MDCAssessment exceeds = planner.Assess(snapshot,new [] {
                new MDCResourceRequirement("GPU_MEMORY","MiB",3500m,4000m,"ct-lower-v1")
            });
            MDCAssessment overlaps = planner.Assess(snapshot,new [] {
                new MDCResourceRequirement("GPU_MEMORY","MiB",2900m,3500m,"ct-bounds-v1")
            });
            Check(fits.Verdict == MDCVerdict.Allowed,
                "certified resource upper bound should be feasible");
            Check(exceeds.Verdict == MDCVerdict.Rejected,
                "certified lower bound exceeding available memory should reject");
            Check(overlaps.Verdict == MDCVerdict.Unknown,
                "overlapping demand interval must not be called feasible");
            Check(planner.Assess(new MDCResourceBudget[0],new [] {
                new MDCResourceRequirement("GPU_MEMORY","MiB",1m,2m,"proof")
            }).Verdict == MDCVerdict.Unknown, "missing resource must be unknown");
            Check(planner.Assess(snapshot,new [] {
                new MDCResourceRequirement("GPU_MEMORY","GB",1m,2m,"proof")
            }).Verdict == MDCVerdict.Unknown, "resource units cannot be guessed");
            Check(planner.Assess(snapshot,new [] {
                new MDCResourceRequirement("GPU_MEMORY","MiB",1m,2m,"")
            }).Verdict == MDCVerdict.Unknown, "no demand evidence cannot be authorized");
            var cpu = new MDCOption("cpu",new MDCAssessment(new MDCConstraintOutcome[0]),
                new[]{Measure("latency",80,"ms")});
            var gpu = new MDCOption("gpu",exceeds,
                new[]{Measure("latency",3,"ms")});
            var chosen = new MDCChoiceService().Compare(new[]{gpu,cpu},
                new[]{new MDCObjective("latency","ms",MDCObjectiveDirection.Minimize)});
            Check(chosen.Selected!=null && chosen.Selected.Option.Id=="cpu" &&
                chosen.Candidates.Single(x=>x.Option.Id=="gpu").Verdict==MDCVerdict.Rejected,
                "fast GPU candidate cannot override a certified resource rejection");
            bool duplicate=false;
            try {planner.Assess(new[]{snapshot[0],snapshot[0]},
                new[]{new MDCResourceRequirement("GPU_MEMORY","MiB",1m,2m,"proof")});}
            catch(ArgumentException){duplicate=true;}
            Check(duplicate,"duplicate Host resource budgets must be rejected");
        }

        private static void CatalogFingerprintVersionAndImpact()
        {
            var a = new MOCCapabilityDescriptor("p.compute", "Professional Compute", "Vision",
                new[] {
                    Port("In", MPCPortDirection.Input, "byte[]", "px"),
                    Port("Out", MPCPortDirection.Output, "long", "px")
                });
            var b = new MOCCapabilityDescriptor("p.measure", "Professional Measure", "CT",
                new[] { Port("Region", MPCPortDirection.Input, "object") });
            var ordered = new MDCStaticCatalogSnapshot(new[] { a, b });
            var reversed = new MDCStaticCatalogSnapshot(new[] { b, a });
            Check(ordered.Fingerprint.Length == 64 &&
                ordered.Fingerprint == reversed.Fingerprint,
                "catalog fingerprint must be stable across provider ordering");
            var samePortsReordered = new MOCCapabilityDescriptor("p.compute",
                "Professional Compute", "Vision",
                new[] {
                    Port("Out", MPCPortDirection.Output, "long", "px"),
                    Port("In", MPCPortDirection.Input, "byte[]", "px")
                });
            var equivalent = new MDCStaticCatalogSnapshot(new[] { b, samePortsReordered });
            Check(equivalent.Fingerprint == ordered.Fingerprint,
                "port enumeration order must not modify semantic fingerprint");
            var changed = new MOCCapabilityDescriptor("p.compute",
                "Professional Compute", "Vision",
                new[] {
                    Port("In", MPCPortDirection.Input, "byte[]", "mm"),
                    Port("Out", MPCPortDirection.Output, "long", "px")
                });
            var updated = new MDCStaticCatalogSnapshot(new[] { changed, b });
            Check(ordered.Fingerprint != updated.Fingerprint &&
                !ordered.DescriptorFingerprints["p.compute"].Equals(
                    updated.DescriptorFingerprints["p.compute"], StringComparison.Ordinal),
                "unit changes must invalidate capability snapshot");
            var impact = MDCCatalogImpactAnalyzer.Compare(ordered, updated);
            Check(impact.Count == 1 && impact[0].CapabilityId == "p.compute" &&
                impact[0].Kind == MDCCatalogChangeKind.Modified,
                "incremental impact should identify modified capability only");
            var added = new MDCStaticCatalogSnapshot(new[] { changed,
                new MOCCapabilityDescriptor("p.extra", "Extra", "Vision") });
            var addRemove = MDCCatalogImpactAnalyzer.Compare(updated, added);
            Check(addRemove.Count == 2 &&
                addRemove.Any(x => x.CapabilityId == "p.extra" &&
                    x.Kind == MDCCatalogChangeKind.Added) &&
                addRemove.Any(x => x.CapabilityId == "p.measure" &&
                    x.Kind == MDCCatalogChangeKind.Removed),
                "added and removed capabilities must be distinguished");
            var guid = Guid.NewGuid();
            var stamp = new MDCDecisionVersionStamp(guid, 5, ordered.Fingerprint,
                "policy-v1", "environment-42");
            stamp.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                ordered.Fingerprint, "policy-v1", "environment-42"));
            bool stale = false;
            try { stamp.RequireCurrent(new MDCDecisionVersionStamp(guid, 6,
                ordered.Fingerprint, "policy-v1", "environment-42")); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale, "Core revision changes must invalidate proposals");
            stale = false;
            try { stamp.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                updated.Fingerprint, "policy-v1", "environment-42")); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale, "catalog version changes must invalidate proposals");
            stale = false;
            try { stamp.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                ordered.Fingerprint, "policy-v2", "environment-42")); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale, "policy changes must invalidate proposals");
            stale = false;
            try { stamp.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                ordered.Fingerprint, "policy-v1", "environment-43")); }
            catch (InvalidOperationException) { stale = true; }
            Check(stale, "environment changes must invalidate proposals");
            bool duplicate = false;
            try { new MDCStaticCatalogSnapshot(new[] { a, a }); }
            catch (ArgumentException) { duplicate = true; }
            Check(duplicate, "duplicate capability identity must not be accepted");
            var fromCatalog = new MDCStaticCatalogSnapshot(
                new MOCCapabilityCatalog(new[] { new Provider(a, b) }));
            Check(fromCatalog.Fingerprint == ordered.Fingerprint,
                "offline catalog and direct static descriptors must use the same fingerprint");
            var catalog = new MOCCapabilityCatalog(new[] { new Provider(a, b) });
            var batch = new MDCDecisionService().FindCandidatesStamped(
                Port("source", MPCPortDirection.Output, "byte[]", "px"), catalog, stamp);
            Check(batch.Candidates.Count == 2 && batch.Version.Equals(stamp),
                "stamped selection must use the same offline capability catalog");
            batch.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                ordered.Fingerprint, "policy-v1", "environment-42"));
            bool prevented = false;
            try { batch.RequireCurrent(new MDCDecisionVersionStamp(guid, 5,
                ordered.Fingerprint, "policy-v1", "environment-new")); }
            catch (InvalidOperationException) { prevented = true; }
            Check(prevented, "stale candidate results must not be submitted to Core edits");
            prevented = false;
            try { new MDCDecisionService().FindCandidatesStamped(
                Port("source", MPCPortDirection.Output, "byte[]", "px"),
                new MOCCapabilityCatalog(new[] { new Provider(changed, b) }), stamp); }
            catch (InvalidOperationException) { prevented = true; }
            Check(prevented, "candidate query must reject catalog snapshot mismatch");

        }

        private static void EndToEndStaticRecommendation()
        {
            var provider = new Provider(
                new MOCCapabilityDescriptor("cpu", "CPU", "Compute", new[] {
                    Port("Pixels", MPCPortDirection.Input,"byte[]","px") }),
                new MOCCapabilityDescriptor("gpu", "GPU", "Compute", new[] {
                    Port("Pixels", MPCPortDirection.Input,"byte[]","px") }),
                new MOCCapabilityDescriptor("gpu.too.large", "GPU large", "Compute",new[] {
                    Port("Pixels", MPCPortDirection.Input,"byte[]","px") }),
                new MOCCapabilityDescriptor("no.evidence", "Unknown", "Compute",new[] {
                    Port("Pixels", MPCPortDirection.Input,"byte[]","px") }),
                new MOCCapabilityDescriptor("wrong.type", "Bad type", "Compute",new[] {
                    Port("Pixels", MPCPortDirection.Input,"string","px") }));
            var catalog = new MOCCapabilityCatalog(new[] { provider });
            var stamp = new MDCDecisionVersionStamp(Guid.NewGuid(), 11,
                new MDCStaticCatalogSnapshot(catalog).Fingerprint,
                "policy-3", "host-snapshot-v1");
            var profiles = new[] {
                new MDCProfessionalCandidateEvidence("cpu",
                    new MDCResourceRequirement[0],new[] { Measure("latency",70,"ms") },
                    new MDCConstraintOutcome[0]),
                new MDCProfessionalCandidateEvidence("gpu",new[] {
                    new MDCResourceRequirement("GPU_MEMORY","MiB",1024m,2048m,"gpu-proof") },
                    new[] { Measure("latency",20,"ms") },
                    new MDCConstraintOutcome[0]),
                new MDCProfessionalCandidateEvidence("gpu.too.large",new[] {
                    new MDCResourceRequirement("GPU_MEMORY","MiB",4000m,4600m,"gpu-proof") },
                    new[] { Measure("latency",1,"ms") },
                    new MDCConstraintOutcome[0]),
                new MDCProfessionalCandidateEvidence("wrong.type",
                    new MDCResourceRequirement[0],new[] { Measure("latency",0.1,"ms") },
                    new MDCConstraintOutcome[0])
            };
            var result = new MDCRecommendationService().Recommend(
                Port("out", MPCPortDirection.Output, "byte[]","px"),
                catalog, stamp,
                new[] { new MDCObjective("latency","ms",MDCObjectiveDirection.Minimize) },
                new[] { new MDCResourceBudget("GPU_MEMORY","MiB",4096m,1024m,"capacity") },
                profiles);
            Check(result.Candidates.Candidates.Count==5 &&
                result.Comparison.Selected != null &&
                result.Comparison.Selected.Option.Id=="gpu",
                "static facade must recommend feasible fastest capability");
            Check(result.Comparison.Candidates.Single(x =>
                x.Option.Id=="gpu.too.large").Verdict==MDCVerdict.Rejected,
                "hard GPU capacity cannot be overridden by latency ranking");
            Check(result.Comparison.Candidates.Single(x =>
                x.Option.Id=="wrong.type").Verdict==MDCVerdict.Rejected,
                "static TypeId mismatch cannot be overridden");
            Check(result.Comparison.Candidates.Single(x =>
                x.Option.Id=="no.evidence").Verdict==MDCVerdict.Unknown,
                "missing professional evidence cannot produce an automatic recommendation");
            var source = Port("out", MPCPortDirection.Output, "byte[]","px");
            var objectives = new[] {
                new MDCObjective("latency","ms",MDCObjectiveDirection.Minimize) };
            var budgets = new[] {
                new MDCResourceBudget("GPU_MEMORY","MiB",4096m,1024m,"capacity") };
            var fingerprint = MDCRecommendationService.ComputeInputsFingerprint(
                source, objectives, budgets, profiles);
            Check(result.InputsFingerprint == fingerprint,
                "recommendation must include objective/resource/evidence fingerprint");
            result.RequireCurrent(stamp, fingerprint);
            bool stale=false;
            try { result.RequireCurrent(new MDCDecisionVersionStamp(stamp.ProjectId,12,
                stamp.CatalogFingerprint,stamp.PolicyRevision,stamp.EnvironmentEpoch),
                fingerprint); }
            catch(InvalidOperationException) { stale=true; }
            Check(stale,"a previously selected option cannot apply to a new project revision");
            stale=false;
            var changedBudget = MDCRecommendationService.ComputeInputsFingerprint(source,
                objectives,new[] { new MDCResourceBudget("GPU_MEMORY","MiB",4096m,
                    3500m,"capacity") },profiles);
            try { result.RequireCurrent(stamp,changedBudget); }
            catch(InvalidOperationException) { stale=true; }
            Check(stale,"new resource reservation must invalidate previous recommendation");
            stale=false;
            var changedEvidence = new [] {
                profiles[0],
                new MDCProfessionalCandidateEvidence("gpu",new[] {
                    new MDCResourceRequirement("GPU_MEMORY","MiB",1024m,2048m,"gpu-proof") },
                    new[] { Measure("latency",99,"ms") },new MDCConstraintOutcome[0]),
                profiles[2], profiles[3]
            };
            try { result.RequireCurrent(stamp,
                MDCRecommendationService.ComputeInputsFingerprint(
                    source,objectives,budgets,changedEvidence)); }
            catch(InvalidOperationException) { stale=true; }
            Check(stale,"changed measured evidence must invalidate previous recommendation");
            bool duplicate=false;
            try { new MDCRecommendationService().Recommend(
                Port("out", MPCPortDirection.Output, "byte[]","px"),catalog,stamp,
                new[] { new MDCObjective("latency","ms",MDCObjectiveDirection.Minimize) },
                new MDCResourceBudget[0],new[] {profiles[0],profiles[0]}); }
            catch(ArgumentException) { duplicate=true; }
            Check(duplicate,"duplicate professional evidence must be rejected");
        }

        private static void ProjectCapabilityImpactUsesCoreSnapshot()
        {
            var session = MEMEngineering.Create("decision-impact");
            try
            {
                var v1 = session.AddPage("V1");
                var legacyBlock = MOLControlBlock.Action("vision.old");
                MOLLogicCodec.Set(session, v1.RootContent,
                    new MOLLogicDefinition(legacyBlock));
                var v2 = session.AddPage("V2");
                var wiredBlock = MOLControlBlock.Action("gpu.backend");
                MOLWiringCodec.Set(session, v2.RootContent,
                    new MOLWiringDefinition(wiredBlock.Id,
                        new[] { wiredBlock },
                        new Dictionary<Guid, IEnumerable<MPCPortDescriptor>> {
                            { wiredBlock.Id, new MPCPortDescriptor[0] }
                        }, new MOLLink[0]));
                var parent = session.AddPage("Parent");
                MOLLogicCodec.Set(session, parent.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(v1.RootContent.Id)));
                var unrelated = session.AddPage("Unrelated");
                MOLLogicCodec.Set(session, unrelated.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.Action("unrelated.compute")));
                var previous = new MDCStaticCatalogSnapshot(new[] {
                    new MOCCapabilityDescriptor("vision.old", "Old", "Vision"),
                    new MOCCapabilityDescriptor("gpu.backend", "GPU", "Compute")
                });
                var updated = new MDCStaticCatalogSnapshot(new[] {
                    new MOCCapabilityDescriptor("vision.old", "Updated", "Vision")
                });
                var changes = MDCCatalogImpactAnalyzer.Compare(previous, updated);
                var report = MDCProjectImpactAnalyzer.Analyze(session.GetSnapshot(), changes);
                Check(report.IsComplete && report.DirectBlocks.Count == 2,
                    "both legacy and V2 changed capabilities must map to real Core blocks");
                Check(report.DirectBlocks.Any(x => x.ContentId == v1.RootContent.Id &&
                    x.BlockId == legacyBlock.Id && x.Change == MDCCatalogChangeKind.Modified),
                    "legacy V1 BlockId must be preserved for user navigation");
                Check(report.DirectBlocks.Any(x => x.ContentId == v2.RootContent.Id &&
                    x.BlockId == wiredBlock.Id && x.Change == MDCCatalogChangeKind.Removed),
                    "V2 BlockId should identify removed capability use");
                Check(report.AffectedContentIds.Contains(parent.RootContent.Id),
                    "changed subflow must propagate to referring parent Content");
                Check(report.AffectedContentIds.Contains(v2.RootContent.Id),
                    "direct V2 content must be included");
                Check(report.AffectedContentIds.Count == 3,
                    "unrelated engineering content must not be marked affected");

                Check(!report.AffectedContentIds.Contains(unrelated.RootContent.Id),
                    "unrelated Content must not be included by inference");
                var none = MDCProjectImpactAnalyzer.Analyze(session.GetSnapshot(),
                    MDCCatalogImpactAnalyzer.Compare(previous,previous));
                Check(none.IsComplete && none.DirectBlocks.Count == 0 &&
                    none.AffectedContentIds.Count == 0,
                    "unchanged catalog must have no affected engineering nodes");
                var missing = session.AddPage("MissingInvoke");
                MOLLogicCodec.Set(session, missing.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(Guid.NewGuid())));
                var incomplete = MDCProjectImpactAnalyzer.Analyze(session.GetSnapshot(), changes);
                Check(!incomplete.IsComplete &&
                    incomplete.UnknownContentIds.Contains(missing.RootContent.Id),
                    "unresolvable Invoke target must report incomplete impact analysis");

            }
            finally { session.Close(true); }
        }

        private static void RetrySafetyRequiresRealAcknowledgment()
        {
            var decision = new MDCRecoverySafetyAssessor();
            var pure = decision.AssessRetry(MDCOperationEffect.Pure,
                MDCOperationReceipt.ConfirmedNoEffect, false, false);
            Check(pure.Verdict == MDCVerdict.Allowed,
                "pure computation should be statically eligible for Runtime-controlled retry");
            var unknownAck = decision.AssessRetry(MDCOperationEffect.NonIdempotentExternal,
                MDCOperationReceipt.AcknowledgmentUnknown, false, true);
            Check(unknownAck.Verdict == MDCVerdict.Rejected &&
                unknownAck.ReasonCodes.Contains("MDC_RETRY_RECONCILIATION_REQUIRED"),
                "unknown external command receipt cannot be treated as failed");
            var idemUnknownAck = decision.AssessRetry(MDCOperationEffect.IdempotentExternal,
                MDCOperationReceipt.AcknowledgmentUnknown, true, true);
            Check(idemUnknownAck.Verdict == MDCVerdict.Unknown,
                "declared idempotency cannot bypass an unknown device acknowledgement");
            var unauthorized = decision.AssessRetry(MDCOperationEffect.IdempotentExternal,
                MDCOperationReceipt.ConfirmedNoEffect, true, false);
            Check(unauthorized.Verdict == MDCVerdict.Unknown,
                "unapproved external operation cannot be automatically retried");
            var unproven = decision.AssessRetry(MDCOperationEffect.IdempotentExternal,
                MDCOperationReceipt.ConfirmedNoEffect, false, true);
            Check(unproven.Verdict == MDCVerdict.Unknown,
                "unverified idempotency cannot permit retry");
            var suitable = decision.AssessRetry(MDCOperationEffect.IdempotentExternal,
                MDCOperationReceipt.ConfirmedNoEffect, true, true);
            Check(suitable.Verdict == MDCVerdict.Allowed &&
                suitable.Outcomes[0].Id == "MDC_RETRY_POLICY_ELIGIBLE",
                "verified idempotent retry can be static policy eligible");
            var external = decision.AssessRetry(MDCOperationEffect.NonIdempotentExternal,
                MDCOperationReceipt.ConfirmedNoEffect, true, true);
            Check(external.Verdict == MDCVerdict.Unknown,
                "non-idempotent side effects need further professional approval");
            var already = decision.AssessRetry(MDCOperationEffect.Pure,
                MDCOperationReceipt.ConfirmedApplied, true, true);
            Check(already.Verdict == MDCVerdict.Rejected,
                "already completed operations are not a retry candidate");
            var notSent = decision.AssessRetry(MDCOperationEffect.IdempotentExternal,
                MDCOperationReceipt.NotSent, true, true);
            Check(notSent.Verdict == MDCVerdict.Rejected,
                "normal first dispatch must not be disguised as retry");
            var unknownEffect = decision.AssessRetry(MDCOperationEffect.Unknown,
                MDCOperationReceipt.ConfirmedNoEffect, true, true);
            Check(unknownEffect.Verdict == MDCVerdict.Unknown,
                "unknown side effects remain unknown");
        }

        private static void StaticWiringPreflightWithoutRuntime()
        {
            var block = MOLControlBlock.Action("professional.cpu");
            var port = new MPCPortDescriptor("Pixels", MPCPortDirection.Input, "byte[]",
                MPCPortKind.Data,MPCPortMultiplicity.One,required:true);
            var descriptor = new MOCCapabilityDescriptor("professional.cpu",
                "CPU", "Vision", new[] {port});
            var catalog = new MOCCapabilityCatalog(new[] { new Provider(descriptor) });
            Func<MPCPortDescriptor[],MOLWiringDefinition> make = ports =>
                new MOLWiringDefinition(block.Id,new[]{block},
                    new Dictionary<Guid,IEnumerable<MPCPortDescriptor>> {
                        {block.Id,ports}
                    },new MOLLink[0]);
            var preflight = new MDCStaticBuildPreflight();
            Check(preflight.Assess(make(new MPCPortDescriptor[] {port}),catalog,false).Verdict==MDCVerdict.Allowed,
                "identical original Core and professional port contracts should be compatible");
            var typeMismatch = preflight.Assess(make(new MPCPortDescriptor[] {
                new MPCPortDescriptor("Pixels",MPCPortDirection.Input,"string",
                    MPCPortKind.Data,MPCPortMultiplicity.One,required:true)}),catalog,false);
            Check(typeMismatch.Verdict==MDCVerdict.Rejected &&
                typeMismatch.ReasonCodes.Any(x=>x.StartsWith(
                    "MDC_PREFLIGHT_PORT_MISMATCH:",StringComparison.Ordinal)),
                "static preflight must reject type mismatch without invoking MRK");
            var missing = preflight.Assess(make(new MPCPortDescriptor[0]),catalog,false);
            Check(missing.Verdict==MDCVerdict.Rejected &&
                missing.ReasonCodes.Any(x=>x.StartsWith(
                    "MDC_PREFLIGHT_PORT_NOT_DECLARED:",StringComparison.Ordinal)),
                "required professional port cannot silently disappear");
            var offlineMissing = preflight.Assess(make(new MPCPortDescriptor[] {port}),
                new MOCCapabilityCatalog(new[] { new Provider() }),false);
            Check(offlineMissing.Verdict==MDCVerdict.Unknown,
                "missing capability descriptor must remain Unknown");

            var project = MEMEngineering.Create("decision-preflight-real-builder");
            try
            {
                var page = project.AddPage("Main");
                Guid realBlockId = Guid.Empty;
                MOCFlowBuilder.Edit(project,page.RootContent,
                    builder => { realBlockId = builder.Step(descriptor); });
                var realWiring = MOLWiringCodec.Get(
                    project.GetSnapshot().Find(page.RootContent.Id));
                var actual = preflight.Assess(realWiring,catalog,false);
                Check(actual.Verdict==MDCVerdict.Allowed,
                    "real MOCFlowBuilder Start/Done controls are not extra professional data ports");
                MOCFlowBuilder.Edit(project,page.RootContent,builder => {
                    builder.AddPort(realBlockId,new MPCPortDescriptor("Extra",
                        MPCPortDirection.Input,"double",MPCPortKind.Data));
                });
                var extended = preflight.Assess(MOLWiringCodec.Get(
                    project.GetSnapshot().Find(page.RootContent.Id)),catalog,false);
                Check(extended.Verdict==MDCVerdict.Unknown &&
                    extended.ReasonCodes.Any(x=>x.StartsWith(
                        "MDC_PREFLIGHT_EXTRA_PORT:",StringComparison.Ordinal)),
                    "unknown professional Data port needs explicit provider schema confirmation");
            }
            finally { project.Close(true); }

        }

        private static void StaticBranchScenarioExploration()
        {
            var session = MEMEngineering.Create("decision-static-path");
            try
            {
                var page = session.AddPage("Main");
                Guid decide = Guid.Empty, yes = Guid.Empty, no = Guid.Empty, join = Guid.Empty;
                MOCFlowBuilder.Edit(session, page.RootContent, builder => {
                    decide = builder.Step("decide");
                    yes = builder.Step("yes");
                    no = builder.Step("no");
                    join = builder.Step("join");
                    builder.ConnectConditional(decide,"Done",yes,"Start","bool:true");
                    builder.ConnectConditional(decide,"Done",no,"Start","bool:false");
                    builder.Connect(yes,"Done",join,"Start");
                    builder.Connect(no,"Done",join,"Start");
                });
                MOLWiringDefinition wiring = MOLWiringCodec.Get(
                    session.GetSnapshot().Find(page.RootContent.Id));
                var explorer = new MDCStaticPathExplorer();
                var unknown = explorer.Explore(wiring);
                Check(unknown.PossibleBlockIds.Count == 4 &&
                    unknown.UndecidedBranchBlockIds.Count == 1 &&
                    unknown.UndecidedBranchBlockIds[0] == decide &&
                    !unknown.HasCompleteAssumptions,
                    "no scenario guard should expand all possible branches without claiming certainty");
                var chosen = explorer.Explore(wiring,
                    new Dictionary<Guid,string> { {decide,"bool:true"} });
                Check(chosen.HasCompleteAssumptions &&
                    chosen.PossibleBlockIds.Count == 3 &&
                    chosen.PossibleBlockIds.Contains(yes) &&
                    chosen.PossibleBlockIds.Contains(join) &&
                    !chosen.PossibleBlockIds.Contains(no),
                    "true guard assumption should narrow possible route while retaining join");
                var alternative = explorer.Explore(wiring,
                    new Dictionary<Guid,string> { {decide,"bool:false"} });
                Check(alternative.HasCompleteAssumptions &&
                    alternative.PossibleBlockIds.Contains(no) &&
                    !alternative.PossibleBlockIds.Contains(yes),
                    "false guard assumption must be distinct");
                var invalid = explorer.Explore(wiring,
                    new Dictionary<Guid,string> { {decide,"case:UNKNOWN"} });
                Check(!invalid.HasCompleteAssumptions &&
                    invalid.InvalidAssumptionBlockIds.Contains(decide),
                    "unknown branch label cannot be treated as valid simulation");
                var unreachable = explorer.Explore(wiring,
                    new Dictionary<Guid,string> { {no,"bool:true"} });
                Check(!unreachable.HasCompleteAssumptions &&
                    unreachable.InvalidAssumptionBlockIds.Contains(no),
                    "assumptions on unconditional blocks cannot be accepted");
            }
            finally { session.Close(true); }
        }

        private static void StaticSourceManifestPreservesCoreRevisionAndClosure()
        {
            var session=MEMEngineering.Create("decision-source-manifest");
            try
            {
                var child=session.AddContent(session.Project,"Reconstruction");
                var childStep=MOLControlBlock.Action("ct.reconstruct");
                MOLWiringCodec.Set(session,child,new MOLWiringDefinition(
                    childStep.Id,new[]{childStep},
                    new Dictionary<Guid,IEnumerable<MPCPortDescriptor>> {
                        {childStep.Id,new MPCPortDescriptor[0]}
                    },new MOLLink[0]));
                var parent=session.AddPage("Inspection");
                var parentStep=MOLControlBlock.Action("vision.acquire");
                var call=MOLControlBlock.InvokeContent(child.Id);
                MOLLogicCodec.Set(session,parent.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.Sequence(parentStep,call)));
                session.Edit("正式引用CT子流程",edit=>{
                    edit.Reference(parent.RootContent,child,"CT子流程");
                    return true;
                }).RequireValue();
                var catalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("ct.reconstruct","CT Reconstruction","CT3D"),
                    new MOCCapabilityDescriptor("vision.acquire","Acquire","Vision")
                });
                var source=session.GetSnapshot();
                var builder=new MDCStaticSourceManifestBuilder();
                var manifest=builder.Create(source,parent.RootContent.Id,catalog);
                var again=builder.Create(source,parent.RootContent.Id,catalog);
                Check(manifest.Contents.Count==2 && manifest.HasCompleteStaticMetadata &&
                    manifest.Fingerprint.Length==64 && manifest.Fingerprint==again.Fingerprint,
                    "deterministic source manifest should track the original Core V1/V2 closure");
                Check(manifest.ProjectId==source.ProjectId &&
                    manifest.EngineeringRevision==source.Revision &&
                    manifest.RootContentId==parent.RootContent.Id,
                    "static manifest must preserve authoritative Core project/version/root IDs");
                var root=manifest.Contents.Single(x=>x.ContentId==parent.RootContent.Id);
                var sub=manifest.Contents.Single(x=>x.ContentId==child.Id);
                Check(root.ReferencedContentIds.Count==1 &&
                    root.ReferencedContentIds[0]==child.Id &&
                    root.CapabilityUses.Count==1 &&
                    root.CapabilityUses[0].BlockId==parentStep.Id &&
                    root.CapabilityUses[0].CapabilityId=="vision.acquire",
                    "V1 invoke must be a source reference, never a cloned runtime step");
                Check(sub.CapabilityUses.Count==1 &&
                    sub.CapabilityUses[0].BlockId==childStep.Id &&
                    sub.CapabilityUses[0].CapabilityId=="ct.reconstruct",
                    "V2 original action identity must survive source extraction");
                manifest.RequireCurrent(session.GetSnapshot(),catalog);
                var partialCatalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("vision.acquire","Acquire","Vision")
                });
                var unknown=builder.Create(session.GetSnapshot(),
                    parent.RootContent.Id,partialCatalog);
                Check(!unknown.HasCompleteStaticMetadata &&
                    unknown.UnknownCapabilityIds.SequenceEqual(new[]{"ct.reconstruct"}),
                    "unknown professional metadata must remain visible and cannot authorize binding");
                bool mismatch=false;
                try {manifest.RequireCurrent(session.GetSnapshot(),partialCatalog);}
                catch(InvalidOperationException) {mismatch=true;}
                Check(mismatch,"catalog schema change must invalidate a static source manifest");


                var v2Parent=session.AddPage("WiredCaller");
                session.Edit("正式引用V2子流程",edit=>{
                    edit.Reference(v2Parent.RootContent,child,"V2CT");
                    return true;
                }).RequireValue();
                Guid v2Invoke=Guid.Empty;
                MOCFlowBuilder.Edit(session,v2Parent.RootContent,flow=>{
                    v2Invoke=flow.InvokeContent(child);
                });
                var v2Manifest=builder.Create(session.GetSnapshot(),
                    v2Parent.RootContent.Id,catalog);
                Check(v2Manifest.Contents.Count==2 &&
                    v2Manifest.HasCompleteStaticMetadata &&
                    v2Manifest.Contents.Single(x=>x.ContentId==v2Parent.RootContent.Id)
                        .ReferencedContentIds.SequenceEqual(new[]{child.Id}) &&
                    !v2Manifest.Contents.Single(x=>x.ContentId==v2Parent.RootContent.Id)
                        .CapabilityUses.Any(x=>x.BlockId==v2Invoke),
                    "real MOCFlowBuilder V2 Invoke must preserve target Content without a fake provider action");

                var updatedStep=MOLControlBlock.Action("ct.optimized");
                MOLWiringCodec.Set(session,child,new MOLWiringDefinition(
                    updatedStep.Id,new[]{updatedStep},
                    new Dictionary<Guid,IEnumerable<MPCPortDescriptor>> {
                        {updatedStep.Id,new MPCPortDescriptor[0]}
                    },new MOLLink[0]));
                mismatch=false;
                try {manifest.RequireCurrent(session.GetSnapshot(),catalog);}
                catch(InvalidOperationException) {mismatch=true;}
                Check(mismatch,"child Content schema/revision must invalidate a parent source manifest");

                var orphan=session.AddPage("Orphan");
                MOLLogicCodec.Set(session,orphan.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(Guid.NewGuid())));
                bool missing=false;
                try {builder.Create(session.GetSnapshot(),orphan.RootContent.Id,catalog);}
                catch(InvalidOperationException) {missing=true;}
                Check(missing,"missing referenced Content cannot become an executable source bundle");

                var noRef=session.AddPage("MissingReference");
                MOLLogicCodec.Set(session,noRef.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(child.Id)));
                bool missingReference=false;
                try {builder.Create(session.GetSnapshot(),noRef.RootContent.Id,catalog);}
                catch(InvalidOperationException ex)
                {
                    missingReference=ex.Message.StartsWith(
                        "decision.source_manifest.missing_content_reference:",
                        StringComparison.Ordinal);
                }
                Check(missingReference,
                    "existing subflow without authoritative Core ContentReference must be rejected");

                var a=session.AddPage("CycleA");
                var b=session.AddPage("CycleB");
                MOLLogicCodec.Set(session,a.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(b.RootContent.Id)));
                MOLLogicCodec.Set(session,b.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.InvokeContent(a.RootContent.Id)));
                bool cycle=false;
                try
                {
                    session.Edit("引用B",edit=>{
                        edit.Reference(a.RootContent,b.RootContent,"B");return true;
                    }).RequireValue();
                    session.Edit("引用A",edit=>{
                        edit.Reference(b.RootContent,a.RootContent,"A");return true;
                    }).RequireValue();
                    builder.Create(session.GetSnapshot(),a.RootContent.Id,catalog);
                }
                catch(InvalidOperationException) {cycle=true;}
                Check(cycle,
                    "Core reference validator or static traversal must reject cyclic subflow references");
            }
            finally {session.Close(true);}
        }

        private sealed class ProbeResolver : IMRKCapabilityResolver
        {
            internal int Calls;
            internal int Executions;
            internal bool IncludeSignature = true;
            internal bool MismatchedInput;
            public bool TryResolve(string id,out MRKResolvedCapability binding)
            {
                Calls++;
                if (id!="vision.gray16") { binding=null; return false; }
                binding=new MRKResolvedCapability((ctx,ct)=>{
                    Executions++;
                    return Task.FromResult((object)42);
                }, inputs: IncludeSignature
                    ? (MismatchedInput
                        ? new[]{new MRKCapabilityInput("unexpected",typeof(int))}
                        : new MRKCapabilityInput[0])
                    : null);
                return true;
            }
        }

        private sealed class VersionedProbeResolver : IMRKVersionedCapabilityResolver
        {
            internal readonly ProbeResolver Binding=new ProbeResolver();
            internal string Version="vision.gray16/algorithm:1";
            internal bool ChangeDuringResolve;
            internal bool MissingVersion;
            internal int VersionChecks;

            public bool TryGetCapabilityVersion(string id,out string version)
            {
                VersionChecks++;
                version=!MissingVersion && id=="vision.gray16" ? Version : null;
                return version!=null;
            }

            public bool TryResolve(string id,out MRKResolvedCapability binding)
            {
                bool success=Binding.TryResolve(id,out binding);
                if(ChangeDuringResolve) Version="vision.gray16/algorithm:2";
                return success;
            }
        }

        private static void VersionPinnedRuntimeHandoffRejectsDrift()
        {
            var session=MEMEngineering.Create("decision-version-fenced-handoff");
            try
            {
                var content=session.AddPage("Main").RootContent;
                var action=MOLControlBlock.Action("vision.gray16");
                MOLLogicCodec.Set(session,content,new MOLLogicDefinition(action));
                var snapshot=session.GetSnapshot();
                var catalog=new MDCStaticCatalogSnapshot(new[] {
                    new MOCCapabilityDescriptor("vision.gray16","Gray16","Vision")
                });
                var plan=new MDCDeclarativePlanCompiler().Compile(
                    snapshot,content.Id,catalog);
                var binder=new MOCManifestRuntimeBinding();
                var expected=new Dictionary<string,string>(StringComparer.Ordinal) {
                    {"vision.gray16","vision.gray16/algorithm:1"}
                };
                var stable=new VersionedProbeResolver();
                var compiled=binder.Bind(snapshot,plan,catalog,stable,
                    expectedCapabilityVersions:expected);
                Check(compiled!=null && compiled.SourceMap.Find(action.Id).Count==1 &&
                    stable.Binding.Calls==1 && stable.Binding.Executions==0 &&
                    stable.VersionChecks>=2,
                    "versioned handoff must pin the same capability across validation and lowering");

                var missingPin=new VersionedProbeResolver();
                bool incomplete=false;
                try {binder.Bind(snapshot,plan,catalog,missingPin,
                    expectedCapabilityVersions:new Dictionary<string,string>());}
                catch(InvalidOperationException ex)
                {incomplete=ex.Message=="moc.bind.version_pin_incomplete";}
                Check(incomplete && missingPin.Binding.Calls==0,
                    "incomplete Host version closure must fail before resolving a provider");

                var staleVersion=new VersionedProbeResolver();
                bool mismatch=false;
                try {binder.Bind(snapshot,plan,catalog,staleVersion,
                    expectedCapabilityVersions:new Dictionary<string,string> {
                        {"vision.gray16","vision.gray16/algorithm:old"}
                    });}
                catch(InvalidOperationException ex)
                {mismatch=ex.Message.StartsWith("moc.bind.provider_version_mismatch:",StringComparison.Ordinal);}
                Check(mismatch && staleVersion.Binding.Calls==0,
                    "stale professional algorithm version must fail before TryResolve");

                var unknownVersion=new VersionedProbeResolver{MissingVersion=true};
                bool unknown=false;
                try {binder.Bind(snapshot,plan,catalog,unknownVersion,
                    expectedCapabilityVersions:expected);}
                catch(InvalidOperationException ex)
                {unknown=ex.Message.StartsWith("moc.bind.provider_version_unavailable:",StringComparison.Ordinal);}
                Check(unknown && unknownVersion.Binding.Calls==0,
                    "unverifiable Host-pinned version cannot be treated as valid");

                var unversioned=new ProbeResolver();
                bool required=false;
                try {binder.Bind(snapshot,plan,catalog,unversioned,
                    expectedCapabilityVersions:expected);}
                catch(InvalidOperationException ex)
                {required=ex.Message=="moc.bind.versioned_resolver_required";}
                Check(required && unversioned.Calls==0,
                    "Host strict pin must reject a versionless Resolver without execution");

                var swapped=new VersionedProbeResolver{ChangeDuringResolve=true};
                bool changed=false;
                try {binder.Bind(snapshot,plan,catalog,swapped,
                    expectedCapabilityVersions:expected);}
                catch(InvalidOperationException ex)
                {changed=ex.Message.StartsWith("moc.bind.provider_version_changed:",StringComparison.Ordinal);}
                Check(changed && swapped.Binding.Calls==1 &&
                    swapped.Binding.Executions==0 && swapped.VersionChecks>=2,
                    "algorithm hot-swap while compiling must not return an inconsistent bound Plan");
            }
            finally {session.Close(true);}
        }

        private static void ManifestRuntimeBindingHandoffAndSafety()
        {
            var session=MEMEngineering.Create("decision-binding-handoff");
            try
            {
                var content=session.AddPage("Main").RootContent;
                var action=MOLControlBlock.Action("vision.gray16");
                MOLLogicCodec.Set(session,content,new MOLLogicDefinition(action));
                var catalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("vision.gray16","Vision 16","Vision")
                });
                var snapshot=session.GetSnapshot();
                var staticManifest=new MDCStaticSourceManifestBuilder().Create(
                    snapshot,content.Id,catalog);
                var resolver=new ProbeResolver();
                var binder=new MOCManifestRuntimeBinding();
                var compiled=binder.Bind(snapshot,staticManifest,catalog,resolver);
                var staticPlan=new MDCDeclarativePlanCompiler().Compile(
                    snapshot,content.Id,catalog);
                var planResolver=new ProbeResolver();
                var lowered=binder.Bind(snapshot,staticPlan,catalog,planResolver);
                Check(lowered!=null && lowered.SourceMap.Find(action.Id).Count>0 &&
                    lowered.Plan.Id==compiled.Plan.Id && planResolver.Calls==1 &&
                    planResolver.Executions==0,
                    "new declarative Plan must hand off to the same original compiler and source map");

                Check(compiled!=null && compiled.Plan!=null &&
                    compiled.ProjectId==snapshot.ProjectId &&
                    compiled.EngineeringRevision==snapshot.Revision &&
                    compiled.SourceMap.Find(action.Id).Count>0,
                    "static handoff must retain the original MOC execution compiler and SourceMap");
                Check(resolver.Calls==1 && resolver.Executions==0,
                    "compile-local resolver cache must pin one delegate without running it");
                var firstOrigin=compiled.SourceMap.Find(action.Id).Single();
                MOCStepOrigin resolvedOrigin;
                Check(compiled.SourceMap.TryResolve(firstOrigin.StepId,out resolvedOrigin) &&
                    resolvedOrigin.BlockId==action.Id &&
                    object.Equals(resolvedOrigin.ContentAddress,firstOrigin.ContentAddress) &&
                    compiled.SourceMap.Find(action.Id)[0].StepId==firstOrigin.StepId,
                    "extracted static SourceMap must retain original step ID and Core Block address");
                var oldManifest=staticManifest;
                var changed=MOLControlBlock.Action("vision.gray16");
                MOLLogicCodec.Set(session,content,new MOLLogicDefinition(changed));
                var staleProbe=new ProbeResolver();
                bool stale=false;
                try {binder.Bind(session.GetSnapshot(),oldManifest,catalog,staleProbe);}
                catch(InvalidOperationException ex)
                { stale=ex.Message.StartsWith("decision.source_manifest.stale",StringComparison.Ordinal); }
                Check(stale && staleProbe.Calls==0,
                    "stale Core source must be rejected before any runtime resolver access");

                var incompleteCatalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("other","Other","Vision")
                });
                var incomplete=new MDCStaticSourceManifestBuilder().Create(
                    session.GetSnapshot(),content.Id,incompleteCatalog);
                var incompleteProbe=new ProbeResolver();
                bool missing=false;
                try {binder.Bind(session.GetSnapshot(),incomplete,incompleteCatalog,incompleteProbe);}
                catch(InvalidOperationException ex)
                { missing=ex.Message.StartsWith("moc.bind.unknown_static_capability:",StringComparison.Ordinal); }
                Check(missing && incompleteProbe.Calls==0,
                    "unknown static capability cannot start runtime resolution");

                var current=new MDCStaticSourceManifestBuilder().Create(
                    session.GetSnapshot(),content.Id,catalog);
                var mismatchProbe=new ProbeResolver{MismatchedInput=true};
                bool mismatch=false;
                try {binder.Bind(session.GetSnapshot(),current,catalog,mismatchProbe);}
                catch(InvalidOperationException ex)
                { mismatch=ex.Message.StartsWith("moc.bind.contract_rejected:",StringComparison.Ordinal); }
                Check(mismatch && mismatchProbe.Calls==1 && mismatchProbe.Executions==0,
                    "unpublished MRK input signature must reject before compiling or executing");

                var legacyProbe=new ProbeResolver{IncludeSignature=false};
                bool unknown=false;
                try {binder.Bind(session.GetSnapshot(),current,catalog,legacyProbe);}
                catch(InvalidOperationException ex)
                { unknown=ex.Message.StartsWith("moc.bind.contract_unverified:",StringComparison.Ordinal); }
                Check(unknown && legacyProbe.Calls==1,
                    "legacy input contract without proof must fail closed by default");
                var legacyOptIn=new ProbeResolver{IncludeSignature=false};
                var compatibility=binder.Bind(session.GetSnapshot(),current,catalog,
                    legacyOptIn,allowUnverifiedLegacyContracts:true);
                Check(compatibility!=null && legacyOptIn.Calls==1 &&
                    legacyOptIn.Executions==0,
                    "explicit legacy compatibility must preserve original MRK compiler behavior");

                // A valid CLR resolver does not override a stale V2 MOL port.
                var wired=session.AddPage("Wired");
                var wireAction=MOLControlBlock.Action("vision.gray16");
                var wrongInput=Port("Pixels",MPCPortDirection.Input,"string");
                MOLWiringCodec.Set(session,wired.RootContent,new MOLWiringDefinition(
                    wireAction.Id,new[]{wireAction},
                    new Dictionary<Guid,IEnumerable<MPCPortDescriptor>> {
                        {wireAction.Id,new[]{wrongInput}}
                    },new MOLLink[0]));
                var declaredCatalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("vision.gray16","Vision 16","Vision",
                        new[]{Port("Pixels",MPCPortDirection.Input,"int")})
                });
                var invalidWiring=new MDCStaticSourceManifestBuilder().Create(
                    session.GetSnapshot(),wired.RootContent.Id,declaredCatalog);
                var staticProbe=new ProbeResolver();
                bool rejectedStatic=false;
                try {binder.Bind(session.GetSnapshot(),invalidWiring,
                    declaredCatalog,staticProbe);}
                catch(InvalidOperationException ex)
                {
                    rejectedStatic=ex.Message.StartsWith("moc.bind.static_wiring_rejected:",
                        StringComparison.Ordinal);
                }
                Check(rejectedStatic && staticProbe.Calls==0,
                    "MOL business port mismatch must reject before MRK resolver probing");

                // Positive V2 path: the existing Builder/Compiler/SourceMap all
                // remain authoritative; the adapter only verifies and pins them.
                var v2Page=session.AddPage("TypedV2");
                Guid v2BlockId=Guid.Empty;
                MOCFlowBuilder.Edit(session,v2Page.RootContent,
                    flow=>{ v2BlockId=flow.Step("vision.gray16"); });
                var v2Snapshot=session.GetSnapshot();
                var v2Manifest=new MDCStaticSourceManifestBuilder().Create(
                    v2Snapshot,v2Page.RootContent.Id,catalog);
                var v2Probe=new ProbeResolver();
                var v2Plan=binder.Bind(v2Snapshot,v2Manifest,catalog,v2Probe);
                Check(v2Plan!=null && v2Plan.SourceMap.Find(v2BlockId).Count>0 &&
                    v2Probe.Calls==1 && v2Probe.Executions==0,
                    "real MOL V2 Builder must retain original compiled SourceMap and one pinned resolver");


            }
            finally {session.Close(true);}
        }

        private static void DeclarativePlanPreservesV1V2Semantics()
        {
            var session=MEMEngineering.Create("decision-immutable-plan");
            try
            {
                var structured=session.AddPage("Structured");
                var first=MOLControlBlock.Action("vision.gray16");
                var iteration=MOLControlBlock.Action("vision.gray16");
                var foreachBody=MOLControlBlock.Action("vision.gray16");
                var loop=MOLControlBlock.Repeat(MOLLoopMode.For,4097,iteration);
                var each=MOLControlBlock.Each("images",5000,foreachBody);
                var branch=MOLControlBlock.If(MOLExpression.InputTrue("enabled"),loop,each);
                var final=MOLControlBlock.Action("vision.gray16");
                var protectedRegion=MOLControlBlock.TryCatchFinally(
                    MOLControlBlock.Throw("inspect.failure"),finallyBody:final,
                    catches:new[]{new MOLCatchClause("inspect.failure",
                        MOLControlBlock.Action("vision.gray16"))});
                var wait=MOLControlBlock.Delay(25);
                MOLLogicCodec.Set(session,structured.RootContent,
                    new MOLLogicDefinition(MOLControlBlock.Sequence(
                        first,branch,protectedRegion,wait)));

                var wired=session.AddPage("Wired");
                Guid sourceId=Guid.Empty,targetId=Guid.Empty,dataLink=Guid.Empty,
                    conditionalLink=Guid.Empty;
                MOCFlowBuilder.Edit(session,wired.RootContent,edit=>{
                    sourceId=edit.Step("vision.gray16");
                    targetId=edit.Step("vision.gray16");
                    edit.AddPort(sourceId,Port("Seed",MPCPortDirection.Input,"gray16"));
                    edit.AddPort(sourceId,Port("Pixels",MPCPortDirection.Output,"gray16"));
                    edit.AddPort(targetId,Port("Image",MPCPortDirection.Input,"gray16"));
                    edit.AddPort(targetId,Port("Result",MPCPortDirection.Output,"gray16"));
                    edit.AddPort(targetId,Port("Threshold",MPCPortDirection.Input,"int"));
                    dataLink=edit.Connect(sourceId,"Pixels",targetId,"Image",MPCPortKind.Data);
                    conditionalLink=edit.ConnectConditional(sourceId,"Done",targetId,
                        "Start","bool:true");
                    edit.ExposeInput("Raw",sourceId,"Seed");
                    edit.ExposeOutput("Detected",targetId,"Result");
                    edit.SetParameter(targetId,"Threshold","5");
                    edit.SetWorkflowPolicy(targetId,new MOLStepPolicy(
                        maxAttempts:2,retryDelayMs:10,timeoutMs:300,
                        idempotent:true,executionGroup:"cpu"));
                });
                var catalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("vision.gray16","Gray16","Vision")
                });
                var snapshot=session.GetSnapshot();
                var compiler=new MDCDeclarativePlanCompiler();
                var v1=compiler.Compile(snapshot,structured.RootContent.Id,catalog);
                var v1Content=v1.Contents.Single();
                Check(v1Content.Blocks.Any(x=>x.BlockId==loop.Id &&
                    x.Kind==MOLControlKind.Loop && x.MaxIterations==4097) &&
                    v1Content.Blocks.Any(x=>x.BlockId==each.Id &&
                    x.Kind==MOLControlKind.ForEach && x.MaxIterations==5000 &&
                    x.CollectionInput=="images"),
                    "Declarative Plan must preserve dynamic Loop/ForEach bounds without expansion");
                Check(v1Content.Semantics.Branches.Count==2 &&
                    v1Content.Semantics.Branches[0].Condition!=null &&
                    v1Content.Semantics.Branches[0].Condition.Source==
                        MOLExpressionSource.FlowInput &&
                    v1Content.Semantics.Branches[0].Condition.Name=="enabled" &&
                    v1Content.Semantics.Branches[1].Condition==null,
                    "V1 typed branch guards and default case must preserve exact predicates");
                Check(v1Content.Semantics.Loops.Count==2 &&
                    v1Content.Semantics.Loops.Any(x=>x.BlockId==loop.Id &&
                        x.MaxIterations==4097 && x.Mode==MOLLoopMode.For) &&
                    v1Content.Semantics.Loops.Any(x=>x.BlockId==each.Id &&
                        x.CollectionInput=="images"),
                    "V1 typed loop rules retain dynamic bounds and collection inputs");
                Check(v1Content.Semantics.Catches.Count==1 &&
                    v1Content.Semantics.Catches[0].ErrorCode=="inspect.failure" &&
                    v1Content.Semantics.Waits.Count==1 &&
                    v1Content.Semantics.Waits[0].DurationOrTimeoutMs==25 &&
                    v1Content.Semantics.Exits.Single().Kind==MOLExitKind.Throw &&
                    v1Content.Semantics.Exits.Single().ErrorCode=="inspect.failure",
                    "V1 declared catch/timeout/throw rules must be typed, not discarded");
                Check(v1Content.Blocks.Count<20 &&
                    v1Content.Blocks.Any(x=>x.BlockId==branch.Id &&
                        x.Kind==MOLControlKind.Branch) &&
                    v1Content.Blocks.Any(x=>x.BlockId==foreachBody.Id &&
                        x.ParentBlockId==each.Id && x.Role=="LoopBody") &&
                    v1Content.Blocks.Any(x=>x.BlockId==final.Id &&
                        x.ParentBlockId==protectedRegion.Id && x.Role=="Finally"),
                    "V1 branch/structured exception tree and original BlockIds must survive");
                Check(v1Content.CopyCanonicalLogicPayload().SequenceEqual(
                    snapshot.Find(structured.RootContent.Id).Aspects.Single(
                        x=>x.Kind==MPMAspectKind.Logic).Payload),
                    "complete original Logic codec payload preserves expressions/try/catches");
                var modifiedPayload=v1Content.CopyCanonicalLogicPayload();
                modifiedPayload[0]=(byte)(modifiedPayload[0]^1);
                Check(!v1Content.CopyCanonicalLogicPayload().SequenceEqual(modifiedPayload),
                    "mutating external payload copy must not alter the static Plan");

                var v2=compiler.Compile(snapshot,wired.RootContent.Id,catalog);
                var w=v2.Contents.Single();
                Check(w.IsWiring && w.Blocks.Count==2 &&
                    w.EntryBlockId==sourceId && w.Links.Count==2 &&
                    w.Links.Any(x=>x.LinkId==dataLink&&x.Kind==MPCPortKind.Data&&
                        x.SourcePort=="Pixels"&&x.TargetPort=="Image") &&
                    w.Links.Any(x=>x.LinkId==conditionalLink&&x.Kind==MPCPortKind.Control&&
                        x.ConditionId=="bool:true"),
                    "V2 control/data links and explicit branch guard must survive");
                Check(w.FlowInputs.Any(x=>x.Name=="Raw"&&x.BlockId==sourceId) &&
                    w.FlowOutputs.Any(x=>x.Name=="Detected"&&x.BlockId==targetId) &&
                    w.Literals.Any(x=>x.BlockId==targetId&&
                        x.PortMemberId=="Threshold"&&x.Value=="5") &&
                    w.Policies.Any(x=>x.BlockId==targetId&&x.MaxAttempts==2 &&
                        x.RetryDelayMs==10&&x.ExecutionGroup=="cpu") &&
                    w.Ports.Any(x=>x.BlockId==targetId && x.MemberId=="Image" &&
                        x.TypeId=="gray16"),
                    "V2 named ports, literal values, policy and type contract must survive");
                Check(v2.Fingerprint.Length==64 &&
                    v2.Fingerprint==compiler.Compile(snapshot,wired.RootContent.Id,catalog)
                        .Fingerprint,"static Plan must be stable for the same Core snapshot");
                v2.RequireCurrent(snapshot,catalog);
                var changedCatalog=new MDCStaticCatalogSnapshot(new[]{
                    new MOCCapabilityDescriptor("vision.gray16","New schema","Vision")
                });
                bool stale=false;
                try {v2.RequireCurrent(snapshot,changedCatalog);}
                catch(InvalidOperationException) {stale=true;}
                Check(stale,"static Plan must reject changed professional catalog");
                var statesPage=session.AddPage("Stateful");
                Guid machineId=Guid.Empty;
                var guarded=MOLExpression.All(MOLExpression.InputTrue("ready"),
                    MOLExpression.InputTrue("authorized"));
                var stateDefinition=new MOLStateMachineDefinition(new[]{
                    new MOLStateDescriptor("Idle","Main",initial:true),
                    new MOLStateDescriptor("Busy","Main",
                        entryCapability:"vision.gray16",exitCapability:"vision.gray16")
                },new[]{
                    new MOLStateTransition("Idle","Start","Busy",guarded,7)
                });
                MOCFlowBuilder.Edit(session,statesPage.RootContent,edit=>{
                    machineId=edit.AddStateMachine(stateDefinition);
                });
                var statePlan=compiler.Compile(session.GetSnapshot(),
                    statesPage.RootContent.Id,catalog).Contents.Single();
                Check(statePlan.IsWiring &&
                    statePlan.Semantics.States.Count==2 &&
                    statePlan.Semantics.States.Any(x=>x.MachineBlockId==machineId &&
                        x.StateId=="Busy" && x.EntryCapabilityId=="vision.gray16" &&
                        x.ExitCapabilityId=="vision.gray16") &&
                    statePlan.Semantics.Transitions.Count==1 &&
                    statePlan.Semantics.Transitions[0].Priority==7 &&
                    statePlan.Semantics.Transitions[0].EventId=="Start" &&
                    statePlan.Semantics.Transitions[0].Guard.Operator==MOLLogicalOperator.All &&
                    statePlan.Semantics.Transitions[0].Guard.Terms.Count==2,
                    "V2 typed hierarchical StateMachine and compound transition guard survive");

                bool noExecutableDelegate=typeof(MDCDeclarativePlan).Assembly
                    .GetTypes().Where(t=>t.Name.StartsWith("MDCPlan",StringComparison.Ordinal))
                    .SelectMany(t=>t.GetProperties())
                    .All(p=>!typeof(Delegate).IsAssignableFrom(p.PropertyType));
                Check(noExecutableDelegate,
                    "pure Plan DTO must not expose an executable Delegate");
            }
            finally {session.Close(true);}
        }

    }
}
