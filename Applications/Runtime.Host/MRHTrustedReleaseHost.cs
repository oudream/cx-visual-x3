using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// A Host-provisioned public signing identity. A publisher may supply KeyId,
    /// but it cannot specify the file or pin for that KeyId.
    /// Pin and certificate path MUST come from protected Host configuration.
    /// </summary>
    internal sealed class MRHTrustedSigningKey
    {
        internal MRHTrustedSigningKey(string keyId,string certificatePath,
            string expectedCertificateSha256)
        {
            if(string.IsNullOrWhiteSpace(keyId)||keyId.Length>128 ||
                string.IsNullOrWhiteSpace(certificatePath) ||
                string.IsNullOrWhiteSpace(expectedCertificateSha256) ||
                expectedCertificateSha256.Length!=64 ||
                expectedCertificateSha256.Any(c=>!Uri.IsHexDigit(c)))
                throw new ArgumentException("mrh.release.invalid_key_configuration");
            KeyId=keyId;
            CertificatePath=Path.GetFullPath(certificatePath);
            ExpectedSha256=expectedCertificateSha256.ToUpperInvariant();
        }
        internal string KeyId {get;private set;}
        internal string CertificatePath {get;private set;}
        internal string ExpectedSha256 {get;private set;}
    }

    /// <summary>
    /// Production assembly of the existing Decision Plan and MRK Publisher.
    /// Requires externally configured trust pins, authorization, an existing
    /// commissioned durable ledger and a real MRK publication policy. It never
    /// uses the simulation publish policy and never auto-activates a Plan.
    /// </summary>
    internal sealed class MRHTrustedReleaseHost
    {
        private const int MaxEnvelope=32*1024*1024+32*1024;
        private readonly MOCTrustedPlanPublisher _trusted;
        private readonly MRKExecutionPlanPublisher _publisher;
        private readonly MDCStaticCatalogSnapshot _catalog;
        private readonly IMRKCapabilityResolver _resolver;

        internal MRHTrustedReleaseHost(string scope,string ledgerPath,
            IEnumerable<MRHTrustedSigningKey> trustedKeys,
            Func<string,bool> authorizeReservation,
            MRKExecutionPlanPublisher publisher,
            MDCStaticCatalogSnapshot catalog,IMRKCapabilityResolver resolver)
        {
            if(string.IsNullOrWhiteSpace(scope)||scope.Length>256||
                trustedKeys==null||authorizeReservation==null||
                publisher==null||catalog==null||resolver==null)
                throw new ArgumentException("mrh.release.host_settings_required");

            // Load, pin and snapshot all trust keys before touching a package.
            // There is no fallback to a user-supplied key or to 'accept all'.
            var keys=new Dictionary<string,RSAParameters>(StringComparer.Ordinal);
            foreach(MRHTrustedSigningKey key in trustedKeys)
            {
                if(key==null||keys.ContainsKey(key.KeyId))
                    throw new ArgumentException("mrh.release.duplicate_trust_identity");
                byte[] certificateBytes=ReadBounded(key.CertificatePath,16384);
                byte[] digest;
                using(SHA256 sha=SHA256.Create())
                    digest=sha.ComputeHash(certificateBytes);
                string actual=BitConverter.ToString(digest).Replace("-","");
                if(!StringComparer.Ordinal.Equals(actual,key.ExpectedSha256))
                    throw new UnauthorizedAccessException("mrh.release.certificate_pin_mismatch");
                using(var certificate=new X509Certificate2(certificateBytes))
                {
                    // Pinning establishes which key the Host trusts. Expired
                    // certificates are not silently accepted as a new identity.
                    DateTime now=DateTime.UtcNow;
                    if(certificate.NotBefore.ToUniversalTime()>now ||
                        certificate.NotAfter.ToUniversalTime()<now)
                        throw new UnauthorizedAccessException("mrh.release.certificate_expired");
                    using(RSA rsa=certificate.GetRSAPublicKey())
                    {
                        if(rsa==null)
                            throw new ArgumentException("mrh.release.non_rsa_certificate");
                        RSAParameters pub=rsa.ExportParameters(false);
                        if(pub.Modulus==null||pub.Modulus.Length<256||
                            pub.Exponent==null||pub.Exponent.Length==0)
                            throw new ArgumentException("mrh.release.weak_public_key");
                        keys.Add(key.KeyId,pub);
                    }
                }
            }
            if(keys.Count==0)
                throw new ArgumentException("mrh.release.trust_key_required");
            // Missing or corrupt ledger fails in this constructor. No auto
            // commissioning or reset is possible from release import.
            var admission=new MRHFileTrustedReleaseAdmission(
                ledgerPath,scope,authorizeReservation);
            _trusted=new MOCTrustedPlanPublisher(scope,
                new MOCPinnedRsaReleaseVerifier(keys),admission,publisher);
            _publisher=publisher;
            _catalog=catalog;_resolver=resolver;
        }

        internal MRKPlanPublicationReceipt StageFile(string principal,string envelopeFile,
            bool allowUnverifiedLegacyContracts=false)
        {
            if(string.IsNullOrWhiteSpace(envelopeFile))
                throw new ArgumentException("mrh.release.envelope_path_required");
            byte[] package=ReadBounded(envelopeFile,MaxEnvelope);
            MOCSignedPlanRelease release=MOCSignedPlanRelease.Import(package);
            return _trusted.VerifyAndStage(principal,release,_catalog,_resolver,
                allowUnverifiedLegacyContracts);
        }

        /// <summary>Explicit Host-controlled activation. StageFile never
        /// calls this method or bypasses the existing MRK safe point.</summary>
        internal bool TryActivate(string principal)
        {
            return _publisher.TryActivate(principal);
        }

        private static byte[] ReadBounded(string filename,int maxSize)
        {
            string path=Path.GetFullPath(filename);
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<=0||stream.Length>maxSize)
                    throw new InvalidDataException("mrh.release.file_size");
                byte[] data=new byte[(int)stream.Length];
                int offset=0;
                while(offset<data.Length)
                {
                    int n=stream.Read(data,offset,data.Length-offset);
                    if(n<=0)throw new EndOfStreamException("mrh.release.incomplete_file");
                    offset+=n;
                }
                if(stream.ReadByte()!=-1)
                    throw new InvalidDataException("mrh.release.file_changed");
                return data;
            }
        }
    }
}
