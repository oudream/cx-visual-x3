using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>
    /// Immutable projection of the existing MOC capability descriptions.
    /// This is not a second registry, configuration owner or runtime resolver.
    /// The fingerprint covers the complete static port contract, not live availability.
    /// </summary>
    public sealed class MDCStaticCatalogSnapshot
    {
        private readonly IReadOnlyDictionary<string, MOCCapabilityDescriptor> _items;
        private readonly IReadOnlyDictionary<string, string> _fingerprints;

        public MDCStaticCatalogSnapshot(MOCCapabilityCatalog catalog)
            : this(catalog == null ? null : catalog.All.Select(x => x.Definition)) { }

        public MDCStaticCatalogSnapshot(IEnumerable<MOCCapabilityDescriptor> descriptions)
        {
            if (descriptions == null) throw new ArgumentNullException("descriptions");
            var all = descriptions.ToList();
            if (all.Count > 4096 || all.Any(x => x == null) ||
                all.GroupBy(x => x.Id, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new ArgumentException("decision.catalog.invalid_static_snapshot");
            var map = new SortedDictionary<string, MOCCapabilityDescriptor>(StringComparer.Ordinal);
            var signatures = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var descriptor in all)
            {
                map.Add(descriptor.Id, descriptor);
                signatures.Add(descriptor.Id, ComputeDescriptorFingerprint(descriptor));
            }
            _items = new ReadOnlyDictionary<string, MOCCapabilityDescriptor>(map);
            _fingerprints = new ReadOnlyDictionary<string, string>(signatures);
            Fingerprint = Digest(write => {
                write.Write(all.Count);
                foreach (var entry in signatures)
                {
                    WriteString(write, entry.Key);
                    WriteString(write, entry.Value);
                }
            });
        }

        public string Fingerprint { get; private set; }
        public IReadOnlyDictionary<string, MOCCapabilityDescriptor> Descriptions
        { get { return _items; } }
        public IReadOnlyDictionary<string, string> DescriptorFingerprints
        { get { return _fingerprints; } }

        public MOCCapabilityDescriptor Find(string capabilityId)
        {
            MOCCapabilityDescriptor value;
            return capabilityId != null && _items.TryGetValue(capabilityId, out value)
                ? value : null;
        }

        private static string ComputeDescriptorFingerprint(MOCCapabilityDescriptor d)
        {
            return Digest(write => {
                WriteString(write, d.Id);
                WriteString(write, d.Name);
                WriteString(write, d.Category);
                write.Write(d.Version);
                write.Write(d.Ports.Count);
                foreach (var port in d.Ports.OrderBy(x => x.MemberId, StringComparer.Ordinal))
                {
                    WriteString(write, port.MemberId);
                    WriteString(write, port.TypeId);
                    write.Write((int)port.Direction);
                    write.Write((int)port.Kind);
                    write.Write((int)port.Multiplicity);
                    write.Write(port.Required);
                    write.Write(port.IsPublic);
                    MTUContractReference contract = port.Contract;
                    WriteString(write, contract == null ? "" : contract.UnitId);
                    WriteString(write, contract == null ? "" : contract.CoordinateFrameId);
                    WriteString(write, contract == null ? "" : contract.TimeBaseId);
                    // The current MPCPortDescriptor rejects project calibration identities.
                    // Never serialize or guess an address in a global capability catalog.
                    if (contract != null && contract.Calibration != null)
                        throw new InvalidOperationException("decision.catalog.project_calibration_not_allowed");
                }
            });
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null) value = "";
            byte[] raw = Encoding.UTF8.GetBytes(value);
            writer.Write(raw.Length);
            writer.Write(raw);
        }

        private static string Digest(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var binary = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    write(binary);
                    binary.Flush();
                }
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream.ToArray());
                    var result = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash)
                        result.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    return result.ToString();
                }
            }
        }
    }

    /// <summary>
    /// A recommendation is usable only for the exact Core revision,
    /// static catalog, policy revision and environment snapshot it assessed.
    /// Host must still re-check live authorization and resource leases.
    /// </summary>
    public sealed class MDCDecisionVersionStamp : IEquatable<MDCDecisionVersionStamp>
    {
        public MDCDecisionVersionStamp(Guid projectId, int revision, string catalogFingerprint,
            string policyRevision, string environmentEpoch)
        {
            if (projectId == Guid.Empty || revision < 0 ||
                string.IsNullOrWhiteSpace(catalogFingerprint) ||
                string.IsNullOrWhiteSpace(policyRevision) ||
                string.IsNullOrWhiteSpace(environmentEpoch))
                throw new ArgumentException("decision.stamp.missing_required_version");
            ProjectId = projectId;
            EngineeringRevision = revision;
            CatalogFingerprint = catalogFingerprint;
            PolicyRevision = policyRevision;
            EnvironmentEpoch = environmentEpoch;
        }
        public Guid ProjectId { get; private set; }
        public int EngineeringRevision { get; private set; }
        public string CatalogFingerprint { get; private set; }
        public string PolicyRevision { get; private set; }
        public string EnvironmentEpoch { get; private set; }
        public bool Equals(MDCDecisionVersionStamp other)
        {
            return other != null && ProjectId == other.ProjectId &&
                EngineeringRevision == other.EngineeringRevision &&
                StringComparer.Ordinal.Equals(CatalogFingerprint, other.CatalogFingerprint) &&
                StringComparer.Ordinal.Equals(PolicyRevision, other.PolicyRevision) &&
                StringComparer.Ordinal.Equals(EnvironmentEpoch, other.EnvironmentEpoch);
        }
        public override bool Equals(object other)
        { return Equals(other as MDCDecisionVersionStamp); }
        public override int GetHashCode()
        {
            unchecked
            {
                int code = ProjectId.GetHashCode() * 397 ^ EngineeringRevision;
                code = code * 397 ^ StringComparer.Ordinal.GetHashCode(CatalogFingerprint);
                code = code * 397 ^ StringComparer.Ordinal.GetHashCode(PolicyRevision);
                return code * 397 ^ StringComparer.Ordinal.GetHashCode(EnvironmentEpoch);
            }
        }
        public void RequireCurrent(MDCDecisionVersionStamp current)
        {
            if (!Equals(current))
                throw new InvalidOperationException("decision.assessment.stale_version");
        }
    }

    public enum MDCCatalogChangeKind { Added = 1, Removed = 2, Modified = 3 }

    public sealed class MDCCatalogChange
    {
        internal MDCCatalogChange(string capabilityId, MDCCatalogChangeKind kind)
        { CapabilityId = capabilityId; Kind = kind; }
        public string CapabilityId { get; private set; }
        public MDCCatalogChangeKind Kind { get; private set; }
    }

    /// <summary>
    /// D10: deterministic conservative impact set for capability schema changes.
    /// Consumers of modified IDs must be rechecked; this does not edit Core or rerun MRK.
    /// </summary>
    public static class MDCCatalogImpactAnalyzer
    {
        public static IReadOnlyList<MDCCatalogChange> Compare(
            MDCStaticCatalogSnapshot previous, MDCStaticCatalogSnapshot current)
        {
            if (previous == null) throw new ArgumentNullException("previous");
            if (current == null) throw new ArgumentNullException("current");
            var changes = new List<MDCCatalogChange>();
            var ids = new SortedSet<string>(previous.DescriptorFingerprints.Keys,
                StringComparer.Ordinal);
            ids.UnionWith(current.DescriptorFingerprints.Keys);
            foreach (string id in ids)
            {
                string oldFingerprint, newFingerprint;
                bool before = previous.DescriptorFingerprints.TryGetValue(id, out oldFingerprint);
                bool after = current.DescriptorFingerprints.TryGetValue(id, out newFingerprint);
                if (!before) changes.Add(new MDCCatalogChange(id, MDCCatalogChangeKind.Added));
                else if (!after) changes.Add(new MDCCatalogChange(id, MDCCatalogChangeKind.Removed));
                else if (!StringComparer.Ordinal.Equals(oldFingerprint, newFingerprint))
                    changes.Add(new MDCCatalogChange(id, MDCCatalogChangeKind.Modified));
            }
            return new ReadOnlyCollection<MDCCatalogChange>(changes);
        }
    }
}
