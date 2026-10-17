using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Data.Types
{
    /// <summary>专业自定义值的安全复制契约，注册在单个运行Flow/Heap而非全局静态表。</summary>
    public interface IFDTDataAdapter
    {
        Type ValueType { get; }
        bool IsImmutable { get; }
        object Capture(object value);
    }

    /// <summary>显式的专业复制函数；未知或浅复制的可变类型必须失败。</summary>
    public sealed class FDTDataAdapter<T> : IFDTDataAdapter
    {
        private readonly Func<T, T> _copy;
        public FDTDataAdapter(Func<T, T> copier, bool immutable = false)
        {
            if (!immutable && copier == null)
                throw new ArgumentNullException("copier");
            _copy = copier;
            IsImmutable = immutable;
        }
        public Type ValueType { get { return typeof(T); } }
        public bool IsImmutable { get; private set; }
        public object Capture(object value)
        {
            if (value == null) return null;
            if (!(value is T)) throw new InvalidCastException("注册类型和运行数据不匹配");
            if (IsImmutable) return value;
            T copied = _copy((T)value);
            if ((object)copied == null) throw new InvalidOperationException("复制器返回了null");
            if (typeof(T).IsClass && object.ReferenceEquals(copied, value))
                throw new InvalidOperationException("复制器返回原引用，拒绝伪深复制");
            // 注册方承诺嵌套资源不会共享可写别名。专业Adapter验收必须验证这一点。
            return copied;
        }
    }

    /// <summary>每个Heap/Flow独立持有的类型能力表。运行开始后可以冻结，防止并行期间变化。</summary>
    public sealed class FDTDataAdapterRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, IFDTDataAdapter> _items =
            new Dictionary<Type, IFDTDataAdapter>();
        private bool _frozen;

        public void Register<T>(Func<T,T> copier, bool immutable = false)
        {
            Register(new FDTDataAdapter<T>(copier, immutable));
        }
        public void Register(IFDTDataAdapter adapter)
        {
            if (adapter == null || adapter.ValueType == null)
                throw new ArgumentNullException("adapter");
            lock (_gate)
            {
                if (_frozen) throw new InvalidOperationException("类型注册表已冻结");
                if (_items.ContainsKey(adapter.ValueType))
                    throw new InvalidOperationException("重复注册数据类型：" + adapter.ValueType.FullName);
                _items.Add(adapter.ValueType, adapter);
            }
        }
        public void Freeze() { lock (_gate) _frozen = true; }

        public bool TryCapture(object value, out object captured)
        {
            captured = null;
            if (value == null) return false;
            IFDTDataAdapter adapter;
            lock (_gate)
            {
                if (!_items.TryGetValue(value.GetType(), out adapter)) return false;
            }
            captured = adapter.Capture(value);
            if (captured == null || !value.GetType().IsInstanceOfType(captured))
                throw new InvalidOperationException("数据适配器返回类型不匹配");
            return true;
        }

        public bool Supports(Type valueType)
        {
            if (valueType == null) return false;
            lock (_gate) return _items.ContainsKey(valueType);
        }
    }
}
