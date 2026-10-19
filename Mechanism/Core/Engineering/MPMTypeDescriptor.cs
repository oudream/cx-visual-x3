using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CxVisual.Mechanism.Core
{
    /// <summary>Provider returns data only. Session enumerates it outside its edit gate; no CLR factories are persisted.</summary>
    public interface IMPMTypeProvider { IEnumerable<MPMTypeDescriptor> DescribeTypes(); }
    public sealed class MPCPortDescriptor
    {
        public MPCPortDescriptor(string memberId,MPCPortDirection direction,string typeId,MPCPortKind kind=MPCPortKind.Data,
            MPCPortMultiplicity multiplicity=MPCPortMultiplicity.Many,MTUContractReference contract=null,bool isPublic=false,bool required=false)
        {
            var check=new MPCPort(Guid.NewGuid(),Guid.NewGuid(),memberId,direction,typeId,kind,multiplicity,contract,isPublic,required);
            MemberId=check.MemberId;Direction=direction;TypeId=check.TypeId;Kind=kind;Multiplicity=multiplicity;Contract=check.Contract;IsPublic=isPublic;Required=required;
            if(Contract.Calibration!=null)throw new ArgumentException("Type defaults cannot capture project object addresses");
        }
        public string MemberId {get;private set;} public MPCPortDirection Direction {get;private set;} public string TypeId {get;private set;}
        public MPCPortKind Kind {get;private set;} public MPCPortMultiplicity Multiplicity {get;private set;}
        public MTUContractReference Contract {get;private set;} public bool IsPublic {get;private set;} public bool Required {get;private set;}
    }
    /// <summary>Frozen semantic schema over the approved Content/Symbol carriers. No parallel object hierarchy.</summary>
    public sealed class MPMTypeDescriptor
    {
        public MPMTypeDescriptor(string typeId,int schemaVersion,MEMNodeKind kind,IEnumerable<MPMPropertyDescriptor> properties=null,
            IEnumerable<MPCPortDescriptor> ports=null,IEnumerable<MPMAspect> aspects=null)
        {
            TypeId=MEMNode.NormalizeName(typeId);if(TypeId.StartsWith("core.",StringComparison.Ordinal)||schemaVersion<1||(kind!=MEMNodeKind.Content&&kind!=MEMNodeKind.Symbol&&kind!=MEMNodeKind.Element))throw new ArgumentException("Invalid custom type schema");
            SchemaVersion=schemaVersion;Kind=kind;
            Properties=Freeze(properties,p=>p.MemberId);Ports=Freeze(ports,p=>p.MemberId);Aspects=Freeze(aspects,a=>a.Kind+":"+a.ProviderId);
            foreach(var p in Properties)if(p.DefaultBinding!=null&&(p.DefaultBinding.Kind==MPMBindingKind.Member||p.DefaultBinding.Kind==MPMBindingKind.Resource))throw new ArgumentException("Type defaults cannot capture project identities");
        }
        private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source,Func<T,string> key) where T:class
        {
            var items=new List<T>();var keys=new HashSet<string>(StringComparer.Ordinal);
            if(source!=null)foreach(var value in source){if(value==null||!keys.Add(key(value)))throw new ArgumentException("Null or duplicate type member");if(items.Count>=4096)throw new ArgumentException("Type member budget exceeded");items.Add(value);}return items.AsReadOnly();
        }
        public string TypeId {get;private set;} public int SchemaVersion {get;private set;} public MEMNodeKind Kind {get;private set;}
        public IReadOnlyList<MPMPropertyDescriptor> Properties {get;private set;} public IReadOnlyList<MPCPortDescriptor> Ports {get;private set;} public IReadOnlyList<MPMAspect> Aspects {get;private set;}
        internal static bool SameSchema(MPMTypeDescriptor a,MPMTypeDescriptor b)
        {
            if(a.TypeId!=b.TypeId||a.SchemaVersion!=b.SchemaVersion||a.Kind!=b.Kind||a.Properties.Count!=b.Properties.Count||a.Ports.Count!=b.Ports.Count||a.Aspects.Count!=b.Aspects.Count)return false;
            for(int i=0;i<a.Properties.Count;i++){var x=a.Properties[i];var y=b.Properties[i];if(x.MemberId!=y.MemberId||x.ValueKind!=y.ValueKind||x.Required!=y.Required||x.IsPublic!=y.IsPublic||x.Minimum!=y.Minimum||x.Maximum!=y.Maximum||!object.Equals(x.DefaultBinding,y.DefaultBinding))return false;}
            for(int i=0;i<a.Ports.Count;i++){var x=a.Ports[i];var y=b.Ports[i];if(x.MemberId!=y.MemberId||x.Direction!=y.Direction||x.Kind!=y.Kind||x.TypeId!=y.TypeId||x.Multiplicity!=y.Multiplicity||x.IsPublic!=y.IsPublic||x.Required!=y.Required||!x.Contract.Equals(y.Contract))return false;}
            for(int i=0;i<a.Aspects.Count;i++){var x=a.Aspects[i];var y=b.Aspects[i];if(x.Kind!=y.Kind||x.ProviderId!=y.ProviderId||x.SchemaVersion!=y.SchemaVersion||x.Required!=y.Required||x.RequiresRemap!=y.RequiresRemap||!x.PayloadBytes.SequenceEqual(y.PayloadBytes))return false;}return true;
        }
    }
    public sealed partial class MEMEngineeringSession
    {
        private int _collectingTypes;
        public MPMTypeDescriptor FindType(string typeId){DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();MPMTypeDescriptor value;return Project.TypeItems!=null&&Project.TypeItems.TryGetValue(typeId,out value)?value:null;}}
        public IReadOnlyList<MPMTypeDescriptor> RegisteredTypes {get{DemandAccess(MEMAccessOperation.Read);lock(_gate){DemandOpen();return Project.TypeItems==null?MPMEmpty<MPMTypeDescriptor>.Items:Project.TypeItems.Values.OrderBy(t=>t.TypeId,StringComparer.Ordinal).ToList().AsReadOnly();}}}
        public void RegisterTypes(IMPMTypeProvider provider)
        {
            DemandAccess(MEMAccessOperation.Edit);
            if(provider==null)throw new ArgumentNullException("provider");int revision;
            lock(_gate){DemandOpen();if(_editing||_callbackThread==Thread.CurrentThread.ManagedThreadId||Interlocked.CompareExchange(ref _collectingTypes,1,0)!=0)throw new InvalidOperationException("org.type.registration_reentrant");revision=Revision;}
            var descriptors=new List<MPMTypeDescriptor>();
            try
            {
                var supplied=provider.DescribeTypes();if(supplied==null)throw new InvalidOperationException("org.type.provider_returned_null");
                foreach(var descriptor in supplied){if(descriptor==null||descriptors.Count>=4096)throw new InvalidOperationException("org.type.provider_budget");descriptors.Add(descriptor);}
            }
            finally{Interlocked.Exchange(ref _collectingTypes,0);}
            Edit("注册工程类型",revision,e=>{foreach(var descriptor in descriptors)e.RegisterType(descriptor);return true;}).RequireValue();
        }
        public MEMContent AddTypedContent(MEMNode parent,string name,string typeId){return Edit("创建业务内容",e=>e.AddTypedContent(parent,name,typeId)).RequireValue();}
        public MEMSymbol AddTypedSymbol(string name,string typeId){return Edit("创建组件定义",e=>e.AddTypedSymbol(name,typeId)).RequireValue();}
        public MRCWidgetElement AddTypedElement(MEMNode parent,string name,string typeId){return Edit("创建控件图元",e=>e.AddTypedElement(parent,name,typeId)).RequireValue();}
        public void RemoveProperty(MEMNode owner,string member){Edit("删除属性",e=>e.RemoveProperty(owner,member)).RequireValue();}
    }
    public sealed partial class MTCEditSession
    {
        internal void RegisterType(MPMTypeDescriptor descriptor)
        {
            Demand();if(_session.FindType(descriptor.TypeId)!=null)throw new InvalidOperationException("org.type.duplicate:"+descriptor.TypeId);
            if(_session.RegisteredTypes.Count>=4096)throw new InvalidOperationException("org.type.registry_budget");var project=_session.Project;
            Change(()=>{if(project.TypeItems==null)project.TypeItems=new Dictionary<string,MPMTypeDescriptor>(StringComparer.Ordinal);project.TypeItems.Add(descriptor.TypeId,descriptor);},()=>project.TypeItems.Remove(descriptor.TypeId),MTCChangeKind.TypeRegistration,project.Id);
        }
        private MEMNode AddTyped(MEMNode parent,string name,string typeId,MEMNodeKind kind)
        {
            Demand();var descriptor=_session.FindType(typeId);if(descriptor==null||descriptor.Kind!=kind)throw new InvalidOperationException("org.type.missing_or_wrong_kind");
            MEMNode node=kind==MEMNodeKind.Content?(MEMNode)new MEMContent(Guid.NewGuid(),name):kind==MEMNodeKind.Symbol?(MEMNode)new MEMSymbol(Guid.NewGuid(),name):new MRCWidgetElement(Guid.NewGuid(),name,typeId,0,0);node.SetTypeId(typeId);Add(parent,node);
            foreach(var property in descriptor.Properties)DefineProperty(node,property);
            foreach(var port in descriptor.Ports)DefinePort(node,port.MemberId,port.Direction,port.TypeId,port.Kind,port.Multiplicity,port.Contract,port.IsPublic,port.Required);
            foreach(var aspect in descriptor.Aspects)SetAspect(node,aspect);return node;
        }
        public MEMContent AddTypedContent(MEMNode parent,string name,string typeId){return (MEMContent)AddTyped(parent,name,typeId,MEMNodeKind.Content);}
        public MEMSymbol AddTypedSymbol(string name,string typeId){return (MEMSymbol)AddTyped(_session.Project,name,typeId,MEMNodeKind.Symbol);}
        public MRCWidgetElement AddTypedElement(MEMNode parent,string name,string typeId){return (MRCWidgetElement)AddTyped(parent,name,typeId,MEMNodeKind.Element);}
    }
}
