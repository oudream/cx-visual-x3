using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Execution;

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
        {Definition=item;RuntimeAvailable=ready;}
        public MOCCapabilityDescriptor Definition{get;private set;}
        public bool RuntimeAvailable{get;private set;}
    }
    public sealed class MOCCapabilityCatalog
    {
        private readonly IReadOnlyList<MOCCapabilityCatalogItem> _all;
        private readonly IDictionary<string,MOCCapabilityCatalogItem> _ids;
        public MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider> providers,
            IMRKCapabilityResolver runtime)
        {
            if(providers==null||runtime==null)throw new ArgumentNullException("providers/runtime");
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
                    MRKResolvedCapability action;
                    bool available=runtime.TryResolve(descriptor.Id,out action)&&action!=null;
                    map.Add(descriptor.Id,new MOCCapabilityCatalogItem(descriptor,available));
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
