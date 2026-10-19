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
            V2WiringCompileRun();
            V2BuilderSharedDefinition();
            V2DraftPortsUndoAndCompileGate();
            V2CapabilityDiscovery();
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
    }
}
