using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

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
                Check(running.State==MRKFlowState.Completed&&trace.Steps.Count>0,
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
