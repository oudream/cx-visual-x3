# F17 NamedPipe独立接收与跨进程大数据实施 V1.0

2026-10-28，起点master `9c0fb6d19579d018621e7640e83a80531ef72198`，实施期间持续纳入并行master更新，最终固定SHA由验收矩阵补录。正式Owner为Foundation.Transport，沿用原FTR配置、契约、地址/Generation、Factory、诊断和池化交付，没有新增项目或第三方NuGet/DLL。

## 实际模型

| 位置 | 正式行为 |
|---|---|
| FTRNamedPipeClientChannel | 一个专属后台Receive Thread；连接阶段使用原ConnectAsync/ConnectTimeout，成功后运行Token独立；无每次Read临时载荷数组 |
| FTRNamedPipeServerChannel | 一个Accept Thread、一个共享Receive Thread和原有有界Delivery Thread，12对端共用同一接收线程；不按连接增加reader Task/Thread，无历史读者列表 |
| FTRPipeIO | Windows PeekNamedPipe查询可读字节，单个读者按可读长度同步Read；单连接写门内同步Write切片，异步入口受基类快照数量/字节限制后复用同底层 |
| 原FTRReceivePipeline/Buffer | 有界接收池先取得额度再Read；Retain/Dispose、来源元数据、Closing/Faulted中的已接纳字节排空保留；EOF/Peer移除排在旧数据之后 |
| 原接收诊断 | 真实线程身份/存活、是否观察到多个接收线程、池水位/字节预算、等待、终止通知额度及在途通知，输出到现有Diagnostics快照 |
| TestCenter原固定CLI预设 | 14个预设不新增另一框架；原channel-transport覆盖管道短专项，原channel-bulk-long同时覆盖TCP和跨进程管道各双向2GiB，标题明确实际范围 |

新增的OS调用是Windows系统Kernel32.PeekNamedPipe；本机实际`C:\Windows\System32\kernel32.dll`版本10.0.19041.7725存在并成功调用，没有虚构SDK或复制第三方二进制。全部管道句柄保留`PipeOptions.Asynchronous`，这是Windows readiness查询与同步写入并行所需的句柄方式，公开同步Send仍等待同一底层Write完成；没有根据IoMode配置维护第二套引擎。

实现依据已核查的[Microsoft PeekNamedPipe说明](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-peeknamedpipe)与[.NET Framework参考源码](https://github.com/microsoft/referencesource/blob/main/System.Core/System/IO/Pipes/Pipe.cs)。前者说明同步句柄可能导致多线程等待，故不能为追求表面“同步”改掉异步句柄标志；这些官方说明不代替本轮真实Windows测试。

## 限流、生命周期与失败语义

- 原创建OS实例前的MaxClients名额保留，覆盖pending、活跃和结束通知尚未交付的实例；最后一次Accept必须先结算，再关闭其转交实例。正常饱和不创建超额实例，不让Listener变Faulted。
- NamedPipe内核输入缓冲建议值为`min(ReceiveBufferBytes,ReceiveByteBudget)`，输出为`min(ReceiveBufferBytes,SendByteBudget)`，按MaxClients另受连接数约束；Windows内核缓冲、线程栈不冒充为托管接收池预算的一部分。
- 逐Peer写门避免慢Peer阻塞其它对端。同步切片借用调用者数据；异步先在正式基类预算中接纳并复制快照。写门等待和整个Write共用SendTimeout，真正阻塞的写入通过截止Token关闭Pipe打断；超时/取消不能证明远端未收到，不自动重发。成功返回前注销本次取消回调，之后取消原Token不能关闭已完成写入的连接。
- EOF、空闲Peer和单Peer故障只清理相应代次；Client空闲进入Faulted，Server保持Listener。显式Reconnect取得新代次，不继承旧协议半包。来源元数据保留在租约和原Received上下文中。
- Stop在同一StopTimeout内取消OS读取、停止Accept/Receive、结算已入场发送，再排空交付；未结算回调必须超时/Faulted，不能提前回收租约或假Closed。显式RemovePeer可并发归还捕获的旧代名额；其SemaphoreSlim未请求AvailableWaitHandle，只保留托管状态等待最后引用释放，不能把旧代归还打到新Open的门上。
- 接收和Accept线程、Delivery回调不能同步等待自身Close。共享接收线程进入时即登记身份，不能等第一块数据才登记，否则首次空闲故障的状态回调仍可能自等待。
- 终止通知另有QueueCapacity额度，包含正在执行的通知；等待额度使用运行停止Token。即使Host反复显式RemovePeer，也不能绕过MaxClients的间接限制使元数据无限排队；取消的未接纳通知不得执行。通知额度、数据块预算和异步发送快照预算相互独立。
- 公共受保护帮助方法`AfterReceived`现要求显式停止Token；仓内TCP/UDP/NamedPipe所有调用已适配。外部基类派生实现需重编译并按当前契约适配；不声称外部二进制兼容。

连续流使用短时让出CPU、真正空闲后低频等待的策略，避免每64KiB固定等待导致人为限速。该策略仅在通道I/O线程内运行，不是业务调度器；协议解析与复杂任务仍归Connectivity/MRK。

## 可执行例子与实测

`FTRNamedPipeBulkTests.CrossProcessDuplex`创建真实第二个ArchitectureSmoke进程：双方仅用65536字节工作块，从同步切片Send持续发送不同逐字节图案并验证顺序；控制台stdin只用于确认双方收齐后退出，体数据全部通过真实NamedPipe传输。没有分配整卷数组。

本机Windows .NET48/x64初测：TCP双向各2GiB8756ms，管道双向各2GiB17966ms（4GiB总量，含子进程启动、生成和逐字节校验，227.82MiB/s）；父端CPU31703.1ms、子端27937.5ms，父端working set51179520、子端33005568，父端GC0=14/GC2=1、子端12/1，接收池两端各262144字节。差值指标可能因GC为负，不可把managed delta当峰值内存。它是本机软件样本，不是网络带宽或产线SLA、24小时稳定性验收。

终止通知额度与停止身份完善后的最终本机复核：管道双向各2GiB11948ms、342.65MiB/s，父/子CPU25734.4/26046.9ms，working set50561024/33202176，GC0/GC2分别23/2与12/1；两端接收池仍各262144字节。TCP复核10854ms，仍逐字节通过。性能随调度与GC波动，以实际runner样本为准，不比较不同源码/环境来制造SLA。

完整Build、专项、默认回归、固定SHA远端CI与测试逐项映射见[验收矩阵](../Testing/CxVisual_F17_NamedPipe跨进程大数据_验收矩阵_V1.0.md)。上一包饱和修复的32轮重连和8轮Accept/Close反例全部保留。

## 完整目标尚未结束

后续仍包括Serial/HTTP/WebSocket/Virtual/Replay等收发模型与全部有效预算、HTTP/WS Server、SharedMemory、自动恢复/保活、完整TestCenter操作与诊断、长期及跨PC/物理设备HIL。默认安全ACL和现有权限检查保留，新增TLS/认证按要求暂缓。本包不替代这些剩余目标。
