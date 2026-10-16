using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CxVisual.Foundation.Data.Execution;
using CxVisual.Mechanism.Data.Execution;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>节点数据访问门面。Output只暂存，必须由运行宿主确认成功后提交。</summary>
    public interface IMDSDataContext
    {
        T Get<T>(string name);
        bool TryGet<T>(string name, out T value);
        void Set<T>(string name, T value);
        void Return<T>(string name, T value);
        IDictionary<string, object> InspectPendingOutputs();
    }

    /// <summary>
    /// 复用现有执行Scope而非再创建平行Scope。
    /// 本阶段Commit返回整体不可变输出视图，不隐式发布到父Scope。
    /// 后续由MRK运行引擎适配原子端口交接。
    /// </summary>
    public sealed class MDSDataContext : IMDSDataContext
    {
        private readonly object _gate = new object();
        private readonly IFDEExecutionDataScope _scope;
        private readonly Dictionary<string, object> _pending =
            new Dictionary<string, object>(StringComparer.Ordinal);
        private bool _finished;

        public MDSDataContext(IFDEExecutionDataScope scope)
        {
            if (scope == null) throw new ArgumentNullException("scope");
            _scope = scope;
        }

        private void EnsureActive()
        {
            if (_scope.IsClosed) throw new ObjectDisposedException("MDSDataContext");
            if (_finished) throw new InvalidOperationException("当前执行上下文已结算");
        }

        private static void CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据名称不能为空", "name");
        }

        private static object SafeValue(object value)
        {
            if (value == null) return null;
            Type t = value.GetType();
            if (t == typeof(byte[])) return ((byte[])value).Clone();
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) ||
                t == typeof(decimal) || t == typeof(Guid) ||
                t == typeof(DateTime) || t == typeof(DateTimeOffset) ||
                t == typeof(TimeSpan)) return value;
            throw new NotSupportedException("尚未注册安全的数据值适配：" + t.FullName);
        }

        public T Get<T>(string name)
        {
            T result;
            if (!TryGet(name, out result)) throw new KeyNotFoundException("找不到数据：" + name);
            return result;
        }

        public bool TryGet<T>(string name, out T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                T found;
                if (!_scope.TryRead(name, out found))
                {
                    value = default(T);
                    return false;
                }
                object safe = SafeValue(found);
                value = (T)safe;
                return true;
            }
        }

        public void Set<T>(string name, T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                _scope.Put(name, (T)SafeValue(value));
            }
        }

        public void Return<T>(string name, T value)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureActive();
                if (_pending.ContainsKey(name))
                    throw new InvalidOperationException("重复返回输出：" + name);
                _pending.Add(name, SafeValue(value));
            }
        }

        public IDictionary<string, object> InspectPendingOutputs()
        {
            lock (_gate)
            {
                EnsureActive();
                return Snapshot();
            }
        }

        private IDictionary<string, object> Snapshot()
        {
            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in _pending)
                copy.Add(item.Key, SafeValue(item.Value));
            return new ReadOnlyDictionary<string, object>(copy);
        }

        /// <summary>仅由运行宿主在成功与端口验证后调用；结果整体快照发布。</summary>
        public IDictionary<string, object> CommitOutputs()
        {
            lock (_gate)
            {
                EnsureActive();
                IDictionary<string, object> result = Snapshot();
                _finished = true;
                _pending.Clear();
                return result;
            }
        }

        public void AbortOutputs()
        {
            lock (_gate)
            {
                if (_finished) return;
                _pending.Clear();
                _finished = true;
            }
        }
    }
}
