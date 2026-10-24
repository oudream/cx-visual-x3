using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>
    /// 命令入参与出参到正式MDS变量、节点数据、FlowHeap的轻量地址适配。
    /// 只管理地址→访问委托；数据、生命周期、读写权限均由原MDS Owner控制。
    /// </summary>
    public sealed class MDSCommandValueBinding : IFDCCommandValueBinding, IDisposable
    {
        private sealed class Entry
        {
            internal Func<object> Read;
            internal Action<object> Write;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private bool _disposed;

        private void Add(string address, Func<object> read, Action<object> write)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("地址不能为空", "address");
            if (read == null) throw new ArgumentNullException("read");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSCommandValueBinding");
                if (_entries.ContainsKey(address)) throw new ArgumentException("重复绑定地址：" + address);
                _entries.Add(address, new Entry { Read = read, Write = write });
            }
        }

        public void BindVariable<T>(string address, MDSVariableTable table, string variableName)
        {
            if (table == null) throw new ArgumentNullException("table");
            MDSVariableDefinition definition = table.Definitions().FirstOrDefault(
                v => v.Name == variableName);
            if (definition == null) throw new KeyNotFoundException("变量未定义：" + variableName);
            if (definition.ValueType != typeof(T)) throw new InvalidCastException(
                "变量类型与绑定类型不一致：" + variableName);
            Add(address, () => table.Get<T>(variableName), definition.Writable
                ? (Action<object>)(value => table.Update<T>(variableName, Validate<T>(value)))
                : null);
        }

        public void BindNode<T>(string address, IMDSDataContext context, string slot,
            bool writable = false)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("节点槽不能为空", "slot");
            Add(address, () => context.Get<T>(slot),
                writable ? (Action<object>)(value => context.Set<T>(slot, Validate<T>(value))) : null);
        }

        public void BindHeap<T>(string address, IMDSDataContext context, string heapName,
            bool writable = true)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (string.IsNullOrWhiteSpace(heapName)) throw new ArgumentException("堆键不能为空", "heapName");
            Add(address, () => context.HeapGet<T>(heapName),
                writable ? (Action<object>)(value => context.HeapSet<T>(heapName, Validate<T>(value))) : null);
        }

        private static T Validate<T>(object value)
        {
            if (value == null)
            {
                if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
                    throw new InvalidCastException("非空MDS绑定不允许null");
                return default(T);
            }
            if (!(value is T)) throw new InvalidCastException(
                "命令输出与MDS绑定类型不一致：" + typeof(T).FullName);
            return (T)value;
        }

        private Entry Resolve(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("绑定地址不能为空");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSCommandValueBinding");
                Entry entry;
                if (!_entries.TryGetValue(address, out entry))
                    throw new KeyNotFoundException("MDS命令地址未绑定：" + address);
                return entry;
            }
        }

        public Task<object> ReadAsync(string address, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Entry entry = Resolve(address);
            object result = entry.Read();
            cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }

        public Task WriteAsync(string address, object value, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Entry entry = Resolve(address);
            if (entry.Write == null) throw new UnauthorizedAccessException("MDS绑定是只读：" + address);
            entry.Write(value);
            cancellation.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public bool Unbind(string address)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSCommandValueBinding");
                return _entries.Remove(address);
            }
        }

        public void Dispose()
        {
            lock (_gate) { _disposed = true; _entries.Clear(); }
            // 所有变量/数据上下文均由调用方与MDS管理，本适配器不擅自Dispose Owner。
        }
    }
}
