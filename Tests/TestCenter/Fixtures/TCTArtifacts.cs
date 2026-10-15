using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.Fixtures
{
    // Runs have their own workspace; no production document or golden is ever modified implicitly.
    public sealed class TCTTestArtifactStore
    {
        private readonly string _root;
        private readonly long _maxBytes;
        public TCTTestArtifactStore(string root, long maxBytes = 64L * 1024 * 1024)
        {
            if (maxBytes < 1) throw new ArgumentOutOfRangeException("maxBytes");
            _root = Path.GetFullPath(root ?? throw new ArgumentNullException("root"));
            _maxBytes = maxBytes;
            Directory.CreateDirectory(Path.Combine(_root, "artifacts"));
        }
        public string Root { get { return _root; } }
        public TCTArtifactRef Save(byte[] bytes, string mime, string attemptId)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (bytes.LongLength > _maxBytes) throw new InvalidDataException("Artifact exceeds size limit");
            string hash = TCTBaselineService.Sha256(bytes);
            string relative = "artifacts/" + hash;
            string path = Path.Combine(_root, "artifacts", hash);
            if (!File.Exists(path))
            {
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    File.WriteAllBytes(temp, bytes);
                    if (!File.Exists(path)) File.Move(temp, path);
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            return new TCTArtifactRef(relative, mime, hash, bytes.LongLength, attemptId);
        }
        public byte[] Read(TCTArtifactRef artifact)
        {
            if (artifact == null) throw new ArgumentNullException("artifact");
            string expectedRelative = "artifacts/" + artifact.Sha256;
            if (!string.Equals(expectedRelative, artifact.RelativePath, StringComparison.Ordinal))
                throw new InvalidDataException("Non content-addressed artifact");
            string path = Path.Combine(_root, "artifacts", artifact.Sha256);
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length != artifact.ByteSize || info.Length > _maxBytes)
                throw new InvalidDataException("Artifact length/availability mismatch");
            byte[] bytes = File.ReadAllBytes(path);
            if (!string.Equals(TCTBaselineService.Sha256(bytes), artifact.Sha256,
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Artifact hash mismatch");
            return bytes;
        }
        public static string RunRoot(string runId)
        {
            Guid guid;
            if (!Guid.TryParseExact(runId, "N", out guid))
                throw new ArgumentException("RunId must be a canonical GUID", "runId");
            return Path.Combine(Path.GetTempPath(), "CxVisual-TCT-Evidence", runId);
        }
    }

    // Teardown cleanup must be confined to a generated directory and must never recurse into reparse points.
    public sealed class TCTTestSandbox : IDisposable
    {
        private readonly string _path;
        private bool _disposed;
        public TCTTestSandbox()
        {
            _path = Path.Combine(Path.GetTempPath(), "CxVisual-TCT-Sandbox",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_path);
        }
        public string Root { get { return _path; } }
        public string PathFor(string relative)
        {
            if (_disposed) throw new ObjectDisposedException("TCTTestSandbox");
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new ArgumentException("Relative path only", "relative");
            string root = _path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(_path, relative));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Sandbox traversal rejected");
            return path;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!Directory.Exists(_path)) return;
            AssertNoReparse(_path);
            Directory.Delete(_path, true);
        }
        private static void AssertNoReparse(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Sandbox contains a reparse point; refusing recursive delete");
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Sandbox contains a reparse point; refusing recursive delete");
                if (Directory.Exists(entry)) AssertNoReparse(entry);
            }
        }
    }

    // Intentional detail-only semantic comparisons; not a substitute for image or 3D golden math.
    public sealed class TCTSnapshotDiff
    {
        public TCTSnapshotDiff(bool equal, int differences, string explanation)
        { Equal = equal; Differences = differences; Explanation = explanation; }
        public bool Equal { get; private set; }
        public int Differences { get; private set; }
        public string Explanation { get; private set; }
    }
    public static class TCTSnapshotDiffService
    {
        public static TCTSnapshotDiff CompareBytes(byte[] expected, byte[] actual)
        {
            if (expected == null || actual == null) throw new ArgumentNullException("buffer");
            int difference = Math.Abs(expected.Length - actual.Length);
            int first = -1;
            for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++)
                if (expected[i] != actual[i])
                {
                    difference++;
                    if (first < 0) first = i;
                }
            return new TCTSnapshotDiff(difference == 0, difference,
                difference == 0 ? "Binary identical" : "First offset=" + first + "; differing/extra bytes=" + difference);
        }
        public static TCTSnapshotDiff CompareLines(string expected, string actual)
        {
            if (expected == null || actual == null) throw new ArgumentNullException("text");
            string[] a = expected.Replace("\r\n", "\n").Split('\n');
            string[] b = actual.Replace("\r\n", "\n").Split('\n');
            int diff = Math.Abs(a.Length - b.Length);
            int first = -1;
            for (int i = 0; i < Math.Min(a.Length, b.Length); i++)
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                { diff++; if (first < 0) first = i + 1; }
            return new TCTSnapshotDiff(diff == 0, diff,
                diff == 0 ? "Text identical" : "First divergent line=" + first + "; total=" + diff);
        }
    }
}
