using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Foundation.Data.Commands
{
    /// <summary>参数绑定由真实 MDS/变量/端口 Owner 实现；通用层不复制运行数据仓库。</summary>
    public interface IFDCCommandValueBinding
    {
        Task<object> ReadAsync(string address, CancellationToken cancellation);
        Task WriteAsync(string address, object value, CancellationToken cancellation);
    }

    /// <summary>
    /// 请求受理前复制参数，响应对外发布前再次复制。
    /// 已知值、byte[]、一维数组、Dictionary&lt;string,object&gt;、List&lt;object&gt;支持快照；
    /// 复杂 DTO / Native Handle 必须提供既有 FDT Adapter，禁止共享可变引用。
    /// </summary>
    public static class FDCCommandValueSnapshot
    {
        public static IReadOnlyDictionary<string, object> Capture(
            IReadOnlyDictionary<string, object> values, FDTDataAdapterRegistry adapters = null)
        {
            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
            if (values != null)
                foreach (KeyValuePair<string, object> pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key) || copy.ContainsKey(pair.Key))
                        throw new ArgumentException("参数键无效或重复");
                    copy.Add(pair.Key, Copy(pair.Value, adapters, 0));
                }
            return new ReadOnlyDictionary<string, object>(copy);
        }

        private static object Copy(object value, FDTDataAdapterRegistry adapters, int depth)
        {
            if (value == null) return null;
            if (depth > 16) throw new ArgumentException("命令参数嵌套超过16层");
            if (value is byte[] || FDTDataValueSafety.IsSimpleImmutable(value.GetType()))
                return FDTDataValueSafety.Capture(value, adapters);
            Dictionary<string, object> map = value as Dictionary<string, object>;
            if (map != null)
            {
                if (map.Count > 10000) throw new ArgumentException("命令参数集合超过上限");
                Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> item in map)
                    result.Add(item.Key, Copy(item.Value, adapters, depth + 1));
                return result;
            }
            List<object> list = value as List<object>;
            if (list != null)
            {
                if (list.Count > 10000) throw new ArgumentException("命令参数集合超过上限");
                List<object> result = new List<object>(list.Count);
                foreach (object item in list) result.Add(Copy(item, adapters, depth + 1));
                return result;
            }
            Array array = value as Array;
            if (array != null && array.Rank == 1 && array.GetLowerBound(0) == 0)
            {
                if (array.Length > 10000) throw new ArgumentException("命令数组超过上限");
                Array result = Array.CreateInstance(value.GetType().GetElementType(), array.Length);
                for (int i = 0; i < array.Length; i++)
                    result.SetValue(Copy(array.GetValue(i), adapters, depth + 1), i);
                return result;
            }
            return FDTDataValueSafety.Capture(value, adapters);
        }
    }
}
