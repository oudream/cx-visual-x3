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
    public sealed class MVDConfigReleaseItem
    {
        public string Id { get; set; }
        public string FileName { get; set; }
        public int SchemaVersion { get; set; }
        public long Revision { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class MVDConfigReleaseManifest
    {
        public int ManifestVersion { get; set; } = 1;
        public string ReleaseId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<MVDConfigReleaseItem> Items { get; set; } = new List<MVDConfigReleaseItem>();
    }

    public sealed class MVDConfigReleasePackage
    {
        public string Path { get; internal set; }
        public MVDConfigReleaseManifest Manifest { get; internal set; }
    }

    /// <summary>版本化不可变发布包。SHA256用于偶然损坏检测，不替代可信签名/授权。</summary>
    public static class MVDConfigReleaseBuilder
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

        public static MVDConfigReleasePackage Build(MTCConfigManager manager,
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
            MVDConfigReleaseManifest manifest = new MVDConfigReleaseManifest
            {
                ReleaseId = releaseId, CreatedUtc = DateTime.UtcNow
            };
            try
            {
                Directory.CreateDirectory(temp);
                foreach (string id in ids)
                {
                    string json = manager.ExportSavedJson(id);
                    FCMConfigDocumentHeader hdr = FCMConfigJsonFile.ReadHeader(json);
                    string name = "payload/" + id + ".json";
                    string targetFile = System.IO.Path.Combine(temp, "payload", id + ".json");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));
                    byte[] data = new UTF8Encoding(false).GetBytes(json);
                    File.WriteAllBytes(targetFile, data);
                    manifest.Items.Add(new MVDConfigReleaseItem
                    {
                        Id = id, FileName = name, SchemaVersion = hdr.SchemaVersion,
                        Revision = hdr.Revision, Sha256 = Hash(data)
                    });
                }
                File.WriteAllText(System.IO.Path.Combine(temp, "manifest.json"),
                    FCMConfigJsonFile.Serialize(manifest), Encoding.UTF8);
                Directory.Move(temp, destination);
                return new MVDConfigReleasePackage { Path = destination, Manifest = manifest };
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public static MVDConfigReleaseManifest Verify(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath)) throw new ArgumentException("Package path required");
            string manifestPath = System.IO.Path.Combine(packagePath, "manifest.json");
            if (!File.Exists(manifestPath)) throw new InvalidDataException("Missing release manifest");
            MVDConfigReleaseManifest manifest;
            try { manifest = (MVDConfigReleaseManifest)FCMConfigJsonFile.Deserialize(
                File.ReadAllText(manifestPath, Encoding.UTF8), typeof(MVDConfigReleaseManifest)); }
            catch (Exception ex) { throw new InvalidDataException("Invalid release manifest", ex); }
            if (manifest == null || manifest.ManifestVersion != 1 ||
                manifest.Items == null || manifest.Items.Count == 0)
                throw new InvalidDataException("Unsupported or empty release manifest");
            ValidateId(manifest.ReleaseId);
            if (manifest.Items.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Items.Count)
                throw new InvalidDataException("Duplicate configuration IDs");
            foreach (MVDConfigReleaseItem item in manifest.Items)
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
                FCMConfigDocumentHeader hdr = FCMConfigJsonFile.ReadHeader(json);
                if (hdr.Id != item.Id || hdr.SchemaVersion != item.SchemaVersion || hdr.Revision != item.Revision)
                    throw new InvalidDataException("CONFIG_PACKAGE_HEADER_MISMATCH: " + item.Id);
            }
            return manifest;
        }
    }

    public sealed class MVDConfigActivePointer
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
    public sealed class MVDConfigReleaseReceiver
    {
        private readonly object _gate = new object();
        private readonly string _root;
        public MVDConfigReleaseReceiver(string root)
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
            MVDConfigReleaseManifest manifest = MVDConfigReleaseBuilder.Verify(packagePath);
            lock (_gate)
            {
                string dest = System.IO.Path.Combine(Releases, manifest.ReleaseId);
                if (Directory.Exists(dest))
                {
                    MVDConfigReleaseManifest existing = MVDConfigReleaseBuilder.Verify(dest);
                    if (FCMConfigJsonFile.Serialize(existing.Items.Select(x => x.Sha256).ToArray()) !=
                        FCMConfigJsonFile.Serialize(manifest.Items.Select(x => x.Sha256).ToArray()))
                        throw new InvalidDataException("Immutable release ID has different payload");
                    return manifest.ReleaseId;
                }
                string temp = dest + ".staging-" + Guid.NewGuid().ToString("N");
                try
                {
                    Directory.CreateDirectory(temp);
                    foreach (MVDConfigReleaseItem item in manifest.Items)
                    {
                        string targetFile = System.IO.Path.Combine(temp, item.FileName);
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));
                        File.Copy(System.IO.Path.Combine(packagePath, item.FileName), targetFile);
                    }
                    File.Copy(System.IO.Path.Combine(packagePath, "manifest.json"),
                        System.IO.Path.Combine(temp, "manifest.json"));
                    MVDConfigReleaseBuilder.Verify(temp);
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
            MVDConfigReleaseBuilder.ValidateId(releaseId);
            lock (_gate)
            {
                MVDConfigReleaseBuilder.Verify(System.IO.Path.Combine(Releases, releaseId));
                using (FileStream gate = OpenPointerGate())
                {
                    MVDConfigActivePointer old = ReadPointer();
                    if (old.ActiveId == releaseId) return;
                    WritePointer(new MVDConfigActivePointer
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
                    MVDConfigActivePointer old = ReadPointer();
                    if (old.ActiveId == null) return;
                    if (old.PreviousId != null)
                        MVDConfigReleaseBuilder.Verify(System.IO.Path.Combine(Releases, old.PreviousId));
                    WritePointer(new MVDConfigActivePointer
                    {
                        ActiveId = old.PreviousId, PreviousId = old.ActiveId,
                        Revision = checked(old.Revision + 1), ChangedUtc = DateTime.UtcNow
                    });
                }
            }
        }

        public string ReadActiveConfigJson(string configId)
        {
            MVDConfigReleaseBuilder.ValidateId(configId);
            lock (_gate)
            {
                string id = ReadPointer().ActiveId;
                if (id == null) throw new InvalidOperationException("No active release");
                string directory = System.IO.Path.Combine(Releases, id);
                MVDConfigReleaseManifest manifest = MVDConfigReleaseBuilder.Verify(directory);
                if (!manifest.Items.Any(x => x.Id == configId))
                    throw new KeyNotFoundException("Release does not contain " + configId);
                MVDConfigReleaseItem item = manifest.Items.First(x => x.Id == configId);
                return File.ReadAllText(System.IO.Path.Combine(directory, item.FileName), Encoding.UTF8);
            }
        }

        private MVDConfigActivePointer ReadPointer()
        {
            if (!File.Exists(PointerFile)) return new MVDConfigActivePointer();
            try
            {
                MVDConfigActivePointer p = (MVDConfigActivePointer)FCMConfigJsonFile.Deserialize(
                    File.ReadAllText(PointerFile, Encoding.UTF8), typeof(MVDConfigActivePointer));
                if (p == null || p.Revision < 0) throw new InvalidDataException("Invalid active pointer");
                if (p.ActiveId != null) MVDConfigReleaseBuilder.ValidateId(p.ActiveId);
                if (p.PreviousId != null) MVDConfigReleaseBuilder.ValidateId(p.PreviousId);
                return p;
            }
            catch (Exception ex) { throw new InvalidDataException("Invalid active release pointer", ex); }
        }

        private FileStream OpenPointerGate()
        {
            return new FileStream(PointerFile + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private void WritePointer(MVDConfigActivePointer value)
        {
            string temp = PointerFile + ".tmp";
            try
            {
                File.WriteAllText(temp, FCMConfigJsonFile.Serialize(value), Encoding.UTF8);
                if (File.Exists(PointerFile)) File.Replace(temp, PointerFile, PointerFile + ".bak");
                else File.Move(temp, PointerFile);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }

    public sealed class MVDConfigPublishReport
    {
        public bool Success { get; internal set; }
        public string Error { get; internal set; }
        public int StagedCount { get; internal set; }
        public int ActivatedCount { get; internal set; }
        public int RolledBackCount { get; internal set; }
    }

    public static class MVDConfigPublisher
    {
        /// <summary>
        /// 先全部Stage，再依次激活。激活失败时尽力回滚已切换目标；
        /// 不承诺跨机器分布式原子事务。失败报告必须交给操作员。
        /// </summary>
        public static MVDConfigPublishReport Publish(string packagePath, params MVDConfigReleaseReceiver[] targets)
        {
            if (targets == null || targets.Length == 0) throw new ArgumentException("At least one target required");
            MVDConfigPublishReport report = new MVDConfigPublishReport();
            List<MVDConfigReleaseReceiver> activated = new List<MVDConfigReleaseReceiver>();
            try
            {
                MVDConfigReleaseManifest manifest = MVDConfigReleaseBuilder.Verify(packagePath);
                foreach (MVDConfigReleaseReceiver target in targets)
                {
                    if (target == null) throw new ArgumentNullException("targets");
                    target.Stage(packagePath);
                    report.StagedCount++;
                }
                foreach (MVDConfigReleaseReceiver target in targets)
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
