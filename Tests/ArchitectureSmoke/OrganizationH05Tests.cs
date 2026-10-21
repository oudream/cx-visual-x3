using System;
using System.IO;
using System.Linq;
using CxVisual.Mechanism.Core;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class OrganizationH05Tests
    {
        private static void Check(bool value,string message){if(!value)throw new InvalidOperationException("H05: "+message);}
        private static void Reject(Action action){bool rejected=false;try{action();}catch(InvalidOperationException){rejected=true;}catch(ArgumentException){rejected=true;}Check(rejected,"expected rejection");}
        public static void Run()
        {
            string dir=Path.Combine(Path.GetTempPath(),"cx-h05-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                var source=MEMEngineering.Create("source");var motor=source.AddContent(source.Project,"Motor");source.DefineProperty(motor,new MPMPropertyDescriptor("speed",MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(10))));
                var output=source.DefinePort(motor,"out",MPCPortDirection.Output,"core.number",isPublic:true);var input=source.DefinePort(motor,"in",MPCPortDirection.Input,"core.number",multiplicity:MPCPortMultiplicity.One,isPublic:true);
                var cabinet=source.AddContent(source.Project,"Cabinet");var a=source.PlaceContent(cabinet,motor,"A");var b=source.PlaceContent(cabinet,motor,"B");source.SetOverride(a,"speed",MPMBinding.Literal(MPMValue.Number(25)));var wire=source.Connect(cabinet,source.Endpoint(a,"out"),source.Endpoint(b,"in"));
                string data=Path.Combine(dir,"static.bin");File.WriteAllBytes(data,new byte[]{1,2,3});var asset=source.AddAsset("static",data);source.DefineProperty(cabinet,new MPMPropertyDescriptor("asset",MPMValueKind.Resource,false,true,MPMBinding.Resource(asset.Id)));
                var target=MEMEngineering.Create("target");var plan=target.PreviewImport(source,new[]{cabinet.Id},target.Project,MTIExternalPolicy.IncludeClosure);
                Check(plan.IsValid&&plan.IdMap[cabinet.Id]!=cabinet.Id&&target.Count==1,"preview mutated target or missing IdMap");
                var result=target.Import(plan);var imported=(MEMContent)target.Find(result.IdMap[cabinet.Id]);var importedA=target.FindReference(result.IdMap[a.Id]);
                Check(imported!=null&&imported.References.Count==2&&target.FindConnection(result.IdMap[wire.Id])!=null&&target.FindPort(result.IdMap[input.Id])!=null&&target.ResolveParameter(importedA,"speed").Value.AsNumber()==25,"import lost ports/references/overrides");
                Check(target.Assets.Count==1&&target.FindProperty(imported,"asset").Binding.ResourceId==result.IdMap[asset.Id]&&target.Validate().IsValid,"asset/member remap or indices");
                target.Undo().RequireValue();Check(target.Count==1&&target.Assets.Count==0,"import not one undo");target.Redo().RequireValue();
                Reject(()=>target.Import(plan));
                string path=Path.Combine(dir,"imported.cxproj");target.SaveAs(path);target.Close();var reopened=MEMEngineering.Open(path);Check(reopened.FindConnection(result.IdMap[wire.Id])!=null&&reopened.Validate().IsValid,"imported file roundtrip");reopened.Close();
                var duplicatePlan=source.PreviewDuplicate(new[]{cabinet.Id},source.Project);var duplicated=source.Import(duplicatePlan);Check(source.FindReference(duplicated.IdMap[a.Id]).TargetId==motor.Id,"duplicate copied rather than pinned shared definition");
                var reject=MEMEngineering.Create("reject");Check(!reject.PreviewImport(source,new[]{cabinet.Id},reject.Project,MTIExternalPolicy.Reject).IsValid,"external rejection ignored");
                var stale=reject.PreviewImport(source,new[]{cabinet.Id},reject.Project);reject.AddContent(reject.Project,"intervening edit");Reject(()=>reject.Import(stale));
                Check(!source.PreviewDuplicate(new[]{cabinet.Id,cabinet.Id},source.Project).IsValid,"duplicate selected roots accepted");
                source.Close(true);reject.Close(true);
                PackagesAndExtraction(dir);
                RemapperAndRollback();
                ResourceImpactAndRevision(dir);
                Console.WriteLine("PASS H05 real cross-project closure/asset import, full identity/endpoint remap, preview, one undo/redo, pin, stale rejection and save/reopen");
            }
            finally{Directory.Delete(dir,true);}
        }
        private static void ResourceImpactAndRevision(string dir)
        {
            MEMEngineeringSession s=MEMEngineering.Create("P0-1 依赖影响");
            MEMContent algorithm=s.AddContent(s.Project,"AI算法定义");
            MEMContent station=s.AddContent(s.Project,"检测工位");
            MEMPage page=s.AddPage("生产HMI");
            MRIResourceReference call=s.PlaceContent(station,algorithm,"检测子流程");
            MRIResourceReference display=s.PlaceContent(page.RootContent,station,"工位引用");
            // 不通过名称做依赖推测：读取现有ResourceEntry的显式元数据。
            int original=s.Revision;
            MRIResourceImpactReport impact=s.AnalyzeResourceImpact(algorithm.Id,original);
            Check(impact.ProjectId==s.Project.Id&&impact.Revision==original&&
                impact.ResourceId==algorithm.Id&&!impact.HasMissingDependencies,
                "影响报告工程/版本身份错误");
            Check(impact.Affected.Count==2&&impact.Affected[0].ResourceId==station.Id&&
                impact.Affected[0].Depth==1&&impact.Affected[1].ResourceId==page.Id&&
                impact.Affected[1].Depth==2&&
                impact.Affected[1].Path.SequenceEqual(new[]{algorithm.Id,station.Id,page.Id}),
                "AI→工位→HMI间接依赖路径错误");
            Check(impact.RebuildResourceIds.SequenceEqual(
                new[]{algorithm.Id,station.Id,page.Id}),"需重构建范围不完整");
            Check(s.AnalyzeResourceImpact(algorithm.Id).Affected.Count==2&&
                s.Revision==original,"影响分析不应修改工程");
            Check(s.AnalyzeResourceImpact(station.Id).Affected.Single().ResourceId==page.Id,
                "只读报表向下游查询错误");
            // 传入内部对象或当前定义也必须归一到资源ID。
            MEMContent inner=s.AddContent(algorithm,"步骤");
            Check(s.AnalyzeResourceImpact(inner.Id).ResourceId==algorithm.Id,
                "内部对象未映射到资源根");
            Reject(()=>s.AnalyzeResourceImpact(algorithm.Id,original));
            // 删除引用后报告应立即反映，旧报告仍为不可变快照。
            s.Remove(display);
            Check(s.AnalyzeResourceImpact(algorithm.Id).Affected.Single().ResourceId==station.Id,
                "删除下游调用后仍错误传播");
            Check(impact.Affected.Count==2,"旧影响报告被可变工程改变");
            s.Undo().RequireValue();
            Check(s.AnalyzeResourceImpact(algorithm.Id).Affected.Count==2,
                "Undo未恢复影响关系");
            // 资产依赖沿用已有MPM资源绑定路径，不能建立第二套绑定索引。
            string file=Path.Combine(dir,"p0-impact.bin");File.WriteAllBytes(file,new byte[]{3,1,4});
            MEMAssetReference asset=s.AddAsset("模型",file);
            s.DefineProperty(algorithm,new MPMPropertyDescriptor("model",MPMValueKind.Resource,
                false,true,MPMBinding.Resource(asset.Id)));
            MRIResourceImpactReport model=s.AnalyzeResourceImpact(asset.Id);
            Check(model.Affected.Select(x=>x.ResourceId).SequenceEqual(
                new[]{algorithm.Id,station.Id,page.Id}),"模型资产变化影响链错误");
            string project=Path.Combine(dir,"impact.cxproj");s.SaveAs(project);s.Close();
            MEMEngineeringSession reopened=MEMEngineering.Open(project);
            Check(reopened.AnalyzeResourceImpact(asset.Id).Affected.Count==3,
                "保存重开后资源影响丢失");
            Reject(()=>reopened.AnalyzeResourceImpact(Guid.NewGuid()));
            reopened.Close();
            Console.WriteLine("PASS P0-1 derived direct/transitive resource impact, revision guard, undo, asset and persistence");
        }
        private sealed class Remapper : IMTIAspectRemapper
        {
            internal Action Callback;
            public MPMAspect Remap(MPMAspect aspect,MTITransferPlan plan)
            {
                if(Callback!=null)Callback();var id=new Guid(aspect.Payload);return new MPMAspect(aspect.Kind,aspect.ProviderId,aspect.SchemaVersion,plan.IdMap[id].ToByteArray(),aspect.Required);
            }
        }
        private static void RemapperAndRollback()
        {
            var source=MEMEngineering.Create("mapped source");var content=source.AddContent(source.Project,"logic");var child=source.AddContent(content,"operation");source.SetAspect(content,new MPMAspect(MPMAspectKind.Logic,"demo.logic",1,child.Id.ToByteArray()));
            var target=MEMEngineering.Create("mapped target");var remapper=new Remapper();var plan=target.PreviewImport(source,new[]{content.Id},target.Project,MTIExternalPolicy.IncludeClosure,remapper);
            Check(plan.IsValid&&plan.Warnings.Count==0&&target.Count==1,"remapper preview mutated target");var result=target.Import(plan);var imported=(MEMContent)target.Find(result.IdMap[content.Id]);Check(new Guid(imported.Aspects[0].Payload)==result.IdMap[child.Id]&&target.ValidateForRun(imported,new[]{"demo.logic"}).IsValid,"provider did not remap private payload identity");
            int revision=target.Revision;remapper.Callback=()=>target.AddContent(target.Project,"illegal callback");Reject(()=>target.PreviewImport(source,new[]{content.Id},target.Project,MTIExternalPolicy.IncludeClosure,remapper));Check(target.Revision==revision,"remapper callback escaped edit boundary");
            remapper.Callback=null;var stale=target.PreviewImport(source,new[]{content.Id},target.Project);source.Rename(child,"changed source");Reject(()=>target.Import(stale));
            // A late extraction failure must reverse every clone/mapping/reference already journaled.
            var page=source.AddPage("P");var item=source.AddContent(page.RootContent,"item");source.DefineProperty(item,new MPMPropertyDescriptor("value",MPMValueKind.Number));source.DefineProperty(page.RootContent,new MPMPropertyDescriptor("outside",MPMValueKind.Number));source.SetProperty(page.RootContent,"outside",MPMBinding.Member(source.GetAddress(item),"value"));
            int count=source.Count,before=source.Revision;Reject(()=>source.ExtractReusable(new[]{item.Id},page.RootContent,"blocked extraction"));Check(source.Count==count&&source.Revision==before&&source.Find(item.Id)==item&&source.Validate().IsValid,"late extraction rejection left partial state");
            source.Close(true);target.Close(true);
        }
        private static void PackagesAndExtraction(string dir)
        {
            var s=MEMEngineering.Create("extract");var page=s.AddPage("P");var owned=s.AddContent(page.RootContent,"station");var external=s.AddContent(page.RootContent,"external");
            var a=s.DefinePort(owned,"output",MPCPortDirection.Output,"core.number");var b=s.DefinePort(external,"input",MPCPortDirection.Input,"core.number",multiplicity:MPCPortMultiplicity.One);
            var wire=s.Connect(page.RootContent,s.Endpoint(a),s.Endpoint(b));
            var symbol=s.AddSymbol("shared visual");var rectangle=s.AddRectangle(symbol.RootGroup,"rect",3,4);s.DefineProperty(symbol,new MPMPropertyDescriptor("W",MPMValueKind.Number,false,true,MPMBinding.Literal(MPMValue.Number(120))));s.ExposeProperty(symbol,"W",s.GetAddress(rectangle),"width");
            var use=s.PlaceSymbol(owned,symbol,"display");s.SetOverride(use,"W",MPMBinding.Literal(MPMValue.Number(150)));s.SetAspect(owned,new MPMAspect(MPMAspectKind.Logic,"unknown",9,new byte[]{0,255,32}));
            var result=s.ExtractReusable(new[]{owned.Id},page.RootContent,"Reusable station");var definition=(MEMContent)s.Find(result.DefinitionId);var mapped=s.FindConnection(wire.Id);
            Check(s.Find(owned.Id)==null&&definition.PublicMappings.Count==1&&mapped.Source.Address.InstancePath.Single()==result.ReferenceId&&s.Validate().IsValid,"extraction failed to expose/reconnect boundary");
            Check(!s.ValidateForRun(definition,new[]{"unknown"}).IsValid,"unmapped opaque payload allowed to run");s.Undo().RequireValue();Check(s.Find(owned.Id)==owned&&s.FindConnection(wire.Id)==wire&&s.Validate().IsValid,"extraction undo lost original wire/identity");s.Redo().RequireValue();
            string path=Path.Combine(dir,"library.cxproj");s.ExportPackage(new[]{definition.Id},path);
            var template=MTIEngineeringTemplates.Create(path,"new project");Check(template.Project.Id!=s.Project.Id&&template.Project.Contents.Count>=1&&template.Validate().IsValid,"template reused project identity or lost library dependencies");
            var definitionCopy=template.Project.Contents.First(c=>c.Name=="Reusable station");var inner=definitionCopy.Contents[0];Check(inner.Aspects[0].RequiresRemap&&inner.Aspects[0].Payload[1]==255,"unknown payload lost in export/template");template.Close(true);s.Close(true);
        }
    }
}
