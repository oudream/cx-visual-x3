using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    public sealed class MOCStepOrigin
    {
        internal MOCStepOrigin(string step, MRIObjectAddress address, Guid block,
            IEnumerable<Guid> callPath, int? iteration, string role)
        {
            StepId = step; ContentAddress = address; BlockId = block;
            CallPath = Array.AsReadOnly(callPath.ToArray()); Iteration = iteration; Role = role;
        }
        public string StepId { get; private set; }
        public MRIObjectAddress ContentAddress { get; private set; }
        public Guid BlockId { get; private set; }
        public IReadOnlyList<Guid> CallPath { get; private set; }
        public int? Iteration { get; private set; }
        public string Role { get; private set; }
    }

    public sealed class MOCSourceMap
    {
        private readonly IReadOnlyDictionary<string, MOCStepOrigin> _steps;
        internal MOCSourceMap(IEnumerable<MOCStepOrigin> origins)
        {
            _steps = new ReadOnlyDictionary<string, MOCStepOrigin>(
                origins.ToDictionary(s => s.StepId, s => s, StringComparer.Ordinal));
        }
        public IReadOnlyDictionary<string, MOCStepOrigin> Steps { get { return _steps; } }
        public bool TryResolve(string compiledStepId, out MOCStepOrigin origin)
        { return _steps.TryGetValue(compiledStepId, out origin); }
        public IList<MOCStepOrigin> Find(Guid blockId)
        {
            // 循环同一Action会生成多个Step；按真实迭代序号稳定呈现，
            // 而不是按编译ID的字典序列出（该ID包含运行结构前缀）。
            return _steps.Values.Where(o => o.BlockId == blockId)
                .OrderBy(o => o.Iteration.HasValue ? 0 : 1)
                .ThenBy(o => o.Iteration ?? int.MaxValue)
                .ThenBy(o => o.StepId, StringComparer.Ordinal).ToList().AsReadOnly();
        }
    }

    public sealed class MOCObservedStep
    {
        internal MOCObservedStep(MRKExecutionTransition transition, MOCStepOrigin origin)
        { Transition = transition; Origin = origin; }
        public MRKExecutionTransition Transition { get; private set; }
        public MOCStepOrigin Origin { get; private set; }
    }
    public sealed class MOCTraceView
    {
        internal MOCTraceView(IList<MOCObservedStep> events, long cursor, long lost)
        { Events = new ReadOnlyCollection<MOCObservedStep>(events); NextCursor = cursor; LostCount = lost; }
        public IList<MOCObservedStep> Events { get; private set; }
        public long NextCursor { get; private set; }
        public long LostCount { get; private set; }
    }

    public sealed class MOCCompiledPlan
    {
        internal MOCCompiledPlan(MRKExecutionPlan plan, MOCSourceMap source, Guid projectId, int revision,
            IDictionary<string,object> inputDefaults=null)
        {
            Plan=plan;SourceMap=source;ProjectId=projectId;EngineeringRevision=revision;
            InputDefaults=new ReadOnlyDictionary<string,object>(
                new Dictionary<string,object>(inputDefaults??new Dictionary<string,object>(),
                    StringComparer.Ordinal));
        }
        /// <summary>由MOC已校验字面量生成的每次执行输入默认值；运行请求可显式覆盖。</summary>
        public IReadOnlyDictionary<string,object> InputDefaults{get;private set;}
        public MRKExecutionPlan Plan { get; private set; }
        public MOCSourceMap SourceMap { get; private set; }
        public Guid ProjectId { get; private set; }
        public int EngineeringRevision { get; private set; }
        public MOCTraceView ReadTrace(MRKFlowHandle handle, long afterSequence)
        {
            if (handle == null) throw new ArgumentNullException("handle");
            if (handle.Context.PlanId != Plan.Id) throw new InvalidOperationException("moc.foreign_plan");
            var batch = handle.Context.Trace.ReadSince(afterSequence);
            var result = new List<MOCObservedStep>();
            foreach (var change in batch.Changes)
            {
                MOCStepOrigin from;
                if (!SourceMap.TryResolve(change.StepId, out from))
                    throw new InvalidOperationException("moc.missing_source:" + change.StepId);
                result.Add(new MOCObservedStep(change, from));
            }
            return new MOCTraceView(result, batch.NextCursor, batch.LostCount);
        }
    }

    // Evaluates only typed, bounded declarative operands. Never compiles user text as C#.
    internal static class MOCExpressions
    {
        internal static bool Evaluate(MOLExpression expr, MRKExecutionContext context, int iteration, string resolvedStepId = null)
        {
            if (expr == null) return true;
            object actual = null;
            switch (expr.Source)
            {
                case MOLExpressionSource.Constant: actual = expr.Name; break;
                case MOLExpressionSource.FlowInput:
                    if (!context.Inputs.TryGetValue(expr.Name, out actual))
                        throw new InvalidOperationException("moc.input_missing:" + expr.Name);
                    break;
                case MOLExpressionSource.StepResult:
                    if (resolvedStepId == null || !context.TryGetAbsoluteResult(resolvedStepId, out actual))
                        throw new InvalidOperationException("moc.step_result_missing:" + expr.Name);
                    break;
                case MOLExpressionSource.Iteration: actual = iteration; break;
                default: throw new InvalidOperationException("moc.expression_source");
            }
            if (expr.Comparison == MOLComparison.Truthy)
            {
                if (actual is bool) return (bool)actual;
                if (actual is string)
                {
                    bool value;
                    if (bool.TryParse((string)actual, out value)) return value;
                }
                if (actual is int) return (int)actual != 0;
                throw new InvalidOperationException("moc.truth_type");
            }
            int order;
            decimal numberA, numberB;
            string left = Convert.ToString(actual, CultureInfo.InvariantCulture);
            string right = expr.Expected;
            bool booleanA, booleanB;
            if (bool.TryParse(left, out booleanA) && bool.TryParse(right, out booleanB))
                order = booleanA.CompareTo(booleanB);
            else if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out numberA)
                && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out numberB))
                order = numberA.CompareTo(numberB);
            else order = string.CompareOrdinal(left, right);
            switch (expr.Comparison)
            {
                case MOLComparison.Equal: return order == 0;
                case MOLComparison.NotEqual: return order != 0;
                case MOLComparison.LessThan: return order < 0;
                case MOLComparison.LessOrEqual: return order <= 0;
                case MOLComparison.GreaterThan: return order > 0;
                case MOLComparison.GreaterOrEqual: return order >= 0;
                default: throw new InvalidOperationException("moc.expression_compare");
            }
        }
    }

    internal sealed class MOCResolver : IMRKCapabilityResolver
    {
        private readonly IMRKCapabilityResolver _underlying;
        private readonly Dictionary<string, MRKResolvedCapability> _internal =
            new Dictionary<string, MRKResolvedCapability>(StringComparer.Ordinal);
        internal MOCResolver(IMRKCapabilityResolver underlying)
        {
            _underlying = underlying;
            _internal.Add("moc.internal.noop", new MRKResolvedCapability(
                (c,t) => Task.FromResult((object)true), new MRKExecutionOutput(typeof(bool))));
        }
        internal string Register(string id, Func<MRKExecutionContext, object> execute, Type type)
        {
            if (_internal.ContainsKey(id)) throw new InvalidOperationException("moc.internal_duplicate");
            _internal.Add(id, new MRKResolvedCapability(
                (ctx, token) => { token.ThrowIfCancellationRequested(); return Task.FromResult(execute(ctx)); },
                new MRKExecutionOutput(type, true)));
            return id;
        }
        public bool TryResolve(string capabilityId, out MRKResolvedCapability capability)
        {
            if (capabilityId != null && capabilityId.StartsWith("moc.internal.", StringComparison.Ordinal))
                return _internal.TryGetValue(capabilityId, out capability);
            return _underlying.TryResolve(capabilityId, out capability);
        }
    }


    /// <summary>Developer and Studio share the same Content Logic Edit transaction; no parallel runtime builder.</summary>
    /// <summary>G04-D临时选区快照，不是工程对象树或第二份可执行定义。</summary>
    public sealed class MOCWiringFragment
    {
        private readonly IReadOnlyList<MOLControlBlock> _blocks;
        private readonly IReadOnlyList<MOLLink> _links;
        private readonly IReadOnlyList<MOLSelectionGroup> _groups;
        private readonly IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> _parameters;
        private readonly IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> _ports;
        private readonly IReadOnlyDictionary<Guid,MOLBlockPosition> _positions;
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
        }
        public IReadOnlyList<MOLControlBlock> Blocks {get{return _blocks;}}
        public IReadOnlyList<MOLLink> Links {get{return _links;}}
        public IReadOnlyList<MOLSelectionGroup> Groups {get{return _groups;}}
        public IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> Parameters
        {get{return _parameters;}}
        public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports {get{return _ports;}}
        public IReadOnlyDictionary<Guid,MOLBlockPosition> Positions {get{return _positions;}}
        /// <summary>受V2 Codec和4MB限制的剪贴板传输格式，绝不作为工程真源保存。</summary>
        public byte[] ToPayload()
        {
            var ports=_ports.ToDictionary(p=>p.Key,
                p=>(IEnumerable<MPCPortDescriptor>)p.Value);
            var positions=_positions.ToDictionary(p=>p.Key,p=>p.Value);
            var parameters=_parameters.ToDictionary(p=>p.Key,
                p=>(IDictionary<string,string>)p.Value.ToDictionary(k=>k.Key,k=>k.Value,StringComparer.Ordinal));
            var wiring=new MOLWiringDefinition(_blocks[0].Id,_blocks,ports,_links,
                positions,_groups,parameters);
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
        private Guid _entry;
        private MOCFlowBuilder(MOLWiringDefinition current)
        {
            if(current==null)return;
            _entry=current.EntryBlockId;
            _blocks.AddRange(current.Blocks);
            _links.AddRange(current.Links);
            foreach(var pair in current.Ports)_ports.Add(pair.Key,pair.Value.ToArray());
            foreach(var pair in current.Positions)_positions.Add(pair.Key,pair.Value);
            _groups.AddRange(current.Groups);
            foreach(var pair in current.Parameters)
                _parameters.Add(pair.Key,pair.Value.ToDictionary(k=>k.Key,k=>k.Value,StringComparer.Ordinal));
        }
        public Guid LastBlockId{get{return _blocks.Count==0?Guid.Empty:_blocks[_blocks.Count-1].Id;}}
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
            foreach(Guid id in selected){_ports.Remove(id);_positions.Remove(id);_parameters.Remove(id);}
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
        {return new MOLWiringDefinition(_entry,_blocks,_ports,_links,_positions,_groups,_parameters);}
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
                definition.Parameters.Keys.Any(set.Contains)||
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
            var child=new MOLWiringDefinition(entryCandidates[0],fragment.Blocks,ports,
                fragment.Links,positions,fragment.Groups);
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
            MEMNodeSnapshot node=session.GetSnapshot().Find(content.Id);
            if(node==null||node.Kind!=MEMNodeKind.Content)
                throw new InvalidOperationException("moc.wiring.foreign_content");
            MOLWiringDefinition old=MOLWiringCodec.Get(node);
            if(old==null&&MOLLogicCodec.Get(node)!=null)
                throw new InvalidOperationException("moc.wiring.legacy_requires_explicit_conversion");
            var builder=new MOCFlowBuilder(old);
            configure(builder);
            var aspect=MOLWiringCodec.CreateAspect(builder.Build());
            session.Edit("编辑自由编排",revision,e=>{e.SetAspect(content,aspect);return true;})
                .RequireValue();
        }
    }

    public sealed class MOCEngineeringCompiler
    {
        private const int MaxSteps = 4096;
        private const int MaxDepth = 24;

        public MOCCompiledPlan Compile(MEMProjectSnapshot snapshot, Guid entryContentId,
            IMRKCapabilityResolver resolver, IEnumerable<Guid> instancePath = null)
        {
            if (snapshot == null || resolver == null)
                throw new ArgumentNullException("snapshot/resolver");
            MEMNodeSnapshot entry = snapshot.Find(entryContentId);
            if (entry == null || entry.Kind != MEMNodeKind.Content)
                throw new InvalidOperationException("moc.entry_must_be_content");
            Guid[] path = (instancePath ?? new Guid[0]).ToArray();
            if (path.Length > 32 || path.Any(x => x == Guid.Empty))
                throw new InvalidOperationException("moc.instance_path");
            var known = snapshot.Nodes.SelectMany(n => n.References).ToDictionary(r => r.Id);
            foreach (Guid id in path)
                if (!known.ContainsKey(id)) throw new InvalidOperationException("moc.unknown_instance:" + id);

            MOLWiringDefinition wired = MOLWiringCodec.Get(entry);
            if (wired != null) return CompileWiring(snapshot, entry, path, wired, resolver);

            var build = new Compilation(snapshot, resolver, path);
            build.EmitContent(entry, new string[0], new Guid[0], null, 0);
            var compiled = new MRKExecutionPlanCompiler().Compile(
                "moc." + snapshot.ProjectId.ToString("N") + "." + entryContentId.ToString("N") +
                ".r" + snapshot.Revision.ToString(CultureInfo.InvariantCulture),
                build.Steps, build.Resolver);
            if (!compiled.Succeeded)
                throw new InvalidOperationException("moc.compile_rejected:" +
                    string.Join(";", compiled.Diagnostics.Select(d => d.Code + "@" + d.StepId)));
            return new MOCCompiledPlan(compiled.Plan, new MOCSourceMap(build.Origins),
                snapshot.ProjectId, snapshot.Revision);
        }


        // Stage 2: V2自由连接实际参与MRK依赖及数据绑定；复杂分支/循环不做假执行。
        private static MOCCompiledPlan CompileWiring(MEMProjectSnapshot snapshot, MEMNodeSnapshot entry,
            Guid[] instancePath, MOLWiringDefinition wiring, IMRKCapabilityResolver resolver)
        {
            wiring.ValidateExecutable();
            var blocks=wiring.Blocks.ToDictionary(x=>x.Id);
            var stepIds=blocks.Keys.ToDictionary(x=>x,x=>"w"+x.ToString("N"));
            var requests=new List<MRKStepCompileRequest>();
            var origins=new List<MOCStepOrigin>();
            var inputDefaults=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(var block in wiring.Blocks)
            {
                var entering=wiring.Links.Where(x=>x.Target.BlockId==block.Id).ToArray();
                var depends=entering.Select(x=>stepIds[x.Source.BlockId])
                    .Distinct(StringComparer.Ordinal).ToArray();
                var inputs=new List<MRKExecutionInput>();
                foreach(var link in entering.Where(x=>x.Kind==MPCPortKind.Data))
                {
                    if(entering.Count(x=>x.Kind==MPCPortKind.Data &&
                        x.Target.PortMemberId==link.Target.PortMemberId)>1)
                        throw new InvalidOperationException("moc.wiring.data_fanin_requires_aggregator:"+
                            link.Target.BlockId+":"+link.Target.PortMemberId);
                    var sourcePorts=wiring.Ports[link.Source.BlockId];
                    var from=sourcePorts.Single(x=>x.MemberId==link.Source.PortMemberId);
                    var target=wiring.Ports[block.Id].Single(x=>x.MemberId==link.Target.PortMemberId);
                    var resolvedCapability=blocks[link.Source.BlockId];
                    MRKResolvedCapability actual;
                    if(!resolver.TryResolve(resolvedCapability.CapabilityId,out actual)||actual==null)
                        throw new InvalidOperationException("moc.wiring.unknown_producer:"+resolvedCapability.CapabilityId);
                    MRKInputSource sourceKind;
                    string sourceName=stepIds[link.Source.BlockId];
                    Type valueType=ResolveWiringType(target.TypeId);
                    if(actual.DataOutputs.ContainsKey(from.MemberId))
                    {
                        sourceKind=MRKInputSource.StepOutput;
                        sourceName+="."+from.MemberId;
                    }
                    else if(sourcePorts.Count(x=>x.Kind==MPCPortKind.Data&&
                        x.Direction!=MPCPortDirection.Input)==1)
                        sourceKind=MRKInputSource.StepResult;
                    else
                        throw new InvalidOperationException("moc.wiring.ambiguous_output:"+
                            link.Source.BlockId+":"+from.MemberId);
                    inputs.Add(new MRKExecutionInput(target.MemberId,sourceKind,sourceName,
                        valueType,target.Required));
                }
                IReadOnlyDictionary<string,string> parameters;
                if(wiring.Parameters.TryGetValue(block.Id,out parameters))
                foreach(var parameter in parameters)
                {
                    var port=wiring.Ports[block.Id].Single(p=>p.MemberId==parameter.Key);
                    Type type=ResolveWiringType(port.TypeId);
                    if(type==typeof(object)||type==typeof(byte[]))
                        throw new InvalidOperationException("moc.wiring.parameter_unknown_type");
                    string inputKey=stepIds[block.Id]+"."+parameter.Key;
                    object literal=type==typeof(string)?(object)parameter.Value:
                        Convert.ChangeType(parameter.Value,type,CultureInfo.InvariantCulture);
                    inputs.Add(new MRKExecutionInput(parameter.Key,MRKInputSource.FlowInput,
                        inputKey,type,port.Required));
                    inputDefaults.Add(inputKey,literal);
                }
                requests.Add(new MRKStepCompileRequest(stepIds[block.Id],block.CapabilityId,
                    depends,inputs));
                origins.Add(new MOCStepOrigin(stepIds[block.Id],
                    new MRIObjectAddress(snapshot.ProjectId,entry.StorageRootId,entry.Id,instancePath),
                    block.Id,new Guid[0],null,"Action"));
            }
            var compiled=new MRKExecutionPlanCompiler().Compile(
                "moc."+snapshot.ProjectId.ToString("N")+"."+entry.Id.ToString("N")+
                ".r"+snapshot.Revision.ToString(CultureInfo.InvariantCulture)+".wired",
                requests,resolver);
            if(!compiled.Succeeded)
                throw new InvalidOperationException("moc.wiring.compile_rejected:"+
                    string.Join(";",compiled.Diagnostics.Select(x=>x.Code+"@"+x.StepId)));
            return new MOCCompiledPlan(compiled.Plan,new MOCSourceMap(origins),
                snapshot.ProjectId,snapshot.Revision,inputDefaults);
        }
        private static Type ResolveWiringType(string id)
        {
            switch(id)
            {
                case "bool":case "System.Boolean":return typeof(bool);
                case "int":case "System.Int32":return typeof(int);
                case "long":case "System.Int64":return typeof(long);
                case "double":case "System.Double":return typeof(double);
                case "string":case "System.String":return typeof(string);
                case "byte[]":case "System.Byte[]":return typeof(byte[]);
                // 自定义组合数据契约由能力Provider和运行时适配器校验；首批不反射加载陌生类型。
                default:return typeof(object);
            }
        }

        private sealed class IterationScope
        {
            internal int Index;
            internal string IndexStep;
            internal string ItemStep;
            internal string Input;
        }
        private sealed class Compilation
        {
            internal readonly List<MRKStepCompileRequest> Steps = new List<MRKStepCompileRequest>();
            internal readonly List<MOCStepOrigin> Origins = new List<MOCStepOrigin>();
            internal readonly MOCResolver Resolver;
            private readonly MEMProjectSnapshot _snapshot;
            private readonly Guid[] _instances;
            private int _serial;
            private readonly HashSet<Guid> _activeContents = new HashSet<Guid>();
            internal Compilation(MEMProjectSnapshot snapshot, IMRKCapabilityResolver resolver, Guid[] instances)
            { _snapshot = snapshot; Resolver = new MOCResolver(resolver); _instances = instances; }
            private string NewId(MOLControlBlock block)
            {
                return "m" + (++_serial).ToString(CultureInfo.InvariantCulture) +
                    "_" + block.Id.ToString("N");
            }
            private string Add(MOLControlBlock block, MEMNodeSnapshot owner, Guid[] calls,
                int? iteration, string role, string capability, IEnumerable<string> dependencies,
                Func<MRKExecutionContext,bool> condition = null, bool join = false,
                IEnumerable<MRKExecutionInput> inputs = null)
            {
                if (Steps.Count >= MaxSteps)
                    throw new InvalidOperationException("moc.step_budget");
                string id = NewId(block);
                Steps.Add(new MRKStepCompileRequest(id, capability, dependencies,
                    inputs: inputs, condition: condition, allowSkippedDependencies: join));
                var address = new MRIObjectAddress(_snapshot.ProjectId,
                    owner.StorageRootId, owner.Id, _instances);
                Origins.Add(new MOCStepOrigin(id, address, block.Id, calls, iteration, role));
                return id;
            }
            private Func<MRKExecutionContext, bool> Predicate(MOLExpression expression, int iteration)
            {
                if (expression == null) return ctx => true;
                string resolved = null;
                if (expression.Source == MOLExpressionSource.StepResult)
                {
                    Guid id;
                    if (!Guid.TryParse(expression.Name, out id))
                        throw new InvalidOperationException("moc.step_result_requires_block_id");
                    var origin = Origins.LastOrDefault(o => o.BlockId == id && o.Role == "Action");
                    if (origin == null)
                        throw new InvalidOperationException("moc.step_result_not_preceding:" + expression.Name);
                    resolved = origin.StepId;
                }
                string stepId = resolved;
                return ctx => MOCExpressions.Evaluate(expression, ctx, iteration, stepId);
            }
            private static string[] Merge(IEnumerable<string> first, IEnumerable<string> second)
            { return first.Concat(second).Distinct(StringComparer.Ordinal).ToArray(); }
            internal string[] EmitContent(MEMNodeSnapshot content, string[] after,
                Guid[] calls, IterationScope scope, int depth)
            {
                if (depth > MaxDepth || !_activeContents.Add(content.Id))
                    throw new InvalidOperationException("moc.recursive_or_deep_subflow");
                try
                {
                    var logic = MOLLogicCodec.Get(content);
                    if (logic == null) throw new InvalidOperationException("moc.content_logic_missing:" + content.Id);
                    return Emit(logic.Root, content, after, calls, scope, depth + 1);
                }
                finally { _activeContents.Remove(content.Id); }
            }
            private string[] Emit(MOLControlBlock block, MEMNodeSnapshot owner,
                string[] before, Guid[] calls, IterationScope scope, int depth)
            {
                if (depth > MaxDepth) throw new InvalidOperationException("moc.depth_budget");
                switch (block.Kind)
                {
                    case MOLControlKind.Action:
                    {
                        var dependencies = new List<string>(before);
                        var inputs = new List<MRKExecutionInput>();
                        if (scope != null)
                        {
                            if (!dependencies.Contains(scope.IndexStep)) dependencies.Add(scope.IndexStep);
                            inputs.Add(new MRKExecutionInput("iteration", MRKInputSource.StepResult,
                                scope.IndexStep, typeof(int)));
                            if (scope.ItemStep != null)
                            {
                                if (!dependencies.Contains(scope.ItemStep)) dependencies.Add(scope.ItemStep);
                                inputs.Add(new MRKExecutionInput("item", MRKInputSource.StepResult,
                                    scope.ItemStep, typeof(object)));
                            }
                        }
                        return new[] {Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "Action", block.CapabilityId, dependencies, inputs: inputs)};
                    }
                    case MOLControlKind.Sequence:
                    {
                        var exits = before;
                        foreach (var child in block.Children)
                            exits = Emit(child, owner, exits, calls, scope, depth + 1);
                        return exits;
                    }
                    case MOLControlKind.Parallel:
                    {
                        var exits = new List<string>();
                        foreach (var child in block.Children)
                            exits.AddRange(Emit(child, owner, before, calls, scope, depth + 1));
                        return new[]{Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "ParallelJoin", "moc.internal.noop", exits)};
                    }
                    case MOLControlKind.Branch:
                    {
                        var joins = new List<string>();
                        var cases = block.Cases.ToArray();
                        var casePredicates = cases.Select(c =>
                            c.Condition == null ? null : Predicate(c.Condition,
                                scope == null ? 0 : scope.Index)).ToArray();
                        for(int i=0;i<cases.Length;i++)
                        {
                            int chosen = i;
                            Func<MRKExecutionContext,bool> guard = ctx =>
                            {
                                for(int earlier=0;earlier<chosen;earlier++)
                                    if (casePredicates[earlier] == null || casePredicates[earlier](ctx))
                                        return false;
                                return casePredicates[chosen] == null || casePredicates[chosen](ctx);
                            };
                            string gate=Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                                "Case." + i, "moc.internal.noop", before, guard);
                            joins.AddRange(Emit(cases[i].Body, owner, new[]{gate}, calls, scope, depth+1));
                        }
                        return new[]{Add(block, owner, calls, scope == null ? (int?)null : scope.Index,
                            "BranchJoin", "moc.internal.noop", joins,
                            condition: ctx => !before.Any(d => {
                                MRKStepState state;
                                return ctx.StatesSnapshot().TryGetValue(d, out state) &&
                                    state == MRKStepState.Skipped;
                            }), join:true)};
                    }
                    case MOLControlKind.Loop:
                    case MOLControlKind.ForEach:
                    {
                        MOLLoopDefinition l = block.Loop;
                        string[] exits = before;
                        string previousGuard = null;
                        for(int i=0;i<l.MaxIterations;i++)
                        {
                            int index = i;
                            string prior = previousGuard;
                            Func<MRKExecutionContext,bool> test = (l.Mode == MOLLoopMode.DoWhile && index == 0)
                                ? (Func<MRKExecutionContext,bool>)(ctx => true)
                                : Predicate(l.Condition, index);
                            Func<MRKExecutionContext,bool> condition = ctx =>
                            {
                                if (before.Any(d => {
                                    MRKStepState state;
                                    return ctx.StatesSnapshot().TryGetValue(d, out state) &&
                                        state == MRKStepState.Skipped;
                                })) return false;
                                if (prior != null)
                                {
                                    object valid;
                                    if (!ctx.TryGetAbsoluteResult(prior, out valid)) return false;
                                }
                                if (block.Kind == MOLControlKind.ForEach)
                                {
                                    object data;
                                    if (!ctx.Inputs.TryGetValue(l.CollectionInput, out data) || !(data is IList))
                                        throw new InvalidOperationException("moc.foreach_requires_IList:" + l.CollectionInput);
                                    return index < ((IList)data).Count;
                                }
                                if (l.Mode == MOLLoopMode.DoWhile && index == 0) return true;
                                return test(ctx);
                            };
                            string gate=Add(block, owner, calls, index, "IterationGuard",
                                "moc.internal.noop", exits, condition, join:true);
                            previousGuard = gate;
                            string idxName="moc.internal.index." + gate;
                            Resolver.Register(idxName, ctx => index, typeof(int));
                            string idx=Add(block, owner, calls, index, "IterationIndex",
                                idxName, new[]{gate});
                            string item=null;
                            if(block.Kind == MOLControlKind.ForEach)
                            {
                                string capability="moc.internal.item." + gate;
                                Resolver.Register(capability, ctx => ((IList)ctx.Inputs[l.CollectionInput])[index],
                                    typeof(object));
                                item=Add(block,owner,calls,index,"IterationItem",capability,new[]{idx});
                            }
                            var iterationScope=new IterationScope{Index=index,IndexStep=idx,ItemStep=item,
                                Input=l.CollectionInput};
                            string[] bodyStart=item==null?new[]{idx}:new[]{item};
                            string[] body=Emit(l.Body,owner,bodyStart,calls,iterationScope,depth+1);
                            exits=new[]{Add(block, owner, calls, index, "IterationJoin",
                                "moc.internal.noop", body, join:true)};
                        }
                        return exits;
                    }
                    case MOLControlKind.Invoke:
                    {
                        Guid target;
                        if (!Guid.TryParse(block.TargetFlow, out target))
                            throw new InvalidOperationException("moc.invoke_target_must_be_content_guid");
                        var content=_snapshot.Find(target);
                        if(content==null||content.Kind!=MEMNodeKind.Content)
                            throw new InvalidOperationException("moc.invoke_missing_content");
                        var nextCalls=calls.Concat(new[]{block.Id}).ToArray();
                        return EmitContent(content,before,nextCalls,scope,depth+1);
                    }
                    default:
                        throw new InvalidOperationException("moc.control_not_yet_supported:" + block.Kind);
                }
            }
        }
    }
}
