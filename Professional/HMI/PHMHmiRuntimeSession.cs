using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Professional.HMI
{
    /// <summary>HMI的显示/操作模式不是设备授权；授权始终由独立宿主裁决。</summary>
    public enum PHMHmiMode { DesignPreview, Simulation, AuthorizedOnline }
    public enum PHMHmiOperationState { Rejected, Accepted, Unknown, Confirmed }

    public sealed class PHMHmiOperationFeedback
    {
        public PHMHmiOperationFeedback(PHMHmiOperationState state, string reason)
        { State = state; Reason = reason ?? ""; }
        public PHMHmiOperationState State { get; private set; }
        public string Reason { get; private set; }
    }

    /// <summary>只读MDS快照的显示投影，非第二份运行变量仓。</summary>
    public sealed class PHMHmiPointObservation
    {
        internal PHMHmiPointObservation(IMDSVariableSnapshot snapshot, string unit,
            MDSVariableQuality quality)
        {
            PointId = snapshot.Name; VariableId = snapshot.VariableId;
            HasValue = snapshot.HasValue; Value = snapshot.CurrentObject;
            Quality = quality; Unit = unit ?? "";
            RefreshUtc = snapshot.RefreshUtc; Revision = snapshot.Revision;
            Source = snapshot.Source ?? "";
            IFormattable number = Value as IFormattable;
            Text = !HasValue ? "—" : number != null
                ? number.ToString(null, CultureInfo.InvariantCulture)
                : Convert.ToString(Value, CultureInfo.InvariantCulture);
        }
        public string PointId { get; private set; }
        public string VariableId { get; private set; }
        public bool HasValue { get; private set; }
        public object Value { get; private set; }
        public string Text { get; private set; }
        public string Unit { get; private set; }
        public MDSVariableQuality Quality { get; private set; }
        public bool IsUsable { get { return HasValue && Quality == MDSVariableQuality.Good; } }
        public DateTime RefreshUtc { get; private set; }
        public string Source { get; private set; }
        public long Revision { get; private set; }
    }

    /// <summary>
    /// 实际HMI会话适配既有MEM身份、MDS快照/LatestOnly、MVI投影与保存的动作。
    /// 不保存独立工程定义、不写入MDS、无PLC/设备驱动调用。
    /// </summary>
    public sealed class PHMHmiRuntimeSession : IMDSExternalBindingResolver, IDisposable
    {
        private readonly MDSVariableTable _table;
        private readonly MDSVariableChangeDispatcher _dispatcher;
        private readonly TimeSpan _maxAge;
        private readonly object _gate = new object();
        private bool _disposed;

        public PHMHmiRuntimeSession(Guid projectId, int projectRevision,
            MDSVariableTable table, PHMHmiMode mode, TimeSpan maxAge)
        {
            if (projectId == Guid.Empty || projectRevision < 0)
                throw new ArgumentException("hmi.invalid_engineering_identity");
            if (table == null) throw new ArgumentNullException("table");
            if (!Enum.IsDefined(typeof(PHMHmiMode), mode))
                throw new ArgumentOutOfRangeException("mode");
            if (maxAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException("maxAge");
            ProjectId = projectId; ProjectRevision = projectRevision;
            _table = table; _maxAge = maxAge; Mode = mode;
            _dispatcher = table.CreateDispatcher(1024);
        }
        public Guid ProjectId { get; private set; }
        public int ProjectRevision { get; private set; }
        public string ScopeId { get { return _table.ScopeId; } }
        public PHMHmiMode Mode { get; private set; }
        private void RequireOpen()
        {
            if (_disposed) throw new ObjectDisposedException("PHMHmiRuntimeSession");
        }
        public IReadOnlyList<MDSVariableDefinition> Points()
        {
            lock (_gate) { RequireOpen(); return Array.AsReadOnly(_table.Definitions()); }
        }
        public PHMHmiPointObservation Read(string pointName)
        {
            lock (_gate)
            {
                RequireOpen();
                IMDSVariableSnapshot s = _table.Inspect(pointName);
                MDSVariableDefinition d = _table.Definitions().First(x => x.Name == s.Name);
                MDSVariableQuality q = s.HasValue ? s.Quality : MDSVariableQuality.Uncertain;
                if (s.HasValue && q == MDSVariableQuality.Good &&
                    (_maxAge == TimeSpan.Zero || s.RefreshUtc == default(DateTime) ||
                     DateTime.UtcNow - s.RefreshUtc > _maxAge))
                    q = MDSVariableQuality.Stale;
                return new PHMHmiPointObservation(s, d.Unit, q);
            }
        }
        /// <summary>Visual已有Variable Binding只消费Good值；整数PLC寄存器支持double投影。</summary>
        public object Resolve(MDSBindingSource kind, string name, Type targetType)
        {
            if (kind != MDSBindingSource.Variable || string.IsNullOrWhiteSpace(name))
                return null;
            if (targetType == null) throw new ArgumentNullException("targetType");
            string point = name;
            if (!_table.Contains(point) && point.EndsWith(".value", StringComparison.Ordinal))
                point = point.Substring(0, point.Length - 6);
            PHMHmiPointObservation s;
            try { s = Read(point); }
            catch (KeyNotFoundException) { return null; }
            if (!s.IsUsable) return null;
            if (targetType.IsInstanceOfType(s.Value)) return s.Value;
            if (targetType == typeof(string))
                return Convert.ToString(s.Value, CultureInfo.InvariantCulture);
            if (targetType == typeof(double) && s.Value is IConvertible)
            {
                TypeCode t = Type.GetTypeCode(s.Value.GetType());
                if (t != TypeCode.Boolean && t != TypeCode.String &&
                    t != TypeCode.DateTime && t != TypeCode.Object)
                {
                    double number = Convert.ToDouble(s.Value, CultureInfo.InvariantCulture);
                    if (!double.IsNaN(number) && !double.IsInfinity(number))
                        return number;
                }
            }
            return null; // 不能把PLC数字1隐式解释为安全布尔信号
        }
        /// <summary>异步回调不在WPF Dispatcher；窗口必须Dispose订阅。</summary>
        public IDisposable Subscribe(Action<IReadOnlyList<string>> changed,
            IEnumerable<string> pointNames)
        {
            if (changed == null) throw new ArgumentNullException("changed");
            if (pointNames == null) throw new ArgumentNullException("pointNames");
            lock (_gate)
            {
                RequireOpen();
                string[] names = pointNames.Distinct(StringComparer.Ordinal).ToArray();
                MDSVariableDefinition[] defs = _table.Definitions();
                if (names.Any(n => !defs.Any(d => d.Name == n)))
                    throw new KeyNotFoundException("hmi.point_not_found");
                string[] ids = defs.Where(d => names.Contains(d.Name, StringComparer.Ordinal))
                    .Select(d => d.VariableId).ToArray();
                return _dispatcher.Subscribe(changes =>
                {
                    lock (_gate) { if (_disposed) return; }
                    string[] selected = changes.Select(x => x.Name)
                        .Distinct(StringComparer.Ordinal).ToArray();
                    if (selected.Length > 0) changed(Array.AsReadOnly(selected));
                }, ids);
            }
        }
        /// <summary>只转交MEM已保存Action；非在线/无可信Host默认拒绝，绝不直连PLC。</summary>
        public async Task<PHMHmiOperationFeedback> RequestActionAsync(
            MEMEngineeringSession engineering, MRIObjectAddress address,
            string eventId, string value,
            Func<MVIVisualActionInvocation,CancellationToken,Task<PHMHmiOperationFeedback>>
                trustedHost, CancellationToken cancellation)
        {
            lock (_gate)
            {
                RequireOpen();
                if (Mode != PHMHmiMode.AuthorizedOnline || trustedHost == null)
                    return new PHMHmiOperationFeedback(PHMHmiOperationState.Rejected,
                        "hmi.online_host_authorization_required");
            }
            if (engineering == null || address == null ||
                engineering.Project.Id != ProjectId || engineering.Revision != ProjectRevision)
                return new PHMHmiOperationFeedback(PHMHmiOperationState.Rejected,
                    "hmi.project_revision_mismatch");
            IReadOnlyList<MVIVisualActionInvocation> actions =
                MVIVisualEventRouter.Resolve(engineering, address, eventId, value);
            if (actions.Count == 0)
                return new PHMHmiOperationFeedback(PHMHmiOperationState.Rejected,
                    "hmi.saved_action_missing");
            PHMHmiOperationFeedback result = null;
            foreach (MVIVisualActionInvocation action in actions)
            {
                try
                {
                    result = await trustedHost(action, cancellation).ConfigureAwait(false);
                    if (result == null)
                        return new PHMHmiOperationFeedback(PHMHmiOperationState.Unknown,
                            "hmi.host_result_missing");
                    if (result.State != PHMHmiOperationState.Confirmed) return result;
                }
                catch (Exception ex)
                {
                    // 跨越宿主边界后的失败可能已发送，不能伪报“没动作”并自动重试。
                    return new PHMHmiOperationFeedback(PHMHmiOperationState.Unknown,
                        "hmi.outcome_unknown:" + ex.GetType().Name);
                }
            }
            return result;
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; }
            _dispatcher.Dispose();
        }
    }
}
