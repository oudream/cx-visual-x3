using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationLogicIntegrationTests
    {
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("MOL/MOC real integration: " + message); }

        private sealed class TestResolver : IMRKCapabilityResolver
        {
            private readonly Dictionary<string, MRKResolvedCapability> _items =
                new Dictionary<string, MRKResolvedCapability>(StringComparer.Ordinal);
            internal TestResolver Register(string id,
                Func<MRKExecutionContext, CancellationToken, Task<object>> action)
            {
                _items.Add(id, new MRKResolvedCapability(action));
                return this;
            }
            internal TestResolver RegisterData(string id,
                Func<MRKExecutionContext,CancellationToken,Task<object>> action,
                IDictionary<string,Type> outputs,IEnumerable<MRKCapabilityInput> inputs)
            {
                _items.Add(id,new MRKResolvedCapability(action,dataOutputs:outputs,inputs:inputs));
                return this;
            }
            public bool TryResolve(string id, out MRKResolvedCapability result)
            { return _items.TryGetValue(id, out result); }
        }

        private static MRKFlowResult Execute(MOCCompiledPlan compiled,
            IDictionary<string, object> inputs, out MOCTraceView trace)
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 128));
            try
            {
                MRKFlowHandle handle = engine.StartFlow(compiled.Plan, inputs);
                Task<MRKFlowResult> task = handle.Completion;
                if (!task.Wait(TimeSpan.FromSeconds(12)))
                {
                    handle.RequestCancel();
                    throw new TimeoutException("MOC integration flow timeout");
                }
                MRKFlowResult result = task.GetAwaiter().GetResult();
                trace = compiled.ReadTrace(handle, 0);
                return result;
            }
            finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
        }

        internal static void Run()
        {
            SaveLoadAndIdentity();
            MOLDefinitionEditingClosure();
            MOLScalarTypeSafety();
            MOLCompoundConditions();
            MOLCompoundStateGuards();
            MOLEventDeclarationContracts();
            MOLTypedDefinitionContracts();
            MOLThrowDefinitionContracts();
            MOLLosslessSchemaConversion();
            MOLCrossSchemaContentDeclaration();
            V1WaitDelayCancellationRoundtrip();
            P01EventAwaitTimeoutCancellationRoundtrip();
            P01TryCatchFinallyRoundtrip();
            V2WiringRoundtrip();
            V2TypedParameterConfiguration();
            G05aConditionalBranchesAndJoin();
            R02V1V2SwitchBranchParity();
            G05cWorkflowRetryPolicy();
            R08_EngineeringStageGroupIdentitySurvivesBuildAndReload();
            G05bStreamBackpressureAndFlow();
            G05dHierarchicalParallelMachine();
            P03StateMachineGuards();
            P03GuardPriorityAndOrthogonalRegions();
            V2ExtractLinearContent();
            G07ProjectReleaseLockAndAuthorization();
            G06CpuVisionThroughMOC();
            G04ParameterSubflowIsolation();
            V2NamedSubflowDataContracts();
            V2ThreeLevelNamedData();
            R06NaturalRepeatedChildInvocation();
            CThreeTierXRayTwoSubflows();
            V2LayoutContract();
            V2BulkEditingClipboard();
            V2WiringCompileRun();
            V2BuilderSharedDefinition();
            V2DraftPortsUndoAndCompileGate();
            V2CapabilityDiscovery();
            V2DiagnosticLocations();
            BranchParallelAndTrace();
            LoopKindsAndBoundedForEach();
            ForeachOverflowMustFailBeforeExecution();
            R05DynamicLoopAndExit();
            InvokedContentAndInvalidSource();
        }

        private static void SaveLoadAndIdentity()
        {
            var session = MEMEngineering.Create("logic");
            MEMPage page = session.AddPage("Main");
            var definition = new MOLLogicDefinition(MOLControlBlock.Sequence(
                MOLControlBlock.Action("acquire"), MOLControlBlock.If(
                    MOLExpression.InputTrue("isOK"), MOLControlBlock.Action("pass"),
                    MOLControlBlock.Action("fail"))));
            MOLLogicCodec.Set(session, page.RootContent, definition);
            byte[] payload = MOLLogicCodec.Serialize(definition);
            MOLLogicDefinition reload = MOLLogicCodec.Deserialize(payload);
            Check(reload.Root.Id == definition.Root.Id &&
                reload.Root.Children[1].Cases.Count == 2, "codec identity and cases");
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-" + Guid.NewGuid().ToString("N") + ".cxproj");
            try
            {
                session.SaveAs(path);
                var restored = MEMEngineering.Open(path);
                var logic = MOLLogicCodec.Get(restored.GetSnapshot().Find(page.RootContent.Id));
                Check(logic != null && logic.Root.Id == definition.Root.Id &&
                    logic.Root.Children[0].CapabilityId == "acquire", "real file roundtrip");
                restored.Close(true);
            }
            finally
            {
                session.Close(true);
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }


        /// <summary>D2～D4：不增第二树，以MEM事务编辑原逻辑；坏Schema不得静默丢弃。</summary>
        private static void MOLDefinitionEditingClosure()
        {
            var session=MEMEngineering.Create("MOL定义四方面收口");
            MEMPage page=session.AddPage("Main");
            MOLControlBlock before=MOLControlBlock.Action("Inspect");
            MOLControlBlock check=MOLControlBlock.Action("Check");
            MOLControlBlock clean=MOLControlBlock.Action("Cleanup");
            MOLControlBlock region=MOLControlBlock.TryCatchFinally(check,finallyBody:clean);
            var definition=new MOLLogicDefinition(MOLControlBlock.Sequence(before,region));
            MOLLogicCodec.Set(session,page.RootContent,definition);
            int revision=session.Revision;
            var change=new MOLControlBlock(check.Id,MOLControlKind.Action,"CheckV2");
            var modified=definition.ReplaceBlock(check.Id,change);
            Check(modified.Root.Id==definition.Root.Id&&
                modified.Root.Children[0].Id==before.Id&&
                modified.Root.Children[1].Id==region.Id&&
                modified.Root.Children[1].TryRegion.TryBody.Id==check.Id&&
                modified.Root.Children[1].TryRegion.FinallyBody.Id==clean.Id&&
                definition.Root.Children[1].TryRegion.TryBody.CapabilityId=="Check",
                "MOL replace affected unrelated identity or mutated old definition");

            bool rejected=false;
            try{definition.ReplaceBlock(check.Id,MOLControlBlock.Action("Other"));}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="logic.edit.identity_changed";}
            Check(rejected,"MOL replace accepted changed block identity");
            rejected=false;
            var missing=MOLControlBlock.Action("Missing");
            try{definition.ReplaceBlock(missing.Id,missing);}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="logic.edit.block_not_found";}
            Check(rejected,"MOL replace accepted nonexistent block identity");

            MOLLogicCodec.Edit(session,page.RootContent,d=>d.ReplaceBlock(check.Id,change));
            Check(session.Revision==revision+1&&
                MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Root.Children[1].TryRegion.TryBody.CapabilityId=="CheckV2",
                "MOL edit did not atomically commit changed inner block");
            Check(session.Undo().Success&&
                MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Root.Children[1].TryRegion.TryBody.CapabilityId=="Check",
                "MOL edit did not reuse MEM Undo");
            Check(session.Redo().Success&&
                MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Root.Children[1].TryRegion.TryBody.CapabilityId=="CheckV2",
                "MOL edit did not reuse MEM Redo");

            revision=session.Revision;
            rejected=false;
            try{MOLLogicCodec.Edit(session,page.RootContent,d=>null);}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="logic.edit.null_definition";}
            Check(rejected&&session.Revision==revision,
                "Null edit unexpectedly committed or changed revision");

            byte[] bytes=MOLLogicCodec.Serialize(modified);
            string xml=System.Text.Encoding.UTF8.GetString(bytes);
            foreach(string bad in new[]{
                xml.Replace("<MOL v=\"1\"","<MOL v=\"1\" lost=\"true\""),
                xml.Replace("kind=\"Action\"","kind=\"Action\" bad=\"x\""),
                xml.Replace("cap=\"CheckV2\"","cap=\"CheckV2\" unsupported=\"x\""),
                xml.Replace("<TryRegion>","<TryRegion unsupported=\"yes\">")
            })
            {
                rejected=false;
                try{MOLLogicCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(bad));}
                catch(System.IO.InvalidDataException){rejected=true;}
                Check(rejected,"Malformed MOL definition silently accepted an unknown field");
            }
            string file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-closure-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(file);
                var restored=MEMEngineering.Open(file);
                try
                {
                    var read=MOLLogicCodec.Get(restored.GetSnapshot().Find(page.RootContent.Id));
                    Check(read.Root.Id==definition.Root.Id&&
                        read.Root.Children[1].Id==region.Id&&
                        read.Root.Children[1].TryRegion.TryBody.CapabilityId=="CheckV2"&&
                        read.Root.Children[1].TryRegion.FinallyBody.Id==clean.Id,
                        "MOL edit not retained across MEM Save/Open");
                }
                finally{restored.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(file))System.IO.File.Delete(file);
                if(System.IO.File.Exists(file+".cx-lock"))System.IO.File.Delete(file+".cx-lock");
            }
        }



        /// <summary>MOL纯值复合规则：短路、原Schema扩展、同一MRK条件执行路径。</summary>
        private static void MOLCompoundConditions()
        {
            MOLExpression condition=MOLExpression.All(MOLExpression.InputTrue("ready"),
                MOLExpression.Any(MOLExpression.InputTrue("approved"),
                    MOLExpression.Not(MOLExpression.InputTrue("blocked"))));
            var values=new Dictionary<string,object>{
                {"ready",false},{"approved",true},{"blocked",false}};
            int reads=0;
            Check(!MOLScalarExpression.EvaluateTree(condition,leaf=>{
                reads++;return values[leaf.Name];
            })&&reads==1,"MOL All did not short circuit");
            values["ready"]=true;values["approved"]=true;reads=0;
            Check(MOLScalarExpression.EvaluateTree(condition,leaf=>{
                reads++;return values[leaf.Name];
            })&&reads==2,"MOL Any did not short circuit");
            values["approved"]=false;values["blocked"]=true;reads=0;
            Check(!MOLScalarExpression.EvaluateTree(condition,leaf=>{
                reads++;return values[leaf.Name];
            })&&reads==3,"MOL Not/Any semantics incorrect");
            bool invalid=false;
            try{MOLExpression.Not(null);}
            catch(ArgumentException){invalid=true;}
            Check(invalid,"MOL compound accepted a null child");

            var succeed=MOLControlBlock.Action("compound.pass");
            var fail=MOLControlBlock.Action("compound.fail");
            var definition=new MOLLogicDefinition(MOLControlBlock.If(condition,succeed,fail));
            byte[] serialized=MOLLogicCodec.Serialize(definition);
            string xml=System.Text.Encoding.UTF8.GetString(serialized);
            Check(xml.StartsWith("<MOL v=\"4\"")&&
                MOLLogicCodec.Deserialize(serialized).Root.Cases[0].Condition.Operator==
                    MOLLogicalOperator.All,
                "Compound MOL Expression schema4 roundtrip failed");
            invalid=false;
            try{MOLLogicCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(
                xml.Replace("v=\"4\"","v=\"1\"")));}
            catch(System.IO.InvalidDataException){invalid=true;}
            Check(invalid,"Legacy schema1 consumed compound expression without explicit upgrade");

            var session=MEMEngineering.Create("MOL Compound");
            var page=session.AddPage("Main");
            string file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-compound-"+Guid.NewGuid().ToString("N")+".cxproj");
            string observed="";
            var resolver=new TestResolver()
                .Register("compound.pass",(ctx,ct)=>{
                    observed="pass";return Task.FromResult((object)true);})
                .Register("compound.fail",(ctx,ct)=>{
                    observed="fail";return Task.FromResult((object)true);});
            MOLLogicCodec.Set(session,page.RootContent,definition);
            try
            {
                session.SaveAs(file);
                var opened=MEMEngineering.Open(file);
                try
                {
                    var copied=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(copied.Root.Id==definition.Root.Id&&
                        copied.Root.Cases[0].Condition.Terms.Count==2,
                        "MEM lost composite logic on Save/Open");
                    var compiled=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    MOCTraceView trace;
                    var result=Execute(compiled,new Dictionary<string,object>{
                        {"ready",true},{"approved",true}},out trace);
                    Check(result.State==MRKFlowState.Completed&&observed=="pass",
                        "MOC/MRK did not apply short circuit for composite guard");
                    observed="";
                    result=Execute(compiled,new Dictionary<string,object>{
                        {"ready",false}},out trace);
                    Check(result.State==MRKFlowState.Completed&&observed=="fail",
                        "MOC/MRK false composite branch not taken");
                    observed="";
                    result=Execute(compiled,new Dictionary<string,object>{
                        {"ready",true},{"approved",false},{"blocked",true}},out trace);
                    Check(result.State==MRKFlowState.Completed&&observed=="fail",
                        "MOC/MRK composite Not branch was incorrectly true");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(file))System.IO.File.Delete(file);
                if(System.IO.File.Exists(file+".cx-lock"))System.IO.File.Delete(file+".cx-lock");
            }
        }


        /// <summary>事件来源与关联声明只保存定义；未接入MRK消息队列时不能冒充Task运行。</summary>
        private static void MOLEventDeclarationContracts()
        {
            var evt=new MOLEventContract("camera.ready",MOLEventSource.Channel,
                payloadTypeId:"frame.metadata",correlationInput:"imageId");
            var wait=MOLControlBlock.AwaitEvent(evt,2000);
            var definition=new MOLLogicDefinition(wait);
            byte[] bytes=MOLLogicCodec.Serialize(definition);
            string xml=System.Text.Encoding.UTF8.GetString(bytes);
            Check(xml.StartsWith("<MOL v=\"4\""),"Extended event contract must use schema4");
            var copied=MOLLogicCodec.Deserialize(bytes);
            Check(copied.Root.Id==wait.Id&&
                copied.Root.Wait.Event.Source==MOLEventSource.Channel&&
                copied.Root.Wait.Event.PayloadTypeId=="frame.metadata"&&
                copied.Root.Wait.Event.CorrelationInput=="imageId",
                "MOL event declaration was lost on serialization");
            bool rejected=false;
            try{new MOLEventContract("bad\nname");}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"MOL accepted invalid event identity");
            rejected=false;
            try{MOLLogicCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(
                xml.Replace("v=\"4\"","v=\"1\"")));}
            catch(System.IO.InvalidDataException){rejected=true;}
            Check(rejected,"Schema1 silently consumed extended event declaration");

            var session=MEMEngineering.Create("MOL Event");
            var page=session.AddPage("Main");
            string file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-event-contract-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                MOLLogicCodec.Set(session,page.RootContent,definition);
                session.SaveAs(file);
                var opened=MEMEngineering.Open(file);
                try
                {
                    var read=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(read.Root.Wait.Event.CorrelationInput=="imageId",
                        "MOL event correlation lost during MEM Save/Open");
                    rejected=false;
                    try{new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException ex)
                    {rejected=ex.Message.Contains("moc.event_delivery_not_supported");}
                    Check(rejected,"MOC silently treated event channel as an input Task");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(file))System.IO.File.Delete(file);
                if(System.IO.File.Exists(file+".cx-lock"))System.IO.File.Delete(file+".cx-lock");
            }
        }


        /// <summary>同一MOLExpression语义在V2状态迁移中仍可短路、保存及真实执行。</summary>
        private static void MOLCompoundStateGuards()
        {
            MOLExpression condition=MOLExpression.All(
                MOLExpression.InputTrue("ready"),
                MOLExpression.Not(MOLExpression.InputTrue("blocked")));
            var machine=new MOLStateMachineDefinition(new[]{
                new MOLStateDescriptor("Idle","Capture",initial:true),
                new MOLStateDescriptor("Busy","Capture")},new[]{
                new MOLStateTransition("Idle","start","Busy",condition)});
            var inputs=new Dictionary<string,object>{{"ready",false}};
            Check(machine.Run(new[]{"start"},inputs)["Capture"]=="Idle",
                "State Guard failed to short-circuit false condition");
            inputs["ready"]=true;inputs["blocked"]=false;
            Check(machine.Run(new[]{"start"},inputs)["Capture"]=="Busy",
                "State Guard compound successful transition failed");
            inputs.Remove("blocked");
            bool rejected=false;
            try{machine.Run(new[]{"start"},inputs);}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="moc.state.guard_input_missing:blocked";}
            Check(rejected,"State compound Guard silently ignored missing input");
            rejected=false;
            try{new MOLStateTransition("Idle","start","Busy",
                MOLExpression.All(MOLExpression.InputTrue("ready"),
                    new MOLExpression(MOLExpressionSource.StepResult,
                        Guid.NewGuid().ToString("N"))));}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"State Guard allowed MRK StepResult in pure state definition");

            var session=MEMEngineering.Create("Compound state");
            var page=session.AddPage("Main");
            Guid id=Guid.Empty;
            MOCFlowBuilder.Edit(session,page.RootContent,b=>id=b.AddStateMachine(machine));
            var before=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
            var bytes=MOLWiringCodec.Serialize(before);
            var xml=System.Text.Encoding.UTF8.GetString(bytes);
            Check(xml.StartsWith("<MOL v=\"5\"")&&
                MOLWiringCodec.CreateAspect(before).SchemaVersion==
                    MOLWiringCodec.CompoundSchemaVersion,
                "Compound state Guard must use Schema5");
            rejected=false;
            try{MOLWiringCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(
                xml.Replace("v=\"5\"","v=\"2\"")));}
            catch(System.IO.InvalidDataException){rejected=true;}
            Check(rejected,"Legacy Wiring Schema2 swallowed compound Guard");
            foreach(string malformed in new[]{
                xml.Replace("<MOL v=\"5\"","<MOL v=\"5\" extra=\"ignore\""),
                xml.Replace("<Guard op=\"All\"","<Guard op=\"All\" extra=\"ignore\""),
                xml.Replace("<Transition from=","<Transition extra=\"ignore\" from="),
                xml.Replace("v=\"5\"","v=\"3\"")
            })
            {
                rejected=false;
                try{MOLWiringCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(malformed));}
                catch(System.IO.InvalidDataException){rejected=true;}
                Check(rejected,"Wiring silently ignored malformed or downgraded compound schema");
            }
            string file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-state-compound-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(file);
                var opened=MEMEngineering.Open(file);
                try
                {
                    var restored=MOLWiringCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(restored.StateMachines[id].Transitions[0].Guard.Operator==
                        MOLLogicalOperator.All,"Compound state Guard lost on Save/Open");
                    Check(MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id))==null,
                        "Schema5 Wiring incorrectly appeared as parallel V1 definition");
                    var compiled=new MOCEngineeringCompiler().Compile(
                        opened.GetSnapshot(),page.RootContent.Id,new TestResolver());
                    MOCTraceView trace;
                    var data=new Dictionary<string,object>{
                        {"moc.state.events."+id.ToString("N"),new[]{"start"}},
                        {"ready",true},{"blocked",false}};
                    var result=Execute(compiled,data,out trace);
                    Check(result.State==MRKFlowState.Completed,
                        "MRK failed real compound state Guard execution");
                    data["ready"]=false;data.Remove("blocked");
                    result=Execute(compiled,data,out trace);
                    Check(result.State==MRKFlowState.Completed,
                        "MRK evaluated missing input despite false short-circuit");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(file))System.IO.File.Delete(file);
                if(System.IO.File.Exists(file+".cx-lock"))System.IO.File.Delete(file+".cx-lock");
            }
        }

        /// <summary>D1：统一Guard/分支/状态机可复用的纯标量类型合同。</summary>
        private static void MOLScalarTypeSafety()
        {
            Check(MOLScalarExpression.Compare(20,"3")>0,
                "MOL numeric comparison fell back to lexical order");
            Check(MOLScalarExpression.Compare("20","3")<0,
                "MOL string comparison did not retain string semantics");
            Check(MOLScalarExpression.Compare(true,"true")==0,
                "MOL boolean comparison incorrect");
            Check(MOLScalarExpression.Compare(1.5d,"1.4")>0,
                "MOL floating comparison incorrect");
            Check(MOLScalarExpression.Compare(20m,"3")>0,
                "MOL decimal comparison incorrect");
            Check(MOLScalarExpression.Evaluate(new MOLExpression(
                MOLExpressionSource.FlowInput,"value",MOLComparison.GreaterThan,"3"),20),
                "MOL numeric Guard semantics incorrect");
            foreach(object actual in new object[]{20,true,1.0d,null})
            {
                bool rejected=false;
                try{MOLScalarExpression.Compare(actual,"not_a_number");}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"MOL incompatible type silently compared as string");
            }
            bool bad=false;
            try{MOLScalarExpression.Compare(double.NaN,"1");}
            catch(InvalidOperationException){bad=true;}
            Check(bad,"MOL nonfinite number silently accepted");
        }


        /// <summary>D1：只扩最小声明合同；尚无执行语义必须被MOC明确拒绝。</summary>
        private static void MOLTypedDefinitionContracts()
        {
            var body=MOLControlBlock.Action("Inspect");
            var handler=MOLControlBlock.Action("HandleDefect");
            var cleanup=MOLControlBlock.Action("Release");
            var named=new MOLCatchClause("inspect.defect",handler);
            var protectedBlock=MOLControlBlock.TryCatchFinally(body,
                finallyBody:cleanup,catches:new[]{named});
            var original=new MOLLogicDefinition(protectedBlock);
            var restored=MOLLogicCodec.Deserialize(MOLLogicCodec.Serialize(original));
            Check(restored.Root.TryRegion.Catches.Count==1&&
                restored.Root.TryRegion.Catches[0].ErrorCode=="inspect.defect"&&
                restored.Root.TryRegion.Catches[0].Body.Id==handler.Id&&
                restored.Root.TryRegion.FinallyBody.Id==cleanup.Id,
                "MOL typed Catch lost definition identity on roundtrip");
            bool rejected=false;
            try{new MOLTryDefinition(body,catches:new[]{
                new MOLCatchClause("repeat",handler),
                new MOLCatchClause("repeat",cleanup)});}
            catch(ArgumentException ex)
            {rejected=ex.Message.Contains("logic.catch_duplicate_or_budget");}
            Check(rejected,"MOL duplicate typed Catch not rejected");
            rejected=false;
            try{new MOLCatchClause("*",handler);}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"MOL typed Catch allows ambiguous wildcard");

            var edit=MEMEngineering.Create("MOL具名Catch");
            var page=edit.AddPage("Main");
            MOLLogicCodec.Set(edit,page.RootContent,original);
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-contract-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                edit.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var copy=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(copy.Root.TryRegion.Catches[0].ErrorCode=="inspect.defect",
                        "MOL named Catch not preserved by MEM Save/Open");
                    rejected=false;
                    try{new MOCEngineeringCompiler().Compile(
                        opened.GetSnapshot(),page.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException ex)
                    {rejected=ex.Message.Contains("moc.try_typed_catch_not_supported");}
                    Check(rejected,"MOC silently downgraded typed Catch to Catch-all");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                edit.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }

            var machine=new MOLStateMachineDefinition(new[]{
                new MOLStateDescriptor("Idle","Scanner",initial:true,
                    entryCapability:"Activate",exitCapability:"Deactivate"),
                new MOLStateDescriptor("Running","Scanner")},new[]{
                new MOLStateTransition("Idle","start","Running")});
            Check(machine.States[0].EntryCapability=="Activate"&&
                machine.States[0].ExitCapability=="Deactivate",
                "MOL state action capability contract missing");
            var stateSession=MEMEngineering.Create("StateAction");
            var statePage=stateSession.AddPage("Main");
            string stateFile=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-state-action-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid id=Guid.Empty;
                MOCFlowBuilder.Edit(stateSession,statePage.RootContent,
                    b=>id=b.AddStateMachine(machine));
                stateSession.SaveAs(stateFile);
                var opened=MEMEngineering.Open(stateFile);
                try
                {
                    var read=MOLWiringCodec.Get(opened.GetSnapshot().Find(
                        statePage.RootContent.Id));
                    Check(read.StateMachines[id].States[0].EntryCapability=="Activate"&&
                        read.StateMachines[id].States[0].ExitCapability=="Deactivate",
                        "MOL State Entry/Exit contract lost on Save/Open");
                    var stateAspect=MOLWiringCodec.CreateAspect(read);
                    Check(stateAspect.SchemaVersion==MOLWiringCodec.ExtendedSchemaVersion,
                        "MOL extended state action must use distinct forward-incompatible schema");
                    string badV2=System.Text.Encoding.UTF8.GetString(stateAspect.Payload)
                        .Replace("v=\"3\"","v=\"2\"");
                    rejected=false;
                    try{MOLWiringCodec.Deserialize(
                        System.Text.Encoding.UTF8.GetBytes(badV2));}
                    catch(System.IO.InvalidDataException ex)
                    {rejected=ex.Message.Contains("wiring.state_action_requires_v3");}
                    Check(rejected,"MOL legacy V2 payload silently consumed state actions");
                    rejected=false;
                    try{new MOCEngineeringCompiler().Compile(
                        opened.GetSnapshot(),statePage.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException ex)
                    {rejected=ex.Message.Contains("moc.state_actions_not_supported");}
                    Check(rejected,"MOC silently ignored unimplemented State Action");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                stateSession.Close(true);
                if(System.IO.File.Exists(stateFile))System.IO.File.Delete(stateFile);
                if(System.IO.File.Exists(stateFile+".cx-lock"))System.IO.File.Delete(stateFile+".cx-lock");
            }
        }


        /// <summary>D2/D3：最小等价子集可转换；有关系的复杂工程必须保持原载荷。</summary>
        private static void MOLLosslessSchemaConversion()
        {
            // D2：Break/Continue是循环作用域的声明，不可从根或任意分支逃逸。
            bool outOfScope=false;
            try{new MOLLogicDefinition(new MOLControlBlock(Guid.NewGuid(),
                MOLControlKind.Exit,exitKind:MOLExitKind.Break));}
            catch(InvalidOperationException ex)
            {outOfScope=ex.Message=="logic.exit_outside_loop";}
            Check(outOfScope,"MOL accepted Break outside any loop");
            outOfScope=false;
            try{new MOLLogicDefinition(MOLControlBlock.If(
                MOLExpression.Always(true),
                new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                    exitKind:MOLExitKind.Continue)));}
            catch(InvalidOperationException ex)
            {outOfScope=ex.Message=="logic.exit_outside_loop";}
            Check(outOfScope,"MOL accepted Continue outside loop from a branch");
            new MOLLogicDefinition(MOLControlBlock.Each("samples",3,
                MOLControlBlock.TryCatchFinally(
                    new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                        exitKind:MOLExitKind.Break),
                    finallyBody:MOLControlBlock.Action("Release"))));

            var session=MEMEngineering.Create("MOL无损Schema迁移");
            MEMPage page=session.AddPage("Main");
            MOLControlBlock action=MOLControlBlock.Action("Inspect");
            MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(action));
            int rev=session.Revision;
            MOLWiringCodec.ConvertFromV1(session,page.RootContent);
            var wiring=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
            Check(session.Revision==rev+1&&wiring.Blocks.Count==1&&
                wiring.Blocks[0].Id==action.Id&&
                wiring.Blocks[0].CapabilityId=="Inspect"&&wiring.Links.Count==0,
                "Single Action conversion V1 to V2 was not lossless");
            int blockedRevision=session.Revision;
            bool blocked=false;
            try{MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(action));}
            catch(InvalidOperationException ex)
            {blocked=ex.Message=="logic.set.requires_explicit_conversion";}
            Check(blocked&&session.Revision==blockedRevision,
                "MOL V1 Set silently overwrote V2 without migration");
            Check(session.Undo().Success&&
                MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Root.Id==action.Id,
                "V1/V2 conversion lost MEM Undo");
            Check(session.Redo().Success&&
                MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .EntryBlockId==action.Id,
                "V1/V2 conversion lost MEM Redo");
            MOLLogicCodec.ConvertFromV2(session,page.RootContent);
            Check(MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                .Root.Id==action.Id,"V2 to V1 converted different identity");
            blockedRevision=session.Revision;blocked=false;
            try{MOLWiringCodec.Set(session,page.RootContent,wiring);}
            catch(InvalidOperationException ex)
            {blocked=ex.Message=="wiring.set.requires_explicit_conversion";}
            Check(blocked&&session.Revision==blockedRevision,
                "MOL V2 Set silently overwrote V1 without migration");

            var complex=new MOLLogicDefinition(MOLControlBlock.Sequence(
                MOLControlBlock.Action("Acquire"),MOLControlBlock.Action("Detect")));
            MOLLogicCodec.Set(session,page.RootContent,complex);
            int current=session.Revision;
            byte[] original=MOLLogicCodec.Serialize(complex);
            bool rejected=false;
            try{MOLWiringCodec.ConvertFromV1(session,page.RootContent);}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="wiring.convert.not_lossless";}
            var unchanged=MOLLogicCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
            Check(rejected&&session.Revision==current&&
                System.Linq.Enumerable.SequenceEqual(original,MOLLogicCodec.Serialize(unchanged)),
                "Complex V1 schema conversion silently lost logic or changed Revision");

            var other=MEMEngineering.Create("V2复杂拒绝");
            MEMPage otherPage=other.AddPage("Main");
            try
            {
                MOCFlowBuilder.Edit(other,otherPage.RootContent,b=>b.Step("Inspect"));
                current=other.Revision;rejected=false;
                try{MOLLogicCodec.ConvertFromV2(other,otherPage.RootContent);}
                catch(InvalidOperationException ex)
                {rejected=ex.Message=="logic.convert.not_lossless";}
                Check(rejected&&other.Revision==current&&
                    MOLWiringCodec.Get(other.GetSnapshot().Find(otherPage.RootContent.Id))!=null,
                    "V2 conversion dropped port Graph or committed rejected edit");
            }
            finally{other.Close(true);}
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-schema-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var read=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(read.Root.Id==complex.Root.Id&&read.Root.Children.Count==2,
                        "Rejected migration changed persisted complex V1 logic");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }


        /// <summary>D1：Throw作为Exit类型声明，不得被MOC误当Return。</summary>
        private static void MOLThrowDefinitionContracts()
        {
            var thrown=MOLControlBlock.Throw("inspect.defect");
            var def=new MOLLogicDefinition(thrown);
            var copy=MOLLogicCodec.Deserialize(MOLLogicCodec.Serialize(def));
            Check(copy.Root.Id==thrown.Id&&copy.Root.ExitKind==MOLExitKind.Throw&&
                copy.Root.ErrorCode=="inspect.defect",
                "MOL Throw lost error identity or schema roundtrip");
            bool rejected=false;
            try{MOLControlBlock.Throw(" ");}catch(ArgumentException){rejected=true;}
            Check(rejected,"MOL Throw accepted empty error code");
            rejected=false;
            try{new MOLLogicDefinition(new MOLControlBlock(Guid.NewGuid(),
                MOLControlKind.Exit,exitKind:MOLExitKind.Throw));}
            catch(InvalidOperationException ex)
            {rejected=ex.Message=="logic.throw_bad_error_code";}
            Check(rejected,"MOL Throw accepted missing error code");
            var session=MEMEngineering.Create("MOL Throw声明");
            var page=session.AddPage("Main");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-throw-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                MOLLogicCodec.Set(session,page.RootContent,def);
                session.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var read=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(read.Root.ExitKind==MOLExitKind.Throw&&
                        read.Root.ErrorCode=="inspect.defect","MOL Throw lost Save/Open");
                    rejected=false;
                    try{new MOCEngineeringCompiler().Compile(
                        opened.GetSnapshot(),page.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException ex)
                    {rejected=ex.Message.Contains("moc.throw_not_supported");}
                    Check(rejected,"MOC quietly converted Throw to Return");
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }


        /// <summary>同一MEM引用可组合V1结构化Content和V2调用图，不复制定义。</summary>
        private static void MOLCrossSchemaContentDeclaration()
        {
            var session=MEMEngineering.Create("MOL cross schema");
            var page=session.AddPage("Main");
            var child=session.AddContent(session.Project,"LegacyLogic");
            var inner=new MOLLogicDefinition(MOLControlBlock.Sequence(
                MOLControlBlock.Action("LegacyCapture"),MOLControlBlock.Action("LegacyInspect")));
            MOLLogicCodec.Set(session,child,inner);
            // 公共合同来自MEM Content，不另存一份V1 Port Graph。
            session.DefinePort(child,"BatchId",MPCPortDirection.Input,
                "string",isPublic:true,required:false);
            session.DefinePort(child,"DefectFlag",MPCPortDirection.Output,
                "bool",isPublic:true);
            session.Edit("引用V1内容",e=>{
                e.Reference(page.RootContent,child,"Legacy");return true;
            }).RequireValue();
            Guid invoke=Guid.Empty;
            MOCFlowBuilder.Edit(session,page.RootContent,flow=>{
                invoke=flow.InvokeContent(child);
                Guid after=flow.Step("Aggregate");
                flow.Connect(invoke,"Done",after,"Start");
            });
            var parent=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
            Check(parent.Blocks.Any(b=>b.Id==invoke&&
                b.CapabilityId=="moc.invoke:"+child.Id.ToString("N"))&&
                parent.Ports[invoke].Any(x=>x.MemberId=="BatchId"&&
                    x.Direction==MPCPortDirection.Input&&x.TypeId=="string")&&
                parent.Ports[invoke].Any(x=>x.MemberId=="DefectFlag"&&
                    x.Direction==MPCPortDirection.Output&&x.TypeId=="bool")&&
                session.GetSnapshot().Find(page.RootContent.Id).References
                    .Any(x=>x.TargetId==child.Id),
                "V2 caller did not declare unique reference to V1 Content");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mol-cross-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var v1=MOLLogicCodec.Get(reopened.GetSnapshot().Find(child.Id));
                    var v2=MOLWiringCodec.Get(reopened.GetSnapshot().Find(page.RootContent.Id));
                    Check(v1.Root.Id==inner.Root.Id&&v2.Blocks.Any(b=>b.Id==invoke)&&
                        v2.Ports[invoke].Any(p=>p.MemberId=="DefectFlag"&&
                            p.Direction==MPCPortDirection.Output),
                        "V1/V2 MEM public port contract or reference lost on Save/Open");
                    bool rejected=false;
                    try{new MOCEngineeringCompiler().Compile(reopened.GetSnapshot(),
                        page.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException ex)
                    {rejected=ex.Message.Contains("moc.invoke.requires_v2");}
                    Check(rejected,"MOC silently substituted V1 child for V2 execution graph");
                }
                finally{reopened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        /// <summary>P0-1：V1定时等待写入同一Logic Aspect，MRK异步执行/取消，拒绝坏合同。</summary>
        private static void V1WaitDelayCancellationRoundtrip()
        {
            bool rejected=false;
            try{new MOLLogicDefinition(new MOLControlBlock(
                Guid.NewGuid(),MOLControlKind.Wait));}
            catch(InvalidOperationException ex){rejected=ex.Message=="logic.wait_missing";}
            Check(rejected,"V1 Wait without a bounded declaration was accepted");
            rejected=false;
            try{MOLControlBlock.Delay(600001);}
            catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"V1 Wait accepted an unbounded delay");

            var session=MEMEngineering.Create("P0-1 可取消等待");
            var page=session.AddPage("Inspect");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-wait-"+Guid.NewGuid().ToString("N")+".cxproj");
            var wait=MOLControlBlock.Delay(20);
            var action=MOLControlBlock.Action("after.wait");
            MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(
                MOLControlBlock.Sequence(wait,action)));
            var resolver=new TestResolver().Register("after.wait",(ctx,ct)=>
                Task.FromResult((object)true));
            try
            {
                session.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var definition=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(definition.Root.Children[0].Wait.DurationMilliseconds==20&&
                        definition.Root.Children[0].Id==wait.Id,
                        "V1 Wait schema1/BlockId lost after real disk roundtrip");
                    var plan=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    MOCTraceView trace;
                    MRKFlowResult done=Execute(plan,null,out trace);
                    Check(done.State==MRKFlowState.Completed&&
                        done.StepStates[plan.SourceMap.Find(wait.Id).Single().StepId]==MRKStepState.Completed&&
                        trace.Events.Any(e=>e.Origin.BlockId==wait.Id),
                        "V1 Wait did not execute in the existing MRK plan/trace");

                    MOLLogicCodec.Set(opened,opened.Project.Pages[0].RootContent,
                        new MOLLogicDefinition(MOLControlBlock.Sequence(
                            MOLControlBlock.Delay(600000),action)));
                    var slow=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    var runtime=new MRKExecutionEngine(new FCRWorkerOptions(2,8));
                    try
                    {
                        MRKFlowHandle handle=runtime.StartFlow(slow.Plan);
                        handle.RequestCancel();
                        Check(handle.Completion.Wait(TimeSpan.FromSeconds(5)),
                            "V1 Wait did not honor MRK cancellation promptly");
                        var stopped=handle.Completion.GetAwaiter().GetResult();
                        Check(stopped.State==MRKFlowState.Cancelled,
                            "V1 Wait cancellation produced a successful flow");
                    }
                    finally{runtime.StopAsync(true).GetAwaiter().GetResult();}
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
            }
        }


        /// <summary>P0-1：MOL逻辑唯一保存，MOC编译到MRK同引擎，成功/捕获/传播/取消均执行Finally。</summary>
        private static void P01TryCatchFinallyRoundtrip()
        {
            bool rejected=false;
            try{MOLControlBlock.TryCatchFinally(MOLControlBlock.Action("work"));}
            catch(ArgumentException ex)
            {rejected=ex.Message.Contains("logic.try_requires_handler_or_finally");}
            Check(rejected,"Try with no Catch/Finally was accepted");
            rejected=false;
            try{new MOLLogicDefinition(new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Try));}
            catch(InvalidOperationException ex){rejected=ex.Message=="logic.try_missing";}
            Check(rejected,"Try without definition was accepted");

            var calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
            bool fail=false,failFinally=false;
            var resolver=new TestResolver()
                .Register("try.work",(ctx,ct)=>{
                    calls.Enqueue("try");
                    return fail?Task.FromException<object>(
                        new InvalidOperationException("inspection.failed")):
                        Task.FromResult((object)true);
                })
                .Register("try.catch",(ctx,ct)=>{
                    calls.Enqueue("catch");return Task.FromResult((object)true);
                })
                .Register("try.finally",(ctx,ct)=>{
                    calls.Enqueue("finally");
                    return failFinally?Task.FromException<object>(
                        new InvalidOperationException("cleanup.failed")):
                        Task.FromResult((object)true);
                })
                .Register("try.after",(ctx,ct)=>{
                    calls.Enqueue("after");return Task.FromResult((object)true);
                });
            var work=MOLControlBlock.Action("try.work");
            var handler=MOLControlBlock.Action("try.catch");
            var cleanup=MOLControlBlock.Action("try.finally");
            var region=MOLControlBlock.TryCatchFinally(work,handler,cleanup);
            var session=MEMEngineering.Create("P0-1 Try Catch Finally");
            var page=session.AddPage("Inspection");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-try-"+Guid.NewGuid().ToString("N")+".cxproj");
            MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(
                MOLControlBlock.Sequence(region,MOLControlBlock.Action("try.after"))));
            try
            {
                session.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var logic=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(logic.Root.Children[0].Id==region.Id&&
                        logic.Root.Children[0].TryRegion.TryBody.Id==work.Id&&
                        logic.Root.Children[0].TryRegion.CatchBody.Id==handler.Id&&
                        logic.Root.Children[0].TryRegion.FinallyBody.Id==cleanup.Id,
                        "Try/Catch/Finally IDs changed after real MEM Save/Open");
                    var build=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    MOCTraceView trace;
                    var success=Execute(build,null,out trace);
                    Check(success.State==MRKFlowState.Completed&&
                        string.Join(",",calls.ToArray())=="try,finally,after"&&
                        trace.Events.Any(x=>x.Origin.BlockId==cleanup.Id)&&
                        trace.Events.Any(x=>x.Origin.BlockId==work.Id),
                        "MRK successful Try skipped Finally or lost SourceMap");
                    calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    fail=true;
                    var handled=Execute(build,null,out trace);
                    Check(handled.State==MRKFlowState.Completed&&
                        string.Join(",",calls.ToArray())=="try,catch,finally,after"&&
                        trace.Events.Any(x=>x.Origin.BlockId==handler.Id),
                        "MRK failed Try did not run Catch/Finally in order");
                    calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    failFinally=true;
                    var broken=Execute(build,null,out trace);
                    Check(broken.State==MRKFlowState.Failed&&
                        string.Join(",",calls.ToArray())=="try,catch,finally"&&
                        broken.Error!=null,
                        "MRK Finally failure must fail Flow and skip downstream");
                    failFinally=false;

                    var uncaught=MOLControlBlock.TryCatchFinally(
                        MOLControlBlock.Action("try.work"),finallyBody:
                        MOLControlBlock.Action("try.finally"));
                    MOLLogicCodec.Set(opened,opened.Project.Pages[0].RootContent,
                        new MOLLogicDefinition(uncaught));
                    var direct=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    var propagated=Execute(direct,null,out trace);
                    Check(propagated.State==MRKFlowState.Failed&&
                        string.Join(",",calls.ToArray())=="try,finally"&&
                        propagated.Error!=null,
                        "Uncaught Try error was swallowed instead of rethrown after Finally");

                    // Foreach中独立Try区域保留IterationIndex与item绑定，不构造第二执行器。
                    resolver.Register("try.iter.work",(ctx,ct)=>{
                        object item;
                        Check(ctx.BoundInputs.TryGetValue("item",out item)&&
                            Convert.ToInt32(item)==(ctx.IterationIndex+1)*10,
                            "Try Foreach item/iteration input was lost");
                        calls.Enqueue("work"+ctx.IterationIndex);
                        return ctx.IterationIndex==1?Task.FromException<object>(
                            new InvalidOperationException("middle.failed")):
                            Task.FromResult((object)true);
                    });
                    resolver.Register("try.iter.catch",(ctx,ct)=>{
                        calls.Enqueue("catch"+ctx.IterationIndex);
                        return Task.FromResult((object)true);
                    });
                    resolver.Register("try.iter.finally",(ctx,ct)=>{
                        object item;
                        Check(ctx.BoundInputs.TryGetValue("item",out item)&&
                            Convert.ToInt32(item)==(ctx.IterationIndex+1)*10,
                            "Foreach Finally lost item binding");
                        calls.Enqueue("finally"+ctx.IterationIndex);
                        return Task.FromResult((object)true);
                    });
                    var iterFinally=MOLControlBlock.Action("try.iter.finally");
                    var iterative=MOLControlBlock.Each("samples",4,
                        MOLControlBlock.TryCatchFinally(
                            MOLControlBlock.Action("try.iter.work"),
                            MOLControlBlock.Action("try.iter.catch"),iterFinally));
                    var serial=MOLLogicCodec.Deserialize(MOLLogicCodec.Serialize(
                        new MOLLogicDefinition(iterative)));
                    Check(serial.Root.Loop.Body.TryRegion!=null,
                        "Try inside Foreach lost structured definition on roundtrip");
                    MOLLogicCodec.Set(opened,opened.Project.Pages[0].RootContent,serial);
                    var iterBuild=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    var iterResult=Execute(iterBuild,new Dictionary<string,object>{
                        {"samples",new int[]{10,20,30}}},out trace);
                    Check(iterResult.State==MRKFlowState.Completed&&
                        string.Join(",",calls.ToArray())==
                            "work0,finally0,work1,catch1,finally1,work2,finally2"&&
                        trace.Events.Any(x=>x.Origin.BlockId==iterFinally.Id),
                        "Dynamic Foreach nested Try/Catch/Finally lost MDS data or SourceMap");

                    var awaited=MOLControlBlock.AwaitEvent("ready",600000);
                    var cancelRegion=MOLControlBlock.TryCatchFinally(awaited,
                        MOLControlBlock.Action("try.catch"),
                        MOLControlBlock.Action("try.finally"));
                    MOLLogicCodec.Set(opened,opened.Project.Pages[0].RootContent,
                        new MOLLogicDefinition(cancelRegion));
                    var cancelBuild=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    var gate=new TaskCompletionSource<bool>();
                    calls=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,16));
                    try
                    {
                        var handle=engine.StartFlow(cancelBuild.Plan,
                            new Dictionary<string,object>{{"ready",gate.Task}});
                        string waitingId=cancelBuild.SourceMap.Find(awaited.Id).Single().StepId;
                        bool entered=false;
                        for(int i=0;i<200;i++)
                        {
                            if(handle.Context.Trace.Snapshot().Any(x=>
                                x.StepId.EndsWith(waitingId,StringComparison.Ordinal)&&
                                (x.State==MRKStepState.Scheduled||
                                 x.State==MRKStepState.Running))){entered=true;break;}
                            System.Threading.Thread.Sleep(10);
                        }
                        Check(entered,"Test did not enter protected Wait before cancellation");
                        handle.RequestCancel();
                        Check(handle.Completion.Wait(TimeSpan.FromSeconds(5)),
                            "Cancelled Try did not finish with bounded Finally");
                        var cancelled=handle.Completion.GetAwaiter().GetResult();
                        Check(cancelled.State==MRKFlowState.Cancelled&&
                            string.Join(",",calls.ToArray())=="finally",
                            "Cancel must skip Catch but run Finally and cancel Flow");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        /// <summary>P0-1：真实MRK事件异步唤醒、有界超时与取消；输入不是事件时必须失败。</summary>
        private static void P01EventAwaitTimeoutCancellationRoundtrip()
        {
            bool rejected=false;
            try{MOLControlBlock.AwaitEvent("inspection.ready",0);}
            catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"P0-1 accepted an unbounded event wait");
            rejected=false;
            try{MOLControlBlock.AwaitEvent(" ",50);}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"P0-1 accepted empty event source");

            var session=MEMEngineering.Create("P0-1 外部事件等待");
            var page=session.AddPage("Inspection");
            var wait=MOLControlBlock.AwaitEvent("inspection.ready",3000);
            var action=MOLControlBlock.Action("inspection.after_event");
            int invoked=0;
            var resolver=new TestResolver().Register("inspection.after_event",(ctx,ct)=>{
                System.Threading.Interlocked.Increment(ref invoked);
                return Task.FromResult((object)true);
            });
            MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(
                MOLControlBlock.Sequence(wait,action)));
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-event-wait-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(path);
                var opened=MEMEngineering.Open(path);
                try
                {
                    var restored=MOLLogicCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    Check(restored.Root.Children[0].Id==wait.Id&&
                        restored.Root.Children[0].Wait.EventInput=="inspection.ready"&&
                        restored.Root.Children[0].Wait.DurationMilliseconds==3000,
                        "P0-1 event contract or BlockId lost during project Save/Open");
                    var compiled=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
                    try
                    {
                        var ready=new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        var running=engine.StartFlow(compiled.Plan,
                            new Dictionary<string,object>{{"inspection.ready",ready.Task}});
                        Check(!running.Completion.Wait(TimeSpan.FromMilliseconds(40))&&
                            System.Threading.Volatile.Read(ref invoked)==0,
                            "P0-1 event wait ran downstream action before the signal");
                        ready.SetResult(true);
                        Check(running.Completion.Wait(TimeSpan.FromSeconds(5))&&
                            running.Completion.Result.State==MRKFlowState.Completed&&
                            System.Threading.Volatile.Read(ref invoked)==1,
                            "P0-1 actual MRK Flow did not resume on event");

                        var missing=engine.StartFlow(compiled.Plan);
                        Check(missing.Completion.Wait(TimeSpan.FromSeconds(5))&&
                            missing.Completion.Result.State==MRKFlowState.Failed&&
                            System.Threading.Volatile.Read(ref invoked)==1,
                            "P0-1 missing event Task incorrectly succeeded");
                        var wrongType=engine.StartFlow(compiled.Plan,
                            new Dictionary<string,object>{{"inspection.ready","signaled"}});
                        Check(wrongType.Completion.Wait(TimeSpan.FromSeconds(5))&&
                            wrongType.Completion.Result.State==MRKFlowState.Failed&&
                            System.Threading.Volatile.Read(ref invoked)==1,
                            "P0-1 non-Task event source incorrectly succeeded");

                        MOLLogicCodec.Set(opened,opened.Project.Pages[0].RootContent,
                            new MOLLogicDefinition(MOLControlBlock.Sequence(
                                MOLControlBlock.AwaitEvent("inspection.ready",60),action)));
                        var timed=new MOCEngineeringCompiler().Compile(opened.GetSnapshot(),
                            page.RootContent.Id,resolver);
                        var pending=new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        var timeout=engine.StartFlow(timed.Plan,
                            new Dictionary<string,object>{{"inspection.ready",pending.Task}});
                        Check(timeout.Completion.Wait(TimeSpan.FromSeconds(5))&&
                            timeout.Completion.Result.State==MRKFlowState.Failed&&
                            System.Threading.Volatile.Read(ref invoked)==1,
                            "P0-1 event timeout was not a failed MRK Flow");
                        var cancelled=engine.StartFlow(compiled.Plan,
                            new Dictionary<string,object>{{"inspection.ready",pending.Task}});
                        cancelled.RequestCancel();
                        Check(cancelled.Completion.Wait(TimeSpan.FromSeconds(5))&&
                            cancelled.Completion.Result.State==MRKFlowState.Cancelled&&
                            System.Threading.Volatile.Read(ref invoked)==1,
                            "P0-1 event cancellation ran downstream action or hung");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                }
                finally{opened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        private static void BranchParallelAndTrace()
        {
            var session = MEMEngineering.Create("branch");
            MEMPage page = session.AddPage("Inspect");
            var acquire = MOLControlBlock.Action("capture");
            var ai = MOLControlBlock.Action("ai");
            var cv = MOLControlBlock.Action("cv");
            var yes = MOLControlBlock.Action("ok");
            var no = MOLControlBlock.Action("ng");
            MOLControlBlock root = MOLControlBlock.Sequence(acquire,
                MOLControlBlock.Parallel(ai, cv),
                MOLControlBlock.If(MOLExpression.InputTrue("accepted"), yes, no));
            MOLLogicCodec.Set(session, page.RootContent, new MOLLogicDefinition(root));
            int aiCalls = 0, cvCalls = 0, yesCalls = 0, noCalls = 0;
            var resolver = new TestResolver()
                .Register("capture", (c,t) => Task.FromResult((object)1))
                .Register("ai", (c,t) => { Interlocked.Increment(ref aiCalls); return Task.FromResult((object)2); })
                .Register("cv", (c,t) => { Interlocked.Increment(ref cvCalls); return Task.FromResult((object)3); })
                .Register("ok", (c,t) => { Interlocked.Increment(ref yesCalls); return Task.FromResult((object)4); })
                .Register("ng", (c,t) => { Interlocked.Increment(ref noCalls); return Task.FromResult((object)5); });
            var compiler = new MOCEngineeringCompiler();
            MOCCompiledPlan plan = compiler.Compile(session.GetSnapshot(), page.RootContent.Id, resolver);
            MOCTraceView trace;
            var passed = Execute(plan, new Dictionary<string,object> {{"accepted", true}}, out trace);
            Check(passed.State == MRKFlowState.Completed &&
                aiCalls == 1 && cvCalls == 1 && yesCalls == 1 && noCalls == 0, "parallel true path");
            Check(plan.Plan.Regions.Count==1&&
                plan.Plan.Regions[0].Members.Count>=5,
                "MOC结构化Branch未生成真实MRK ControlRegion");
            Check(plan.SourceMap.Find(yes.Id).Count == 1 &&
                plan.SourceMap.Find(yes.Id)[0].ContentAddress.ObjectId == page.RootContent.Id,
                "stable source address");
            Check(trace.Events.Count != 0 &&
                trace.Events.All(e => e.Origin != null && e.Origin.BlockId != Guid.Empty),
                "runtime trace source map");
            Check(passed.StepStates[plan.SourceMap.Find(no.Id).Single().StepId] == MRKStepState.Skipped,
                "non selected branch is not Skipped");
            var rejected = Execute(plan, new Dictionary<string,object> {{"accepted", false}}, out trace);
            Check(rejected.State == MRKFlowState.Completed &&
                noCalls == 1 && yesCalls == 1, "mutually exclusive else path");
            var judge = MOLControlBlock.Action("judge");
            var selected = MOLControlBlock.Action("chosen");
            MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(
                MOLControlBlock.Sequence(judge, MOLControlBlock.If(
                    new MOLExpression(MOLExpressionSource.StepResult,judge.Id.ToString("N")),
                    selected,MOLControlBlock.Action("ng")))));
            var byResult = new TestResolver()
                .Register("judge",(c,t)=>Task.FromResult((object)true))
                .Register("chosen",(c,t)=>Task.FromResult((object)1))
                .Register("ng",(c,t)=>Task.FromResult((object)2));
            var conditional = compiler.Compile(session.GetSnapshot(),page.RootContent.Id,byResult);
            var fromResult = Execute(conditional,null,out trace);
            Check(fromResult.State==MRKFlowState.Completed &&
                fromResult.StepStates[conditional.SourceMap.Find(selected.Id).Single().StepId]
                    ==MRKStepState.Completed, "branch did not consume real preceding step result");

            session.Close(true);
        }

        private static void LoopKindsAndBoundedForEach()
        {
            var session = MEMEngineering.Create("loops");
            var page = session.AddPage("Loops");
            int count = 0;
            var action = MOLControlBlock.Action("inspect");
            var root = MOLControlBlock.Sequence(
                MOLControlBlock.Repeat(MOLLoopMode.For, 3, action),
                MOLControlBlock.Repeat(MOLLoopMode.While, 2, MOLControlBlock.Action("inspect"),
                    MOLExpression.InputTrue("go")),
                MOLControlBlock.Repeat(MOLLoopMode.DoWhile, 3,
                    MOLControlBlock.Action("inspect"), MOLExpression.Always(false)),
                MOLControlBlock.Each("images", 4, MOLControlBlock.Action("each")));
            MOLLogicCodec.Set(session, page.RootContent, new MOLLogicDefinition(root));
            var resolver = new TestResolver()
                .Register("inspect", (c,t) =>
                {
                    Check(c.BoundInputs.ContainsKey("iteration"), "loop iteration unbound");
                    Interlocked.Increment(ref count);
                    return Task.FromResult((object)1);
                })
                .Register("each", (c,t) =>
                {
                    Check(c.BoundInputs.ContainsKey("item") &&
                        c.BoundInputs.ContainsKey("iteration"), "foreach input missing");
                    Interlocked.Increment(ref count);
                    return Task.FromResult((object)c.BoundInputs["item"]);
                });
            var plan = new MOCEngineeringCompiler().Compile(session.GetSnapshot(), page.RootContent.Id, resolver);
            MOCTraceView trace;
            var result = Execute(plan, new Dictionary<string,object> {
                {"go",true}, {"images",new []{"a","b","c"}} },out trace);
            Check(result.State == MRKFlowState.Completed && count == 9,
                "For/While/DoWhile/Foreach actual invocation count");
            Check(plan.SourceMap.Find(action.Id).Count==1&&
                plan.Plan.Steps.Count==4&&
                plan.SourceMap.Find(action.Id)[0].Role=="Action",
                "loop Body should compile once, not clone 3 static iterations");
            session.Close(true);
        }

        /// <summary>MOL→MOC→MRK端到端：Foreach预算不足必须拒绝，而不是只处理前两张图片。</summary>
        private static void ForeachOverflowMustFailBeforeExecution()
        {
            var session=MEMEngineering.Create("Foreach全批次完整性");
            var page=session.AddPage("Batch");
            int inspected=0;
            try
            {
                MOLControlBlock loop=MOLControlBlock.Each("images",2,
                    MOLControlBlock.Action("inspect"));
                MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(loop));
                var resolver=new TestResolver().Register("inspect",(ctx,ct)=>{
                    Interlocked.Increment(ref inspected);
                    return Task.FromResult((object)ctx.BoundInputs["item"]);
                });
                var compiled=new MOCEngineeringCompiler().Compile(
                    session.GetSnapshot(),page.RootContent.Id,resolver);
                MOCTraceView trace;
                MRKFlowResult failed=Execute(compiled,
                    new Dictionary<string,object>{{"images",new[]{"a","b","c"}}},out trace);
                Check(failed.State==MRKFlowState.Failed&&
                    failed.Error!=null&&failed.Error.ToString().Contains(
                        "moc.foreach_budget_exceeded")&&
                    failed.CommittedOutputs.Count==0&&inspected==0,
                    "MOC Foreach ignored surplus image without failing the full batch");
                MRKFlowResult complete=Execute(compiled,
                    new Dictionary<string,object>{{"images",new[]{"a","b"}}},out trace);
                Check(complete.State==MRKFlowState.Completed&&inspected==2,
                    "MOC Foreach failed with an exact-size image batch");
            }
            finally{session.Close(true);}
        }

        /// <summary>
        /// R05真实动态循环：100000上限仅编译一个Loop节点；运行时多次激活
        /// 同一多节点Body，Break/Continue/Return及条件分支不泄漏。
        /// </summary>
        private static void R05DynamicLoopAndExit()
        {
            var session=MEMEngineering.Create("R05动态控制区域");
            var page=session.AddPage("DynLoop");
            try
            {
                bool invalidLimitRejected=false;
                try{MOLControlBlock.Repeat(MOLLoopMode.For,0,
                    MOLControlBlock.Action("invalid"));}
                catch(ArgumentOutOfRangeException){invalidLimitRejected=true;}
                Check(invalidLimitRejected,
                    "R05 must reject zero loop bound despite enabling dynamic large loops");
                int first=0,second=0;
                var a=MOLControlBlock.Action("first");
                var b=MOLControlBlock.Action("second");
                var loop=MOLControlBlock.Repeat(MOLLoopMode.While,100000,
                    MOLControlBlock.Sequence(a,b),
                    new MOLExpression(MOLExpressionSource.Iteration,
                        comparison:MOLComparison.LessThan,expected:"220"));
                MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(loop));
                var observed=new System.Collections.Concurrent.ConcurrentDictionary<int,int>();
                var resolver=new TestResolver()
                    .Register("first",(ctx,ct)=>{
                        Check(ctx.Invocation!=null&&ctx.Data==null&&
                            ctx.BoundInputs["iteration"] is int,
                            "dynamic loop step lost MRK invocation or index");
                        int n=(int)ctx.BoundInputs["iteration"];
                        Check(ctx.Invocation.FrameKey.Contains(".i"+n+"."),
                            "loop activation identity is not per iteration");
                        observed.AddOrUpdate(n,1,(key,val)=>val+1);
                        Interlocked.Increment(ref first);
                        return Task.FromResult((object)n);
                    })
                    .Register("second",(ctx,ct)=>{
                        Interlocked.Increment(ref second);
                        return Task.FromResult((object)ctx.BoundInputs["iteration"]);
                    });
                var compiled=new MOCEngineeringCompiler().Compile(
                    session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(compiled.Plan.Steps.Count==1&&
                    compiled.Plan.Steps[0].LoopRegion!=null&&
                    compiled.Plan.Steps[0].LoopRegion.Body.Steps.Count<=5&&
                    compiled.SourceMap.Find(a.Id).Count==1,
                    "R05 high-count loop still statically expands the body");
                var engine=new MRKExecutionEngine(new FCRWorkerOptions(3,128));
                try
                {
                    MRKFlowHandle handle=engine.StartFlow(compiled.Plan);
                    var result=handle.Completion.GetAwaiter().GetResult();
                    Check(result.State==MRKFlowState.Completed&&
                        first==220&&second==220&&observed.Count==220&&
                        observed.Values.All(x=>x==1),
                        "R05 dynamic activation count or repeated Body execution wrong");
                    Check(handle.Context.ObservationsSnapshot().Keys
                        .Count(x=>x.Contains(".i")&&x.Contains(a.Id.ToString("N")))==220,
                        "R05 per-iteration trace identity collapsed to one step");
                    var view=compiled.ReadTrace(handle,0);
                    Check(view.Events.Count!=0&&
                        view.Events.All(x=>x.Origin!=null&&x.Origin.BlockId!=Guid.Empty),
                        "R05 dynamic StepId cannot resolve its original MOC SourceMap");
                }
                finally{engine.StopAsync(true).GetAwaiter().GetResult();}

                int visit=0,after=0;
                var branch=MOLControlBlock.Branch(
                    new MOLBranchCase(new MOLExpression(MOLExpressionSource.Iteration,
                        comparison:MOLComparison.Equal,expected:"1"),
                        new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                            exitKind:MOLExitKind.Continue)),
                    new MOLBranchCase(new MOLExpression(MOLExpressionSource.Iteration,
                        comparison:MOLComparison.Equal,expected:"3"),
                        new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                            exitKind:MOLExitKind.Break)),
                    new MOLBranchCase(null,MOLControlBlock.Action("after")));
                var exitLoop=MOLControlBlock.Repeat(MOLLoopMode.For,200,
                    MOLControlBlock.Sequence(MOLControlBlock.Action("visit"),branch));
                MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(exitLoop));
                var guarded=new TestResolver()
                    .Register("visit",(ctx,ct)=>{
                        Interlocked.Increment(ref visit);
                        return Task.FromResult((object)ctx.IterationIndex);
                    })
                    .Register("after",(ctx,ct)=>{
                        Interlocked.Increment(ref after);
                        return Task.FromResult((object)ctx.IterationIndex);
                    });
                var plan=new MOCEngineeringCompiler().Compile(
                    session.GetSnapshot(),page.RootContent.Id,guarded);
                MOCTraceView trace;
                var result2=Execute(plan,null,out trace);
                Check(result2.State==MRKFlowState.Completed&&visit==4&&after==2&&
                    plan.Plan.Steps.Count==1,
                    "R05 Break/Continue did not settle remaining nodes at safe points: "+
                    "state="+result2.State+", visit="+visit+", after="+after+
                    ", steps="+plan.Plan.Steps.Count+
                    ", error="+(result2.Error==null?"none":result2.Error.ToString()));

                int tail=0;
                var terminating=MOLControlBlock.Sequence(
                    MOLControlBlock.Repeat(MOLLoopMode.For,10,
                        MOLControlBlock.Sequence(
                            MOLControlBlock.Action("visit"),
                            new MOLControlBlock(Guid.NewGuid(),MOLControlKind.Exit,
                                exitKind:MOLExitKind.Return))),
                    MOLControlBlock.Action("tail"));
                MOLLogicCodec.Set(session,page.RootContent,new MOLLogicDefinition(terminating));
                var returned=new TestResolver()
                    .Register("visit",(ctx,ct)=>Task.FromResult((object)1))
                    .Register("tail",(ctx,ct)=>{
                        Interlocked.Increment(ref tail);return Task.FromResult((object)0);
                    });
                var returnPlan=new MOCEngineeringCompiler().Compile(
                    session.GetSnapshot(),page.RootContent.Id,returned);
                var done=Execute(returnPlan,null,out trace);
                Check(done.State==MRKFlowState.Completed&&tail==0,
                    "R05 Return did not prevent downstream Flow execution");
            }
            finally{session.Close(true);}
        }

        private static void InvokedContentAndInvalidSource()
        {
            var session=MEMEngineering.Create("invoke");
            MEMPage page=session.AddPage("P");
            MEMContent child=session.AddContent(session.Project,"Shared");
            var internalStep=MOLControlBlock.Action("work");
            MOLLogicCodec.Set(session, child, new MOLLogicDefinition(internalStep));
            var call=MOLControlBlock.InvokeContent(child.Id);
            MOLLogicCodec.Set(session, page.RootContent, new MOLLogicDefinition(call));
            int calls=0;
            var resolver=new TestResolver().Register("work", (c,t) =>
            { Interlocked.Increment(ref calls); return Task.FromResult((object)1); });
            bool missingReference=false;
            try{new MOCEngineeringCompiler().Compile(session.GetSnapshot(),
                page.RootContent.Id,resolver);}
            catch(InvalidOperationException ex)
            {missingReference=ex.Message=="moc.invoke_missing_content_ref";}
            Check(missingReference,
                "V1 Invoke without MEM ContentReference unexpectedly compiled");
            session.Edit("引用结构化子逻辑",e=>{
                e.Reference(page.RootContent,child,"Shared");return true;
            }).RequireValue();
            var plan=new MOCEngineeringCompiler().Compile(session.GetSnapshot(),page.RootContent.Id,resolver);
            MOCTraceView trace;
            var result=Execute(plan,null,out trace);
            Check(result.State==MRKFlowState.Completed && calls==1 &&
                plan.SourceMap.Find(internalStep.Id).Single().CallPath.Single()==call.Id &&
                plan.SourceMap.Find(internalStep.Id).Single().ContentAddress.ObjectId==child.Id,
                "subflow expanded source call chain");
            bool failed=false;
            try { new MOCEngineeringCompiler().Compile(session.GetSnapshot(),page.RootContent.Id,
                new TestResolver()); } catch(InvalidOperationException){ failed=true; }
            Check(failed,"unknown provider silently built a runnable plan");
            session.Close(true);
        }

        // V2自由连接：保持V1独立编译，验证真实文件保存/重开和非法连接拒绝。
        private static void V2WiringRoundtrip()
        {
            var first=MOLControlBlock.Action("capture");
            var second=MOLControlBlock.Action("inspect");
            var last=MOLControlBlock.Action("judge");
            var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
            ports[first.Id]=new[]{
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                new MPCPortDescriptor("Image",MPCPortDirection.Output,"image",MPCPortKind.Data)};
            ports[second.Id]=new[]{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",MPCPortKind.Control,
                    required:true),
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                new MPCPortDescriptor("Image",MPCPortDirection.Input,"image",MPCPortKind.Data,
                    required:true)};
            ports[last.Id]=new[]{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",MPCPortKind.Control,
                    required:true)};
            var a=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(first.Id,"Done"),
                new MOLBlockEndpoint(second.Id,"Start"),MPCPortKind.Control);
            var b=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(second.Id,"Done"),
                new MOLBlockEndpoint(last.Id,"Start"),MPCPortKind.Control);
            var data=new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(first.Id,"Image"),
                new MOLBlockEndpoint(second.Id,"Image"),MPCPortKind.Data);
            var definition=new MOLWiringDefinition(first.Id,new[]{first,second,last},ports,
                new[]{a,b,data});
            var reread=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(definition));
            Check(reread.Links.Count==3&&reread.Links[2].Id==data.Id&&
                reread.Blocks[1].Id==second.Id,"V2 codec identity");
            var session=MEMEngineering.Create("FreeWiring");
            var page=session.AddPage("Main");
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "cx-wiring-"+Guid.NewGuid().ToString("N")+".cxproj");
            try{
                MOLWiringCodec.Set(session,page.RootContent,definition);
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try{
                    var wire=MOLWiringCodec.Get(reopened.GetSnapshot().Find(page.RootContent.Id));
                    Check(wire!=null&&wire.EntryBlockId==first.Id&&wire.Links[0].Id==a.Id,
                        "V2 file roundtrip");
                }finally{reopened.Close(true);}
            }finally{session.Close(true);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
            bool rejected=false;
            try{new MOLWiringDefinition(first.Id,new[]{first,second,last},ports,
                    new[]{a,a,b,data});}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"duplicate link rejected");
            rejected=false;
            try{new MOLWiringDefinition(first.Id,new[]{first,second,last},ports,
                    new[]{a,b,data,new MOLLink(Guid.NewGuid(),
                      new MOLBlockEndpoint(last.Id,"Start"),
                      new MOLBlockEndpoint(first.Id,"Done"),MPCPortKind.Control)});}
            catch(InvalidOperationException){rejected=true;}
            Check(rejected,"reversed port rejected");
        }


        // 保存重开后编译V2有向边：真实StepResult绑定、顺序及SourceMap。
        private static void V2WiringCompileRun()
        {
            var session=MEMEngineering.Create("V2Runtime");
            var page=session.AddPage("Main");
            var acquire=MOLControlBlock.Action("capture");
            var inspect=MOLControlBlock.Action("inspect");
            var judge=MOLControlBlock.Action("judge");
            var ports=new Dictionary<Guid,IEnumerable<MPCPortDescriptor>>();
            ports[acquire.Id]=new[]{
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                new MPCPortDescriptor("Image",MPCPortDirection.Output,"int",MPCPortKind.Data)};
            ports[inspect.Id]=new[]{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",MPCPortKind.Control,required:true),
                new MPCPortDescriptor("Done",MPCPortDirection.Output,"signal",MPCPortKind.Control),
                new MPCPortDescriptor("Image",MPCPortDirection.Input,"int",MPCPortKind.Data,
                    required:true)};
            ports[judge.Id]=new[]{
                new MPCPortDescriptor("Start",MPCPortDirection.Input,"signal",MPCPortKind.Control,required:true)};
            var links=new[]{
                new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(acquire.Id,"Done"),
                    new MOLBlockEndpoint(inspect.Id,"Start"),MPCPortKind.Control),
                new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(inspect.Id,"Done"),
                    new MOLBlockEndpoint(judge.Id,"Start"),MPCPortKind.Control),
                new MOLLink(Guid.NewGuid(),new MOLBlockEndpoint(acquire.Id,"Image"),
                    new MOLBlockEndpoint(inspect.Id,"Image"),MPCPortKind.Data)};
            MOLWiringCodec.Set(session,page.RootContent,new MOLWiringDefinition(acquire.Id,
                new[]{acquire,inspect,judge},ports,links));
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-wired-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.SaveAs(path);
                var reloaded=MEMEngineering.Open(path);
                try
                {
                    int calls=0;
                    var resolver=new TestResolver()
                        .Register("capture",(c,t)=>{Interlocked.Increment(ref calls);
                            return Task.FromResult((object)42);})
                        .Register("inspect",(c,t)=>{
                            Check((int)c.BoundInputs["Image"]==42,"V2 real data edge");
                            Interlocked.Increment(ref calls);return Task.FromResult((object)true);})
                        .Register("judge",(c,t)=>{
                            Check(calls==2,"V2 real Control dependencies");
                            Interlocked.Increment(ref calls);return Task.FromResult((object)1);});
                    var compiled=new MOCEngineeringCompiler().Compile(
                        reloaded.GetSnapshot(),page.RootContent.Id,resolver);
                    MOCTraceView trace;
                    MRKFlowResult result=Execute(compiled,null,out trace);
                    Check(result.State==MRKFlowState.Completed&&calls==3,
                        "V2 compiled CPU flow");
                    Check(compiled.SourceMap.Find(inspect.Id).Count==1&&
                        trace.Events.Any(x=>x.Origin.BlockId==inspect.Id),
                        "V2 SourceMap and trace");
                    bool rejected=false;
                    try {new MOCEngineeringCompiler().Compile(
                        reloaded.GetSnapshot(),page.RootContent.Id,new TestResolver());}
                    catch(InvalidOperationException){rejected=true;}
                    Check(rejected,"V2 unavailable capability was not rejected");
                }
                finally{reloaded.Close(true);}
            }
            finally{session.Close(true);if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }

        private static void V2BuilderSharedDefinition()
        {
            var session=MEMEngineering.Create("Builder");
            var page=session.AddPage("P");
            Guid acquire=Guid.Empty, inspect=Guid.Empty;
            try
            {
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    acquire=b.Step("capture");
                    inspect=b.Step("inspect");
                    b.Connect(acquire,"Done",inspect,"Start");
                });
                int revision=session.Revision;
                var model=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(model!=null&&model.Links.Count==1&&model.Blocks[0].Id==acquire,
                    "builder uses authoritative Content Logic");
                bool rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.Connect(inspect,"Done",acquire,"Start"));}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "builder cycle rejected without commit");
                var resolver=new TestResolver()
                    .Register("capture",(c,t)=>Task.FromResult((object)1))
                    .Register("inspect",(c,t)=>Task.FromResult((object)2));
                var plan=new MOCEngineeringCompiler().Compile(session.GetSnapshot(),
                    page.RootContent.Id,resolver);
                MOCTraceView trace;
                var result=Execute(plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&
                    trace.Events.Any(x=>x.Origin.BlockId==inspect),
                    "builder content compiled and traced");
            }
            finally{session.Close(true);}
        }

        private static void V2DraftPortsUndoAndCompileGate()
        {
            var session=MEMEngineering.Create("自由编辑草稿");
            var page=session.AddPage("Flow");
            try
            {
                Guid first=Guid.Empty, second=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    first=b.Step("capture");
                    second=b.Step("inspect");
                    b.AddPort(first,new MPCPortDescriptor("Image",MPCPortDirection.Output,
                        "int",MPCPortKind.Data));
                    b.AddPort(second,new MPCPortDescriptor("Image",MPCPortDirection.Input,
                        "int",MPCPortKind.Data,required:true));
                });
                var stored=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(stored!=null&&stored.Blocks.Count==2&&stored.Links.Count==0,
                    "draft could not persist disconnected Blocks");
                bool rejected=false;
                var provider=new TestResolver()
                    .Register("capture",(c,t)=>Task.FromResult((object)42))
                    .Register("inspect",(c,t)=>Task.FromResult((object)1));
                try{new MOCEngineeringCompiler().Compile(session.GetSnapshot(),
                    page.RootContent.Id,provider);}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"unfinished draft was compiled");
                int revision=session.Revision;
                rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    b.Connect(second,"Image",first,"Image",MPCPortKind.Data);
                });}catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "illegal port direction changed document");
                rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    b.RemovePort(first,"Image");
                    b.Connect(first,"Image",second,"Image",MPCPortKind.Data);
                });}catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "failed edit transaction did not roll back");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    b.SetCapability(first,"capture");
                    b.Connect(first,"Done",second,"Start",MPCPortKind.Control);
                    b.Connect(first,"Image",second,"Image",MPCPortKind.Data);
                });
                var complete=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(complete.Links.Count==2&&complete.Blocks[0].Id==first,
                    "connection edit corrupted stable identity");
                complete.ValidateExecutable();
                var reverted=session.Undo();
                Check(reverted.Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id)).Links.Count==0,
                    "Undo failed to restore draft");
                Check(session.Redo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id)).Links.Count==2,
                    "Redo failed to restore wiring");
                rejected=false;
                try { session.Edit("stale",revision,e=>true).RequireValue(); }
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"stale edit revision should be rejected");
            }
            finally{session.Close(true);}
        }

        private sealed class DemoCapabilityProvider:IMOCCapabilityProvider
        {
            private readonly MOCCapabilityDescriptor[] _definitions;
            internal DemoCapabilityProvider(params MOCCapabilityDescriptor[] types){_definitions=types;}
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities(){return _definitions;}
        }
        private static void V2CapabilityDiscovery()
        {
            var capture=new MOCCapabilityDescriptor("vision.capture","采图","Vision");
            var inspect=new MOCCapabilityDescriptor("vision.inspect","图像检测","Vision",
                new[]{new MPCPortDescriptor("Image",MPCPortDirection.Input,"int",
                    MPCPortKind.Data,required:true)});
            var resolver=new TestResolver()
                .Register("vision.capture",(ctx,t)=>Task.FromResult((object)7));
            var catalog=new MOCCapabilityCatalog(
                new[]{new DemoCapabilityProvider(capture,inspect)},resolver);
            Check(catalog.All.Count==2&&catalog.Search("inspect").Count==1,
                "capability discovery/search");
            Check(catalog.Find("vision.capture").RuntimeAvailable&&
                !catalog.Find("vision.inspect").RuntimeAvailable,
                "unresolved professional capability reported available");
            bool rejected=false;
            try{new MOCCapabilityCatalog(new[]{
                new DemoCapabilityProvider(inspect),new DemoCapabilityProvider(inspect)},resolver);}
            catch(InvalidOperationException){rejected=true;}
            Check(rejected,"catalog silently merged duplicate capability");
            var session=MEMEngineering.Create("能力发现");
            try
            {
                var page=session.AddPage("Main");
                Guid id=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>id=b.Step(inspect));
                var def=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(def.Blocks[0].Id==id&&def.Ports[id].Any(x=>x.MemberId=="Image"),
                    "capability ports not used by common Builder");
            }
            finally{session.Close(true);}
        }

        private static void V2LayoutContract()
        {
            var session=MEMEngineering.Create("可视编排布局契约");
            var page=session.AddPage("Main");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-layout-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid first=Guid.Empty,second=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    first=b.Step("capture");
                    second=b.Step("inspect");
                    b.Connect(first,"Done",second,"Start");
                });
                var initial=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(initial.Positions.Count==0,"old V2 position-free layout must be accepted");
                var oldPayload=MOLWiringCodec.Serialize(initial);
                Check(MOLWiringCodec.Deserialize(oldPayload).Positions.Count==0,
                    "old V2 codec unexpectedly required layout coordinates");

                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.Move(second,145.5,203.25));
                var located=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(located.Positions.ContainsKey(second)&&
                    Math.Abs(located.Positions[second].X-145.5)<0.001&&
                    located.Links.Count==1,"Builder move changed link identity or lost position");
                var payload=MOLWiringCodec.Serialize(located);
                var decoded=MOLWiringCodec.Deserialize(payload);
                Check(decoded.Positions.ContainsKey(second)&&
                    Math.Abs(decoded.Positions[second].Y-203.25)<0.001,
                    "V2 layout XML roundtrip dropped position");

                int revision=session.Revision;
                bool rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,
                    b=>b.Move(second,double.NaN,9));}
                catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "invalid or non-finite position mutated the engineering document");
                rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,
                    b=>b.Move(Guid.NewGuid(),15,22));}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "unknown BlockId unexpectedly accepted a layout edit");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.Step("more"));
                var afterStep=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(afterStep.Positions.ContainsKey(second)&&
                    afterStep.Links[0].Id==located.Links[0].Id,
                    "subsequent Builder edits failed to preserve layout/link identity");
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var persisted=MOLWiringCodec.Get(reopened.GetSnapshot().Find(page.RootContent.Id));
                    Check(persisted.Positions.ContainsKey(second)&&
                        Math.Abs(persisted.Positions[second].X-145.5)<0.001,
                        "actual cxproj Save/Open lost V2 optional layout");
                }
                finally{reopened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        /// <summary>C验收：X-Ray主流程与AI/CV两个真实可执行Content子流程。</summary>
        private static void CThreeTierXRayTwoSubflows()
        {
            var session=MEMEngineering.Create("C-XRay主从工程");
            var page=session.AddPage("X-Ray检测线");
            Guid acquire=Guid.Empty,ai=Guid.Empty,aiJudge=Guid.Empty,
                cv=Guid.Empty,cvJudge=Guid.Empty,quality=Guid.Empty,result=Guid.Empty;
            string file=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "cxvisual-c-xray-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    acquire=b.Step("xray.acquire");
                    ai=b.Step("xray.ai");
                    aiJudge=b.Step("xray.ai.post");
                    cv=b.Step("xray.cv");
                    cvJudge=b.Step("xray.cv.post");
                    quality=b.Step("xray.judge");
                    result=b.Step("xray.result");
                    var steps=new[]{acquire,ai,aiJudge,cv,cvJudge,quality,result};
                    for(int i=1;i<steps.Length;i++)
                        b.Connect(steps[i-1],"Done",steps[i],"Start");
                });
                var aiContent=MOCFlowBuilder.ExtractLinearContent(session,page.RootContent,
                    new[]{ai,aiJudge},"AI检测子流程");
                var cvContent=MOCFlowBuilder.ExtractLinearContent(session,page.RootContent,
                    new[]{cv,cvJudge},"传统算法子流程");
                var root=session.GetSnapshot().Find(page.RootContent.Id);
                var wiring=MOLWiringCodec.Get(root);
                Check(root.References.Count==2&&wiring.Blocks.Count==5&&
                    wiring.Links.Count==4&&
                    wiring.Blocks.Count(x=>x.CapabilityId.StartsWith("moc.invoke:",
                        StringComparison.Ordinal))==2,
                    "C main flow must own two executable Content references, not canvas-only groups");
                int executed=0;
                var resolver=new TestResolver();
                foreach(string id in new[]{"xray.acquire","xray.ai","xray.ai.post",
                    "xray.cv","xray.cv.post","xray.judge","xray.result"})
                {
                    string capability=id;
                    resolver.Register(capability,(ctx,token)=>{
                        Interlocked.Increment(ref executed);
                        return Task.FromResult((object)capability);
                    });
                }
                var build=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(build.Succeeded,"C two subflow build failed: "+
                    string.Join(",",build.Diagnostics.Select(x=>x.Code+" "+x.Message)));
                MOCTraceView trace;
                var finished=Execute(build.Plan,null,out trace);
                Check(finished.State==MRKFlowState.Completed&&executed==7&&
                    trace.Events.Count>=7,"C actual MRK main/child flows didn't all execute");
                foreach(var item in new[]{
                    new{Id=ai,Content=aiContent.Id},
                    new{Id=aiJudge,Content=aiContent.Id},
                    new{Id=cv,Content=cvContent.Id},
                    new{Id=cvJudge,Content=cvContent.Id}})
                {
                    var origin=build.Plan.SourceMap.Find(item.Id).Single();
                    Check(origin.ContentAddress.ObjectId==item.Content&&
                        origin.CallPath.Count==1&&
                        trace.Events.Any(ev=>ev.Origin.BlockId==item.Id&&
                            ev.Transition.State==MRKStepState.Completed),
                        "C failed to map child MRK transition back to its real source Content");
                }
                session.SaveAs(file);
                using(var restored=MEMEngineering.Open(file))
                {
                    var snapshot=restored.GetSnapshot();
                    Check(snapshot.Find(aiContent.Id)!=null&&snapshot.Find(cvContent.Id)!=null&&
                        snapshot.Find(page.RootContent.Id).References.Count==2,
                        "C two executable subflows were not persisted with their references");
                    var reopenedBuild=new MOCBuildService().Build(snapshot,
                        page.RootContent.Id,resolver);
                    Check(reopenedBuild.Succeeded,"C reopened project did not recompile");
                }
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(file))System.IO.File.Delete(file);
                if(System.IO.File.Exists(file+".cx-lock"))System.IO.File.Delete(file+".cx-lock");
            }
        }

        /// <summary>G04-F：提取带Data参数的Control子流程，按两次调用隔离MRK绑定。</summary>
        /// <summary>G06：真实CPU像素处理经V2 MOC构建、MRK调度和专业Provider，不用模拟结果。</summary>
        private static void G06CpuVisionThroughMOC()
        {
            var session=MEMEngineering.Create("G06工业视觉CPU能力");
            var page=session.AddPage("在线阈值检测");
            string filename=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-vision-"+Guid.NewGuid().ToString("N")+".cxproj");
            string configDirectory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "mhs-vision-provider-"+Guid.NewGuid().ToString("N"));
            try
            {
                Guid source=Guid.Empty,inspect=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    source=b.Step("vision.test.source");
                    inspect=b.Step(CxVisionThresholdProvider.CapabilityId);
                    b.AddPort(source,new MPCPortDescriptor("Pixels",
                        MPCPortDirection.Output,"byte[]",MPCPortKind.Data));
                    b.AddPort(inspect,new MPCPortDescriptor("RawPixels",
                        MPCPortDirection.Input,"byte[]",MPCPortKind.Data,required:true));
                    b.AddPort(inspect,new MPCPortDescriptor("Width",
                        MPCPortDirection.Input,"int",MPCPortKind.Data,required:true));
                    b.AddPort(inspect,new MPCPortDescriptor("Height",
                        MPCPortDirection.Input,"int",MPCPortKind.Data,required:true));
                    b.SetParameter(inspect,"Width","5");
                    b.SetParameter(inspect,"Height","3");
                    b.Connect(source,"Done",inspect,"Start");
                    b.Connect(source,"Pixels",inspect,"RawPixels",MPCPortKind.Data);
                });
                var options=new CxVisionInspectionOptions{
                    PixelDepth=CxVisionPixelDepth.Bits8,GrayThreshold=200,
                    MinDefectArea=2,MaxDefectArea=100,UseAi=false};
                byte[] pixels={210,210,0,0,0,210,0,0,0,0,0,0,0,0,250};
                var sourceResolver=new TestResolver().Register("vision.test.source",
                    (ctx,token)=>Task.FromResult((object)((byte[])pixels.Clone())));
                var provider=new CxVisionThresholdProvider(options,12,sourceResolver);
                Check(provider.ConfigurationRevision==12&&
                    provider.ReleaseVersion=="vision.cpu.threshold.v1/config:12"&&
                    new CxVisionThresholdProvider(options,13).ReleaseVersion!=provider.ReleaseVersion,
                    "G06 real Vision Provider failed to expose config-bound immutable release version");
                // 必需Data输入允许类型化参数满足，但移除参数后必须由构建门禁拒绝。
                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.RemoveParameter(inspect,"Width"));
                var incomplete=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,provider);
                Check(!incomplete.Succeeded&&incomplete.Diagnostics.Any(d=>
                    d.Code=="MOC_WIRING_INVALID"),
                    "G06 missing required Width parameter escaped V2 compile gate");
                Check(session.Undo().Success,"G06 missing required input Undo failed");
                var build=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,provider);
                Check(build.Succeeded,"G06 Vision real MOC build failed: "+
                    string.Join(";",build.Diagnostics.Select(x=>x.Code+":"+x.Message)));
                MOCTraceView trace;
                var result=Execute(build.Plan,
                    build.Plan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value),out trace);
                Check(result.State==MRKFlowState.Completed&&
                    result.Results.ContainsKey("w"+inspect.ToString("N")),
                    "G06 real Vision capability did not reach MRK StepResult");
                var image=result.Results["w"+inspect.ToString("N")] as CxVisionInspectionResult;
                Check(image!=null&&image.ConfigRevision==12&&image.Width==5&&
                    image.Height==3&&image.ThresholdPixelCount==4&&
                    image.Regions.Count==1&&image.Regions[0].PixelArea==3&&
                    !image.Passed,
                    "G06 pixel threshold/connected regions were not actually computed");
                Check(trace.Events.Any(e=>e.Origin.BlockId==inspect&&
                    e.Transition.State==MRKStepState.Completed),
                    "G06 true professional Provider lost MRK SourceMap tracing");
                // G07+G06：专业CPU算法和配置Revision进入已有MOC锁定→MRK授权发布门。
                var declared=new Dictionary<string,string>(StringComparer.Ordinal){
                    {"vision.test.source","g06.source.v1"},
                    {CxVisionThresholdProvider.CapabilityId,provider.ReleaseVersion}};
                var candidate=MOCProjectRelease.Prepare(session.GetSnapshot(),
                    page.RootContent.Id,provider,declared);
                var spoofed=new Dictionary<string,string>(declared,StringComparer.Ordinal);
                spoofed[CxVisionThresholdProvider.CapabilityId]=
                    "vision.cpu.threshold.v1/config:999";
                bool refusedAttestation=false;
                try{MOCProjectRelease.Prepare(session.GetSnapshot(),
                    page.RootContent.Id,provider,spoofed);}
                catch(InvalidOperationException ex)
                {refusedAttestation=ex.Message.Contains("provider_version_mismatch");}
                Check(refusedAttestation,
                    "G06 Provider真实配置版本未拦截Host虚假版本声明");
                Check(candidate.CapabilityVersions[CxVisionThresholdProvider.CapabilityId]
                    ==provider.ReleaseVersion&&candidate.EngineeringRevision==session.Revision,
                    "G06 professional version was not pinned into genuine MOC release");
                var prior=new MRKExecutionPlan("g06.prior",new[]{
                    new MRKExecutionStep("noop",new string[0],
                        (c,t)=>Task.FromResult((object)0))});
                var slot=new MRKExecutionPlanSlot(prior,1,true);
                var policy=new ProjectReleaseTestPolicy();
                var publisher=new MRKExecutionPlanPublisher(slot,prior,1,policy);
                bool refused=false;
                try{MOCProjectRelease.Stage(candidate,session.GetSnapshot(),provider,
                    declared,publisher,"visitor",2);}
                catch(UnauthorizedAccessException){refused=true;}
                Check(refused&&!publisher.PendingRevision.HasValue,
                    "G06 unapproved professional release unexpectedly entered MRK slot");
                var receipt=MOCProjectRelease.Stage(candidate,session.GetSnapshot(),provider,
                    declared,publisher,"release-officer",2);
                Check(receipt.Accepted&&!publisher.TryActivate("release-officer"),
                    "G06 published professional software plan ignored Host safety veto");
                policy.Safe=true;
                Check(publisher.TryActivate("release-officer")&&publisher.ActiveRevision==2,
                    "G06 approved and safe Vision CPU plan was not activated");
                using(var lease=slot.Capture())
                {
                    Check(object.ReferenceEquals(lease.Plan,candidate.Plan.Plan),
                        "G06 published MRK Plan differs from MOC pinned candidate");
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
                    try
                    {
                        MRKFlowHandle flow=engine.StartFlow(lease.Plan,
                            candidate.Plan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value));
                        var publishedRun=flow.Completion.GetAwaiter().GetResult();
                        var publishedTrace=candidate.Plan.ReadTrace(flow,0);
                        Check(publishedRun.State==MRKFlowState.Completed&&
                            publishedTrace.Events.Any(e=>e.Origin.BlockId==inspect&&
                                e.Transition.State==MRKStepState.Completed),
                            "G06 approved version did not execute original professional algorithm");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                }
                var upgradedProvider=new CxVisionThresholdProvider(options,13,sourceResolver);
                bool refusedStaleProvider=false;
                try{MOCProjectRelease.Stage(candidate,session.GetSnapshot(),
                    upgradedProvider,declared,publisher,"release-officer",3);}
                catch(InvalidOperationException ex)
                {refusedStaleProvider=ex.Message.Contains("provider_version_mismatch");}
                Check(refusedStaleProvider&&!publisher.PendingRevision.HasValue,
                    "G06 Host漏报升级后的真实Vision Provider版本，旧Plan却进入了发布Slot");
                var changed=new Dictionary<string,string>(declared,StringComparer.Ordinal);
                changed[CxVisionThresholdProvider.CapabilityId]=
                    upgradedProvider.ReleaseVersion;
                refused=false;
                try{MOCProjectRelease.Stage(candidate,session.GetSnapshot(),provider,
                    changed,publisher,"release-officer",3);}
                catch(InvalidOperationException){refused=true;}
                Check(refused&&!publisher.PendingRevision.HasValue&&
                    publisher.ActiveRevision==2,
                    "G06 provider config update staged stale plan or contaminated active version");

                // 真实MHS Saved/Effective → 专业Provider快照 → MOC→MRK发布。
                // 不允许业务层手抄配置Revision，避免Saved误当Effective。
                var configService=new MHSConfigService(configDirectory);
                MHSConfigSection<CxVisionInspectionOptions> config=
                    configService.ForProject(session.Project.Id.ToString("N"))
                        .Open<CxVisionInspectionOptions>();
                MHSConfigCommitResult initialSaved=config.UpdateAsync(c=>{
                    c.PixelDepth=CxVisionPixelDepth.Bits8;
                    c.GrayThreshold=200;
                    c.MinDefectArea=2;
                    c.MaxDefectArea=100;
                    c.UseAi=false;
                },"G06准备真实MHS八位检测配置",CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(initialSaved.Success,"G06 MHS有效配置初次保存失败");
                if(config.HasPendingApply)
                    Check(config.ApplyPendingAtTaskBoundary(),
                        "G06 MHS NextTask有效配置未能在任务边界生效");
                var effectiveProvider=CxVisionThresholdProvider.FromEffectiveConfiguration(
                    config,sourceResolver);
                long effectiveVersion=long.Parse(config.EffectiveVersion.Substring(1),
                    System.Globalization.CultureInfo.InvariantCulture);
                Check(effectiveProvider.ConfigurationRevision==effectiveVersion&&
                    effectiveProvider.ReleaseVersion.EndsWith("config:"+effectiveVersion,
                        StringComparison.Ordinal),
                    "G06 Provider未从MHS Effective真源取得实际配置Revision");
                var effectiveVersions=new Dictionary<string,string>(
                    StringComparer.Ordinal){
                    {"vision.test.source","g06.source.v1"},
                    {CxVisionThresholdProvider.CapabilityId,effectiveProvider.ReleaseVersion}};
                MOCProjectReleaseCandidate effectiveCandidate=MOCProjectRelease.Prepare(
                    session.GetSnapshot(),page.RootContent.Id,effectiveProvider,effectiveVersions);
                MOCTraceView effectiveTrace;
                MRKFlowResult effectiveResult=Execute(effectiveCandidate.Plan,
                    effectiveCandidate.Plan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value),
                    out effectiveTrace);
                var actualImage=effectiveResult.Results["w"+inspect.ToString("N")]
                    as CxVisionInspectionResult;
                Check(effectiveResult.State==MRKFlowState.Completed&&
                    actualImage!=null&&actualImage.ConfigRevision==effectiveVersion&&
                    actualImage.ThresholdPixelCount==4,
                    "G06 MHS Effective配置没有实际用于MRK图像检测");
                MHSConfigCommitResult savedOnly=config.UpdateAsync(c=>{
                    c.GrayThreshold=220;
                },"G06下一任务候选阈值",CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(savedOnly.Success&&config.HasPendingApply&&
                    config.EffectiveVersion!=config.SavedVersion,
                    "G06 NextTask阈值没有保持Saved/Effective分离");
                var currentBeforeApply=CxVisionThresholdProvider.FromEffectiveConfiguration(
                    config,sourceResolver);
                Check(currentBeforeApply.ReleaseVersion==effectiveProvider.ReleaseVersion,
                    "G06 未生效Saved版本污染了正在使用的Provider软件版本");
                // 仅保存尚未生效的候选，不应让当前Effective的Provider提前失效。
                var beforeApplyCandidate=MOCProjectRelease.Prepare(session.GetSnapshot(),
                    page.RootContent.Id,currentBeforeApply,effectiveVersions);
                Check(beforeApplyCandidate.Fingerprint==effectiveCandidate.Fingerprint,
                    "G06只有Saved变化时错误拒绝旧Effective的软件发布身份");
                Check(config.ApplyPendingAtTaskBoundary(),
                    "G06 Pending NextTask配置未在任务边界生效");
                var nextProvider=CxVisionThresholdProvider.FromEffectiveConfiguration(
                    config,sourceResolver);
                bool oldEffectiveRejected=false;
                try{MOCProjectRelease.Stage(effectiveCandidate,session.GetSnapshot(),
                    effectiveProvider,effectiveVersions,publisher,"release-officer",3);}
                catch(InvalidOperationException ex)
                {oldEffectiveRejected=ex.Message.Contains("effective_version_changed");}
                Check(oldEffectiveRejected&&publisher.ActiveRevision==2&&
                    !publisher.PendingRevision.HasValue,
                    "G06 MHS已切换Effective但旧快照Provider竟可重新发布旧Plan");
                Check(nextProvider.ConfigurationRevision>effectiveVersion&&
                    nextProvider.ReleaseVersion!=effectiveProvider.ReleaseVersion,
                    "G06 配置任务边界版本未触发新Provider身份");
                bool staleRejected=false;
                try{MOCProjectRelease.Stage(effectiveCandidate,session.GetSnapshot(),
                    nextProvider,effectiveVersions,publisher,"release-officer",3);}
                catch(InvalidOperationException ex)
                {staleRejected=ex.Message.Contains("provider_version_mismatch");}
                Check(staleRejected&&publisher.ActiveRevision==2&&
                    !publisher.PendingRevision.HasValue,
                    "G06 MHS新Effective版本未阻断旧发布候选或污染当前Plan");
                var nextVersions=new Dictionary<string,string>(effectiveVersions,
                    StringComparer.Ordinal);
                nextVersions[CxVisionThresholdProvider.CapabilityId]=
                    nextProvider.ReleaseVersion;
                var nextCandidate=MOCProjectRelease.Prepare(session.GetSnapshot(),
                    page.RootContent.Id,nextProvider,nextVersions);
                Check(nextCandidate.Fingerprint!=effectiveCandidate.Fingerprint,
                    "G06新MHS Effective配置没有产生不同软件发布指纹");
                session.SaveAs(filename);
                using(var reopened=MEMEngineering.Open(filename))
                {
                    var repeat=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,provider);
                    Check(repeat.Succeeded&&repeat.Plan.InputDefaults.Count==2,
                        "G06 CPU Vision lost Port and parameters after real .cxproj reopen");
                }
                var invalidSource=new TestResolver().Register("vision.test.source",
                    (ctx,token)=>Task.FromResult((object)new byte[]{210,210}));
                var invalidProvider=new CxVisionThresholdProvider(options,12,invalidSource);
                var invalidBuild=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,invalidProvider);
                Check(invalidBuild.Succeeded,"G06 invalid-data test failed at compile");
                var failure=Execute(invalidBuild.Plan,
                    invalidBuild.Plan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value),out trace);
                Check(failure.State==MRKFlowState.Failed,
                    "G06 truncated detector bytes were silently accepted");
                bool unsupported=false;
                options.UseAi=true;
                options.AiModelName="unsupported-ai.onnx"; // 合法AI配置，CPU Provider仍必须拒绝执行
                try{new CxVisionThresholdProvider(options,12);}
                catch(NotSupportedException){unsupported=true;}
                Check(unsupported,"G06 CPU threshold Provider falsely claims to implement AI");
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(filename))System.IO.File.Delete(filename);
                if(System.IO.File.Exists(filename+".cx-lock"))
                    System.IO.File.Delete(filename+".cx-lock");
                if(System.IO.Directory.Exists(configDirectory))
                    System.IO.Directory.Delete(configDirectory,true);
            }
        }

        private sealed class ProjectReleaseTestPolicy : IMRKPlanPublicationPolicy
        {
            public bool Safe;
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {return principal=="release-officer";}
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision){return Safe;}
        }

        /// <summary>G07：来源Logic与版本锁核对、授权、设备安全点拒绝、在途版本及变更反证。</summary>
        private static void G07ProjectReleaseLockAndAuthorization()
        {
            var session=MEMEngineering.Create("G07软件发布候选锁定");
            var page=session.AddPage("发布页");
            var path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-release-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid first=Guid.Empty,second=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    first=b.Step("acquire");
                    second=b.Step("analyze");
                    b.Connect(first,"Done",second,"Start");
                });
                var resolver=new TestResolver()
                    .Register("acquire",(c,t)=>Task.FromResult((object)1))
                    .Register("analyze",(c,t)=>Task.FromResult((object)2));
                var versions=new Dictionary<string,string>(StringComparer.Ordinal){
                    {"acquire","1.0.0"},{"analyze","2.1.3"}};
                int revision=session.Revision;
                bool rejected=false;
                try{MOCProjectRelease.Prepare(session.GetSnapshot(),page.RootContent.Id,
                    resolver,new Dictionary<string,string>{{"acquire","1.0.0"}});}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "G07 silently accepted an unversioned capability");
                var release=MOCProjectRelease.Prepare(session.GetSnapshot(),
                    page.RootContent.Id,resolver,versions);
                Check(release.EngineeringRevision==revision&&release.ProjectId==session.Project.Id&&
                    release.Fingerprint.Length==64&&release.CapabilityVersions.Count==2,
                    "G07 release candidate did not bind actual Project and dependencies");
                session.SaveAs(path);
                using(var reopened=MEMEngineering.Open(path))
                {
                    var baseline=new MRKExecutionPlan("previous",new[]{
                        new MRKExecutionStep("old",new string[0],
                            (c,t)=>Task.FromResult((object)1))});
                    var slot=new MRKExecutionPlanSlot(baseline,1,true);
                    var policy=new ProjectReleaseTestPolicy();
                    var publisher=new MRKExecutionPlanPublisher(slot,baseline,1,policy);
                    rejected=false;
                    try{MOCProjectRelease.Stage(release,reopened.GetSnapshot(),
                        resolver,versions,publisher,"reader",2);}
                    catch(UnauthorizedAccessException){rejected=true;}
                    Check(rejected&&!publisher.PendingRevision.HasValue,
                        "G07 unauthorized principal staged plan");
                    var receipt=MOCProjectRelease.Stage(release,reopened.GetSnapshot(),
                        resolver,versions,publisher,"release-officer",2);
                    Check(receipt.Accepted&&publisher.PendingRevision==2,
                        "G07 validated project plan did not reach standard MRK Publisher");
                    Check(!publisher.TryActivate("release-officer")&&
                        publisher.ActiveRevision==1,
                        "G07 device safety point was bypassed");
                    policy.Safe=true;
                    Check(publisher.TryActivate("release-officer")&&
                        publisher.ActiveRevision==2,
                        "G07 approved software Plan failed to activate");
                    using(var lease=slot.Capture())
                        Check(lease.Plan.Id==release.Plan.Plan.Id,
                            "G07 activated Plan differs from locked source");
                    var altered=new Dictionary<string,string>(versions,StringComparer.Ordinal);
                    altered["analyze"]="2.1.4";
                    rejected=false;
                    try{MOCProjectRelease.Stage(release,reopened.GetSnapshot(),resolver,
                        altered,publisher,"release-officer",3);}
                    catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&!publisher.PendingRevision.HasValue&&
                        publisher.ActiveRevision==2,
                        "G07 capability version change contaminated active Plan");
                }
                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.Move(second,500,120));
                var baseline2=new MRKExecutionPlan("previous2",new[]{
                    new MRKExecutionStep("old",new string[0],
                        (c,t)=>Task.FromResult((object)1))});
                var slot2=new MRKExecutionPlanSlot(baseline2,1,true);
                var policy2=new ProjectReleaseTestPolicy{Safe=true};
                var publisher2=new MRKExecutionPlanPublisher(slot2,baseline2,1,policy2);
                rejected=false;
                try{MOCProjectRelease.Stage(release,session.GetSnapshot(),resolver,
                    versions,publisher2,"release-officer",2);}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&!publisher2.PendingRevision.HasValue,
                    "G07 stale engineering Revision could publish a previous Plan");
                Check(session.Undo().Success&&session.Revision>revision,
                    "G07 stale source Undo must still require fresh release fingerprint");
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))
                    System.IO.File.Delete(path+".cx-lock");
            }
        }

        // One engineering Content calls a typed sub-Content; all output commits use the original MRK/MDS.
        private static void V2NamedSubflowDataContracts()
        {
            var session=MEMEngineering.Create("V2 typed child data");
            var page=session.AddPage("Top");
            var child=session.AddContent(session.Project,"Double Content");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-public-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid leaf=Guid.Empty;
                MOCFlowBuilder.Edit(session,child,b=>{
                    leaf=b.Step("Double",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Doubled",MPCPortDirection.Output,"int",MPCPortKind.Data)
                    });
                    b.ExposeInput("Number",leaf,"N");
                    b.ExposeOutput("LeafValue",leaf,"Doubled");
                });
                var childRead=MOLWiringCodec.Get(session.GetSnapshot().Find(child.Id));
                Check(childRead.FlowInputs["Number"].BlockId==leaf&&
                      childRead.FlowOutputs["LeafValue"].PortMemberId=="Doubled",
                    "V2 child public ports were not persisted");
                Guid seed=Guid.Empty,call=Guid.Empty,plus=Guid.Empty,sourceLink=Guid.Empty;
                session.Edit("引用可复用子Content",e=>{
                    e.Reference(page.RootContent,child,"DoubleRef");return true;
                }).RequireValue();
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    seed=b.Step("Seed",new[]{
                        new MPCPortDescriptor("Value",MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    call=b.Step("moc.invoke:"+child.Id.ToString("N"),new[]{
                        new MPCPortDescriptor("Number",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("LeafValue",MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    plus=b.Step("Plus",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Answer",MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    b.Connect(seed,"Done",call,"Start");
                    b.Connect(call,"Done",plus,"Start");
                    sourceLink=b.Connect(seed,"Value",call,"Number",MPCPortKind.Data);
                    b.Connect(call,"LeafValue",plus,"N",MPCPortKind.Data);
                    b.ExposeOutput("Final",plus,"Answer");
                });
                var seedValue=4;
                var providers=new TestResolver()
                    .RegisterData("Seed",(c,t)=>{
                        c.Data.Return("Value",seedValue);return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Value",typeof(int)}},null)
                    .RegisterData("Double",(c,t)=>{
                        c.Data.Return("Doubled",c.Data.Get<int>("N")*2);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Doubled",typeof(int)}},
                      new[]{new MRKCapabilityInput("N",typeof(int))})
                    .RegisterData("Plus",(c,t)=>{
                        c.Data.Return("Answer",c.Data.Get<int>("N")+1);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Answer",typeof(int)}},
                      new[]{new MRKCapabilityInput("N",typeof(int))});
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var after=MOLWiringCodec.Get(reopened.GetSnapshot().Find(child.Id));
                    Check(after.FlowInputs.ContainsKey("Number")&&after.FlowOutputs.ContainsKey("LeafValue"),
                        "V2 child public contract did not survive actual disk roundtrip");
                    var compiled=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,providers);
                    Check(compiled.Succeeded,"V2 child data build: "+
                        string.Join(";",compiled.Diagnostics.Select(d=>d.Code+":"+d.Message)));
                    Check(compiled.Plan.Plan.DataExportTypes["Final"]==typeof(int),
                        "root DataExports not committed");
                    MOCTraceView trace;
                    var run=Execute(compiled.Plan,null,out trace);
                    Check(run.State==MRKFlowState.Completed&&
                        run.GetCommittedOutput<int>("Final")==9,
                        "typed root→child→root data not executed");
                    Check(trace.Events.Count>0&&compiled.Plan.SourceMap.Find(leaf).Any(),
                        "nested executed content source not traceable");
                    seedValue=10;
                    var again=Execute(compiled.Plan,null,out trace);
                    Check(again.State==MRKFlowState.Completed&&
                        again.GetCommittedOutput<int>("Final")==21,
                        "two Flow invocations shared prior committed output");
                    MOCFlowBuilder.Edit(reopened,reopened.Project.Pages[0].RootContent,b=>b.Disconnect(sourceLink));
                    var invalid=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,providers);
                    Check(!invalid.Succeeded,"unmapped required child input compiled successfully");
                }
                finally{reopened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        private static void V2ThreeLevelNamedData()
        {
            var session=MEMEngineering.Create("MOC three level typed data");
            var page=session.AddPage("Top");
            var mid=session.AddContent(session.Project,"Mid");
            var leaf=session.AddContent(session.Project,"Leaf");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-3level-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                session.Edit("配置三层资源引用",e=>{
                    e.Reference(page.RootContent,mid,"MidRef");
                    e.Reference(mid,leaf,"LeafRef");
                    return true;
                }).RequireValue();
                Guid leafStep=Guid.Empty,midCall=Guid.Empty,rootCall=Guid.Empty,midGate=Guid.Empty,midRejected=Guid.Empty;
                MOCFlowBuilder.Edit(session,leaf,b=>{
                    leafStep=b.Step("Double",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Doubled",MPCPortDirection.Output,"int",MPCPortKind.Data)
                    });
                    b.ExposeInput("Number",leafStep,"N");
                    b.ExposeOutput("LeafValue",leafStep,"Doubled");
                });
                MOCFlowBuilder.Edit(session,mid,b=>{
                    // R06：父Content从真实子Content公开契约推导端口，不再手抄Schema。
                    midGate=b.Step("MidGate");
                    midCall=b.InvokeContent(leaf);
                    midRejected=b.Step("MidWrongBranch");
                    b.ConnectConditional(midGate,"Done",midCall,"Start","bool:true");
                    b.ConnectConditional(midGate,"Done",midRejected,"Start","bool:false");
                    b.ExposeInput("MidSeed",midCall,"Number");
                    b.ExposeOutput("MidResult",midCall,"LeafValue");
                });
                Guid seed=Guid.Empty,plus=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    seed=b.Step("Seed",new[]{new MPCPortDescriptor("Value",
                        MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    rootCall=b.InvokeContent(mid);
                    plus=b.Step("Plus",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Answer",MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    b.Connect(seed,"Done",rootCall,"Start");
                    b.Connect(rootCall,"Done",plus,"Start");
                    b.Connect(seed,"Value",rootCall,"MidSeed",MPCPortKind.Data);
                    b.Connect(rootCall,"MidResult",plus,"N",MPCPortKind.Data);
                    b.ExposeOutput("Final",plus,"Answer");
                });
                bool failLeafForR07=false;
                bool midAccepted=true;
                int rejectedCalls=0;
                int executedLeaf=0;
                var resolver=new TestResolver()
                    .Register("MidGate",(c,t)=>Task.FromResult((object)midAccepted))
                    .Register("MidWrongBranch",(c,t)=>{
                        Interlocked.Increment(ref rejectedCalls);
                        return Task.FromResult((object)true);
                    })
                    .RegisterData("Seed",(c,t)=>{
                        c.Data.Return("Value",4);return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Value",typeof(int)}},null)
                    .RegisterData("Double",(c,t)=>{
                        Interlocked.Increment(ref executedLeaf);
                        if(failLeafForR07)
                            throw new InvalidOperationException("r07.leaf.failure");
                        c.Data.Return("Doubled",c.Data.Get<int>("N")*2);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Doubled",typeof(int)}},
                    new[]{new MRKCapabilityInput("N",typeof(int))})
                    .RegisterData("Plus",(c,t)=>{
                        c.Data.Return("Answer",c.Data.Get<int>("N")+1);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Answer",typeof(int)}},
                    new[]{new MRKCapabilityInput("N",typeof(int))});
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var node=reopened.GetSnapshot().Find(mid.Id);
                    Check(MOLWiringCodec.Get(node).FlowInputs.ContainsKey("MidSeed"),
                        "three-level public input lost after disk reload");
                    var compiled=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    Check(compiled.Succeeded,"three-level compilation failed: "+
                        string.Join(";",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
                    MOCTraceView trace;
                    var result=Execute(compiled.Plan,null,out trace);
                    Check(result.State==MRKFlowState.Completed&&
                        result.GetCommittedOutput<int>("Final")==9,
                        "three-level actual data return is wrong");
                    Check(rejectedCalls==0&&
                        compiled.Plan.SourceMap.Find(midRejected).Count>0,
                        "nested guarded branch was executed despite its false predicate");
                    // 未选分支可以Skipped，但公开结果源若Skipped就不能提交半成品。
                    midAccepted=false;
                    int leafBefore=executedLeaf;
                    var denied=Execute(compiled.Plan,null,out trace);
                    Check(denied.State==MRKFlowState.Failed&&
                        denied.CommittedOutputs.Count==0&&
                        executedLeaf==leafBefore&&rejectedCalls==1,
                        "nested skipped required export falsely committed or executed leaf");
                    midAccepted=true;
                    Check(result.CommittedOutputs.Count==1&&
                        !result.CommittedOutputs.ContainsKey("LeafValue")&&
                        !result.CommittedOutputs.ContainsKey("MidResult"),
                        "nested private outputs escaped the declared DataExports boundary");
                    Check(compiled.Plan.SourceMap.Find(leafStep).Any(x=>
                        x.CallPath.Count==2&&x.CallPath[0]==rootCall&&x.CallPath[1]==midCall),
                        "three-level leaf SourceMap did not preserve exact invocation chain");
                    Check(trace.Events.Count>0,"three-level trace was empty");
                    // R07：已编译的三层Content发生最深层异常，不能冒充成功。
                    failLeafForR07=true;
                    var failed=Execute(compiled.Plan,null,out trace);
                    Check(failed.State==MRKFlowState.Failed && failed.Error!=null &&
                        failed.Error.ToString().Contains("r07.leaf.failure") &&
                        failed.CommittedOutputs.Count==0,
                        "three-level Leaf failure swallowed or partial outputs leaked");
                    Check(trace.Events.Any(e=>e.Origin.BlockId==leafStep &&
                        e.Transition.State==MRKStepState.Failed &&
                        e.Origin.CallPath.Count==2 &&
                        e.Origin.CallPath[0]==rootCall &&
                        e.Origin.CallPath[1]==midCall),
                        "three-level failed Leaf lost two-tier invocation origin");
                }
                finally{reopened.Close(true);}
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        /// <summary>
        /// R06：同一Content两个实例共用一份公开Port契约；
        /// 保存/重开后仍经同一MOC/MRK执行，输出不串账、不泄漏私有端口。
        /// </summary>
        private static void R06NaturalRepeatedChildInvocation()
        {
            var session=MEMEngineering.Create("R06双实例类型化子流程");
            var page=session.AddPage("Main");
            var child=session.AddContent(session.Project,"ReusableDouble");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-r06-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid leaf=Guid.Empty;
                MOCFlowBuilder.Edit(session,child,childFlow=>{
                    leaf=childFlow.Step("Double",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",
                            MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Doubled",MPCPortDirection.Output,"int",
                            MPCPortKind.Data)
                    });
                    childFlow.ExposeInput("Number",leaf,"N");
                    childFlow.ExposeOutput("LeafValue",leaf,"Doubled");
                });
                session.Edit("引用可复用Content",e=>{
                    e.Reference(page.RootContent,child,"ReuseDouble");return true;
                }).RequireValue();
                Guid a=Guid.Empty,b=Guid.Empty,callA=Guid.Empty,callB=Guid.Empty,sum=Guid.Empty;
                Guid sourceA=Guid.Empty,sourceB=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,flow=>{
                    a=flow.Step("SeedA",new[]{
                        new MPCPortDescriptor("Value",MPCPortDirection.Output,"int",
                            MPCPortKind.Data)});
                    callA=flow.InvokeContent(child);
                    b=flow.Step("SeedB",new[]{
                        new MPCPortDescriptor("Value",MPCPortDirection.Output,"int",
                            MPCPortKind.Data)});
                    callB=flow.InvokeContent(child);
                    sum=flow.Step("Sum",new[]{
                        new MPCPortDescriptor("Left",MPCPortDirection.Input,"int",
                            MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Right",MPCPortDirection.Input,"int",
                            MPCPortKind.Data,required:true),
                        new MPCPortDescriptor("Total",MPCPortDirection.Output,"int",
                            MPCPortKind.Data)
                    });
                    flow.Connect(a,"Done",callA,"Start");
                    flow.Connect(callA,"Done",b,"Start");
                    flow.Connect(b,"Done",callB,"Start");
                    flow.Connect(callB,"Done",sum,"Start");
                    sourceA=flow.Connect(a,"Value",callA,"Number",MPCPortKind.Data);
                    sourceB=flow.Connect(b,"Value",callB,"Number",MPCPortKind.Data);
                    flow.Connect(callA,"LeafValue",sum,"Left",MPCPortKind.Data);
                    flow.Connect(callB,"LeafValue",sum,"Right",MPCPortKind.Data);
                    flow.ExposeOutput("Result",sum,"Total");
                });
                MOLWiringDefinition caller=MOLWiringCodec.Get(
                    session.GetSnapshot().Find(page.RootContent.Id));
                Check(caller.Ports[callA].Any(p=>p.MemberId=="Number"&&
                        p.TypeId=="int"&&p.Required)&&
                    caller.Ports[callB].Any(p=>p.MemberId=="LeafValue"&&
                        p.TypeId=="int"&&p.Direction==MPCPortDirection.Output),
                    "R06 invoked Content did not inherit real public typed ports");
                // 输入按FlowId隔离，而不是依赖测试闭包上的共享可变变量。
                var resolver=new TestResolver()
                    .RegisterData("SeedA",(ctx,ct)=>{
                        ctx.Data.Return("Value",(int)ctx.Inputs["R06Left"]);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Value",typeof(int)}},null)
                    .RegisterData("SeedB",(ctx,ct)=>{
                        ctx.Data.Return("Value",(int)ctx.Inputs["R06Right"]);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Value",typeof(int)}},null)
                    .RegisterData("Double",(ctx,ct)=>{
                        ctx.Data.Return("Doubled",ctx.Data.Get<int>("N")*2);
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Doubled",typeof(int)}},
                        new[]{new MRKCapabilityInput("N",typeof(int))})
                    .RegisterData("Sum",(ctx,ct)=>{
                        ctx.Data.Return("Total",ctx.Data.Get<int>("Left")+
                            ctx.Data.Get<int>("Right"));
                        return Task.FromResult((object)true);
                    },new Dictionary<string,Type>{{"Total",typeof(int)}},
                        new[]{new MRKCapabilityInput("Left",typeof(int)),
                            new MRKCapabilityInput("Right",typeof(int))});
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var rebuilt=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    Check(rebuilt.Succeeded,
                        "R06 content compilation failed: "+
                        string.Join(";",rebuilt.Diagnostics.Select(x=>x.Code+":"+x.Message)));
                    Check(rebuilt.Plan.SourceMap.Find(leaf).Count==2&&
                        rebuilt.Plan.SourceMap.Find(leaf)
                            .Select(x=>x.StepId).Distinct(StringComparer.Ordinal).Count()==2,
                        "R06 reused Content callers collapsed to a single invocation identity");
                    MOCTraceView trace;
                    MRKFlowResult first=Execute(rebuilt.Plan,new Dictionary<string,object>{
                        {"R06Left",3},{"R06Right",7}},out trace);
                    Check(first.State==MRKFlowState.Completed&&
                        first.GetCommittedOutput<int>("Result")==20&&
                        first.CommittedOutputs.Count==1&&
                        trace.Events.Any(x=>x.Origin.BlockId==leaf),
                        "R06 caller public outputs/trace or private-output isolation failed");
                    MRKFlowResult second=Execute(rebuilt.Plan,new Dictionary<string,object>{
                        {"R06Left",11},{"R06Right",2}},out trace);
                    Check(second.State==MRKFlowState.Completed&&
                        second.GetCommittedOutput<int>("Result")==26&&
                        second.CommittedOutputs.Count==1,
                        "R06 repeated Flow reused stale per-Content input or committed output");
                    // 同一个不可变Plan同时运行两个Flow，每个Flow的两个
                    // 子Content实例必须读取各自输入，不能共享MDS Frame/Commit。
                    var concurrentEngine=new MRKExecutionEngine(new FCRWorkerOptions(4,128));
                    try
                    {
                        MRKFlowHandle parallelA=concurrentEngine.StartFlow(rebuilt.Plan.Plan,
                            new Dictionary<string,object>{{"R06Left",4},{"R06Right",6}});
                        MRKFlowHandle parallelB=concurrentEngine.StartFlow(rebuilt.Plan.Plan,
                            new Dictionary<string,object>{{"R06Left",8},{"R06Right",9}});
                        MRKFlowResult[] parallel=Task.WhenAll(parallelA.Completion,
                            parallelB.Completion).GetAwaiter().GetResult();
                        Check(parallelA.FlowId!=parallelB.FlowId&&
                            parallel.All(x=>x.State==MRKFlowState.Completed)&&
                            parallel[0].GetCommittedOutput<int>("Result")==20&&
                            parallel[1].GetCommittedOutput<int>("Result")==34&&
                            parallel.All(x=>x.CommittedOutputs.Count==1),
                            "R06 concurrent Flow reused a caller's private Frame or output");
                        MOCTraceView traceA=rebuilt.Plan.ReadTrace(parallelA,0);
                        MOCTraceView traceB=rebuilt.Plan.ReadTrace(parallelB,0);
                        Check(traceA.Events.Any(x=>x.Origin.BlockId==leaf)&&
                            traceB.Events.Any(x=>x.Origin.BlockId==leaf),
                            "R06 concurrent invocations lost the original Content source map");
                    }
                    finally{concurrentEngine.StopAsync(true).GetAwaiter().GetResult();}
                    MOCFlowBuilder.Edit(reopened,reopened.Project.Pages[0].RootContent,
                        flow=>flow.Disconnect(sourceA));
                    var rejected=new MOCBuildService().Build(reopened.GetSnapshot(),
                        page.RootContent.Id,resolver);
                    Check(!rejected.Succeeded,
                        "R06 required Content input missing but compile succeeded");
                }
                finally{reopened.Close(true);}
                // 缺少可执行Logic时不允许凭Content名字或Guid制造不可运行调用。
                var draft=session.AddContent(session.Project,"Unconfigured");
                int revision=session.Revision;
                bool denied=false;
                try{
                    MOCFlowBuilder.Edit(session,page.RootContent,flow=>
                        flow.InvokeContent(draft));
                }
                catch(InvalidOperationException){denied=true;}
                Check(denied&&revision==session.Revision,
                    "R06 undeclared Content was accepted or wrote half-edit");
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        private static void G04ParameterSubflowIsolation()
        {
            var session=MEMEngineering.Create("G04-F类型化参数子流程");
            var page=session.AddPage("参数化子流程入口");
            try
            {
                Guid start=Guid.Empty,inspect=Guid.Empty,finish=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    start=b.Step("acquire");
                    inspect=b.Step("inspect");
                    finish=b.Step("finish");
                    b.Connect(start,"Done",inspect,"Start");
                    b.Connect(inspect,"Done",finish,"Start");
                    b.AddPort(inspect,new MPCPortDescriptor("Threshold",
                        MPCPortDirection.Input,"double",MPCPortKind.Data));
                    b.SetParameter(inspect,"Threshold","2.25");
                    b.Move(start,20,40);b.Move(inspect,340,40);b.Move(finish,660,40);
                });
                var child=MOCFlowBuilder.ExtractLinearContent(session,page.RootContent,
                    new[]{inspect,finish},"检测阈值子流程");
                var stored=MOLWiringCodec.Get(session.GetSnapshot().Find(child.Id));
                Check(stored.Parameters[inspect]["Threshold"]=="2.25",
                    "G04-F extract lost typed Data parameters");
                var first=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Blocks.Single(b=>b.CapabilityId=="moc.invoke:"+child.Id.ToString("N"));
                Guid second=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    second=b.Step("moc.invoke:"+child.Id.ToString("N"));
                    b.Connect(first.Id,"Done",second,"Start");
                });
                int count=0;
                var resolver=new TestResolver()
                    .Register("acquire",(ctx,t)=>Task.FromResult((object)1))
                    .Register("inspect",(ctx,t)=>{
                        object value;
                        if(!ctx.BoundInputs.TryGetValue("Threshold",out value)||
                            !(value is double)||Math.Abs((double)value-2.25)>0.00001)
                            throw new InvalidOperationException("G04-F instance parameter mismatch");
                        Interlocked.Increment(ref count);
                        return Task.FromResult((object)value);
                    })
                    .Register("finish",(ctx,t)=>Task.FromResult((object)true));
                var result=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(result.Succeeded,"G04-F parameter subflow compile: "+
                    string.Join(";",result.Diagnostics.Select(d=>d.Code+":"+d.Message)));
                Check(result.Plan.InputDefaults.Count==2&&
                    result.Plan.InputDefaults.Keys.Distinct().Count()==2&&
                    result.Plan.InputDefaults.Values.All(v=>v is double &&
                        Math.Abs((double)v-2.25)<0.00001),
                    "G04-F two Content calls share an incorrect input-default key");
                MOCTraceView trace;
                var flow=Execute(result.Plan,
                    result.Plan.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value),out trace);
                Check(flow.State==MRKFlowState.Completed&&count==2&&
                    result.Plan.SourceMap.Find(inspect).Count==2,
                    "G04-F parameterized child did not execute twice with source identity");
                int revision=session.Revision;
                bool rejected=false;
                try
                {
                    MOCFlowBuilder.Edit(session,page.RootContent,b=>
                        b.AddPort(first.Id,new MPCPortDescriptor("Unknown",
                            MPCPortDirection.Input,"double",MPCPortKind.Data)));
                    var modified=new MOCBuildService().Build(
                        session.GetSnapshot(),page.RootContent.Id,resolver);
                    Check(modified.Succeeded,"G04-F added unused port changed executable plan");
                }
                catch(Exception){rejected=true;}
                Check(!rejected,"G04-F unrelated unbound port disrupted child invocation");
                Check(session.Revision==revision+1,"G04-F unrelated port edit did not commit once");
                Check(session.Undo().Success,"G04-F Undo on caller port failed");
            }
            finally{session.Close(true);}
        }

        private static void V2ExtractLinearContent()
        {
            var session=MEMEngineering.Create("G04真实可复用子流程");
            var page=session.AddPage("检测入口");
            Guid a=Guid.Empty,b=Guid.Empty,c=Guid.Empty,d=Guid.Empty;
            try
            {
                MOCFlowBuilder.Edit(session,page.RootContent,flow=>{
                    a=flow.Step("acquire");b=flow.Step("inspect");
                    c=flow.Step("classify");d=flow.Step("save");
                    flow.Connect(a,"Done",b,"Start");
                    flow.Connect(b,"Done",c,"Start");
                    flow.Connect(c,"Done",d,"Start");
                    flow.Move(a,30,50);flow.Move(b,320,50);
                    flow.Move(c,610,50);flow.Move(d,900,50);
                });
                int revision=session.Revision;
                var child=MOCFlowBuilder.ExtractLinearContent(session,page.RootContent,
                    new[]{b,c},"AI子流程");
                Check(session.Revision==revision+1,"subflow extract was not one MEM transaction");
                var root=session.GetSnapshot().Find(page.RootContent.Id);
                var extracted=session.GetSnapshot().Find(child.Id);
                var parentWiring=MOLWiringCodec.Get(root);
                var childWiring=MOLWiringCodec.Get(extracted);
                Check(parentWiring.Blocks.Count==3&&parentWiring.Links.Count==2&&
                    childWiring.Blocks.Count==2&&childWiring.Links.Count==1&&
                    root.References.Any(r=>r.TargetId==child.Id)&&
                    extracted.Ports.Count==2,
                    "extract did not make a real MEMContent with Control public ports and reference");
                var call=parentWiring.Blocks.Single(x=>
                    x.CapabilityId=="moc.invoke:"+child.Id.ToString("N"));
                Check(parentWiring.Links.Any(l=>l.Source.BlockId==a&&l.Target.BlockId==call.Id)&&
                    parentWiring.Links.Any(l=>l.Source.BlockId==call.Id&&l.Target.BlockId==d),
                    "parent Control boundary links were not reconnected");
                int count=0;
                var resolver=new TestResolver()
                    .Register("acquire",(ctx,t)=>{Interlocked.Increment(ref count);return Task.FromResult((object)1);})
                    .Register("inspect",(ctx,t)=>{Interlocked.Increment(ref count);return Task.FromResult((object)2);})
                    .Register("classify",(ctx,t)=>{Interlocked.Increment(ref count);return Task.FromResult((object)3);})
                    .Register("save",(ctx,t)=>{Interlocked.Increment(ref count);return Task.FromResult((object)4);});
                var build=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(build.Succeeded,"real subflow did not compile: "+
                    string.Join(",",build.Diagnostics.Select(x=>x.Code+"="+x.Message)));
                MOCTraceView trace;
                var result=Execute(build.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&count==4&&
                    build.Plan.SourceMap.Find(b).Any(origin=>
                        origin.CallPath.Count==1&&origin.CallPath[0]==call.Id&&
                        origin.ContentAddress.ObjectId==child.Id),
                    "MRK did not execute inlined Content with correct SourceMap CallPath");
                string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "moc-extract-"+Guid.NewGuid().ToString("N")+".cxproj");
                try
                {
                    session.SaveAs(path);
                    var reopened=MEMEngineering.Open(path);
                    try
                    {
                        var after=MOLWiringCodec.Get(reopened.GetSnapshot().Find(page.RootContent.Id));
                        Check(after.Blocks.Count==3&&
                            reopened.GetSnapshot().Find(child.Id)!=null,
                            "real file lost extracted Content and call");
                    }
                    finally{reopened.Close(true);}
                }
                finally
                {
                    if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                    if(System.IO.File.Exists(path+".cx-lock"))
                        System.IO.File.Delete(path+".cx-lock");
                }
                Check(session.Undo().Success&&
                    session.GetSnapshot().Find(child.Id)==null&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                        .Blocks.Count==4,
                    "extract undo did not restore original V2 flow atomically");
                Check(session.Redo().Success&&
                    session.GetSnapshot().Find(child.Id)!=null,
                    "extract redo did not restore referenced Content atomically");
            }
            finally{session.Close(true);}
        }

        /// <summary>G05d：单Logic内StateMachine按正交Region处理同一事件，并以MRK真实执行。</summary>
        private static void G05dHierarchicalParallelMachine()
        {
            var machine=new MOLStateMachineDefinition(new[]{
                new MOLStateDescriptor("CameraRoot","Camera",initial:true),
                new MOLStateDescriptor("CameraIdle","Camera","CameraRoot",true),
                new MOLStateDescriptor("CameraBusy","Camera","CameraRoot"),
                new MOLStateDescriptor("MotionRoot","Motion",initial:true),
                new MOLStateDescriptor("MotionParked","Motion","MotionRoot",true),
                new MOLStateDescriptor("MotionMoving","Motion","MotionRoot")
            },new[]{
                new MOLStateTransition("CameraIdle","start","CameraBusy"),
                new MOLStateTransition("CameraBusy","stop","CameraIdle"),
                new MOLStateTransition("CameraRoot","reset","CameraIdle"),
                new MOLStateTransition("MotionParked","start","MotionMoving"),
                new MOLStateTransition("MotionMoving","stop","MotionParked"),
                new MOLStateTransition("MotionRoot","reset","MotionParked")
            });
            var active=machine.Run(new[]{"start"});
            Check(active["Camera"]=="CameraBusy"&&active["Motion"]=="MotionMoving",
                "G05d orthogonal regions did not consume shared event");
            active=machine.Run(new[]{"start","reset"});
            Check(active["Camera"]=="CameraIdle"&&active["Motion"]=="MotionParked",
                "G05d hierarchical parent transition did not override active child");
            active=machine.Run(new[]{"unsupported"});
            Check(active["Camera"]=="CameraIdle"&&active["Motion"]=="MotionParked",
                "G05d unknown event changed current state");
            bool rejected=false;
            try{new MOLStateMachineDefinition(machine.States,
                new[]{new MOLStateTransition("CameraIdle","start","MotionMoving")});}
            catch(InvalidOperationException){rejected=true;}
            Check(rejected,"G05d cross-region transition was not rejected");
            var session=MEMEngineering.Create("G05d状态机工程");
            var page=session.AddPage("状态监控");
            try
            {
                Guid id=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>id=b.AddStateMachine(machine));
                var read=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(read.Blocks.Count==1&&read.StateMachines.ContainsKey(id),
                    "G05d state machine did not persist as same Content Logic");
                var code=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(read));
                Check(code.StateMachines[id].States.Count==6&&
                    code.StateMachines[id].Transitions.Count==6,
                    "G05d state machine V2 codec lost hierarchy or transitions");
                var fragment=MOCWiringFragment.Capture(read,new[]{id});
                Check(MOCWiringFragment.FromPayload(fragment.ToPayload())
                    .StateMachines[id].Transitions.Count==6,
                    "G05d state machine clipboard lost definition");
                var compiled=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,new TestResolver());
                Check(compiled.Succeeded,"G05d state machine failed to compile: "+
                    string.Join(",",compiled.Diagnostics.Select(x=>x.Code+":"+x.Message)));
                var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,8));
                try
                {
                    var events=new Dictionary<string,object>{
                        {"moc.state.events."+id.ToString("N"),new[]{"start","reset"}}};
                    var handle=engine.StartFlow(compiled.Plan.Plan,events);
                    var result=handle.Completion.GetAwaiter().GetResult();
                    object output;
                    var outputState=handle.Context.TryGetResult("w"+id.ToString("N"),out output)?
                        output as IReadOnlyDictionary<string,string>:null;
                    Check(result.State==MRKFlowState.Completed&&outputState!=null&&
                        outputState["Camera"]=="CameraIdle"&&
                        outputState["Motion"]=="MotionParked"&&
                        compiled.Plan.SourceMap.Find(id).Single().Role=="StateMachine",
                        "G05d MRK execution did not produce deterministic StateMachine result");
                }
                finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                int before=session.Revision;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.SetStateMachine(id,new MOLStateMachineDefinition(machine.States,
                        new[]{new MOLStateTransition("CameraIdle","start","CameraBusy")})));
                Check(session.Revision==before+1&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                        .StateMachines[id].Transitions.Count==1,
                    "G05d state machine edit not atomic");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                        .StateMachines[id].Transitions.Count==6,
                    "G05d state machine undo did not recover event rules");
            }
            finally{session.Close(true);}
        }

        /// <summary>P0-3：状态机Guard共享MOL标量语义，序列化往返与MRK执行结果正确。</summary>
        private static void P03StateMachineGuards()
        {
            bool invalid=false;
            try
            {
                new MOLStateTransition("Idle","start","Busy",
                    new MOLExpression(MOLExpressionSource.StepResult,"foreign"));
            }
            catch(ArgumentException ex){invalid=ex.Message.Contains("moc.state.guard_source");}
            Check(invalid,"P0-3 external StepResult guard escaped state-machine declaration");

            var machine=new MOLStateMachineDefinition(new[]{
                new MOLStateDescriptor("Idle","Unit",initial:true),
                new MOLStateDescriptor("Busy","Unit"),
                new MOLStateDescriptor("Finished","Unit")
            },new[]{
                new MOLStateTransition("Idle","start","Busy",
                    MOLExpression.InputTrue("permit")),
                new MOLStateTransition("Busy","finish","Finished")
            });
            var session=MEMEngineering.Create("P0-3 state guards");
            var page=session.AddPage("Unit");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-state-guard-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid block=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>block=b.AddStateMachine(machine));
                session.SaveAs(path);
                using(var opened=MEMEngineering.Open(path))
                {
                    var saved=MOLWiringCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    var restored=saved.StateMachines[block];
                    Check(restored.Transitions[0].Guard!=null&&
                        restored.Transitions[0].Guard.Name=="permit"&&
                        restored.Transitions[0].Guard.Source==MOLExpressionSource.FlowInput,
                        "P0-3 Guard lost its schema2 payload/identity in actual project file");
                    Check(restored.Run(new[]{"start"},new Dictionary<string,object>{
                        {"permit",false}})["Unit"]=="Idle"&&
                        restored.Run(new[]{"start"},new Dictionary<string,object>{
                        {"permit",true}})["Unit"]=="Busy",
                        "P0-3 Guard did not reject/permit deterministic transition");

                    var build=new MOCBuildService().Build(opened.GetSnapshot(),
                        page.RootContent.Id,new TestResolver());
                    Check(build.Succeeded,"P0-3 guarded StateMachine compile failed");
                    Func<IDictionary<string,object>,string> execute=inputs=>{
                        var runtime=new MRKExecutionEngine(new FCRWorkerOptions(1,8));
                        try
                        {
                            var handle=runtime.StartFlow(build.Plan.Plan,inputs);
                            var done=handle.Completion.GetAwaiter().GetResult();
                            if(done.State!=MRKFlowState.Completed)
                                return "FAILED";
                            object value;
                            if(!handle.Context.TryGetResult("w"+block.ToString("N"),out value))
                                return "MISSING";
                            var result=value as IReadOnlyDictionary<string,string>;
                            return result==null?"INVALID":result["Unit"];
                        }
                        finally{runtime.StopAsync(true).GetAwaiter().GetResult();}
                    };
                    string eventKey="moc.state.events."+block.ToString("N");
                    string checkpointKey="moc.state.checkpoint."+block.ToString("N");
                    var pureCheckpoint=restored.Run(new[]{"start"},
                        new Dictionary<string,object>{{"permit",true}});
                    Check(restored.Run(new[]{"finish"},null,pureCheckpoint)["Unit"]=="Finished"&&
                        restored.Run(new[]{"finish"})["Unit"]=="Idle",
                        "P0-3 pure transition did not resume from a validated snapshot");
                    bool invalidCheckpoint=false;
                    try{restored.Run(new[]{"finish"},null,
                        new Dictionary<string,string>{{"Unit","Ghost"}});}
                    catch(InvalidOperationException ex)
                    {invalidCheckpoint=ex.Message.Contains("moc.state.invalid_checkpoint");}
                    Check(invalidCheckpoint,"P0-3 accepted unknown checkpoint state");
                    invalidCheckpoint=false;
                    try{restored.Run(new[]{"finish"},null,
                        new Dictionary<string,string>());}
                    catch(InvalidOperationException ex)
                    {invalidCheckpoint=ex.Message.Contains("moc.state.invalid_checkpoint");}
                    Check(invalidCheckpoint,"P0-3 accepted missing checkpoint region");
                    Check(execute(new Dictionary<string,object>{
                        {eventKey,new[]{"start"}},{"permit",false}})=="Idle"&&
                        execute(new Dictionary<string,object>{
                        {eventKey,new[]{"start"}},{"permit",true}})=="Busy"&&
                        execute(new Dictionary<string,object>{
                        {eventKey,new[]{"start"}}})=="FAILED",
                        "P0-3 MRK did not honor Guard or reject missing Guard input");
                    string firstFlow=execute(new Dictionary<string,object>{
                        {eventKey,new[]{"start"}},{"permit",true}});
                    var fromFirstFlow=new Dictionary<string,string>{{"Unit",firstFlow}};
                    Check(firstFlow=="Busy"&&
                        execute(new Dictionary<string,object>{
                            {eventKey,new[]{"finish"}},
                            {checkpointKey,fromFirstFlow}})=="Finished"&&
                        execute(new Dictionary<string,object>{
                            {eventKey,new[]{"finish"}}})=="Idle"&&
                        execute(new Dictionary<string,object>{
                            {eventKey,new[]{"finish"}},
                            {checkpointKey,new Dictionary<string,string>{{"Unit","Ghost"}}}})=="FAILED"&&
                        execute(new Dictionary<string,object>{
                            {eventKey,new[]{"finish"}},
                            {checkpointKey,"invalid"}})=="FAILED",
                        "P0-3 MRK checkpoint resume/rejection or fresh Flow isolation failed");
                }
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }


        /// <summary>P0-3：多Guard优先级、正交Region、快照、保存重开与真实MRK反证。</summary>
        private static void P03GuardPriorityAndOrthogonalRegions()
        {
            var states=new[]{
                new MOLStateDescriptor("CameraRoot","Camera",initial:true),
                new MOLStateDescriptor("CameraIdle","Camera","CameraRoot",true),
                new MOLStateDescriptor("CameraAccepted","Camera","CameraRoot"),
                new MOLStateDescriptor("CameraDeferred","Camera","CameraRoot"),
                new MOLStateDescriptor("CameraDenied","Camera","CameraRoot"),
                new MOLStateDescriptor("MotionIdle","Motion",initial:true),
                new MOLStateDescriptor("MotionBusy","Motion")
            };
            var transitions=new[]{
                new MOLStateTransition("CameraIdle","start","CameraAccepted",
                    MOLExpression.InputTrue("approve"),30),
                new MOLStateTransition("CameraIdle","start","CameraDeferred",
                    MOLExpression.InputTrue("defer"),20),
                new MOLStateTransition("CameraIdle","start","CameraDenied"),
                new MOLStateTransition("MotionIdle","start","MotionBusy",
                    MOLExpression.InputTrue("motion"),3),
                new MOLStateTransition("CameraRoot","reset","CameraIdle"),
                new MOLStateTransition("MotionBusy","reset","MotionIdle")
            };
            bool invalid=false;
            try{new MOLStateMachineDefinition(states,new[]{
                transitions[0],new MOLStateTransition("CameraIdle","start",
                    "CameraDeferred",MOLExpression.InputTrue("defer"),30)});}
            catch(InvalidOperationException ex)
            {invalid=ex.Message.Contains("moc.state.invalid_transition");}
            Check(invalid,"P0-3 same-priority conflict not rejected");
            invalid=false;
            try{new MOLStateTransition("CameraIdle","start","CameraAccepted",
                MOLExpression.Always(true),1001);}
            catch(ArgumentOutOfRangeException){invalid=true;}
            Check(invalid,"P0-3 invalid priority budget not rejected");

            var machine=new MOLStateMachineDefinition(states,transitions);
            var both=new Dictionary<string,object>{
                {"approve",true},{"defer",true},{"motion",true}};
            var deferred=new Dictionary<string,object>{
                {"approve",false},{"defer",true},{"motion",false}};
            var denied=new Dictionary<string,object>{
                {"approve",false},{"defer",false},{"motion",false}};
            var accepted=machine.Run(new[]{"start"},both);
            Check(accepted["Camera"]=="CameraAccepted"&&
                accepted["Motion"]=="MotionBusy"&&
                machine.Run(new[]{"start"},deferred)["Camera"]=="CameraDeferred"&&
                machine.Run(new[]{"start"},denied)["Camera"]=="CameraDenied",
                "P0-3 guarded priority, fallback or orthogonal region incorrect");
            var resumed=machine.Run(new[]{"reset"},both,accepted);
            Check(resumed["Camera"]=="CameraIdle"&&resumed["Motion"]=="MotionIdle",
                "P0-3 checkpoint or hierarchical transition incorrect");
            invalid=false;
            try{machine.Run(new[]{"start"},new Dictionary<string,object>{
                {"defer",true},{"motion",true}});}
            catch(InvalidOperationException ex)
            {invalid=ex.Message.Contains("moc.state.guard_input_missing:approve");}
            Check(invalid,"P0-3 missing priority input silently fell back");
            var session=MEMEngineering.Create("P0-3 Guard优先级");
            var page=session.AddPage("检测工位");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "moc-state-priority-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid id=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>id=b.AddStateMachine(machine));
                session.SaveAs(path);
                using(var opened=MEMEngineering.Open(path))
                {
                    var saved=MOLWiringCodec.Get(opened.GetSnapshot().Find(page.RootContent.Id));
                    var restored=saved.StateMachines[id];
                    Check(restored.Transitions[0].Priority==30&&
                        restored.Transitions[1].Priority==20&&
                        restored.Transitions[2].Priority==0&&
                        restored.Run(new[]{"start"},both)["Camera"]=="CameraAccepted",
                        "P0-3 priority lost after Save/Open");
                    byte[] serialized=MOLWiringCodec.Serialize(saved);
                    string xml=System.Text.Encoding.UTF8.GetString(serialized);
                    Check(xml.Contains("priority=\"30\""),"P0-3 priority schema missing");
                    bool schemaInvalid=false;
                    try{MOLWiringCodec.Deserialize(System.Text.Encoding.UTF8.GetBytes(
                        xml.Replace("priority=\"30\"","priority=\"1001\"")));}
                    catch(System.IO.InvalidDataException ex)
                    {schemaInvalid=ex.Message.Contains("wiring.state.priority");}
                    Check(schemaInvalid,"P0-3 malformed priority schema accepted");
                    var build=new MOCBuildService().Build(opened.GetSnapshot(),
                        page.RootContent.Id,new TestResolver());
                    Check(build.Succeeded,"P0-3 guarded build failed: "+
                        string.Join(",",build.Diagnostics.Select(d=>d.Code)));
                    string eventKey="moc.state.events."+id.ToString("N");
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,8));
                    try
                    {
                        var input=new Dictionary<string,object>(both){
                            {eventKey,new[]{"start"}}};
                        var good=engine.StartFlow(build.Plan.Plan,input);
                        var result=good.Completion.GetAwaiter().GetResult();
                        object output;
                        var active=good.Context.TryGetResult("w"+id.ToString("N"),
                            out output)?output as IReadOnlyDictionary<string,string>:null;
                        Check(result.State==MRKFlowState.Completed&&active!=null&&
                            active["Camera"]=="CameraAccepted"&&
                            active["Motion"]=="MotionBusy",
                            "P0-3 MOC/MRK priority or Region result incorrect");
                        var missing=new Dictionary<string,object>{
                            {eventKey,new[]{"start"}},{"defer",true},{"motion",true}};
                        var bad=engine.StartFlow(build.Plan.Plan,missing);
                        Check(bad.Completion.GetAwaiter().GetResult().State==
                            MRKFlowState.Failed,
                            "P0-3 MRK missing Guard input did not fail");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                }
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        /// <summary>G05b：真实背压、双向隔离、取消及MOC→MRK Stream Port通信。</summary>
        private static void G05bStreamBackpressureAndFlow()
        {
            using(var stream=new MDSBoundedByteStream(1,128))
            {
                byte[] first={1,2,3};
                stream.WriteAsync(first).GetAwaiter().GetResult();
                first[0]=99;
                var pending=stream.WriteAsync(new byte[]{4});
                Check(!pending.IsCompleted,"G05b full-capacity writer did not wait for consumer");
                var consumed=stream.ReadAsync().GetAwaiter().GetResult();
                Check(consumed.Length==3&&consumed[0]==1,
                    "G05b sender mutation changed enqueued bytes");
                consumed[0]=55;
                pending.GetAwaiter().GetResult();
                stream.Complete();
                var last=stream.ReadAsync().GetAwaiter().GetResult();
                Check(last.Length==1&&last[0]==4&&
                    stream.ReadAsync().GetAwaiter().GetResult()==null,
                    "G05b complete did not drain then return end-of-stream");
            }
            using(var stream=new MDSBoundedByteStream(1,4))
            using(var cancellation=new CancellationTokenSource())
            {
                stream.WriteAsync(new byte[]{1}).GetAwaiter().GetResult();
                var blocked=stream.WriteAsync(new byte[]{2},cancellation.Token);
                Check(!blocked.IsCompleted,"G05b cancellation prerequisite not blocked");
                cancellation.Cancel();
                bool cancelled=false;
                try{blocked.GetAwaiter().GetResult();}
                catch(OperationCanceledException){cancelled=true;}
                Check(cancelled,"G05b pending writer ignored cancellation");
                bool oversize=false;
                try{stream.WriteAsync(new byte[5]).GetAwaiter().GetResult();}
                catch(ArgumentException){oversize=true;}
                Check(oversize,"G05b chunk budget not enforced");
            }
            using(var stream=new MDSBoundedByteStream(1,5))
            {
                stream.Complete(new InvalidOperationException("producer failed"));
                bool failed=false;
                try{stream.ReadAsync().GetAwaiter().GetResult();}
                catch(InvalidOperationException ex){failed=ex.InnerException!=null;}
                Check(failed,"G05b producer fault did not propagate to consumer");
            }
            var session=MEMEngineering.Create("G05b 数据流");
            var page=session.AddPage("工业图像流");
            try
            {
                Guid source=Guid.Empty,target=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    source=b.Step("stream.producer");target=b.Step("stream.consumer");
                    b.AddPort(source,new MPCPortDescriptor("Chunks",
                        MPCPortDirection.Output,"stream.bytes",MPCPortKind.Stream));
                    b.AddPort(target,new MPCPortDescriptor("Chunks",
                        MPCPortDirection.Input,"stream.bytes",MPCPortKind.Stream,
                        required:true));
                    b.Connect(source,"Done",target,"Start");
                    b.Connect(source,"Chunks",target,"Chunks",MPCPortKind.Stream);
                });
                var wiring=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(wiring.Links.Any(l=>l.Kind==MPCPortKind.Stream),
                    "G05b Stream link not stored in authoritative V2");
                int aggregate=0;
                var resolver=new TestResolver()
                    .Register("stream.producer",async (ctx,cancel)=>{
                        var buffer=new MDSBoundedByteStream(1,1024);
                        Task ignored=Task.Run(async()=>{
                            try
                            {
                                await buffer.WriteAsync(new byte[]{1,2},cancel).ConfigureAwait(false);
                                await buffer.WriteAsync(new byte[]{3,4},cancel).ConfigureAwait(false);
                                buffer.Complete();
                            }
                            catch(Exception ex){buffer.Complete(ex);}
                        },cancel);
                        await Task.Yield();
                        return (object)buffer;
                    })
                    .Register("stream.consumer",async (ctx,cancel)=>{
                        var buffer=(MDSBoundedByteStream)ctx.BoundInputs["Chunks"];
                        try
                        {
                            byte[] item;
                            while((item=await buffer.ReadAsync(cancel).ConfigureAwait(false))!=null)
                                foreach(byte value in item)Interlocked.Add(ref aggregate,value);
                        }
                        finally{buffer.Dispose();}
                        return (object)aggregate;
                    });
                var built=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(built.Succeeded,
                    "G05b Stream Port flow failed to compile: "+
                    string.Join(",",built.Diagnostics.Select(x=>x.Code+"="+x.Message)));
                MOCTraceView trace;
                var flow=Execute(built.Plan,null,out trace);
                Check(flow.State==MRKFlowState.Completed&&aggregate==10&&trace.Events.Count>0,
                    "G05b streaming producer/consumer failed real MRK handoff");
                int revision=session.Revision;
                bool rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.ReplacePort(target,new MPCPortDescriptor("Chunks",
                        MPCPortDirection.Input,"int",MPCPortKind.Stream)));}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "G05b incompatible Stream type did not roll back");
            }
            finally{session.Close(true);}
        }

        /// <summary>G05c策略必须实测MRK尝试次数、未知结果拒绝与原子工程回滚。</summary>
        private static void G05cWorkflowRetryPolicy()
        {
            var session=MEMEngineering.Create("G05c 工作流策略");
            var page=session.AddPage("幂等执行");
            try
            {
                Guid block=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    block=b.Step("idempotent.task");
                    b.SetWorkflowPolicy(block,new MOLStepPolicy(3,1,0,true));
                });
                var definition=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(definition.Policies[block].MaxAttempts==3&&
                    definition.Policies[block].Idempotent,
                    "G05c policy not persisted in V2 Logic");
                var decoded=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(definition));
                Check(decoded.Policies[block].RetryDelayMs==1,
                    "G05c policy codec lost retry delay");
                var snapshot=MOCWiringFragment.Capture(definition,new[]{block});
                var payload=MOCWiringFragment.FromPayload(snapshot.ToPayload());
                Check(payload.Policies[block].MaxAttempts==3,
                    "G05c clipboard lost workflow policy");
                int attempts=0;
                var resolver=new TestResolver().Register("idempotent.task",(ctx,token)=>{
                    int n=Interlocked.Increment(ref attempts);
                    if(n<2)throw new InvalidOperationException("transient");
                    return Task.FromResult((object)n);
                });
                var checkedBuild=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(checkedBuild.Succeeded,
                    "G05c workflow execution policy build failed");
                MOCTraceView trace;
                var result=Execute(checkedBuild.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&attempts==2,
                    "G05c MRK did not apply bounded idempotent retry");

                int before=session.Revision;
                bool refused=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.SetWorkflowPolicy(block,new MOLStepPolicy(2,0,0,false)));}
                catch(ArgumentException){refused=true;}
                Check(refused&&session.Revision==before,
                    "G05c non-idempotent retry wrongly committed");
                var failing=new TestResolver().Register("idempotent.task",(ctx,token)=>{
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("permanent failure");
                });
                MOCBuildResult negative=new MOCBuildService().Build(
                    session.GetSnapshot(),page.RootContent.Id,failing);
                Check(negative.Succeeded,"G05c failure case failed to compile");
                attempts=0;
                result=Execute(negative.Plan,null,out trace);
                Check(result.State==MRKFlowState.Failed&&attempts==3,
                    "G05c bounded retries did not stop at attempt budget");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.ClearWorkflowPolicy(block));
                Check(!MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Policies.ContainsKey(block),"G05c policy removal not persisted");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Policies.ContainsKey(block),"G05c policy Undo not restored");
            }
            finally{session.Close(true);}
        }

        private static void R08_EngineeringStageGroupIdentitySurvivesBuildAndReload()
        {
            var session=MEMEngineering.Create("R08执行组保存编译");
            var page=session.AddPage("Stage");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "cxvisual-r08-stage-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                Guid block=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    block=b.Step("stage.work");
                    b.SetExecutionGroup(block,"vision-stage");
                });
                var persisted=MOLWiringCodec.Get(
                    session.GetSnapshot().Find(page.RootContent.Id));
                Check(persisted.Policies[block].ExecutionGroup=="vision-stage" &&
                    MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(persisted))
                        .Policies[block].ExecutionGroup=="vision-stage",
                    "R08 Stage组身份没有保存在原V2 Logic Codec");
                session.SaveAs(path);
                var reopened=MEMEngineering.Open(path);
                try
                {
                    var saved=MOLWiringCodec.Get(
                        reopened.GetSnapshot().Find(page.RootContent.Id));
                    Check(saved.Policies[block].ExecutionGroup=="vision-stage",
                        "R08工程保存/重开丢失Stage组ID");
                    var resolver=new TestResolver().Register("stage.work",
                        (ctx,ct)=>Task.FromResult((object)7));
                    var build=new MOCBuildService().Build(
                        reopened.GetSnapshot(),page.RootContent.Id,resolver);
                    Check(build.Succeeded&&build.Plan.Plan.Steps.Count==1&&
                        build.Plan.Plan.Steps[0].ExecutionGroup=="vision-stage",
                        "R08 MOC编译未将原工程Stage组ID传入MRK Plan");
                    // 工程保存仅携带组身份；必须与Host的真实MRK配额配置核对，
                    // 不能因Studio页面填了名字就在生产环境发出未知组指令。
                    bool missingGroup=false;
                    try { build.Plan.RequireExecutionGroups(
                        new MRKExecutionGroupDefinition[0]); }
                    catch (InvalidOperationException error)
                    {
                        missingGroup=error.Message.Contains(
                            "moc.execution_group_unregistered:");
                    }
                    Check(missingGroup,"R08未知Host执行组没有在工程编译后发布前拒绝");
                    bool duplicateGroup=false;
                    try
                    {
                        var group=new MRKExecutionGroupDefinition("vision-stage",1,1,
                            MRKExecutionGroupScope.Stage);
                        build.Plan.RequireExecutionGroups(new[]{group,group});
                    }
                    catch(ArgumentException){duplicateGroup=true;}
                    Check(duplicateGroup,"R08 Host重复组定义未明确拒绝");
                    var approved=new MRKExecutionGroupDefinition("vision-stage",1,1,
                        MRKExecutionGroupScope.Stage);
                    var deniedBuild=new MOCBuildService().Build(
                        reopened.GetSnapshot(),page.RootContent.Id,resolver,
                        new MRKExecutionGroupDefinition[0]);
                    Check(!deniedBuild.Succeeded && deniedBuild.Plan==null &&
                        deniedBuild.Diagnostics.Any(x=>
                            x.Code=="MOC_EXECUTION_GROUP_INVALID"),
                        "R08未授权运行组不应生成可发布MOC Plan");
                    var authorizedBuild=new MOCBuildService().Build(
                        reopened.GetSnapshot(),page.RootContent.Id,resolver,
                        new[]{approved});
                    Check(authorizedBuild.Succeeded&&authorizedBuild.Plan!=null &&
                        authorizedBuild.Plan.Plan.Steps[0].ExecutionGroup=="vision-stage",
                        "R08已授权真实Host组配置未完成构建");
                    Check(object.ReferenceEquals(build.Plan,
                        build.Plan.RequireExecutionGroups(new[]{approved})) &&
                        build.Plan.Plan.Steps[0].ExecutionGroup=="vision-stage",
                        "R08校验不应另建MRK Plan或修改原MOL工程组引用");
                    var engine=new MRKExecutionEngine(new FCRWorkerOptions(1,4),
                        groups:new[]{new MRKExecutionGroupDefinition(
                            "vision-stage",1,1,MRKExecutionGroupScope.Stage)});
                    try
                    {
                        MRKFlowResult result=engine.StartFlow(build.Plan.Plan).Completion
                            .GetAwaiter().GetResult();
                        Check(result.State==MRKFlowState.Completed,
                            "R08工程Stage配置未被原MRK真实执行");
                        engine.StopAsync().GetAwaiter().GetResult();
                        Check(engine.GroupSnapshot()[0].Active==0,
                            "R08工程Stage运行后组许可证未归还");
                    }
                    finally{engine.StopAsync(true).GetAwaiter().GetResult();}
                }
                finally{reopened.Close(true);}
                long version=session.Revision;
                bool invalid=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.SetExecutionGroup(block,new string('X',129)));}
                catch(ArgumentException){invalid=true;}
                Check(invalid&&session.Revision==version,
                    "R08非法组ID错误提交MEM工程版本");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.SetExecutionGroup(block,null));
                Check(!MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Policies.ContainsKey(block),"R08清除组配置没有还原原策略缺省");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                        .Policies[block].ExecutionGroup=="vision-stage",
                    "R08执行组配置的撤销没有恢复原工程状态");
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
            }
        }

        /// <summary>G05a Control分支实际执行与Join：不能用编排图或源码存在替代结果。</summary>
        /// <summary>
        /// R02：真实MEM工程内V1结构化Branch与V2自由连线Switch共用标量语义。
        /// 数值1与1.0语义相等时只能选择第一个Case；持久重开后仍一致。
        /// </summary>
        private static void R02V1V2SwitchBranchParity()
        {
            var session=MEMEngineering.Create("R02双入口语义对照");
            var structured=session.AddPage("CodeBranch");
            var wired=session.AddPage("StudioBranch");
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "r02-branch-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var decision=MOLControlBlock.Action("decide");
                var optionA=MOLControlBlock.Action("a");
                var shadow=MOLControlBlock.Action("shadow");
                var optionB=MOLControlBlock.Action("b");
                var otherwise=MOLControlBlock.Action("otherwise");
                var after=MOLControlBlock.Action("joined");
                var stepValue=new MOLExpression(MOLExpressionSource.StepResult,
                    decision.Id.ToString("N"),MOLComparison.Equal,"1.0");
                var aliasValue=new MOLExpression(MOLExpressionSource.StepResult,
                    decision.Id.ToString("N"),MOLComparison.Equal,"1");
                var secondValue=new MOLExpression(MOLExpressionSource.StepResult,
                    decision.Id.ToString("N"),MOLComparison.Equal,"2");
                MOLControlBlock branch=MOLControlBlock.Branch(
                    new MOLBranchCase(stepValue,optionA,"case 1.0"),
                    new MOLBranchCase(aliasValue,shadow,"numeric alias"),
                    new MOLBranchCase(secondValue,optionB,"case 2"),
                    new MOLBranchCase(null,otherwise,"default"));
                MOLLogicCodec.Set(session,structured.RootContent,new MOLLogicDefinition(
                    MOLControlBlock.Sequence(decision,branch,after)));
                Guid source=Guid.Empty,a=Guid.Empty,alias=Guid.Empty,b=Guid.Empty,
                    fallback=Guid.Empty,join=Guid.Empty;
                MOCFlowBuilder.Edit(session,wired.RootContent,flow=>{
                    source=flow.Step("decide");
                    a=flow.Step("a");
                    alias=flow.Step("shadow");
                    b=flow.Step("b");
                    fallback=flow.Step("otherwise");
                    join=flow.Step("joined");
                    flow.ConnectConditional(source,"Done",a,"Start","case:1.0");
                    flow.ConnectConditional(source,"Done",alias,"Start","case:1");
                    flow.ConnectConditional(source,"Done",b,"Start","case:2");
                    flow.ConnectConditional(source,"Done",fallback,"Start","default");
                    flow.Connect(a,"Done",join,"Start");
                    flow.Connect(alias,"Done",join,"Start");
                    flow.Connect(b,"Done",join,"Start");
                    flow.Connect(fallback,"Done",join,"Start");
                });
                session.SaveAs(path);
                using(var restored=MEMEngineering.Open(path))
                {
                    var snapshot=restored.GetSnapshot();
                    var reloaded=MOLWiringCodec.Get(snapshot.Find(wired.RootContent.Id));
                    Check(reloaded!=null&&reloaded.Links.Count(l=>l.ConditionId.Length>0)==4,
                        "V2 Switch guards lost on actual project Save/Open");
                    foreach(var testcase in new[]{
                        new{Input=(object)1,Expected="a"},
                        new{Input=(object)2,Expected="b"},
                        new{Input=(object)3,Expected="otherwise"}
                    })
                    {
                        foreach(var contentId in new[]{structured.RootContent.Id,wired.RootContent.Id})
                        {
                            var hits=new List<string>();
                            var resolver=new TestResolver().Register("decide",(ctx,ct)=>
                                Task.FromResult((object)ctx.Inputs["selection"]));
                            foreach(string cap in new[]{"a","shadow","b","otherwise","joined"})
                            {
                                string name=cap;
                                resolver.Register(name,(ctx,ct)=>{
                                    lock(hits){hits.Add(name);}
                                    return Task.FromResult((object)name);
                                });
                            }
                            var compiled=new MOCEngineeringCompiler().Compile(
                                snapshot,contentId,resolver);
                            MOCTraceView trace;
                            var result=Execute(compiled,new Dictionary<string,object>{
                                {"selection",testcase.Input}},out trace);
                            Check(result.State==MRKFlowState.Completed,
                                "R02 V1/V2 flow execution failed");
                            Check(hits.Count==2&&hits.Contains(testcase.Expected)&&
                                hits.Contains("joined")&&!hits.Contains("shadow"),
                                "R02 ordered numeric/Else/Join semantics diverged: "+
                                string.Join(",",hits));
                            Guid selected=contentId==structured.RootContent.Id?
                                testcase.Expected=="a"?optionA.Id:
                                testcase.Expected=="b"?optionB.Id:otherwise.Id:
                                testcase.Expected=="a"?a:
                                testcase.Expected=="b"?b:fallback;
                            Check(result.StepStates[compiled.SourceMap.Find(selected).Single().StepId]
                                    ==MRKStepState.Completed &&
                                trace.Events.Any(e=>e.Origin.BlockId==selected),
                                "R02 selected Case not mapped into real MRK/SourceMap");
                        }
                    }
                }
                int revision=session.Revision;
                bool rejected=false;
                try
                {
                    MOCFlowBuilder.Edit(session,wired.RootContent,flow=>
                        flow.ConnectConditional(source,"Done",a,"Start","case:1.0"));
                }
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "R02 duplicate Link/Case broke atomic engineering edit");
                rejected=false;
                try
                {
                    MOLControlBlock.Branch(
                        new MOLBranchCase(null,MOLControlBlock.Action("a")),
                        new MOLBranchCase(MOLExpression.Always(true),
                            MOLControlBlock.Action("b")));
                }
                catch(InvalidOperationException){rejected=true;}
                Check(rejected,"R02 code Branch must reject Default before other Cases");
            }
            finally
            {
                session.Close(true);
                if(System.IO.File.Exists(path))System.IO.File.Delete(path);
                if(System.IO.File.Exists(path+".cx-lock"))System.IO.File.Delete(path+".cx-lock");
            }
        }

        private static void G05aConditionalBranchesAndJoin()
        {
            var session=MEMEngineering.Create("G05a 条件分支");
            var page=session.AddPage("检测与判定");
            try
            {
                Guid source=Guid.Empty,pass=Guid.Empty,fail=Guid.Empty,join=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    source=b.Step("decide");
                    pass=b.Step("pass");fail=b.Step("fail");join=b.Step("join");
                    b.ConnectConditional(source,"Done",pass,"Start","bool:true");
                    b.ConnectConditional(source,"Done",fail,"Start","bool:false");
                    b.Connect(pass,"Done",join,"Start");
                    b.Connect(fail,"Done",join,"Start");
                });
                var roundtrip=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(roundtrip.Links.Count(l=>l.ConditionId.StartsWith("bool:",StringComparison.Ordinal))==2,
                    "G05a branch guard not stored in Logic V2");
                var encoded=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(roundtrip));
                Check(encoded.Links.Count==4&&encoded.Links.Any(l=>l.ConditionId=="bool:true"),
                    "G05a guarded V2 serialization roundtrip failed");

                int yes=0,no=0,joined=0;
                var resolver=new TestResolver()
                    .Register("decide",(c,t)=>Task.FromResult((object)true))
                    .Register("pass",(c,t)=>{Interlocked.Increment(ref yes);return Task.FromResult((object)1);})
                    .Register("fail",(c,t)=>{Interlocked.Increment(ref no);return Task.FromResult((object)2);})
                    .Register("join",(c,t)=>{Interlocked.Increment(ref joined);return Task.FromResult((object)3);});
                var built=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,resolver);
                Check(built.Succeeded,"G05a guarded Control flow failed MOC validation: "+
                    string.Join(",",built.Diagnostics.Select(d=>d.Code+":"+d.Message)));
                MOCTraceView trace;
                var result=Execute(built.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&yes==1&&no==0&&joined==1,
                    "G05a true branch/Join did not execute correctly");
                Check(built.Plan.SourceMap.Find(join).Count==1&&trace.Events.Count>0,
                    "G05a branch SourceMap or runtime trace missing");

                yes=0;no=0;joined=0;
                var negative=new TestResolver()
                    .Register("decide",(c,t)=>Task.FromResult((object)false))
                    .Register("pass",(c,t)=>{Interlocked.Increment(ref yes);return Task.FromResult((object)1);})
                    .Register("fail",(c,t)=>{Interlocked.Increment(ref no);return Task.FromResult((object)2);})
                    .Register("join",(c,t)=>{Interlocked.Increment(ref joined);return Task.FromResult((object)3);});
                var rejectedBranch=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,negative);
                Check(rejectedBranch.Succeeded,"G05a false branch build failed");
                result=Execute(rejectedBranch.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&yes==0&&no==1&&joined==1,
                    "G05a false branch/Join did not execute correctly");

                int rev=session.Revision;
                bool conflict=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.ConnectConditional(source,"Done",join,"Start","bool:true"));}
                catch(InvalidOperationException){conflict=true;}
                Check(conflict&&session.Revision==rev,
                    "G05a duplicate branch guard did not roll back atomically");

                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    b.RemoveMany(new[]{pass,fail,join});
                    pass=b.Step("pass");fail=b.Step("fail");join=b.Step("join");
                    b.ConnectConditional(source,"Done",pass,"Start","case:PASS");
                    b.ConnectConditional(source,"Done",fail,"Start","default");
                    b.Connect(pass,"Done",join,"Start");
                    b.Connect(fail,"Done",join,"Start");
                });
                yes=0;no=0;joined=0;
                var switchResolver=new TestResolver()
                    .Register("decide",(c,t)=>Task.FromResult((object)"PASS"))
                    .Register("pass",(c,t)=>{Interlocked.Increment(ref yes);return Task.FromResult((object)1);})
                    .Register("fail",(c,t)=>{Interlocked.Increment(ref no);return Task.FromResult((object)2);})
                    .Register("join",(c,t)=>{Interlocked.Increment(ref joined);return Task.FromResult((object)3);});
                var switchBuild=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,switchResolver);
                Check(switchBuild.Succeeded,"G05a Switch could not compile");
                result=Execute(switchBuild.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&yes==1&&no==0&&joined==1,
                    "G05a Switch case path failed");
                yes=0;no=0;joined=0;
                var defaultResolver=new TestResolver()
                    .Register("decide",(c,t)=>Task.FromResult((object)"UNKNOWN"))
                    .Register("pass",(c,t)=>{Interlocked.Increment(ref yes);return Task.FromResult((object)1);})
                    .Register("fail",(c,t)=>{Interlocked.Increment(ref no);return Task.FromResult((object)2);})
                    .Register("join",(c,t)=>{Interlocked.Increment(ref joined);return Task.FromResult((object)3);});
                var defaultBuild=new MOCBuildService().Build(session.GetSnapshot(),page.RootContent.Id,defaultResolver);
                result=Execute(defaultBuild.Plan,null,out trace);
                Check(result.State==MRKFlowState.Completed&&yes==0&&no==1&&joined==1,
                    "G05a Switch default path failed");
            }
            finally{session.Close(true);}
        }

        private static void V2TypedParameterConfiguration()
        {
            var session=MEMEngineering.Create("G04节点参数与类型化端口");
            var page=session.AddPage("参数页");
            try
            {
                Guid first=Guid.Empty,second=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    first=b.Step("acquire");
                    second=b.Step("inspect");
                    b.AddPort(second,new MPCPortDescriptor("Threshold",
                        MPCPortDirection.Input,"double",MPCPortKind.Data));
                    b.AddPort(second,new MPCPortDescriptor("Mode",
                        MPCPortDirection.Input,"string",MPCPortKind.Data));
                    b.Connect(first,"Done",second,"Start");
                    b.SetParameter(second,"Threshold","2.25");
                    b.SetParameter(second,"Mode","Fast");
                });
                var flow=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(flow.Parameters[second].Count==2&&
                    flow.Parameters[second]["Threshold"]=="2.25",
                    "typed parameter did not persist in V2 Logic");
                var decoded=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(flow));
                Check(decoded.Parameters[second]["Mode"]=="Fast",
                    "V2 codec parameter roundtrip failed");
                var snapshot=MOCWiringFragment.Capture(flow,new[]{second});
                var fragment=MOCWiringFragment.FromPayload(snapshot.ToPayload());
                Check(fragment.Parameters[second]["Mode"]=="Fast",
                    "clipboard lost typed parameters");
                IReadOnlyDictionary<Guid,Guid> newIds=null;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    newIds=b.Paste(fragment,80,40));
                var pasted=MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id));
                Check(newIds.Count==1&&newIds[second]!=second&&
                    pasted.Parameters[newIds[second]]["Threshold"]=="2.25",
                    "clipboard pasted old ID or lost values");
                Check(session.Undo().Success&&
                    !MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                        .Parameters.ContainsKey(newIds[second]),
                    "parameter clipboard paste was not atomic Undo");
                // 未连线的Data Port字面量参与MOC→MRK输入绑定，不只是保存在UI。
                var resolver=new TestResolver()
                    .Register("acquire",(c,t)=>Task.FromResult((object)1))
                    .Register("inspect",(c,t)=>Task.FromResult((object)2));
                var compiled=new MOCBuildService().Build(session.GetSnapshot(),
                    page.RootContent.Id,resolver);
                Check(compiled.Succeeded&&compiled.Plan.InputDefaults.Count==2&&
                    compiled.Plan.InputDefaults.Values.OfType<double>().Single()==2.25,
                    "MOC did not compile typed defaults for runtime");
                MOCTraceView trace;
                var running=Execute(compiled.Plan,
                    compiled.Plan.InputDefaults.ToDictionary(k=>k.Key,k=>k.Value),
                    out trace);
                Check(running.State==MRKFlowState.Completed&&trace.Events.Count>0,
                    "MRK did not execute configured input defaults");
                int revision=session.Revision;
                bool rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.SetParameter(second,"Threshold","not-a-double"));}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "invalid typed parameter was not atomic");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.ReplacePort(second,new MPCPortDescriptor("Mode",
                        MPCPortDirection.Input,"string",MPCPortKind.Data)));
                revision=session.Revision;rejected=false;
                try{MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.RemovePort(first,"Done"));}
                catch(InvalidOperationException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "in-use Control Port removal wrongly committed");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>
                    b.RemoveParameter(second,"Threshold"));
                Check(!MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Parameters[second].ContainsKey("Threshold"),
                    "parameter delete did not persist");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(page.RootContent.Id))
                    .Parameters[second].ContainsKey("Threshold"),
                    "parameter delete Undo failed");
            }
            finally{session.Close(true);}
        }

        private static void V2BulkEditingClipboard()
        {
            var session=MEMEngineering.Create("G04批量事务");
            var firstPage=session.AddPage("Page A");
            var secondPage=session.AddPage("Page B");
            try
            {
                Guid a=Guid.Empty,b=Guid.Empty,c=Guid.Empty;
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>{
                    a=builder.Step("capture");b=builder.Step("inspect");c=builder.Step("save");
                    builder.Connect(a,"Done",b,"Start");
                    builder.Connect(b,"Done",c,"Start");
                    builder.Move(a,30,40);builder.Move(b,360,140);builder.Move(c,690,220);
                });
                var initial=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Guid boundaryId=initial.Links.Single(l=>l.Target.BlockId==c).Id;
                var snapshot=MOCWiringFragment.Capture(initial,new[]{a,b});
                Check(snapshot.Blocks.Count==2&&snapshot.Links.Count==1,
                    "selection copied edges crossing outside selection");
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>{
                    builder.MoveMany(new[]{a,b},12,17);
                    builder.Align(new[]{a,b},MOCBlockAlignment.Left);
                });
                var aligned=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Check(aligned.Positions[a].X==aligned.Positions[b].X&&
                    aligned.Links.Single(l=>l.Target.BlockId==c).Id==boundaryId,
                    "batch align damaged external link identity");
                Guid groupId=Guid.Empty;
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    groupId=builder.Group(new[]{a,b},"检测组合"));
                var grouped=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Check(grouped.Groups.Count==1&&grouped.Groups[0].Id==groupId&&
                    grouped.Groups[0].BlockIds.Count==2,
                    "group persisted wrong Block IDs");
                var decodedGroups=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(grouped));
                Check(decodedGroups.Groups.Count==1&&decodedGroups.Groups[0].Id==groupId,
                    "V2 grouped layout XML roundtrip failed");
                snapshot=MOCWiringFragment.Capture(grouped,new[]{a,b});
                Check(snapshot.Groups.Count==1,"clipboard lost complete selected group");
                var transferred=MOCWiringFragment.FromPayload(snapshot.ToPayload());
                Check(transferred.Blocks.Count==2&&transferred.Links.Count==1&&
                    transferred.Groups.Count==1&&
                    transferred.Groups[0].Id==snapshot.Groups[0].Id,
                    "cross-process clipboard V2 payload lost identity or internal links");
                bool malformedRejected=false;
                try{MOCWiringFragment.FromPayload(new byte[]{60,33,68,79,67,84,89,80,69,32,120,62});}
                catch(Exception){malformedRejected=true;}
                Check(malformedRejected,"malformed clipboard payload accepted");
                snapshot=transferred;
                int conflictRevision=session.Revision;
                bool groupOverlapRejected=false;
                try{MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.Group(new[]{b,c},"非法交叠"));}
                catch(InvalidOperationException){groupOverlapRejected=true;}
                Check(groupOverlapRejected&&session.Revision==conflictRevision,
                    "overlapping groups were silently accepted");
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.Distribute(new[]{a,b,c},true));
                var distributed=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                var xs=new[]{a,b,c}.Select(id=>distributed.Positions[id].X)
                    .OrderBy(x=>x).ToArray();
                Check(Math.Abs(xs[1]-(xs[0]+xs[2])/2)<0.001,
                    "horizontal distribution lost equal intervals");
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.ArrangeGrid(new[]{a,b,c}));
                var arranged=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Check(arranged.Positions[a].X<arranged.Positions[b].X&&
                    arranged.Positions[c].X==arranged.Positions[a].X&&
                    arranged.Positions[c].Y>arranged.Positions[b].Y&&
                    arranged.Groups.Count==1,"grid arrangement changed group or order");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id))
                    .Groups.Count==1,"grid layout Undo lost group");
                int revision=session.Revision;
                bool rejected=false;
                try{MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.MoveMany(new[]{a,b},-999999,0));}
                catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected&&session.Revision==revision,
                    "invalid batch move partially mutated document");
                IReadOnlyDictionary<Guid,Guid> mapping=null;
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    mapping=builder.Paste(snapshot,60,65));
                var copied=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Check(mapping.Count==2&&mapping[a]!=a&&mapping[b]!=b&&
                    copied.Blocks.Count==5&&copied.Links.Count==3&&
                    copied.Groups.Count==2&&
                    copied.Groups.Select(g=>g.Id).Distinct().Count()==2&&
                    copied.Links.Any(l=>l.Source.BlockId==mapping[a]&&
                        l.Target.BlockId==mapping[b])&&
                    copied.Links.All(l=>!(l.Source.BlockId==mapping[b]&&l.Target.BlockId==c)),
                    "paste IDs or internal-only wiring are incorrect");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id)).Blocks.Count==3,
                    "bulk paste did not undo as one transaction");
                Check(session.Redo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id)).Blocks.Count==5,
                    "bulk paste Redo failed");

                // 第二个Content不是复制时的实时对象引用，而是不可变选区快照。
                MOCFlowBuilder.Edit(session,secondPage.RootContent,builder=>{
                    builder.Step("receiver");
                    builder.Paste(snapshot,45,50);
                });
                var second=MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id));
                Check(second.Blocks.Count==3&&second.Links.Count==1&&
                    second.Groups.Count==1&&
                    second.Blocks.All(x=>!initial.Blocks.Any(y=>y.Id==x.Id)),
                    "cross-Content paste identity or internal edge lost");
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.RemoveMany(new[]{mapping[a],mapping[b]}));
                var afterDelete=MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id));
                Check(afterDelete.Blocks.Count==3&&afterDelete.Links.Count==2&&
                    afterDelete.Groups.Count==1&&afterDelete.Groups[0].Id==groupId&&
                    afterDelete.Links.Any(l=>l.Id==boundaryId),
                    "batch delete removed unrelated links");
                // 删除全部节点生成合法编辑草稿；构建与执行必须继续拒绝。
                int beforeClear=session.Revision;
                MOCFlowBuilder.Edit(session,secondPage.RootContent,builder=>
                    builder.RemoveMany(builder.Blocks.Select(x=>x.Id).ToArray()));
                var empty=MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id));
                Check(session.Revision==beforeClear+1&&empty.Blocks.Count==0&&
                    empty.Links.Count==0&&empty.Ports.Count==0&&
                    empty.Groups.Count==0&&empty.EntryBlockId==Guid.Empty,
                    "delete all did not save a clean empty draft");
                var emptyCodec=MOLWiringCodec.Deserialize(MOLWiringCodec.Serialize(empty));
                Check(emptyCodec.Blocks.Count==0&&emptyCodec.EntryBlockId==Guid.Empty,
                    "empty V2 draft cannot roundtrip");
                bool emptyRejected=false;
                try{empty.ValidateExecutable();}
                catch(InvalidOperationException){emptyRejected=true;}
                Check(emptyRejected,"empty draft was treated as executable");
                var invalidBuild=new MOCBuildService().Build(session.GetSnapshot(),
                    secondPage.RootContent.Id,new TestResolver());
                Check(!invalidBuild.Succeeded&&invalidBuild.Diagnostics.Any(d=>
                    d.Code=="MOC_WIRING_INVALID"),
                    "empty draft bypassed MOC build gate");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id))
                    .Blocks.Count==3,"delete-all Undo failed");
                Check(session.Redo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id))
                    .Blocks.Count==0,"delete-all Redo failed");
                string emptyPath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "moc-empty-"+Guid.NewGuid().ToString("N")+".cxproj");
                try
                {
                    session.SaveAs(emptyPath);
                    var again=MEMEngineering.Open(emptyPath);
                    try
                    {
                        var reopen=MOLWiringCodec.Get(again.GetSnapshot().Find(secondPage.RootContent.Id));
                        Check(reopen!=null&&reopen.Blocks.Count==0&&
                            reopen.EntryBlockId==Guid.Empty,
                            "actual engineering file lost empty wiring draft");
                    }
                    finally{again.Close(true);}
                }
                finally
                {
                    if(System.IO.File.Exists(emptyPath))System.IO.File.Delete(emptyPath);
                    if(System.IO.File.Exists(emptyPath+".cx-lock"))System.IO.File.Delete(emptyPath+".cx-lock");
                }
                // 清空后能继续通过统一Builder粘贴、编译校验并原子撤销/重做。
                IReadOnlyDictionary<Guid,Guid> reintroduced=null;
                MOCFlowBuilder.Edit(session,secondPage.RootContent,builder=>
                    reintroduced=builder.Paste(transferred,120,90));
                var restoredDraft=MOLWiringCodec.Get(
                    session.GetSnapshot().Find(secondPage.RootContent.Id));
                Check(restoredDraft.Blocks.Count==2&&restoredDraft.Links.Count==1&&
                    restoredDraft.Groups.Count==1&&restoredDraft.EntryBlockId!=Guid.Empty&&
                    reintroduced.Count==2&&reintroduced.Values.Distinct().Count()==2,
                    "paste into empty draft failed to restore entry, groups, or internal links");
                restoredDraft.ValidateExecutable();
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id))
                        .Blocks.Count==0,
                    "paste-into-empty was not one undo step");
                Check(session.Redo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(secondPage.RootContent.Id))
                        .Blocks.Count==2,
                    "paste-into-empty redo lost flow identity");
                MOCFlowBuilder.Edit(session,firstPage.RootContent,builder=>
                    builder.Ungroup(groupId));
                Check(MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id))
                    .Groups.Count==0,"ungroup removed wrong objects or was not saved");
                Check(session.Undo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id))
                    .Groups.Count==1,"ungroup Undo did not restore group");
                Check(session.Redo().Success&&
                    MOLWiringCodec.Get(session.GetSnapshot().Find(firstPage.RootContent.Id))
                    .Groups.Count==0,"ungroup Redo did not remove group");
            }
            finally{session.Close(true);}
        }

        private static void V2DiagnosticLocations()
        {
            var session=MEMEngineering.Create("诊断");
            var page=session.AddPage("Inspection");
            try
            {
                Guid acquire=Guid.Empty, inspect=Guid.Empty;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    acquire=b.Step("capture"); inspect=b.Step("vision.not_registered");
                    b.Connect(acquire,"Done",inspect,"Start");
                });
                var snapshot=session.GetSnapshot();
                var missing=new MOCBuildService().Build(snapshot,page.RootContent.Id,
                    new TestResolver().Register("capture",(c,t)=>Task.FromResult((object)1)));
                Check(!missing.Succeeded&&missing.Diagnostics.Any(x=>
                    x.Code=="MOC_CAPABILITY_NOT_REGISTERED"&&x.BlockId==inspect),
                    "build diagnostics lost block origin");
                var valid=new MOCBuildService().Build(snapshot,page.RootContent.Id,
                    new TestResolver()
                        .Register("capture",(c,t)=>Task.FromResult((object)1))
                        .Register("vision.not_registered",(c,t)=>Task.FromResult((object)2)));
                Check(valid.Succeeded&&valid.Plan.SourceMap.Find(inspect).Count==1,
                    "successful build did not use ordinary MRK plan");
                MOCFlowBuilder.Edit(session,page.RootContent,b=>b.Disconnect(
                    b.Links.Single().Id));
                var incomplete=new MOCBuildService().Build(session.GetSnapshot(),
                    page.RootContent.Id,new TestResolver());
                Check(!incomplete.Succeeded&&incomplete.Diagnostics.Any(x=>
                    x.Code=="MOC_WIRING_INVALID"),
                    "incomplete draft did not return typed diagnostics");
            }finally{session.Close(true);}
        }
    }
}
