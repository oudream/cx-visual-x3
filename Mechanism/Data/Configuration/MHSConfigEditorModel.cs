using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>UI 无关的属性状态：Schema、当前草稿、保存值、有效值与验证错误。</summary>
    public sealed class MHSConfigEditorField
    {
        internal MHSConfigEditorField(FCMConfigFieldInfo schema, string draft, string saved,
            string effective, IList<FCMConfigIssue> issues)
        {
            Schema = schema;
            DraftJson = draft; SavedJson = saved; EffectiveJson = effective;
            HasDraftValue = draft != null; HasSavedValue = saved != null;
            IsModified = !string.Equals(draft, saved, StringComparison.Ordinal);
            Issues = new List<FCMConfigIssue>(issues).AsReadOnly();
        }
        public FCMConfigFieldInfo Schema { get; private set; }
        public string DraftJson { get; private set; }
        public string SavedJson { get; private set; }
        public string EffectiveJson { get; private set; }
        public bool HasDraftValue { get; private set; }
        public bool HasSavedValue { get; private set; }
        public bool IsModified { get; private set; }
        public IList<FCMConfigIssue> Issues { get; private set; }
    }

    /// <summary>
    /// 与 UI 无关的编辑会话：深拷贝、事务预览、Undo/Redo、Schema/依赖诊断。
    /// 一次提交仍委托 MHSConfigEdit<T>，不构建第二套存储、校验器或事务真源。
    /// </summary>
    public sealed class MHSConfigEditorModel<T> : IDisposable where T : new()
    {
        private MHSConfigEdit<T> _edit;
        private readonly MHSConfigSection<T> _section;
        private readonly List<string> _states = new List<string>();
        private readonly FCMConfigDependencyGraph _dependencies;
        private int _position;
        private readonly int _maxStates;
        public MHSConfigEditorModel(MHSConfigSection<T> section, string reason, int maxUndoStates = 64)
        {
            if (section == null) throw new ArgumentNullException("section");
            if (maxUndoStates < 2 || maxUndoStates > 1000) throw new ArgumentOutOfRangeException("maxUndoStates");
            _section = section;
            _dependencies = FCMConfigCatalog.BuildDependencyGraph<T>();
            _maxStates = maxUndoStates;
            _edit = section.BeginEdit(reason);
            _states.Add(FCMJsonValueCodec.Serialize(_edit.WorkingCopy));
        }
        public FCMConfigIdentity Identity { get { return _section.Identity; } }
        public IList<FCMConfigFieldInfo> SchemaFields { get { return FCMConfigCatalog.Describe<T>(); } }
        public T Draft { get { EnsureOpen(); return Clone(_edit.WorkingCopy); } }
        public bool CanUndo { get { EnsureOpen(); return _position > 0; } }
        public bool CanRedo { get { EnsureOpen(); return _position + 1 < _states.Count; } }
        public IList<string> AffectedBy(string path)
        { EnsureOpen(); return _dependencies.AffectedBy(path); }

        public void Modify(Action<T> change)
        {
            EnsureOpen();
            if (change == null) throw new ArgumentNullException("change");
            T detached = Clone(_edit.WorkingCopy);
            change(detached);
            string json = FCMJsonValueCodec.Serialize(detached);
            if (json == _states[_position]) return;
            // Redo history after the current position belongs to the previous branch.
            if (_position + 1 < _states.Count)
                _states.RemoveRange(_position + 1, _states.Count - _position - 1);
            _states.Add(json); _position++;
            if (_states.Count > _maxStates) { _states.RemoveAt(0); _position--; }
            _edit.ReplaceWorkingCopy(detached);
        }
        public bool Undo()
        {
            EnsureOpen();
            if (!CanUndo) return false;
            _position--; LoadCurrent(); return true;
        }
        public bool Redo()
        {
            EnsureOpen();
            if (!CanRedo) return false;
            _position++; LoadCurrent(); return true;
        }
        public MHSConfigPreview Preview()
        { EnsureOpen(); return _edit.Preview(); }
        public Task<MHSConfigPreview> PreviewAsync(CancellationToken token)
        { EnsureOpen(); return _edit.PreviewAsync(token); }

        public IList<MHSConfigEditorField> DescribeFields()
        {
            EnsureOpen();
            MHSConfigPreview preview = _edit.Preview();
            T original = _section.GetLatestSaved();
            T effective = _section.Get();
            List<MHSConfigEditorField> fields = new List<MHSConfigEditorField>();
            foreach (FCMConfigFieldInfo member in SchemaFields)
            {
                string draft, saved, active;
                MVBConfigLayerPath.TryReadJson(_edit.WorkingCopy, member.Path, out draft);
                MVBConfigLayerPath.TryReadJson(original, member.Path, out saved);
                MVBConfigLayerPath.TryReadJson(effective, member.Path, out active);
                IList<FCMConfigIssue> matched = preview.Validation.Issues
                    .Where(x => x.Path == member.Path || x.Path.StartsWith(member.Path + ".", StringComparison.Ordinal) ||
                        x.Path.StartsWith(member.Path + "[", StringComparison.Ordinal)).ToList();
                fields.Add(new MHSConfigEditorField(member, draft, saved, active, matched));
            }
            return fields.AsReadOnly();
        }
        public async Task<MHSConfigCommitResult> CommitAsync(CancellationToken token)
        {
            EnsureOpen();
            MHSConfigCommitResult result = await _edit.CommitAsync(token).ConfigureAwait(false);
            if (result.Success) Dispose();
            return result;
        }
        private void LoadCurrent() { _edit.ReplaceWorkingCopy(FCMJsonValueCodec.Deserialize<T>(_states[_position])); }
        private static T Clone(T value)
        { return FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(value)); }
        private void EnsureOpen()
        { if (_edit == null) throw new ObjectDisposedException("MHSConfigEditorModel"); }
        public void Dispose()
        {
            MHSConfigEdit<T> edit = _edit;
            _edit = null;
            if (edit != null) edit.Dispose();
            _states.Clear();
        }
    }
}
