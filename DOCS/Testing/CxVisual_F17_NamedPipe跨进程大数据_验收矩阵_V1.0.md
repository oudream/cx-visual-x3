# F17 NamedPipe跨进程大数据验收矩阵 V1.0

2026-10-28。[实现与线程/所有权合同](../Implementation/CxVisual_F17_NamedPipe独立接收与跨进程大数据_实施验收_V1.0.md)。属于完整Channel体系的增量，不把本矩阵通过当作全部60项完成。

| 用例 | 实际入口 | 正反验收 |
|---|---|---|
| NP-01 | FTRNamedPipeAdmissionTests | 原MaxClients=1饱和拒绝、32轮真实重连和8轮最终Accept关闭保留 |
| NP-02 | CrossProcessDuplex / RunChild | 两个真实PID，双向各8MiB图案逐字节/顺序校验；父子均独立后台接收/交付、池<=256KiB、正确计数，不用整卷数组 |
| NP-03 | MultiplePeersAndIndependentSends | 12个真实Windows对端共用一个观察到的接收线程；慢端8MiB写入已真实开始后，快端仍先完成回应；慢端真实Timeout、另一阻塞写真实取消，Listener继续Open |
| NP-04 | SlowConsumerAndTail | 两块128KiB预算；慢回调StopTimeout/Faulted，Receive已停止而Delivery仍存活；跨Close租约有效且最终归还；已接纳8192尾字节在EOF/PeerRemoved前交付 |
| NP-05 | IdleAndCallbackClose | Server空闲Peer清理不杀Listener；回调自Close立即拒绝；Client在首块数据之前空闲故障时状态回调也不得等待自身 |
| NP-06 | ReconnectSnapshotAndCancellation | Open Token取消后仍收发、显式Reconnect新代次、64KiB异步输入修改不污染网络、已完成Send Token后取消不杀连接、预取消无副作用、真实不存在Pipe连接超时与失败清理 |
| NP-07 | RunVolumeScale / 原--channel-bulk-long | TCP和真实跨进程NamedPipe各自双向各2147483648字节；父子CPU/GC/managed delta/working set、池/背压、聚合校验吞吐输出 |
| NP-08 | FTRReceiveNotificationTests | 正式FIFO终止元数据额度包含执行中的回调；满额入场阻塞可取消；未接纳通知不能执行；排空归还全部额度；该资源测试不模拟硬件通过 |

原`--channel-transport`与默认ArchitectureSmoke运行NP-01～06/08。原`--channel-bulk-long`运行NP-07，不另建CLI测试框架；TestCenter固定预设沿用，子进程角色仅是本测试EXE的受限内部入口。

## 本机实际验证

- 完整Debug/x64方案Build成功，原通道、TCP/UDP/组播、Factory、管道入场及本轮短专项exit0；默认ArchitectureSmoke回归exit0。
- 本轮双向2GiB初测实际exit0，TCP和跨进程NamedPipe均有完成标记；管道总4GiB约17966ms、两端池各262144字节，详细指标见实施记录。终止通知额度和线程存活字段增构后的最终复核单独保留日志，不拿初测冒充最终固定SHA。
- 最终工作代码完整方案Build、默认ArchitectureSmoke、`--concurrency`、TCP与跨进程管道各双向2GiB均exit0；管道11948ms、两端池各262144字节，详见`f17-pipe-final-*.log`。
- 完整默认回归首轮再次暴露旧`MRKExecutionTrigger.StopAsync`仅等Flow而未等登记清理的竞态，已在正式Runtime Owner以`f1e0a3b18c1a181e33acd58425021f281836091d`修复，保留原周期断言、增加64轮结算验证；同提交仅限定最新数据WPF用例的五处Button类型名解除编译遮蔽，真实`--data-lifecycle-ui-smoke`exit0。没有通过重跑、跳过或放宽断言隐藏这两个阻断。
- 日志：`Output/Debug/x64/f17-pipe-thread-*.log`、`f17-pipe-bulk-short.log`、`f17-pipe-volume-long.log`。NamedPipe真实访问需正常Windows权限，未放宽ACL或跳过测试以适应沙箱。

## Windows CI门禁

F17工作流保留完整Release/x64 Rebuild、旧通道回归、逐文件Compile/名称/唯一Owner检查；短专项必须输出管道Admission/Bulk与通知预算标记，长专项必须同时输出TCP和跨进程管道2GiB完成标记，状态码必须0。两端指标沿用F17-volume-stream-measurements artifact。实际固定SHA/run结果待提交后核查补录。

完整TestCenter Windows必须继续运行真实图元/WPF/工程Undo、三维相关验收、默认隔离和并行三轮/历史回放；不能用F17单个headless通过替代。

## 未验证边界

24小时以上持续负载、多机SMB管道、不同Windows版本/权限和网络故障、采集设备与真实RS232/485等HIL、全部专业Provider与完整GUI操作仍未完成；双进程同机测试不冒充这些能力。新增TLS/认证仍暂缓。
