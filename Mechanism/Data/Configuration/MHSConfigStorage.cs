using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    // Each revision is immutable. Extra history files after interrupted commits are harmless;
    // the current revision is recovered by comparing the current JSON to revision content.
    internal sealed class MHSConfigStorage
    {
        private readonly string _folder;
        private readonly string _current;
        private readonly string _schemaContract;
        private readonly string _history;
        private readonly string _effective;
        private readonly string _previousEffective;
        private readonly string _effectiveJournalOrigin;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public MHSConfigStorage(string folder)
        {
            _folder = folder;
            _current = Path.Combine(folder, "current.json");
            _schemaContract = Path.Combine(folder, "schema.contract");
            _history = Path.Combine(folder, "history");
            _effective = Path.Combine(folder, "effective.txt");
            _previousEffective = Path.Combine(folder, "previous-effective.txt");
            _effectiveJournalOrigin = Path.Combine(folder, "effective-journal-origin.txt");
            Directory.CreateDirectory(_history);
        }
        private string RevisionFile(int revision) { return Path.Combine(_history, "r" + revision.ToString("D8", CultureInfo.InvariantCulture) + ".json"); }
        private string ParentFile(int revision) { return Path.Combine(_history, "r" + revision.ToString("D8", CultureInfo.InvariantCulture) + ".parent"); }
        private string AuditFile(int revision) { return Path.Combine(_history, "r" + revision.ToString("D8", CultureInfo.InvariantCulture) + ".audit.json"); }
        private string EffectiveEventFile(int revision) { return Path.Combine(_history, "r" + revision.ToString("D8", CultureInfo.InvariantCulture) + ".effective.json"); }
        public static string Name(int revision) { return "v" + revision.ToString(CultureInfo.InvariantCulture); }
        public static int ParseName(string name)
        {
            int n;
            if (name == null || !name.StartsWith("v", StringComparison.Ordinal) ||
                !int.TryParse(name.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out n) || n < 1)
                throw new ArgumentException("Invalid config revision: " + name);
            return n;
        }
        public bool HasCurrent { get { return File.Exists(_current); } }
        public string ReadCurrent() { return File.ReadAllText(_current, Utf8); }
        public string ReadVersion(int revision)
        {
            string path = RevisionFile(revision);
            if (!File.Exists(path)) throw new FileNotFoundException("Config revision is not available: " + Name(revision));
            return File.ReadAllText(path, Utf8);
        }
        public IList<int> ListVersions()
        {
            return Directory.GetFiles(_history, "r????????.json").Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(f => { int n; return int.TryParse(f.Substring(1), out n) ? n : 0; })
                .Where(n => n > 0).OrderBy(n => n).ToArray();
        }
        public int IdentifyCurrent(string json)
        {
            foreach (int version in ListVersions().Reverse())
                if (string.Equals(ReadVersion(version), json, StringComparison.Ordinal)) return version;
            // Crash after replacing current, before metadata: preserve recovered body as new revision.
            int next = ListVersions().DefaultIfEmpty(0).Max() + 1;
            SaveVersion(next, json);
            return next;
        }
        /// <summary>Fail closed when persisted data has no matching schema identity.
        /// A legacy unversioned document requires an explicit reviewed migration.</summary>
        public int Initialize(string defaults, string schemaContract)
        {
            if (string.IsNullOrWhiteSpace(schemaContract)) throw new ArgumentException("Schema contract required");
            using (FileStream lease = AcquireWriteLease())
            {
                if (File.Exists(_schemaContract))
                {
                    if (!string.Equals(File.ReadAllText(_schemaContract, Utf8), schemaContract,
                        StringComparison.Ordinal))
                        throw new InvalidOperationException("CONFIG_SCHEMA_MISMATCH");
                }
                else if (HasCurrent)
                    throw new InvalidOperationException("CONFIG_SCHEMA_METADATA_MISSING");
                else
                    AtomicReplace(_schemaContract, schemaContract);
                if (!HasCurrent) AtomicReplace(_current, defaults);
                int revision = IdentifyCurrent(ReadCurrent());
                // Create the initial effective marker before any other writer may Commit.
                // Reopening an existing pending configuration must never reset this marker.
                if (!File.Exists(_effective))
                    AtomicReplace(_effective, revision.ToString(CultureInfo.InvariantCulture));
                return revision;
            }
        }
        private FileStream AcquireWriteLease()
        {
            return new FileStream(Path.Combine(_folder, "write.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public int Commit(string expectedCurrent, string newJson, MHSConfigAuditEntry audit)
        {
            // Cross-process exclusive lock for the compare-and-swap + atomic file replace.
            // A live second writer fails visibly instead of corrupting a revision sequence.
            using (FileStream lease = AcquireWriteLease())
            {
                if (!HasCurrent || !string.Equals(ReadCurrent(), expectedCurrent, StringComparison.Ordinal))
                    throw new InvalidOperationException("CONFIG_WRITE_CONFLICT: file changed outside this configuration session");
                int version = ListVersions().DefaultIfEmpty(0).Max() + 1;
                int previous = IdentifyCurrent(expectedCurrent);
                if (audit == null) throw new ArgumentNullException("audit");
                audit.Revision = Name(version);
                audit.JsonSha256 = MHSConfigPackage.Hash(newJson);
                SaveVersion(version, newJson);
                AtomicReplace(AuditFile(version), FCMJsonValueCodec.Serialize(audit));
                AtomicReplace(ParentFile(version), previous.ToString(CultureInfo.InvariantCulture));
                AtomicReplace(_current, newJson);
                return version;
            }
        }
        public IList<MHSConfigAuditEntry> ListAudit(int currentRevision)
        {
            List<MHSConfigAuditEntry> found = new List<MHSConfigAuditEntry>();
            foreach (int revision in CommittedHistory(currentRevision))
            {
                string file = AuditFile(revision);
                if (!File.Exists(file))
                {
                    // Initialization/recovery revisions have no audit; committed child revisions must.
                    if (File.Exists(ParentFile(revision)))
                        throw new InvalidOperationException("CONFIG_AUDIT_MISSING: " + Name(revision));
                    continue;
                }
                MHSConfigAuditEntry item = FCMJsonValueCodec.Deserialize<MHSConfigAuditEntry>(
                    File.ReadAllText(file, Utf8));
                if (item == null || item.Revision != Name(revision) ||
                    !string.Equals(item.JsonSha256, MHSConfigPackage.Hash(ReadVersion(revision)),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("CONFIG_AUDIT_INTEGRITY_FAILURE: " + Name(revision));
                found.Add(item);
            }
            return found.AsReadOnly();
        }
        public int PreviousCommitted(int revision)
        {
            string file = ParentFile(revision);
            if (!File.Exists(file)) return 0;
            int n;
            if (!int.TryParse(File.ReadAllText(file, Utf8).Trim(), out n) || n >= revision || n < 1 ||
                !File.Exists(RevisionFile(n))) throw new InvalidOperationException("Corrupted revision parent chain");
            return n;
        }
        public IList<int> CommittedHistory(int currentRevision)
        {
            List<int> reversed = new List<int>();
            int revision = currentRevision;
            while (revision > 0)
            {
                reversed.Add(revision);
                revision = PreviousCommitted(revision);
                if (reversed.Count > 1000000) throw new InvalidOperationException("Excessive revision history");
            }
            reversed.Reverse();
            return reversed;
        }
        public int ReadEffectiveOr(int latest)
        {
            if (!File.Exists(_effective)) return latest;
            string s = File.ReadAllText(_effective, Utf8).Trim();
            int revision;
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out revision) || !File.Exists(RevisionFile(revision)))
                throw new InvalidOperationException("Corrupted effective revision marker");
            return revision;
        }
        public int PreviousEffectiveRevision
        {
            get
            {
                int n;
                if (!File.Exists(_previousEffective)) return 0;
                if (!int.TryParse(File.ReadAllText(_previousEffective, Utf8).Trim(), out n) ||
                    !File.Exists(RevisionFile(n))) throw new InvalidOperationException("Corrupted previous effective revision");
                return n;
            }
        }
        /// <summary>
        /// Saved identity and effective predecessor are compared under the same OS lease
        /// as the durable event and pointer write; a stale session cannot claim success.
        /// </summary>
        public bool TrySetEffective(int expectedEffective, int expectedSaved,
            string expectedJson, string transactionId, IList<string> changedPaths)
        {
            using (FileStream lease = AcquireWriteLease())
            {
                if (!HasCurrent || !string.Equals(ReadCurrent(), expectedJson, StringComparison.Ordinal))
                    return false;
                if (IdentifyCurrent(expectedJson) != expectedSaved ||
                    ReadEffectiveOr(expectedSaved) != expectedEffective)
                    return false;
                if (!CommittedHistory(expectedSaved).Contains(expectedEffective))
                    throw new InvalidOperationException("CONFIG_EFFECTIVE_ANCESTRY_MISMATCH");
                SetEffectiveCore(expectedSaved, transactionId, changedPaths);
                return true;
            }
        }

        /// <summary>Journal first, pointer last; caller holds the exclusive write lease.</summary>
        private void SetEffectiveCore(int revision, string transactionId, IList<string> changedPaths)
        {
            if (!File.Exists(RevisionFile(revision))) throw new FileNotFoundException("Missing effective revision");
            if (File.Exists(_effective))
            {
                string previous = File.ReadAllText(_effective, Utf8).Trim();
                if (previous != revision.ToString(CultureInfo.InvariantCulture))
                {
                    int oldRevision;
                    if (!int.TryParse(previous, NumberStyles.None, CultureInfo.InvariantCulture,
                        out oldRevision) || oldRevision < 1 || oldRevision >= revision)
                        throw new InvalidOperationException("CONFIG_EFFECTIVE_REVISION_ORDER");
                    if (transactionId != null)
                    {
                        // Anchor the first journal transition before moving the effective pointer.
                        // If a crash occurs here the current effective marker is unchanged and valid.
                        if (!File.Exists(_effectiveJournalOrigin))
                            AtomicReplace(_effectiveJournalOrigin, oldRevision.ToString(CultureInfo.InvariantCulture));
                        MHSConfigEffectiveRecord evidence = new MHSConfigEffectiveRecord
                        {
                            Revision = Name(revision), PreviousVersion = Name(oldRevision),
                            TransactionId = transactionId, RecordedAtUtc = DateTimeOffset.UtcNow,
                            BodySha256 = MHSConfigPackage.Hash(ReadVersion(revision)),
                            ChangedPaths = new List<string>(changedPaths ?? new List<string>())
                        };
                        AtomicReplace(EffectiveEventFile(revision), FCMJsonValueCodec.Serialize(evidence));
                    }
                    AtomicReplace(_previousEffective, previous);
                }
            }
            AtomicReplace(_effective, revision.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>只能沿当前有效版本的前驱链重放；孤立的崩溃记录不属于生效事件。</summary>
        public IList<MHSConfigEffectiveRecord> ReadEffectiveHistory(int effectiveRevision)
        {
            List<MHSConfigEffectiveRecord> reversed = new List<MHSConfigEffectiveRecord>();
            int revision = effectiveRevision;
            int origin = effectiveRevision;
            if (File.Exists(_effectiveJournalOrigin))
            {
                if (!int.TryParse(File.ReadAllText(_effectiveJournalOrigin, Utf8).Trim(),
                    NumberStyles.None, CultureInfo.InvariantCulture, out origin) ||
                    origin < 1 || origin > effectiveRevision)
                    throw new InvalidOperationException("CONFIG_EFFECTIVE_JOURNAL_ORIGIN_INVALID");
            }
            while (revision > origin)
            {
                string path = EffectiveEventFile(revision);
                if (!File.Exists(path))
                    throw new InvalidOperationException("CONFIG_EFFECTIVE_JOURNAL_MISSING: " + Name(revision));
                MHSConfigEffectiveRecord record =
                    FCMJsonValueCodec.Deserialize<MHSConfigEffectiveRecord>(File.ReadAllText(path, Utf8));
                if (record == null || record.Revision != Name(revision) ||
                    record.TransactionId == null ||
                    !string.Equals(record.BodySha256, MHSConfigPackage.Hash(ReadVersion(revision)),
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("CONFIG_EFFECTIVE_JOURNAL_INTEGRITY: " + Name(revision));
                int previous = ParseName(record.PreviousVersion);
                if (previous >= revision) throw new InvalidOperationException("CONFIG_EFFECTIVE_JOURNAL_CYCLE");
                reversed.Add(record);
                revision = previous;
                if (reversed.Count > 1000000) throw new InvalidOperationException("CONFIG_EFFECTIVE_HISTORY_TOO_DEEP");
            }
            reversed.Reverse();
            return reversed.AsReadOnly();
        }

        private void SaveVersion(int version, string json)
        {
            string filename = RevisionFile(version);
            if (File.Exists(filename)) throw new InvalidOperationException("Revision collision");
            AtomicReplace(filename, json);
        }
        private static void AtomicReplace(string path, string content)
        {
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] data = Utf8.GetBytes(content);
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
                else File.Move(tmp, path);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
}