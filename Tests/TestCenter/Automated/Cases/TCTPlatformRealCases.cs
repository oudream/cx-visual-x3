using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    // 对Foundation与Mechanism当前公开的真实生产API做黑盒测试，绝不复制实现。
    public static class TCTPlatformRealCases
    {
        [FCMConfigSection("TCT.FCM.Exposure", SchemaVersion = 1)]
        public sealed class TCTExposureConfiguration
        {
            [FCMConfigItem("曝光时间", Unit = "ms", Order = 1)]
            [Range(1, 1000)]
            public int ExposureMs { get; set; } = 80;

            [FCMConfigItem("设备启用", Order = 2)]
            public bool Enabled { get; set; } = true;
        }

        [TCTTest("TCT.FCR.001", "FCRWorker执行与停止真实状态", TCTTestAspect.Foundation,
            "Infrastructure / Concurrency", ObjectId = "FCRWorker")]
        public static void WorkerCompletes(TCTTestContext context)
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(2, 8));
            try
            {
                FCRWorkHandle<int> handle = worker.Invoke(() => 6 * 7, "TCT.Line01");
                FCRWorkResult<int> result = handle.Completion.GetAwaiter().GetResult();
                context.AssertEqual(FCRWorkState.Completed, result.State, "真实Worker状态");
                context.AssertEqual(42, result.Value, "真实执行返回值");
                context.AssertTrue(result.Succeeded, "成功标记", true, result.Succeeded);
                FCRWorkerSnapshot snapshot = worker.GetSnapshot();
                context.AssertBetween(1, 100, snapshot.Finished, "Finished计数");
                context.Observe("Runtime", "FCRWorker真实运行快照",
                    "State="+result.State+"; Completed="+snapshot.Finished+
                    "; Pending="+snapshot.Pending+"; Active="+snapshot.Active);
            }
            finally { worker.StopAsync(true).GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.FCR.002", "FCRWorker异常必须可观察", TCTTestAspect.Foundation,
            "Infrastructure / Concurrency", ObjectId = "FCRWorkHandle")]
        public static void WorkerFaultIsVisible(TCTTestContext context)
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 2));
            try
            {
                FCRWorkHandle<int> bad = worker.Submit<int>(token =>
                {
                    throw new InvalidOperationException("TCT_EXPECTED_FAILURE");
                }, "TCT.FailLane");
                FCRWorkResult<int> result = bad.Completion.GetAwaiter().GetResult();
                context.AssertEqual(FCRWorkState.Failed, result.State, "故障结果");
                context.AssertTrue(result.Error is InvalidOperationException, "异常类型", true,
                    result.Error is InvalidOperationException);
                context.AssertEqual("TCT_EXPECTED_FAILURE", result.Error.Message, "异常内容");
                context.Observe("Runtime", "FCRWorker故障证据",
                    "WorkState="+result.State+"; Exception="+result.Error.GetType().Name+
                    "; Error="+result.Error.Message);
            }
            finally { worker.StopAsync(true).GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.FLD.001", "FLDLogService写入、Flush与历史快照", TCTTestAspect.Foundation,
            "Infrastructure / Logging", ObjectId = "FLDLogService")]
        public static void LoggingRoundtrip(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                FLDLogRecord[] snapshot;
                using (FLDLogService service = new FLDLogService(
                    sandbox.PathFor("logfiles"), 32, 20))
                {
                    bool accepted = service.Log(FLDLogLevel.Information, "TCT.Foundation",
                        "TCT真实日志回归");
                    context.AssertTrue(accepted, "队列接收", true, accepted);
                    service.Flush(5000);
                    snapshot = service.Snapshot();
                    context.AssertTrue(snapshot.Any(x=>x.Message=="TCT真实日志回归"),
                        "快照可见", true, snapshot.Length);
                    context.AssertEqual(0L, service.DroppedCount, "正常无丢失");
                }
                context.Observe("Runtime", "FLDLogService真实Flush/Dispose",
                    "Snapshot.Count="+snapshot.Length+"; First.Message="+snapshot[0].Message);
            }
        }

        [TCTTest("TCT.FCM.001", "FCM配置Schema/字段搜索", TCTTestAspect.Foundation,
            "Data / Configuration", ObjectId = "FCMConfigCatalog")]
        public static void ConfigCatalog(TCTTestContext context)
        {
            IList<FCMConfigFieldInfo> fields = FCMConfigCatalog.Describe<TCTExposureConfiguration>();
            context.AssertTrue(fields.Any(x=>x.Path=="ExposureMs" && x.Unit=="ms"),
                "曝光时间字段与单位", true, fields.Count);
            IList<FCMConfigFieldInfo> match = FCMConfigCatalog.Search<TCTExposureConfiguration>("ExposureMs");
            context.AssertEqual(1, match.Count, "配置字段搜索");
            string exported = FCMConfigCatalog.ExportMarkdown<TCTExposureConfiguration>();
            context.AssertTrue(exported.Contains("ExposureMs"), "真实Schema导出", true, exported.Contains("ExposureMs"));
            context.Observe("ObjectTree", "FCM真实Schema",
                string.Join(" → ", fields.Select(x=>x.Path+" ("+x.DataType.Name+")")));
        }

        [TCTTest("TCT.FCM.002", "FCM配置校验与范围拒绝", TCTTestAspect.Foundation,
            "Data / Configuration", ObjectId = "FCMConfigValidationEngine")]
        public static void ConfigValidation(TCTTestContext context)
        {
            TCTExposureConfiguration good = new TCTExposureConfiguration();
            FCMConfigValidationReport valid = FCMConfigValidationEngine.Validate(good, "TCT");
            context.AssertTrue(valid.IsValid, "有效数据通过", true, valid.IsValid);
            TCTExposureConfiguration wrong = new TCTExposureConfiguration { ExposureMs = 0 };
            FCMConfigValidationReport invalid = FCMConfigValidationEngine.Validate(wrong, "TCT");
            context.AssertTrue(!invalid.IsValid, "非法范围被拒绝", false, invalid.IsValid);
            context.AssertTrue(invalid.Issues.Count>0, "有可诊断错误", true, invalid.Issues.Count);
            context.Observe("ObjectTree", "FCM范围错误",
                string.Join(" | ", invalid.Issues.Select(i=>i.Path+": "+i.Code)));
        }

        [TCTTest("TCT.MRK.001", "MRK真实DAG执行及Trace", TCTTestAspect.Mechanism,
            "Runtime / Execution", ObjectId = "MRKExecutionEngine")]
        public static void ExecutionWithTrace(TCTTestContext context)
        {
            MRKExecutionPlan plan = new MRKExecutionPlan("TCT-DAG", new [] {
                new MRKExecutionStep("A", new string[0],
                    (runtime, token) => Task.FromResult((object)3)),
                new MRKExecutionStep("B", new [] { "A" },
                    (runtime, token) => {
                        object a;
                        if (!runtime.TryGetResult("A", out a)) throw new InvalidOperationException("A missing");
                        return Task.FromResult((object)((int)a * 2));
                    })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(2, 8));
            try
            {
                MRKFlowHandle handle = engine.StartFlow(plan);
                MRKFlowResult result = handle.Completion.GetAwaiter().GetResult();
                context.AssertEqual(MRKFlowState.Completed, result.State, "实际Flow完成");
                context.AssertEqual(6, (int)result.Results["B"], "数据依赖真实执行");
                IList<MRKExecutionTransition> trace = handle.Context.Trace.Snapshot();
                context.AssertTrue(trace.Count>=2, "Trace存在", true, trace.Count);
                context.AssertTrue(trace.Zip(trace.Skip(1),(x,y)=>x.Sequence<y.Sequence).All(x=>x),
                    "Trace严格保序", true, true);
                context.Observe("Runtime", "MRK运行时DAG+Trace (非演示)",
                    "FlowId="+result.FlowId+"; State="+result.State+
                    "; B="+result.Results["B"]+"\n"+
                    string.Join("\n", trace.Select(e=>"#"+e.Sequence+" "+e.StepId+" "+e.State)));
            }
            finally { engine.StopAsync(true).GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MRK.002", "MRK计划循环依赖拒绝", TCTTestAspect.Mechanism,
            "Runtime / Execution", ObjectId = "MRKExecutionPlan")]
        public static void PlanCycleRejected(TCTTestContext context)
        {
            context.AssertThrows<ArgumentException>(() => new MRKExecutionPlan("BadCycle", new [] {
                new MRKExecutionStep("A", new [] { "B" }, (c,t)=>Task.FromResult((object)1)),
                new MRKExecutionStep("B", new [] { "A" }, (c,t)=>Task.FromResult((object)2))
            }), "循环计划应被拒绝");
        }
    }
}
