using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMFileProjectStore
    {
        private static readonly Encoding StrictUtf8=new UTF8Encoding(false,true);
        private static string Encode(string value){return Convert.ToBase64String(StrictUtf8.GetBytes(value));}
        private static string Decode(string value){return StrictUtf8.GetString(Convert.FromBase64String(value));}
        private static XElement ReadSmallElement(XmlReader reader){using(var subtree=reader.ReadSubtree())return XElement.Load(subtree,LoadOptions.PreserveWhitespace);}
        private static string XRequired(XElement element,string name){var value=element.Attribute(name);if(value==null)throw new InvalidDataException("Missing declaration attribute "+name);return value.Value;}
        private static string XOptional(XElement element,string name,string fallback=""){var value=element.Attribute(name);return value==null?fallback:value.Value;}
        private static bool XBool(XElement element,string name,bool fallback=false){var value=element.Attribute(name);return value==null?fallback:XmlConvert.ToBoolean(value.Value);}
        private static T XEnum<T>(XElement element,string name) where T:struct
        {T value;if(!Enum.TryParse(XRequired(element,name),out value)||!Enum.IsDefined(typeof(T),value))throw new InvalidDataException("Invalid "+name);return value;}
        private static void XFields(XElement element,string fields,bool children=false)
        {
            var allowed=new HashSet<string>(fields.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries),StringComparer.Ordinal);
            if(element.Name.NamespaceName!=""||element.Attributes().Any(a=>a.Name.NamespaceName!=""||!allowed.Contains(a.Name.LocalName)))throw new InvalidDataException("Unsupported declaration attribute");
            if(!children&&element.Elements().Any())throw new InvalidDataException("Unexpected declaration child");
            if(element.Name.LocalName!="Aspect"&&element.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)))throw new InvalidDataException("Unexpected declaration text");
        }
        private static void WriteBinding(XmlWriter w,MPMBinding binding)
        {
            w.WriteAttributeString("kind",binding.Kind.ToString());
            switch(binding.Kind)
            {
                case MPMBindingKind.Literal:w.WriteAttributeString("valueKind",binding.Value.Kind.ToString());w.WriteAttributeString("value64",Encode(binding.Value.Canonical));break;
                case MPMBindingKind.Resource:w.WriteAttributeString("resource",binding.ResourceId.Value.ToString("D"));break;
                case MPMBindingKind.Member:w.WriteAttributeString("address",binding.Address.ToString());w.WriteAttributeString("member64",Encode(binding.MemberId));break;
                case MPMBindingKind.Configuration:
                    var c=binding.Configuration;w.WriteAttributeString("section64",Encode(c.Target.SectionId));w.WriteAttributeString("scopeKind64",Encode(c.Target.ScopeKind));w.WriteAttributeString("scopeId64",Encode(c.Target.ScopeId));w.WriteAttributeString("path64",Encode(c.Path));w.WriteAttributeString("optional",XmlConvert.ToString(c.Optional));break;
                case MPMBindingKind.Expression:w.WriteAttributeString("source64",Encode(binding.SourceId));break;
                default:w.WriteAttributeString("source64",Encode(binding.SourceId));w.WriteAttributeString("member64",Encode(binding.MemberId));break;
            }
        }
        private static MPMBinding ReadBinding(XElement element,bool hasProperty=false)
        {
            MPMBindingKind kind=element.Attribute("kind")==null?MPMBindingKind.Legacy:XEnum<MPMBindingKind>(element,"kind");
            string fields;
            switch(kind){case MPMBindingKind.Literal:fields="valueKind value64";break;case MPMBindingKind.Resource:fields="resource";break;case MPMBindingKind.Member:fields="address member member64";break;case MPMBindingKind.Configuration:fields="section64 scopeKind64 scopeId64 path64 optional";break;case MPMBindingKind.Expression:fields="source source64";break;default:fields="source source64 member member64";break;}
            XFields(element,"kind "+fields+(hasProperty?" property":""));
            foreach(var field in new[]{"source","member"})if(element.Attribute(field)!=null&&element.Attribute(field+"64")!=null)throw new InvalidDataException("Ambiguous binding field");
            Func<string,string,string> text=(name,fallback)=>element.Attribute(name+"64")!=null?Decode(XRequired(element,name+"64")):XOptional(element,name,fallback);
            switch(kind)
            {
                case MPMBindingKind.Literal:return MPMBinding.Literal(MPMValue.Parse(XEnum<MPMValueKind>(element,"valueKind"),Decode(XRequired(element,"value64"))));
                case MPMBindingKind.Resource:return MPMBinding.Resource(Guid.Parse(XRequired(element,"resource")));
                case MPMBindingKind.Member:return MPMBinding.Member(MRIObjectAddress.Parse(XRequired(element,"address")),text("member",""));
                case MPMBindingKind.Configuration:return MPMBinding.Config(new FCMConfigReference(new FCMConfigIdentity(Decode(XRequired(element,"section64")),Decode(XRequired(element,"scopeKind64")),Decode(XRequired(element,"scopeId64"))),Decode(XRequired(element,"path64")),XBool(element,"optional")));
                case MPMBindingKind.Variable:return MPMBinding.Variable(text("source",""),text("member","value"));
                case MPMBindingKind.Expression:return MPMBinding.Expression(text("source",""));
                default:return new MPMBinding(text("source",""),text("member",""));
            }
        }
        private static void WriteDeclarations(XmlWriter w,MEMNode owner)
        {
            var state=MPMDefinitionState.For(owner);if(state==null)return;
            foreach(var property in MPMDefinitionState.PropertiesOf(owner))
            {
                var d=property.Descriptor;w.WriteStartElement("Property");w.WriteAttributeString("member",d.MemberId);w.WriteAttributeString("valueKind",d.ValueKind.ToString());w.WriteAttributeString("required",XmlConvert.ToString(d.Required));w.WriteAttributeString("public",XmlConvert.ToString(d.IsPublic));
                if(d.Minimum.HasValue)Number(w,"min",d.Minimum.Value);if(d.Maximum.HasValue)Number(w,"max",d.Maximum.Value);
                if(d.DefaultBinding!=null){w.WriteStartElement("Default");WriteBinding(w,d.DefaultBinding);w.WriteEndElement();}
                if(property.ExplicitBinding!=null){w.WriteStartElement("Explicit");WriteBinding(w,property.ExplicitBinding);w.WriteEndElement();}w.WriteEndElement();
            }
            foreach(var port in MPMDefinitionState.PortsOf(owner))
            {
                w.WriteStartElement("Port");w.WriteAttributeString("id",port.Id.ToString("D"));w.WriteAttributeString("member",port.MemberId);w.WriteAttributeString("direction",port.Direction.ToString());w.WriteAttributeString("typeId",port.TypeId);w.WriteAttributeString("kind",port.Kind.ToString());w.WriteAttributeString("multiplicity",port.Multiplicity.ToString());w.WriteAttributeString("public",XmlConvert.ToString(port.IsPublic));w.WriteAttributeString("required",XmlConvert.ToString(port.Required));
                w.WriteAttributeString("unit64",Encode(port.Contract.UnitId));w.WriteAttributeString("frame64",Encode(port.Contract.CoordinateFrameId));w.WriteAttributeString("time64",Encode(port.Contract.TimeBaseId));if(port.Contract.Calibration!=null)w.WriteAttributeString("calibration",port.Contract.Calibration.ToString());w.WriteEndElement();
            }
            foreach(var connection in MPMDefinitionState.ConnectionsOf(owner))
            {w.WriteStartElement("Connection");w.WriteAttributeString("id",connection.Id.ToString("D"));WriteEndpoint(w,"source",connection.Source);WriteEndpoint(w,"target",connection.Target);w.WriteEndElement();}
            foreach(var mapping in MPMDefinitionState.MappingsOf(owner))
            {
                w.WriteStartElement("Mapping");w.WriteAttributeString("kind",mapping.Kind.ToString());w.WriteAttributeString("member",mapping.MemberId);w.WriteAttributeString("target",mapping.TargetAddress.ToString());
                if(mapping.Kind==MRCPublicMappingKind.Port){w.WriteAttributeString("publicPort",mapping.PublicPortId.Value.ToString("D"));w.WriteAttributeString("targetPort",mapping.TargetPortId.Value.ToString("D"));}else w.WriteAttributeString("targetMember",mapping.TargetMemberId);w.WriteEndElement();
            }
            foreach(var aspect in MPMDefinitionState.AspectsOf(owner))
            {w.WriteStartElement("Aspect");w.WriteAttributeString("kind",aspect.Kind.ToString());w.WriteAttributeString("provider",aspect.ProviderId);w.WriteAttributeString("schema",aspect.SchemaVersion.ToString(Invariant));w.WriteAttributeString("required",XmlConvert.ToString(aspect.Required));if(aspect.RequiresRemap)w.WriteAttributeString("remap","true");w.WriteBase64(aspect.PayloadBytes,0,aspect.PayloadBytes.Length);w.WriteEndElement();}
        }
        private static void WriteEndpoint(XmlWriter w,string prefix,MPCEndpoint endpoint){w.WriteAttributeString(prefix,endpoint.Address.ToString());w.WriteAttributeString(prefix+"Port",endpoint.PortId.ToString("D"));}
        private static MPCEndpoint ReadEndpoint(XElement e,string prefix){return new MPCEndpoint(MRIObjectAddress.Parse(XRequired(e,prefix)),Guid.Parse(XRequired(e,prefix+"Port")));}
        private static bool TryReadDeclaration(XmlReader reader,MEMNode owner,Dictionary<Guid,MPMDefinitionState> states)
        {
            if(!new[]{"Property","Port","Connection","Mapping","Aspect"}.Contains(reader.Name))return false;
            if(!(owner is IMPMDefinition))throw new InvalidDataException("Invalid declaration owner");MPMDefinitionState state;if(!states.TryGetValue(owner.Id,out state)){state=new MPMDefinitionState();states.Add(owner.Id,state);}var e=ReadSmallElement(reader);
            switch(e.Name.LocalName)
            {
                case "Property":
                    XFields(e,"member valueKind required public min max",true);if(e.Elements().Any(x=>x.Name!="Default"&&x.Name!="Explicit")||e.Elements("Default").Count()>1||e.Elements("Explicit").Count()>1)throw new InvalidDataException("Invalid property payload");
                    var def=e.Element("Default");var explicitValue=e.Element("Explicit");
                    var descriptor=new MPMPropertyDescriptor(XRequired(e,"member"),XEnum<MPMValueKind>(e,"valueKind"),XBool(e,"required"),XBool(e,"public",true),def==null?null:ReadBinding(def),e.Attribute("min")==null?(double?)null:double.Parse(XRequired(e,"min"),Invariant),e.Attribute("max")==null?(double?)null:double.Parse(XRequired(e,"max"),Invariant));
                    var property=new MPMProperty(owner.Id,descriptor);property.ExplicitBinding=explicitValue==null?null:ReadBinding(explicitValue);descriptor.Validate(property.ExplicitBinding);
                    if(state.PropertyItems==null)state.PropertyItems=new List<MPMProperty>();if(state.PropertyItems.Any(p=>p.MemberId==property.MemberId))throw new InvalidDataException("Duplicate property");state.PropertyItems.Add(property);break;
                case "Port":
                    XFields(e,"id member direction typeId kind multiplicity public required unit64 frame64 time64 calibration");
                    var contract=new MTUContractReference(Decode(XRequired(e,"unit64")),Decode(XRequired(e,"frame64")),Decode(XRequired(e,"time64")),e.Attribute("calibration")==null?null:MRIObjectAddress.Parse(XRequired(e,"calibration")));
                    var port=new MPCPort(Guid.Parse(XRequired(e,"id")),owner.Id,XRequired(e,"member"),XEnum<MPCPortDirection>(e,"direction"),XRequired(e,"typeId"),XEnum<MPCPortKind>(e,"kind"),XEnum<MPCPortMultiplicity>(e,"multiplicity"),contract,XBool(e,"public"),XBool(e,"required"));
                    if(state.PortItems==null)state.PortItems=new List<MPCPort>();if(state.PortItems.Any(p=>p.MemberId==port.MemberId))throw new InvalidDataException("Duplicate port member");state.PortItems.Add(port);break;
                case "Connection":
                    XFields(e,"id source sourcePort target targetPort");if(state.ConnectionItems==null)state.ConnectionItems=new List<MPCConnection>();state.ConnectionItems.Add(new MPCConnection(Guid.Parse(XRequired(e,"id")),owner.Id,ReadEndpoint(e,"source"),ReadEndpoint(e,"target")));break;
                case "Mapping":
                    var kind=XEnum<MRCPublicMappingKind>(e,"kind");XFields(e,"kind member target "+(kind==MRCPublicMappingKind.Property?"targetMember":"publicPort targetPort"));var address=MRIObjectAddress.Parse(XRequired(e,"target"));
                    var mapping=kind==MRCPublicMappingKind.Property?new MRCPublicMapping(owner.Id,XRequired(e,"member"),address,XRequired(e,"targetMember")):new MRCPublicMapping(owner.Id,XRequired(e,"member"),Guid.Parse(XRequired(e,"publicPort")),new MPCEndpoint(address,Guid.Parse(XRequired(e,"targetPort"))));
                    if(state.MappingItems==null)state.MappingItems=new List<MRCPublicMapping>();if(state.MappingItems.Any(m=>m.Kind==mapping.Kind&&m.MemberId==mapping.MemberId))throw new InvalidDataException("Duplicate public mapping");state.MappingItems.Add(mapping);break;
                case "Aspect":
                    XFields(e,"kind provider schema required remap",true);if(e.Elements().Any()||e.Value.Length>5600000)throw new InvalidDataException("Invalid/oversized aspect payload");var aspect=new MPMAspect(XEnum<MPMAspectKind>(e,"kind"),XRequired(e,"provider"),int.Parse(XRequired(e,"schema"),Invariant),Convert.FromBase64String(e.Value),XBool(e,"required",true),XBool(e,"remap"));
                    if(state.AspectItems==null)state.AspectItems=new List<MPMAspect>();if(state.AspectItems.Any(a=>a.Kind==aspect.Kind&&a.ProviderId==aspect.ProviderId))throw new InvalidDataException("Duplicate aspect");state.AspectItems.Add(aspect);break;
            }
            return true;
        }
        private static void WriteReference(XmlWriter w,MRIResourceReference reference)
        {
            w.WriteStartElement("Reference");w.WriteAttributeString("id",reference.Id.ToString("D"));w.WriteAttributeString("name",reference.Name);w.WriteAttributeString("kind",reference.Kind.ToString());w.WriteAttributeString("target",reference.TargetId.ToString("D"));
            if(reference.PlacementValue!=null){var p=reference.Placement;Number(w,"m11",p.M11);Number(w,"m12",p.M12);Number(w,"m21",p.M21);Number(w,"m22",p.M22);Number(w,"x",p.X);Number(w,"y",p.Y);}
            foreach(var pair in reference.Overrides){w.WriteStartElement("Override");w.WriteAttributeString("property",pair.Key);WriteBinding(w,pair.Value);w.WriteEndElement();}w.WriteEndElement();
        }
        private static MRIResourceReference ReadReference(XmlReader reader,MEMNode owner)
        {
            var e=ReadSmallElement(reader);XFields(e,"id name kind target m11 m12 m21 m22 x y",true);if(e.Elements().Any(x=>x.Name!="Override"))throw new InvalidDataException("Unknown reference payload");
            var id=Guid.Parse(XRequired(e,"id"));if(id==Guid.Empty)throw new InvalidDataException("Empty reference identity");var reference=new MRIResourceReference(id,XRequired(e,"name"),Guid.Parse(XRequired(e,"target")),XEnum<MRIReferenceKind>(e,"kind"),owner);
            var matrixFields=new[]{"m11","m12","m21","m22","x","y"};int matrixCount=matrixFields.Count(n=>e.Attribute(n)!=null);if(matrixCount!=0&&matrixCount!=6)throw new InvalidDataException("Incomplete placement");
            if(matrixCount==6)
            {Func<string,double> number=name=>double.Parse(XRequired(e,name),Invariant);var transform=new MRCTransform(number("m11"),number("m12"),number("m21"),number("m22"),number("x"),number("y"));double a,sx,sy,sh;transform.Decompose(out a,out sx,out sy,out sh);reference.PlacementValue=new MRIPlacementState(transform);}
            foreach(var item in e.Elements()) {if(reference.OverrideItems==null)reference.OverrideItems=new Dictionary<string,MPMBinding>(StringComparer.Ordinal);reference.OverrideItems.Add(MEMNode.NormalizeName(XRequired(item,"property")),ReadBinding(item,true));}
            return reference;
        }
    }
}
