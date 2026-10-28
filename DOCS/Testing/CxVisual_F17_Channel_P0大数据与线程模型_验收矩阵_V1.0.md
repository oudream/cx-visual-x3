# F17 P0 大数据与线程模型验收矩阵 V1.0

2026-10-27。范围为完整Channel目标中的TCP P0增量；其它通道和长期/现场门禁不因本矩阵通过而完成。
对应[完整14方面审计](../Architecture/CxVisual_F17_Channel完整体系_能力审计与实施基线_V1.0.md)、[正式实现与所有权合同](../Implementation/CxVisual_F17_Channel_P0独立接收与有界大数据_实施记录_V1.0.md)。

## 可执行用例与实际判据

`Tests/ArchitectureSmoke/Transport/FTRTcpBulkTests.cs`，`--channel-bulk`；原`--channel-transport`和默认回归也执行短专项。

| 用例 | 执行方法 | 正例与失败判据 |
|---|---|---|
| F17-P0-01 | ConfigRoundtrip | FCM新限制往返、Kind切换保持、非法块大小拒绝 |
| F17-P0-02 | AsyncSendAdmission | 入场前数量/字节预算；拒绝不得调用底层或创建快照；已入场快照不受源数组修改；结算释放额度 |
| F17-P0-03 | DuplexBulk | 双向各40MiB逐字节、顺序和总数；非线程池后台交付、接收线程独立；池与证据预算、完整诊断计数 |
| F17-P0-04 | RetainedBackpressure | 两块131072字节预算；消费者阻塞产生真实背压；反向发送不受接收锁阻塞；不丢1MiB数据；跨Close保留租约、归还与重复Dispose |
| F17-P0-05 | MultiPeerThreadBudget | 一个Server同时12个真实TCP对端、独立SubChannel、精确回送；共享接收/交付线程与有限缓冲 |
| F17-P0-06 | StopAndObserverIsolation | 异常订阅者不吞后继交付；慢回调Close必须超时/Faulted；回调释放后重新Close才结算并释放池 |
| F17-P0-07 | SynchronousOrderAndAsyncSnapshot | 并发两个1MiB同步Send，8191/4001分块不允许交错；异步64KiB发送后修改源数组不污染网络数据 |
| F17-P0-08 | LifecycleAndCallbackClose | 回调不能等待自身关闭；状态观察器异常不破坏状态流；Open的Token取消后仍可收发；显式重连、Dispose幂等及禁止重开 |
| F17-P0-09 | IdleTimeouts | Client读空闲变Faulted并有诊断；Server单Peer读空闲清理而Listener保持Open |
| F17-P0-10 | HalfCloseTailDelivery | 在回调被阻塞时真实Shutdown(Send)，确认EOF通知已排队；Protocol不能提前重置；最终尾包仍能回应然后再移除Peer |
| F17-P0-11 | RunVolumeScale / --channel-bulk-long | 同一连接双向各2147483648字节，不分配整卷数组，逐字节校验；每端池<=262144字节，输出环境、墙钟、CPU、内存、GC、背压和线程 |
| F17-P0-12 | 原ModuleChannelTransportSmokeTests | 原12类装配、Virtual/TCP/UDP/NamedPipe/HTTP/WS/Replay、非法配置/代次、Connector/Modbus实际回环全部保留 |
| F17-P0-13 | TCT.UI.ARCHSMOKE.001 | TestCenter真实隔离Worker检查14个固定CLI预设，保留原11个，3个F17入口可在原测试控制台操作，伪造参数仍拒绝 |

以上是软件可执行路径，不把Serial/RS485、PLC、跨PC、外部网络和硬件ACK验收列为已通过。

## 本地Windows实测

- 完整.NET48 Debug/x64方案编译成功，`UseSharedCompilation=false`用于避免本机编译Server连接等待，不改变语言/目标框架。
- `--channel-transport`实际exit0，输出原`CHANNEL.CONNECTOR: 12 built-in FTR kinds`及`CHANNEL.TRANSPORT: TCP/UDP loopback`成功证据。
- `--channel-bulk`实际exit0；40MiB双向、异步入场、慢消费者、12对端及生命周期正反用例运行。
- `--channel-bulk-long`初测exit0：双向各2GiB，第一轮8827ms，随后一轮7724ms；后一轮CPU33328.1ms、managed delta15904048、working set44195840、GC0=14/GC2=1，两端接收池各262144字节，峰值4块。吞吐包含数据生成和逐字节验证，不是纯网络带宽或产线SLA。
- ArchitectureSmoke默认回归与`--concurrency`已实际运行成功；TestCenter `--run --id TCT.UI.ARCHSMOKE.001 --isolate`产生JUnit：tests=1、failures=0、errors=0。
- 提交前最终复核：默认回归第一次在既有`ExecutionTriggerTests.PeriodicTriggerNeverLaunchesUnboundedFlows`的周期触发时序断言失败；未修改该测试和Runtime，原代码完整重跑exit0，随后`--concurrency`exit0。该波动保留为待观察项，不以重跑覆盖首次失败记录。
- 本机沙箱默认权限拒绝NamedPipe连接与File.Replace，正常Windows权限重跑后真实通过；不为环境权限问题放宽测试或改传输实现。
- 日志位于`Output/Debug/x64/f17-p0-*.log`及`F17-TestCenterProfiles.junit.xml`，不提交生成物。

本地证据对应本轮工作代码；正式源码提交与远端CI必须按最终固定SHA另行核对，不能把以上当作远端通过。

## Windows CI门禁

现有`.github/workflows/f17-channel-windows.yml`保留完整Release/x64 Rebuild、原通道回归和唯一Owner检查，并必须执行双向2GiB，检查实际完成字节标记。Owner检查逐个核对60个现有FTR源文件均有唯一Compile项，不再以过时固定56限制真实增构；旧通道源码不得回流Mechanism。体数据指标日志作为artifact保存。

## 固定源码的远端验收补录（2026-10-28）

- 正式实现提交`edbdefbe7180517af20b98ae655025c705d10df2`；与并行master正常合并后，实际CI源码`549e7ed2be43a32952536b286e8c5ce771461631`。
- [F17 Windows专项40540778043](https://github.com/oudream/cx-visual-x3/actions/runs/40540778043) success；完整Release/x64方案Rebuild、原通道/Connector/OS回环、短专项、60源文件唯一Compile/Owner全部成功。
- 真实CI双向各2147483648字节完成；9450ms，CPU32515.6ms，managed delta14995640，working set40742912，GC0=14/GC2=1，两端接收池各262144字节/峰值4块，含生成与逐字节校验的总吞吐433.43MiB/s；`F17.BULK_VOLUME_PASS`存在。指标是该4逻辑CPU runner的软件样本，不是工业SLA。
- [完整TestCenter Windows 40540777960](https://github.com/oudream/cx-visual-x3/actions/runs/40540777960) success；图元WPF/工程Undo、三维GPU与UIA、默认142用例及Regression的152唯一用例×3=456次并行尝试全部通过。阴性探针中的故意失败由CI逐项核对其失败语义，不计作真实套件意外失败。
- 上述证据只覆盖固定SHA；后续Factory/Provider增量需另按自身源码SHA验收。

## 完整目标尚未验收

24小时及更长持续负载、工业网络丢包/断网、多机网卡与组播环境、真实RS232/485/PLC/采集设备HIL、所有通道独立接收与有效预算、HTTP/WS Server、SharedMemory、Custom Provider、自动重连/心跳/保活及全部TestCenter手动操作仍按完整审计继续。不以2GiB软件样本替代这些门禁。新增TLS/证书/认证暂缓，现有安全保护保留。

## P0-RC-01 恢复事件回调内停止/释放（2026-10-28 补充）

| 用例 | 入口 | 验收要求 | 证据 |
|---|---|---|---|
| F17-P0-RC-01 | `FTRReconnectControllerTests.StopWithinRecoveryCallbacks`，由 `--channel-transport` 调用 | 失败事件内同步 `StopAsync` 不发生自等待死锁；成功事件内同步 `Dispose` 亦不死锁；外部停止实际等待任务资源清理；停止后没有新一轮自动连接 | [F17 Windows #40657016715](https://github.com/oudream/cx-visual-x3/actions/runs/40657016715) success，固定SHA `4d9976ee`，日志 `F17.RECONNECT_PASS ... callback-safe stop` |

涉及 `FTRReconnectController.cs`，未修改 Protocol/Connector/MDS/MRK 或业务执行语义。此专项回归不覆盖强杀进程、24h长稳、现场网络或PLC设备。
