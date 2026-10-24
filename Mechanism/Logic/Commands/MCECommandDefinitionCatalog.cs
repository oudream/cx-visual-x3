using System;
using System.Collections.Generic;
using System.Linq;
namespace CxVisual.Mechanism.Logic.Commands
{
    /// <summary>仅存已发布CommandDefinition的只读运行快照，不存第二工程真源。</summary>
    public sealed class MCECommandDefinitionCatalog
    {
        private readonly Dictionary<long,MCECommandDefinition> _ids=new Dictionary<long,MCECommandDefinition>();
        private readonly Dictionary<string,MCECommandDefinition> _codes=
            new Dictionary<string,MCECommandDefinition>(StringComparer.Ordinal);
        public MCECommandDefinitionCatalog(IEnumerable<MCECommandDefinition> definitions)
        {
            if(definitions==null)throw new ArgumentNullException("definitions");
            foreach(MCECommandDefinition d in definitions)
            {
                if(d==null)throw new ArgumentException("定义不能为空");
                if(_ids.ContainsKey(d.CommandId)||_codes.ContainsKey(d.CommandCode))
                    throw new ArgumentException("CommandId/Code重复");
                _ids.Add(d.CommandId,d);_codes.Add(d.CommandCode,d);
            }
        }
        public MCECommandDefinition Find(long id)
        {MCECommandDefinition d;return _ids.TryGetValue(id,out d)?d:null;}
        public MCECommandDefinition Find(string code)
        {MCECommandDefinition d;return code!=null&&_codes.TryGetValue(code,out d)?d:null;}
        public IReadOnlyList<MCECommandDefinition> Snapshot()
        {return Array.AsReadOnly(_ids.Values.OrderBy(d=>d.CommandCode,StringComparer.Ordinal).ToArray());}
    }
}
