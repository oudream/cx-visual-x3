using System;
using System.IO;
using System.Text;
using System.Xml;

namespace CxVisual.DockingPoC
{
    /// <summary>只校验 UI 布局 XML 的输入边界，不保存工程对象。</summary>
    public static class LayoutPersistence
    {
        public const int MaxLayoutBytes = 1024 * 1024;

        public static string DefaultFilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "CxVisual", "AvalonDock-PoC", "workspace.layout.xml");
            }
        }

        public static void ValidateXml(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new InvalidDataException("布局文件为空。");
            if (Encoding.UTF8.GetByteCount(xml) > MaxLayoutBytes)
                throw new InvalidDataException("布局文件超过 1MB 上限。");

            XmlReaderSettings settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxLayoutBytes,
                MaxCharactersFromEntities = 0
            };
            using (StringReader input = new StringReader(xml))
            using (XmlReader reader = XmlReader.Create(input, settings))
            {
                // 提前禁止 DTD、外部实体和超限数据，避免对非可信 XML 直接反序列化。
                while (reader.Read()) { }
            }
        }

        public static string ReadChecked(string path)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("未找到布局文件。", path);
            if (file.Length > MaxLayoutBytes) throw new InvalidDataException("布局文件超过 1MB 上限。");
            string xml = File.ReadAllText(path, Encoding.UTF8);
            ValidateXml(xml);
            return xml;
        }

        public static void SaveAtomic(string path, string xml)
        {
            ValidateXml(xml);
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("无效布局路径。");
            Directory.CreateDirectory(directory);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, xml, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
