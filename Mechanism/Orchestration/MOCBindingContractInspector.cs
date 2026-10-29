using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>
    /// Only at the MOC/MRK integration boundary: compare the authoritative
    /// static MPC contract to a resolved MRK binding without executing it.
    /// This adapter belongs on the Runtime-facing side after Decision migration.
    /// </summary>
    public sealed class MOCBindingContractInspector
    {
        private static readonly IReadOnlyDictionary<string, Type> KnownTypes =
            new ReadOnlyDictionary<string, Type>(new Dictionary<string, Type>(StringComparer.Ordinal)
            {
                {"bool",typeof(bool)}, {"System.Boolean",typeof(bool)},
                {"byte",typeof(byte)}, {"System.Byte",typeof(byte)},
                {"byte[]",typeof(byte[])}, {"System.Byte[]",typeof(byte[])},
                {"short",typeof(short)}, {"System.Int16",typeof(short)},
                {"ushort",typeof(ushort)}, {"System.UInt16",typeof(ushort)},
                {"int",typeof(int)}, {"System.Int32",typeof(int)},
                {"uint",typeof(uint)}, {"System.UInt32",typeof(uint)},
                {"long",typeof(long)}, {"System.Int64",typeof(long)},
                {"float",typeof(float)}, {"System.Single",typeof(float)},
                {"double",typeof(double)}, {"System.Double",typeof(double)},
                {"string",typeof(string)}, {"System.String",typeof(string)}
            });

        /// <summary>Explicit domain type bindings are provided by the professional Host;
        /// no arbitrary Type.GetType, reflection scan, or implicit conversions.</summary>
        public MDCAssessment Assess(MOCCapabilityDescriptor description,
            MRKResolvedCapability binding, IReadOnlyDictionary<string, Type> domainTypes = null)
        {
            if (description == null) throw new ArgumentNullException("description");
            if (binding == null) throw new ArgumentNullException("binding");
            var rules = new List<MDCConstraintOutcome>();
            var ports = description.Ports.Where(x => x.Kind == MPCPortKind.Data ||
                x.Kind == MPCPortKind.Stream).ToList();
            var inputs = ports.Where(x => x.Direction == MPCPortDirection.Input ||
                x.Direction == MPCPortDirection.InOut).ToList();
            var outputs = ports.Where(x => x.Direction == MPCPortDirection.Output ||
                x.Direction == MPCPortDirection.InOut).ToList();

            if (!binding.HasInputContract)
                rules.Add(Rule("MDC_BIND_INPUT_UNDECLARED", MDCVerdict.Unknown,
                    "Runtime provider has no input signature; legacy validation stays in MRK"));
            else
            {
                var declared = binding.Inputs.ToDictionary(x => x.Name, StringComparer.Ordinal);
                foreach (var port in inputs)
                {
                    MRKCapabilityInput actual;
                    if (!declared.TryGetValue(port.MemberId, out actual))
                    {
                        rules.Add(Rule("MDC_BIND_INPUT_MISSING:" + port.MemberId,
                            MDCVerdict.Rejected, "static input absent from the MRK signature"));
                        continue;
                    }
                    if (actual.Required && !port.Required)
                        rules.Add(Rule("MDC_BIND_REQUIRED_MISMATCH:" + port.MemberId,
                            MDCVerdict.Rejected, "Runtime requires an input declared optional"));
                    CheckType(port, actual.ValueType, domainTypes, rules, "INPUT");
                }
                foreach (var actual in binding.Inputs)
                    if (!inputs.Any(x => x.MemberId == actual.Name))
                        rules.Add(Rule("MDC_BIND_UNDECLARED_INPUT:" + actual.Name,
                            MDCVerdict.Rejected, "MRK input has no public static MPC description"));
            }
            foreach (var port in outputs)
            {
                Type actual;
                if (binding.DataOutputs.TryGetValue(port.MemberId, out actual))
                    CheckType(port, actual, domainTypes, rules, "OUTPUT");
                else if (binding.Output != null)
                    rules.Add(Rule("MDC_BIND_OUTPUT_UNCONFIRMED:" + port.MemberId,
                        MDCVerdict.Unknown, "StepResult is not a named Data.Return output"));
                else
                    rules.Add(Rule("MDC_BIND_OUTPUT_MISSING:" + port.MemberId,
                        MDCVerdict.Rejected, "static output absent from runtime contract"));
            }
            if (rules.Count == 0)
                rules.Add(Rule("MDC_BIND_CONTRACT_MATCH", MDCVerdict.Allowed,
                    "declared ports and MRK binding are consistent"));
            return new MDCAssessment(rules);
        }

        private static void CheckType(MPCPortDescriptor port, Type actual,
            IReadOnlyDictionary<string, Type> domainTypes,
            ICollection<MDCConstraintOutcome> results, string direction)
        {
            Type expected;
            if (!KnownTypes.TryGetValue(port.TypeId, out expected))
            {
                if (domainTypes == null || !domainTypes.TryGetValue(port.TypeId, out expected) ||
                    expected == null || expected == typeof(void))
                {
                    results.Add(Rule("MDC_BIND_TYPE_UNKNOWN:" + direction + ":" + port.MemberId,
                        MDCVerdict.Unknown, "professional TypeId requires explicit host mapping"));
                    return;
                }
            }
            if (actual == typeof(object) && expected != typeof(object))
                results.Add(Rule("MDC_BIND_OBJECT_UNVERIFIED:" + direction + ":" + port.MemberId,
                    MDCVerdict.Unknown,
                    "legacy object input is broader than the declared semantic TypeId"));
            else if (actual != expected)
                results.Add(Rule("MDC_BIND_TYPE_MISMATCH:" + direction + ":" + port.MemberId,
                    MDCVerdict.Rejected, "static TypeId differs from CLR runtime contract"));
        }

        private static MDCConstraintOutcome Rule(string id, MDCVerdict verdict, string reason)
        { return new MDCConstraintOutcome(id, verdict, reason); }
    }
}
