using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Lifecycle
{
    public sealed class CxConfigReleaseItem
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public int SchemaVersion { get; set; }
        public long Revision { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class CxConfigReleaseManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public string ReleaseId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<CxConfigReleaseItem> Items { get; set; } = new List<CxConfigReleaseItem>();
    }

    public sealed class CxConfigReleasePackage
    {
        public string Path { get; internal set; }
        public CxConfigReleaseManifest Manifest { get; internal set; }
    }

    /// <summary>版本化不可变发布包。SHA256用于偶然损坏检测，不替代可信签名/授权。</summary>
    public static class CxConfigReleaseBuilder
    {
        internal static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 96 || id == "." || id == "..")
                throw new ArgumentException("Invalid release/configuration identity: " + id);
            foreach (char ch in id)
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-'))
                    throw new ArgumentException("Unsafe identifier: " + id);
        }

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();
        }

        public static CxConfigReleasePackage Build(CxConfigManager manager,
            IEnumerable<string> configIds, string releaseRoot, string releaseId)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (configIds == null) throw new ArgumentNullException("configIds");
            ValidateId(releaseId);
            if (string.IsNullOrWhiteSpace(releaseRoot)) throw new ArgumentException("Release root required");
            string[] ids = configIds.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            if (ids.Length == 0) throw new ArgumentException("Cannot publish empty configuration package");
            foreach (string id in ids) ValidateId(id);
            string root = System.IO.Path.GetFullPath(releaseRoot);
            Directory.CreateDirectory(root);
            string destination = System.IO.Path.Combine(root, releaseId);
            if (Directory.Exists(destination)) throw new IOException("Release ID already exists and is immutable: " + releaseId);
            string temp = destination + ".building-" + Guid.NewGuid().ToString("N");
            CxConfigReleaseManifest manifest = new CxConfigReleaseManifest
            {
                ReleaseId = releaseId, CreatedUtc = DateTime.UtcNow
            };
            try
            {
                Directory.CreateDirectory(temp);
                foreach (string id in ids)
                {
                    string json = manager.ExportSavedJson(id);
                    CxConfigDocumentHeader hdr = CxConfigJsonFile.ReadHeader(json);
                    string name = "payload/" + id + ".json";
                    string targetFile = System.IO.Path.Combine(temp, "payload", id + ".json");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));
                    byte[] data = new UTF8Encoding(false).GetBytes(json);
                    File.WriteAllBytes(targetFile, data);
                    manifest.Items.Add(new CxConfigReleaseItem
                    {
                        Id = id, FileName = name, SchemaVersion = hdr.SchemaVersion,
                        Revision = hdr.Revision, Sha256 = Hash(data)
                    });
                }
                File.WriteAllText(System.IO.Path.Combine(temp, "manifest.json"),
                    CxConfigJsonFile.Serialize(manifest), Encoding.UTF8);
                Directory.Move(temp, destination);
                return new CxConfigReleasePackage { Path = destination, Manifest = manifest };
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static CxConfigReleaseManifest Verify(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath)) throw new ArgumentException("Package path required");
            string manifestPath = System.IO.Path.Combine(packagePath, "manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidDataException("Missing release manifest");
            CxConfigReleaseManifest manifest;
            try { manifest = (CxConfigReleaseManifest)CxConfigJsonFile.Deserialize(
                File.ReadAllText(manifestPath, Encoding.UTF8), typeof(CxConfigReleaseManifest)); }
            catch (Exception ex) { throw new InvalidDataException("Invalid release manifest", ex); }
            if (manifest == null || manifest.ManifestVersion != 1 ||
                manifest.Items == null || manifest.Items.Count == 0)
                throw new InvalidDataException("Unsupported or empty release manifest");
            ValidateId(manifest.ReleaseId);
            if (manifest.Items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Items.Count)
                throw new InvalidDataException("Duplicate configuration IDs");
            foreach (CxConfigReleaseItem item in manifest.Items)
            {
                if (item == null) throw new InvalidDataException("Null release item");
                ValidateId(item.Id);
                if (item.FileName != "payload/" + item.Id + ".json" || item.SchemaVersion < 1 || item.Revision < 0)
                    throw new InvalidDataException("Invalid release item");
                string file = System.IO.Path.Combine(packagePath, item.FileName);
                if (!File.Exists(file)) throw new InvalidDataException("Missing configuration payload " + item.FileName);
                byte[] bytes = File.ReadAllBytes(file);
                if (!string.Equals(Hash(bytes), item.Sha256, StringComparison.Ordinal))
                    throw new InvalidDataException("CONFIG_PACKAGE_HASH_MISMATCH: " + item.Id);
                string json;
                try { json = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException ex) { throw new InvalidDataException("Invalid UTF-8 payload", ex); }
                CxConfigDocumentHeader hdr = CxConfigJsonFile.ReadHeader(json);
                if (hdr.Id != item.Id || hdr.SchemaVersion != item.SchemaVersion || hdr.Revision != item.Revision)
                    throw new InvalidDataException("CONFIG_PACKAGE_HEADER_MISMATCH: " + item.Id);
            }
            return manifest;
        }
    }

    public sealed class CxConfigActivePointer
    {
        public string ActiveId { get; set; }
        public string PreviousId { get; set; }
        public long Revision { get; set; }
        public DateTime ChangedUtc { get; set; }
    }

    /// <summary>
    /// L4文件/IPC边界的Runtime接收端：Stage → 校验 → Activate → Rollback。
    /// 多进程可经共享目录协作；跨机器网络传输由后续适配器负责。
    /// </summary>
    public sealed class CxConfigReleaseReceiver
    {
        private readonly object _gate = new object();
        private readonly string _root;
        public CxConfigReleaseReceiver(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Runtime directory required");
            _root = System.IO.Path.GetFullPath(root);
            Directory.CreateDirectory(System.IO.Path.Combine(_root, "releases"));
        }

        private string Releases { get { return System.IO.Path.Combine(_root, "releases"); } }
        private string PointerFile { get { return System.IO.Path.Combine(_root, "active.json"); } }

        public string ActiveReleaseId
        {
            get { lock (_gate) return ReadPointer().ActiveId; }
        }

        public string Stage(string packagePath)
        {
            CxConfigReleaseManifest manifest = CxConfigReleaseBuilder.Verify(packagePath);
            lock (_gate)
            {
                string dest = System.IO.Path.Combine(Releases, manifest.ReleaseId);
                if (Directory.Exists(dest))
                {
                    CxConfigReleaseManifest existing = CxConfigReleaseBuilder.Verify(dest);
                    if (CxConfigJsonFile.Serialize(existing.Items.Select(x => x.Sha256).ToArray()) !=
                        CxConfigJsonFile.Serialize(manifest.Items.Select(x => x.Sha256).ToArray()))
                        throw new InvalidDataException("Immutable release ID has different payload");
                    return manifest.ReleaseId;
                }
                string temp = dest + ".staging-" + Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(temp);
                    foreach (CxConfigReleaseItem item in manifest.Items)
                    {
                        string targetFile = System.IO.Path.Combine(temp, item.FileName);
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));
                        File.Copy(System.IO.Path.Combine(packagePath, item.FileName), targetFile);
                    }
                    File.Copy(System.IO.Path.Combine(packagePath, "manifest.json"),
                        System.IO.Path.Combine(temp, "manifest.json"));
                    CxConfigReleaseBuilder.Verify(temp);
                    Directory.Move(temp, dest);
                    return manifest.ReleaseId;
                }
                finally
                {
                    try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public void Activate(string releaseId)
        {
            CxConfigReleaseBuilder.ValidateId(releaseId);
            lock (_gate)
            {
                CxConfigReleaseBuilder.Verify(System.IO.Path.Combine(Releases, releaseId));
                using (FileStream gate = OpenPointerGate())
                {
                    CxConfigActivePointer old = ReadPointer();
                    if (old.ActiveId == releaseId) return;
                    WritePointer(new CxConfigActivePointer
                    {
                        ActiveId = releaseId, PreviousId = old.ActiveId,
                        Revision = checked(old.Revision + 1), ChangedUtc = DateTime.UtcNow
                    });
                }
            }
        }

        public void Rollback()
        {
            lock (_gate)
            {
                using (FileStream gate = OpenPointerGate())
                {
                    CxConfigActivePointer old = ReadPointer();
                    if (old.ActiveId == null) return;
                    if (old.PreviousId != null)
                        CxConfigReleaseBuilder.Verify(System.IO.Path.Combine(Releases, old.PreviousId));
                    WritePointer(new CxConfigActivePointer
                    {
                        ActiveId = old.PreviousId, PreviousId = old.ActiveId,
                        Revision = checked(old.Revision + 1), ChangedUtc = DateTime.UtcNow
                    });
                }
            }
        }

        public string ReadActiveConfigJson(string configId)
        {
            CxConfigReleaseBuilder.ValidateId(configId);
            lock (_gate)
            {
                string id = ReadPointer().ActiveId;
                if (id == null) throw new InvalidOperationException("No active release");
                string directory = System.IO.Path.Combine(Releases, id);
                CxConfigReleaseManifest manifest = CxConfigReleaseBuilder.Verify(directory);
                if (!manifest.Items.Any(x => x.Id == configId))
                    throw new KeyNotFoundException("Release does not contain " + configId);
                CxConfigReleaseItem item = manifest.Items.First(x => x.Id == configId);
                return File.ReadAllText(System.IO.Path.Combine(directory, item.FileName), Encoding.UTF8);
            }
        }

        private CxConfigActivePointer ReadPointer()
        {
            if (!File.Exists(PointerFile)) return new CxConfigActivePointer();
            try
            {
                CxConfigActivePointer p = (CxConfigActivePointer)CxConfigJsonFile.Deserialize(
                    File.ReadAllText(PointerFile, Encoding.UTF8), typeof(CxConfigActivePointer));
                if (p == null || p.Revision < 0) throw new InvalidDataException("Invalid active pointer");
                if (p.ActiveId != null) CxConfigReleaseBuilder.ValidateId(p.ActiveId);
                if (p.PreviousId != null) CxConfigReleaseBuilder.ValidateId(p.PreviousId);
                return p;
            }
            catch (Exception ex) { throw new InvalidDataException("Invalid active release pointer", ex); }
        }

        private FileStream OpenPointerGate()
        {
            return new FileStream(PointerFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private void WritePointer(CxConfigActivePointer value)
        {
            string temp = PointerFile + ".tmp";
            try
            {
                File.WriteAllText(temp, CxConfigJsonFile.Serialize(value), Encoding.UTF8);
                if (File.Exists(PointerFile)) File.Replace(temp, PointerFile, PointerFile + ".bak");
                else File.Move(temp, PointerFile);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class CxConfigPublishReport
    {
        public bool Success { get; internal set; }
        public string Error { get; internal set; }
        public int StagedCount { get; internal set; }
        public int ActivatedCount { get; internal set; }
        public int RolledBackCount { get; internal set; }
    }

    public static class CxConfigPublisher
    {
        /// <summary>
        /// 先全部Stage，再依次激活。激活失败时尽力回滚已切换目标；
        /// 不承诺跨机器分布式原子事务。失败报告必须交给操作员。
        /// </summary>
        public static CxConfigPublishReport Publish(string packagePath, params CxConfigReleaseReceiver[] targets)
        {
            if (targets == null || targets.Length == 0) throw new ArgumentException("At least one target required");
            CxConfigPublishReport report = new CxConfigPublishReport();
            List<CxConfigReleaseReceiver> activated = new List<CxConfigReleaseReceiver>();
            try
            {
                CxConfigReleaseManifest manifest = CxConfigReleaseBuilder.Verify(packagePath);
                foreach (CxConfigReleaseReceiver target in targets)
                {
                    if (target == null) throw new ArgumentNullException("targets");
                    target.Stage(packagePath);
                    report.StagedCount++;
                }
                foreach (CxConfigReleaseReceiver target in targets)
                {
                    target.Activate(manifest.ReleaseId);
                    activated.Add(target);
                    report.ActivatedCount++;
                }
                report.Success = true;
                return report;
            }
            catch (Exception ex)
            {
                report.Error = ex.ToString();
                for (int i = activated.Count - 1; i >= 0; --i)
                {
                    try { activated[i].Rollback(); report.RolledBackCount++; }
                    catch (Exception rollbackEx) { report.Error += "\nROLLBACK_FAILED: " + rollbackEx; }
                }
                return report;
            }
        }
    }
}
