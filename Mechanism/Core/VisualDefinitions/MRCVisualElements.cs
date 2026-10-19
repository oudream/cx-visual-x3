using System;
using System.IO;
using System.Text;

namespace CxVisual.Mechanism.Core
{
    /// <summary>图元叶子：文字。文字是工程声明，不继承任何WPF文本控件。</summary>
    public sealed class MRCTextElement : MRCElement
    {
        internal const string TextProvider="core.visual.text";
        public MRCTextElement(Guid id,string name,double x,double y,double width=150,double height=30)
            :base(id,name,"core.text",x,y,width,height){}
        public string Text
        {
            get
            {
                foreach(var aspect in Aspects)
                    if(aspect.Kind==MPMAspectKind.Visual&&aspect.ProviderId==TextProvider)
                    {
                        if(aspect.SchemaVersion!=1)throw new InvalidDataException("visual.text.version");
                        return new UTF8Encoding(false,true).GetString(aspect.Payload);
                    }
                return "";
            }
        }
        internal static MPMAspect ToAspect(string text)
        {
            if(text==null)throw new ArgumentNullException("text");
            if(text.Length>1000000)throw new ArgumentException("visual.text.budget");
            return new MPMAspect(MPMAspectKind.Visual,TextProvider,1,new UTF8Encoding(false,true).GetBytes(text));
        }
    }
    /// <summary>图片通过 MP MBinding source → MEMAssetReference引用；不存运行中的Bitmap。</summary>
    public sealed class MRCImageElement : MRCElement
    {
        public MRCImageElement(Guid id,string name,double x,double y,double width=120,double height=80)
            :base(id,name,"core.image",x,y,width,height){}
        public MPMBinding Source{get{return GetBinding("source");}}
    }
    /// <summary>SVG图片资源引用；实际清洗、解析与WPF绘制由原Visual/WPF Owner承担。</summary>
    public sealed class MRCSvgElement : MRCElement
    {
        public MRCSvgElement(Guid id,string name,double x,double y,double width=120,double height=80)
            :base(id,name,"core.svg",x,y,width,height){}
        public MPMBinding Source{get{return GetBinding("source");}}
    }
    /// <summary>控件类型可由 MPMTypeDescriptor(Kind.Element) 注册；不序列化活动WPF Control。</summary>
    public sealed class MRCWidgetElement : MRCElement
    {
        public MRCWidgetElement(Guid id,string name,string typeId,double x,double y,double width=120,double height=36)
            :base(id,name,"core.widget",x,y,width,height)
        {
            if(typeId!="core.widget")SetTypeId(typeId);
        }
        public string WidgetTypeId{get{return TypeId;}}
    }
    public sealed partial class MTCEditSession
    {
        public MRCTextElement AddText(MEMNode parent,string name,string text,double x=0,double y=0,double width=150,double height=30)
        {var item=(MRCTextElement)Add(parent,new MRCTextElement(Guid.NewGuid(),name,x,y,width,height));SetAspect(item,MRCTextElement.ToAspect(text));return item;}
        public void SetText(MRCTextElement element,string text){SetAspect(element,MRCTextElement.ToAspect(text));}
        public MRCImageElement AddImage(MEMNode parent,string name,double x=0,double y=0,double width=120,double height=80)
        {return (MRCImageElement)Add(parent,new MRCImageElement(Guid.NewGuid(),name,x,y,width,height));}
        public MRCSvgElement AddSvg(MEMNode parent,string name,double x=0,double y=0,double width=120,double height=80)
        {return (MRCSvgElement)Add(parent,new MRCSvgElement(Guid.NewGuid(),name,x,y,width,height));}
    }
    public sealed partial class MEMEngineeringSession
    {
        public MRCTextElement AddText(MEMNode parent,string name,string text,double x=0,double y=0,double width=150,double height=30)
        {return Edit("添加文字",e=>e.AddText(parent,name,text,x,y,width,height)).RequireValue();}
        public void SetText(MRCTextElement element,string text)
        {Edit("修改文字",e=>e.SetText(element,text)).RequireValue();}
        public MRCImageElement AddImage(MEMNode parent,string name,double x=0,double y=0,double width=120,double height=80)
        {return Edit("添加图片",e=>e.AddImage(parent,name,x,y,width,height)).RequireValue();}
        public MRCSvgElement AddSvg(MEMNode parent,string name,double x=0,double y=0,double width=120,double height=80)
        {return Edit("添加SVG",e=>e.AddSvg(parent,name,x,y,width,height)).RequireValue();}
    }
}
