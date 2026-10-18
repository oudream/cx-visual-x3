using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMFileProjectStore
    {
        private static void WriteAssetManifest(XmlWriter writer,MEMProject project)
        {
            if(project.AssetItems==null)return;
            foreach(var asset in project.AssetItems.Values.OrderBy(a=>a.Id))
            {writer.WriteStartElement("Asset");writer.WriteAttributeString("id",asset.Id.ToString("D"));writer.WriteAttributeString("name",asset.Name);writer.WriteAttributeString("media",asset.MediaType);writer.WriteAttributeString("hash",asset.ContentHash);writer.WriteAttributeString("length",asset.Length.ToString(Invariant));writer.WriteEndElement();}
        }
        private static void WriteAssetBytes(ZipArchive package,MEMSaveSnapshot snapshot,System.Threading.CancellationToken cancellation)
        {
            foreach(var asset in snapshot.Assets)
                using(var target=package.CreateEntry(asset.ArchivePath,CompressionLevel.Fastest).Open())using(var input=asset.OpenRead())
                {var buffer=new byte[81920];int read;while((read=input.Read(buffer,0,buffer.Length))!=0){cancellation.ThrowIfCancellationRequested();target.Write(buffer,0,read);}}
        }
        private static void ReadAsset(XmlReader reader,MEMProject project,ZipArchive package)
        {
            if(package==null)throw new InvalidDataException("Assets require a project package");var e=ReadSmallElement(reader);XFields(e,"id name media hash length");var id=Guid.Parse(XRequired(e,"id"));
            var entry=package.GetEntry("assets/"+id.ToString("N"));int length=int.Parse(XRequired(e,"length"),Invariant);
            if(entry==null||length<0||length>MEMAssetReference.MaximumBytes||entry.Length!=length)throw new InvalidDataException("Asset missing or invalid length");
            byte[] bytes;using(var stream=entry.Open())bytes=MEMAssetReference.ReadBytes(stream);var asset=new MEMAssetReference(id,XRequired(e,"name"),XRequired(e,"media"),bytes);
            if(asset.ContentHash!=XRequired(e,"hash"))throw new InvalidDataException("Asset hash mismatch");
            if(project.AssetItems==null)project.AssetItems=new Dictionary<Guid,MEMAssetReference>();
            if(project.AssetItems.Count>=4096||project.AssetItems.Values.Any(a=>a.Name==asset.Name)||project.AssetItems.Values.Sum(a=>(long)a.Length)+length>256L*1024*1024)throw new InvalidDataException("Asset manifest budget/identity conflict");
            project.AssetItems.Add(id,asset);
        }
    }
}
