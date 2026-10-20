using System;
using System.Globalization;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core
{
    public enum MPMValueKind { Null=0, String=1, Number=2, Integer=3, Boolean=4, Decimal=5, Resource=6 }
    /// <summary>Closed persistent engineering literal, not a configuration store or runtime heap.</summary>
    public sealed class MPMValue : IEquatable<MPMValue>
    {
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        private MPMValue(MPMValueKind kind,string canonical){Kind=kind;Canonical=canonical;}
        public MPMValueKind Kind {get;private set;}
        public string Canonical {get;private set;}
        public static MPMValue Null {get{return new MPMValue(MPMValueKind.Null,"");}}
        public static MPMValue String(string value){if(value==null)return Null;for(int i=0;i<value.Length;i++){if(char.IsHighSurrogate(value[i])){if(++i>=value.Length||!char.IsLowSurrogate(value[i]))throw new ArgumentException("Unpaired UTF16 surrogate");}else if(char.IsLowSurrogate(value[i]))throw new ArgumentException("Unpaired UTF16 surrogate");}return new MPMValue(MPMValueKind.String,value);}
        public static MPMValue Number(double value){MRCVisualItem.Finite(value);return new MPMValue(MPMValueKind.Number,value.ToString("R",Invariant));}
        public static MPMValue Integer(long value){return new MPMValue(MPMValueKind.Integer,value.ToString(Invariant));}
        public static MPMValue Decimal(decimal value){return new MPMValue(MPMValueKind.Decimal,value.ToString("G29",Invariant));}
        public static MPMValue Boolean(bool value){return new MPMValue(MPMValueKind.Boolean,value?"true":"false");}
        public double AsNumber(){if(Kind!=MPMValueKind.Number)throw new InvalidOperationException("Not a number literal");return double.Parse(Canonical,Invariant);}
        public long AsInteger(){if(Kind!=MPMValueKind.Integer)throw new InvalidOperationException("Not an integer literal");return long.Parse(Canonical,Invariant);}
        public decimal AsDecimal(){if(Kind!=MPMValueKind.Decimal)throw new InvalidOperationException("Not a decimal literal");return decimal.Parse(Canonical,Invariant);}
        public string AsString(){if(Kind!=MPMValueKind.String)throw new InvalidOperationException("Not a string literal");return Canonical;}
        public bool AsBoolean(){if(Kind!=MPMValueKind.Boolean)throw new InvalidOperationException("Not a boolean literal");return Canonical=="true";}
        internal static MPMValue Parse(MPMValueKind kind,string text)
        {
            switch(kind){case MPMValueKind.Null:if(text!="")throw new ArgumentException("Invalid null literal");return Null;case MPMValueKind.String:return String(text);case MPMValueKind.Number:return Number(double.Parse(text,Invariant));case MPMValueKind.Integer:return Integer(long.Parse(text,Invariant));case MPMValueKind.Decimal:return Decimal(decimal.Parse(text,Invariant));case MPMValueKind.Boolean:if(text!="true"&&text!="false")throw new ArgumentException("Invalid boolean literal");return Boolean(text=="true");default:throw new ArgumentException("Unknown literal kind");}
        }
        public bool Equals(MPMValue other){return other!=null&&Kind==other.Kind&&Canonical==other.Canonical;}
        public override bool Equals(object value){return Equals(value as MPMValue);}
        public override int GetHashCode(){return ((int)Kind*397)^Canonical.GetHashCode();}
    }
    public enum MPMBindingKind { Legacy=0, Literal=1, Configuration=2, Variable=3, Resource=4, Member=5, Expression=6 }
    /// <summary>Immutable source declaration. Existing config/data/provider owners resolve it; Core never evaluates scripts or opens devices.</summary>
    public sealed class MPMBinding : IEquatable<MPMBinding>
    {
        public MPMBinding(string sourceId,string memberId):this(MPMBindingKind.Legacy,Required(sourceId),Required(memberId)){}
        private MPMBinding(MPMBindingKind kind,string source,string member){Kind=kind;SourceId=source;MemberId=member;}
        private static string Required(string value){if(string.IsNullOrWhiteSpace(value)||value.Length>4096)throw new ArgumentException("Binding identity required");return value;}
        public MPMBindingKind Kind {get;private set;}
        public string SourceId {get;private set;}
        public string MemberId {get;private set;}
        public MPMValue Value {get;private set;}
        public MRIObjectAddress Address {get;private set;}
        public Guid? ResourceId {get;private set;}
        public FCMConfigReference Configuration {get;private set;}
        public static MPMBinding Literal(MPMValue value){if(value==null)throw new ArgumentNullException("value");return new MPMBinding(MPMBindingKind.Literal,"literal",""){Value=value};}
        public static MPMBinding Variable(string id,string member="value"){return new MPMBinding(MPMBindingKind.Variable,Required(id),Required(member));}
        public static MPMBinding Resource(Guid id){if(id==Guid.Empty)throw new ArgumentException("Resource identity required");return new MPMBinding(MPMBindingKind.Resource,id.ToString("D"),""){ResourceId=id};}
        public static MPMBinding Member(MRIObjectAddress address,string member){if(address==null)throw new ArgumentNullException("address");return new MPMBinding(MPMBindingKind.Member,address.ToString(),Required(member)){Address=address};}
        public static MPMBinding Config(FCMConfigReference reference){if(reference==null)throw new ArgumentNullException("reference");return new MPMBinding(MPMBindingKind.Configuration,reference.Target.CanonicalKey,reference.Path){Configuration=reference};}
        public static MPMBinding Expression(string registeredId){return new MPMBinding(MPMBindingKind.Expression,Required(registeredId),"");}
        public bool Equals(MPMBinding other)
        {return other!=null&&Kind==other.Kind&&SourceId==other.SourceId&&MemberId==other.MemberId&&object.Equals(Value,other.Value)&&(Configuration==null||Configuration.Optional==other.Configuration.Optional);}
        public override bool Equals(object value){return Equals(value as MPMBinding);}
        public override int GetHashCode(){return SourceId.GetHashCode()^((int)Kind*397)^MemberId.GetHashCode();}
    }
    public sealed class MPMPropertyDescriptor
    {
        public MPMPropertyDescriptor(string memberId,MPMValueKind valueKind,bool required=false,bool isPublic=true,MPMBinding defaultBinding=null,double? minimum=null,double? maximum=null,bool isReadOnly=false)
        {
            MemberId=MEMNode.NormalizeName(memberId);if(!Enum.IsDefined(typeof(MPMValueKind),valueKind)||valueKind==MPMValueKind.Null)throw new ArgumentException("Declared value kind required");
            ValueKind=valueKind;Required=required;IsPublic=isPublic;IsReadOnly=isReadOnly;Minimum=minimum;Maximum=maximum;
            if(minimum.HasValue)MRCVisualItem.Finite(minimum.Value);if(maximum.HasValue)MRCVisualItem.Finite(maximum.Value);if(minimum>maximum)throw new ArgumentException("Invalid range");
            if((minimum.HasValue||maximum.HasValue)&&valueKind!=MPMValueKind.Number&&valueKind!=MPMValueKind.Integer&&valueKind!=MPMValueKind.Decimal)throw new ArgumentException("Range requires a numeric declaration");
            Validate(defaultBinding);DefaultBinding=defaultBinding;
        }
        public string MemberId {get;private set;}
        public MPMValueKind ValueKind {get;private set;}
        public bool Required {get;private set;}
        public bool IsPublic {get;private set;}
        /// <summary>Immutable engineering value after definition; independent from public visibility.</summary>
        public bool IsReadOnly {get;private set;}
        public MPMBinding DefaultBinding {get;private set;}
        public double? Minimum {get;private set;}
        public double? Maximum {get;private set;}
        internal void Validate(MPMBinding binding)
        {
            if(binding!=null&&binding.Kind==MPMBindingKind.Resource&&ValueKind!=MPMValueKind.Resource)throw new InvalidOperationException("org.property.type_mismatch: "+MemberId);
            if(binding==null||binding.Kind!=MPMBindingKind.Literal||binding.Value.Kind==MPMValueKind.Null)return;
            if(binding.Value.Kind!=ValueKind)throw new InvalidOperationException("org.property.type_mismatch: "+MemberId);
            if(Minimum.HasValue||Maximum.HasValue)
            {
                double value=ValueKind==MPMValueKind.Number?binding.Value.AsNumber():ValueKind==MPMValueKind.Integer?binding.Value.AsInteger():ValueKind==MPMValueKind.Decimal?(double)binding.Value.AsDecimal():double.NaN;
                if(double.IsNaN(value))throw new InvalidOperationException("Range requires numeric declaration");
                if(value<Minimum||value>Maximum)throw new InvalidOperationException("org.property.out_of_range: "+MemberId);
            }
        }
    }
    public sealed class MPMProperty
    {
        internal MPMProperty(Guid ownerId,MPMPropertyDescriptor descriptor){OwnerId=ownerId;Descriptor=descriptor;}
        public Guid OwnerId {get;private set;}
        public MPMPropertyDescriptor Descriptor {get;private set;}
        public string MemberId {get{return Descriptor.MemberId;}}
        public MPMBinding Binding {get{return ExplicitBinding??Descriptor.DefaultBinding;}}
        public bool HasOverride {get{return ExplicitBinding!=null;}}
        internal MPMBinding ExplicitBinding;
    }
}
