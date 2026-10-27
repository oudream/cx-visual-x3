using CxVisual.Foundation.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Professional.Automation.DeviceVariables;
using CxVisual.Mechanism.Data.Messages;
using CxVisual.Mechanism.Data;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Lifecycle.Modules;
using CxVisual.Professional.Vision.Modules;
using CxVisual.Professional.Simulation.Modules;

namespace CxVisual.Tests.ArchitectureSmoke
{
    // 真实构造/校验/状态测试；不以架构骨架冒充 TCP/PLC 实机测试。
    internal static class ModuleFoundationSmokeTests
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("FM.FOUNDATION: " + message);
        }
        private static void Reject<TException>(Action action) where TException : Exception
        {
            bool raised = false;
            try { action(); }
            catch (TException) { raised = true; }
            Check(raised, "Expected exception: " + typeof(TException).Name);
        }
        private sealed class Increment : IMFMComputeOperation<int, int>
        {
            public int Execute(int x) { return x + 1; }
        }
        private sealed class CounterModel : IPSMSimulationModel
        {
            public string ModelId { get { return "axis"; } }
            public int Count { get; private set; }
            public void Initialize(int seed) { Count = seed; }
            public void Advance(TimeSpan delta) { Count++; }
            public void Reset() { Count = 0; }
        }
        private sealed class SimpleService : MLCServiceModuleBase
        {
            public int Starts { get; private set; }
            public int Stops { get; private set; }
            public SimpleService(MFMModuleDescriptor descriptor) : base(descriptor) { }
            protected override Task OnStartAsync(CancellationToken ct) { Starts++; return Task.CompletedTask; }
            protected override Task OnStopAsync(CancellationToken ct) { Stops++; return Task.CompletedTask; }
        }

        private static void ModbusTcpTransactionSmoke()
        {
            FTRVirtualChannel[] pair = FTRVirtualChannel.CreatePair("mb-request", "mb-device");
            MPAModbusTcpProtocol protocol = new MPAModbusTcpProtocol();
            MPAProtocolConfiguration config = new MPAProtocolConfiguration("mb-request-config", "modbus.tcp");
            config.TimeoutMs = 70;
            MPAProtocolContext context = new MPAProtocolContext(config.Id, "virtual-request",
                new FTRSubChannelAddress("mb-request"), configuration: config, transport: pair[0]);
            int behavior = 0;
            int acceptedResponseCount = 0;
            pair[0].Received += received =>
                acceptedResponseCount += protocol.ProcessReceived(received, context).Count;
            pair[1].Received += received =>
            {
                byte[] frame = received.Data;
                if (behavior == 4) return; // 故意无响应，用于真实超时
                byte[] pdu = behavior == 2 ? new byte[] { 0x83, 0x02 } :
                    (frame[7] == 3 ? new byte[] { 3, 2, 0, 42 } :
                    frame.Skip(7).Take(5).ToArray());
                byte[] reply = new byte[pdu.Length + 7];
                reply[0] = frame[0]; reply[1] = frame[1]; // 保留实际 TransactionId
                reply[5] = (byte)(pdu.Length + 1);
                reply[6] = (byte)(behavior == 3 ? frame[6] + 1 : frame[6]);
                Buffer.BlockCopy(pdu, 0, reply, 7, pdu.Length);
                pair[1].SendAsync(reply, new FTRSubChannelAddress("mb-device"),
                    CancellationToken.None).GetAwaiter().GetResult();
            };
            pair[0].Open(); pair[1].Open();
            try
            {
                MPAProtocolOperationResult read = protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult();
                Check(read.IsCompleted && ((byte[])read.Value).SequenceEqual(new byte[] { 3, 2, 0, 42 }),
                    "Modbus TCP 实际发送并关联 TransactionId 的 03 响应");
                behavior = 1;
                MPAProtocolOperationResult write = protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.WriteSingle(100, 42)),
                    context, CancellationToken.None).GetAwaiter().GetResult();
                Check(write.IsCompleted && ((byte[])write.Value)[0] == 6,
                    "Modbus TCP 06 写入必须等待回显确认");
                behavior = 2;
                Reject<System.IO.InvalidDataException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                behavior = 3;
                Reject<System.IO.InvalidDataException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Check(acceptedResponseCount == 2,
                    "错误功能码及Unit的事务响应不可作为有效Payload发布");
                behavior = 4;
                Reject<TimeoutException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), MPAModbusRegisterOperations.ReadHolding(100, 1)),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Reject<NotSupportedException>(() => protocol.RequestAsync(
                    new MPAProtocolOperationRequest(MPAProtocolOperationKind.Request,
                        new MPAProtocolAddress("3"), new byte[] { 16, 0, 1, 0, 1 }),
                    context, CancellationToken.None).GetAwaiter().GetResult());
                Check(pair[0].State == FTRChannelState.Open, "异常响应/超时不能错误关闭 Channel");
                protocol.ResetAll();
            }
            finally { pair[0].Close(); pair[1].Close(); }
        }


        // P0-1：x2变量对象/运行状态语义的首次真实MDS落位，兼容旧表与作用域。
        private static void VariableObjectSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("leak-tester");
            MDSVariableDefinition definition = new MDSVariableDefinition(
                "Pressure", typeof(double), "MPa", true, 0.0, "pressure-001");
            table.Define(definition);
            MDSVariable<double> pressure = table.Resolve<double>("Pressure");
            MDSVariableSnapshot<double> empty = pressure.Snapshot();
            Check(empty.VariableId == "pressure-001" && empty.HasDefaultValue &&
                empty.DefaultValue == 0.0 && !empty.HasValue && !empty.HasPreviousValue &&
                empty.Quality == MDSVariableQuality.Uncertain,
                "Default is definition, not a successfully sampled current value");
            int notifications = 0;
            table.VariableChanged += name => { if (name == "Pressure") notifications++; };
            table.Update<double>("Pressure", 1.5);
            MDSVariableSnapshot<double> first = pressure.Snapshot();
            Check(first.CurrentValue == 1.5 && first.Revision == 1 &&
                first.HasValue && !first.HasPreviousValue && first.Quality == MDSVariableQuality.Good,
                "First update uses the single authoritative typed variable");
            DateTime changedAt = first.ChangeUtc;
            pressure.Update(1.5); // 同值只刷新，不能覆盖上一次不同的值。
            MDSVariableSnapshot<double> refresh = pressure.Snapshot();
            Check(refresh.Revision == 2 && refresh.ChangeUtc == changedAt &&
                !refresh.HasPreviousValue && notifications == 2, "Equal refresh semantics");
            pressure.Update(2.5);
            pressure.Update(2.5);
            MDSVariableSnapshot<double> last = pressure.Snapshot();
            Check(last.PreviousValue == 1.5 && last.CurrentValue == 2.5 &&
                last.Revision == 4 && last.HasPreviousValue && last.ChangeUtc >= changedAt &&
                table.Get<double>("Pressure") == 2.5 && notifications == 4,
                "Previous tracks last distinct value; hot handle shares the table owner");
            Reject<InvalidCastException>(() => table.Resolve<int>("Pressure"));
            Reject<ArgumentException>(() => new MDSVariableDefinition("Wrong", typeof(int),
                defaultValue: "string-not-int"));
            table.Define(new MDSVariableDefinition("raw", typeof(byte[]),
                defaultValue: new byte[] { 1, 2 }));
            MDSVariable<byte[]> raw = table.Resolve<byte[]>("raw");
            byte[] source = { 3, 4 };
            raw.Update(source); source[0] = 9;
            MDSVariableSnapshot<byte[]> snap = raw.Snapshot();
            byte[] output = snap.CurrentValue; output[1] = 7;
            byte[] originalDefault = snap.DefaultValue; originalDefault[0] = 99;
            Check(raw.Get()[0] == 3 && raw.Get()[1] == 4 &&
                raw.Snapshot().DefaultValue[0] == 1,
                "Snapshot must not leak mutable arrays");
            table.Define(new MDSVariableDefinition("Readonly", typeof(int), writable: false));
            Reject<InvalidOperationException>(() => table.Resolve<int>("Readonly").Update(1));
            MDSVariableTable another = new MDSVariableTable("leak-tester-2");
            another.Define(new MDSVariableDefinition("Pressure", typeof(double)));
            another.Update<double>("Pressure", 99.0);
            Check(table.Get<double>("Pressure") == 2.5 && another.Get<double>("Pressure") == 99.0,
                "Variable scopes must not share values");
            Check(table.Remove("Pressure"), "Removing a typed variable");
            Reject<ObjectDisposedException>(() => pressure.Update(3.0));
            Check(!table.Contains("Pressure") && notifications == 4,
                "Removed typed handles must not publish stale updates");
        }

        private static void VariableTreeSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("line");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, true, null, "pressure-id"));
            table.Define(new MDSVariableDefinition("temp", typeof(int), null, true, null, "temp-id"));
            table.Update<double>("pressure", 1.25);
            MDSVariable<double> original = table.Resolve<double>("pressure");
            MDSVariableGroup group = table.Tree.AddGroup("Line1", id: "line-id");
            MDSVariableGroup station = table.Tree.AddGroup("StationA", "line-id", "station-id");
            table.Tree.Move("pressure-id", station.VariableId);
            MDSVariableLeaf leaf = (MDSVariableLeaf)table.Tree.FindByPath("/Line1/StationA/pressure");
            Check(leaf != null && ReferenceEquals(leaf.Resolve<double>(), original), "变量树不得复制变量值");
            table.Tree.Move("station-id", null, "StationB");
            Check(table.Tree.FindByPath("/Line1/StationA/pressure") == null &&
                table.Tree.FindByPath("/StationB/pressure") == leaf &&
                table.Get<double>("pressure") == 1.25, "变量移动仅更新路径");
            table.Tree.SetOwner("pressure-id", "Device-01");
            Check(leaf.OwnerRef == "Device-01", "业务Owner独立于Parent");
            table.Tree.AddTag("pressure-id", "Critical");
            Check(leaf.HasTag("Critical") && leaf.Tags.Contains("Critical") &&
                table.Tree.RemoveTag("pressure-id", "Critical") &&
                !leaf.HasTag("Critical"), "标签增删与快照不得形成二次变量状态");
            Reject<InvalidOperationException>(() => table.Tree.Move("station-id", "station-id"));
            Reject<InvalidOperationException>(() => table.Tree.Move("temp-id", null, "StationB"));
            Reject<InvalidOperationException>(() => table.Tree.RemoveEmptyGroup("station-id"));
            Check(table.Remove("pressure") && table.Tree.FindById("pressure-id") == null, "移除变量同步移除目录");
            Reject<ObjectDisposedException>(() => original.Update(2.0));
            Check(table.Tree.RemoveEmptyGroup("station-id"), "清空后的分组可以删除");
        }

        private static void VariableDispatcherSmoke()
        {
            MDSVariableTable variables = new MDSVariableTable("dispatch");
            variables.Define(new MDSVariableDefinition("pressure", typeof(int), null, true, null, "P"));
            variables.Define(new MDSVariableDefinition("temperature", typeof(int), null, true, null, "T"));
            MDSVariableChange[] observed = null;
            int callbacks = 0;
            using (MDSVariableChangeDispatcher dispatcher = variables.CreateDispatcher(8))
            {
                using (dispatcher.Subscribe(batch => {
                    observed = batch.ToArray();
                    Interlocked.Increment(ref callbacks);
                }, new[] { "P" }))
                {
                    variables.Update<int>("pressure", 10);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化派发排空1");
                    Check(observed != null && observed.Length == 1 && observed[0].Revision == 1 &&
                        (int)observed[0].CurrentValue == 10, "回调获得提交时的快照");
                    variables.Update<int>("pressure", 11);
                    variables.Update<int>("temperature", 50);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化派发排空2");
                    Check(observed.Length == 1 && observed[0].VariableId == "P" &&
                        observed[0].Revision == 2 && (int)observed[0].PreviousValue == 10,
                        "筛选与前值都必须准确");
                }
                int completed = callbacks;
                variables.Update<int>("pressure", 12);
                Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && callbacks == completed, "退订必须生效");
                Check(dispatcher.Metrics().Received == 4 &&
                    dispatcher.Metrics().SubscriberFailures == 0, "派发统计准确");
                variables.ChangeCaptured -= dispatcher.Enqueue;
            }
        }

        private static void VariableBoundAccessSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("fast");
            registry.Register(table);
            table.Define(new MDSVariableDefinition("readback", typeof(int)));
            table.Resolve<int>("readback").Update(12);
            MDSProtocolDataTargetAccess access = new MDSProtocolDataTargetAccess(registry);
            object current = access.ReadAsync(MPADataTargetKind.Variable, "fast/readback",
                CancellationToken.None).GetAwaiter().GetResult();
            Check((int)current == 12, "点表读取应直接访问单份变量");
            access.WriteAsync(MPADataTargetKind.Variable, "fast/readback", 30,
                CancellationToken.None).GetAwaiter().GetResult();
            Check(table.Get<int>("readback") == 30, "点表写入应走预解析变量");
            Reject<InvalidCastException>(() => access.WriteAsync(MPADataTargetKind.Variable,
                "fast/readback", 1.0, CancellationToken.None).GetAwaiter().GetResult());
        }

        private sealed class FakeVariableProvider : IMDSVariableProvider
        {
            private Action<MDSVariableProviderSample> _push;
            public string ProviderId { get { return "device-p1"; } }
            public void Start(Action<MDSVariableProviderSample> callback) { _push = callback; }
            public void Stop() { _push = null; }
            public void Dispose() { Stop(); }
            public void Emit(string name, object value)
            {
                Action<MDSVariableProviderSample> listener = _push;
                if (listener != null) listener(new MDSVariableProviderSample(name, value));
            }
        }
        private static void VariableProviderSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable table = new MDSVariableTable("leak");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, false, null, "P"));
            registry.Register(table);
            FakeVariableProvider source = new FakeVariableProvider();
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(source);
                host.Bind("device-p1", "pressure-point", "leak", "pressure");
                Check(host.Descriptors().Length == 1 &&
                    host.Descriptors()[0].ProviderId == "device-p1" &&
                    host.Descriptors()[0].BindingCount == 1 &&
                    !host.Descriptors()[0].Running,
                    "Provider只读元数据与真实Host同源");
                Reject<ArgumentException>(() => host.Bind("device-p1", "pressure-point", "leak", "pressure"));
                Reject<InvalidCastException>(() => host.ResolveTyped<int>("device-p1", "pressure-point"));
                host.Start();
                source.Emit("pressure-point", 0.18);
                source.Emit("unknown-point", 1.0);
                Check(table.Get<double>("pressure") == 0.18 && host.Health().Applied == 1 &&
                    host.Health().Rejected == 1, "Provider仅能写授权映射，允许刷新只读变量");
                MDSVariableTypedPointBinding<double> hot = host.ResolveTyped<double>("device-p1", "pressure-point");
                hot.Apply(0.22);
                Check(table.Resolve<double>("pressure").Snapshot().PreviousValue == 0.18 &&
                    table.Get<double>("pressure") == 0.22, "预绑定强类型Provider写入唯一变量");
                Check(registry.Remove("leak"), "模拟旧Scope已从Host卸载");
                Reject<InvalidOperationException>(() => hot.Apply(0.31));
                source.Emit("pressure-point", 0.32);
                Check(table.Get<double>("pressure") == 0.22 && host.Health().Rejected == 2,
                    "旧Provider回调和热句柄不能写入已注销Scope");
                host.Stop();
                Reject<InvalidOperationException>(() => hot.Apply(0.3));
                Check(table.Get<double>("pressure") == 0.22 && !host.Health().Running,
                    "停止后旧世代Point句柄不可再写入");
                Check(host.Unregister("device-p1") && host.Bindings().Length == 0 &&
                    host.Descriptors().Length == 0 &&
                    !host.Unregister("device-p1"), "Provider注销同时撤销点表映射且可重复检查");
            }
        }

        private static void VariableCasSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("cas");
            table.Define(new MDSVariableDefinition("pressure", typeof(double), null, true, null, "CAS1"));
            MDSVariable<double> variable = table.Resolve<double>("pressure");
            MDSVariableUpdateResult accepted = variable.Update(1.0,
                new MDSVariableUpdateOptions("operator", expectedRevision: 0));
            Check(accepted.Revision == 1 && accepted.ValueChanged && variable.Snapshot().Source == "operator",
                "CAS第一次提交成功并保留来源");
            MDSVariableUpdateResult failed;
            Check(!variable.TryUpdate(2.0, 0, out failed) && failed == null &&
                variable.Snapshot().CurrentValue == 1.0 && variable.Snapshot().Revision == 1,
                "旧版本不得修改当前值或版本");
            Reject<MDSVariableVersionConflictException>(() => variable.Update(2.0,
                new MDSVariableUpdateOptions(expectedRevision: 0)));
            MDSVariableUpdateResult refresh = variable.Update(1.0,
                new MDSVariableUpdateOptions(expectedRevision: 1));
            Check(!refresh.ValueChanged && refresh.Revision == 2 &&
                variable.Snapshot().ChangeUtc <= refresh.RefreshUtc,
                "同值刷新提升Revision但不提升变化语义");
        }

        private static void VariableScopeSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable oldTable = new MDSVariableTable("machine");
            oldTable.Define(new MDSVariableDefinition("Pressure", typeof(double),
                null, true, null, "pressure-stable"));
            registry.Register(oldTable);
            MDSVariableScope original = registry.FindScope("machine");
            CxVisual.Foundation.Data.Variables.FDVVariableKey key =
                new CxVisual.Foundation.Data.Variables.FDVVariableKey("machine", "pressure-stable");
            MDSVariableHandle<double> firstHandle = registry.ResolveHandle<double>(key);
            firstHandle.Update(0.5);
            Check(firstHandle.IsActive && firstHandle.Snapshot().CurrentValue == 0.5 &&
                original.Resolve<double>(key).Get() == 0.5, "按Scope和稳定ID指向同一个正式变量");
            Reject<InvalidOperationException>(() => registry.Resolve<double>(
                new CxVisual.Foundation.Data.Variables.FDVVariableKey("wrong", "pressure-stable")));
            Check(registry.Remove("machine") && !original.IsActive, "注销旧Scope应使代次失效");
            Check(!firstHandle.IsActive, "旧代次Handle必须失效");
            Reject<ObjectDisposedException>(() => firstHandle.Get());
            Reject<ObjectDisposedException>(() => firstHandle.Update(0.8));
            Reject<ObjectDisposedException>(() => original.Resolve<double>(key));
            MDSVariableTable fresh = new MDSVariableTable("machine");
            fresh.Define(new MDSVariableDefinition("Pressure", typeof(double),
                null, true, null, "pressure-stable"));
            registry.Register(fresh);
            Check(registry.FindScope("machine").Generation != original.Generation,
                "重复Scope名称不复用上代Generation");
            MDSVariableHandle<double> newHandle = registry.ResolveHandle<double>(key);
            newHandle.Update(0.7);
            Check(newHandle.IsActive && newHandle.Generation != firstHandle.Generation &&
                newHandle.Get() == 0.7 && oldTable.Get<double>("Pressure") == 0.5,
                "新旧会话句柄按Generation隔离并且没有第二份值");
        }

        private static void VariableMessageBridgeSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("line1");
            table.Define(new MDSVariableDefinition("Pressure", typeof(double), null, true, null, "pressure"));
            using (MNMMessageCenter messages = new MNMMessageCenter())
            using (MDSVariableChangeDispatcher dispatcher = table.CreateDispatcher(32))
            {
                MDSVariableChangeNotice received = null;
                using (IDisposable receiver = messages.Subscribe<MDSVariableChangeNotice>(
                    "/machine/line1/app/CxVisual/module/Variables/message/pressure", context => received = context.Payload))
                using (MDSVariableMessageBridge bridge =
                    new MDSVariableMessageBridge(table, dispatcher, messages))
                {
                    table.Update<double>("Pressure", 1.25);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变化通知桥应能排空");
                    Check(received != null && received.ScopeId == "line1" && received.Revision == 1 &&
                        received.VariableId == "pressure" && bridge.Published == 1 && bridge.Failed == 0,
                        "通知必须来自已有MessageCenter且只包含准确变量身份");
                    Check(CxVisual.Foundation.Data.Messages.MNMTopicFilter.ValidateTopic(
                        "/machine/line1/app/CxVisual/module/Variables/message/pressure") != null,
                        "变量消息应遵守M36既有八层Topic契约");
                }
                Reject<ArgumentException>(() => new MDSVariableMessageBridge(
                    table, dispatcher, messages, "/cxvisual/variables"));
            }
        }

        private static void VariableBatchSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("batch");
            table.Define(new MDSVariableDefinition("A", typeof(int)));
            table.Define(new MDSVariableDefinition("B", typeof(int)));
            table.Define(new MDSVariableDefinition("RO", typeof(int), writable: false));
            MDSVariableChangeBatch changedBatch = null;
            table.BatchCommitted += batch => changedBatch = batch;
            MDSVariableBatch request = new MDSVariableBatch("batch",
                new[] { new MDSVariableBatchItem("A", 10),
                        new MDSVariableBatchItem("B", 10) }, "first");
            MDSVariableBatchResult first = table.UpdateBatch(request);
            Check(request.BatchId == "first" && request.Items.Count == 2,
                "批量事务具有稳定请求身份和不可变条目");
            Reject<InvalidOperationException>(() => table.UpdateBatch(
                new MDSVariableBatch("other", new[] { new MDSVariableBatchItem("A", 99) })));
            Reject<ArgumentException>(() => new MDSVariableBatch("batch", new[] {
                new MDSVariableBatchItem("A", 1), new MDSVariableBatchItem("A", 2) }));
            Check(first.Results.Count == 2 && first.NotificationsSucceeded &&
                table.Get<int>("A") == 10 && table.Get<int>("B") == 10, "同Scope多变量批量提交");
            Check(changedBatch != null && changedBatch.BatchId == "first" &&
                changedBatch.ScopeId == "batch" && changedBatch.BatchSequence == 1 &&
                changedBatch.Changes.Count == 2 &&
                changedBatch.Changes[0].Revision == 1 &&
                changedBatch.Changes[1].Revision == 1,
                "批次事件必须冻结全部变化与同Scope连续批次号");
            Reject<InvalidCastException>(() => table.UpdateBatch(
                new MDSVariableBatchItem("A", 15),
                new MDSVariableBatchItem("B", "not-int")));
            Check(table.Get<int>("A") == 10 && table.Get<int>("B") == 10 &&
                table.Resolve<int>("A").Snapshot().Revision == 1, "后续类型错误不得半提交");
            Reject<InvalidOperationException>(() => table.UpdateBatch(
                new MDSVariableBatchItem("A", 30),
                new MDSVariableBatchItem("RO", 1)));
            Reject<MDSVariableVersionConflictException>(() => table.UpdateBatch(
                new MDSVariableBatchItem("A", 30),
                new MDSVariableBatchItem("B", 30,
                    new MDSVariableUpdateOptions(expectedRevision: 0))));
            Check(table.Get<int>("A") == 10 && table.Get<int>("B") == 10, "只读/版本冲突不得改变任何变量");
            System.Threading.Tasks.Parallel.For(0, 2, index =>
            {
                if (index == 0)
                {
                    for (int k = 0; k < 200; ++k)
                        table.UpdateBatch(new MDSVariableBatchItem("A", k),
                            new MDSVariableBatchItem("B", k));
                }
                else
                {
                    for (int k = 0; k < 200; ++k)
                    {
                        IReadOnlyDictionary<string, object> snapshots = table.SnapshotBatch("A", "B");
                        MDSVariableSnapshot<int> a = (MDSVariableSnapshot<int>)snapshots["A"];
                        MDSVariableSnapshot<int> b = (MDSVariableSnapshot<int>)snapshots["B"];
                        Check(a.CurrentValue == b.CurrentValue, "事务快照不得看到半提交");
                    }
                }
            });
            Check(table.Get<int>("A") == 199 && table.Get<int>("B") == 199,
                "批量并发提交最终状态和Revision正确");
        }

        private static void VariableSubscriptionFilterSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("filtered");
            table.Define(new MDSVariableDefinition("State", typeof(int), null, true, null, "STATE"));
            using (MDSVariableChangeDispatcher dispatcher = table.CreateDispatcher(8))
            {
                int deliveries = 0;
                using (dispatcher.SubscribeWithOptions(batch =>
                    deliveries += batch.Count, new MDSVariableSubscriptionOptions(
                        new[] { "STATE" }, MDSVariableChangeKind.Value)))
                {
                    table.Update<int>("State", 1);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "订阅初次排空");
                    Check(deliveries == 1, "首次值变化匹配筛选");
                    table.Update<int>("State", 1);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "同值刷新排空");
                    Check(deliveries == 1, "仅Value订阅不得收到Refresh");
                    table.Update<int>("State", 2);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)), "变值刷新排空");
                    Check(deliveries == 2, "后续Value变化可继续接收");
                }
                Reject<ArgumentOutOfRangeException>(() =>
                    new MDSVariableSubscriptionOptions(kinds: MDSVariableChangeKind.None));
            }
        }

        private sealed class FakeIndustrialWriteBackend : IPADDeviceVariableWriteBackend
        {
            public int CallCount { get; private set; }
            public object LastValue { get; private set; }
            public Task<bool> WriteAsync(PADDeviceVariableDefinition variable,
                object nativeValue, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++; LastValue = nativeValue;
                return Task.FromResult(true);
            }
        }
        private static void IndustrialDeviceVariableWriteSmoke()
        {
            PADDeviceVariableCatalog catalog = new PADDeviceVariableCatalog("plc-write");
            catalog.Register(new PADDeviceVariableDefinition("valve", "Station", "Valve",
                typeof(bool), null, true));
            catalog.Register(new PADDeviceVariableDefinition("pressure", "Station", "Pressure",
                typeof(double), "MPa", false));
            FakeIndustrialWriteBackend backend = new FakeIndustrialWriteBackend();
            using (PADDeviceVariableProvider provider = new PADDeviceVariableProvider(
                catalog, backend, (principal, definition) => principal == "authorized"))
            {
                int samples = 0;
                provider.Start(sample => samples++);
                Reject<UnauthorizedAccessException>(() => provider.WriteAsync("valve", true,
                    "guest", CancellationToken.None).GetAwaiter().GetResult());
                Reject<InvalidOperationException>(() => provider.WriteAsync("pressure", 0.2,
                    "authorized", CancellationToken.None).GetAwaiter().GetResult());
                Reject<InvalidCastException>(() => provider.WriteAsync("valve", 5,
                    "authorized", CancellationToken.None).GetAwaiter().GetResult());
                Check(provider.WriteAsync("valve", true, "authorized",
                    CancellationToken.None).GetAwaiter().GetResult() &&
                    backend.CallCount == 1 && samples == 0,
                    "经授权写入只提交真实后端，不伪造设备回读或直接修改MDS");
                provider.Stop();
                Reject<InvalidOperationException>(() => provider.WriteAsync("valve", false,
                    "authorized", CancellationToken.None).GetAwaiter().GetResult());
            }
        }

        private static void IndustrialDeviceVariableAdapterSmoke()
        {
            PADDeviceVariableCatalog catalog = new PADDeviceVariableCatalog("legacy-plc");
            catalog.Register(new PADDeviceVariableDefinition("pressure-1", "LeakTester",
                "Pressure", typeof(double), "MPa"));
            catalog.Register(new PADDeviceVariableDefinition("ready-1", "LeakTester",
                "Ready", typeof(bool)));
            Reject<InvalidOperationException>(() => catalog.Register(
                new PADDeviceVariableDefinition("other", "LeakTester", "Pressure", typeof(double))));
            MDSVariableTable table = new MDSVariableTable("Industrial");
            catalog.InstallDefinitions(table);
            Check(table.Definitions().Length == 2, "工业目录只生成正式MDS定义");
            MDSVariableRegistry registry = new MDSVariableRegistry();
            registry.Register(table);
            PADDeviceVariableProvider provider = new PADDeviceVariableProvider(catalog);
            using (MDSVariableProviderHost host = new MDSVariableProviderHost(registry))
            {
                host.Register(provider);
                catalog.Bind(host, "Industrial");
                host.Start();
                Check(provider.Emit("pressure-1", 0.18) &&
                    table.Get<double>("LeakTester.Pressure") == 0.18, "真实Provider注入同一变量");
                Reject<InvalidCastException>(() => provider.Emit("pressure-1", "incorrect"));
                Check(!provider.Emit("unknown", 0.2), "不得自动创建未知设备点");
                PADLegacyVariableManagerBridge bridge = new PADLegacyVariableManagerBridge(
                    catalog, provider, def => def.StableId == "pressure-1" ? (object)0.32 : true);
                Check(bridge.AcceptChanges(new[] {
                    new PADLegacyVariableChange("LeakTester", "Pressure"),
                    new PADLegacyVariableChange("LeakTester", "Pressure"),
                    new PADLegacyVariableChange("LeakTester", "Ready") }) == 2 &&
                    table.Get<double>("LeakTester.Pressure") == 0.32 &&
                    table.Get<bool>("LeakTester.Ready"), "旧批量事件按稳定ID合并最新读数");
                host.Stop();
                Check(!provider.Emit("pressure-1", 0.64) &&
                    table.Get<double>("LeakTester.Pressure") == 0.32,
                    "Provider停止不能继续修改正式Owner");
            }
        }

        private static void VariableRestoreSmoke()
        {
            MDSVariableTable original = new MDSVariableTable("recovery");
            original.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "MPa", true, 0.0, "stable-pressure"));
            original.Define(new MDSVariableDefinition("Ready", typeof(bool),
                null, true, false, "stable-ready"));
            original.Update<double>("Pressure", 0.12);
            original.Update<double>("Pressure", 0.14);
            original.Update<bool>("Ready", true);
            MDSVariableStoreSnapshot saved = original.ExportSnapshot();
            MDSVariableTable restored = new MDSVariableTable("recovery");
            restored.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "MPa", true, 0.0, "stable-pressure"));
            restored.Define(new MDSVariableDefinition("Ready", typeof(bool),
                null, true, false, "stable-ready"));
            restored.RestoreSnapshot(saved);
            MDSVariableSnapshot<double> value = restored.Resolve<double>("Pressure").Snapshot();
            Check(value.CurrentValue == 0.14 && value.HasPreviousValue &&
                value.PreviousValue == 0.12 && value.Revision == 2 &&
                restored.Get<bool>("Ready"), "恢复应保持前值/当前值/版本和独立变量");
            Reject<InvalidOperationException>(() => restored.RestoreSnapshot(saved));
            MDSVariableTable incompatible = new MDSVariableTable("recovery");
            incompatible.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "MPa", true, 0.0, "wrong-id"));
            incompatible.Define(new MDSVariableDefinition("Ready", typeof(bool),
                null, true, false, "stable-ready"));
            Reject<InvalidOperationException>(() => incompatible.RestoreSnapshot(saved));
            Check(!incompatible.Resolve<bool>("Ready").Snapshot().HasValue &&
                incompatible.Resolve<double>("Pressure").Snapshot().Revision == 0,
                "校验失败不能半恢复");
            MDSVariableTable wrongUnit = new MDSVariableTable("recovery");
            wrongUnit.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "Pa", true, 0.0, "stable-pressure"));
            wrongUnit.Define(new MDSVariableDefinition("Ready", typeof(bool),
                null, true, false, "stable-ready"));
            Reject<InvalidOperationException>(() => wrongUnit.RestoreSnapshot(saved));
            Check(!wrongUnit.Resolve<double>("Pressure").Snapshot().HasValue,
                "即使值类型相同，也不得把不同单位的历史快照恢复到新定义");
        }

        private static void VariableOutOfOrderSmoke()
        {
            using (MDSVariableChangeDispatcher dispatcher = new MDSVariableChangeDispatcher(16))
            {
                int delivered = 0;
                using (dispatcher.Subscribe(batch => delivered += batch.Count))
                {
                    Action<long, long> emit = (generation, revision) =>
                        dispatcher.Enqueue(new MDSVariableChange("ID", "Pressure", revision, 0, true,
                            MDSVariableQuality.Good, "test", DateTime.UtcNow, revision,
                            MDSVariableChangeKind.Value, generation));
                    emit(1, 3);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && delivered == 1,
                        "第一代最高Revision交付");
                    emit(1, 2); // 队列早已排空，仍应拒绝过期回调。
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && delivered == 1,
                        "旧Revision不能在已派发后重入");
                    emit(2, 1); // 同一稳定VariableId，但运行对象已重建。
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && delivered == 2,
                        "新世代可重新从Revision 1开始");
                    emit(1, 99);
                    Check(dispatcher.Drain(TimeSpan.FromSeconds(5)) && delivered == 2 &&
                        dispatcher.Metrics().StaleRejected == 2,
                        "已卸载世代的迟到高Revision也不可污染新状态");
                }
            }
        }

        private static void VariableInspectableSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("inspect");
            table.Define(new MDSVariableDefinition("Counter", typeof(int), null, true, 5, "C"));
            IMDSVariableSnapshot empty = table.Inspect("Counter");
            Check(!empty.HasValue && empty.HasDefaultValue &&
                (int)empty.DefaultObject == 5 && empty.ValueType == typeof(int),
                "通用观察必须区分默认值与未采样的运行值");
            table.Update<int>("Counter", 9);
            IMDSVariableSnapshot ready = table.Inspect("Counter");
            Check((int)ready.CurrentObject == 9 && !ready.HasPreviousValue &&
                ready.Quality == MDSVariableQuality.Good && ready.VariableId == "C",
                "类型无关观察必须返回唯一Owner的只读快照");
        }

        // 本专项隔离变量测试；默认全架构回归仍完整执行。
        private static void VariableAccessPolicySmoke()
        {
            MDSVariableAccessPolicy rule = new MDSVariableAccessPolicy(
                new[] { "Operator" }, new[] { "PLC-1" }, true);
            MDSVariableTable table = new MDSVariableTable("AccessScope");
            table.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "MPa", true, 0.0, "P1", rule));
            table.Define(new MDSVariableDefinition("Open", typeof(bool),
                null, true, false, "V1"));
            Reject<UnauthorizedAccessException>(() => table.Update<double>("Pressure", 0.25));
            Reject<UnauthorizedAccessException>(() => table.UpdateBatch(
                new MDSVariableBatchItem("Open", true),
                new MDSVariableBatchItem("Pressure", 0.25)));
            Check(!table.Resolve<bool>("Open").Snapshot().HasValue,
                "批量无权写入不得半提交其他变量");
            MDSVariableUpdateResult result = table.Resolve<double>("Pressure").Update(0.3,
                new MDSVariableUpdateOptions("operator-panel",
                    MDSVariableQuality.Good, null, "Operator"));
            Check(result.Revision == 1 && table.Get<double>("Pressure") == 0.3,
                "具备明确Principal的操作员可按策略更新");
            table.UpdateBatch(new MDSVariableBatchItem("Pressure", 0.35,
                new MDSVariableUpdateOptions("operator-panel",
                    MDSVariableQuality.Good, 1, "Operator")));
            Check(table.Get<double>("Pressure") == 0.35,
                "批量与普通更新统一执行同一权限契约");
            MDSVariableAccessPolicy a = new MDSVariableAccessPolicy(new[] { "Operator" },
                new[] { "PLC-1" }, true);
            Check(rule.EquivalentTo(a) &&
                !rule.EquivalentTo(new MDSVariableAccessPolicy(new[] { "Guest" })),
                "访问策略签名应支持快照Schema安全比对");
            MDSVariableStoreSnapshot saved = table.ExportSnapshot();
            MDSVariableTable changedRule = new MDSVariableTable("AccessScope");
            changedRule.Define(new MDSVariableDefinition("Pressure", typeof(double),
                "MPa", true, 0.0, "P1",
                new MDSVariableAccessPolicy(new[] { "Guest" })));
            changedRule.Define(new MDSVariableDefinition("Open", typeof(bool),
                null, true, false, "V1"));
            Reject<InvalidOperationException>(() => changedRule.RestoreSnapshot(saved));
            Check(!changedRule.Resolve<double>("Pressure").Snapshot().HasValue,
                "访问策略收紧/改变后不能用旧快照绕过新Schema");
        }

        private sealed class FaultyStopVariableProvider : IMDSVariableProvider
        {
            public FaultyStopVariableProvider(string id, bool rejectStop)
            { ProviderId = id; RejectStop = rejectStop; }
            public string ProviderId { get; private set; }
            public bool RejectStop { get; private set; }
            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }
            public void Start(Action<MDSVariableProviderSample> publish) { }
            public void Stop()
            {
                StopCount++;
                if (RejectStop) throw new InvalidOperationException("simulated-provider-stop-failure");
            }
            public void Dispose() { DisposeCount++; }
        }
        private static void VariableProviderFailureIsolationSmoke()
        {
            MDSVariableRegistry registry = new MDSVariableRegistry();
            FaultyStopVariableProvider a = new FaultyStopVariableProvider("bad", true);
            FaultyStopVariableProvider b = new FaultyStopVariableProvider("good", false);
            MDSVariableProviderHost host = new MDSVariableProviderHost(registry);
            host.Register(a); host.Register(b);
            host.Start();
            Reject<AggregateException>(() => host.Stop());
            Check(!host.Health().Running && a.StopCount == 1 && b.StopCount == 1,
                "单个Provider停止抛出异常仍应停止全部Provider并失效旧世代");
            host.Dispose();
            Check(a.DisposeCount == 1 && b.DisposeCount == 1,
                "宿主释放必须遍历全部Provider而非遇异常提前退出");
            Reject<ObjectDisposedException>(() => host.Descriptors());
        }

        private static void VariableReliableBatchQueueSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("reliable");
            table.Define(new MDSVariableDefinition("Count", typeof(int)));
            using (MDSVariableReliableChangeJournal queue =
                new MDSVariableReliableChangeJournal(table, 1))
            {
                MDSVariableBatchResult first = table.UpdateBatch(new MDSVariableBatchItem("Count", 1));
                Check(first.NotificationsSucceeded && first.CommittedChanges != null &&
                    queue.PendingCount == 1 && queue.ReadPending()[0].BatchSequence == 1,
                    "已接受批次在Ack之前始终保留并支持重复读取");
                Check(queue.ReadPending()[0].BatchSequence == 1,
                    "未确认批次可重放，读取不移除");
                MDSVariableBatchResult second = table.UpdateBatch(new MDSVariableBatchItem("Count", 2));
                Check(!second.NotificationsSucceeded &&
                    second.NotificationFailures.OfType<MDSVariableReliableBackpressureException>().Any() &&
                    second.CommittedChanges != null && queue.PendingCount == 1 &&
                    table.Get<int>("Count") == 2,
                    "满载明确背压，变量已提交不可假装回滚，须回传补投快照");
                Check(!queue.TryAppend(second.CommittedChanges) && queue.Rejected == 2,
                    "满载补投必须明确拒绝而不能覆盖未确认批次");
                Check(queue.Acknowledge(1) &&
                    queue.TryAppend(second.CommittedChanges) &&
                    queue.TryAppend(second.CommittedChanges) &&
                    queue.ReadPending().Length == 1 &&
                    queue.ReadPending()[0].BatchSequence == 2,
                    "Ack释放容量、补投幂等且不静默重复");
                Check(queue.Acknowledge(2) && queue.PendingCount == 0 &&
                    queue.LastConfirmedSequence == 2, "按连续批次确认并释放队列");
                Check(queue.TryAppend(first.CommittedChanges) && queue.PendingCount == 0,
                    "已确认批次重发不得重新入队");
            }
        }

        private static void VariableReliableGapSmoke()
        {
            MDSVariableTable table = new MDSVariableTable("reliable-gap");
            table.Define(new MDSVariableDefinition("Value", typeof(int)));
            // 先提交，再创建Journal：起始序列应是当前Scope已提交批次的水位。
            table.UpdateBatch(new MDSVariableBatch("reliable-gap",
                new[] { new MDSVariableBatchItem("Value", 1) }, "preexisting"));
            using (MDSVariableReliableChangeJournal queue =
                new MDSVariableReliableChangeJournal(table, 1))
            {
                Check(queue.LastConfirmedSequence == 1 && queue.ExpectedNextSequence == 2,
                    "运行中热插入必须从表的当前提交水位开始");
                MDSVariableBatchResult second = table.UpdateBatch(new MDSVariableBatch(
                    "reliable-gap", new[] { new MDSVariableBatchItem("Value", 2) }, "second"));
                MDSVariableBatchResult third = table.UpdateBatch(new MDSVariableBatch(
                    "reliable-gap", new[] { new MDSVariableBatchItem("Value", 3) }, "third"));
                Check(second.NotificationsSucceeded && !third.NotificationsSucceeded &&
                    third.NotificationFailures.OfType<MDSVariableReliableBackpressureException>().Any(),
                    "容量满时必须明确拒绝第三批并保留可补投快照");
                Check(queue.Acknowledge(2) && queue.ExpectedNextSequence == 3,
                    "Ack释放容量后下一批必须是被拒绝的第三批");
                MDSVariableBatchResult fourth = table.UpdateBatch(new MDSVariableBatch(
                    "reliable-gap", new[] { new MDSVariableBatchItem("Value", 4) }, "fourth"));
                Check(!fourth.NotificationsSucceeded &&
                    fourth.NotificationFailures.OfType<MDSVariableReliableSequenceGapException>().Any() &&
                    queue.PendingCount == 0 && queue.GapRejected == 1,
                    "不能先接受第四批而丢失第三批");
                Check(!queue.TryAppend(fourth.CommittedChanges) &&
                    queue.TryAppend(third.CommittedChanges) &&
                    queue.ReadPending()[0].BatchSequence == 3 &&
                    queue.Acknowledge(3) &&
                    queue.TryAppend(fourth.CommittedChanges) &&
                    queue.ReadPending()[0].BatchSequence == 4 &&
                    queue.Acknowledge(4),
                    "必须先补投缺口、按序确认，之后才可恢复后续批次");
                Check(queue.LastConfirmedSequence == 4 && queue.PendingCount == 0,
                    "未确认事件不能被跳过");
            }
        }

        public static void RunVariableSuite()
        {
            VariableReliableGapSmoke();
            VariableReliableBatchQueueSmoke();
            IndustrialDeviceVariableWriteSmoke();
            VariableProviderFailureIsolationSmoke();
            VariableAccessPolicySmoke();
            VariableInspectableSmoke();
            VariableOutOfOrderSmoke();
            VariableRestoreSmoke();
            IndustrialDeviceVariableAdapterSmoke();
            VariableSubscriptionFilterSmoke();
            VariableBatchSmoke();
            VariableMessageBridgeSmoke();
            VariableScopeSmoke();
            VariableCasSmoke();
            VariableProviderSmoke();
            VariableBoundAccessSmoke();
            VariableDispatcherSmoke();
            VariableTreeSmoke();
            VariableObjectSmoke();
            Console.WriteLine("PASS MDS V1.2 variable suite");
        }

        public static void Run()
        {
            RunVariableSuite();
            MFMModuleComposition module = new MFMModuleComposition("PLC01", new[]
            {
                new MFMComponentReference(MFMComponentRole.Channel, "TCP01", true),
                new MFMComponentReference(MFMComponentRole.Protocol, "MOD01")
            });
            Check(module.References.Count == 2 && module.Find(MFMComponentRole.Channel).Count == 1,
                "Module component references");
            Reject<ArgumentException>(() => new MFMModuleComposition("PLC01",
                new[] { new MFMComponentReference(MFMComponentRole.Channel, "X"),
                    new MFMComponentReference(MFMComponentRole.Channel, "X") }));

            FTRChannelConfiguration channel = new FTRTcpClientChannelConfiguration {
                Id = "tcp01", Kind = FTRChannelKind.TcpClient, Host = "127.0.0.1", Port = 502
            };
            channel.Validate();
            FTRChannelTypeDescriptor tcp = channel.DescribeType();
            Check(tcp.IO == FTRIOCapabilities.ByteStream &&
                  tcp.Topology == FTRChannelTopology.SinglePeer, "Channel metadata");
            Reject<ArgumentException>(() => new FTRTcpClientChannelConfiguration {
                Id = "bad", Kind = FTRChannelKind.TcpClient, Port = 0 }.Validate());
            Reject<ArgumentException>(() => new FTRVirtualChannelConfiguration {
                QueueCapacity = 0 }.Validate());

            FTRChannelTypeDescriptor tcpServer = new FTRTcpServerChannelConfiguration
                { Kind = FTRChannelKind.TcpServer, Port = 9000 }.DescribeType();
            FTRChannelTypeDescriptor udpServer = new FTRUdpServerChannelConfiguration
                { Kind = FTRChannelKind.UdpServer, Port = 9001 }.DescribeType();
            Check(tcpServer.Topology == FTRChannelTopology.MultiPeer &&
                  (tcpServer.IO & FTRIOCapabilities.Listener) != 0 &&
                  udpServer.Topology == FTRChannelTopology.MultiPeer &&
                  udpServer.IO == FTRIOCapabilities.Datagram,
                "TCP/UDP Server manage multiple peers inside one parent Channel");
            FTRChannelTypeDescriptor serial = new FTRSerialChannelConfiguration
                { Kind = FTRChannelKind.Serial, PortName = "COM5" }.DescribeType();
            Check(serial.Topology == FTRChannelTopology.SharedBus,
                "RS-485 shares a Serial Channel, stations belong to Protocol points");

            FTRSubChannelDirectory directory = new FTRSubChannelDirectory("server01");
            FTRSubChannelAddress first = directory.Register("peer01", "127.0.0.1:9000").Address;
            Check(directory.FindSubChannel(first) != null && directory.SnapshotSubChannels().Length == 1,
                "SubChannel registration");
            Reject<InvalidOperationException>(() => directory.Register("peer01", "duplicate"));
            Check(directory.Remove(first), "SubChannel removal");
            FTRSubChannelAddress next = directory.Register("peer01", "127.0.0.1:9001").Address;
            Check(next.Generation > first.Generation && directory.FindSubChannel(first) == null &&
                !directory.Remove(first) && directory.FindSubChannel(next) != null, "Stale generation must not route");
            byte[] input = { 1, 2, 3 };
            FTRChannelReceiveContext received = new FTRChannelReceiveContext(next, input);
            input[0] = 99;
            Check(received.Data[0] == 1, "Receive context input copy");
            byte[] output = received.Data; output[0] = 8;
            Check(received.Data[0] == 1, "Receive context output copy");

            MPAProtocolDescriptor modbus = new MPAProtocolDescriptor("modbus.tcp", "Modbus TCP", 1,
                MPAProtocolCapabilities.Interpret | MPAProtocolCapabilities.Read |
                MPAProtocolCapabilities.Write | MPAProtocolCapabilities.RequestResponse,
                FTRIOCapabilities.ByteStream);
            MPAProtocolConfiguration binding = new MPAProtocolConfiguration("mod01", "modbus.tcp");
            binding.ValidateCompatibility(modbus, tcp);
            Reject<ArgumentException>(() => binding.ValidateCompatibility(modbus, null));
            Reject<ArgumentException>(() => new MPAProtocolConfiguration {
                Id = "bad", Kind = MPAProtocolKind.ModbusTcpDraft,
                ProcessingMode = MPAProtocolProcessingMode.Serial,
                MaxConcurrency = 2 }.Validate());
            MPAProtocolSession session = new MPAProtocolSession(binding, new FTRSubChannelAddress("tcp01"));
            Check(session.State == MPAProtocolSessionState.Created, "Session created");
            session.Start(); Check(session.State == MPAProtocolSessionState.Active, "Session start");
            Reject<InvalidOperationException>(() => session.Start());
            Task<object> pendingResponse = session.WaitResponseAsync("modbus-request-01",
                TimeSpan.FromSeconds(1), CancellationToken.None);
            Check(session.PendingRequests == 1 &&
                session.CompleteResponse("modbus-request-01", new byte[] { 3, 2, 0, 5 }) &&
                ((byte[])pendingResponse.GetAwaiter().GetResult()).SequenceEqual(new byte[] { 3, 2, 0, 5 }) &&
                session.PendingRequests == 0, "规约会话 CorrelationId 关联并返回完整响应");
            Check(!session.CompleteResponse("unknown", null), "未知请求不能伪造成功");
            Task<object> timeoutResponse = session.WaitResponseAsync("short-timeout",
                TimeSpan.FromMilliseconds(1), CancellationToken.None);
            Reject<TimeoutException>(() => timeoutResponse.GetAwaiter().GetResult());
            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                Task<object> cancelledResponse = session.WaitResponseAsync("cancel-request",
                    TimeSpan.FromSeconds(10), cancelled.Token);
                cancelled.Cancel();
                Reject<OperationCanceledException>(() => cancelledResponse.GetAwaiter().GetResult());
            }
            session.Stop(); session.Dispose();
            Reject<ObjectDisposedException>(() => session.Start());
            Reject<ArgumentException>(() => new MPAProtocolOperationResult(MPAProtocolOperationStage.Failed));
            Check(!new MPAProtocolOperationResult(MPAProtocolOperationStage.Sent).IsCompleted,
                "Sent must not be treated as completed");

            MPAPointTable pointTable = new MPAPointTable();
            MPAPointDefinition point = new MPAPointDefinition("temperature", "holding:100", typeof(ushort))
                { CanWrite = true, StationAddress = "1" };
            pointTable.Add(point);
            Reject<ArgumentException>(() => pointTable.Add(
                new MPAPointDefinition("temperature", "holding:200", typeof(ushort))));
            MCNConnectorConfiguration connector = new MCNConnectorConfiguration {
                DisplayName = "PLC", Channel = channel,
                Protocol = new MPAProtocolConfiguration("mod01", "modbus.tcp") {
                    Points = new List<MPAPointDefinition> { point }
                }
            };
            connector.Validate();
            Check(connector.Channel == channel && connector.Protocol.Points.Count == 1,
                "Connector has one Channel and one Protocol");
            connector.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "modbus.tcp", modbus } });
            Reject<ArgumentException>(() => new MCNConnectorConfiguration { Channel = null }.Validate());
            Reject<ArgumentException>(() => new MCNConnectorConfiguration { Protocol = null }.Validate());
            MCNConnectorConfiguration empty = new MCNConnectorConfiguration();
            empty.ValidateProtocolCapabilities(new Dictionary<string, MPAProtocolDescriptor>
                { { "empty", new MPAEmptyProtocol().Descriptor } });

            MPAPointDefinition stationOne = new MPAPointDefinition("reg1", "holding:1", typeof(ushort))
                { StationAddress = "1" };
            MPAPointDefinition stationTwo = new MPAPointDefinition("reg2", "holding:1", typeof(ushort))
                { StationAddress = "2" };
            MCNConnectorConfiguration multiStation = new MCNConnectorConfiguration {
                Channel = new FTRSerialChannelConfiguration { Id = "bus", Kind = FTRChannelKind.Serial, PortName = "COM3" },
                Protocol = new MPAProtocolConfiguration("rtu", "modbus.rtu") {
                    Points = new List<MPAPointDefinition> { stationOne, stationTwo }
                }
            };
            multiStation.Validate();
            Check(multiStation.Protocol.Points.Count == 2 &&
                  multiStation.Protocol.Points[0].StationAddress != multiStation.Protocol.Points[1].StationAddress,
                  "Multiple Modbus station addresses belong to a single Protocol PointTable");
            stationTwo.StationAddress = "256";
            Reject<ArgumentException>(() => multiStation.Validate());
            stationTwo.StationAddress = "2";

            // 实际纯传输 Virtual 不解释长度帧：由独立 Protocol 解析拆分数据。
            FTRVirtualChannel[] raw = FTRVirtualChannel.CreatePair("raw-tx", "raw-rx");
            MPALengthFrameProtocol framed = new MPALengthFrameProtocol();
            List<byte[]> parsed = new List<byte[]>();
            raw[1].Received += receivedChunk => {
                foreach (byte[] payload in framed.ProcessReceived(receivedChunk,
                    new MPAProtocolContext("length", "session")))
                    parsed.Add(payload);
            };
            raw[0].Open(); raw[1].Open();
            byte[] encoded = framed.PrepareSend(new byte[] { 10, 20, 30 }, null);
            raw[0].SendAsync(encoded.Take(3).ToArray(),
                new FTRSubChannelAddress("raw-tx"), CancellationToken.None).GetAwaiter().GetResult();
            raw[0].SendAsync(encoded.Skip(3).ToArray(),
                new FTRSubChannelAddress("raw-tx"), CancellationToken.None).GetAwaiter().GetResult();
            Check(parsed.Count == 1 && parsed[0].SequenceEqual(new byte[] { 10, 20, 30 }) &&
                  raw[1].ReadFrames().All(x => x.DecodedPayload == null),
                "Protocol owns frame parsing; Channel only transports raw chunks");
            raw[0].Close(); raw[1].Close();

            // Connector 实例与纯传输 Channel / Protocol 的实际入站闭环。
            MCNConnectorConfiguration streamConfig = new MCNConnectorConfiguration {
                DisplayName = "分帧连接",
                Channel = new FTRVirtualChannelConfiguration { Id = "raw-tx", Kind = FTRChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Kind = MPAProtocolKind.LengthFrame }
            };
            MCNConnectorModule streamModule = new MCNConnectorModule(
                streamConfig.Describe("stream", MFMModuleKind.Connector), streamConfig,
                raw[0], new MPALengthFrameProtocol());
            List<byte[]> assembled = new List<byte[]>();
            FTRSubChannelAddress lastSource = null;
            streamModule.PayloadReceived += x => assembled.Add(x);
            streamModule.PayloadReceivedFrom += (source, x) => lastSource = source;
            raw[1].Open();
            streamModule.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            byte[] dataFrame = new MPALengthFrameProtocol().PrepareSend(new byte[] { 7, 8 }, null);
            raw[1].SendAsync(dataFrame, new FTRSubChannelAddress("raw-rx"),
                CancellationToken.None).GetAwaiter().GetResult();
            Check(lastSource != null && lastSource.ChannelId == "raw-tx",
                "Connector inbound event preserves Channel/SubChannel route");
            Check(assembled.Count == 1 && assembled[0].SequenceEqual(new byte[] { 7, 8 }),
                "Connector receives and delegates parsing to Protocol");
            streamModule.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            raw[1].Close();

            FTRVirtualChannel[] emptyPair = FTRVirtualChannel.CreatePair("empty-tx", "empty-rx");
            MCNConnectorConfiguration emptyCfg = new MCNConnectorConfiguration {
                Channel = new FTRVirtualChannelConfiguration { Id = "empty-tx", Kind = FTRChannelKind.Virtual },
                Protocol = new MPAProtocolConfiguration { Kind = MPAProtocolKind.Empty }
            };
            MCNConnectorModule emptyModule = new MCNConnectorModule(
                emptyCfg.Describe("pass", MFMModuleKind.Connector), emptyCfg,
                emptyPair[0], new MPAEmptyProtocol());
            byte[] passthrough = null;
            emptyModule.PayloadReceived += x => passthrough = x;
            emptyPair[1].Open();
            emptyModule.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            emptyPair[1].SendAsync(new byte[] { 3, 4, 5 }, new FTRSubChannelAddress("empty-rx"),
                CancellationToken.None).GetAwaiter().GetResult();
            Check(passthrough != null && passthrough.SequenceEqual(new byte[] { 3, 4, 5 }),
                "Empty Protocol transparently passes data, not an Empty Channel");
            emptyModule.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            emptyPair[1].Close();
            ModbusTcpTransactionSmoke();
            // Modbus TCP/RTU 的实际帧校验：TCP 半包，RTU 站号和 CRC16。
            // 按代次清理半包：旧客户端遗留字节不能污染新的 ProtocolSession。
            MPALengthFrameProtocol cleanupParser = new MPALengthFrameProtocol();
            byte[] wrapped = cleanupParser.PrepareSend(new byte[] { 9, 8, 7 }, null);
            FTRSubChannelAddress former = new FTRSubChannelAddress("server", "peer", 1);
            Check(cleanupParser.ProcessReceived(new FTRChannelReceiveContext(former,
                wrapped.Take(2).ToArray()), new MPAProtocolContext("length", "one")).Count == 0,
                "单个子通道半包保留在规约解析状态");
            ((IMPAProtocolSessionCleanup)cleanupParser).Reset(former);
            Check(cleanupParser.ProcessReceived(new FTRChannelReceiveContext(former,
                wrapped), new MPAProtocolContext("length", "one")).Count == 1,
                "断开后清理半包，再收到整帧必须独立解析");
            ((IMPAProtocolSessionCleanup)cleanupParser).ResetAll();
            MPAProtocolContext mbCtx = new MPAProtocolContext("modbus", "session",
                new FTRSubChannelAddress("raw-rx"), protocolAddress: new MPAProtocolAddress("3"));
            MPAModbusTcpProtocol mbTcp = new MPAModbusTcpProtocol();
            byte[] tcpResponse = mbTcp.PrepareSend(new byte[] { 3, 2, 0, 5 }, mbCtx);
            byte[] headHalf = tcpResponse.Take(5).ToArray();
            byte[] tailHalf = tcpResponse.Skip(5).ToArray();
            Check(mbTcp.ProcessReceived(new FTRChannelReceiveContext(
                new FTRSubChannelAddress("raw-rx"), headHalf), mbCtx).Count == 0,
                "Modbus TCP 半包暂不输出");
            IReadOnlyList<byte[]> mbTcpFrames = mbTcp.ProcessReceived(
                new FTRChannelReceiveContext(new FTRSubChannelAddress("raw-rx"), tailHalf), mbCtx);
            Check(mbTcpFrames.Count == 1 && mbTcpFrames[0].SequenceEqual(new byte[] { 3, 2, 0, 5 }),
                "Modbus TCP MBAP 粘包解析与读寄存器响应");
            MPAModbusRtuProtocol mbRtu = new MPAModbusRtuProtocol();
            byte[] rtuResponse = mbRtu.PrepareSend(new byte[] { 3, 2, 0, 5 }, mbCtx);
            IReadOnlyList<byte[]> mbRtuFrames = mbRtu.ProcessReceived(
                new FTRChannelReceiveContext(new FTRSubChannelAddress("raw-rx"), rtuResponse), mbCtx);
            Check(mbRtuFrames.Count == 1 && mbRtuFrames[0].SequenceEqual(new byte[] { 3, 2, 0, 5 }),
                "Modbus RTU CRC16/站号/帧处理");
            byte[] badCrc = (byte[])rtuResponse.Clone(); badCrc[badCrc.Length - 1] ^= 1;
            Reject<System.IO.InvalidDataException>(() => mbRtu.ProcessReceived(
                new FTRChannelReceiveContext(new FTRSubChannelAddress("raw-rx"), badCrc), mbCtx));
            MPAProtocolConfiguration collectionConfig = new MPAProtocolConfiguration();
            collectionConfig.Points.Add(new MPAPointDefinition("sensor", "raw:1", typeof(int)));
            MPADataMappingCollection collection = new MPADataMappingCollection(collectionConfig);
            MPADataMapping mapped = new MPADataMapping {
                PointRef = "sensor", TargetRef = "scope/reading",
                TargetKind = MPADataTargetKind.Variable, Operation = MPADataOperation.WriteValue,
                Transform = MPADataTransformKind.Int32
            };
            collection.Add(mapped);
            Check(collection.Find("sensor").Count == 1 &&
                (int)MPADataValueTransform.Apply("42", MPADataTransformKind.Int32) == 42,
                "Mapping 集合与实际转换语义");
            Reject<ArgumentException>(() => collection.Add(new MPADataMapping {
                PointRef = "not-found", TargetRef = "scope/missing" }));
            Check(collection.Remove(mapped), "Mapping 删除与唯一正式配置");

            MDSVariableRegistry registry = new MDSVariableRegistry();
            MDSVariableTable variables = new MDSVariableTable("plc01");
            registry.Register(variables);
            variables.Define(new MDSVariableDefinition("temperature", typeof(double), "C"));
            Check(registry.Find("plc01") == variables, "MDS scope ownership");
            double emptyValue;
            Check(!variables.TryGet<double>("temperature", out emptyValue), "Uninitialized variables must not be successful");
            int changed = 0; variables.VariableChanged += name => { if (name == "temperature") changed++; };
            variables.Update<double>("temperature", 25.5);
            Check(changed == 1 && variables.Get<double>("temperature") == 25.5 &&
                variables.Sample<double>("temperature").HasInitialValue, "MDS update/sample/notification");
            Reject<InvalidCastException>(() => variables.Update<int>("temperature", 25));
            Reject<KeyNotFoundException>(() => variables.Update<int>("missing", 1));
            variables.Define(new MDSVariableDefinition("bytes", typeof(byte[])));
            byte[] mutable = { 5, 6 };
            variables.Update<byte[]>("bytes", mutable);
            mutable[0] = 99;
            byte[] copy = variables.Get<byte[]>("bytes"); copy[1] = 88;
            Check(variables.Get<byte[]>("bytes")[0] == 5 && variables.Get<byte[]>("bytes")[1] == 6,
                "Byte array variable isolation");
            Reject<ArgumentException>(() => registry.Register(variables));

            MPAProtocolConfiguration mapProtocol = new MPAProtocolConfiguration {
                Kind = MPAProtocolKind.Empty,
                Points = new List<MPAPointDefinition> {
                    new MPAPointDefinition("speed", "speed", typeof(double)) { CanWrite = true }
                },
                Mappings = new List<MPADataMapping> {
                    new MPADataMapping { PointRef = "speed", TargetKind = MPADataTargetKind.Variable,
                        TargetRef = "plc01/temperature", Operation = MPADataOperation.WriteValue,
                        Direction = MPADataDirection.Inbound },
                    new MPADataMapping { PointRef = "speed", TargetKind = MPADataTargetKind.Variable,
                        TargetRef = "plc01/temperature", Operation = MPADataOperation.ReadValue,
                        Direction = MPADataDirection.Outbound }
                }
            };
            MPADataMappingExecutor mapping = new MPADataMappingExecutor(
                new MDSProtocolDataTargetAccess(registry));
            mapping.ApplyInboundAsync(mapProtocol, "speed", 55.0, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(variables.Get<double>("temperature") == 55.0, "Point mapped to real MDS variable");
            MPAProtocolContext mappingContext = new MPAProtocolContext("mapping", "m-session",
                configuration: mapProtocol, dataAccess: new MDSProtocolDataTargetAccess(registry));
            MPAEmptyProtocol emptyProtocol = new MPAEmptyProtocol();
            MPAProtocolOperationResult mappedRead = emptyProtocol.ReadAsync(
                new MPAProtocolOperationRequest(MPAProtocolOperationKind.Read,
                    new MPAProtocolAddress("speed")), mappingContext, CancellationToken.None)
                .GetAwaiter().GetResult();
            Check(mappedRead.IsCompleted && (double)mappedRead.Value == 55.0,
                "IMPAProtocol.ReadAsync 读取真实 MDS 数据");
            MPAProtocolOperationResult mappedWrite = emptyProtocol.WriteAsync(
                new MPAProtocolOperationRequest(MPAProtocolOperationKind.Write,
                    new MPAProtocolAddress("speed"), 57.0),
                mappingContext, CancellationToken.None).GetAwaiter().GetResult();
            Check(mappedWrite.IsCompleted && variables.Get<double>("temperature") == 57.0,
                "IMPAProtocol.WriteAsync 写入真实 MDS 数据");
            object outbound = mapping.ReadOutboundAsync(mapProtocol, "speed", CancellationToken.None)
                .GetAwaiter().GetResult();
            Check((double)outbound == 57.0, "Outbound reads actual MDS variable");
            mapProtocol.Mappings.Add(new MPADataMapping {
                PointRef = "speed", TargetKind = MPADataTargetKind.Message,
                TargetRef = "Alarm", Operation = MPADataOperation.PublishMessage
            });
            Reject<NotSupportedException>(() => mapping.ApplyInboundAsync(mapProtocol, "speed", 56.0,
                CancellationToken.None).GetAwaiter().GetResult());

            using (MDSDataHeap heap = new MDSDataHeap())
            {
                MPAProtocolConfiguration heapProtocol = new MPAProtocolConfiguration {
                    Points = new List<MPAPointDefinition> { new MPAPointDefinition {
                        Name = "heapValue", Address = "heap:1", CanWrite = true
                    }},
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "heapValue", TargetKind = MPADataTargetKind.DataHeap,
                            TargetRef = "Run.Quality", Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "heapValue", TargetKind = MPADataTargetKind.DataHeap,
                            TargetRef = "Run.Quality", Direction = MPADataDirection.Outbound,
                            Operation = MPADataOperation.ReadValue }
                    }
                };
                MPADataMappingExecutor heapMap = new MPADataMappingExecutor(
                    new MDSProtocolDataTargetAccess(heap: heap));
                heapMap.ApplyInboundAsync(heapProtocol, "heapValue", 42, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(heap.Get<int>("Run.Quality") == 42 &&
                    (int)heapMap.ReadOutboundAsync(heapProtocol, "heapValue", CancellationToken.None)
                       .GetAwaiter().GetResult() == 42, "MDS Heap mapping actual read/write");
            }
            using (CxVisual.Mechanism.Data.Execution.MDEExecutionDataScope scope =
                CxVisual.Mechanism.Data.Execution.MDEExecutionDataScope.CreateRoot("mapping-root"))
            {
                MDSDataContext frameContext = new MDSDataContext(scope);
                MPAProtocolConfiguration stackProtocol = new MPAProtocolConfiguration {
                    Points = new List<MPAPointDefinition> { new MPAPointDefinition {
                        Name = "stackValue", Address = "stack:1", CanWrite = true
                    }},
                    Mappings = new List<MPADataMapping> {
                        new MPADataMapping { PointRef = "stackValue", TargetKind = MPADataTargetKind.DataStack,
                            TargetRef = "Node.Speed", Operation = MPADataOperation.WriteValue },
                        new MPADataMapping { PointRef = "stackValue", TargetKind = MPADataTargetKind.DataStack,
                            TargetRef = "Node.Speed", Direction = MPADataDirection.Outbound,
                            Operation = MPADataOperation.ReadValue }
                    }
                };
                MPADataMappingExecutor stackMap = new MPADataMappingExecutor(
                    new MDSProtocolDataTargetAccess(frame: frameContext));
                stackMap.ApplyInboundAsync(stackProtocol, "stackValue", 17, CancellationToken.None)
                    .GetAwaiter().GetResult();
                Check(frameContext.Get<int>("Node.Speed") == 17 &&
                    (int)stackMap.ReadOutboundAsync(stackProtocol, "stackValue", CancellationToken.None)
                       .GetAwaiter().GetResult() == 17, "MDS DataStack frame mapping actual read/write");
            }

            // 外部接入报文绝不能跳过正式命令授权。
            bool invokedUnsafeDeviceCommand = false;
            MDSProtocolDataTargetAccess deniedCommands = new MDSProtocolDataTargetAccess(
                invoke: (commandId, value, ct) => {
                    invokedUnsafeDeviceCommand = true;
                    return Task.CompletedTask;
                },
                authorizeCommand: (commandId, value) => false);
            Reject<UnauthorizedAccessException>(() => deniedCommands.InvokeAsync(
                "Device.Stop", 1, CancellationToken.None).GetAwaiter().GetResult());
            Check(!invokedUnsafeDeviceCommand,
                "未授权命令不得调用真实 Owner");
            MDSProtocolDataTargetAccess noCommands = new MDSProtocolDataTargetAccess();
            Reject<NotSupportedException>(() => noCommands.InvokeAsync(
                "Device.Stop", 1, CancellationToken.None).GetAwaiter().GetResult());

            MFMModuleDescriptor driverDescriptor =
                new MFMModuleDescriptor("camera01", "X-Ray Camera", MFMModuleKind.Driver);
            Check(driverDescriptor.Kind == MFMModuleKind.Driver, "Fifth Driver module kind");
            MFMDriverModuleConfiguration sdk = new MFMDriverModuleConfiguration {
                DisplayName = "采图驱动", DriverTypeId = "vision.xray.sdk",
                Backend = MFMDriverBackendKind.Sdk, DeviceRef = "camera.01"
            };
            sdk.Validate();
            Check(sdk.InternalResourceRefs.Count == 0,
                "SDK driver does not require EmptyChannel or a Connector instance");
            Reject<ArgumentException>(() => new MFMDriverModuleConfiguration
                { DriverTypeId = "" }.Validate());
            Reject<ArgumentException>(() => new MFMDriverModuleConfiguration {
                DriverTypeId = "vision.xray", InternalResourceRefs = new List<string> { "tcp", "tcp" }
            }.Validate());

            MFMModuleDescriptor computeDescriptor = new MFMModuleDescriptor("calc", "Calc", MFMModuleKind.Compute);
            PVCComputeModuleProfile compute = new PVCComputeModuleProfile(computeDescriptor);
            Check(compute.Descriptor.Kind == MFMModuleKind.Compute && new Increment().Execute(7) == 8, "Compute");
            Reject<ArgumentException>(() => new PVCComputeModuleConfiguration { MaxConcurrency = 0 }.Validate());

            MFMModuleDescriptor simulationDescriptor = new MFMModuleDescriptor("sim", "Sim", MFMModuleKind.Simulation);
            CounterModel model = new CounterModel();
            PSMSimulationModule simulation = new PSMSimulationModule(simulationDescriptor,
                new PSMSimulationModuleConfiguration { ModelId = "axis", StepMilliseconds = 1, Seed = 17 }, model);
            Reject<InvalidOperationException>(() => simulation.Advance(TimeSpan.FromMilliseconds(1)));
            simulation.Initialize();
            simulation.Advance(TimeSpan.FromMilliseconds(1));
            Check(model.Count == 18 && simulation.SimulatedTime == TimeSpan.FromMilliseconds(1), "Simulation step");
            simulation.Reset();
            Check(simulation.SimulatedTime == TimeSpan.Zero, "Simulation reset");

            MFMModuleDescriptor serviceDescriptor = new MFMModuleDescriptor("svc", "Svc", MFMModuleKind.Service);
            SimpleService service = new SimpleService(serviceDescriptor);
            service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(service.IsReady && service.Starts == 1, "Service start");
            service.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            Check(!service.IsReady && service.Stops == 1 && service.State == MLCServiceModuleState.Stopped,
                "Service stop");
            Console.WriteLine("FM.FOUNDATION: driver/module/channel/protocol/point/variable/compute/sim/service PASS");
        }
    }
}
