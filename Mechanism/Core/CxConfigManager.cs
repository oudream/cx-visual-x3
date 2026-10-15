using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Foundation.Data;

namespace CxVisual.Mechanism.Core
{
    public sealed class CxConfigChange
    {
        public string Id { get; internal set; }
        public long Revision { get; internal set; }
        public bool Saved { get; internal set; }
        public DateTime ChangedUtc { get; internal set; }
    }

    /// <summary>一次锁内捕获的配置值与版本；Value为调用者独享的可编辑克隆，不是内部运行引用。</summary>
    public sealed class CxConfigSnapshot<T> where T : class
    {
        public string Id { get; internal set; }
        public T Value { get; internal set; }
        public long Revision { get; internal set; }
        public bool Saved { get; internal set; }
    }

    /// <summary>
    /// M04/M07 L1统一配置入口。写入采用乐观并发、不可变快照语义，通知在锁外执行。
    /// Update只提交内存版本；UpdateAndSave在磁盘成功后才发布内存版本。
    /// </summary>
    public sealed class CxConfigManager
    {
        private sealed class Loaded
        {
            public object Data;
            public long Revision;
            public int SchemaVersion;
            public string Id;
            public string RawJson;
        }
        private sealed class Entry
        {
            public string Id, Path;
            public Type Type;
            public int SchemaVersion;
            public bool RequireExisting;
            public Func<object> Default;
            public Func<object, object> Clone;
            public Func<object, string> Validate;
            public Func<Loaded> Load;
            public Func<int, string, object> Migrate;
            public Action<object, long, long> Save;
            public object Data;
            public long Revision, PersistedRevision;
            public List<Action<object, CxConfigChange>> Listeners = new List<Action<object, CxConfigChange>>();
        }

        private sealed class Subscription : IDisposable
        {
            private readonly CxConfigManager _manager;
            private readonly Entry _entry;
            private readonly Action<object, CxConfigChange> _listener;
            private bool _disposed;
            public Subscription(CxConfigManager manager, Entry entry, Action<object, CxConfigChange> listener)
            { _manager = manager; _entry = entry; _listener = listener; }
            public void Dispose()
            {
                lock (_manager._gate)
                {
                    if (_disposed) return;
                    _entry.Listeners.Remove(_listener);
                    _disposed = true;
                }
            }
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly string _directory;
        private bool _initialized;
        public event Action<string, Exception> NotificationFailed;
        public CxConfigManager(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Configuration root required", "directory");
            _directory = Path.GetFullPath(directory);
        }

        public void Register<T>(string id, bool requireExisting = false, int schemaVersion = 1,
            Func<T, string> validator = null, Func<int, string, T> migrate = null) where T : class
        {
            ValidateId(id);
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException("schemaVersion");
            string path = Path.Combine(_directory, id + ".json");
            Entry entry = new Entry
            {
                Id = id, Path = path, Type = typeof(T),
                SchemaVersion = schemaVersion, RequireExisting = requireExisting,
                Default = () => Activator.CreateInstance(typeof(T), true),
                Migrate = migrate == null ? null : new Func<int, string, object>((version, json) => migrate(version, json)),
                Clone = value => CxConfigJsonFile.Clone((T)value),
                Validate = value =>
                {
                    string error = CxConfigDescription.Validate((T)value);
                    return error ?? (validator == null ? null : validator((T)value));
                },
                Load = () =>
                {
                    if (!File.Exists(path)) return null;
                    string raw = File.ReadAllText(path);
                    CxConfigDocumentHeader header = CxConfigJsonFile.ReadHeader(raw);
                    if (header.SchemaVersion != schemaVersion)
                        return new Loaded { Id = header.Id, Revision = header.Revision,
                            SchemaVersion = header.SchemaVersion, RawJson = raw };
                    CxConfigFileDocument<T> doc = CxConfigJsonFile.ParseDocument<T>(raw, path);
                    return new Loaded { Data = doc.Data, Id = doc.Id,
                        Revision = doc.Revision, SchemaVersion = doc.SchemaVersion, RawJson = raw };
                },
                Save = (value, revision, expected) => CxConfigJsonFile.Save(path,
                    new CxConfigFileDocument<T>
                    {
                        Id = id, SchemaVersion = schemaVersion,
                        Revision = revision, Data = (T)value
                    }, expected)
            };
            lock (_gate)
            {
                if (_initialized) throw new InvalidOperationException("Register before Initialize()");
                if (_entries.ContainsKey(id)) throw new InvalidOperationException("Duplicate configuration ID: " + id);
                _entries.Add(id, entry);
            }
        }

        /// <summary>缺失非关键配置时生成默认值并持久化；损坏/未来版本/无效配置拒绝覆盖。</summary>
        public void Initialize()
        {
            lock (_gate)
            {
                if (_initialized) return;
                foreach (Entry e in _entries.Values)
                {
                    Loaded doc = e.Load();
                    if (doc == null)
                    {
                        if (e.RequireExisting) throw new FileNotFoundException("Required configuration missing", e.Path);
                        object initial = e.Default();
                        Validate(e, initial);
                        e.Save(initial, 0, -1);
                        e.Data = e.Clone(initial);
                        e.Revision = e.PersistedRevision = 0;
                    }
                    else
                    {
                        if (doc.Id != e.Id) throw new InvalidDataException("Config identity mismatch: " + e.Id);
                        if (doc.SchemaVersion != e.SchemaVersion)
                        {
                            if (doc.SchemaVersion > e.SchemaVersion || e.Migrate == null)
                                throw new InvalidDataException("Unsupported schema " + doc.SchemaVersion + " for " + e.Id +
                                    ", expected " + e.SchemaVersion + "; explicit migration required");
                            object migrated = e.Migrate(doc.SchemaVersion, doc.RawJson);
                            Validate(e, migrated);
                            long next = checked(doc.Revision + 1);
                            e.Save(migrated, next, doc.Revision);
                            e.Data = e.Clone(migrated);
                            e.Revision = e.PersistedRevision = next;
                        }
                        else
                        {
                            Validate(e, doc.Data);
                            e.Data = e.Clone(doc.Data);
                            e.Revision = e.PersistedRevision = doc.Revision;
                        }
                    }
                }
                _initialized = true;
            }
        }

        public T Get<T>(string id) where T : class
        {
            return GetSnapshot<T>(id).Value;
        }

        /// <summary>同时获取Value/Revision/Saved，避免跨线程两次查询导致撕裂快照。</summary>
        public CxConfigSnapshot<T> GetSnapshot<T>(string id) where T : class
        {
            lock (_gate)
            {
                Entry e = Resolve<T>(id);
                return new CxConfigSnapshot<T>
                {
                    Id = id, Value = (T)e.Clone(e.Data),
                    Revision = e.Revision, Saved = e.Revision == e.PersistedRevision
                };
            }
        }

        public long GetRevision(string id)
        {
            lock (_gate) { return Resolve(id).Revision; }
        }
        public bool IsDirty(string id)
        {
            lock (_gate)
            {
                Entry e = Resolve(id);
                return e.Revision != e.PersistedRevision;
            }
        }

        public void Update<T>(string id, Action<T> edit, long? expectedRevision = null) where T : class
        { Change(id, edit, false, expectedRevision); }

        public void UpdateAndSave<T>(string id, Action<T> edit, long? expectedRevision = null) where T : class
        { Change(id, edit, true, expectedRevision); }

        private void Change<T>(string id, Action<T> edit, bool saveNow, long? expectedRevision) where T : class
        {
            if (edit == null) throw new ArgumentNullException("edit");
            Entry e;
            T draft;
            long baseRevision;
            lock (_gate)
            {
                e = Resolve<T>(id);
                baseRevision = e.Revision;
                if (expectedRevision.HasValue && expectedRevision.Value != baseRevision)
                    throw new InvalidOperationException("CONFIG_REVISION_CONFLICT: " + id);
                draft = (T)e.Clone(e.Data);
            }
            edit(draft);
            Validate(e, draft);
            Action<object, CxConfigChange>[] listeners;
            CxConfigChange change;
            lock (_gate)
            {
                if (e.Revision != baseRevision)
                    throw new InvalidOperationException("CONFIG_REVISION_CONFLICT: " + id);
                // 无变更时不产生新的版本或重复通知。
                if (CxConfigJsonFile.Serialize(e.Data) == CxConfigJsonFile.Serialize(draft)) return;
                long nextRevision = checked(e.Revision + 1);
                if (saveNow) e.Save(draft, nextRevision, e.PersistedRevision);
                e.Data = e.Clone(draft);
                e.Revision = nextRevision;
                if (saveNow) e.PersistedRevision = nextRevision;
                listeners = e.Listeners.ToArray();
                change = new CxConfigChange
                {
                    Id = id, Revision = nextRevision, Saved = saveNow, ChangedUtc = DateTime.UtcNow
                };
            }
            foreach (Action<object, CxConfigChange> callback in listeners)
            {
                try { callback(e.Clone(draft), change); }
                catch (Exception ex)
                {
                    Action<string, Exception> failed = NotificationFailed;
                    if (failed != null)
                    {
                        try { failed(id, ex); } catch { /* notification failure must not undo committed data */ }
                    }
                }
            }
        }

        /// <summary>保存已提交的内存版本；磁盘被其他进程更新时拒绝覆盖。</summary>
        public void Save(string id)
        {
            lock (_gate)
            {
                Entry e = Resolve(id);
                if (e.Revision == e.PersistedRevision) return;
                e.Save(e.Data, e.Revision, e.PersistedRevision);
                e.PersistedRevision = e.Revision;
            }
        }


        /// <summary>L2：将上一份.bak在当前版本之后作为新Revision恢复，不回拨Revision。</summary>
        public void RestoreBackup<T>(string id) where T : class
        {
            Entry e;
            lock (_gate) { e = Resolve<T>(id); }
            string backupPath = e.Path + ".bak";
            CxConfigFileDocument<T> backup = CxConfigJsonFile.Read<T>(backupPath);
            if (backup == null) throw new FileNotFoundException("No configuration backup exists", backupPath);
            if (backup.Id != id || backup.SchemaVersion != e.SchemaVersion)
                throw new InvalidDataException("Backup identity or schema does not match " + id);
            Validate(e, backup.Data);
            T replacement = CxConfigJsonFile.Clone(backup.Data);
            UpdateAndSave<T>(id, draft =>
            {
                foreach (CxConfigProperty property in CxConfigDescription.Describe<T>())
                    property.Property.SetValue(draft, property.Property.GetValue(replacement, null), null);
            });
        }

        /// <summary>L4：只允许已持久化且磁盘Revision仍一致的配置进入发布包。</summary>
        public string ExportSavedJson(string id)
        {
            lock (_gate)
            {
                Entry e = Resolve(id);
                if (e.Revision != e.PersistedRevision)
                    throw new InvalidOperationException("Unsaved configuration cannot be published: " + id);
                string json = File.ReadAllText(e.Path);
                CxConfigDocumentHeader hdr = CxConfigJsonFile.ReadHeader(json);
                if (hdr.Id != id || hdr.SchemaVersion != e.SchemaVersion || hdr.Revision != e.Revision)
                    throw new InvalidOperationException("Configuration disk revision changed: " + id);
                return json;
            }
        }

        public IDisposable Subscribe<T>(string id, Action<T, CxConfigChange> observer) where T : class
        {
            if (observer == null) throw new ArgumentNullException("observer");
            lock (_gate)
            {
                Entry e = Resolve<T>(id);
                Action<object, CxConfigChange> listener = (value, change) => observer((T)value, change);
                e.Listeners.Add(listener);
                return new Subscription(this, e, listener);
            }
        }

        public CxConfigProperty[] Describe<T>(string id) where T : class
        {
            lock (_gate) Resolve<T>(id);
            return CxConfigDescription.Describe<T>();
        }

        private Entry Resolve(string id)
        {
            if (!_initialized) throw new InvalidOperationException("Call Initialize() before accessing configurations");
            Entry result;
            if (!_entries.TryGetValue(id, out result)) throw new KeyNotFoundException("Unknown config ID: " + id);
            return result;
        }

        private Entry Resolve<T>(string id)
        {
            Entry e = Resolve(id);
            if (e.Type != typeof(T)) throw new InvalidOperationException("Configuration type mismatch: " + id);
            return e;
        }

        private static void Validate(Entry e, object value)
        {
            string message = e.Validate(value);
            if (!string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("CONFIG_INVALID: " + e.Id + ": " + message);
        }
        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 96)
                throw new ArgumentException("Config ID required and must be <= 96 chars", "id");
            foreach (char ch in id)
                if (!(char.IsLetterOrDigit(ch) && ch <= 127) && ch != '.' && ch != '_' && ch != '-')
                    throw new ArgumentException("Config ID may contain only ASCII letters, digits, dash, underscore and dot", "id");
            if (id == "." || id == "..") throw new ArgumentException("Reserved config ID", "id");
        }
    }
}
