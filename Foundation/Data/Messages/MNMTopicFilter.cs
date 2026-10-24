using System;
namespace CxVisual.Foundation.Data.Messages
{
    /// <summary>MQTT风格的Topic过滤表达式：+匹配一层，#只能位于末尾并匹配零或多层。</summary>
    public sealed class MNMTopicFilter
    {
        private readonly string[] _parts;
        public string Filter { get; private set; }
        public MNMTopicFilter(string filter)
        {
            Filter = ValidateFilter(filter);
            _parts = Filter.Substring(1).Split('/');
        }
        internal static bool IsCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value == "." || value == "..")
                return false;
            foreach (char c in value)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                    (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
                    return false;
            return true;
        }
        /// <summary>只接受CxVisual八层消息Topic，具体发布路径不能含通配符。</summary>
        public static string ValidateTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic) || topic.Length > 512 || topic[0] != '/')
                throw new ArgumentException("消息Topic必须为绝对路径", "topic");
            string[] p = topic.Substring(1).Split('/');
            if (p.Length != 8 || (p[0] != "machine" && p[0] != "container") ||
                p[2] != "app" || p[4] != "module" || p[6] != "message")
                throw new ArgumentException("消息Topic须采用machine/container、app、module、message八层路径", "topic");
            foreach (string part in p)
                if (!IsCode(part)) throw new ArgumentException("消息Topic存在非法段", "topic");
            return topic;
        }
        public static string ValidateFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter) || filter.Length > 512 || filter[0] != '/')
                throw new ArgumentException("过滤表达式必须为绝对路径", "filter");
            string[] p = filter.Substring(1).Split('/');
            if (p.Length > 8 || (p[p.Length - 1] != "#" && p.Length != 8))
                throw new ArgumentException("过滤表达式必须完整或以#结束", "filter");
            for (int i = 0; i < p.Length; ++i)
                if (p[i] == "#")
                {
                    if (i != p.Length - 1)
                        throw new ArgumentException("#只允许独占最后一层", "filter");
                }
                else if (p[i] != "+" && !IsCode(p[i]))
                    throw new ArgumentException("只允许完整层+或最后一层#", "filter");
            if (p[0] != "machine" && p[0] != "container" && p[0] != "+" && p[0] != "#")
                throw new ArgumentException("过滤表达式的根无效", "filter");
            if (p.Length > 2 && p[2] != "app" && p[2] != "+" && p[2] != "#")
                throw new ArgumentException("app层无效", "filter");
            if (p.Length > 4 && p[4] != "module" && p[4] != "+" && p[4] != "#")
                throw new ArgumentException("module层无效", "filter");
            if (p.Length > 6 && p[6] != "message" && p[6] != "+" && p[6] != "#")
                throw new ArgumentException("message层无效", "filter");
            return filter;
        }
        public bool Matches(string topic)
        {
            string[] p = ValidateTopic(topic).Substring(1).Split('/');
            for (int i = 0; i < _parts.Length; ++i)
            {
                if (_parts[i] == "#") return true;
                if (i >= p.Length) return false;
                if (_parts[i] != "+" && !string.Equals(_parts[i], p[i], StringComparison.Ordinal))
                    return false;
            }
            return _parts.Length == p.Length;
        }
        public static bool IsMatch(string filter, string topic)
        { return new MNMTopicFilter(filter).Matches(topic); }
    }
}
