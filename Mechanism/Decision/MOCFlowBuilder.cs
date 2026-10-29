using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Orchestration
{
    /// <summary>G04-D临时选区快照，不是工程对象树或第二份可执行定义。</summary>
    public sealed class MOCWiringFragment
    {
        private readonly IReadOnlyList<MOLControlBlock> _blocks;
        private readonly IReadOnlyList<MOLLink> _links;
        private readonly IReadOnlyList<MOLSelectionGroup> _groups;
        private readonly IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> _parameters;
        private readonly IReadOnlyDictionary<Guid,MOLStepPolicy> _policies;
        private readonly IReadOnlyDictionary<Guid,MOLStateMachineDefinition> _stateMachines;
        private readonly IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> _ports;
        private readonly IReadOnlyDictionary<Guid,MOLBlockPosition> _positions;
        private readonly IReadOnlyDictionary<string,MOLBlockEndpoint> _flowInputs;
        private readonly IReadOnlyDictionary<string,MOLBlockEndpoint> _flowOutputs;
        private MOCWiringFragment(MOLWiringDefinition definition,Guid[] selected)
        {
            var ids=new HashSet<Guid>(selected);
            _blocks=definition.Blocks.Where(x=>ids.Contains(x.Id)).ToList().AsReadOnly();
            _links=definition.Links.Where(x=>ids.Contains(x.Source.BlockId)&&
                ids.Contains(x.Target.BlockId)).ToList().AsReadOnly();
            _groups=definition.Groups.Where(g=>g.BlockIds.All(ids.Contains)).ToList().AsReadOnly();
            _parameters=new ReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>>(
                definition.Parameters.Where(g=>ids.Contains(g.Key)).ToDictionary(
                    g=>g.Key,g=>g.Value));
            _policies=new ReadOnlyDictionary<Guid,MOLStepPolicy>(
                definition.Policies.Where(g=>ids.Contains(g.Key)).ToDictionary(
                    g=>g.Key,g=>g.Value));
            _stateMachines=new ReadOnlyDictionary<Guid,MOLStateMachineDefinition>(
                definition.StateMachines.Where(g=>ids.Contains(g.Key)).ToDictionary(
                    g=>g.Key,g=>g.Value));
            _ports=new ReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>>(
                _blocks.ToDictionary(x=>x.Id,x=>definition.Ports[x.Id]));
            var layout=new Dictionary<Guid,MOLBlockPosition>();
            double top=20;
            foreach(var block in definition.Blocks)
            {
                var ports=definition.Ports[block.Id];
                int ins=ports.Count(p=>p.Direction!=MPCPortDirection.Output);
                int outs=ports.Count(p=>p.Direction!=MPCPortDirection.Input);
                double height=Math.Max(58,32+22*Math.Max(ins,outs));
                MOLBlockPosition saved;
                if(ids.Contains(block.Id))
                    layout.Add(block.Id,definition.Positions.TryGetValue(block.Id,out saved)?
                        saved:new MOLBlockPosition(20,top));
                top+=height+24;
            }
            _positions=new ReadOnlyDictionary<Guid,MOLBlockPosition>(layout);
            _flowInputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                definition.FlowInputs.Where(x=>ids.Contains(x.Value.BlockId))
                    .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal));
            _flowOutputs=new ReadOnlyDictionary<string,MOLBlockEndpoint>(
                definition.FlowOutputs.Where(x=>ids.Contains(x.Value.BlockId))
                    .ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal));
        }
        public IReadOnlyList<MOLControlBlock> Blocks {get{return _blocks;}}
        public IReadOnlyList<MOLLink> Links {get{return _links;}}
        public IReadOnlyList<MOLSelectionGroup> Groups {get{return _groups;}}
        public IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> Parameters
        {get{return _parameters;}}
        public IReadOnlyDictionary<Guid,MOLStepPolicy> Policies{get{return _policies;}}
        public IReadOnlyDictionary<Guid,MOLStateMachineDefinition> StateMachines
        {get{return _stateMachines;}}
        public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports {get{return _ports;}}
        public IReadOnlyDictionary<Guid,MOLBlockPosition> Positions {get{return _positions;}}
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowInputs {get{return _flowInputs;}}
        public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowOutputs {get{return _flowOutputs;}}
        /// <summary>受V2 Codec和4MB限制的剪贴板传输格式，绝不作为工程真源保存。</summary>
        public byte[] ToPayload()
        {
            var ports=_ports.ToDictionary(p=>p.Key,
                p=>(IEnumerable<MPCPortDescriptor>)p.Value);
            var positions=_positions.ToDictionary(p=>p.Key,p=>p.Value);
            var parameters=_parameters.ToDictionary(p=>p.Key,
                p=>(IDictionary<string,string>)p.Value.ToDictionary(k=>k.Key,k=>k.Value,StringComparer.Ordinal));
            var wiring=new MOLWiringDefinition(_blocks[0].Id,_blocks,ports,_links,
                positions,_groups,parameters,_policies.ToDictionary(x=>x.Key,x=>x.Value),
                _stateMachines.ToDictionary(x=>x.Key,x=>x.Value),
                _flowInputs.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal),
                _flowOutputs.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal));
            return MOLWiringCodec.Serialize(wiring);
        }
        public static MOCWiringFragment FromPayload(byte[] payload)
        {
            var wiring=MOLWiringCodec.Deserialize(payload);
            return Capture(wiring,wiring.Blocks.Select(b=>b.Id));
        }
        public static MOCWiringFragment Capture(MOLWiringDefinition definition,IEnumerable<Guid> selection)
        {
            if(definition==null||selection==null)throw new ArgumentNullException("definition/selection");
            Guid[] ids=selection.Distinct().ToArray();
            if(ids.Length==0||ids.Any(id=>!definition.Blocks.Any(b=>b.Id==id)))
                throw new InvalidOperationException("moc.clipboard.invalid_selection");
            return new MOCWiringFragment(definition,ids);
        }
    }

    public enum MOCBlockAlignment
    {
        Left=1,Right=2,Top=3,Bottom=4,HorizontalCenter=5,VerticalCenter=6
    }

    public sealed class MOCFlowBuilder
    {
        private readonly List<MOLControlBlock> _blocks=new List<MOLControlBlock>();
        private readonly List<MOLLink> _links=new List<MOLLink>();
        private readonly Dictionary<Guid,IEnumerable<MPCPortDescriptor>> _ports=
            new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
        private readonly Dictionary<Guid,MOLBlockPosition> _positions=
            new Dictionary<Guid,MOLBlockPosition>();
        private readonly List<MOLSelectionGroup> _groups=new List<MOLSelectionGroup>();
        private readonly Dictionary<Guid,IDictionary<string,string>> _parameters=
            new Dictionary<Guid,IDictionary<string,string>>();
        private readonly Dictionary<Guid,MOLStepPolicy> _policies=
            new Dictionary<Guid,MOLStepPolicy>();
        private readonly Dictionary<Guid,MOLStateMachineDefinition> _stateMachines=
            new Dictionary<Guid,MOLStateMachineDefinition>();
        private readonly Dictionary<string,MOLBlockEndpoint> _flowInputs=
            new Dictionary<string,MOLBlockEndpoint>(StringComparer.Ordinal);
        private readonly Dictionary<string,MOLBlockEndpoint> _flowOutputs=
            new Dictionary<string,MOLBlockEndpoint>(StringComparer.Ordinal);
        private Guid _entry;
        private readonly MEMProjectSnapshot _editingSnapshot;
        private MOCFlowBuilder(MOLWiringDefinition current,MEMProjectSnapshot snapshot=null)
        {
            _editingSnapshot=snapshot;
            if(current==null)return;
            _entry=current.EntryBlockId;
            _blocks.AddRange(current.Blocks);
            _links.AddRange(current.Links);
            foreach(var pair in current.Ports)_ports.Add(pair.Key,pair.Value.ToArray());
            foreach(var pair in current.Positions)_positions.Add(pair.Key,pair.Value);
            _groups.AddRange(current.Groups);
            foreach(var pair in current.Parameters)
                _parameters.Add(pair.Key,pair.Value.ToDictionary(k=>k.Key,k=>k.Value,StringComparer.Ordinal));
            foreach(var pair in current.Policies)_policies.Add(pair.Key,pair.Value);
            foreach(var pair in current.StateMachines)_stateMachines.Add(pair.Key,pair.Value);
            foreach(var pair in current.FlowInputs)_flowInputs.Add(pair.Key,pair.Value);
            foreach(var pair in current.FlowOutputs)_flowOutputs.Add(pair.Key,pair.Value);
        }
        /// <summary>公开子 Content 输入端口，仍使用现有内部 Port Schema 与 Logic Aspect。</summary>
        public void ExposeInput(string publicName,Guid blockId,string inputPort)
        {
            if(string.IsNullOrWhiteSpace(publicName)||_flowInputs.ContainsKey(publicName))
                throw new InvalidOperationException("moc.flow_input_duplicate");
            _flowInputs.Add(publicName,new MOLBlockEndpoint(blockId,inputPort));
        }
        /// <summary>将一个 Data.Output 发布给父流程；不能直接绕过子流程提交屏障。</summary>
        public void ExposeOutput(string publicName,Guid blockId,string outputPort)
        {
            if(string.IsNullOrWhiteSpace(publicName)||_flowOutputs.ContainsKey(publicName))
                throw new InvalidOperationException("moc.flow_output_duplicate");
            _flowOutputs.Add(publicName,new MOLBlockEndpoint(blockId,outputPort));
        }
        public void RemoveExposedInput(string publicName)
        {
            if(!_flowInputs.Remove(publicName))throw new InvalidOperationException("moc.flow_input_missing");
        }
        public void RemoveExposedOutput(string publicName)
        {
            if(!_flowOutputs.Remove(publicName))throw new InvalidOperationException("moc.flow_output_missing");
        }
        public Guid LastBlockId{get{return _blocks.Count==0?Guid.Empty:_blocks[_blocks.Count-1].Id;}}
        /// <summary>
        /// R06：直接使用MEM Content的公开Logic端口生成调用节点，避免手写
        /// moc.invoke:Guid或重复维护调用口Schema。父Content引用仍由MEM负责。
        /// 真实三层及多实例执行由同一MOC/MRK管线负责，端口以当前工程快照为准。
        /// 仍需调用者通过MEM登记ContentRef；编译期会拒绝缺失引用、未公开端口
        /// 与必需输入遗漏。InvokeContent只负责便利的类型化编辑，不绕过验证。
        /// </summary>
        public Guid InvokeContent(MEMContent content)
        {
            if(content==null)throw new ArgumentNullException("content");
            if(_editingSnapshot==null)
                throw new InvalidOperationException("moc.invoke.requires_engineering_session");
            MEMNodeSnapshot target=_editingSnapshot.Find(content.Id);
            if(target==null||target.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("moc.invoke.foreign_content");
            MOLWiringDefinition definition=MOLWiringCodec.Get(target);
            if(definition==null)
            {
                // 结构化V1没有自由连线公开端口，不复制V1树为另一份图。
                // 允许声明零端口Content引用；MOC未支持的跨Schema执行须明确拒绝。
                MOLLogicDefinition structured=MOLLogicCodec.Get(target);
                if(structured==null)
                    throw new InvalidOperationException("moc.invoke.missing_logic");
                // 公共端口的唯一真源是MEMContent已有MPCPort声明，不复制到V1 Logic。
                var declared=target.Ports.Where(x=>x.IsPublic&&
                    (x.Kind==MPCPortKind.Data||x.Kind==MPCPortKind.Stream))
                    .Select(x=>new MPCPortDescriptor(x.MemberId,x.Direction,
                        x.TypeId,x.Kind,x.Multiplicity,x.Contract,required:x.Required))
                    .ToArray();
                return Step("moc.invoke:"+content.Id.ToString("N"),declared);
            }
            definition.ValidateExecutable();
            var published=new List<MPCPortDescriptor>();
            foreach(var pair in definition.FlowInputs.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                MPCPortDescriptor source=definition.Ports[pair.Value.BlockId].Single(p=>
                    p.MemberId==pair.Value.PortMemberId);
                published.Add(new MPCPortDescriptor(pair.Key,MPCPortDirection.Input,
                    source.TypeId,MPCPortKind.Data,source.Multiplicity,source.Contract,
                    required:source.Required));
            }
            foreach(var pair in definition.FlowOutputs.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                MPCPortDescriptor source=definition.Ports[pair.Value.BlockId].Single(p=>
                    p.MemberId==pair.Value.PortMemberId);
                published.Add(new MPCPortDescriptor(pair.Key,MPCPortDirection.Output,
                    source.TypeId,MPCPortKind.Data,source.Multiplicity,source.Contract));
            }
            return Step("moc.invoke:"+content.Id.ToString("N"),published);
        }

        public Guid Step(MOCCapabilityDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");
            return Step(descriptor.Id,descriptor.Ports);
        }
        public Guid Step(string capabilityId,IEnumerable<MPCPortDescriptor> extraPorts=null)
        {
            if(string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("能力ID不能为空","capabilityId");
            var action=MOLControlBlock.Action(capabilityId);
            var declared=new List<MPCPortDescriptor>{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",
                    MPCPortKind.Control,MPCPortMultiplicity.Many),
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control)
            };
            if(extraPorts!=null)declared.AddRange(extraPorts);
            if(declared.Any(x=>x==null)||declared.GroupBy(x=>x.MemberId,StringComparer.Ordinal)
                .Any(x=>x.Count()>1))
                throw new ArgumentException("重复端口","extraPorts");
            _blocks.Add(action);_ports.Add(action.Id,declared.ToArray());
            if(_entry==Guid.Empty)_entry=action.Id;
            return action.Id;
        }
        public Guid Connect(Guid source,string outPort,Guid target,string inPort,
            MPCPortKind kind=MPCPortKind.Control)
        {
            var link=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(source,outPort),
                new MOLBlockEndpoint(target,inPort),kind);
            _links.Add(link);return link.Id;
        }
        /// <summary>G05a：Control出口基于上游结果判断，条件只属于原有Logic边。</summary>
        public Guid ConnectConditional(Guid source,string outPort,Guid target,string inPort,
            string guard)
        {
            if(guard==null)throw new ArgumentNullException("guard");
            var link=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(source,outPort),
                new MOLBlockEndpoint(target,inPort),MPCPortKind.Control,guard);
            _links.Add(link);
            return link.Id;
        }
        public void Disconnect(Guid linkId)
        {
            int removed=_links.RemoveAll(x=>x.Id==linkId);
            if(removed!=1)throw new InvalidOperationException("moc.wiring.link_missing");
        }
        public void Remove(Guid blockId)
        {
            if(_blocks.RemoveAll(x=>x.Id==blockId)!=1)
                throw new InvalidOperationException("moc.wiring.block_missing");
            _ports.Remove(blockId);
            _positions.Remove(blockId);
            _parameters.Remove(blockId);
            _policies.Remove(blockId);
            _stateMachines.Remove(blockId);
            foreach(string key in _flowInputs.Where(x=>x.Value.BlockId==blockId).Select(x=>x.Key).ToArray())
                _flowInputs.Remove(key);
            foreach(string key in _flowOutputs.Where(x=>x.Value.BlockId==blockId).Select(x=>x.Key).ToArray())
                _flowOutputs.Remove(key);
            _links.RemoveAll(x=>x.Source.BlockId==blockId||x.Target.BlockId==blockId);
            RemoveGroupMembers(new HashSet<Guid>(new[]{blockId}));
            if(_entry==blockId)_entry=_blocks.Count==0?Guid.Empty:_blocks[0].Id;
        }

        /// <summary>查询正式端口，UI可按PortKind/Direction显示候选，不写第二份目录。</summary>
        public IReadOnlyList<MPCPortDescriptor> GetPorts(Guid blockId)
        {
            IEnumerable<MPCPortDescriptor> source;
            if(!_ports.TryGetValue(blockId,out source))
                throw new InvalidOperationException("moc.wiring.block_missing");
            return source.ToList().AsReadOnly();
        }
        public IReadOnlyList<MOLControlBlock> Blocks
        {get{return _blocks.ToList().AsReadOnly();}}
        public IReadOnlyList<MOLLink> Links
        {get{return _links.ToList().AsReadOnly();}}
        public void AddPort(Guid blockId,MPCPortDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");
            IEnumerable<MPCPortDescriptor> existing;
            if(!_ports.TryGetValue(blockId,out existing))
                throw new InvalidOperationException("moc.wiring.block_missing");
            var updated=existing.ToList();
            if(updated.Any(x=>x.MemberId==descriptor.MemberId))
                throw new InvalidOperationException("moc.wiring.duplicate_port");
            updated.Add(descriptor);
            _ports[blockId]=updated.ToArray();
        }
        /// <summary>更改现有端口契约；连线不兼容时Build拒绝整笔事务。</summary>
        public void ReplacePort(Guid blockId,MPCPortDescriptor descriptor)
        {
            if(descriptor==null)throw new ArgumentNullException("descriptor");
            IEnumerable<MPCPortDescriptor> found;
            if(!_ports.TryGetValue(blockId,out found))
                throw new InvalidOperationException("moc.wiring.block_missing");
            var ports=found.ToList();
            int index=ports.FindIndex(p=>p.MemberId==descriptor.MemberId);
            if(index<0)throw new InvalidOperationException("moc.wiring.port_missing");
            ports[index]=descriptor;
            _ports[blockId]=ports.ToArray();
        }
        /// <summary>执行策略在V2 Logic内事务管理，由MRKExecutionPolicy负责实际执行。</summary>
        public Guid AddStateMachine(MOLStateMachineDefinition definition)
        {
            if(definition==null)throw new ArgumentNullException("definition");
            Guid id=Step("moc.state-machine");
            _stateMachines.Add(id,definition);
            return id;
        }
        public void SetStateMachine(Guid blockId,MOLStateMachineDefinition definition)
        {
            if(definition==null)throw new ArgumentNullException("definition");
            if(!_blocks.Any(b=>b.Id==blockId&&b.CapabilityId=="moc.state-machine"))
                throw new InvalidOperationException("moc.state.block_missing");
            _stateMachines[blockId]=definition;
        }
        public void SetWorkflowPolicy(Guid blockId,MOLStepPolicy policy)
        {
            if(policy==null)throw new ArgumentNullException("policy");
            if(!_blocks.Any(x=>x.Id==blockId))
                throw new InvalidOperationException("moc.wiring.block_missing");
            _policies[blockId]=policy;
        }
        /// <summary>
        /// 统一工程逻辑Aspect保存的组引用，和重试/超时策略采用相同MEM事务；
        /// 仅存原组ID，Stage/WholeFlow/Step容量定义不复制到节点。
        /// </summary>
        public void SetExecutionGroup(Guid blockId,string groupId)
        {
            if(!_blocks.Any(b=>b.Id==blockId))
                throw new InvalidOperationException("moc.wiring.block_missing");
            MOLStepPolicy current;
            _policies.TryGetValue(blockId,out current);
            MOLStepPolicy updated=new MOLStepPolicy(
                current==null?1:current.MaxAttempts,
                current==null?0:current.RetryDelayMs,
                current==null?0:current.TimeoutMs,
                current!=null&&current.Idempotent,groupId);
            if(updated.ExecutionGroup.Length==0&&updated.MaxAttempts==1&&
                updated.RetryDelayMs==0&&updated.TimeoutMs==0&&!updated.Idempotent)
                _policies.Remove(blockId);
            else _policies[blockId]=updated;
        }

        public void ClearWorkflowPolicy(Guid blockId)
        {
            if(!_policies.Remove(blockId))
                throw new InvalidOperationException("moc.wiring.policy_missing");
        }
        public void SetParameter(Guid blockId,string memberId,string text)
        {
            IEnumerable<MPCPortDescriptor> ports;
            if(!_ports.TryGetValue(blockId,out ports))
                throw new InvalidOperationException("moc.wiring.block_missing");
            if(text==null)throw new ArgumentNullException("text");
            if(!ports.Any(p=>p.MemberId==memberId&&p.Kind==MPCPortKind.Data&&
                p.Direction==MPCPortDirection.Input))
                throw new InvalidOperationException("moc.wiring.parameter_port_missing");
            IDictionary<string,string> values;
            if(!_parameters.TryGetValue(blockId,out values))
            {
                values=new Dictionary<string,string>(StringComparer.Ordinal);
                _parameters.Add(blockId,values);
            }
            values[memberId]=text;
        }
        public void RemoveParameter(Guid blockId,string memberId)
        {
            IDictionary<string,string> values;
            if(!_parameters.TryGetValue(blockId,out values)||!values.Remove(memberId))
                throw new InvalidOperationException("moc.wiring.parameter_missing");
            if(values.Count==0)_parameters.Remove(blockId);
        }
        public void RemovePort(Guid blockId,string memberId)
        {
            IEnumerable<MPCPortDescriptor> existing;
            if(!_ports.TryGetValue(blockId,out existing))
                throw new InvalidOperationException("moc.wiring.block_missing");
            if(_links.Any(x=>(x.Source.BlockId==blockId&&x.Source.PortMemberId==memberId)||
                (x.Target.BlockId==blockId&&x.Target.PortMemberId==memberId)))
                throw new InvalidOperationException("moc.wiring.port_in_use");
            var updated=existing.Where(x=>x.MemberId!=memberId).ToArray();
            if(updated.Length==existing.Count())
                throw new InvalidOperationException("moc.wiring.port_missing");
            _ports[blockId]=updated;
            IDictionary<string,string> values;
            if(_parameters.TryGetValue(blockId,out values))
            {
                values.Remove(memberId);
                if(values.Count==0)_parameters.Remove(blockId);
            }
        }
        /// <summary>只修改能力身份，不更换BlockId或已有Port/Link身份。</summary>
        public void SetCapability(Guid blockId,string capabilityId)
        {
            if(string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("moc.wiring.capability_missing","capabilityId");
            int index=_blocks.FindIndex(x=>x.Id==blockId);
            if(index<0)throw new InvalidOperationException("moc.wiring.block_missing");
            _blocks[index]=new MOLControlBlock(blockId,MOLControlKind.Action,capabilityId);
        }
        public void SetEntry(Guid blockId)
        {
            if(!_blocks.Any(x=>x.Id==blockId))
                throw new InvalidOperationException("moc.wiring.entry_missing");
            _entry=blockId;
        }
        /// <summary>节点布局跟Logic定义同事务保存；不会影响控制和数据连接。</summary>
        public void Move(Guid blockId,double x,double y)
        {
            if(!_blocks.Any(b=>b.Id==blockId))
                throw new InvalidOperationException("moc.wiring.block_missing");
            _positions[blockId]=new MOLBlockPosition(x,y);
        }
        /// <summary>画布编辑组不能更改执行语义，必须至少两个节点且不得重叠。</summary>
        public Guid Group(IEnumerable<Guid> ids,string name)
        {
            Guid[] members=ResolveSelection(ids);
            if(members.Length<2)throw new InvalidOperationException("moc.wiring.group_needs_two");
            if(_groups.Any(g=>g.BlockIds.Any(id=>members.Contains(id))))
                throw new InvalidOperationException("moc.wiring.group_overlap");
            var group=new MOLSelectionGroup(Guid.NewGuid(),name,members);
            _groups.Add(group);
            return group.Id;
        }
        public void Ungroup(Guid id)
        {
            if(_groups.RemoveAll(g=>g.Id==id)!=1)
                throw new InvalidOperationException("moc.wiring.group_missing");
        }
        public IReadOnlyList<MOLSelectionGroup> Groups
        {get{return _groups.ToList().AsReadOnly();}}
        private void RemoveGroupMembers(HashSet<Guid> removed)
        {
            var groups=_groups.ToArray();
            _groups.Clear();
            foreach(var group in groups)
            {
                var remaining=group.BlockIds.Where(id=>!removed.Contains(id)).ToArray();
                if(remaining.Length>=2)
                    _groups.Add(new MOLSelectionGroup(group.Id,group.Name,remaining));
            }
        }

        private MOLBlockPosition EffectivePosition(Guid id)
        {
            MOLBlockPosition stored;
            if(_positions.TryGetValue(id,out stored))return stored;
            double top=20;
            foreach(var block in _blocks)
            {
                if(block.Id==id)return new MOLBlockPosition(20,top);
                var ports=_ports[block.Id].ToArray();
                int ins=ports.Count(p=>p.Direction!=MPCPortDirection.Output);
                int outs=ports.Count(p=>p.Direction!=MPCPortDirection.Input);
                top+=Math.Max(58,32+22*Math.Max(ins,outs))+24;
            }
            throw new InvalidOperationException("moc.wiring.block_missing");
        }
        private MOLBlockPosition GetSize(Guid id)
        {
            var ports=_ports[id].ToArray();
            int ins=ports.Count(p=>p.Direction!=MPCPortDirection.Output);
            int outs=ports.Count(p=>p.Direction!=MPCPortDirection.Input);
            return new MOLBlockPosition(260,Math.Max(58,32+22*Math.Max(ins,outs)));
        }
        private Guid[] ResolveSelection(IEnumerable<Guid> ids)
        {
            if(ids==null)throw new ArgumentNullException("ids");
            Guid[] selected=ids.Distinct().ToArray();
            if(selected.Length==0||selected.Any(id=>!_ports.ContainsKey(id)))
                throw new InvalidOperationException("moc.wiring.invalid_selection");
            return selected;
        }
        /// <summary>统一事务内移动多节点，失败则整个工程编辑回滚。</summary>
        public void MoveMany(IEnumerable<Guid> ids,double dx,double dy)
        {
            var selected=ResolveSelection(ids);
            var updates=new Dictionary<Guid,MOLBlockPosition>();
            foreach(Guid id in selected)
            {
                var old=EffectivePosition(id);
                updates.Add(id,new MOLBlockPosition(old.X+dx,old.Y+dy));
            }
            foreach(var pair in updates)_positions[pair.Key]=pair.Value;
        }
        /// <summary>按真实块尺寸对齐，不改数据连接及执行顺序。</summary>
        public void Align(IEnumerable<Guid> ids,MOCBlockAlignment alignment)
        {
            var selected=ResolveSelection(ids);
            if(!Enum.IsDefined(typeof(MOCBlockAlignment),alignment))
                throw new ArgumentOutOfRangeException("alignment");
            var coords=selected.ToDictionary(id=>id,id=>EffectivePosition(id));
            var sizes=selected.ToDictionary(id=>id,id=>GetSize(id));
            double anchor=0;
            switch(alignment)
            {
                case MOCBlockAlignment.Left: anchor=coords.Values.Min(p=>p.X);break;
                case MOCBlockAlignment.Right: anchor=selected.Max(id=>coords[id].X+sizes[id].X);break;
                case MOCBlockAlignment.Top: anchor=coords.Values.Min(p=>p.Y);break;
                case MOCBlockAlignment.Bottom: anchor=selected.Max(id=>coords[id].Y+sizes[id].Y);break;
                case MOCBlockAlignment.HorizontalCenter: anchor=selected.Average(id=>coords[id].X+sizes[id].X/2);break;
                case MOCBlockAlignment.VerticalCenter: anchor=selected.Average(id=>coords[id].Y+sizes[id].Y/2);break;
            }
            var updated=new Dictionary<Guid,MOLBlockPosition>();
            foreach(Guid id in selected)
            {
                double x=coords[id].X,y=coords[id].Y;
                switch(alignment)
                {
                    case MOCBlockAlignment.Left:x=anchor;break;
                    case MOCBlockAlignment.Right:x=anchor-sizes[id].X;break;
                    case MOCBlockAlignment.Top:y=anchor;break;
                    case MOCBlockAlignment.Bottom:y=anchor-sizes[id].Y;break;
                    case MOCBlockAlignment.HorizontalCenter:x=anchor-sizes[id].X/2;break;
                    case MOCBlockAlignment.VerticalCenter:y=anchor-sizes[id].Y/2;break;
                }
                updated.Add(id,new MOLBlockPosition(x,y));
            }
            foreach(var pair in updated)_positions[pair.Key]=pair.Value;
        }
        /// <summary>同一坐标维度均匀分布；只写位置，不改变连接图。</summary>
        public void Distribute(IEnumerable<Guid> ids,bool horizontal)
        {
            Guid[] selected=ResolveSelection(ids);
            if(selected.Length<3)throw new InvalidOperationException("moc.wiring.distribute_needs_three");
            var ordered=selected.OrderBy(id=>horizontal?EffectivePosition(id).X:
                EffectivePosition(id).Y).ThenBy(id=>id).ToArray();
            double start=horizontal?EffectivePosition(ordered[0]).X:
                EffectivePosition(ordered[0]).Y;
            double end=horizontal?EffectivePosition(ordered[ordered.Length-1]).X:
                EffectivePosition(ordered[ordered.Length-1]).Y;
            double step=(end-start)/(ordered.Length-1);
            var updates=new Dictionary<Guid,MOLBlockPosition>();
            for(int i=0;i<ordered.Length;i++)
            {
                var position=EffectivePosition(ordered[i]);
                double value=start+step*i;
                updates.Add(ordered[i],horizontal?
                    new MOLBlockPosition(value,position.Y):
                    new MOLBlockPosition(position.X,value));
            }
            foreach(var pair in updates)_positions[pair.Key]=pair.Value;
        }
        /// <summary>矩阵自动排列，无持久Graph、无执行顺序副作用。</summary>
        public void ArrangeGrid(IEnumerable<Guid> ids)
        {
            var selected=new HashSet<Guid>(ResolveSelection(ids));
            var blocks=_blocks.Where(b=>selected.Contains(b.Id)).ToArray();
            int cols=Math.Min(4,(int)Math.Ceiling(Math.Sqrt(blocks.Length)));
            double left=blocks.Min(b=>EffectivePosition(b.Id).X);
            double top=blocks.Min(b=>EffectivePosition(b.Id).Y);
            double y=top;
            var updates=new Dictionary<Guid,MOLBlockPosition>();
            for(int offset=0;offset<blocks.Length;offset+=cols)
            {
                var row=blocks.Skip(offset).Take(cols).ToArray();
                double rowHeight=row.Max(b=>GetSize(b.Id).Y);
                for(int j=0;j<row.Length;j++)
                    updates.Add(row[j].Id,new MOLBlockPosition(left+j*340,y));
                y+=rowHeight+28;
            }
            foreach(var pair in updates)_positions[pair.Key]=pair.Value;
        }

        /// <summary>批量删除块及其边界连接，至少保留一个有效入口节点。</summary>
        public void RemoveMany(IEnumerable<Guid> ids)
        {
            var selected=ResolveSelection(ids);
            var removed=new HashSet<Guid>(selected);
            _blocks.RemoveAll(x=>removed.Contains(x.Id));
            _links.RemoveAll(x=>removed.Contains(x.Source.BlockId)||removed.Contains(x.Target.BlockId));
            foreach(Guid id in selected){_ports.Remove(id);_positions.Remove(id);_parameters.Remove(id);_policies.Remove(id);_stateMachines.Remove(id);}
            foreach(string key in _flowInputs.Where(x=>removed.Contains(x.Value.BlockId)).Select(x=>x.Key).ToArray())
                _flowInputs.Remove(key);
            foreach(string key in _flowOutputs.Where(x=>removed.Contains(x.Value.BlockId)).Select(x=>x.Key).ToArray())
                _flowOutputs.Remove(key);
            RemoveGroupMembers(removed);
            if(removed.Contains(_entry))_entry=_blocks.Count==0?Guid.Empty:_blocks[0].Id;
        }
        /// <summary>跨Content粘贴快照；新建Block/Link ID，仅复制选区内部边。</summary>
        public IReadOnlyDictionary<Guid,Guid> Paste(MOCWiringFragment fragment,double dx=40,double dy=40)
        {
            if(fragment==null)throw new ArgumentNullException("fragment");
            var ids=fragment.Blocks.ToDictionary(x=>x.Id,x=>Guid.NewGuid());
            var addedPositions=new Dictionary<Guid,MOLBlockPosition>();
            foreach(var source in fragment.Blocks)
            {
                MOLBlockPosition point=fragment.Positions[source.Id];
                addedPositions.Add(ids[source.Id],new MOLBlockPosition(point.X+dx,point.Y+dy));
            }
            foreach(var source in fragment.Blocks)
            {
                Guid newId=ids[source.Id];
                _blocks.Add(new MOLControlBlock(newId,MOLControlKind.Action,source.CapabilityId));
                _ports.Add(newId,fragment.Ports[source.Id].ToArray());
                _positions.Add(newId,addedPositions[newId]);
            }
            foreach(var pair in fragment.Parameters)
                _parameters.Add(ids[pair.Key],
                    pair.Value.ToDictionary(k=>k.Key,k=>k.Value,StringComparer.Ordinal));
            foreach(var pair in fragment.Policies)_policies.Add(ids[pair.Key],pair.Value);
            foreach(var pair in fragment.StateMachines)
                _stateMachines.Add(ids[pair.Key],pair.Value);
            foreach(var group in fragment.Groups)
                _groups.Add(new MOLSelectionGroup(Guid.NewGuid(),group.Name,
                    group.BlockIds.Select(id=>ids[id])));
            foreach(var link in fragment.Links)
                _links.Add(new MOLLink(Guid.NewGuid(),
                    new MOLBlockEndpoint(ids[link.Source.BlockId],link.Source.PortMemberId),
                    new MOLBlockEndpoint(ids[link.Target.BlockId],link.Target.PortMemberId),
                    link.Kind,link.ConditionId,link.Order));
            if(_entry==Guid.Empty)_entry=ids[fragment.Blocks[0].Id];
            return new ReadOnlyDictionary<Guid,Guid>(ids);
        }
        public MOLWiringDefinition Build()
        {return new MOLWiringDefinition(_entry,_blocks,_ports,_links,_positions,_groups,
            _parameters,_policies,_stateMachines,_flowInputs,_flowOutputs);}
        /// <summary>
        /// G04-F：提取无Data边界的连续Control动作段为真实MEMContent，
        /// 在同一MEM事务中替换为调用节点。Data参数跨Content待独立契约支持，明确拒绝。
        /// </summary>
        public static MEMContent ExtractLinearContent(MEMEngineeringSession session,
            MEMContent parent,IEnumerable<Guid> selectedBlocks,string name)
        {
            if(session==null||parent==null||selectedBlocks==null)
                throw new ArgumentNullException("session/parent/selectedBlocks");
            Guid[] requested=selectedBlocks.Distinct().ToArray();
            if(requested.Length<2)throw new InvalidOperationException("moc.extract.needs_two");
            int revision=session.Revision;
            var node=session.GetSnapshot().Find(parent.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("moc.extract.foreign_content");
            var definition=MOLWiringCodec.Get(node);
            if(definition==null)throw new InvalidOperationException("moc.extract.requires_v2");
            var set=new HashSet<Guid>(requested);
            if(requested.Any(id=>!definition.Blocks.Any(b=>b.Id==id))||
                definition.Links.Any(l=>l.Kind!=MPCPortKind.Control&&
                    (set.Contains(l.Source.BlockId)||set.Contains(l.Target.BlockId)))||
                definition.StateMachines.Keys.Any(set.Contains)||
                definition.Groups.Any(g=>g.BlockIds.Any(set.Contains)&&
                    g.BlockIds.Any(id=>!set.Contains(id))))
                throw new InvalidOperationException("moc.extract.unsupported_boundary");
            var inside=definition.Links.Where(l=>set.Contains(l.Source.BlockId)&&
                set.Contains(l.Target.BlockId)).ToArray();
            var incoming=definition.Links.Where(l=>!set.Contains(l.Source.BlockId)&&
                set.Contains(l.Target.BlockId)).ToArray();
            var outgoing=definition.Links.Where(l=>set.Contains(l.Source.BlockId)&&
                !set.Contains(l.Target.BlockId)).ToArray();
            if(inside.Length!=requested.Length-1||incoming.Length>1||outgoing.Length>1||
                inside.GroupBy(l=>l.Source.BlockId).Any(g=>g.Count()>1)||
                inside.GroupBy(l=>l.Target.BlockId).Any(g=>g.Count()>1))
                throw new InvalidOperationException("moc.extract.requires_linear_control");
            Guid[] entryCandidates=requested.Where(id=>!inside.Any(l=>l.Target.BlockId==id)).ToArray();
            Guid[] exitCandidates=requested.Where(id=>!inside.Any(l=>l.Source.BlockId==id)).ToArray();
            if(entryCandidates.Length!=1||exitCandidates.Length!=1||
                (incoming.Length==1&&incoming[0].Target.BlockId!=entryCandidates[0])||
                (outgoing.Length==1&&outgoing[0].Source.BlockId!=exitCandidates[0])||
                (definition.EntryBlockId!=entryCandidates[0]&&incoming.Length==0))
                throw new InvalidOperationException("moc.extract.boundary_invalid");
            if(definition.Blocks.Count==requested.Length&&incoming.Length+outgoing.Length!=0)
                throw new InvalidOperationException("moc.extract.invalid_scope");
            var fragment=MOCWiringFragment.Capture(definition,requested);
            var ports=fragment.Ports.ToDictionary(p=>p.Key,
                p=>(IEnumerable<MPCPortDescriptor>)p.Value);
            var positions=fragment.Positions.ToDictionary(p=>p.Key,p=>p.Value);
            var childParameters=fragment.Parameters.ToDictionary(p=>p.Key,
                p=>(IDictionary<string,string>)p.Value.ToDictionary(v=>v.Key,v=>v.Value,
                    StringComparer.Ordinal));
            var child=new MOLWiringDefinition(entryCandidates[0],fragment.Blocks,ports,
                fragment.Links,positions,fragment.Groups,childParameters,
                fragment.Policies.ToDictionary(x=>x.Key,x=>x.Value));
            child.ValidateExecutable();
            var edit=new MOCFlowBuilder(definition);
            edit.RemoveMany(requested);
            Guid callId=Guid.NewGuid();
            var call=new MOLControlBlock(callId,MOLControlKind.Action,
                "moc.invoke:"+"00000000000000000000000000000000");
            // 占位ID在同一事务生成目标Content后修正，外部代码不接触半成品。
            var location=positions[entryCandidates[0]];
            var oldIn=incoming.Length==0?null:incoming[0];
            var oldOut=outgoing.Length==0?null:outgoing[0];
            MEMContent created=null;
            session.Edit("提取可复用Content子流程",revision,e=>
            {
                created=e.AddContent(session.Project,name);
                e.Reference(parent,created,name+"调用");
                var realCall=new MOLControlBlock(callId,MOLControlKind.Action,
                    "moc.invoke:"+created.Id.ToString("N"));
                edit._blocks.Add(realCall);
                edit._ports.Add(callId,new MPCPortDescriptor[]{
                    new MPCPortDescriptor("Start",MPCPortDirection.Input,
                        "signal",MPCPortKind.Control,MPCPortMultiplicity.Many),
                    new MPCPortDescriptor("Done",MPCPortDirection.Output,
                        "signal",MPCPortKind.Control)});
                edit._positions.Add(callId,location);
                if(definition.EntryBlockId==entryCandidates[0])edit._entry=callId;
                if(oldIn!=null)edit._links.Add(new MOLLink(Guid.NewGuid(),
                    oldIn.Source,new MOLBlockEndpoint(callId,"Start"),MPCPortKind.Control));
                if(oldOut!=null)edit._links.Add(new MOLLink(Guid.NewGuid(),
                    new MOLBlockEndpoint(callId,"Done"),oldOut.Target,MPCPortKind.Control));
                e.SetAspect(created,MOLWiringCodec.CreateAspect(child));
                e.DefinePort(created,"Start",MPCPortDirection.Input,"signal",
                    MPCPortKind.Control,MPCPortMultiplicity.Many,null,true);
                e.DefinePort(created,"Done",MPCPortDirection.Output,"signal",
                    MPCPortKind.Control,MPCPortMultiplicity.Many,null,true);
                e.SetAspect(parent,MOLWiringCodec.CreateAspect(edit.Build()));
                return true;
            }).RequireValue();
            return created;
        }

        public static void Edit(MEMEngineeringSession session,MEMContent content,
            Action<MOCFlowBuilder> configure)
        {
            if(session==null||content==null||configure==null)
                throw new ArgumentNullException("session/content/configure");
            int revision=session.Revision;
            MEMProjectSnapshot snapshot=session.GetSnapshot();
            MEMNodeSnapshot node=snapshot.Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("moc.wiring.foreign_content");
            MOLWiringDefinition old=MOLWiringCodec.Get(node);
            if(old==null&&MOLLogicCodec.Get(node)!=null)
                throw new InvalidOperationException("moc.wiring.legacy_requires_explicit_conversion");
            var builder=new MOCFlowBuilder(old,snapshot);
            configure(builder);
            var aspect=MOLWiringCodec.CreateAspect(builder.Build());
            session.Edit("编辑自由编排",revision,e=>{e.SetAspect(content,aspect);return true;})
                .RequireValue();
        }
    }

}
