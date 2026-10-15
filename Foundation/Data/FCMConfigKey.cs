using System;

namespace CxVisual.Foundation.Data
{
    /// <summary>F15配置键：Scope + Path组成稳定配置身份；不持有文件、设备或全局状态。</summary>
    public readonly struct FCMConfigKey : IEquatable<FCMConfigKey>
    {
        public string Scope { get; }
        public string Path { get; }
        public string Id { get; }

        public FCMConfigKey(string scope, string path)
        {
            Scope = ValidatePart(scope, "scope");
            Path = ValidatePart(path, "path");
            Id = Scope + "." + Path;
            if (Id.Length > 96)
                throw new ArgumentException("Config key exceeds the 96 character persistent ID limit");
        }

        private static string ValidatePart(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." ||
                value.StartsWith(".", StringComparison.Ordinal) ||
                value.EndsWith(".", StringComparison.Ordinal) ||
                value.Contains(".."))
                throw new ArgumentException("Config key part is invalid", name);
            foreach (char ch in value)
                if (!(ch >= 'A' && ch <= 'Z') && !(ch >= 'a' && ch <= 'z') &&
                    !(ch >= '0' && ch <= '9') && ch != '_' && ch != '-' && ch != '.')
                    throw new ArgumentException("Config key part contains invalid characters", name);
            return value;
        }

        public bool Equals(FCMConfigKey other)
        {
            return string.Equals(Id, other.Id, StringComparison.Ordinal);
        }
        public override bool Equals(object obj)
        {
            return obj is FCMConfigKey && Equals((FCMConfigKey)obj);
        }
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        }
        public override string ToString() { return Id ?? string.Empty; }
        public static bool operator ==(FCMConfigKey left, FCMConfigKey right) { return left.Equals(right); }
        public static bool operator !=(FCMConfigKey left, FCMConfigKey right) { return !left.Equals(right); }
    }
}
