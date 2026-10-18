using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Core
{
    public interface IMPMDefinition
    {
        Guid Id {get;}
        IReadOnlyList<MPMProperty> Properties {get;}
        IReadOnlyList<MPCPort> Ports {get;}
        IReadOnlyList<MPCConnection> Connections {get;}
        IReadOnlyList<MRCPublicMapping> PublicMappings {get;}
        IReadOnlyList<MPMAspect> Aspects {get;}
    }
    public enum MPMAspectKind {Visual=1,Logic=2,Data=3,Collaboration=4}
    public sealed class MPMAspect
    {
        private readonly byte[] _payload;
        public MPMAspect(MPMAspectKind kind,string providerId,int schemaVersion,byte[] payload,bool required=true,bool requiresRemap=false)
        {if(!Enum.IsDefined(typeof(MPMAspectKind),kind)||schemaVersion<1)throw new ArgumentException("Invalid aspect contract");if(payload!=null&&payload.Length>4*1024*1024)throw new ArgumentException("Aspect budget exceeded");Kind=kind;ProviderId=MEMNode.NormalizeName(providerId);SchemaVersion=schemaVersion;_payload=(payload??new byte[0]).ToArray();Required=required;RequiresRemap=requiresRemap;}
        public MPMAspectKind Kind {get;private set;} public string ProviderId {get;private set;}
        public int SchemaVersion {get;private set;} public bool Required {get;private set;}
        public bool RequiresRemap {get;private set;}
        public byte[] Payload {get{return (byte[])_payload.Clone();}}
        internal byte[] PayloadBytes {get{return _payload;}}
    }
    internal static class MPMEmpty<T> {internal static readonly IReadOnlyList<T> Items=Array.AsReadOnly(new T[0]);}
    /// <summary>Optional declaration storage belongs to capable concrete owners, never MEMNode; each list is also lazy.</summary>
    internal sealed class MPMDefinitionState
    {
        internal List<MPMProperty> PropertyItems;
        internal List<MPCPort> PortItems;
        internal List<MPCConnection> ConnectionItems;
        internal List<MRCPublicMapping> MappingItems;
        internal List<MPMAspect> AspectItems;
        internal bool HasData {get{return (PropertyItems!=null&&PropertyItems.Count!=0)||(PortItems!=null&&PortItems.Count!=0)||(ConnectionItems!=null&&ConnectionItems.Count!=0)||(MappingItems!=null&&MappingItems.Count!=0)||(AspectItems!=null&&AspectItems.Count!=0);}}
        internal static MPMDefinitionState For(MEMNode node,bool create=false)
        {
            var content=node as MEMContent;if(content!=null){if(create&&content.DefinitionState==null)content.DefinitionState=new MPMDefinitionState();return content.DefinitionState;}
            var symbol=node as MEMSymbol;if(symbol!=null){if(create&&symbol.DefinitionState==null)symbol.DefinitionState=new MPMDefinitionState();return symbol.DefinitionState;}
            var visual=node as MRCVisualItem;if(visual!=null){if(create&&visual.DefinitionState==null)visual.DefinitionState=new MPMDefinitionState();return visual.DefinitionState;}
            if(create)throw new InvalidOperationException("org.definition.owner_unsupported");return null;
        }
        internal static IReadOnlyList<MPMProperty> PropertiesOf(MEMNode node){var s=For(node);return s==null||s.PropertyItems==null?MPMEmpty<MPMProperty>.Items:s.PropertyItems.AsReadOnly();}
        internal static IReadOnlyList<MPCPort> PortsOf(MEMNode node){var s=For(node);return s==null||s.PortItems==null?MPMEmpty<MPCPort>.Items:s.PortItems.AsReadOnly();}
        internal static IReadOnlyList<MPCConnection> ConnectionsOf(MEMNode node){var s=For(node);return s==null||s.ConnectionItems==null?MPMEmpty<MPCConnection>.Items:s.ConnectionItems.AsReadOnly();}
        internal static IReadOnlyList<MRCPublicMapping> MappingsOf(MEMNode node){var s=For(node);return s==null||s.MappingItems==null?MPMEmpty<MRCPublicMapping>.Items:s.MappingItems.AsReadOnly();}
        internal static IReadOnlyList<MPMAspect> AspectsOf(MEMNode node){var s=For(node);return s==null||s.AspectItems==null?MPMEmpty<MPMAspect>.Items:s.AspectItems.AsReadOnly();}
    }
}
