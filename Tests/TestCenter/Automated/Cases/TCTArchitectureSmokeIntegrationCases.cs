using System;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.UI;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTArchitectureSmokeIntegrationCases
    {
        [TCTTest("TCT.UI.ARCHSMOKE.001",
            "ArchitectureSmoke固定CLI兼容、自动测试子节点注册与非法配置反证",
            TCTTestAspect.Integration, "TestCenter / ArchitectureSmoke",
            ObjectId = "TCTArchitectureSmokeProfiles")]
        public static void FixedEntrypointsAndInvalidLaunch(TCTTestContext test)
        {
            TCTWorkspaceRegistry registry = TCTWorkspaceRegistry.CreateDefaults(
                id => { throw new InvalidOperationException("Factories must be lazy"); });
            TCTWorkspaceDescriptor node = registry.Find("auto.architecture-smoke");
            test.AssertTrue(node != null && node.ParentId == "auto.root" && node.Factory != null,
                "ArchitectureSmoke是自动测试根下独立的注册工作区", true, true);
            test.AssertEqual(4, registry.Items.Count(x => x.Kind == TCTWorkspaceNodeKind.Root),
                "仍然只有四个根节点");
            test.AssertEqual(11, TCTArchitectureSmokeProfiles.All.Count,
                "所有预设来自ArchitectureSmoke Program.cs正式入口");
            test.AssertTrue(TCTArchitectureSmokeProfiles.All.Select(p => p.Id)
                    .Distinct(StringComparer.Ordinal).Count() == 11,
                "固定预设ID必须全局唯一", true, true);
            test.AssertEqual("", TCTArchitectureSmokeProfiles.Find("default").Arguments,
                "默认命令无参数");
            test.AssertEqual("--foundation",
                TCTArchitectureSmokeProfiles.Find("foundation").Arguments,
                "Foundation原CLI参数完全兼容");
            test.AssertEqual("--concurrency",
                TCTArchitectureSmokeProfiles.Find("concurrency").Arguments,
                "Concurrency原CLI参数完全兼容");
            test.AssertEqual("--config-wpf",
                TCTArchitectureSmokeProfiles.Find("config-wpf").Arguments,
                "Config WPF原CLI参数完全兼容");
            string file = TCTArchitectureSmokeProcessRunner.ResolveExecutable(Path.GetTempPath());
            test.AssertTrue(string.Equals(Path.GetFileName(file),
                    "CxVisual.Tests.ArchitectureSmoke.exe", StringComparison.Ordinal),
                "只允许固定名称的独立ArchitectureSmoke EXE", true, true);

            bool blocked = false;
            using (TCTArchitectureSmokeProcessRunner runner = new TCTArchitectureSmokeProcessRunner())
            {
                try
                {
                    runner.RunAsync(new TCTArchitectureSmokeProfile("default", "伪装执行",
                        "--unsupported-custom-args", 2), CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                catch (ArgumentException) { blocked = true; }
            }
            test.AssertTrue(blocked, "未注册/伪造CLI参数不得被子进程调用器执行", true, blocked);
            TCTArchitectureSmokeRunResult noRun = new TCTArchitectureSmokeRunResult();
            test.AssertTrue(!noRun.Succeeded, "无ExitCode不得伪造总体测试成功", true, true);
        }
    }
}
