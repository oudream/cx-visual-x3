using System;
using System.Collections.Generic;
using System.Linq;
namespace CxVisual.Foundation.Data.Commands
{
    /// <summary>仅存已发布CommandDefinition的只读运行快照，不存第二工程真源。</summary>
    public sealed class FDCCommandDefinitionCatalog
    {
        private readonly Dictionary<long,FDCCommandDefinition> _ids=new Dictionary<long,FDCCommandDefinition>();
        private readonly Dictionary<string,FDCCommandDefinition> _codes=
            new Dictionary<string,FDCCommandDefinition>(StringComparer.Ordinal);
        public FDCCommandDefinitionCatalog(IEnumerable<FDCCommandDefinition> definitions)
        {
            if(definitions==null)throw new ArgumentNullException("definitions");
            foreach(FDCCommandDefinition d in definitions)
            {
                if(d==null)throw new ArgumentException("定义不能为空");
                if(_ids.ContainsKey(d.CommandId)||_codes.ContainsKey(d.CommandCode))
                    throw new ArgumentException("CommandId/Code重复");
                _ids.Add(d.CommandId,d);_codes.Add(d.CommandCode,d);
            }
        }
        public FDCCommandDefinition Find(long id)
        {FDCCommandDefinition d;return _ids.TryGetValue(id,out d)?d:null;}
        public FDCCommandDefinition Find(string code)
        {FDCCommandDefinition d;return code!=null&&_codes.TryGetValue(code,out d)?d:null;}
        public IReadOnlyList<FDCCommandDefinition> Snapshot()
        {return Array.AsReadOnly(_ids.Values.OrderBy(d=>d.CommandCode,StringComparer.Ordinal).ToArray());}
    }
}
