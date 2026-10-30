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
                keyId.Any(char.IsControl) ||
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
        private readonly IMRKVersionedCapabilityResolver _versionedResolver;
        private readonly Func<string,bool> _authorizePrincipal;
        private readonly object _releaseGate=new object();
        // A staged signed package retains its own professional version pins.
        // MRK itself is still the only owner of activation and PlanSlot.
        private long? _signedPendingRevision;
        private Dictionary<string,string> _signedPendingVersions;

        internal MRHTrustedReleaseHost(string scope,string ledgerPath,
            IEnumerable<MRHTrustedSigningKey> trustedKeys,
            Func<string,bool> authorizeReservation,
            MRKExecutionPlanPublisher publisher,
            MDCStaticCatalogSnapshot catalog,IMRKCapabilityResolver resolver)
        {
            if(string.IsNullOrWhiteSpace(scope)||scope.Length>256||
                scope.Any(char.IsControl)||trustedKeys==null||authorizeReservation==null||
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
                    X509KeyUsageExtension usage=certificate.Extensions
                        .OfType<X509KeyUsageExtension>().FirstOrDefault();
                    if(usage!=null &&
                        (usage.KeyUsages & X509KeyUsageFlags.DigitalSignature)==0)
                        throw new UnauthorizedAccessException("mrh.release.certificate_not_signing");
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
            _versionedResolver=resolver as IMRKVersionedCapabilityResolver;
            _authorizePrincipal=authorizeReservation;
        }

        internal MRKPlanPublicationReceipt StageFile(string principal,string envelopeFile)
        {
            if(string.IsNullOrWhiteSpace(envelopeFile))
                throw new ArgumentException("mrh.release.envelope_path_required");
            byte[] package=ReadBounded(envelopeFile,MaxEnvelope);
            MOCSignedPlanRelease release=MOCSignedPlanRelease.Import(package);
            lock(_releaseGate)
            {
                // Production has no bypass for unverified professional contracts.
                MRKPlanPublicationReceipt receipt=_trusted.VerifyAndStage(
                    principal,release,_catalog,_resolver,
                    allowUnverifiedLegacyContracts:false);
                if(receipt.Accepted)
                {
                    if(_publisher.PendingRevision!=receipt.Revision)
                        throw new InvalidOperationException("mrh.release.pending_revision_changed");
                    _signedPendingRevision=receipt.Revision;
                    _signedPendingVersions=release.CapabilityVersions.ToDictionary(
                        x=>x.Key,x=>x.Value,StringComparer.Ordinal);
                }
                return receipt;
            }
        }

        /// <summary>Explicit Host activation. Recheck professional versions
        /// AFTER Stage and immediately BEFORE MRK's existing authorized
        /// safety-point gate. This prevents an intervening hot swap from being
        /// accepted as a signed plan. Physical device interlocks still belong
        /// to the injected Host policy, not this software version fence.</summary>
        internal bool TryActivate(string principal)
        {
            if(string.IsNullOrWhiteSpace(principal)||
                !_authorizePrincipal(principal))
                throw new UnauthorizedAccessException("mrh.release.activation_principal_denied");
            lock(_releaseGate)
            {
                if(!_signedPendingRevision.HasValue)return false;
                if(_publisher.PendingRevision!=_signedPendingRevision)
                    throw new InvalidOperationException("mrh.release.pending_revision_changed");
                if(_versionedResolver==null || _signedPendingVersions==null)
                    throw new InvalidOperationException("mrh.release.versioned_resolver_required");
                foreach(KeyValuePair<string,string> pin in _signedPendingVersions
                    .OrderBy(x=>x.Key,StringComparer.Ordinal))
                {
                    string current;
                    if(!_versionedResolver.TryGetCapabilityVersion(pin.Key,out current)||
                        !StringComparer.Ordinal.Equals(current,pin.Value))
                        throw new InvalidOperationException(
                            "mrh.release.provider_version_changed:"+pin.Key);
                }
                bool activated=_publisher.TryActivate(principal);
                if(activated)
                {
                    _signedPendingRevision=null;
                    _signedPendingVersions=null;
                }
                return activated;
            }
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
