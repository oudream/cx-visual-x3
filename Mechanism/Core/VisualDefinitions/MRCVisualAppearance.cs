using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace CxVisual.Mechanism.Core
{
    /// <summary>持久化颜色值，不依赖 WPF Brush；统一采用 ARGB。</summary>
    public struct MRCColor : IEquatable<MRCColor>
    {
        public MRCColor(byte a,byte r,byte g,byte b){A=a;R=r;G=g;B=b;}
        public byte A{get;private set;}public byte R{get;private set;}public byte G{get;private set;}public byte B{get;private set;}
        public static MRCColor FromRgb(byte r,byte g,byte b){return new MRCColor(255,r,g,b);}
        public static MRCColor Parse(string hex)
        {
            if(string.IsNullOrEmpty(hex)||hex[0]!='#'||(hex.Length!=7&&hex.Length!=9))throw new FormatException("visual.color.hex");
            string s=hex.Length==7?"FF"+hex.Substring(1):hex.Substring(1);
            return new MRCColor(byte.Parse(s.Substring(0,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),
                byte.Parse(s.Substring(2,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),
                byte.Parse(s.Substring(4,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture),
                byte.Parse(s.Substring(6,2),NumberStyles.HexNumber,CultureInfo.InvariantCulture));
        }
        public override string ToString(){return "#"+A.ToString("X2")+R.ToString("X2")+G.ToString("X2")+B.ToString("X2");}
        public bool Equals(MRCColor o){return A==o.A&&R==o.R&&G==o.G&&B==o.B;}
        public override bool Equals(object o){return o is MRCColor&&Equals((MRCColor)o);}
        public override int GetHashCode(){return A<<24|R<<16|G<<8|B;}
    }
    public enum MRCBrushKind { None=0, Solid=1, LinearGradient=2, Resource=3 }
    public sealed class MRCGradientStop
    {
        public MRCGradientStop(double offset,MRCColor color)
        {MRCVisualItem.Finite(offset);if(offset<0||offset>1)throw new ArgumentOutOfRangeException("offset");Offset=offset;Color=color;}
        public double Offset{get;private set;}public MRCColor Color{get;private set;}
    }
    public sealed class MRCBrushDefinition
    {
        private readonly ReadOnlyCollection<MRCGradientStop> _stops;
        private MRCBrushDefinition(MRCBrushKind kind,MRCColor color,string resource,IEnumerable<MRCGradientStop> stops)
        {Kind=kind;Color=color;ResourceId=resource;_stops=new List<MRCGradientStop>(stops??new MRCGradientStop[0]).AsReadOnly();}
        public MRCBrushKind Kind{get;private set;}public MRCColor Color{get;private set;}public string ResourceId{get;private set;}
        public IReadOnlyList<MRCGradientStop> Stops{get{return _stops;}}
        public static MRCBrushDefinition None(){return new MRCBrushDefinition(MRCBrushKind.None,default(MRCColor),null,null);}
        public static MRCBrushDefinition Solid(MRCColor c){return new MRCBrushDefinition(MRCBrushKind.Solid,c,null,null);}
        public static MRCBrushDefinition Resource(string id)
        {if(string.IsNullOrWhiteSpace(id)||id.Length>512)throw new ArgumentException("visual.style.resource");return new MRCBrushDefinition(MRCBrushKind.Resource,default(MRCColor),id,null);}
        public static MRCBrushDefinition LinearGradient(IEnumerable<MRCGradientStop> stops)
        {
            if(stops==null)throw new ArgumentNullException("stops");
            var values=new List<MRCGradientStop>(stops);
            if(values.Count<2||values.Count>64)throw new ArgumentException("visual.style.gradient");
            double previous=-1;
            foreach(var s in values){if(s==null||s.Offset<previous)throw new ArgumentException("visual.style.gradient_order");previous=s.Offset;}
            return new MRCBrushDefinition(MRCBrushKind.LinearGradient,default(MRCColor),null,values);
        }
    }
    public sealed class MRCVisualStyle
    {
        public MRCVisualStyle(MRCBrushDefinition fill,MRCBrushDefinition stroke,double strokeWidth=1,double opacity=1,
            string fontFamily="Segoe UI",double fontSize=12,string themeKey="")
        {
            Fill=fill??MRCBrushDefinition.None();Stroke=stroke??MRCBrushDefinition.None();
            MRCVisualItem.Finite(strokeWidth);MRCVisualItem.Finite(opacity);MRCVisualItem.Finite(fontSize);
            if(strokeWidth<0||opacity<0||opacity>1||fontSize<=0)throw new ArgumentOutOfRangeException("style");
            if(string.IsNullOrEmpty(fontFamily)||fontFamily.Length>256||themeKey==null||themeKey.Length>512)throw new ArgumentException("style.identity");
            StrokeWidth=strokeWidth;Opacity=opacity;FontFamily=fontFamily;FontSize=fontSize;ThemeKey=themeKey;
        }
        public MRCBrushDefinition Fill{get;private set;}public MRCBrushDefinition Stroke{get;private set;}
        public double StrokeWidth{get;private set;}public double Opacity{get;private set;}
        public string FontFamily{get;private set;}public double FontSize{get;private set;}public string ThemeKey{get;private set;}
        public static MRCVisualStyle Default{get{return new MRCVisualStyle(MRCBrushDefinition.None(),MRCBrushDefinition.Solid(MRCColor.FromRgb(0,0,0)));}}
        public static MRCVisualStyle Solid(MRCColor color)
        {return new MRCVisualStyle(MRCBrushDefinition.Solid(color),MRCBrushDefinition.None());}
    }

    /// <summary>状态仅描述目标呈现样式；不承载正在运行的状态值。</summary>
    public sealed class MRCVisualStateDefinition
    {
        public MRCVisualStateDefinition(string name,MRCVisualStyle style)
        {Name=Required(name);Style=style??throw new ArgumentNullException("style");}
        public string Name{get;private set;}public MRCVisualStyle Style{get;private set;}
        internal static string Required(string s)
        {if(string.IsNullOrWhiteSpace(s)||s.Length>512)throw new ArgumentException("visual.identity");return s;}
    }
    public enum MRCAnimationEasing { Linear=0, EaseIn=1, EaseOut=2, EaseInOut=3 }
    public sealed class MRCAnimationDefinition
    {
        /// <summary>只保留声明：实际值由 Visual 层求值，不写入工程。</summary>
        public MRCAnimationDefinition(string property,double from,double to,int durationMilliseconds,string triggerId="",
            MRCAnimationEasing easing=MRCAnimationEasing.Linear,bool repeat=false)
        {
            Property=MRCVisualStateDefinition.Required(property);MRCVisualItem.Finite(from);MRCVisualItem.Finite(to);
            if(durationMilliseconds<1||durationMilliseconds>86400000||triggerId==null||triggerId.Length>512||
                !Enum.IsDefined(typeof(MRCAnimationEasing),easing))throw new ArgumentException("visual.animation");
            From=from;To=to;DurationMilliseconds=durationMilliseconds;TriggerId=triggerId;Easing=easing;Repeat=repeat;
        }
        public string Property{get;private set;}public double From{get;private set;}public double To{get;private set;}
        public int DurationMilliseconds{get;private set;}public string TriggerId{get;private set;}
        public MRCAnimationEasing Easing{get;private set;}public bool Repeat{get;private set;}
    }
    public sealed class MRCVisualEventAction
    {
        public MRCVisualEventAction(string eventId,string actionId,string parameterBindingId="")
        {EventId=MRCVisualStateDefinition.Required(eventId);ActionId=MRCVisualStateDefinition.Required(actionId);
         ParameterBindingId=parameterBindingId??throw new ArgumentNullException("parameterBindingId");
         if(parameterBindingId.Length>512)throw new ArgumentException("visual.event.binding");}
        public string EventId{get;private set;}public string ActionId{get;private set;}
        public string ParameterBindingId{get;private set;}
    }
    public sealed class MRCVisualBehaviorDefinition
    {
        public MRCVisualBehaviorDefinition(IEnumerable<MRCVisualStateDefinition> states=null,
            IEnumerable<MRCAnimationDefinition> animations=null,IEnumerable<MRCVisualEventAction> eventActions=null)
        {
            var s=new List<MRCVisualStateDefinition>(states??new MRCVisualStateDefinition[0]);
            var a=new List<MRCAnimationDefinition>(animations??new MRCAnimationDefinition[0]);
            var e=new List<MRCVisualEventAction>(eventActions??new MRCVisualEventAction[0]);
            if(s.Count>128||a.Count>1024||e.Count>1024||s.Exists(x=>x==null)||a.Exists(x=>x==null)||e.Exists(x=>x==null))
                throw new ArgumentException("visual.behavior.budget");
            var found=new HashSet<string>(StringComparer.Ordinal);
            foreach(var state in s)if(!found.Add(state.Name))throw new ArgumentException("visual.state.duplicate");
            States=s.AsReadOnly();Animations=a.AsReadOnly();EventActions=e.AsReadOnly();
        }
        public IReadOnlyList<MRCVisualStateDefinition> States{get;private set;}
        public IReadOnlyList<MRCAnimationDefinition> Animations{get;private set;}
        public IReadOnlyList<MRCVisualEventAction> EventActions{get;private set;}
    }

    /// <summary>Style/Behavior 使用原有 MPMAspect 同源存储；禁止绑定动态值或执行委托进入工程。</summary>
    public static class MRCVisualAppearance
    {
        public const string StyleProvider="core.visual.style";
        public const string BehaviorProvider="core.visual.behavior";
        private const int StyleMagic=0x4D535431,BehaviorMagic=0x4D424831;
        private static MPMAspect Find(MRCVisualItem item,string provider)
        {if(item==null)throw new ArgumentNullException("item");foreach(var a in item.Aspects)if(a.Kind==MPMAspectKind.Visual&&a.ProviderId==provider)return a;return null;}
        /// <summary>从不可变工程快照解析样式，保证引用实例与文档图元使用同一份定义。</summary>
        public static MRCVisualStyle StyleFromAspects(IEnumerable<MPMAspect> aspects)
        {
            if(aspects==null)return MRCVisualStyle.Default;
            foreach(MPMAspect aspect in aspects)
                if(aspect.Kind==MPMAspectKind.Visual&&aspect.ProviderId==StyleProvider)
                    return ReadStyleAspect(aspect);
            return MRCVisualStyle.Default;
        }
        public static MRCVisualStyle StyleOf(MRCVisualItem item)
        {MPMAspect aspect=Find(item,StyleProvider);return aspect==null?MRCVisualStyle.Default:ReadStyleAspect(aspect);}
        public static MRCVisualBehaviorDefinition BehaviorOf(MRCVisualItem item)
        {MPMAspect aspect=Find(item,BehaviorProvider);return aspect==null?new MRCVisualBehaviorDefinition():ReadBehaviorAspect(aspect);}
        public static MPMAspect StyleAspect(MRCVisualStyle style)
        {
            if(style==null)throw new ArgumentNullException("style");
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms,Encoding.UTF8))
            {w.Write(StyleMagic);w.Write(1);WriteStyle(w,style);w.Flush();return new MPMAspect(MPMAspectKind.Visual,StyleProvider,1,ms.ToArray());}
        }
        public static MPMAspect BehaviorAspect(MRCVisualBehaviorDefinition behavior)
        {
            if(behavior==null)throw new ArgumentNullException("behavior");
            using(var ms=new MemoryStream())using(var w=new BinaryWriter(ms,Encoding.UTF8))
            {
                w.Write(BehaviorMagic);w.Write(1);
                w.Write(behavior.States.Count);
                foreach(var s in behavior.States){w.Write(s.Name);WriteStyle(w,s.Style);}
                w.Write(behavior.Animations.Count);
                foreach(var a in behavior.Animations){w.Write(a.Property);w.Write(a.From);w.Write(a.To);w.Write(a.DurationMilliseconds);w.Write(a.TriggerId);w.Write((int)a.Easing);w.Write(a.Repeat);}
                w.Write(behavior.EventActions.Count);
                foreach(var e in behavior.EventActions){w.Write(e.EventId);w.Write(e.ActionId);w.Write(e.ParameterBindingId);}
                w.Flush();return new MPMAspect(MPMAspectKind.Visual,BehaviorProvider,1,ms.ToArray());
            }
        }
        private static void WriteBrush(BinaryWriter w,MRCBrushDefinition b)
        {w.Write((int)b.Kind);w.Write(b.Color.ToString());w.Write(b.ResourceId??"");w.Write(b.Stops.Count);foreach(var stop in b.Stops){w.Write(stop.Offset);w.Write(stop.Color.ToString());}}
        private static MRCBrushDefinition ReadBrush(BinaryReader r)
        {
            var kind=(MRCBrushKind)r.ReadInt32();var color=MRCColor.Parse(r.ReadString());string reference=r.ReadString();
            int n=r.ReadInt32();if(n<0||n>64)throw new InvalidDataException("visual.brush.budget");
            var stops=new List<MRCGradientStop>();for(int i=0;i<n;i++)stops.Add(new MRCGradientStop(r.ReadDouble(),MRCColor.Parse(r.ReadString())));
            switch(kind){case MRCBrushKind.None:if(n!=0)throw new InvalidDataException("visual.brush.none_stops");return MRCBrushDefinition.None();
                case MRCBrushKind.Solid:if(n!=0)throw new InvalidDataException("visual.brush.solid_stops");return MRCBrushDefinition.Solid(color);
                case MRCBrushKind.Resource:if(n!=0)throw new InvalidDataException("visual.brush.resource_stops");return MRCBrushDefinition.Resource(reference);
                case MRCBrushKind.LinearGradient:return MRCBrushDefinition.LinearGradient(stops);
                default:throw new InvalidDataException("visual.brush.kind");}
        }
        private static void WriteStyle(BinaryWriter w,MRCVisualStyle style)
        {WriteBrush(w,style.Fill);WriteBrush(w,style.Stroke);w.Write(style.StrokeWidth);w.Write(style.Opacity);w.Write(style.FontFamily);w.Write(style.FontSize);w.Write(style.ThemeKey);}
        private static MRCVisualStyle ReadStyle(BinaryReader r)
        {var fill=ReadBrush(r);var stroke=ReadBrush(r);double width=r.ReadDouble(),opacity=r.ReadDouble();string font=r.ReadString();double fontSize=r.ReadDouble();string theme=r.ReadString();return new MRCVisualStyle(fill,stroke,width,opacity,font,fontSize,theme);}
        private static void ValidateAspect(MPMAspect a,string provider)
        {if(a==null||a.Kind!=MPMAspectKind.Visual||a.ProviderId!=provider||a.SchemaVersion!=1)throw new InvalidDataException("visual.aspect.schema");}
        private static MRCVisualStyle ReadStyleAspect(MPMAspect aspect)
        {
            ValidateAspect(aspect,StyleProvider);
            using(var ms=new MemoryStream(aspect.Payload,false))using(var r=new BinaryReader(ms,Encoding.UTF8))
            {if(r.ReadInt32()!=StyleMagic||r.ReadInt32()!=1)throw new InvalidDataException("visual.style.version");var style=ReadStyle(r);if(ms.Position!=ms.Length)throw new InvalidDataException("visual.style.trailing");return style;}
        }
        private static MRCVisualBehaviorDefinition ReadBehaviorAspect(MPMAspect aspect)
        {
            ValidateAspect(aspect,BehaviorProvider);
            using(var ms=new MemoryStream(aspect.Payload,false))using(var r=new BinaryReader(ms,Encoding.UTF8))
            {
                if(r.ReadInt32()!=BehaviorMagic||r.ReadInt32()!=1)throw new InvalidDataException("visual.behavior.version");
                int n=r.ReadInt32();if(n<0||n>128)throw new InvalidDataException("visual.behavior.state_budget");
                var states=new List<MRCVisualStateDefinition>();
                for(int i=0;i<n;i++)states.Add(new MRCVisualStateDefinition(r.ReadString(),ReadStyle(r)));
                n=r.ReadInt32();if(n<0||n>1024)throw new InvalidDataException("visual.behavior.animation_budget");
                var animations=new List<MRCAnimationDefinition>();
                for(int i=0;i<n;i++)animations.Add(new MRCAnimationDefinition(r.ReadString(),r.ReadDouble(),r.ReadDouble(),r.ReadInt32(),r.ReadString(),(MRCAnimationEasing)r.ReadInt32(),r.ReadBoolean()));
                n=r.ReadInt32();if(n<0||n>1024)throw new InvalidDataException("visual.behavior.event_budget");
                var actions=new List<MRCVisualEventAction>();
                for(int i=0;i<n;i++)actions.Add(new MRCVisualEventAction(r.ReadString(),r.ReadString(),r.ReadString()));
                if(ms.Position!=ms.Length)throw new InvalidDataException("visual.behavior.trailing");
                return new MRCVisualBehaviorDefinition(states,animations,actions);
            }
        }
    }
    public sealed partial class MTCEditSession
    {
        public void SetVisualStyle(MRCVisualItem item,MRCVisualStyle style){SetAspect(item,MRCVisualAppearance.StyleAspect(style));}
        public void SetVisualBehavior(MRCVisualItem item,MRCVisualBehaviorDefinition definition){SetAspect(item,MRCVisualAppearance.BehaviorAspect(definition));}
    }
    public sealed partial class MEMEngineeringSession
    {
        public void SetVisualStyle(MRCVisualItem item,MRCVisualStyle style)
        {Edit("设置图元样式",e=>e.SetVisualStyle(item,style)).RequireValue();}
        public void SetVisualBehavior(MRCVisualItem item,MRCVisualBehaviorDefinition definition)
        {Edit("设置图元状态动画和事件",e=>e.SetVisualBehavior(item,definition)).RequireValue();}
    }
}
