using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    public static class ListStringHelper
    {
        /// <summary>
        /// 获取指定 key 的值，如果不存在返回 null。
        /// </summary>
        public static string GetValue(List<KeyValuePair<string, string>> list, string key)
        {
            if (list == null || string.IsNullOrEmpty(key)) return string.Empty;
            var kv = list.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
            return kv.Equals(default(KeyValuePair<string, string>)) ? string.Empty : kv.Value;
        }

        /// <summary>
        /// 设置 key 对应的值（存在则更新，不存在则添加）。
        /// </summary>
        public static void SetValue(List<KeyValuePair<string, string>> list, string key, string value)
        {
            if (list == null || string.IsNullOrEmpty(key)) return;
            var idx = list.FindIndex(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                list[idx] = new KeyValuePair<string, string>(key, value);
            }
            else
            {
                list.Add(new KeyValuePair<string, string>(key, value));
            }
        }

        /// <summary>
        /// 移除指定 key 的所有项，返回是否有删除。
        /// </summary>
        public static bool RemoveKey(List<KeyValuePair<string, string>> list, string key)
        {
            if (list == null || string.IsNullOrEmpty(key)) return false;
            return list.RemoveAll(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        /// <summary>
        /// 判断是否包含指定 key。
        /// </summary>
        public static bool ContainsKey(List<KeyValuePair<string, string>> list, string key)
        {
            if (list == null || string.IsNullOrEmpty(key)) return false;
            return list.Any(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 将 List 转换为 Dictionary（重复 key 保留最后一个）。
        /// </summary>
        public static Dictionary<string, string> ToDictionary(List<KeyValuePair<string, string>> list)
        {
            if (list == null) return new Dictionary<string, string>();
            return list.GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 去重（相同 key 只保留最后一个）。
        /// </summary>
        public static List<KeyValuePair<string, string>> DistinctByKey(List<KeyValuePair<string, string>> list)
        {
            if (list == null) return new List<KeyValuePair<string, string>>();
            return list.GroupBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                       .Select(g => g.Last())
                       .ToList();
        }

        /// <summary>
        /// 获取所有 key。
        /// </summary>
        public static List<string> Keys(List<KeyValuePair<string, string>> list)
        {
            if (list == null) return new List<string>();
            return list.Select(k => k.Key).ToList();
        }

        /// <summary>
        /// 获取所有 value。
        /// </summary>
        public static List<string> Values(List<KeyValuePair<string, string>> list)
        {
            if (list == null) return new List<string>();
            return list.Select(k => k.Value).ToList();
        }
    }
}
