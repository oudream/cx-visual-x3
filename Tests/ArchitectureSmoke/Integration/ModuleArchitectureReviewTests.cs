// 本批最小架构合同测试；只测试结构身份，不代表连接/规约/设备行为已实现。
using System;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ModuleArchitectureReviewTests
    {
        public static void Run()
        {
            foreach (MFMModuleKind kind in Enum.GetValues(typeof(MFMModuleKind)))
            {
                MFMModuleDescriptor descriptor = new MFMModuleDescriptor(
                    "review."+kind, kind.ToString(), kind, 1, new [] { "capability."+kind });
                if (descriptor.Kind != kind || descriptor.CapabilityIds.Count != 1)
                    throw new InvalidOperationException("五类模块合同描述错误");
            }
            bool rejected=false;
            try { new MFMModuleDescriptor("m","m",MFMModuleKind.Compute,1,new[]{"same","same"}); }
            catch (ArgumentException) { rejected=true; }
            if (!rejected) throw new InvalidOperationException("重复能力 Id 应拒绝");
            ModuleFoundationSmokeTests.Run();
            ModuleConfigAnnotationSmokeTests.Run();
        }
    }
}
