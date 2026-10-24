using CxVisual.Foundation.Data.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.Messages;
using CxVisual.Mechanism.Data;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MessageCenterTests
    {
        private const string Camera = "/machine/1201/app/XRay/module/Camera/message/Captured";
        private const string Ai = "/machine/1201/app/XRay/module/AI/message/Completed";
        private const string Reply = "/machine/1201/app/XRay/module/Camera/message/Reply";
        private const string Fault = "/machine/1201/app/XRay/module/Camera/message/Faulted";

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("BUS.M36: " + message);
        }
        private static void Expect<T>(Action a) where T : Exception
        {
            bool got = false;
            try { a(); } catch (T) { got=true; }
            Check(got,"expected "+typeof(T).Name);
        }
        private static async Task ExpectAsync<T>(Func<Task> a) where T : Exception
        {
            bool got = false;
            try { await a().ConfigureAwait(false); } catch(T) { got = true; }
            Check(got,"expected async "+typeof(T).Name);
        }

        public static void Run()
        {
            RunAsync().GetAwaiter().GetResult();
            Console.WriteLine("PASS BUS.M36 Message Center sync/async/wildcards/shared/QoS/retained/request/overflow/lifecycle");
        }

        private static async Task RunAsync()
        {
            await TestBasicAsync().ConfigureAwait(false);
            await TestSharedAsync().ConfigureAwait(false);
            await TestRetentionAndFilterAsync().ConfigureAwait(false);
            await TestOverflowAndStopAsync().ConfigureAwait(false);
            await TestRetriesAsync().ConfigureAwait(false);
            await TestRequestResponseAsync().ConfigureAwait(false);
            await TestConcurrencyAsync().ConfigureAwait(false);
            await TestSecurityAsync().ConfigureAwait(false);
            await TestTargetAndSourceAsync().ConfigureAwait(false);
            await TestNestedAsyncPublishAsync().ConfigureAwait(false);
            await TestParallelAndOptionsSnapshotAsync().ConfigureAwait(false);
            TestCodecAndLatency();
            await TestRetainedRoutingAndInterceptorsAsync().ConfigureAwait(false);
            await TestMpaMessageMappingAsync().ConfigureAwait(false);
        }


        private static async Task TestParallelAndOptionsSnapshotAsync()
        {
            const string slow="/machine/1201/app/XRay/module/Camera/message/Slow";
            const string fast="/machine/1201/app/XRay/module/Camera/message/Fast";
            MNMMessageOptions host=new MNMMessageOptions {QueueCapacity=1,MaxParallelDispatches=2};
            using (MNMMessageCenter bus=new MNMMessageCenter(host))
            {
                host.MaxParallelDispatches=1; // Host配置应在Center构造时复制。
                TaskCompletionSource<bool> entered=new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<bool> release=new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int observed=0;
                MNMSubscriptionOptions subscribe=new MNMSubscriptionOptions {
                    SubscriberId="original",SourceFilter="sourceA"};
                using(bus.SubscribeAsync<int>(slow,async ctx=>
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    Interlocked.Increment(ref observed);
                },subscribe))
                using(bus.Subscribe<string>(fast,ctx=>Interlocked.Increment(ref observed)))
                {
                    subscribe.SourceFilter="other"; // 不得改变现有Subscription匹配。
                    MNMPublishReceipt first=bus.Publish(slow,1,new MNMPublishOptions {
                        DispatchMode=MNMDispatchMode.ThreadPool,SourceId="sourceA"});
                    Check(first.Accepted,"ThreadPool accepted");
                    Task started=await Task.WhenAny(entered.Task,Task.Delay(4000)).ConfigureAwait(false);
                    Check(started==entered.Task,"ThreadPool started");
                    MNMPublishReceipt fastResult=await bus.PublishAsync(fast,"fast",
                        new MNMPublishOptions{DispatchMode=MNMDispatchMode.ThreadPool}).ConfigureAwait(false);
                    Check(fastResult.Stage==MNMPublishStage.Delivered &&
                        observed==1,"slow topic cannot block independent ThreadPool work");
                    Check(bus.Metrics().ParallelInFlight>=1,"in-flight metric");
                    release.TrySetResult(true);
                    await bus.StopAsync(true).ConfigureAwait(false);
                    Check(observed==2,"parallel in-flight drained before StopAsync return");
                }
            }

            using(MNMMessageCenter bus=new MNMMessageCenter(new MNMMessageOptions {
                MaxParallelDispatches=1}))
            {
                TaskCompletionSource<bool> entered=new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                TaskCompletionSource<bool> release=new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using(bus.SubscribeAsync<int>(slow,async ctx=>
                { entered.TrySetResult(true); await release.Task.ConfigureAwait(false); }))
                {
                    bus.Publish(slow,1,new MNMPublishOptions{DispatchMode=MNMDispatchMode.ThreadPool});
                    Task started=await Task.WhenAny(entered.Task,Task.Delay(4000)).ConfigureAwait(false);
                    Check(started==entered.Task,"capacity test callback started");
                    MNMPublishReceipt rejected=bus.Publish(slow,2,new MNMPublishOptions {
                        DispatchMode=MNMDispatchMode.ThreadPool});
                    Check(rejected.Stage==MNMPublishStage.Rejected,
                        "bounded ThreadPool over-capacity must reject");
                    release.TrySetResult(true);
                    await bus.StopAsync(true).ConfigureAwait(false);
                }
            }
        }


        [System.Runtime.Serialization.DataContract]
        private sealed class SimpleInspectionDto
        {
            [System.Runtime.Serialization.DataMember]
            public string ImageRef { get; set; }
            [System.Runtime.Serialization.DataMember]
            public int Width { get; set; }
        }

        private static void TestCodecAndLatency()
        {
            MNMMessageSerialization codecs = new MNMMessageSerialization();
            MNMMessageTypeDefinition textType = new MNMMessageTypeDefinition(
                "Camera.Text",1,typeof(string),maxPayloadBytes:6);
            byte[] encodedText=codecs.Serialize(textType,"Hi");
            Check((string)codecs.Deserialize(textType,encodedText)=="Hi","text codec");
            Expect<ArgumentOutOfRangeException>(()=>codecs.Serialize(textType,"你好世界"));

            MNMMessageTypeDefinition binaryType = new MNMMessageTypeDefinition(
                "Camera.Binary",1,typeof(byte[]),maxPayloadBytes:3);
            byte[] original={1,2,3};
            byte[] encoded=codecs.Serialize(binaryType,original);
            original[0]=88;
            byte[] restored=(byte[])codecs.Deserialize(binaryType,encoded);
            Check(restored[0]==1,"binary encode/decode copies");
            encoded[0]=99;
            Check(restored[0]==1,"binary decode returns independent buffer");

            MNMMessageTypeDefinition structured = new MNMMessageTypeDefinition(
                "Vision.Result",1,typeof(SimpleInspectionDto),
                contentType:"application/json",maxPayloadBytes:1024);
            byte[] json=codecs.Serialize(structured,
                new SimpleInspectionDto{ImageRef="Image001",Width=4300});
            SimpleInspectionDto dto=(SimpleInspectionDto)codecs.Deserialize(structured,json);
            Check(dto.ImageRef=="Image001"&&dto.Width==4300,"structured json codec");
            Expect<NotSupportedException>(()=>codecs.Serialize(
                new MNMMessageTypeDefinition("Vision.NoCodec",1,typeof(SimpleInspectionDto)),dto));

            using(MNMMessageCenter bus = new MNMMessageCenter())
            {
                using(bus.Subscribe<int>(Camera,ctx=>{}))
                {
                    MNMPublishReceipt receipt=bus.Publish(Camera,1,
                        new MNMPublishOptions{DispatchMode=MNMDispatchMode.Inline});
                    Check(receipt.DeliveredCount==1,"latency completion");
                }
                MNMMessageMetrics metrics=bus.Metrics();
                Check(metrics.LatencySamples>=1 &&
                    metrics.P99DeliveryMilliseconds>=metrics.P95DeliveryMilliseconds &&
                    metrics.P95DeliveryMilliseconds>=0,
                    "bounded latency diagnostics");
            }
        }


        private sealed class ProbeInterceptor : IMNMMessageInterceptor
        {
            internal int Publishing;
            internal int Delivered;
            internal bool RejectBefore;
            internal bool ThrowAfter;
            public void BeforePublish(MNMMessage message)
            {
                Interlocked.Increment(ref Publishing);
                if (RejectBefore) throw new InvalidOperationException("blocked by interceptor");
            }
            public void AfterDelivery(MNMMessage message, MNMPublishReceipt receipt)
            {
                Interlocked.Increment(ref Delivered);
                if (ThrowAfter) throw new InvalidOperationException("diagnostic observer failure");
            }
        }

        private static async Task TestRetainedRoutingAndInterceptorsAsync()
        {
            using (MNMMessageCenter bus=new MNMMessageCenter())
            {
                bus.RegisterType(new MNMMessageTypeDefinition(
                    "Camera.Captured",1,typeof(int),topic:Camera));
                MNMPublishReceipt sent=await bus.PublishAsync(Camera,4300,new MNMPublishOptions {
                    TypeId="Camera.Captured",SourceId="Camera",TargetId="HMI",Retain=true
                }).ConfigureAwait(false);
                Check(sent.Stage==MNMPublishStage.Accepted,"retained targeted publish accepted");
                int received=0;

                // 历史定向消息不能重放给其他订阅者。
                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref received),
                    new MNMSubscriptionOptions {SubscriberId="AI"}))
                    Check(bus.Metrics().QueueDepth==0 && received==0,"retained TargetId isolation");

                // 即使Topic匹配，SourceFilter/TypeId/NoLocal不满足，也不能历史重放。
                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref received),
                    new MNMSubscriptionOptions {SubscriberId="HMI",SourceFilter="Other"}))
                    Check(bus.Metrics().QueueDepth==0 && received==0,"retained SourceFilter isolation");
                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref received),
                    new MNMSubscriptionOptions {SubscriberId="HMI",TypeId="Camera.Unrelated"}))
                    Check(bus.Metrics().QueueDepth==0 && received==0,"retained TypeId isolation");
                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref received),
                    new MNMSubscriptionOptions {SubscriberId="HMI",NoLocal=true,SourceId="Camera"}))
                    Check(bus.Metrics().QueueDepth==0 && received==0,"retained NoLocal isolation");
                Expect<ArgumentException>(()=>bus.Subscribe<int>(Camera,ctx=>{},
                    new MNMSubscriptionOptions{NoLocal=true}));

                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref received),
                    new MNMSubscriptionOptions {
                        SubscriberId="HMI",SourceFilter="Camera",TypeId="Camera.Captured"
                    }))
                {
                    Check(SpinWait.SpinUntil(()=>Volatile.Read(ref received)==1,4000),
                        "valid retained replay delivered once");
                }
                await bus.StopAsync(true).ConfigureAwait(false);
            }

            using (MNMMessageCenter bus=new MNMMessageCenter())
            {
                int handled=0;
                int errorCount=0;
                bus.InterceptorError += error=>Interlocked.Increment(ref errorCount);
                ProbeInterceptor probe=new ProbeInterceptor();
                IDisposable lease=bus.AddInterceptor(probe);
                using (bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref handled)))
                {
                    MNMPublishReceipt first=await bus.PublishAsync(Camera,1).ConfigureAwait(false);
                    Check(first.Stage==MNMPublishStage.Delivered &&
                        probe.Publishing==1 && probe.Delivered==1,
                        "interceptor before/after");
                    probe.RejectBefore=true;
                    Expect<InvalidOperationException>(()=>bus.Publish(Camera,2));
                    Check(bus.Metrics().Published==1 && handled==1 && probe.Delivered==1,
                        "pre-publish interceptor fail-closed without phantom published");
                    probe.RejectBefore=false;
                    probe.ThrowAfter=true;
                    MNMPublishReceipt observed=await bus.PublishAsync(Camera,3).ConfigureAwait(false);
                    Check(observed.Stage==MNMPublishStage.Delivered &&
                        handled==2 && bus.Metrics().InterceptorFailures==1 &&
                        errorCount==1,"post-delivery observer failure isolated");
                    Check(bus.Metrics().MeanPublishPerSecond>0,"published throughput metric");
                    lease.Dispose();
                    await bus.PublishAsync(Camera,4).ConfigureAwait(false);
                    Check(probe.Publishing==3 && probe.Delivered==2,
                        "disposed interceptor not called on later publish");
                }
                await bus.StopAsync(true).ConfigureAwait(false);
            }
            Console.WriteLine("PASS BUS.M36 retained ACL-shaped filters, interceptor lifecycle and isolation");
        }


        /// <summary>
        /// MPA DataMapping的真实消息发送映射：复用MDSProtocolDataTargetAccess的
        /// 既有publish注入，不增加一套消息管理器，不将Accepted冒充实际投递完成。
        /// </summary>
        private static async Task TestMpaMessageMappingAsync()
        {
            const string mapped="/machine/1201/app/XRay/module/Camera/message/Captured";
            int observed=0;
            using (MNMMessageCenter bus=new MNMMessageCenter())
            {
                Func<string,object,CancellationToken,Task> binding=async (target,payload,ct)=>
                {
                    MNMPublishReceipt receipt=await bus.PublishAsync<object>(
                        target,payload,
                        new MNMPublishOptions{RequireSubscriber=true},
                        ct).ConfigureAwait(false);
                    if(receipt.Stage!=MNMPublishStage.Delivered || receipt.DeliveredCount==0)
                        throw new InvalidOperationException(
                            "MPA消息目标未真正完成订阅投递: "+receipt.Stage);
                };
                MDSProtocolDataTargetAccess mpa=new MDSProtocolDataTargetAccess(publish:binding);
                using(bus.Subscribe<string>(mapped,ctx=>
                {
                    Check(ctx.Payload=="Image001","MPA message payload");
                    Interlocked.Increment(ref observed);
                }))
                {
                    await mpa.PublishAsync(mapped,"Image001",CancellationToken.None)
                        .ConfigureAwait(false);
                    Check(observed==1,"MPA actual injected M36 delivery");
                    await ExpectAsync<ArgumentException>(async()=>await mpa.PublishAsync(
                        "/machine/1201/app/XRay/module/Camera/message/+",
                        "Image001",CancellationToken.None).ConfigureAwait(false))
                        .ConfigureAwait(false);
                    using(CancellationTokenSource cancel=new CancellationTokenSource())
                    {
                        cancel.Cancel();
                        await ExpectAsync<OperationCanceledException>(async()=>await
                            mpa.PublishAsync(mapped,"Image001",cancel.Token)
                            .ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    Check(observed==1,"MPA rejected/aborted operations have no side effect");
                }
                await ExpectAsync<InvalidOperationException>(async()=>await
                    mpa.PublishAsync(mapped,"Image002",CancellationToken.None)
                    .ConfigureAwait(false)).ConfigureAwait(false);
                Check(observed==1,"MPA no subscribers must fail instead of fake success");
                await bus.StopAsync(true).ConfigureAwait(false);
            }
            MDSProtocolDataTargetAccess absent=new MDSProtocolDataTargetAccess();
            Expect<NotSupportedException>(()=>absent.PublishAsync(
                mapped,"Image001",CancellationToken.None));
            Console.WriteLine("PASS BUS.M36 MPA data mapping / real delivery and reject semantics");
        }

        private static async Task TestBasicAsync()
        {
            using (MNMMessageCenter bus = new MNMMessageCenter())
            {
                int first=0, second=0, duplicate=0;
                IDisposable a=bus.Subscribe<string>(
                    "/machine/1201/app/XRay/module/+/message/Captured",ctx =>
                    {
                        Check(ctx.Payload=="Image001","payload"); Interlocked.Increment(ref first);
                    },new MNMSubscriptionOptions{SubscriberId="camera-reader"});
                IDisposable b=bus.Subscribe<string>(Camera,ctx => Interlocked.Increment(ref second),
                    new MNMSubscriptionOptions{SubscriberId="second-reader"});
                IDisposable overlap=bus.Subscribe<string>(Camera,ctx => Interlocked.Increment(ref duplicate),
                    new MNMSubscriptionOptions{SubscriberId="camera-reader"});
                // 重叠过滤对同一SubscriberId仅投递一次。
                MNMPublishReceipt receipt=await bus.PublishAsync(Camera,"Image001").ConfigureAwait(false);
                Check(receipt.Stage==MNMPublishStage.Delivered &&
                    receipt.MatchedCount==2 && receipt.DeliveredCount==2,"fanout and dedup");
                Check(first+duplicate==1 && second==1,"overlap exactly one per subscriber");
                a.Dispose(); overlap.Dispose(); b.Dispose();
                receipt=await bus.PublishAsync(Camera,"Image002").ConfigureAwait(false);
                Check(receipt.MatchedCount==0 && receipt.Stage==MNMPublishStage.Accepted,"zero subscriber");
                receipt=await bus.PublishAsync(Camera,"Image003",
                    new MNMPublishOptions{RequireSubscriber=true}).ConfigureAwait(false);
                Check(receipt.Stage==MNMPublishStage.Rejected,"require subscriber rejection");

                IDisposable scoped=bus.Subscribe<int>(Fault,ctx=>Interlocked.Increment(ref first));
                receipt=bus.Publish(Fault,1,new MNMPublishOptions{DispatchMode=MNMDispatchMode.Inline});
                Check(receipt.Stage==MNMPublishStage.Delivered,"inline publish");
                scoped.Dispose();

                byte[] original={1,2,3};
                byte[] observed=null;
                using(bus.Subscribe<byte[]>(Camera,ctx=>observed=ctx.Payload))
                {
                    receipt=bus.Publish(Camera,original,
                        new MNMPublishOptions{DispatchMode=MNMDispatchMode.Inline});
                    Check(receipt.Stage==MNMPublishStage.Delivered,"binary publish");
                    original[0]=99;
                    Check(observed[0]==1,"binary copy prevents alias mutation");
                }

                bus.RegisterType(new MNMMessageTypeDefinition(
                    "Camera.Captured",1,typeof(string),topic:Camera));
                receipt=await bus.PublishAsync(Camera,"abc",
                    new MNMPublishOptions{TypeId="Camera.Captured"}).ConfigureAwait(false);
                Check(receipt.Stage==MNMPublishStage.Accepted,"registered schema publish");
                Expect<ArgumentException>(()=>bus.Publish(Camera,123,
                    new MNMPublishOptions{TypeId="Camera.Captured"}));
                Expect<InvalidOperationException>(()=>bus.RegisterType(new MNMMessageTypeDefinition(
                    "Camera.Captured",1,typeof(string),topic:Camera)));
                Check(bus.Metrics().SubscriptionCount==0,"subscription dispose");
            }
        }

        private static async Task TestSharedAsync()
        {
            using(MNMMessageCenter bus = new MNMMessageCenter())
            {
                int a=0,b=0,observer=0;
                string shared="$share/workers/"+Camera;
                using(bus.Subscribe<int>(shared,ctx=>Interlocked.Increment(ref a),
                    new MNMSubscriptionOptions{SubscriberId="a"}))
                using(bus.Subscribe<int>(shared,ctx=>Interlocked.Increment(ref b),
                    new MNMSubscriptionOptions{SubscriberId="b"}))
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref observer)))
                {
                    for(int k=0;k<10;k++)
                    {
                        MNMPublishReceipt rc=await bus.PublishAsync(Camera,k).ConfigureAwait(false);
                        Check(rc.MatchedCount==2 && rc.DeliveredCount==2,"one per shared group");
                    }
                    Check(a==5 && b==5 && observer==10,"shared round robin and broadcast");
                }
            }
        }

        private static async Task TestRetentionAndFilterAsync()
        {
            using(MNMMessageCenter bus = new MNMMessageCenter(new MNMMessageOptions{
                RetainedCapacity=2 }))
            {
                await bus.PublishAsync(Camera,4300,
                    new MNMPublishOptions{Retain=true}).ConfigureAwait(false);
                int matched=0, filtered=0;
                using(bus.Subscribe<int>("/machine/1201/app/XRay/#",
                    ctx=>Interlocked.Increment(ref matched)))
                using(bus.Subscribe<int>("/machine/1201/app/XRay/#",
                    ctx=>Interlocked.Increment(ref filtered),
                    new MNMSubscriptionOptions{Predicate=m=> (int)m.Payload.Value>5000}))
                {
                    Check(SpinWait.SpinUntil(()=>Volatile.Read(ref matched)==1,4000),"retained replay");
                    Check(filtered==0,"predicate filters retained");
                    MNMPublishReceipt rc=await bus.PublishAsync(Camera,5500).ConfigureAwait(false);
                    Check(rc.DeliveredCount==2 && filtered==1,"predicate delivered");
                }
                int local=0;
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref local),
                    new MNMSubscriptionOptions{SourceId="self",NoLocal=true,ReplayRetained=false}))
                {
                    await bus.PublishAsync(Camera,2,
                        new MNMPublishOptions{SourceId="self"}).ConfigureAwait(false);
                    Check(local==0,"no-local filter");
                    await bus.PublishAsync(Camera,2,
                        new MNMPublishOptions{SourceId="other"}).ConfigureAwait(false);
                    Check(local==1,"other source allowed");
                }
            }
        }

        private static async Task TestOverflowAndStopAsync()
        {
            using(MNMMessageCenter bus = new MNMMessageCenter(
                new MNMMessageOptions{QueueCapacity=1}))
            {
                int observed=0;
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref observed)))
                {
                    bus.Pause();
                    MNMPublishReceipt accepted=bus.Publish(Camera,1);
                    MNMPublishReceipt rejected=bus.Publish(Camera,2);
                    Check(accepted.Stage==MNMPublishStage.Accepted &&
                        rejected.Stage==MNMPublishStage.Rejected,"bounded queue");
                    bus.Resume();
                    Check(SpinWait.SpinUntil(()=>observed==1,4000),"resume flush");
                    await bus.StopAsync().ConfigureAwait(false);
                    Check(bus.State==MNMMessageState.Stopped,"stopped state");
                    Expect<ObjectDisposedException>(()=>bus.Publish(Camera,3));
                }
            }
            using(MNMMessageCenter bus = new MNMMessageCenter(
                new MNMMessageOptions{QueueCapacity=1,
                    OverflowPolicy=MNMOverflowPolicy.DropOldest}))
            {
                bus.Pause();
                MNMPublishReceipt oldest=bus.Publish(Camera,1);
                MNMPublishReceipt latest=bus.Publish(Camera,2);
                Check(oldest.Stage==MNMPublishStage.Rejected &&
                    latest.Stage==MNMPublishStage.Accepted,"drop oldest");
                bus.Resume();
                await bus.StopAsync().ConfigureAwait(false);
            }
        }

        private static async Task TestRetriesAsync()
        {
            using(MNMMessageCenter bus = new MNMMessageCenter(new MNMMessageOptions{
                MaxAttempts=3,RetryDelayMilliseconds=1,DeadLetterCapacity=2}))
            {
                int attempts=0;
                using(bus.SubscribeAsync<int>(Camera,ctx=>{
                    if(Interlocked.Increment(ref attempts)<3)
                        throw new InvalidOperationException("temporary");
                    return Task.CompletedTask;
                }))
                {
                    MNMPublishReceipt receipt=await bus.PublishAsync(Camera,10,
                        new MNMPublishOptions{QoS=MNMMessageQoS.AtLeastOnceInProcess}).ConfigureAwait(false);
                    Check(receipt.DeliveredCount==1 && attempts==3,"QoS1 in process retry");
                    Check(bus.Metrics().Retried==2,"retry metrics");
                }
                using(bus.SubscribeAsync<int>(Camera,ctx=>{
                    throw new InvalidOperationException("always fail");
                }))
                {
                    MNMPublishReceipt receipt=await bus.PublishAsync(Camera,11,
                        new MNMPublishOptions{QoS=MNMMessageQoS.AtLeastOnceInProcess}).ConfigureAwait(false);
                    Check(receipt.Stage==MNMPublishStage.Failed && receipt.FailedCount==1,"permanent error");
                    Check(bus.DeadLetters().Length==1 && bus.Metrics().Failed==1,"deadletter");
                }
                using(bus.SubscribeAsync<int>(Camera,ctx=>{
                    throw new InvalidOperationException("QoS0 fail");
                }))
                {
                    int before=bus.DeadLetters().Length;
                    MNMPublishReceipt result=await bus.PublishAsync(Camera,12,
                        new MNMPublishOptions{QoS=MNMMessageQoS.AtMostOnce}).ConfigureAwait(false);
                    Check(result.FailedCount==1 &&
                        bus.DeadLetters().Length==before+1,"QoS0 once");
                }
            }
        }

        private static async Task TestRequestResponseAsync()
        {
            using(MNMMessageCenter bus=new MNMMessageCenter())
            using(bus.SubscribeAsync<int>(Camera,ctx =>
                ctx.ReplyAsync("ok:"+ctx.Payload).ContinueWith(t=>{
                    if(t.IsFaulted) throw t.Exception;
                })))
            {
                MNMMessageContext<string> reply=await bus.RequestAsync<int,string>(
                    Camera,42,Reply,TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Check(reply.Payload=="ok:42" && reply.Header.CorrelationId!=Guid.Empty,
                    "request response linked");
                using (bus.Subscribe<int>(Ai,ctx => { /* 有消费者，但故意不回复 */ }))
                {
                    await ExpectAsync<TimeoutException>(async()=>
                        await bus.RequestAsync<int,string>(Ai,55,Reply,
                            TimeSpan.FromMilliseconds(40)).ConfigureAwait(false)).ConfigureAwait(false);
                }
            }
        }

        private static async Task TestConcurrencyAsync()
        {
            using(MNMMessageCenter bus=new MNMMessageCenter(
                new MNMMessageOptions{QueueCapacity=2048}))
            {
                int seen=0;
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref seen)))
                {
                    Stopwatch sw=Stopwatch.StartNew();
                    Task<MNMPublishReceipt>[] jobs=Enumerable.Range(0,400).
                        Select(k=>bus.PublishAsync(Camera,k)).ToArray();
                    await Task.WhenAll(jobs).ConfigureAwait(false);
                    sw.Stop();
                    Check(seen==400 && jobs.All(k=>k.Result.DeliveredCount==1),
                        "400 concurrent sends delivered once");
                    Check(bus.Metrics().QueueDepth==0,"queue drained");
                    Console.WriteLine("M36 sampled throughput messages/s="+
                        (400.0/Math.Max(0.001,sw.Elapsed.TotalSeconds)).ToString("F0"));
                }
            }
        }

        private static async Task TestSecurityAsync()
        {
            using(MNMMessageCenter bus=new MNMMessageCenter(new MNMMessageOptions{
                AuthorizePublish=(src,topic)=>src=="trusted",
                AuthorizeSubscribe=(subscriber,topic)=>subscriber!="blocked"}))
            {
                Expect<UnauthorizedAccessException>(()=>bus.Subscribe<int>(Camera,ctx=>{},
                    new MNMSubscriptionOptions{SubscriberId="blocked"}));
                using(bus.Subscribe<int>(Camera,ctx=>{}))
                {
                    Expect<UnauthorizedAccessException>(()=>bus.Publish(Camera,5));
                    MNMPublishReceipt rc=await bus.PublishAsync(Camera,5,
                        new MNMPublishOptions{SourceId="trusted"}).ConfigureAwait(false);
                    Check(rc.DeliveredCount==1,"allowed source");
                }
            }
        }
        private static async Task TestTargetAndSourceAsync()
        {
            using(MNMMessageCenter bus=new MNMMessageCenter())
            {
                int a=0,b=0;
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref a),
                    new MNMSubscriptionOptions{SubscriberId="AI",SourceFilter="Camera"}))
                using(bus.Subscribe<int>(Camera,ctx=>Interlocked.Increment(ref b),
                    new MNMSubscriptionOptions{SubscriberId="HMI"}))
                {
                    MNMPublishReceipt first=await bus.PublishAsync(Camera,1,
                        new MNMPublishOptions{SourceId="Camera",TargetId="AI"}).ConfigureAwait(false);
                    Check(first.MatchedCount==1 && a==1 && b==0,"point-to-point AI only");
                    MNMPublishReceipt second=await bus.PublishAsync(Camera,2,
                        new MNMPublishOptions{SourceId="Unknown",TargetId="AI"}).ConfigureAwait(false);
                    Check(second.MatchedCount==0 && a==1,"source filter");
                    MNMPublishReceipt third=await bus.PublishAsync(Camera,3,
                        new MNMPublishOptions{SourceId="Camera"}).ConfigureAwait(false);
                    Check(third.MatchedCount==2 && a==2 && b==1,"broadcast remains");
                }
            }
        }
        private static async Task TestNestedAsyncPublishAsync()
        {
            using(MNMMessageCenter bus=new MNMMessageCenter())
            {
                int nested=0;
                using(bus.Subscribe<string>(Ai,ctx=>Interlocked.Increment(ref nested)))
                using(bus.SubscribeAsync<int>(Camera,async ctx=>
                {
                    MNMPublishReceipt reply=await bus.PublishAsync(Ai,"done").ConfigureAwait(false);
                    Check(reply.DeliveredCount==1,"nested callback publish");
                }))
                {
                    MNMPublishReceipt rc=await bus.PublishAsync(Camera,99).ConfigureAwait(false);
                    Check(rc.DeliveredCount==1 && nested==1,
                        "nested PublishAsync cannot deadlock single consumer");
                }

                bool slowHandlerCompleted = false;
                using(bus.SubscribeAsync<int>(Fault,async ctx=>
                {
                    await Task.Delay(300).ConfigureAwait(false);
                    slowHandlerCompleted = true;
                }))
                {
                    await ExpectAsync<TimeoutException>(async() =>
                        await bus.RequestAsync<int,string>(Fault,1,Reply,
                            TimeSpan.FromMilliseconds(20)).ConfigureAwait(false)).ConfigureAwait(false);
                    Check(!slowHandlerCompleted,
                        "timeout applies during slow request handler, not only after send");
                }
                await bus.StopAsync().ConfigureAwait(false);
            }
        }
    }
}
