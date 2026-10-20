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
            V2WiringRoundtrip();
            V2TypedParameterConfiguration();
            G05aConditionalBranchesAndJoin();
            R02V1V2SwitchBranchParity();
            G05cWorkflowRetryPolicy();
            G05bStreamBackpressureAndFlow();
            G05dHierarchicalParallelMachine();
            V2ExtractLinearContent();
            G07ProjectReleaseLockAndAuthorization();
            G06CpuVisionThroughMOC();
            G04ParameterSubflowIsolation();
            V2NamedSubflowDataContracts();
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
            Check(plan.SourceMap.Find(action.Id).Count == 3 &&
                plan.SourceMap.Find(action.Id).Select(x => x.Iteration.Value).SequenceEqual(new[]{0,1,2}),
                "loop step provenance missing");
            session.Close(true);
        }

        private static void InvokedContentAndInvalidSource()
        {
            var session=MEMEngineering.Create("invoke");
            MEMPage page=session.AddPage("P");
            MEMContent child=session.AddContent(session.Project,"Shared");
            var internalStep=MOLControlBlock.Action("work");
            MOLLogicCodec.Set(session, child, new MOLLogicDefinition(internalStep));
            var call=new MOLControlBlock(Guid.NewGuid(), MOLControlKind.Invoke, targetFlow:child.Id.ToString("N"));
            MOLLogicCodec.Set(session, page.RootContent, new MOLLogicDefinition(call));
            int calls=0;
            var resolver=new TestResolver().Register("work", (c,t) =>
            { Interlocked.Increment(ref calls); return Task.FromResult((object)1); });
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
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data),
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
                        new MPCPortDescriptor("Number",MPCPortDirection.Input,"int",MPCPortKind.Data),
                        new MPCPortDescriptor("LeafValue",MPCPortDirection.Output,"int",MPCPortKind.Data)});
                    plus=b.Step("Plus",new[]{
                        new MPCPortDescriptor("N",MPCPortDirection.Input,"int",MPCPortKind.Data),
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
