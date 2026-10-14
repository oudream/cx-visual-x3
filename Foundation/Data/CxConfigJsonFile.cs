using System;
using System.IO;
using System.Web.Script.Serialization;

namespace CxVisual.Foundation.Data
{
    /// <summary>版本化配置文档。Id是逻辑身份；Revision是每个配置实例的乐观并发版本。</summary>
    public sealed class CxConfigFileDocument<T>
    {
        public string Id { get; set; }
        public int SchemaVersion { get; set; }
        public long Revision { get; set; }
        public T Data { get; set; }
    }

    /// <summary>F15配置的JSON载体。安全保存由同目录临时文件、跨进程排他锁及File.Replace组成。</summary>
    public static class CxConfigJsonFile
    {
        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 100 };
        }

        public static T Clone<T>(T value)
        {
            if (value == null) return default(T);
            JavaScriptSerializer serializer = Serializer();
            return serializer.Deserialize<T>(serializer.Serialize(value));
        }

        public static string Serialize<T>(T value)
        {
            return Serializer().Serialize(value);
        }

        public static CxConfigFileDocument<T> Read<T>(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Missing path", "path");
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            try
            {
                CxConfigFileDocument<T> doc = Serializer().Deserialize<CxConfigFileDocument<T>>(json);
                if (doc == null || doc.Data == null || doc.Revision < 0 || doc.SchemaVersion <= 0 ||
                    string.IsNullOrWhiteSpace(doc.Id))
                    throw new InvalidDataException("Invalid config document: " + path);
                return doc;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidDataException("Cannot read configuration; original file was not modified: " + path, ex);
            }
        }

        /// <summary>
        /// 仅在磁盘版本符合expectedRevision时保存。首写用-1；遇锁/磁盘错误抛出异常且不删除现有目标。
        /// 不承诺多个独立配置文件的跨文件原子事务。
        /// </summary>
        public static void Save<T>(string path, CxConfigFileDocument<T> document, long expectedRevision)
        {
            if (document == null || document.Data == null) throw new ArgumentNullException("document");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Missing path", "path");
            if (document.Revision < 0 || document.SchemaVersion < 1) throw new ArgumentOutOfRangeException("document");
            string full = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(full);
            Directory.CreateDirectory(directory);
            using (FileStream gate = new FileStream(full + ".lock", FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None))
            {
                CxConfigFileDocument<T> existing = Read<T>(full);
                long actual = existing == null ? -1 : existing.Revision;
                if (actual != expectedRevision)
                    throw new InvalidOperationException("CONFIG_DISK_CONFLICT: " + document.Id +
                        " expected " + expectedRevision + " but disk has " + actual);
                string temp = full + ".tmp";
                try
                {
                    string json = Serializer().Serialize(document);
                    File.WriteAllText(temp, json);
                    // 文件写入成功后再切换；Windows File.Replace 保留旧文件作为 .bak。
                    if (existing != null) File.Replace(temp, full, full + ".bak");
                    else File.Move(temp, full);
                }
                finally
                {
                    // 若写入失败，不触碰原文件；.tmp如果已创建则清理。异常不覆盖原始异常。
                    try { if (File.Exists(temp)) File.Delete(temp); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
