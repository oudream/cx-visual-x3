using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionPlacementTests
    {
        internal static void Run()
        {
            RuntimeUsesSameWorkerButSdkRunsOnDedicatedThread();
            MissingDedicatedPlacementRejectedBeforeRunning();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("执行位置：" + message); }
        private static T Await<T>(Task<T> task)
        {
            Task result=Task.WhenAny(task,Task.Delay(8000)).GetAwaiter().GetResult();
            if(!object.ReferenceEquals(result,task)) throw new TimeoutException("线程位置测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            Task result=Task.WhenAny(task,Task.Delay(8000)).GetAwaiter().GetResult();
            if(!object.ReferenceEquals(result,task)) throw new TimeoutException("线程位置测试超时");
            task.GetAwaiter().GetResult();
        }
        private static void RuntimeUsesSameWorkerButSdkRunsOnDedicatedThread()
        {
            FCRDedicatedExecutor dedicated=new FCRDedicatedExecutor("SdkThread",8);
            int sdkCalls=0;
            MRKExecutionStep action=new MRKExecutionStep("sdk",new string[0],
                (ctx,token) => { throw new InvalidOperationException("不应执行同步SDK的普通委托"); },
                dedicatedExecutorId:"hardware",
                synchronousSdk:(ctx,token)=>
                {
                    Interlocked.Increment(ref sdkCalls);
                    Check(Thread.CurrentThread.ManagedThreadId==dedicated.ThreadId,
                        "SDK调用未在所声明的专用线程执行");
                    return (object)22;
                });
            MRKExecutionStep normal=new MRKExecutionStep("normal",new string[0],
                (ctx,token)=>Task.FromResult((object)11));
            MRKExecutionPlan plan=new MRKExecutionPlan("placement",new[]{action,normal});
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(2,4),
                dedicatedExecutors:new Dictionary<string,FCRDedicatedExecutor>{{"hardware",dedicated}});
            MRKFlowResult result=Await(engine.StartFlow(plan).Completion);
            Check(result.State==MRKFlowState.Completed && (int)result.Results["sdk"]==22 &&
                (int)result.Results["normal"]==11 && sdkCalls==1,
                "运行引擎没有统一管理SDK与普通异步任务");
            Await(engine.StopAsync());
            Await(dedicated.StopAsync());
        }
        private static void MissingDedicatedPlacementRejectedBeforeRunning()
        {
            int called=0;
            MRKExecutionPlan plan=new MRKExecutionPlan("missing-sdk",new[]{
                new MRKExecutionStep("sdk",new string[0],
                    (ctx,ct)=>Task.FromResult((object)0),
                    dedicatedExecutorId:"unregistered",synchronousSdk:(ctx,ct)=>
                    {Interlocked.Increment(ref called);return (object)1;})
            });
            MRKExecutionEngine engine=new MRKExecutionEngine(new FCRWorkerOptions(1,2));
            bool refused=false;
            try{engine.StartFlow(plan);}catch(ArgumentException){refused=true;}
            Check(refused && called==0,"不支持的执行位置没有在启动前拒绝");
            Await(engine.StopAsync());
        }
    }
}
