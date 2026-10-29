using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Decision
{
    /// <summary>Versioned, bounded, deterministic transport of a frozen Decision Plan.
    /// SHA256 detects accidental mutation; it is NOT a publisher signature.
    /// Host trust, anti-rollback, authorization and Stage/Activate remain external.</summary>
    public static class MDCPlanArchiveCodec
    {
        private const int Magic = 0x4B504344; // DCPK in little endian
        private const int Version = 1;
        private const int MaxSize = 32 * 1024 * 1024;
        private const int MaxContents = 1024;
        private const int MaxReferences = 4096;
        private const int MaxUses = 131072;
        private static readonly Encoding TextEncoding = new UTF8Encoding(false,true);

        public static byte[] Serialize(MDCDeclarativePlan plan)
        {
            if(plan==null)throw new ArgumentNullException("plan");
            plan.RequireIntegrity();
            var manifest=plan.SourceManifest;
            if(!manifest.HasCompleteStaticMetadata)
                throw new InvalidOperationException("decision.archive.unknown_capabilities");
            byte[] payload;
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,TextEncoding,true))
            {
                writer.Write(Magic);
                writer.Write(Version);
                WriteGuid(writer,plan.ProjectId);
                writer.Write(plan.EngineeringRevision);
                WriteGuid(writer,plan.RootContentId);
                WriteText(writer,plan.CatalogFingerprint,128);
                WriteText(writer,plan.Fingerprint,128);
                writer.Write(manifest.Contents.Count);
                foreach(MDCStaticContentSource source in manifest.Contents)
                {
                    WriteGuid(writer,source.ContentId);
                    writer.Write(source.SchemaVersion);
                    WriteText(writer,source.LogicFingerprint,128);
                    writer.Write(source.ReferencedContentIds.Count);
                    foreach(Guid target in source.ReferencedContentIds)WriteGuid(writer,target);
                    writer.Write(source.CapabilityUses.Count);
                    foreach(MDCStaticCapabilityUse use in source.CapabilityUses)
                    {
                        WriteGuid(writer,use.ContentId);
                        WriteGuid(writer,use.BlockId);
                        WriteText(writer,use.CapabilityId,4096);
                    }
                }
                writer.Write(plan.Contents.Count);
                foreach(MDCPlanContent content in plan.Contents)
                {
                    WriteGuid(writer,content.ContentId);
                    WriteGuid(writer,content.StorageRootId);
                    writer.Write(content.LogicRequired);
                    writer.Write(content.SchemaVersion);
                    writer.Write(content.IsWiring);
                    writer.Write(content.References.Count);
                    foreach(MDCPlanReference reference in content.References)
                    {
                        WriteGuid(writer,reference.Id);
                        WriteGuid(writer,reference.TargetId);
                        writer.Write((int)reference.Kind);
                    }
                    byte[] logic=content.CopyCanonicalLogicPayload();
                    if(logic.Length==0 || logic.Length>4*1024*1024)
                        throw new InvalidDataException("decision.archive.logic_budget");
                    writer.Write(logic.Length);
                    writer.Write(logic);
                }
                writer.Flush();
                if(stream.Length>MaxSize-32)
                    throw new InvalidDataException("decision.archive.size_budget");
                payload=stream.ToArray();
            }
            using(SHA256 sha=SHA256.Create())
            {
                byte[] hash=sha.ComputeHash(payload);
                var result=new byte[payload.Length+hash.Length];
                Buffer.BlockCopy(payload,0,result,0,payload.Length);
                Buffer.BlockCopy(hash,0,result,payload.Length,hash.Length);
                return result;
            }
        }

        /// <summary>Restores a real MDCDeclarativePlan without opening any Core
        /// project or acquiring any professional/runtime instance.</summary>
        public static MDCDeclarativePlan Deserialize(byte[] archive)
        {
            if(archive==null)throw new ArgumentNullException("archive");
            if(archive.Length<80 || archive.Length>MaxSize)
                throw new InvalidDataException("decision.archive.size_budget");
            int bodyLength=archive.Length-32;
            using(SHA256 sha=SHA256.Create())
            {
                byte[] digest=sha.ComputeHash(archive,0,bodyLength);
                bool matches=true;
                for(int i=0;i<digest.Length;i++)
                    matches &= digest[i]==archive[bodyLength+i];
                if(!matches)throw new InvalidDataException("decision.archive.digest_mismatch");
            }
            using(var stream=new MemoryStream(archive,0,bodyLength,false))
            using(var reader=new BinaryReader(stream,TextEncoding,true))
            {
                if(reader.ReadInt32()!=Magic || reader.ReadInt32()!=Version)
                    throw new InvalidDataException("decision.archive.unsupported_version");
                Guid projectId=ReadGuid(reader);
                int revision=reader.ReadInt32();
                Guid root=ReadGuid(reader);
                string catalogFingerprint=ReadText(reader,128);
                string fingerprint=ReadText(reader,128);
                int sourceCount=ReadCount(reader,MaxContents);
                if(sourceCount==0 || projectId==Guid.Empty || root==Guid.Empty || revision<0)
                    throw new InvalidDataException("decision.archive.invalid_identity");
                var sources=new List<MDCStaticContentSource>(sourceCount);
                for(int i=0;i<sourceCount;i++)
                {
                    Guid contentId=ReadGuid(reader);
                    int schema=reader.ReadInt32();
                    string hash=ReadText(reader,128);
                    int refCount=ReadCount(reader,MaxReferences);
                    var refs=new List<Guid>(refCount);
                    for(int j=0;j<refCount;j++)refs.Add(ReadGuid(reader));
                    int useCount=ReadCount(reader,MaxUses);
                    var uses=new List<MDCStaticCapabilityUse>(useCount);
                    for(int j=0;j<useCount;j++)
                        uses.Add(new MDCStaticCapabilityUse(ReadGuid(reader),
                            ReadGuid(reader),ReadText(reader,4096)));
                    sources.Add(new MDCStaticContentSource(contentId,schema,hash,refs,uses));
                }
                int contentCount=ReadCount(reader,MaxContents);
                if(contentCount!=sourceCount)
                    throw new InvalidDataException("decision.archive.closure_count");
                var contents=new List<MDCPlanContent>(contentCount);
                for(int i=0;i<contentCount;i++)
                {
                    Guid contentId=ReadGuid(reader);
                    Guid storageRoot=ReadGuid(reader);
                    bool required=reader.ReadBoolean();
                    int schema=reader.ReadInt32();
                    bool isWiring=reader.ReadBoolean();
                    int refCount=ReadCount(reader,MaxReferences);
                    var refs=new List<MDCPlanReference>(refCount);
                    for(int j=0;j<refCount;j++)
                    {
                        Guid id=ReadGuid(reader),target=ReadGuid(reader);
                        int kind=reader.ReadInt32();
                        if(!Enum.IsDefined(typeof(MRIReferenceKind),kind))
                            throw new InvalidDataException("decision.archive.reference_kind");
                        refs.Add(new MDCPlanReference(id,target,(MRIReferenceKind)kind));
                    }
                    int length=reader.ReadInt32();
                    if(length<1 || length>4*1024*1024 ||
                        length>stream.Length-stream.Position)
                        throw new InvalidDataException("decision.archive.logic_budget");
                    byte[] logic=reader.ReadBytes(length);
                    bool declaredWiring=schema==MOLWiringCodec.SchemaVersion ||
                        schema==MOLWiringCodec.ExtendedSchemaVersion ||
                        schema==MOLWiringCodec.CompoundSchemaVersion;
                    if(declaredWiring!=isWiring)
                        throw new InvalidDataException("decision.archive.schema_mismatch");
                    contents.Add(MDCDeclarativePlanCompiler.CreateFromCanonical(
                        contentId,storageRoot,refs,required,schema,logic));
                }
                if(stream.Position!=stream.Length)
                    throw new InvalidDataException("decision.archive.trailing_data");
                string computed=MDCStaticSourceManifestBuilder.ComputeFingerprint(
                    projectId,revision,root,catalogFingerprint,sources);
                if(!StringComparer.Ordinal.Equals(computed,fingerprint))
                    throw new InvalidDataException("decision.archive.manifest_mismatch");
                var manifest=new MDCStaticSourceManifest(projectId,revision,root,
                    catalogFingerprint,fingerprint,sources,new string[0]);
                var plan=new MDCDeclarativePlan(manifest,contents);
                plan.RequireIntegrity();
                return plan;
            }
        }

        private static int ReadCount(BinaryReader reader,int max)
        {
            int count=reader.ReadInt32();
            if(count<0 || count>max)
                throw new InvalidDataException("decision.archive.count_budget");
            return count;
        }
        private static void WriteGuid(BinaryWriter writer,Guid value)
        {writer.Write(value.ToByteArray());}
        private static Guid ReadGuid(BinaryReader reader)
        {
            byte[] bytes=reader.ReadBytes(16);
            if(bytes.Length!=16)throw new EndOfStreamException("decision.archive.guid");
            return new Guid(bytes);
        }
        private static void WriteText(BinaryWriter writer,string text,int max)
        {
            byte[] bytes=TextEncoding.GetBytes(text??"");
            if(bytes.Length>max)throw new InvalidDataException("decision.archive.text_budget");
            writer.Write(bytes.Length);writer.Write(bytes);
        }
        private static string ReadText(BinaryReader reader,int max)
        {
            int count=ReadCount(reader,max);
            byte[] bytes=reader.ReadBytes(count);
            if(bytes.Length!=count)throw new EndOfStreamException("decision.archive.text");
            return TextEncoding.GetString(bytes);
        }
    }
}
