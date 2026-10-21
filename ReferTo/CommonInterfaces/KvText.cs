using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    /// <summary>
    /// KvText：k=v 逐行文本載體（非 Helper）。
    /// - 每行一個鍵值對：key=value
    /// - 只用第一個 Separator 作分隔，value 可包含 '='
    /// - 兼容 \n / \r\n / \r
    /// - 工業使用：讀取不拋異常，提供 TryGet / GetXxx(default)
    /// </summary>
    public sealed class KvText
    {
        private readonly Dictionary<string, string> _map;
        private readonly ReadOnlyDictionary<string, string> _readOnlyView;

        /// <summary>分隔符，默認 '='。</summary>
        public char Separator { get; private set; }

        /// <summary>Key 是否大小寫不敏感，默認 false（敏感）。</summary>
        public bool CaseInsensitiveKeys { get; private set; }

        /// <summary>是否忽略註釋行（行首 # 或 ;），默認 true。</summary>
        public bool IgnoreCommentLines { get; private set; }

        /// <summary>當解析到沒有 Separator 的行，是否忽略。默認 true。</summary>
        public bool IgnoreMalformedLines { get; private set; }

        /// <summary>當解析到空 key（如 "=abc"），是否忽略。默認 true。</summary>
        public bool IgnoreEmptyKeys { get; private set; }

        /// <summary>當 Set(key, null) 時，是否轉成空字串。默認 true。</summary>
        public bool NullValueAsEmptyString { get; private set; }

        public int Count { get { return _map.Count; } }

        /// <summary>建立一個空的 KvText，用於逐步 Set。</summary>
        public KvText()
            : this(separator: '=', caseInsensitiveKeys: false)
        {
        }

        /// <summary>由文本解析。</summary>
        public KvText(string text)
            : this(separator: '=', caseInsensitiveKeys: false)
        {
            LoadFromText(text);
        }

        /// <summary>自定義配置構造（需要時用）。</summary>
        public KvText(char separator, bool caseInsensitiveKeys)
        {
            Separator = separator;
            CaseInsensitiveKeys = caseInsensitiveKeys;

            IgnoreCommentLines = true;
            IgnoreMalformedLines = true;
            IgnoreEmptyKeys = true;
            NullValueAsEmptyString = true;

            _map = new Dictionary<string, string>(caseInsensitiveKeys
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);

            _readOnlyView = new ReadOnlyDictionary<string, string>(_map);
        }

        /// <summary>語義更清晰的解析入口。</summary>
        public static KvText Parse(string text)
        {
            return new KvText(text);
        }

        /// <summary>返回只讀視圖（不會複製）。</summary>
        public IReadOnlyDictionary<string, string> AsReadOnly()
        {
            return _readOnlyView;
        }

        public bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _map.ContainsKey(key);
        }

        /// <summary>
        /// 設置鍵值（鏈式）。
        /// key 會做 Trim（避免 " SN" / "SN " 造成查不到），value 原樣保留（不 Trim）。
        /// </summary>
        public KvText Set(string key, string value)
        {
            ValidateKeyForSet(key);

            string k = key.Trim();
            string v = value;

            if (v == null && NullValueAsEmptyString)
            {
                v = string.Empty;
            }

            _map[k] = v ?? string.Empty;
            return this;
        }

        public KvText Set(string key, object value)
        {
            if (value == null) return Set(key, (string)null);
            return Set(key, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        public KvText SetRange(IDictionary<string, string> values)
        {
            if (values == null) return this;

            foreach (KeyValuePair<string, string> kv in values)
            {
                Set(kv.Key, kv.Value);
            }
            return this;
        }

        public KvText Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return this;
            _map.Remove(key.Trim());
            return this;
        }

        public KvText Clear()
        {
            _map.Clear();
            return this;
        }

        // ------------------ 帶默認值 Get ------------------

        public string GetString(string key, string defaultValue)
        {
            string v;
            return TryGetString(key, out v) ? v : defaultValue;
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            int v;
            return TryGetInt(key, out v) ? v : defaultValue;
        }

        public long GetLong(string key, long defaultValue = 0)
        {
            long v;
            return TryGetLong(key, out v) ? v : defaultValue;
        }

        public double GetDouble(string key, double defaultValue = 0)
        {
            double v;
            return TryGetDouble(key, out v) ? v : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            bool v;
            return TryGetBool(key, out v) ? v : defaultValue;
        }

        public DateTime GetDateTime(string key, DateTime defaultValue)
        {
            DateTime v;
            return TryGetDateTime(key, out v) ? v : defaultValue;
        }

        // ------------------ TryGet 型別 ------------------

        public bool TryGetString(string key, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            return _map.TryGetValue(key.Trim(), out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = default(int);

            string s;
            if (!TryGetString(key, out s)) return false;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetLong(string key, out long value)
        {
            value = default(long);

            string s;
            if (!TryGetString(key, out s)) return false;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();
            return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetDouble(string key, out double value)
        {
            value = default(double);

            string s;
            if (!TryGetString(key, out s)) return false;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// TryGetBool 支持：true/false, 1/0, yes/no, y/n, on/off（大小寫不敏感）
        /// </summary>
        public bool TryGetBool(string key, out bool value)
        {
            value = default(bool);

            string s;
            if (!TryGetString(key, out s)) return false;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();

            // bool.TryParse 只認 true/false
            bool b;
            if (bool.TryParse(s, out b))
            {
                value = b;
                return true;
            }

            // 工業常見
            string u = s.ToUpperInvariant();
            if (u == "1" || u == "YES" || u == "Y" || u == "ON" || u == "OK" || u == "TRUE")
            {
                value = true;
                return true;
            }
            if (u == "0" || u == "NO" || u == "N" || u == "OFF" || u == "NG" || u == "FALSE")
            {
                value = false;
                return true;
            }

            return false;
        }

        /// <summary>
        /// TryGetDateTime：
        /// 1) 支持 ISO/常見日期格式（InvariantCulture）
        /// 2) 若是純數字：當作 Unix epoch 毫秒（13位常見），或秒（10位）自動判斷
        /// </summary>
        public bool TryGetDateTime(string key, out DateTime value)
        {
            value = default(DateTime);

            string s;
            if (!TryGetString(key, out s)) return false;
            if (string.IsNullOrWhiteSpace(s)) return false;

            s = s.Trim();

            // 先試標準日期
            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out dt))
            {
                value = dt;
                return true;
            }

            // 再試 Unix time
            long n;
            if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return false;
            }

            // 粗略判斷秒/毫秒
            // 10位左右：秒；13位左右：毫秒
            try
            {
                if (n > 1000000000000L) // > 1e12 當毫秒
                {
                    value = UnixEpochLocal().AddMilliseconds(n);
                    return true;
                }
                if (n > 1000000000L) // > 1e9 當秒
                {
                    value = UnixEpochLocal().AddSeconds(n);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        // ------------------ 載入 / 輸出 ------------------

        /// <summary>把當前內容輸出為 k=v\nk=v（key 會排序，保證穩定）。</summary>
        public string ToText(bool valueIsBase64 = false)
        {
            if (_map.Count == 0) return string.Empty;

            List<string> keys = new List<string>(_map.Keys);
            keys.Sort(StringComparer.Ordinal); // 穩定輸出（方便 diff）

            StringBuilder sb = new StringBuilder(_map.Count * 16);
            for (int i = 0; i < keys.Count; i++)
            {
                string k = keys[i];
                string v = _map[k];
                if (string.IsNullOrEmpty(v))
                {
                    v = string.Empty;
                }
                else
                {
                    if (valueIsBase64)
                    {
                        v = Convert.ToBase64String(Encoding.UTF8.GetBytes(v));
                    }
                }

                sb.Append(k);
                sb.Append(Separator);
                sb.Append(v);

                if (i != keys.Count - 1)
                {
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }


        public override string ToString()
        {
            return ToText();
        }

        /// <summary>返回一份副本 Dictionary，避免外部改到內部。</summary>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>(_map, _map.Comparer);
        }

        /// <summary>重新載入文本（會 Clear 再解析）。</summary>
        public KvText LoadFromText(string text, bool valueIsBase64 = false)
        {
            _map.Clear();

            if (string.IsNullOrEmpty(text))
            {
                return this;
            }

            foreach (string rawLine in SplitLines(text))
            {
                if (rawLine == null) continue;

                string line = rawLine;

                if (line.Length == 0) continue;

                // 忽略註釋行（行首 # 或 ;）
                if (IgnoreCommentLines)
                {
                    char c0 = line[0];
                    if (c0 == '#' || c0 == ';') continue;
                }

                int idx = line.IndexOf(Separator);
                if (idx < 0)
                {
                    if (!IgnoreMalformedLines)
                    {
                        // 沒 Separator，視為 key，value = ""
                        string onlyKey = line.Trim();
                        if (onlyKey.Length == 0)
                        {
                            if (!IgnoreEmptyKeys)
                            {
                                // ignore
                            }
                            continue;
                        }
                        _map[onlyKey] = string.Empty;
                    }
                    continue;
                }

                string key = line.Substring(0, idx).Trim();
                string value = (idx + 1 < line.Length) ? line.Substring(idx + 1) : string.Empty;

                if (key.Length == 0)
                {
                    if (IgnoreEmptyKeys) continue;
                    // 若不忽略空 key，就跳過或存入空 key（通常不建議）
                    continue;
                }

                if (string.IsNullOrEmpty(value))
                {
                    // 後者覆蓋前者
                    _map[key] = string.Empty;
                }
                else
                {
                    if (valueIsBase64)
                    {
                        // 1. Base64 解码
                        var rawBytes = Convert.FromBase64String(value);
                        string rawText = Encoding.UTF8.GetString(rawBytes);
                        _map[key] = rawText;
                    }
                    else
                    {
                        _map[key] = value;
                    }
                }
            }

            return this;
        }

        // ------------------ 私有工具 ------------------

        private void ValidateKeyForSet(string key)
        {
            if (key == null) throw new ArgumentNullException("key");

            string k = key.Trim();
            if (k.Length == 0) throw new ArgumentException("key is empty.", "key");

            // 禁止換行（會破壞格式）
            if (k.IndexOf('\n') >= 0 || k.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("key contains newline.", "key");
            }

            // 禁止 separator（會歧義）
            if (k.IndexOf(Separator) >= 0)
            {
                throw new ArgumentException("key contains separator '" + Separator + "'.", "key");
            }
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            // 手寫 split：兼容 \n / \r\n / \r，且不產生多餘空行
            int len = text.Length;
            int start = 0;

            for (int i = 0; i < len; i++)
            {
                char c = text[i];
                if (c == '\r' || c == '\n')
                {
                    int count = i - start;
                    if (count > 0)
                    {
                        yield return text.Substring(start, count);
                    }
                    else
                    {
                        // 空行，跳過
                    }

                    // \r\n 一次吃掉
                    if (c == '\r' && i + 1 < len && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    start = i + 1;
                }
            }

            // 最後一行
            if (start < len)
            {
                int count = len - start;
                if (count > 0)
                {
                    yield return text.Substring(start, count);
                }
            }
        }

        private static DateTime UnixEpochLocal()
        {
            // epoch (1970-01-01) in local time
            return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local);
        }

    }

}
