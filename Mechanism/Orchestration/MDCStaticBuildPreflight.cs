using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>
    /// Static check of the existing MOL wiring's declared ports against the
    /// existing MOC capability catalog. Does not compile a second Plan or
    /// resolve/execute an MRK provider. Runtime checks remain separate.
    /// </summary>
    public sealed class MDCStaticBuildPreflight
    {
        public MDCAssessment Assess(MOLWiringDefinition wiring,
            MOCCapabilityCatalog catalog, bool requireExecutable = true)
        {
            if (wiring == null) throw new ArgumentNullException("wiring");
            if (catalog == null) throw new ArgumentNullException("catalog");
            try
            {
                if (requireExecutable) wiring.ValidateExecutable();
                else wiring.Validate();
            }
            catch (ArgumentException ex)
            {
                return Rejected("MDC_PREFLIGHT_WIRING_INVALID", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Rejected("MDC_PREFLIGHT_WIRING_INVALID", ex.Message);
            }
            var checks = new List<MDCConstraintOutcome>();
            foreach (MOLControlBlock block in wiring.Blocks)
            {
                // These are Core/MOL control definitions, not professional runtime abilities.
                if (block.CapabilityId == "moc.state-machine" ||
                    block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                    continue;
                MOCCapabilityCatalogItem found = catalog.Find(block.CapabilityId);
                string prefix = block.Id.ToString("N") + ":";
                if (found == null)
                {
                    checks.Add(new MDCConstraintOutcome(
                        "MDC_PREFLIGHT_CAPABILITY_UNKNOWN:" + prefix + block.CapabilityId,
                        MDCVerdict.Unknown,
                        "no static capability descriptor in this catalog snapshot"));
                    continue;
                }
                IReadOnlyList<MPCPortDescriptor> declared;
                if (!wiring.Ports.TryGetValue(block.Id,out declared))
                {
                    checks.Add(new MDCConstraintOutcome(
                        "MDC_PREFLIGHT_PORT_SCHEMA_MISSING:" + prefix,
                        MDCVerdict.Rejected,"MOL block has no original port schema"));
                    continue;
                }
                var byName = declared.ToDictionary(x => x.MemberId,StringComparer.Ordinal);
                foreach (MPCPortDescriptor port in found.Definition.Ports)
                {
                    MPCPortDescriptor actual;
                    if (!byName.TryGetValue(port.MemberId,out actual))
                    {
                        checks.Add(new MDCConstraintOutcome(
                            "MDC_PREFLIGHT_PORT_NOT_DECLARED:" + prefix + port.MemberId,
                            port.Required ? MDCVerdict.Rejected : MDCVerdict.Unknown,
                            "MOL block has not declared this professional port"));
                        continue;
                    }
                    if (port.Direction != actual.Direction || port.TypeId != actual.TypeId ||
                        port.Kind != actual.Kind || port.Required != actual.Required ||
                        port.Multiplicity != actual.Multiplicity ||
                        !object.Equals(port.Contract,actual.Contract))
                        checks.Add(new MDCConstraintOutcome(
                            "MDC_PREFLIGHT_PORT_MISMATCH:" + prefix + port.MemberId,
                            MDCVerdict.Rejected,
                            "MOL port differs from the professional schema; explicit migration required"));
                }
                foreach (MPCPortDescriptor port in declared)
                    // Start/Done and other MOL control ports belong to the
                    // engineering workflow, not the professional data signature.
                    if ((port.Kind == MPCPortKind.Data || port.Kind == MPCPortKind.Stream) &&
                        !found.Definition.Ports.Any(x => x.MemberId == port.MemberId))
                        checks.Add(new MDCConstraintOutcome(
                            "MDC_PREFLIGHT_EXTRA_PORT:" + prefix + port.MemberId,
                            MDCVerdict.Unknown,
                            "MOL defines a port absent from the current capability schema"));
            }
            return new MDCAssessment(checks);
        }

        private static MDCAssessment Rejected(string code, string message)
        {
            return new MDCAssessment(new[] {
                new MDCConstraintOutcome(code, MDCVerdict.Rejected, message)
            });
        }
    }
}
