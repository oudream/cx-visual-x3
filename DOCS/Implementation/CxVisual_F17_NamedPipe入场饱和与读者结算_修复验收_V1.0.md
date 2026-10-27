# F17 NamedPipe入场饱和与读者结算修复 V1.0

2026-10-28；正式Owner仍为Foundation.Transport，增量修复已有NamedPipe Server，不增加项目或DLL。

## 真实缺陷与修复

原AcceptLoop每接纳一个对端便立即创建下一NamedPipeServerStream，构造参数maxNumberOfServerInstances等于MaxClients。已有实例达到MaxClients时仍继续创建会触发Windows实例耗尽错误，经外层Fault把正常饱和变成整个Listener故障。原`_clients.Count`检查位于创建和连接之后，不能保护这一Windows资源边界。

新增独立入场名额，在创建OS实例之前等待；连接成功后由该读者保留，完成断开、移除和Dispose后归还；创建/等待失败和取消未转交时在finally归还。没有放大Windows的实例上限，也不把第二客户端超时当连接成功。单Peer读取错误只诊断并清理自身，不杀死Listener。

原`_readers`无限保存历史完成Task，直到整通道Close才清空；每次新读者入场前剔除已完成项，防止随历史连接数持续增长。仍在执行的读者必须保留以供Close结算。关闭先取消并释放pending/现有Pipe，等待Accept退出，然后再次取得完整的最后入场读者/连接快照，关闭并等待其结算后才释放信号量，避免漏掉刚通过取消检查的Accept。

## 可执行验证

`Tests/ArchitectureSmoke/Transport/FTRNamedPipeAdmissionTests.cs`在原`--channel-transport`和默认回归中运行，采用真实Windows NamedPipe：

- MaxClients=1：第一连接真实写入并接收；第二连接必须超时或busy拒绝，Listener仍Open、目录只有一个Peer。
- 首连接关闭后，同一Listener完成32轮真实新连接/传输/断开，逐轮检查全部字节到达、Peer清空和监听Open。
- 8轮连接刚完成即Close，验证Closed和空目录，再次Open，覆盖最后Accept/读者的结算边界。
- 原全通道/TCP体数据/Factory/UDP组播回归保留，不跳过或放宽测试。

本机Windows .NET48/x64完整方案Build成功；通道专项exit0，包含32轮重连和8轮最终Accept关闭，输出`F17.PIPE_ADMISSION PASS`；默认ArchitectureSmoke exit0。远端CI按最终固定SHA补录。历史完成Task剔除由真实源码证实，本测试没有把私有集合元素数量冒充传输功能验证。

## 仍未完成

本包不宣称NamedPipe完整目标完成：原Server/Client仍有异步接收Task、临时数组与旧发送门；独立后台接收、有界池化、逐Peer发送门、全部超时/慢消费者、跨进程大数据与可靠关闭的完整模型仍按总审计继续。不以32轮连接替代长期稳定性、物理设备或全部通道完成。
