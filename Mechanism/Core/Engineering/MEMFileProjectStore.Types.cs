using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace CxVisual.Mechanism.Core
{
    public sealed partial class MEMFileProjectStore
    {
        private static void WriteTypes(XmlWriter w,MEMProject project)
        {
            if(project.TypeItems==null)return;
            foreach(var type in project.TypeItems.Values.OrderBy(t=>t.TypeId,StringComparer.Ordinal))
            {
                w.WriteStartElement("Type");w.WriteAttributeString("id",type.TypeId);w.WriteAttributeString("schema",type.SchemaVersion.ToString(Invariant));w.WriteAttributeString("kind",type.Kind.ToString());
                foreach(var property in type.Properties)
                {
                    w.WriteStartElement("Property");w.WriteAttributeString("member",property.MemberId);w.WriteAttributeString("valueKind",property.ValueKind.ToString());w.WriteAttributeString("required",XmlConvert.ToString(property.Required));w.WriteAttributeString("public",XmlConvert.ToString(property.IsPublic));if(property.IsReadOnly)w.WriteAttributeString("readOnly","true");
                    if(property.Minimum.HasValue)Number(w,"min",property.Minimum.Value);if(property.Maximum.HasValue)Number(w,"max",property.Maximum.Value);
                    if(property.DefaultBinding!=null){w.WriteStartElement("Default");WriteBinding(w,property.DefaultBinding);w.WriteEndElement();}w.WriteEndElement();
                }
                foreach(var port in type.Ports)
                {
                    w.WriteStartElement("Port");w.WriteAttributeString("member",port.MemberId);w.WriteAttributeString("direction",port.Direction.ToString());w.WriteAttributeString("typeId",port.TypeId);w.WriteAttributeString("kind",port.Kind.ToString());w.WriteAttributeString("multiplicity",port.Multiplicity.ToString());w.WriteAttributeString("public",XmlConvert.ToString(port.IsPublic));w.WriteAttributeString("required",XmlConvert.ToString(port.Required));
                    w.WriteAttributeString("unit64",Encode(port.Contract.UnitId));w.WriteAttributeString("frame64",Encode(port.Contract.CoordinateFrameId));w.WriteAttributeString("time64",Encode(port.Contract.TimeBaseId));w.WriteEndElement();
                }
                foreach(var aspect in type.Aspects)
                {w.WriteStartElement("Aspect");w.WriteAttributeString("kind",aspect.Kind.ToString());w.WriteAttributeString("provider",aspect.ProviderId);w.WriteAttributeString("schema",aspect.SchemaVersion.ToString(Invariant));w.WriteAttributeString("required",XmlConvert.ToString(aspect.Required));if(aspect.RequiresRemap)w.WriteAttributeString("remap","true");w.WriteBase64(aspect.PayloadBytes,0,aspect.PayloadBytes.Length);w.WriteEndElement();}
                w.WriteEndElement();
            }
        }
        private static MPMTypeDescriptor ReadType(XmlReader reader)
        {
            var e=ReadSmallElement(reader);XFields(e,"id schema kind",true);
            var properties=new List<MPMPropertyDescriptor>();var ports=new List<MPCPortDescriptor>();var aspects=new List<MPMAspect>();
            foreach(var child in e.Elements())
            {
                if(child.Name=="Property")
                {
                    XFields(child,"member valueKind required public min max readOnly",true);if(child.Elements().Any(c=>c.Name!="Default")||child.Elements().Count()>1)throw new InvalidDataException("Invalid type property");var value=child.Element("Default");
                    properties.Add(new MPMPropertyDescriptor(XRequired(child,"member"),XEnum<MPMValueKind>(child,"valueKind"),XBool(child,"required"),XBool(child,"public",true),value==null?null:ReadBinding(value),child.Attribute("min")==null?(double?)null:double.Parse(XRequired(child,"min"),Invariant),child.Attribute("max")==null?(double?)null:double.Parse(XRequired(child,"max"),Invariant),XBool(child,"readOnly")));
                }
                else if(child.Name=="Port")
                {
                    XFields(child,"member direction typeId kind multiplicity public required unit64 frame64 time64");
                    ports.Add(new MPCPortDescriptor(XRequired(child,"member"),XEnum<MPCPortDirection>(child,"direction"),XRequired(child,"typeId"),XEnum<MPCPortKind>(child,"kind"),XEnum<MPCPortMultiplicity>(child,"multiplicity"),new MTUContractReference(Decode(XRequired(child,"unit64")),Decode(XRequired(child,"frame64")),Decode(XRequired(child,"time64"))),XBool(child,"public"),XBool(child,"required")));
                }
                else if(child.Name=="Aspect")
                {
                    XFields(child,"kind provider schema required remap",true);if(child.Elements().Any()||child.Value.Length>5600000)throw new InvalidDataException("Invalid type aspect");
                    aspects.Add(new MPMAspect(XEnum<MPMAspectKind>(child,"kind"),XRequired(child,"provider"),int.Parse(XRequired(child,"schema"),Invariant),Convert.FromBase64String(child.Value),XBool(child,"required",true),XBool(child,"remap")));
                }
                else throw new InvalidDataException("Unsupported type schema field");
                if(properties.Count+ports.Count+aspects.Count>4096)throw new InvalidDataException("Type member budget exceeded");
            }
            return new MPMTypeDescriptor(XRequired(e,"id"),int.Parse(XRequired(e,"schema"),Invariant),XEnum<MEMNodeKind>(e,"kind"),properties,ports,aspects);
        }
    }
}
