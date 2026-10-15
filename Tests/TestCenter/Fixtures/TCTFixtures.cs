using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CxVisual.TestCenter.Fixtures
{
    // Fixture是测试输入；不得把基准文件覆盖操作和测试执行隐式绑定。
    public sealed class TCTTestFixtureService
    {
        private readonly string _root;
        public TCTTestFixtureService(string root)
        {
            _root = Path.GetFullPath(root ?? throw new ArgumentNullException("root"));
        }
        public string ResolvePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name required", "name");
            string path = Path.GetFullPath(Path.Combine(_root, name));
            string prefix = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Fixture path escapes root: " + name);
            return path;
        }
        public byte[] ReadBytes(string name) { return File.ReadAllBytes(ResolvePath(name)); }
        public string ReadText(string name) { return File.ReadAllText(ResolvePath(name), Encoding.UTF8); }
        public IList<string> List(string pattern)
        {
            return Directory.Exists(_root) ?
                new List<string>(Directory.GetFiles(_root, pattern, SearchOption.AllDirectories)) :
                new List<string>();
        }
    }

    public sealed class TCTBaselineService
    {
        private readonly TCTTestFixtureService _fixtures;
        public TCTBaselineService(TCTTestFixtureService fixtures)
        { _fixtures = fixtures ?? throw new ArgumentNullException("fixtures"); }
        public bool Matches(string relativePath, byte[] actual)
        {
            if (actual == null) throw new ArgumentNullException("actual");
            byte[] expected = _fixtures.ReadBytes(relativePath);
            if (expected.Length != actual.Length) return false;
            int diff = 0;
            for (int i = 0; i < actual.Length; i++) diff |= actual[i] ^ expected[i];
            return diff == 0;
        }
        // 明确人工审批调用才能更新Golden；Runner永不自动Approve。
        public void Approve(string relativePath, byte[] bytes, string approval)
        {
            if (!string.Equals(approval, "APPROVE_BASELINE", StringComparison.Ordinal))
                throw new InvalidOperationException("Explicit baseline approval required");
            if (bytes == null) throw new ArgumentNullException("bytes");
            string path = _fixtures.ResolvePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }
        public static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
