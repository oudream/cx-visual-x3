using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 只有此视图授予宿主插件时才建立访问边界；宿主不可同时向非可信方暴露
    /// 原始 MHSConfigService / MHSConfigSection。所有授权在每次操作时重新验证。
    /// </summary>
    public sealed class MHSConfigAuthorizedSession
    {
        private readonly MHSConfigService _service;
        private readonly FCMConfigPrincipal _principal;
        private readonly IFCMConfigAccessPolicy _policy;
        internal MHSConfigAuthorizedSession(MHSConfigService service,
            FCMConfigPrincipal principal, IFCMConfigAccessPolicy policy)
        {
            _service = service; _principal = principal; _policy = policy;
        }
        public MHSConfigAuthorizedSection<T> Open<T>(FCMConfigIdentity identity) where T : new()
        {
            Require(identity, FCMConfigAccessAction.Read, null);
            // Opening a typed section grants no data itself. Non-sensitive fields remain
            // readable with Read permission; Get()/snapshots still require ReadSensitive.
            MHSConfigSection<T> section = _service.Open<T>(identity);
            // Opening a section is a read-only discovery operation. It must not silently
            // alter Immediate behavior of other callers using the same instance.
            return new MHSConfigAuthorizedSection<T>(section, this);
        }
        internal void Require(FCMConfigIdentity identity, FCMConfigAccessAction action, string path)
        {
            FCMConfigAccessDecision decision = _policy.Evaluate(_principal,
                new FCMConfigAccessRequest(identity, action, path));
            if (decision == null || !decision.Granted)
                throw new UnauthorizedAccessException("CONFIG_ACCESS_DENIED: " +
                    action + " " + identity + " " + (decision == null ? "No decision" : decision.Reason));
        }
        internal void RequireReadable<T>(FCMConfigIdentity identity, string path)
        {
            Require(identity, FCMConfigAccessAction.Read, path);
            if (FCMConfigSensitivity.IsSensitivePath(typeof(T), path))
                Require(identity, FCMConfigAccessAction.ReadSensitive, path);
        }
    }

    /// <summary>不暴露底层实例或可绕过权限的 Raw Edit；只提供按角色许可的配置 API。</summary>
    public sealed class MHSConfigAuthorizedSection<T> where T : new()
    {
        private readonly MHSConfigSection<T> _source;
        private readonly MHSConfigAuthorizedSession _session;
        internal MHSConfigAuthorizedSection(MHSConfigSection<T> source, MHSConfigAuthorizedSession session)
        { _source = source; _session = session; }
        public FCMConfigIdentity Identity { get { return _source.Identity; } }
        public FCMConfigRevision Revision
        { get { Demand(FCMConfigAccessAction.Read, null); return _source.Revision; } }
        public T Get()
        {
            _session.RequireReadable<T>(Identity, null);
            return _source.Get();
        }
        public TValue Value<TValue>(Expression<Func<T, TValue>> selector)
        {
            string path = FCMConfigPathUtil.FromExpression(selector);
            _session.RequireReadable<T>(Identity, path);
            return _source.Value(selector);
        }
        /// <summary>授权高频读取：表达式仅编译一次，但每次读仍重新检查字段权限。</summary>
        public Func<V> PrepareValue<V>(Expression<Func<T, V>> expression)
        {
            if (expression == null) throw new ArgumentNullException("expression");
            string path = FCMConfigPathUtil.FromExpression(expression);
            _session.RequireReadable<T>(Identity, path);
            Func<V> prepared = _source.PrepareValue(expression);
            return () =>
            {
                _session.RequireReadable<T>(Identity, path);
                return prepared();
            };
        }
        public MHSConfigAuthorizedEdit<T> BeginEdit(string reason)
        {
            Demand(FCMConfigAccessAction.Edit, null);
            _session.RequireReadable<T>(Identity, null);
            // The safety boundary becomes mandatory only when a privileged writer starts.
            _source.RequireExplicitApply();
            return new MHSConfigAuthorizedEdit<T>(_source.BeginEdit(reason), _session, Identity);
        }
        /// <summary>授权会话中的多字段编辑，不暴露原始 Section。</summary>
        public async Task<MHSConfigCommitResult> UpdateAsync(Action<T> change,
            string reason, CancellationToken token)
        {
            if (change == null) throw new ArgumentNullException("change");
            using (MHSConfigAuthorizedEdit<T> edit = BeginEdit(reason))
            {
                change(edit.WorkingCopy);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }

        public MHSConfigApplyResult TryApplyPendingAtBoundary(FCMConfigApplyPolicy boundary)
        {
            Demand(FCMConfigAccessAction.Apply, null);
            return _source.TryApplyPendingAtBoundary(boundary, null);
        }
        public MHSConfigSnapshot<T> CaptureForTask(string taskId)
        {
            _session.RequireReadable<T>(Identity, null);
            return _source.CaptureForTask(taskId);
        }
        public IList<MHSConfigCommittedRecord> ReadCommittedAfter(string cursor, int count)
        {
            Demand(FCMConfigAccessAction.Audit, null);
            _session.RequireReadable<T>(Identity, null); // Reasons may include sensitive context.
            return _source.ReadCommittedAfter(cursor, count);
        }
        public MHSConfigStatus GetStatus()
        {
            Demand(FCMConfigAccessAction.Audit, null);
            _session.RequireReadable<T>(Identity, null); // PendingChanges contains raw OldJson/NewJson.
            return _source.GetStatus();
        }
        public MHSConfigPackage ExportPackage()
        {
            Demand(FCMConfigAccessAction.Export, null);
            _session.RequireReadable<T>(Identity, null);
            return _source.ExportPackage();
        }
        public async Task<MHSConfigCommitResult> ImportPackageAsync(MHSConfigPackage package, CancellationToken token)
        {
            Demand(FCMConfigAccessAction.Import, null);
            Demand(FCMConfigAccessAction.Commit, null);
            _source.RequireExplicitApply();
            return await _source.ImportPackageAsync(package, token).ConfigureAwait(false);
        }
        public async Task<MHSConfigCommitResult> RestoreSavedVersionAsync(string version,
            string reason, CancellationToken token)
        {
            Demand(FCMConfigAccessAction.Restore, null);
            Demand(FCMConfigAccessAction.Commit, null);
            _source.RequireExplicitApply();
            return await _source.RestoreSavedVersionAsync(version, reason, token).ConfigureAwait(false);
        }
        public IDisposable Subscribe<TValue>(Expression<Func<T, TValue>> selector,
            FCMConfigChangeStage stage, Action<FCMConfigChange<TValue>> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            string path = FCMConfigPathUtil.FromExpression(selector);
            Demand(FCMConfigAccessAction.Subscribe, path);
            _session.RequireReadable<T>(Identity, path);
            return _source.Subscribe(selector, stage, change =>
            {
                Demand(FCMConfigAccessAction.Subscribe, path);
                _session.RequireReadable<T>(Identity, path);
                callback(change);
            });
        }
        /// <summary>监听有效值：同一入口保留按字段的权限检查。</summary>
        public IDisposable Watch<TValue>(Expression<Func<T, TValue>> accessor,
            Action<FCMConfigChange<TValue>> callback)
        { return Subscribe(accessor, FCMConfigChangeStage.Effective, callback); }

        public IDisposable WatchSaved<TValue>(Expression<Func<T, TValue>> accessor,
            Action<FCMConfigChange<TValue>> callback)
        { return Subscribe(accessor, FCMConfigChangeStage.Committed, callback); }

        private void Demand(FCMConfigAccessAction action, string path) { _session.Require(Identity, action, path); }
    }

    /// <summary>动态权限必须在提交时重检，编辑开始时获得的角色不能永久保留写权限。</summary>
    public sealed class MHSConfigAuthorizedEdit<T> : IDisposable where T : new()
    {
        private MHSConfigEdit<T> _edit;
        private readonly MHSConfigAuthorizedSession _session;
        private readonly FCMConfigIdentity _identity;
        internal MHSConfigAuthorizedEdit(MHSConfigEdit<T> edit,
            MHSConfigAuthorizedSession session, FCMConfigIdentity identity)
        { _edit = edit; _session = session; _identity = identity; }
        public T WorkingCopy
        {
            get
            {
                if (_edit == null) throw new ObjectDisposedException("MHSConfigAuthorizedEdit");
                _session.Require(_identity, FCMConfigAccessAction.Edit, null);
                return _edit.WorkingCopy;
            }
        }
        public Task<MHSConfigPreview> PreviewAsync(CancellationToken token)
        {
            if (_edit == null) throw new ObjectDisposedException("MHSConfigAuthorizedEdit");
            _session.Require(_identity, FCMConfigAccessAction.Edit, null);
            return _edit.PreviewAsync(token);
        }
        public Task<MHSConfigCommitResult> CommitAsync(CancellationToken token)
        {
            if (_edit == null) throw new ObjectDisposedException("MHSConfigAuthorizedEdit");
            _session.Require(_identity, FCMConfigAccessAction.Commit, null);
            return _edit.CommitAsync(token);
        }
        public void Dispose()
        {
            MHSConfigEdit<T> edit = Interlocked.Exchange(ref _edit, null);
            if (edit != null) edit.Dispose();
        }
    }
}
