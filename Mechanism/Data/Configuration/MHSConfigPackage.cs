using System;
using System.Security.Cryptography;
using System.Text;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 可交换的离线配置包，包含内容完整性校验与来源证据。
    /// Hash 不是签名：发布授权、目标审批、跨机 ACK 由外围机制提供。
    /// </summary>
    public sealed class MHSConfigPackage
    {
        public string SectionId { get; set; }
        public int SchemaVersion { get; set; }
        public string SourceScope { get; set; }
        public string SourceRevision { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public string Json { get; set; }
        public string Sha256 { get; set; }

        public static string Hash(string json)
        {
            if (json == null) throw new ArgumentNullException("json");
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(json));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
        public bool Verify()
        {
            if (Json == null || Sha256 == null || Sha256.Length != 64) return false;
            return string.Equals(Hash(Json), Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }
}
