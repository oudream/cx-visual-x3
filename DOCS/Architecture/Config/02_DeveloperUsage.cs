// CxVisual 配置开发体验的候选 API 演示；并非 V7.8 可编译运行代码。
using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Configuration; // 候选门面命名空间

namespace CxVisual.Examples.Configuration
{
    public static class DeveloperUsage
    {
        public static async Task ExampleAsync(CancellationToken cancellationToken)
        {
            // 插件程序集装入后自动发现 [ConfigSection]：开发者不写 Load/Save/Editor/Registry。
            CxConfigSection<XRayLineConfig> config = CxConfig.ForMachine("Machine-A")
                .Open<XRayLineConfig>();

            // 1. 整个配置 / 截面 / 单值：都是强类型，结果不允许就地写回运行真源。
            XRayLineConfig all = config.Get();
            CaptureConfig capture = config.Section(
                x => x.Devices.Detectors["Detector-01"].Capture);
            int exposure = config.Value(
                x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
            decimal minArea = config.Value(
                x => x.Vision.Algorithms["VoidDetection"].MinAreaMm2);

            // 2. 一次查询四类值，并查看来源与版本（这些概念不能混成一个 Version）。
            CxConfigValueInfo<int> state = config.Explain(
                x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
            Console.WriteLine("默认值=" + state.DefaultValue);
            Console.WriteLine("上一保存值=" + state.PreviousSavedValue);
            Console.WriteLine("上一有效值=" + state.PreviousEffectiveValue);
            Console.WriteLine("最新保存值=" + state.LatestSavedValue);
            Console.WriteLine("当前有效值=" + state.EffectiveValue);
            Console.WriteLine("来源=" + state.EffectiveSource);
            Console.WriteLine("保存版本=" + state.SavedVersion);
            Console.WriteLine("生效版本=" + state.EffectiveVersion);
            Console.WriteLine("应用策略=" + state.ApplyPolicy);
            // 还可从 state.SourceChain 查看每层值、胜出来源、未生效原因。

            // 3. 一个参数的生效订阅；旧值/新值来自同一条已确认的生效事件。
            using (IDisposable oneValue = config.Subscribe(
                x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs,
                CxConfigChangeStage.Effective,
                change =>
                {
                    Console.WriteLine("曝光切换 " + change.OldValue + " -> " + change.NewValue);
                    Console.WriteLine("默认=" + change.DefaultValue + ", 版本=" + change.Version);
                }))
            using (IDisposable group = config.SubscribeSection(
                x => x.Vision.Algorithms["VoidDetection"],
                CxConfigChangeStage.Committed,
                change => Console.WriteLine("算法修改字段数=" + change.ChangedPaths.Count)))
            using (IDisposable all = config.SubscribeAll(
                CxConfigChangeStage.Committed,
                change => Console.WriteLine("配置整体提交，版本=" + change.Version)))
            {
                // 4. 一次修改多层配置：独立工作副本、预览差异、验证、原子提交、保存。
                using (CxConfigEdit<XRayLineConfig> edit = config.BeginEdit("改进采图与缺陷检测"))
                {
                    edit.WorkingCopy.Devices.Detectors["Detector-01"].Capture.ExposureMs = 120;
                    edit.WorkingCopy.Vision.Algorithms["VoidDetection"].Threshold = 2100;
                    edit.WorkingCopy.Vision.Algorithms["VoidDetection"].MinAreaMm2 = 0.30m;

                    CxConfigPreview preview = edit.Preview();
                    if (preview.Validation.IsValid)
                    {
                        CxConfigCommitResult result = await edit.CommitAsync(cancellationToken);
                        if (!result.Success)
                        {
                            Console.WriteLine(result.ErrorMessage);
                        }
                        // 保存成功 != 已被正在运行的采图任务采用。
                    }
                }
            }

            // 5. 固定任务快照：后续修改配置不会改变该任务已捕获参数。
            using (CxConfigSnapshot<XRayLineConfig> taskConfig = config.CaptureForTask("Scan-001"))
            {
                int fixedExposure = taskConfig.Value(
                    x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
                Console.WriteLine("任务参数：" + fixedExposure + " ms, v" + taskConfig.Version);
            }
        }
    }
}
