using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>工程参数选择器的数据来源类别，非新的变量/工程资源仓库。</summary>
    public enum MDSBindingSource
    {
        Constant,
        CurrentSlot,
        NodeOutput,
        FlowHeap,
        Variable,
        EngineeringResource,
        Expression
    }

    /// <summary>可持久化/配置的数据绑定意图；节点输出仍由MRK的端口编译器绑定成Input。</summary>
    public sealed class MDSDataBinding
    {
        public MDSDataBinding(MDSBindingSource source, Type valueType,
            string sourceName = null, object constant = null)
        {
            if (valueType == null || valueType == typeof(void))
                throw new ArgumentException("绑定目标类型无效", "valueType");
            if (source != MDSBindingSource.Constant && string.IsNullOrWhiteSpace(sourceName))
                throw new ArgumentException("绑定来源名称不能为空", "sourceName");
            if (source == MDSBindingSource.Constant)
            {
                if (constant == null && valueType.IsValueType &&
                    Nullable.GetUnderlyingType(valueType) == null)
                    throw new ArgumentException("非空值类型不能绑定null常量");
                if (constant != null && !valueType.IsInstanceOfType(constant))
                    throw new InvalidCastException("常量类型不符合绑定目标");
            }
            Source = source;
            ValueType = valueType;
            SourceName = sourceName;
            Constant = source == MDSBindingSource.Constant
                ? FDTDataValueSafety.Capture(constant) : null;
        }
        public MDSBindingSource Source { get; private set; }
        public Type ValueType { get; private set; }
        public string SourceName { get; private set; }
        /// <summary>常量也不得向调用方暴露可写byte[]原始引用。</summary>
        private readonly object Constant;
        internal object CaptureConstant(FDTDataAdapterRegistry adapters)
        { return FDTDataValueSafety.Capture(Constant, adapters); }
    }

    /// <summary>变量/工程资源/表达式只委托给专业或平台既有Owner，不建第二套数据管理器。</summary>
    public interface IMDSExternalBindingResolver
    {
        object Resolve(MDSBindingSource source, string sourceName, Type targetType);
    }

    public static class MDSDataBindingResolver
    {
        public static T Resolve<T>(IMDSDataContext data, MDSDataBinding binding,
            IMDSExternalBindingResolver external = null)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (binding == null) throw new ArgumentNullException("binding");
            if (!binding.ValueType.IsAssignableFrom(typeof(T)) &&
                !typeof(T).IsAssignableFrom(binding.ValueType))
                throw new InvalidCastException("请求读取的强类型与绑定声明不兼容");
            object result;
            switch (binding.Source)
            {
                case MDSBindingSource.Constant:
                    result = binding.CaptureConstant(data.Adapters);
                    break;
                case MDSBindingSource.CurrentSlot:
                case MDSBindingSource.NodeOutput:
                    // 节点输出必须先经过MRK StepOutput→本节点Input的类型/权限验证。
                    result = data.Get<object>(binding.SourceName);
                    break;
                case MDSBindingSource.FlowHeap:
                    result = data.HeapGet<object>(binding.SourceName);
                    break;
                case MDSBindingSource.Variable:
                case MDSBindingSource.EngineeringResource:
                case MDSBindingSource.Expression:
                    if (external == null)
                        throw new InvalidOperationException("外部来源必须提供已有平台Owner解析器");
                    result = external.Resolve(binding.Source, binding.SourceName, binding.ValueType);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("binding.Source");
            }
            if (result == null && typeof(T).IsValueType &&
                Nullable.GetUnderlyingType(typeof(T)) == null)
                throw new InvalidCastException("null不可绑定到非空值类型");
            if (result != null && !(result is T))
                throw new InvalidCastException("实际来源数据类型与端口类型不匹配");
            return (T)FDTDataValueSafety.Capture(result, data.Adapters);
        }

        /// <summary>编辑器可直接用于兼容候选过滤，绝不加载未加载大文件。</summary>
        public static IList<MDSDataSnapshot> CompatibleCandidates(IMDSDataContext data,
            Type targetType, bool includeUnloaded = false)
        {
            if (data == null) throw new ArgumentNullException("data");
            if (targetType == null) throw new ArgumentNullException("targetType");
            List<MDSDataSnapshot> found = new List<MDSDataSnapshot>();
            // 只扫描当前可见域；分批分页，避免对固定500单页限制形成漏项。
            int offset = 0;
            while (true)
            {
                IList<MDSDataSnapshot> page = data.Search(new MDSDataQuery {
                    Skip = offset, Take = 500
                });
                if (page.Count == 0) break;
                foreach (MDSDataSnapshot entry in page)
                {
                    if (entry.Location == MDSDataLocation.PendingOutput) continue;
                    if (!targetType.IsAssignableFrom(entry.DataType)) continue;
                    if (!includeUnloaded && entry.LoadState == MDSDataLoadState.Unloaded) continue;
                    if (entry.LoadState == MDSDataLoadState.Loading ||
                        entry.LoadState == MDSDataLoadState.Failed) continue;
                    found.Add(entry);
                }
                offset += page.Count;
                if (page.Count < 500) break;
                if (offset >= 10000)
                    throw new InvalidOperationException("候选项超出检索安全上限，请缩小范围");
            }
            return found.AsReadOnly();
        }
    }
}
