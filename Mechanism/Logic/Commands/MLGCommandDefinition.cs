using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace CxVisual.Mechanism.Logic.Commands
{
    /// <summary>命令参数定义，区分缺失、0、false和显式null；不复制FCM参数库。</summary>
    public sealed class MLGCommandParameterDefinition
    {
        public MLGCommandParameterDefinition(string code,Type dataType,bool required,
            bool hasDefaultValue=false,object defaultValue=null)
        {
            CheckCode(code);
            if(dataType==null||dataType==typeof(void)||dataType.IsPointer||
                dataType.ContainsGenericParameters)throw new ArgumentException("无效参数类型","dataType");
            if(required&&hasDefaultValue)throw new ArgumentException("必填参数不得指定默认值");
            Code=code;DataType=dataType;Required=required;HasDefaultValue=hasDefaultValue;
            if(hasDefaultValue)ValidateValue(defaultValue);
            DefaultValue=defaultValue;
        }
        public string Code{get;private set;}
        public Type DataType{get;private set;}
        public bool Required{get;private set;}
        public bool HasDefaultValue{get;private set;}
        public object DefaultValue{get;private set;}
        internal static void CheckCode(string code)
        {
            if(string.IsNullOrWhiteSpace(code)||code.Length>64)throw new ArgumentException("无效命令编码","code");
            foreach(char c in code)
                if(!((c>='A'&&c<='Z')||(c>='a'&&c<='z')||
                     (c>='0'&&c<='9')||c=='_'||c=='-'||c=='.'))
                    throw new ArgumentException("命令编码存在非法字符","code");
        }
        public void ValidateValue(object value)
        {
            if(value==null)
            {
                if(Required||(DataType.IsValueType&&Nullable.GetUnderlyingType(DataType)==null))
                    throw new ArgumentException("参数不能为null: "+Code);
                return;
            }
            Type underlying=Nullable.GetUnderlyingType(DataType);
            if(!DataType.IsInstanceOfType(value)&&
               (underlying==null||!underlying.IsInstanceOfType(value)))
                throw new ArgumentException("命令参数类型不匹配: "+Code);
        }
    }

    /// <summary>命令定义只描述输入输出Schema，不持有请求/运行状态。</summary>
    public sealed class MLGCommandDefinition
    {
        private readonly ReadOnlyCollection<MLGCommandParameterDefinition> _inputs;
        private readonly ReadOnlyCollection<MLGCommandParameterDefinition> _outputs;
        public MLGCommandDefinition(long id,string code,string name,
            IEnumerable<MLGCommandParameterDefinition> inputs,
            IEnumerable<MLGCommandParameterDefinition> outputs,int schemaVersion=1)
        {
            if(id<=0)throw new ArgumentOutOfRangeException("id");
            MLGCommandParameterDefinition.CheckCode(code);
            if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("显示名无效","name");
            if(schemaVersion<1)throw new ArgumentOutOfRangeException("schemaVersion");
            MLGCommandParameterDefinition[] ins=(inputs??Enumerable.Empty<MLGCommandParameterDefinition>()).ToArray();
            MLGCommandParameterDefinition[] outs=(outputs??Enumerable.Empty<MLGCommandParameterDefinition>()).ToArray();
            if(ins.Concat(outs).Any(x=>x==null)||
                ins.GroupBy(x=>x.Code,StringComparer.Ordinal).Any(g=>g.Count()>1)||
                outs.GroupBy(x=>x.Code,StringComparer.Ordinal).Any(g=>g.Count()>1))
                throw new ArgumentException("存在空参数或重复参数编码");
            CommandId=id;CommandCode=code;Name=name;SchemaVersion=schemaVersion;
            _inputs=Array.AsReadOnly(ins);_outputs=Array.AsReadOnly(outs);
        }
        public long CommandId{get;private set;}
        public string CommandCode{get;private set;}
        public string Name{get;private set;}
        public int SchemaVersion{get;private set;}
        public IReadOnlyList<MLGCommandParameterDefinition> Inputs{get{return _inputs;}}
        public IReadOnlyList<MLGCommandParameterDefinition> Outputs{get{return _outputs;}}
        public IReadOnlyDictionary<string,object> BindInputs(IReadOnlyDictionary<string,object> values)
        {return Bind(_inputs,values,true);}
        public IReadOnlyDictionary<string,object> ValidateOutputs(IReadOnlyDictionary<string,object> values)
        {return Bind(_outputs,values,false);}
        private static IReadOnlyDictionary<string,object> Bind(
            IEnumerable<MLGCommandParameterDefinition> definitions,
            IReadOnlyDictionary<string,object> values,bool defaults)
        {
            MLGCommandParameterDefinition[] ds=definitions.ToArray();
            IReadOnlyDictionary<string,object> provided=values ??
                new ReadOnlyDictionary<string,object>(new Dictionary<string,object>());
            Dictionary<string,object> result=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(KeyValuePair<string,object> pair in provided)
                if(!ds.Any(d=>d.Code==pair.Key))
                    throw new ArgumentException("未知命令参数: "+pair.Key);
            foreach(MLGCommandParameterDefinition d in ds)
            {
                object value;
                if(provided.TryGetValue(d.Code,out value))
                {d.ValidateValue(value);result.Add(d.Code,value);}
                else if(defaults&&d.HasDefaultValue)
                    result.Add(d.Code,d.DefaultValue);
                else if(d.Required)
                    throw new ArgumentException("缺少必填参数: "+d.Code);
            }
            return new ReadOnlyDictionary<string,object>(result);
        }
    }
}
