using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Orchestration;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// Host-owned crash-safe, cross-process exclusive revision reservation.
    /// Commission once in a protected directory. Never auto-create a missing
    /// ledger on normal startup: a deleted ledger MUST fail closed, not reset
    /// the anti-rollback watermark. Scope and ACL belong to the Host operator.
    /// </summary>
    internal sealed class MRHFileTrustedReleaseAdmission : IMOCTrustedReleaseAdmission
    {
        private const int Magic=0x31524C48; // HLR1
        private const int Version=1;
        private const int RecordLength=72;
        private const long MaxLedgerSize=16L*1024*1024;
        private readonly string _file;
        private readonly string _scope;
        private readonly Func<string,bool> _authorize;
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);

        internal MRHFileTrustedReleaseAdmission(string file,string scope,
            Func<string,bool> authorize)
        {
            if(string.IsNullOrWhiteSpace(file)||string.IsNullOrWhiteSpace(scope)||
                scope.Length>256||authorize==null)
                throw new ArgumentException("mrh.release.ledger_settings");
            _file=Path.GetFullPath(file);_scope=scope;_authorize=authorize;
            // A missing or malformed journal cannot silently reset the floor.
            using(FileStream stream=Open())
                ReadRevision(stream);
        }

        /// <summary>Explicit first commissioning only, never used by normal
        /// publisher startup. Production Host must protect directory ACL and
        /// commission from a trusted deployment/recovery transaction.</summary>
        internal static void CommissionNew(string file,string scope,long initialRevision)
        {
            if(string.IsNullOrWhiteSpace(file)||string.IsNullOrWhiteSpace(scope)||
                scope.Length>256||initialRevision<0)
                throw new ArgumentException("mrh.release.commission_parameters");
            string path=Path.GetFullPath(file);
            // No Directory.CreateDirectory here: operator must provision the
            // correct trust boundary before writing the first watermark.
            byte[] scopeBytes=Utf8.GetBytes(scope);
            if(scopeBytes.Length==0||scopeBytes.Length>1024)
                throw new ArgumentException("mrh.release.scope");
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,
                FileShare.None,4096,FileOptions.WriteThrough))
            using(var writer=new BinaryWriter(stream,Utf8,true))
            {
                writer.Write(Magic);writer.Write(Version);
                writer.Write(scopeBytes.Length);writer.Write(scopeBytes);
                writer.Write(initialRevision);
                writer.Flush();stream.Flush(true);
            }
        }

        private FileStream Open()
        {
            return new FileStream(_file,FileMode.Open,FileAccess.ReadWrite,
                FileShare.None,4096,FileOptions.WriteThrough);
        }

        public bool IsAuthorized(string principal,string scope)
        {
            return !string.IsNullOrWhiteSpace(principal) &&
                StringComparer.Ordinal.Equals(scope,_scope) &&
                _authorize(principal);
        }

        public bool TryAuthorizeAndReserve(string principal,string scope,
            long revision,byte[] signedPayloadSha256)
        {
            if(!IsAuthorized(principal,scope) ||
                revision<=0||signedPayloadSha256==null||
                signedPayloadSha256.Length!=32)
                return false;
            // FileShare.None serializes every process using this Host ledger.
            // A crashed writer leaves either a durable reservation or a torn
            // record; torn/corrupt records are rejected, never repaired silently.
            using(FileStream stream=Open())
            {
                LedgerState state=ReadRevision(stream);
                if(revision<=state.Revision)return false;
                byte[] number=BitConverter.GetBytes(revision);
                if(!BitConverter.IsLittleEndian)Array.Reverse(number);
                byte[] input=state.Chain.Concat(number)
                    .Concat(signedPayloadSha256).ToArray();
                byte[] digest=Sha(input);
                if(stream.Length+RecordLength>MaxLedgerSize)
                    throw new InvalidOperationException("mrh.release.ledger_full");
                stream.Position=stream.Length;
                stream.Write(number,0,number.Length);
                stream.Write(signedPayloadSha256,0,32);
                stream.Write(digest,0,32);
                stream.Flush(true);
                return true;
            }
        }

        private sealed class LedgerState
        {
            internal long Revision;
            internal byte[] Chain;
        }

        private LedgerState ReadRevision(FileStream stream)
        {
            if(stream.Length<24||stream.Length>MaxLedgerSize)
                throw new InvalidDataException("mrh.release.ledger_size");
            stream.Position=0;
            using(var reader=new BinaryReader(stream,Utf8,true))
            {
                int magic=reader.ReadInt32(),version=reader.ReadInt32();
                int scopeLength=reader.ReadInt32();
                if(magic!=Magic||version!=Version||scopeLength<1||scopeLength>1024||
                    scopeLength>stream.Length-stream.Position-8)
                    throw new InvalidDataException("mrh.release.ledger_header");
                string actual=Utf8.GetString(reader.ReadBytes(scopeLength));
                if(!StringComparer.Ordinal.Equals(actual,_scope))
                    throw new UnauthorizedAccessException("mrh.release.ledger_scope_mismatch");
                long revision=reader.ReadInt64();
                if(revision<0)throw new InvalidDataException("mrh.release.revision");
                long headerLength=stream.Position;
                if((stream.Length-headerLength)%RecordLength!=0)
                    throw new InvalidDataException("mrh.release.torn_record");
                if(stream.Length>MaxLedgerSize)
                    throw new InvalidDataException("mrh.release.ledger_full");
                stream.Position=0;
                byte[] header=reader.ReadBytes((int)headerLength);
                byte[] chain=Sha(header);
                while(stream.Position<stream.Length)
                {
                    byte[] number=reader.ReadBytes(8);
                    byte[] signedHash=reader.ReadBytes(32);
                    byte[] expected=reader.ReadBytes(32);
                    if(number.Length!=8||signedHash.Length!=32||expected.Length!=32)
                        throw new InvalidDataException("mrh.release.torn_record");
                    long next=BitConverter.ToInt64(
                        BitConverter.IsLittleEndian?number:number.Reverse().ToArray(),0);
                    if(next<=revision)
                        throw new InvalidDataException("mrh.release.nonmonotonic");
                    byte[] actualHash=Sha(chain.Concat(number).Concat(signedHash).ToArray());
                    bool matches=true;
                    for(int i=0;i<32;i++)matches &= actualHash[i]==expected[i];
                    if(!matches)throw new InvalidDataException("mrh.release.chain_mismatch");
                    chain=actualHash;revision=next;
                }
                return new LedgerState{Revision=revision,Chain=chain};
            }
        }

        private static byte[] Sha(byte[] data)
        {
            using(SHA256 sha=SHA256.Create())return sha.ComputeHash(data);
        }
    }
}
