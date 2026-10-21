using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    /// <summary>
    /// 字典安全操作工具类（静态类）
    /// - 不会抛异常（所有方法都安全处理 null / 空）
    /// - 传入的 Dictionary<string,string> 由调用方维护（可用 StringComparer.OrdinalIgnoreCase 控制大小写）
    /// - 支持常用的 Key/Value 增删改查、合并、重命名、解析、导出
    /// - 额外提供 TryGetInt / TryGetDouble / TryGetBool 类型转换读取
    /// </summary>
    public static class DicionaryStringHelper
    {
        // 是否自动 Trim Key/Value
        private static readonly bool _trim = true;

        /// <summary>
        /// 判断是否包含某键
        /// </summary>
        public static bool ContainsKey(Dictionary<string, string> dict, string key)
        {
            if (dict == null || key == null) return false;
            return dict.ContainsKey(NormalizeKey(key));
        }

        /// <summary>
        /// 尝试获取值（获取成功返回 true，value 输出对应值）
        /// </summary>
        public static bool TryGet(Dictionary<string, string> dict, string key, out string value)
        {
            value = string.Empty;
            if (dict == null || key == null) return false;
            return dict.TryGetValue(NormalizeKey(key), out value);
        }

        /// <summary>
        /// 获取值或默认值（不存在时返回 defaultValue）
        /// </summary>
        public static string GetOrDefault(Dictionary<string, string> dict, string key, string defaultValue = "")
        {
            if (dict == null || key == null) return defaultValue;
            return dict.TryGetValue(NormalizeKey(key), out var v) ? v : defaultValue;
        }

        /// <summary>
        /// 设置键值（存在则覆盖）
        /// </summary>
        public static bool Set(Dictionary<string, string> dict, string key, string value)
        {
            if (dict == null || key == null) return false;
            dict[NormalizeKey(key)] = NormalizeValue(value);
            return true;
        }

        /// <summary>
        /// 仅当不存在时添加
        /// </summary>
        public static bool AddIfAbsent(Dictionary<string, string> dict, string key, string value)
        {
            if (dict == null || key == null) return false;
            var k = NormalizeKey(key);
            if (dict.ContainsKey(k)) return false;
            dict[k] = NormalizeValue(value);
            return true;
        }

        /// <summary>
        /// 仅当存在时更新
        /// </summary>
        public static bool UpdateIfPresent(Dictionary<string, string> dict, string key, string value)
        {
            if (dict == null || key == null) return false;
            var k = NormalizeKey(key);
            if (!dict.ContainsKey(k)) return false;
            dict[k] = NormalizeValue(value);
            return true;
        }

        /// <summary>
        /// 删除键
        /// </summary>
        public static bool Remove(Dictionary<string, string> dict, string key)
        {
            if (dict == null || key == null) return false;
            return dict.Remove(NormalizeKey(key));
        }

        /// <summary>
        /// 重命名键（可选择是否覆盖新键）
        /// </summary>
        public static bool RenameKey(Dictionary<string, string> dict, string oldKey, string newKey, bool overwrite = false)
        {
            if (dict == null || oldKey == null || newKey == null) return false;

            var ok = NormalizeKey(oldKey);
            var nk = NormalizeKey(newKey);

            if (!dict.TryGetValue(ok, out var value))
                return false;

            if (!overwrite && dict.ContainsKey(nk))
                return false;

            dict.Remove(ok);
            dict[nk] = value;
            return true;
        }

        /// <summary>
        /// 合并另一个字典（可选择是否覆盖）
        /// </summary>
        public static int Merge(Dictionary<string, string> dict, Dictionary<string, string> other, bool overwrite = true)
        {
            if (dict == null || other == null) return 0;
            int count = 0;
            foreach (var kv in other)
            {
                var k = NormalizeKey(kv.Key ?? "");
                var v = NormalizeValue(kv.Value);
                if (string.IsNullOrEmpty(k)) continue;

                if (overwrite || !dict.ContainsKey(k))
                {
                    dict[k] = v;
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 导出为 key=value 格式行
        /// </summary>
        public static IEnumerable<string> ToLines(Dictionary<string, string> dict, char separator = '=')
        {
            if (dict == null) yield break;
            foreach (var kv in dict)
                yield return kv.Key + separator + kv.Value;
        }

        /// <summary>
        /// 从 key=value 行解析到字典
        /// </summary>
        public static void ParseLines(Dictionary<string, string> dict, IEnumerable<string> lines, char separator = '=')
        {
            if (dict == null || lines == null) return;
            dict.Clear();

            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var line = raw.Trim();
                if (line.StartsWith("#") || line.StartsWith(";")) continue;

                var idx = line.IndexOf(separator);
                if (idx < 0) continue;

                var k = NormalizeKey(line.Substring(0, idx));
                if (string.IsNullOrEmpty(k)) continue;

                var v = NormalizeValue(line.Substring(idx + 1));
                dict[k] = v;
            }
        }

        // ---------------- 类型转换读取 ----------------

        /// <summary>
        /// 尝试获取整数值
        /// </summary>
        public static bool TryGetInt(Dictionary<string, string> dict, string key, out int result)
        {
            result = 0;
            if (!TryGet(dict, key, out var s)) return false;
            return int.TryParse(s, out result);
        }

        /// <summary>
        /// 尝试获取双精度值
        /// </summary>
        public static bool TryGetDouble(Dictionary<string, string> dict, string key, out double result)
        {
            result = 0;
            if (!TryGet(dict, key, out var s)) return false;
            return double.TryParse(s, out result);
        }

        /// <summary>
        /// 尝试获取布尔值（支持 true/false/1/0）
        /// </summary>
        public static bool TryGetBool(Dictionary<string, string> dict, string key, out bool result)
        {
            result = false;
            if (!TryGet(dict, key, out var s)) return false;

            if (bool.TryParse(s, out result))
                return true;

            if (s == "1") { result = true; return true; }
            if (s == "0") { result = false; return true; }

            return false;
        }

        /// <summary>
        /// 判断两个 Key 对应的值是否相等
        /// </summary>
        /// <param name="dict">字典</param>
        /// <param name="key1">第一个 Key</param>
        /// <param name="key2">第二个 Key</param>
        /// <param name="ignoreCase">是否忽略大小写（默认 true）</param>
        public static bool AreValuesEqual(Dictionary<string, string> dict, string key1, string key2, bool ignoreCase = true)
        {
            if (dict == null || key1 == null || key2 == null) return false;

            if (!dict.TryGetValue(NormalizeKey(key1), out var v1)) return false;
            if (!dict.TryGetValue(NormalizeKey(key2), out var v2)) return false;

            return ignoreCase
                ? string.Equals(v1, v2, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v1, v2, StringComparison.Ordinal);
        }

        // ---------------- 内部工具 ----------------

        /// <summary>
        /// 标准化 Key
        /// </summary>
        private static string NormalizeKey(string key)
        {
            if (key == null) return string.Empty;
            return _trim ? key.Trim() : key;
        }

        /// <summary>
        /// 标准化 Value
        /// </summary>
        private static string NormalizeValue(string value)
        {
            if (value == null) return string.Empty;
            return _trim ? value.Trim() : value;
        }

    }

}
