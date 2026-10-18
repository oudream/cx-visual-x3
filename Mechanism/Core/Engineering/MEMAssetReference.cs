using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Manifest metadata plus immutable packaged static bytes. Not a second node/resource tree or runtime data heap.</summary>
    public sealed class MEMAssetReference : IMEMResource
    {
        internal const int MaximumBytes=64*1024*1024;
        private readonly byte[] _bytes;
        internal MEMAssetReference(Guid id,string name,string mediaType,byte[] bytes)
        {
            if(id==Guid.Empty||bytes==null||bytes.Length>MaximumBytes)throw new ArgumentException("Invalid engineering asset");
            Id=id;Name=MEMNode.NormalizeName(name);MediaType=MEMNode.NormalizeName(mediaType);_bytes=bytes;ContentHash=Hash(bytes);
        }
        internal static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        internal static byte[] ReadBytes(Stream stream)
        {
            using(var output=new MemoryStream()){var buffer=new byte[81920];int read;while((read=stream.Read(buffer,0,buffer.Length))!=0){if(output.Length+read>MaximumBytes)throw new InvalidDataException("Asset byte budget exceeded");output.Write(buffer,0,read);}return output.ToArray();}
        }
        internal string ArchivePath {get{return "assets/"+Id.ToString("N");}}
        internal Stream OpenRead(){return new MemoryStream(_bytes,0,_bytes.Length,false,false);}
        public Guid Id {get;private set;} public string Name {get;private set;} public string MediaType {get;private set;} public string ContentHash {get;private set;}
        public int Length {get{return _bytes.Length;}} public string TypeId {get{return "core.asset";}}
        public Guid StorageRootId {get{return Id;}} public MEMResourceScope Scope {get{return MEMResourceScope.Standalone;}}
    }
    public sealed partial class MEMEngineeringSession
    {
        public IReadOnlyList<MEMAssetReference> Assets {get{DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();return Project.AssetItems==null?MPMEmpty<MEMAssetReference>.Items:Project.AssetItems.Values.OrderBy(a=>a.Name,StringComparer.Ordinal).ToList().AsReadOnly();}}}
        internal MEMAssetReference FindAsset(Guid id){MEMAssetReference asset;return Project.AssetItems!=null&&Project.AssetItems.TryGetValue(id,out asset)?asset:null;}
        public Stream OpenAsset(Guid id){DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();var asset=FindAsset(id);if(asset==null)throw new InvalidOperationException("org.asset.missing");return asset.OpenRead();}}
        public MEMAssetReference AddAsset(string name,string file,string mediaType="application/octet-stream")
        {
            DemandAccess(MEMAccessOperation.Edit);
            lock(_gate){DemandOpen();if(_editing||System.Threading.Volatile.Read(ref _collectingTypes)!=0)throw new InvalidOperationException("No IO in edit/provider callback");}
            byte[] bytes;using(var stream=File.OpenRead(file))bytes=MEMAssetReference.ReadBytes(stream);
            var asset=new MEMAssetReference(Guid.NewGuid(),name,mediaType,bytes);return Edit("添加静态资产",e=>e.AddAsset(asset)).RequireValue();
        }
        public void RemoveAsset(Guid id){Edit("移除静态资产",e=>e.RemoveAsset(id)).RequireValue();}
    }
    public sealed partial class MTCEditSession
    {
        internal MEMAssetReference AddAsset(MEMAssetReference asset)
        {
            Demand();if(_session.IdentityExists(asset.Id)||_session.Assets.Any(a=>a.Name==asset.Name))throw new InvalidOperationException("org.asset.duplicate");
            if(_session.Assets.Count>=4096||_session.Assets.Sum(a=>(long)a.Length)+asset.Length>256L*1024*1024)throw new InvalidOperationException("org.asset.package_budget");
            var project=_session.Project;Change(()=>{if(project.AssetItems==null)project.AssetItems=new Dictionary<Guid,MEMAssetReference>();project.AssetItems.Add(asset.Id,asset);},()=>project.AssetItems.Remove(asset.Id),MTCChangeKind.Asset,asset.Id);return asset;
        }
        public void RemoveAsset(Guid id)
        {
            Demand();var asset=_session.FindAsset(id);if(asset==null)throw new InvalidOperationException("org.asset.missing");if(_session.HasDeclarationUse(id))throw new InvalidOperationException("org.asset.in_use");
            Change(()=>_session.Project.AssetItems.Remove(id),()=>_session.Project.AssetItems.Add(id,asset),MTCChangeKind.Asset,id);
        }
    }
}
