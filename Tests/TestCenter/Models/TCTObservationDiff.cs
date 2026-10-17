using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Models
{
    public enum TCTObservationChangeKind { Added, Removed, Changed }

    public sealed class TCTObservationChange
    {
        public TCTObservationChange(string key, TCTObservationChangeKind kind,
            string beforeSha256, string afterSha256)
        { Key = key; Kind = kind; BeforeSha256 = beforeSha256; AfterSha256 = afterSha256; }
        public string Key { get; private set; }
        public TCTObservationChangeKind Kind { get; private set; }
        public string BeforeSha256 { get; private set; }
        public string AfterSha256 { get; private set; }
    }

    public sealed class TCTObservationComparison
    {
        public TCTObservationComparison(string testId, IList<TCTObservationChange> differences,
            int beforeCount, int afterCount)
        {
            TestId = testId; BeforeCount = beforeCount; AfterCount = afterCount;
            Differences = new ReadOnlyCollection<TCTObservationChange>(
                new List<TCTObservationChange>(differences));
        }
        public string TestId { get; private set; }
        public int BeforeCount { get; private set; }
        public int AfterCount { get; private set; }
        public bool Equal { get { return Differences.Count == 0; } }
        public IList<TCTObservationChange> Differences { get; private set; }
    }

    // Compares two independently verified real Runs, not UI drawings or NUnit verdict strings.
    public static class TCTObservationDiffService
    {
        public static TCTObservationComparison Compare(TCTTestCaseResult before,
            TCTTestCaseResult after, TCTTestArtifactStore beforeStore = null,
            TCTTestArtifactStore afterStore = null)
        {
            if (before == null || after == null) throw new ArgumentNullException("result");
            if (before.Descriptor.Id != after.Descriptor.Id)
                throw new InvalidDataException("Cannot compare observations of different TestIds");
            IList<TCTObservationEnvelope> left = TCTObservationReplay.Read(before, beforeStore);
            IList<TCTObservationEnvelope> right = TCTObservationReplay.Read(after, afterStore);
            var lhs = Index(left);
            var rhs = Index(right);
            var changes = new List<TCTObservationChange>();
            foreach (string key in lhs.Keys.Union(rhs.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                TCTObservationEnvelope a, b;
                bool hasA = lhs.TryGetValue(key, out a), hasB = rhs.TryGetValue(key, out b);
                string shaA = hasA && a.PayloadRef != null ? a.PayloadRef.Sha256 : "";
                string shaB = hasB && b.PayloadRef != null ? b.PayloadRef.Sha256 : "";
                if (!hasA)
                    changes.Add(new TCTObservationChange(key, TCTObservationChangeKind.Added, "", shaB));
                else if (!hasB)
                    changes.Add(new TCTObservationChange(key, TCTObservationChangeKind.Removed, shaA, ""));
                else if (a.Details != b.Details || a.PayloadRef == null != (b.PayloadRef == null) ||
                    shaA != shaB || a.SourceObjectId != b.SourceObjectId ||
                    a.DefinitionAddress != b.DefinitionAddress || a.Origin != b.Origin)
                    changes.Add(new TCTObservationChange(key, TCTObservationChangeKind.Changed, shaA, shaB));
            }
            return new TCTObservationComparison(before.Descriptor.Id, changes, left.Count, right.Count);
        }
        private static SortedDictionary<string, TCTObservationEnvelope> Index(
            IEnumerable<TCTObservationEnvelope> observations)
        {
            var result = new SortedDictionary<string, TCTObservationEnvelope>(StringComparer.Ordinal);
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (TCTObservationEnvelope o in observations)
            {
                // Definition location + source + logical title are stable across different RunIds.
                // Runtime instance GUIDs and timestamps deliberately are not compared as stable keys.
                string baseKey = o.SourceObjectId + "|" + o.DefinitionAddress + "|" +
                    o.Kind + "|" + o.Title + "|" + o.Origin;
                int sequence;
                occurrences.TryGetValue(baseKey, out sequence);
                occurrences[baseKey] = ++sequence;
                result.Add(baseKey + "|#" + sequence, o);
            }
            return result;
        }
    }
}
