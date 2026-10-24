using System;
using System.Collections.Generic;
using System.Linq;
namespace CxVisual.Mechanism.Logic.Commands
{
    /// <summary>仅存已发布CommandDefinition的只读运行快照，不存第二工程真源。</summary>
    public sealed class MLGCommandDefinitionCatalog
    {
        private readonly Dictionary<long,MLGCommandDefinition> _ids=new Dictionary<long,MLGCommandDefinition>();
        private readonly Dictionary<string,MLGCommandDefinition> _codes=
            new Dictionary<string,MLGCommandDefinition>(StringComparer.Ordinal);
        public MLGCommandDefinitionCatalog(IEnumerable<MLGCommandDefinition> definitions)
        {
            if(definitions==null)throw new ArgumentNullException("definitions");
            foreach(MLGCommandDefinition d in definitions)
            {
                if(d==null)throw new ArgumentException("定义不能为空");
                if(_ids.ContainsKey(d.CommandId)||_codes.ContainsKey(d.CommandCode))
                    throw new ArgumentException("CommandId/Code重复");
                _ids.Add(d.CommandId,d);_codes.Add(d.CommandCode,d);
            }
        }
        public MLGCommandDefinition Find(long id)
        {MLGCommandDefinition d;return _ids.TryGetValue(id,out d)?d:null;}
        public MLGCommandDefinition Find(string code)
        {MLGCommandDefinition d;return code!=null&&_codes.TryGetValue(code,out d)?d:null;}
        public IReadOnlyList<MLGCommandDefinition> Snapshot()
        {return Array.AsReadOnly(_ids.Values.OrderBy(d=>d.CommandCode,StringComparer.Ordinal).ToArray());}
    }
}
