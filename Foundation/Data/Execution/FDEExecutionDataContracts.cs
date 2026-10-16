using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Data.Execution
{
    /// <summary>
    /// 运行数据作用域的最小公共契约：父级只读可见，本地一次赋值，子级写入不污染父级。
    /// 作用域之间仅共享引用，不承诺对象内部是不可变的；大对象应通过受控数据租约交接。
    /// </summary>
    public interface IFDEExecutionDataScope : IDisposable
    {
        string Id { get; }
        IFDEExecutionDataScope Parent { get; }
        bool IsClosed { get; }
        IFDEExecutionDataScope CreateChild(string name);
        void Put<T>(string name, T value);
        void PutOwned<T>(string name, T value, Action<T> release);
        bool TryRead<T>(string name, out T value);
        IDictionary<string, object> SnapshotLocal();
    }
}
