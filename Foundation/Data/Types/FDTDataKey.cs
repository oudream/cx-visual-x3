using System;

namespace CxVisual.Foundation.Data.Types
{
    /// <summary>可选的强类型数据槽键；不创建全局注册表，名称由工程/端口提供。</summary>
    public sealed class FDTDataKey<T>
    {
        public FDTDataKey(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("数据键名称不能为空", "name");
            Name = name;
        }
        public string Name { get; private set; }
        public Type ValueType { get { return typeof(T); } }
        public override string ToString() { return Name; }
    }
}
