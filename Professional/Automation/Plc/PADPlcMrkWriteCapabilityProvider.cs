using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Automation.Plc
{
    /// <summary>
    /// MOC/MRK capability adapter for the existing authorized PLC command.
    /// The Host, not this provider, owns the command center, trusted caller,
    /// device resource lease, publication authorization and live version stamp.
    /// A Modbus register readback is NOT evidence of a physical actuator action.
    /// </summary>
    public sealed class PADPlcMrkWriteCapabilityProvider :
        IMOCCapabilityProvider, IMRKVersionedCapabilityResolver
    {
        public const string CapabilityId = "automation.plc.holding.uint16.write-confirmed";
        private readonly MRKCommandCenter _commands;
        private readonly string _target;
        private readonly string _caller;
        private readonly string _version;
        private readonly IMRKVersionedCapabilityResolver _fallback;
        private readonly MRKResolvedCapability _binding;

        /// <summary>
        /// The Host must pin the exact approved PLC point table and command
        /// registration in capabilityVersion. It must register the command with
        /// PADPlcMrkCommandRegistration before starting the MRK Flow.
        /// No default principal, anonymous authorization or implicit retry.
        /// </summary>
        public PADPlcMrkWriteCapabilityProvider(MRKCommandCenter commands,
            string deviceRef, string trustedCaller, string capabilityVersion,
            IMRKVersionedCapabilityResolver fallback = null)
        {
            if (commands == null) throw new ArgumentNullException("commands");
            if (string.IsNullOrWhiteSpace(trustedCaller) ||
                trustedCaller.Length > 128 || trustedCaller.Any(char.IsControl))
                throw new ArgumentException("automation.plc.trusted_caller_required");
            if (string.IsNullOrWhiteSpace(capabilityVersion) ||
                capabilityVersion.Length > 128 || capabilityVersion.Any(char.IsControl))
                throw new ArgumentException("automation.plc.approved_version_required");
            _commands = commands;
            _target = PADPlcMrkCommandRegistration.TargetForDevice(deviceRef);
            _caller = trustedCaller.Trim();
            _version = capabilityVersion;
            _fallback = fallback;
            _binding = new MRKResolvedCapability(ExecuteAsync,
                new MRKExecutionOutput(typeof(bool)),
                dataOutputs: new Dictionary<string, Type> {
                    { "RegisterReadbackMatched", typeof(bool) }
                },
                inputs: new[] {
                    new MRKCapabilityInput("StableId", typeof(string)),
                    new MRKCapabilityInput("Value", typeof(ushort))
                });
        }

        /// <summary>
        /// Safe offline authoring contract. Does not instantiate a command
        /// center, authorize a PLC or claim runtime availability.
        /// Studio and the real Host share this one professional definition.
        /// </summary>
        public static MOCCapabilityDescriptor DescribeOfflineContract()
        {
            return new MOCCapabilityDescriptor(CapabilityId,
                "已授权PLC UInt16写入及寄存器回读", "Automation/PLC",
                new[] {
                    new MPCPortDescriptor("StableId", MPCPortDirection.Input,
                        "string", MPCPortKind.Data, required:true),
                    new MPCPortDescriptor("Value", MPCPortDirection.Input,
                        "ushort", MPCPortKind.Data, required:true),
                    new MPCPortDescriptor("RegisterReadbackMatched",
                        MPCPortDirection.Output,"bool",MPCPortKind.Data)
                });
        }

        public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
        { return new[] { DescribeOfflineContract() }; }

        public bool TryResolve(string id, out MRKResolvedCapability result)
        {
            if (StringComparer.Ordinal.Equals(id, CapabilityId))
            {
                result = _binding;
                return true;
            }
            if (_fallback != null) return _fallback.TryResolve(id, out result);
            result = null;
            return false;
        }

        public bool TryGetCapabilityVersion(string id, out string version)
        {
            if (StringComparer.Ordinal.Equals(id, CapabilityId))
            {
                version = _version;
                return true;
            }
            if (_fallback != null) return _fallback.TryGetCapabilityVersion(id, out version);
            version = null;
            return false;
        }

        private async Task<object> ExecuteAsync(MRKExecutionContext context,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            // A general MRK Plan may have been marked idempotent in error.
            // The PLC command itself is NEVER retry-safe after an uncertain
            // acknowledgement. Reject subsequent policy attempts before IO.
            if (context != null && context.AttemptIndex > 1)
                throw new InvalidOperationException(
                    "automation.plc.nonidempotent_retry_denied");
            object id, value;
            if (context == null ||
                !context.BoundInputs.TryGetValue("StableId", out id) ||
                !(id is string) || string.IsNullOrWhiteSpace((string)id) ||
                !context.BoundInputs.TryGetValue("Value", out value) ||
                !(value is ushort))
                throw new InvalidOperationException("automation.plc.typed_input_required");

            MRKCommandRequest request = new MRKCommandRequest(_target,
                PADPlcMrkCommandRegistration.WriteUInt16Code,
                new Dictionary<string, object> {
                    { "StableId", (string)id }, { "Value", (ushort)value }
                }, caller: _caller, expectedSchemaVersion: 1);
            // The device operation may have taken effect even if the caller
            // times out or cancels. Never retry a new OperationId automatically.
            MRKCommandResponse response = await _commands.InvokeAsync(
                request, cancellation).ConfigureAwait(false);
            object verified;
            if (response == null ||
                response.Stage != MRKCommandStage.Accepted ||
                response.State != MRKCommandState.Completed ||
                response.BusinessSuccess != true ||
                !StringComparer.Ordinal.Equals(response.BusinessCode,
                    "PLC_REGISTER_READBACK_CONFIRMED") ||
                !response.Outputs.TryGetValue("RegisterReadbackMatched", out verified) ||
                !(verified is bool) || !(bool)verified)
                throw new InvalidOperationException(
                    "automation.plc.register_outcome_unconfirmed");
            cancellation.ThrowIfCancellationRequested();
            context.Data.Return("RegisterReadbackMatched", true);
            return true;
        }
    }
}
