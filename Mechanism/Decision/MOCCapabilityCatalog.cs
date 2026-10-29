using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Orchestration
{
    public sealed class MOCCapabilityDescriptor
    {
        public MOCCapabilityDescriptor(string id,string name,string category,
            IEnumerable<MPCPortDescriptor> ports=null,int version=1)
        {
            Id=Normalize(id);Name=Normalize(name);Category=Normalize(category);
            if(version<1)throw new ArgumentOutOfRangeException("version");
            Version=version;
            var items=(ports??Enumerable.Empty<MPCPortDescriptor>()).ToList();
            if(items.Count>256||items.Any(x=>x==null)||
                items.Any(x=>x.MemberId=="Start"||x.MemberId=="Done")||
                items.GroupBy(x=>x.MemberId,StringComparer.Ordinal).Any(x=>x.Count()>1))
                throw new ArgumentException("moc.catalog.invalid_ports");
            Ports=new ReadOnlyCollection<MPCPortDescriptor>(items);
        }
        private static string Normalize(string value)
        {
            if(string.IsNullOrWhiteSpace(value))throw new ArgumentException("moc.catalog.empty");
            value=value.Trim().Normalize();
            if(value.Length>128||value.Any(char.IsControl))
                throw new ArgumentException("moc.catalog.invalid_text");
            return value;
        }
        public string Id{get;private set;}
        public string Name{get;private set;}
        public string Category{get;private set;}
        public int Version{get;private set;}
        public IReadOnlyList<MPCPortDescriptor> Ports{get;private set;}
    }
    /// <summary>只读动态发现；执行权仍归现有 IMRKCapabilityResolver。</summary>
    public interface IMOCCapabilityProvider
    {
        IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities();
    }
    public sealed class MOCCapabilityCatalogItem
    {
        internal MOCCapabilityCatalogItem(MOCCapabilityDescriptor item,bool ready)
        {Definition=item;RuntimeAvailable=ready;AvailabilityKnown=true;}
        internal MOCCapabilityCatalogItem(MOCCapabilityDescriptor item)
        {Definition=item;RuntimeAvailable=false;AvailabilityKnown=false;}
        public MOCCapabilityDescriptor Definition{get;private set;}
        public bool RuntimeAvailable{get;private set;}
        /// <summary>离线描述没有查询Resolver，不可把false解释成运行不可用。</summary>
        public bool AvailabilityKnown{get;private set;}
    }
    /// <summary>
    /// MFM descriptors and existing MOC catalog are cross-indexed on demand.
    /// This is a read-only view, NOT a second module registry, runtime state or authorization.
    /// Missing catalog definitions remain visible instead of being silently treated as supported.
    /// </summary>
    public sealed class MOCModuleCapabilityEntry
    {
        internal MOCModuleCapabilityEntry(MFMModuleDescriptor module,string capabilityId,
            MOCCapabilityCatalogItem catalogItem)
        {
            Module=module;CapabilityId=capabilityId;CatalogItem=catalogItem;
        }
        public MFMModuleDescriptor Module {get;private set;}
        public string CapabilityId {get;private set;}
        public MOCCapabilityCatalogItem CatalogItem {get;private set;}
        public bool Declared {get{return true;}}
        public bool Described {get{return CatalogItem!=null;}}
        /// <summary>Resolver matched; NOT device Ready, Supported or Authorized.</summary>
        public bool RuntimeResolved {get{return CatalogItem!=null&&CatalogItem.RuntimeAvailable;}}
    }
    public sealed partial class MOCCapabilityCatalog
    {
        private readonly IReadOnlyList<MOCCapabilityCatalogItem> _all;
        private readonly IDictionary<string,MOCCapabilityCatalogItem> _ids;
        /// <summary>无需MRK实例的离线只读能力发现。此构造不会解析或执行Provider。</summary>
        public MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider> providers)
            : this(providers,(Func<string,bool>)null) { }

        private MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider> providers,
            Func<string,bool> availability)
        {
            if(providers==null)throw new ArgumentNullException("providers");
            var map=new Dictionary<string,MOCCapabilityCatalogItem>(StringComparer.Ordinal);
            foreach(var provider in providers)
            {
                if(provider==null)throw new InvalidOperationException("moc.catalog.null_provider");
                var descriptors=provider.DescribeCapabilities();
                if(descriptors==null)throw new InvalidOperationException("moc.catalog.null_list");
                foreach(var descriptor in descriptors)
                {
                    if(descriptor==null||map.Count>=4096)throw new InvalidOperationException("moc.catalog.invalid_item");
                    if(map.ContainsKey(descriptor.Id))
                        throw new InvalidOperationException("moc.catalog.duplicate:"+descriptor.Id);
                    map.Add(descriptor.Id,availability==null
                        ? new MOCCapabilityCatalogItem(descriptor)
                        : new MOCCapabilityCatalogItem(descriptor,availability(descriptor.Id)));
                }
            }
            _ids=new ReadOnlyDictionary<string,MOCCapabilityCatalogItem>(map);
            _all=new ReadOnlyCollection<MOCCapabilityCatalogItem>(
                map.Values.OrderBy(x=>x.Definition.Category,StringComparer.Ordinal)
                .ThenBy(x=>x.Definition.Name,StringComparer.Ordinal).ToList());
        }
        public IReadOnlyList<MOCCapabilityCatalogItem> All{get{return _all;}}
        public MOCCapabilityCatalogItem Find(string id)
        {MOCCapabilityCatalogItem item;return id!=null&&_ids.TryGetValue(id,out item)?item:null;}
        /// <summary>
        /// Join the given real MFM module identities to this MOC capability directory.
        /// The supplied modules remain owned by their original host/engineering source.
        /// Multiple module instances may declare the same capability; duplicate ModuleId is rejected.
        /// </summary>
        public IReadOnlyList<MOCModuleCapabilityEntry> DescribeModules(
            IEnumerable<MFMModuleDescriptor> modules)
        {
            if(modules==null)throw new ArgumentNullException("modules");
            var unique=new HashSet<string>(StringComparer.Ordinal);
            var results=new List<MOCModuleCapabilityEntry>();
            foreach(MFMModuleDescriptor module in modules)
            {
                if(module==null)throw new ArgumentException("moc.catalog.null_module","modules");
                if(!unique.Add(module.Id))
                    throw new InvalidOperationException("moc.catalog.duplicate_module:"+module.Id);
                if(unique.Count>4096)
                    throw new InvalidOperationException("moc.catalog.module_capacity");
                foreach(string id in module.CapabilityIds)
                {
                    MOCCapabilityCatalogItem entry;
                    _ids.TryGetValue(id,out entry);
                    results.Add(new MOCModuleCapabilityEntry(module,id,entry));
                    if(results.Count>65536)
                        throw new InvalidOperationException("moc.catalog.module_capability_capacity");
                }
            }
            return new ReadOnlyCollection<MOCModuleCapabilityEntry>(results
                .OrderBy(x=>x.Module.Kind).ThenBy(x=>x.Module.Id,StringComparer.Ordinal)
                .ThenBy(x=>x.CapabilityId,StringComparer.Ordinal).ToList());
        }

        public IReadOnlyList<MOCCapabilityCatalogItem> Search(string query,int limit=50)
        {
            if(limit<1||limit>4096)throw new ArgumentOutOfRangeException("limit");
            string q=(query??"").Trim();
            return new ReadOnlyCollection<MOCCapabilityCatalogItem>(_all.Where(x=>
                q.Length==0||x.Definition.Id.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||
                x.Definition.Name.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||
                x.Definition.Category.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)
                .Take(limit).ToList());
        }
    }
}
