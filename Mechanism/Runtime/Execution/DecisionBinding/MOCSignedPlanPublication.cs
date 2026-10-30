using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>
    /// A canonical, domain-separated publisher envelope. Its Plan bytes and
    /// capability version closure are part of the signature; signatures are
    /// supplied by the Host, never generated using secrets in this library.
    /// </summary>
    public sealed class MOCSignedPlanRelease
    {
        private const int Magic=0x31525043; // CPR1
        private const int Format=1;
        private const int MaxArchiveBytes=32*1024*1024;
        private static readonly Encoding Encoding=new UTF8Encoding(false,true);
        private readonly byte[] _archive;
        private readonly byte[] _signature;
        private readonly ReadOnlyDictionary<string,string> _versions;

        public MOCSignedPlanRelease(string scope,string keyId,long revision,
            byte[] archive,IReadOnlyDictionary<string,string> versions,byte[] signature)
        {
            if(string.IsNullOrWhiteSpace(scope)||string.IsNullOrWhiteSpace(keyId)||
                scope.Length>256||keyId.Length>128 || revision<=0)
                throw new ArgumentException("moc.release.invalid_identity");
            if(archive==null||archive.Length<80||archive.Length>MaxArchiveBytes)
                throw new ArgumentException("moc.release.invalid_archive");
            if(signature==null||signature.Length<256||signature.Length>1024)
                throw new ArgumentException("moc.release.invalid_signature");
            if(versions==null||versions.Count>4096)
                throw new ArgumentException("moc.release.invalid_versions");
            var snapshot=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(KeyValuePair<string,string> pair in versions)
            {
                if(string.IsNullOrWhiteSpace(pair.Key)||pair.Key.Length>4096 ||
                    string.IsNullOrWhiteSpace(pair.Value)||pair.Value.Length>128 ||
                    pair.Key.Any(char.IsControl)||pair.Value.Any(char.IsControl) ||
                    snapshot.ContainsKey(pair.Key))
                    throw new ArgumentException("moc.release.invalid_versions");
                snapshot.Add(pair.Key,pair.Value);
            }
            Scope=scope;KeyId=keyId;PublicationRevision=revision;
            _archive=(byte[])archive.Clone();
            _signature=(byte[])signature.Clone();
            _versions=new ReadOnlyDictionary<string,string>(snapshot);
        }
        public string Scope {get;private set;}
        public string KeyId {get;private set;}
        public long PublicationRevision {get;private set;}
        public IReadOnlyDictionary<string,string> CapabilityVersions {get{return _versions;}}
        public byte[] CopyArchive(){return (byte[])_archive.Clone();}
        public byte[] CopySignature(){return (byte[])_signature.Clone();}

        public byte[] SigningPayload()
        {
            return BuildSigningPayload(Scope,KeyId,PublicationRevision,_archive,_versions);
        }

        /// <summary>Host signs these bytes using its private key. KeyId is only
        /// looked up in the Host's trusted key ring, never taken as a public key.</summary>
        public static byte[] BuildSigningPayload(string scope,string keyId,long revision,
            byte[] archive,IReadOnlyDictionary<string,string> versions)
        {
            if(string.IsNullOrWhiteSpace(scope)||string.IsNullOrWhiteSpace(keyId)||
                scope.Length>256||keyId.Length>128||revision<=0||
                archive==null||archive.Length<80||archive.Length>MaxArchiveBytes||
                versions==null||versions.Count>4096)
                throw new ArgumentException("moc.release.invalid_payload");
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,Encoding,true))
            {
                writer.Write(Magic);writer.Write(Format);
                WriteString(writer,"CxVisual.Decision.SignedPlanRelease/v1",64);
                WriteString(writer,scope,256);
                WriteString(writer,keyId,128);
                writer.Write(revision);
                writer.Write(versions.Count);
                foreach(KeyValuePair<string,string> pair in versions
                    .OrderBy(x=>x.Key,StringComparer.Ordinal))
                {
                    WriteString(writer,pair.Key,4096);
                    WriteString(writer,pair.Value,128);
                }
                writer.Write(archive.Length);writer.Write(archive);
                writer.Flush();
                return stream.ToArray();
            }
        }

        public byte[] Export()
        {
            byte[] payload=SigningPayload();
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,Encoding,true))
            {
                writer.Write(payload.Length);writer.Write(payload);
                writer.Write(_signature.Length);writer.Write(_signature);
                writer.Flush();return stream.ToArray();
            }
        }

        public static MOCSignedPlanRelease Import(byte[] data)
        {
            if(data==null||data.Length<310||data.Length>MaxArchiveBytes+32*1024)
                throw new InvalidDataException("moc.release.envelope_size");
            using(var stream=new MemoryStream(data,false))
            using(var reader=new BinaryReader(stream,Encoding,true))
            {
                int payloadLength=ReadCount(reader,MaxArchiveBytes+16384);
                if(payloadLength>stream.Length-stream.Position-260)
                    throw new InvalidDataException("moc.release.envelope_size");
                byte[] payload=reader.ReadBytes(payloadLength);
                int signatureLength=ReadCount(reader,1024);
                if(signatureLength<256||signatureLength!=stream.Length-stream.Position)
                    throw new InvalidDataException("moc.release.signature_size");
                byte[] signature=reader.ReadBytes(signatureLength);
                using(var inner=new MemoryStream(payload,false))
                using(var p=new BinaryReader(inner,Encoding,true))
                {
                    if(p.ReadInt32()!=Magic||p.ReadInt32()!=Format ||
                        ReadString(p,64)!="CxVisual.Decision.SignedPlanRelease/v1")
                        throw new InvalidDataException("moc.release.envelope_version");
                    string scope=ReadString(p,256),keyId=ReadString(p,128);
                    long revision=p.ReadInt64();
                    int count=ReadCount(p,4096);
                    var versions=new Dictionary<string,string>(StringComparer.Ordinal);
                    for(int i=0;i<count;i++)
                    {
                        string key=ReadString(p,4096);
                        string version=ReadString(p,128);
                        if(versions.ContainsKey(key))
                            throw new InvalidDataException("moc.release.duplicate_version");
                        versions.Add(key,version);
                    }
                    int archiveLength=ReadCount(p,MaxArchiveBytes);
                    if(archiveLength<80||archiveLength!=inner.Length-inner.Position)
                        throw new InvalidDataException("moc.release.archive_length");
                    byte[] archive=p.ReadBytes(archiveLength);
                    var release=new MOCSignedPlanRelease(scope,keyId,revision,
                        archive,versions,signature);
                    if(!release.SigningPayload().SequenceEqual(payload))
                        throw new InvalidDataException("moc.release.noncanonical");
                    return release;
                }
            }
        }
        private static void WriteString(BinaryWriter writer,string value,int max)
        {
            byte[] bytes=Encoding.GetBytes(value??"");
            if(bytes.Length==0||bytes.Length>max||value.Any(char.IsControl))
                throw new ArgumentException("moc.release.string");
            writer.Write(bytes.Length);writer.Write(bytes);
        }
        private static string ReadString(BinaryReader reader,int max)
        {
            int n=ReadCount(reader,max);
            if(n==0)throw new InvalidDataException("moc.release.string");
            byte[] bytes=reader.ReadBytes(n);
            if(bytes.Length!=n)throw new EndOfStreamException("moc.release.string");
            return Encoding.GetString(bytes);
        }
        private static int ReadCount(BinaryReader reader,int max)
        {
            int n=reader.ReadInt32();
            if(n<0||n>max)throw new InvalidDataException("moc.release.count");
            return n;
        }
    }

    /// <summary>Only Host-configured trusted public keys are consulted.</summary>
    public interface IMOCReleaseSignatureVerifier
    {
        bool Verify(string keyId,byte[] payload,byte[] signature);
    }

    /// <summary>RSA-2048+ SHA-256 PKCS#1 v1.5 verification. The key ring
    /// contains ONLY public key parameters provided by the trusted Host.</summary>
    public sealed class MOCPinnedRsaReleaseVerifier : IMOCReleaseSignatureVerifier
    {
        private readonly Dictionary<string,RSAParameters> _keys =
            new Dictionary<string,RSAParameters>(StringComparer.Ordinal);
        public MOCPinnedRsaReleaseVerifier(IDictionary<string,RSAParameters> publicKeys)
        {
            if(publicKeys==null||publicKeys.Count==0)
                throw new ArgumentException("moc.release.trust_anchors_required");
            foreach(KeyValuePair<string,RSAParameters> entry in publicKeys)
            {
                if(string.IsNullOrWhiteSpace(entry.Key)||entry.Value.Modulus==null||
                    entry.Value.Modulus.Length<256||entry.Value.Exponent==null||
                    entry.Value.Exponent.Length==0||entry.Value.D!=null)
                    throw new ArgumentException("moc.release.invalid_trusted_key");
                var publicCopy=new RSAParameters{
                    Modulus=(byte[])entry.Value.Modulus.Clone(),
                    Exponent=(byte[])entry.Value.Exponent.Clone()
                };
                _keys.Add(entry.Key,publicCopy);
            }
        }
        public bool Verify(string keyId,byte[] payload,byte[] signature)
        {
            RSAParameters key;
            if(string.IsNullOrWhiteSpace(keyId)||payload==null||signature==null||
                !_keys.TryGetValue(keyId,out key))return false;
            try
            {
                using(RSA rsa=RSA.Create())
                {
                    rsa.ImportParameters(key);
                    return rsa.VerifyData(payload,signature,
                        HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
                }
            }
            catch(CryptographicException){return false;}
        }
    }

    /// <summary>
    /// Host-owned authorization + durable monotonic reservation MUST be atomic.
    /// For the same deployment scope it must reject every revision no greater
    /// than the last reserved one, across crashes/process restarts. It must not
    /// grant a reservation to an unauthorized principal. No permissive default.
    /// </summary>
    public interface IMOCTrustedReleaseAdmission
    {
        /// <summary>Side-effect-free Host principal/scope authorization. This
        /// runs before the professional Resolver is accessed, and never consumes
        /// a publication revision. Reserve MUST independently reauthorize.</summary>
        bool IsAuthorized(string principal,string scope);
        bool TryAuthorizeAndReserve(string principal,string scope,
            long revision,byte[] signedPayloadSha256);
    }

    /// <summary>Signed archive admission in front of the existing MRK Publisher.
    /// Failures cannot stage/activate a Plan; every success goes through the
    /// Publisher's real Host authorization and PlanSlot version gate.</summary>
    public sealed class MOCTrustedPlanPublisher
    {
        private readonly string _scope;
        private readonly IMOCReleaseSignatureVerifier _verifier;
        private readonly IMOCTrustedReleaseAdmission _admission;
        private readonly MRKExecutionPlanPublisher _publisher;
        public MOCTrustedPlanPublisher(string scope,IMOCReleaseSignatureVerifier verifier,
            IMOCTrustedReleaseAdmission admission,MRKExecutionPlanPublisher publisher)
        {
            if(string.IsNullOrWhiteSpace(scope)||verifier==null||
                admission==null||publisher==null)
                throw new ArgumentException("moc.release.trusted_host_required");
            _scope=scope;_verifier=verifier;_admission=admission;_publisher=publisher;
        }
        public MRKPlanPublicationReceipt VerifyAndStage(string principal,
            MOCSignedPlanRelease release,MDCStaticCatalogSnapshot catalog,
            IMRKCapabilityResolver resolver,bool allowUnverifiedLegacyContracts=false)
        {
            if(release==null)throw new ArgumentNullException("release");
            if(string.IsNullOrWhiteSpace(principal)||
                !StringComparer.Ordinal.Equals(_scope,release.Scope))
                throw new UnauthorizedAccessException("moc.release.scope_denied");
            byte[] signingPayload=release.SigningPayload();
            if(!_verifier.Verify(release.KeyId,signingPayload,release.CopySignature()))
                throw new UnauthorizedAccessException("moc.release.signature_invalid");

            // Deny unauthorized callers before any professional Resolver
            // query or CPU-heavy compilation. Host re-checks atomically at
            // TryAuthorizeAndReserve to prevent a time-of-check bypass.
            if(!_admission.IsAuthorized(principal,_scope))
                throw new UnauthorizedAccessException("moc.release.admission_denied");

            MDCDeclarativePlan plan=MDCPlanArchiveCodec.Deserialize(release.CopyArchive());
            // Includes exact static catalog, complete Host capability versions,
            // and double-check that no provider changed its version while lowering.
            IMRKVersionedCapabilityResolver versioned=resolver as IMRKVersionedCapabilityResolver;
            MOCCompiledPlan compiled=new MOCManifestRuntimeBinding().Bind(
                plan,catalog,resolver,allowUnverifiedLegacyContracts:
                allowUnverifiedLegacyContracts,expectedCapabilityVersions:release.CapabilityVersions);
            foreach(KeyValuePair<string,string> entry in release.CapabilityVersions)
            {
                string current;
                if(versioned==null||!versioned.TryGetCapabilityVersion(entry.Key,out current)||
                    !StringComparer.Ordinal.Equals(current,entry.Value))
                    throw new InvalidOperationException("moc.release.provider_version_changed");
            }
            byte[] hash;
            using(SHA256 sha=SHA256.Create())hash=sha.ComputeHash(signingPayload);
            // Reserve BEFORE MRK Stage so a Host crash cannot erase the replay
            // watermark after a successful stage. A subsequent Stage failure
            // consumes this revision; the Host must issue a HIGHER revision.
            if(!_admission.TryAuthorizeAndReserve(principal,_scope,
                release.PublicationRevision,hash))
                throw new UnauthorizedAccessException("moc.release.admission_denied");
            return _publisher.StagePrecompiled(principal,compiled.Plan,
                release.PublicationRevision);
        }
    }
}
