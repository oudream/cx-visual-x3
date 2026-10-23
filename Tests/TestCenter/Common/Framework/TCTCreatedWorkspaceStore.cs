using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.TestCenter.UI
{
    // TestCenter工作台导航元数据；不是MEM工程模型、MHS配置或第二套Catalog/Run。
    public sealed class TCTCreatedWorkspace
    {
        public string Id { get; private set; }
        public string TemplateId { get; private set; }
        public string Title { get; private set; }
        public string ConfigurationId { get; private set; }
        private string[] _caseIds = new string[0];
        public string[] CaseIds { get { return (string[])_caseIds.Clone(); } }
        internal void SetCases(IEnumerable<string> ids)
        {
            string[] items = ids.Distinct(StringComparer.Ordinal).ToArray();
            if (items.Length > 5000 || items.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
                throw new InvalidDataException("用例引用不合法");
            _caseIds = items;
        }
        internal TCTCreatedWorkspace(string id, string template, string title, string configurationId)
        {
            if (!Guid.TryParseExact(id, "N", out Guid ignored) ||
                string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(title) ||
                title.Length > 140 || (configurationId ?? "").Length > 80)
                throw new InvalidDataException("工作区实例元数据不合法");
            Id = "tct.instance." + id;
            TemplateId = template; Title = title; ConfigurationId = configurationId ?? "";
        }
        internal static TCTCreatedWorkspace Load(string id, string template, string title, string configurationId)
        {
            const string prefix = "tct.instance.";
            if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal))
                throw new InvalidDataException("工作区实例身份不合法");
            return new TCTCreatedWorkspace(id.Substring(prefix.Length), template, title, configurationId);
        }
    }
    public sealed class TCTCreatedWorkspaceStore
    {
        private readonly string _path;
        private readonly List<TCTCreatedWorkspace> _items = new List<TCTCreatedWorkspace>();
        private bool _writesBlocked;
        public bool IsRecoveryBlocked { get { return _writesBlocked; } }
        private void EnsureWritable()
        {
            if (_writesBlocked)
                throw new InvalidOperationException(
                    "原工作区记录校验失败，已禁止覆盖；请备份并修复原文件后重开");
        }
        public IReadOnlyList<TCTCreatedWorkspace> Items { get { return _items.AsReadOnly(); } }
        public TCTCreatedWorkspaceStore(string path)
        { _path = Path.GetFullPath(path ?? throw new ArgumentNullException("path")); }
        public TCTCreatedWorkspace Create(string templateId, string title, string configId)
        {
            EnsureWritable();
            if (_items.Count >= 2000) throw new InvalidOperationException("工作区实例超过上限");
            string unique = Guid.NewGuid().ToString("N");
            // 模块MHS Scope绝不按可复用的显示序号命名，避免删除后重新创建误用旧配置。
            string uniqueConfigId = string.IsNullOrWhiteSpace(configId)
                ? "" : configId + "_" + unique.Substring(0, 12);
            TCTCreatedWorkspace item = new TCTCreatedWorkspace(unique,
                templateId, title, uniqueConfigId);
            _items.Add(item);
            try { Save(); }
            catch { _items.Remove(item); throw; }
            return item;
        }
        public void UpdateCases(string instanceId, IEnumerable<string> ids)
        {
            EnsureWritable();
            TCTCreatedWorkspace record = _items.FirstOrDefault(x => x.Id == instanceId);
            if (record == null) throw new InvalidOperationException("测试计划实例不存在");
            string[] previous = record.CaseIds;
            record.SetCases(ids);
            try { Save(); }
            catch { record.SetCases(previous); throw; }
        }
        public bool Delete(string id)
        {
            EnsureWritable();
            TCTCreatedWorkspace item = _items.FirstOrDefault(x => x.Id == id);
            if (item == null) return false;
            _items.Remove(item);
            try { Save(); }
            catch { _items.Add(item); throw; }
            return true;
        }
        public void Load(IEnumerable<string> allowedTemplates)
        {
            // 成功完成严格校验前始终禁止写回，防止破坏待取证的原文件。
            _writesBlocked = true;
            _items.Clear();
            if (!File.Exists(_path)) { _writesBlocked = false; return; }
            if (new FileInfo(_path).Length > 512 * 1024)
                throw new InvalidDataException("工作区列表超出安全长度");
            HashSet<string> allowed = new HashSet<string>(allowedTemplates, StringComparer.Ordinal);
            XmlReaderSettings settings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 512 * 1024 };
            XDocument xml;
            using (XmlReader reader = XmlReader.Create(_path, settings))
                xml = XDocument.Load(reader);
            if (xml.Root == null || xml.Root.Name != "TCTCreatedWorkspaces" ||
                (string)xml.Root.Attribute("version") != "1")
                throw new InvalidDataException("工作区格式版本不支持");
            XElement[] entries = xml.Root.Elements().ToArray();
            if (entries.Length > 2000 || entries.Any(x => x.Name != "Workspace"))
                throw new InvalidDataException("工作区列表包含无效条目");
            List<TCTCreatedWorkspace> loaded = new List<TCTCreatedWorkspace>();
            foreach (XElement e in entries)
            {
                TCTCreatedWorkspace item = TCTCreatedWorkspace.Load(
                    (string)e.Attribute("id"), (string)e.Attribute("template"),
                    (string)e.Attribute("title"), (string)e.Attribute("configuration"));
                if (!allowed.Contains(item.TemplateId) || loaded.Any(x => x.Id == item.Id))
                    throw new InvalidDataException("工作区引用非法或ID重复");
                XElement[] cases = e.Elements().ToArray();
                if (cases.Any(x => x.Name != "Case"))
                    throw new InvalidDataException("未知的工作区子记录");
                item.SetCases(cases.Select(x => (string)x.Attribute("id")));
                loaded.Add(item);
            }
            _items.AddRange(loaded);
            _writesBlocked = false;
        }
        private void Save()
        {
            EnsureWritable();
            XElement root = new XElement("TCTCreatedWorkspaces", new XAttribute("version", "1"));
            foreach (TCTCreatedWorkspace entry in _items)
            {
                XElement workspace = new XElement("Workspace", new XAttribute("id", entry.Id),
                    new XAttribute("template", entry.TemplateId),
                    new XAttribute("title", entry.Title),
                    new XAttribute("configuration", entry.ConfigurationId));
                foreach (string caseId in entry.CaseIds)
                    workspace.Add(new XElement("Case", new XAttribute("id", caseId)));
                root.Add(workspace);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string tmp = _path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                File.WriteAllText(tmp, root.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
                if (File.Exists(_path)) File.Replace(tmp, _path, null);
                else File.Move(tmp, _path);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
}
