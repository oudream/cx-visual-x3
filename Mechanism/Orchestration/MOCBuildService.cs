using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Orchestration
{
    public enum MOCDiagnosticSeverity { Info=1, Warning=2, Error=3 }
    public sealed class MOCDiagnostic
    {
        internal MOCDiagnostic(MOCDiagnosticSeverity severity,string code,string message,
            Guid contentId,Guid? blockId=null,Guid? linkId=null)
        {Severity=severity;Code=code;Message=message;ContentId=contentId;BlockId=blockId;LinkId=linkId;}
        public MOCDiagnosticSeverity Severity{get;private set;}
        public string Code{get;private set;}
        public string Message{get;private set;}
        public Guid ContentId{get;private set;}
        public Guid? BlockId{get;private set;}
        public Guid? LinkId{get;private set;}
    }
    public sealed class MOCBuildResult
    {
        internal MOCBuildResult(MOCCompiledPlan plan,IEnumerable<MOCDiagnostic> diagnostics)
        {Plan=plan;Diagnostics=new ReadOnlyCollection<MOCDiagnostic>(diagnostics.ToList());}
        public bool Succeeded{get{return Plan!=null&&Diagnostics.All(x=>x.Severity!=MOCDiagnosticSeverity.Error);}}
        public MOCCompiledPlan Plan{get;private set;}
        public IReadOnlyList<MOCDiagnostic> Diagnostics{get;private set;}
    }
    /// <summary>无副作用构建预检查，复用现有MOC→MRK编译器并保留工程定位。</summary>
    public sealed class MOCBuildService
    {
        public MOCBuildResult Build(MEMProjectSnapshot snapshot,Guid contentId,
            IMRKCapabilityResolver resolver)
        {
            if(snapshot==null||resolver==null)throw new ArgumentNullException("snapshot/resolver");
            var errors=new List<MOCDiagnostic>();
            MEMNodeSnapshot node=snapshot.Find(contentId);
            if(node==null||node.Kind!=MEMNodeKind.Content)
            {
                errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,"MOC_CONTENT_MISSING",
                    "构建目标必须为工程中的Content",contentId));
                return new MOCBuildResult(null,errors);
            }
            MOLWiringDefinition wiring=null;
            try
            {
                wiring=MOLWiringCodec.Get(node);
                if(wiring!=null)wiring.ValidateExecutable();
            }
            catch(InvalidOperationException e)
            {
                errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                    "MOC_WIRING_INVALID",e.Message,contentId));
            }
            catch(InvalidDataException e)
            {
                errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                    "MOC_WIRING_SCHEMA",e.Message,contentId));
            }
            if(errors.Count>0)return new MOCBuildResult(null,errors);
            if(wiring!=null)
            {
                var available=new Dictionary<Guid,MRKResolvedCapability>();
                foreach(var block in wiring.Blocks)
                {
                    // Content引用由MOC编译校验并经MRK子流程内联，不冒充专业Provider。
                    if(block.CapabilityId.StartsWith("moc.invoke:",StringComparison.Ordinal))
                        continue;
                    if(block.CapabilityId=="moc.state-machine")
                    {
                        if(!wiring.StateMachines.ContainsKey(block.Id))
                            errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                                "MOC_STATE_MACHINE_MISSING",
                                "节点未声明StateMachine状态和转移规则",contentId,block.Id));
                        continue;
                    }
                    MRKResolvedCapability action;
                    if(!resolver.TryResolve(block.CapabilityId,out action)||action==null)
                    {
                        errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                            "MOC_CAPABILITY_NOT_REGISTERED",
                            "专业能力不可用: "+block.CapabilityId,contentId,block.Id));
                        continue;
                    }
                    available.Add(block.Id,action);
                }
                foreach(var link in wiring.Links.Where(x=>x.Kind==MPCPortKind.Data||
                    x.Kind==MPCPortKind.Stream))
                {
                    MRKResolvedCapability target;
                    if(!available.TryGetValue(link.Target.BlockId,out target))continue;
                    if(!target.HasInputContract)continue; // Legacy Provider由MRK进行运行约束校验
                    var declared=target.Inputs.FirstOrDefault(x=>x.Name==link.Target.PortMemberId);
                    if(declared==null)
                        errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                            "MOC_INPUT_UNKNOWN","目标能力未声明此输入端口: "+
                            link.Target.PortMemberId,contentId,link.Target.BlockId,link.Id));
                }
                if(errors.Count>0)return new MOCBuildResult(null,errors);
            }
            try
            {
                var plan=new MOCEngineeringCompiler().Compile(snapshot,contentId,resolver);
                return new MOCBuildResult(plan,errors);
            }
            catch(InvalidOperationException ex)
            {
                errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                    "MOC_COMPILE_REJECTED",ex.Message,contentId));
            }
            catch(ArgumentException ex)
            {
                errors.Add(new MOCDiagnostic(MOCDiagnosticSeverity.Error,
                    "MOC_COMPILE_ARGUMENT",ex.Message,contentId));
            }
            return new MOCBuildResult(null,errors);
        }
    }
}
