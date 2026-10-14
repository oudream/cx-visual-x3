# ccxx 跨平台 C++ 基础库：架构、运行机制、完整能力及 CxVisual 复用指南（V2.0）

> **唯一档案**：本文件为 ccxx 在 CxVisual 项目中的唯一技术资产档案。今后的聊天/Codex 审核请先读本文件，再按需读真实源码；不再维护四份单独的“深入分析”文档。  
> **指定核心**：[oudream/ccxx/tree/master/ccxx](https://github.com/oudream/ccxx/tree/master/ccxx)。本次静态源码基线：[`26cecfb02e861ce6b821b33350493bac4793e997`](https://github.com/oudream/ccxx/commit/26cecfb02e861ce6b821b33350493bac4793e997)，核对日期 **2026-10-14**。下次开发要重新核实各仓 HEAD。  
> **资产性质**：ccxx 是项目负责人投入约一年集中开发、并已在多个工业项目中多年应用的跨平台 C/C++ 公共基础库。长期生产应用的事实由负责人提供；**本轮只做源码审查，不代表本轮已编译、自动测试或现场验证**。  
> **区分证据**：[事实] 当前commit可核实声明与实现；[经验] 负责人说明的长期工程实践；[建议] 新CxVisual可采用的架构；[候选] 目标类型/Owner尚未批准。不要把“新规格对象”当作“当前代码已实现”。

## 00. 快速认知（首次阅读 1～3 分钟）

**一句话：ccxx = 轻量、可组合、跨平台的 C/C++ 工具与应用运行底座。** 它不只是 String/File/Math 工具集合，更包含应用主循环、IO 通道自管理、线程与Timer、事件发布、业务对象管理、可描述属性、配置、数据库、信息/日志和IPC等能力。

**项目原始设计追求**（[仓库README](https://github.com/oudream/ccxx/blob/master/readme.md)）：模块尽量精简、独立、降低依赖，尽可能能拷贝少量文件完成一个目标；CMake/qmake按需组合；重视测试和不同运行环境。**我们要继承的是成熟行为、管理入口、解耦与灵活组合，不是逐类翻译成C#。**

| 立即记住 | 正确理解 |
|---|---|
| 业务主线 | ccxx 的 Main/Application Thread 是核心业务事件处理线程；后台IO/Timer将事件转交应用队列，另有可选独立处理/发送线程 |
| 管理方式 | 常见“Manager统一入口 + Factory创建 + Instance承担工作 + Publisher通知”，Manager不必天然拥有专用线程 |
| CxVisual运行演进 | 按数据一致性、效率、专业特殊性把多个子方面归入一个 Execution Group；不是每个子方面一线程；这些是**未来建议，不是ccxx现有类** |
| .NET优先 | Thread/Mutex/Time/Process/Socket/File等，C#/.NET Framework 4.8已具成熟能力；主要补充**好调用、注释完整**的便利方法 |
| 最应复用的机制 | Channel管理、规约/Packet/事务链路、Describe、Business数据发布、Application/Timer执行思想 |
| 主要复用来源 | ccxx 原生实现 + [cx-visual-x2的C# Channels/Protocols](https://github.com/oudream/cx-visual-x2/tree/master/Platform) + [cx-vision-x2实际X-Ray/CT业务](https://github.com/oudream/cx-vision-x2)，新代码落在 [cx-visual-x3](https://github.com/oudream/cx-visual-x3) |

**阅读指引**：理解架构→01～03；查全部源码和类→04；研究机制→05～07；具体开发一个CxVisual项目→**08复用映射**；构建/测试/风险→09。

## 01. 整体架构与设计思想

### 1.1 逻辑架构（能力视图；不是C++目录/实际依赖图）

~~~text
CxApplication / CxAppEnv                  程序入口、生命周期、主业务循环
   ├─ CxThread / CxTimer                  线程、同步、周期事件
   ├─ CxChannelManager / CxChannel        通道管理、IO自管理、TCP/UDP/Serial
   │       └─ CxSocket / CxPacket5        原始网络、包收发与帧辅助
   ├─ CxMsObject*                          业务对象类型/数据/变化通知
   ├─ CxDescribe* / CxContext / CxProfile   元数据、属性描述、配置加载/保存
   ├─ CxInterinfo / CxLog                 信息输入输出、分类过滤与落盘
   ├─ CxDatabase / CxFile / CxResource    存储、文件与资源
   ├─ CxProcess / CxShareMemory           进程、IPC、系统资源
   └─ String / Time / Math / Buffer ...   无状态基础工具
可选扩展：Lua、Python、Qt、Redis、FTP等
~~~

### 1.2 四条值得长期保留的原则

1. **公共能力轻量可组合**：不强制使用全量ccxx；保留小模块按需引入、减少跨模块依赖。
2. **IO/专业业务解耦**：Channel负责连接/收发；业务响应交给业务执行上下文，避免业务耗时拖慢IO。
3. **统一管理入口**：资源与运行对象统一注册、发现、创建、启停、诊断；普通值类型和纯工具不必配Manager。
4. **事件与数据关联明确**：输入、命令、状态、结果、日志分开表达，不把一切通知都当作请求应答，也不把观察者等同于完整EventBus。

**适用边界**：ccxx是多个成熟产品中使用的公共资产；目前不应推断ccxx自行实现了完整工业规约平台、CT/AI算法、C#运行时或工业硬实时调度。

## 02. 程序运行机制（ccxx实际模式 + CxVisual建议扩展）

### 2.1 ccxx实际运行路线

~~~text
main()
  -> CxApplication::init()        建立程序环境、初始化组件
  -> 注册Init/Start/Stop回调       通过各模块入口管理生命周期
  -> CxApplication::exec()        Main线程业务事件循环
       ↑
       │ CxApplication::pushProcessCallBack + signalMainThread
       │
   Channel IO线程 ─┬─连接、收、发；通道可选SendQueue
                   ├─ProcessQueue可选独立线程处理数据事件
                   └─通知Channel观察者（回调线程取决于所选处理路径）
   Timer后台扫描 ────投递到Application队列，再在业务事件循环执行
   LogThread  ──────后台日志落盘
       ↓
   业务对象处理 / 状态变化 / 发布订阅
  -> Stop回调、关闭通道/Timer、事件Drain、日志Flush与退出
~~~

可核实入口：[cxapplication.h](https://github.com/oudream/ccxx/blob/master/ccxx/cxapplication.h)、[cxapplication.cpp](https://github.com/oudream/ccxx/blob/master/ccxx/cxapplication.cpp)、[cxchannel.cpp](https://github.com/oudream/ccxx/blob/master/ccxx/cxchannel.cpp)、[cxtimer.cpp](https://github.com/oudream/ccxx/blob/master/ccxx/cxtimer.cpp)。

| 执行位置 | ccxx已确认机制 | 不能误解为 |
|---|---|---|
| Main/Application线程 | 回调队列、业务事件集中处理、程序启动/停止 | 保证任何回调都无锁；或无限容量 |
| Channel IO | 不同Channel自行维护连接/收发线程；发送队列可配置 | 所有通道永远恰好一条线程 |
| Channel ProcessQueue | `IsProcessQueue`模式可用独立处理线程 | 已拥有完整可配置工业规约独立线程体系 |
| Timer | 后台扫描到期、投递回调；会有事件循环排队延迟 | 精确运动控制/采图的硬实时触发 |
| Logger | 同步/异步日志模式 | 无界吞吐或所有日志都必达磁盘 |

### 2.2 未来CxVisual：按切面与一致性域分配执行位置（**建议**）

ccxx主线程可视为**一个业务一致性域**；CxVisual不必永远只有一个。多个相关子方面可共用一个 Execution Group，独立IO、独立规约、共享Worker、专业任务或外部进程按需组装：

~~~text
Runtime/Manager（统一入口、注册、配置、生命周期、调度与诊断）
 ├─ Execution Group A：主业务一致性域，相关专业子方面串行处理
 ├─ Execution Group B：另一业务一致性域，跨组经消息/快照协作
 ├─ IO：异步IO / Channel自管理
 ├─ Protocol：Inline / Dedicated / SharedPool（可选）
 ├─ Professional：Inline / Dedicated / SharedPool / ExternalProcess（可选）
 └─ Timer/Logging/Monitoring：按各自生命周期和延迟要求安排
~~~

- **分组依据**：共享可变数据、一致性和顺序、响应时延、吞吐/CPU、阻塞风险、设备SDK线程亲和性、专业特殊性。
- **状态责任**：一个一致性域有明确的提交/写入责任方；它不等于“所有共享数据必须由系统Main线程写”。
- **消息交付**：同一会话可要求严格排序；跨组发布只读快照或复制数据，避免异步回调引用可变接收缓冲。
- **实时区分**：电机、触发采图等要求确定时序的任务，应由运动控制卡/FPGA/实时控制设备承担；普通.NET/ccxx业务线程负责准备轨迹、监测和处理结果。
- **Windows UI**：WPF UI线程不是Runtime业务线程；通过明确的Presentation投递适配，禁止让运行机制依赖Dispatcher。

### 2.3 管理对象、执行对象和发布对象相互独立

- Manager：注册/配置/查找/创建/停机/诊断/资源管理；
- Executor：在哪个执行组、线程或进程执行任务；
- Publisher：订阅/发布事件及错误信息；
- Session：维护连接、规约、请求或专业运行状态。

**没有必要令每个Manager都拥有自己的线程或把每个业务对象变为全局单例。**

## 03. 管理入口、工厂与生命周期

> 这些是源码中**真实存在的主要统一入口**，但它们的内存所有权、线程安全保证不一定相同；不能因为叫Manager就假设行为一致。

| 入口/管理器 | 主要对象/类型 | 使用价值 |
|---|---|---|
| `CxApplication` | 程序/回调/启动停止函数 | 唯一应用主循环、init/exec/exit、事件回调 |
| `CxAppEnv` | 环境/参数/部署路径 | 稳定应用目录、配置/日志/资源路径 |
| `CxChannelManager` | Channel列表/实例 | 通道统一创建、查找、启停 |
| `CxChannelFactoryManager` | ChannelFactory | 根据配置类型选择不同通道实现 |
| `CxChannelRoadManager` | ChannelRoad | 按通信来源、通道和目的建立路由 |
| `CxTimerManager` | CxTimer/ICxTimerNotify | 周期管理、启动停止与调度 |
| `CxDatabaseManager` | 数据库连接/驱动 | 连接及数据库管理 |
| `CxSkverManager` | 配置对象/引用 | beginUse/endUse、加载、共享与保存 |
| `CxDescribeManager` | Describe Project/Catalog/Unit | 描述资源和元数据加载管理 |
| `CxDescribeFactoryManager` | 描述类型工厂 | 类型/属性描述的可配置创建 |
| `CxMsObjectTypeManager` | MsObjectType | 业务类型管理 |
| `CxMsObjectDataManager` | MsObjectData | 业务数据、变更通知 |
| `CxInterinfoOut / CxInterinfoIn` | 消息订阅者、输入回调 | 分类信息的发布/输入与过滤 |
| `CxLogManager` | 日志实例/文件策略 | 输出、启停、文件管理 |
| `CxResource` | 资源集合 | 资源服务入口 |

此外 `CxProcess / CxSystem / CxFileSystem / CxHiredis` 等以静态或工具入口提供服务，不强制增加同名Manager。

**推荐的API规则（新平台建议）**：
- 公共Manager入口便于“开箱即用”，支持按ID注册/查询/创建/配置/释放、运行快照和诊断；
- 工厂/Provider只决定创建哪种实现；拥有者、租用者及释放责任应显式区分；
- 每个运行对象的状态机、线程/取消、队列容量、异常传播与观察者注销都要可验证；
- 纯计算/值对象首选静态方法、readonly值类型或短生命周期实例；“静态工具优先”**不等于全局可变设备会话**。

## 04. ccxx完整能力与核心类/接口索引

**枚举口径**：本次核对 `ccxx/` 顶层 **55个 .h/.hpp** 文件和 **52个顶层 .cpp** 文件；还存在配置/构建文件与10个子目录。下面分八组**逐一覆盖55个顶层头文件**；关键公有类、接口、Factory、模板与重要嵌套类均列出。头文件中的平台系统声明/辅助结构、所有函数模板及.cpp内局部类不逐项展开。**仅有类声明不等于功能完整或本次已实测。** 路径均相对 `ccxx/`。可按文件名访问 [核心目录](https://github.com/oudream/ccxx/tree/master/ccxx)。

### 4.A 应用、执行与同步（9）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxglobal.h` | CxGlobal、CxValueType、CxException、CxFactoryTemplate、CxFactoryManagerTemplate | 平台类型、工厂模板、异常 |
| `cxappenv.h` | CxAppEnv、CommandLineArguments | 应用路径、参数、部署环境 |
| `cxapplication.h` | CxApplication | 应用主循环与生命周期 |
| `cxapplication_qt.h` | CxApplicationQt、CxApplicationReceiver | Qt事件循环适配 |
| `cxthread.h` | CxThreadGlobal、CxThread、CxJoinableThread、CxDetachedThread、CxMutex、CxMutexScope、CxConditional、CxConditionalAccess、CxSingleWait、CxBarrier、CxSemaphore | 线程及原语 |
| `cxtimer.h` | CxTimer、ICxTimerNotify、CxTimerManager | Timer与通知 |
| `cxsafequeue.h` | CxSafeQueue<T> | 带锁队列模板 |
| `cxsafethread.h` | CxSafeThread<T> | 唤醒+队列消费线程模板（并发保证需核） |
| `cxsystem.h` | CxSystem、SysInfo、DiskInfo | 系统与磁盘信息 |

### 4.B 网络、通道与报文（11）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxsocket.h` | CxSocket、CxIpAddress、CxIpAddressExtend、CxSocketExtend、CxListenSocket、CxNetwork | 底层网络、IP与监听 |
| `cxchannel.h` | CxChannelBase、CxChannelManager、CxChannelFactoryBase、CxChannelFactoryManager、CxChannelRoad、CxChannelRoadManager、CxIChannelSubject、CxChannelPublisher、CxIChannelSend | 通道工厂、实例、路由、发布订阅 |
| `cxchannel_global.h` | 通道类型与配置常量（非类） | Type/Endpoint/IsSendQueue/IsProcessQueue |
| `cxchannel_none.h` | CxChannelNone、CxChannelNoneFactory | 空通道 |
| `cxchannel_tcpclient.h` | CxChannelTcpclient、CxChannelTcpclientFactory；内部ConnecterThread/ReceiverThread | TCP客户端 |
| `cxchannel_tcpserver.h` | CxChannelTcpserver、CxChannelTcpserverFactory；内部AccepterThread/SelecterThread等 | TCP服务端 |
| `cxchannel_udp.h` | CxChannelUdp、CxChannelUdpFactory；内部ReceiverThread | UDP |
| `cxchannel_serial.h` | CxChannelSerial、CxChannelSerialFactory；内部ReceiverThread | 串口 |
| `cxpacket.h` | CxPacket5<...>、内嵌PacketData、ReceivePacket、SendPacket | 通用报文组装/拆解模板 |
| `cxbuffer.h` | CxBuffer | 字节与位、缓冲编解码 |
| `cxsafebuffer.h` | CxLoopBuf、CxSafeBuffer | 环形/安全缓冲（边界要核） |

### 4.C 业务对象、上下文、配置与元数据（6）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxbusiness.h` | CxMsObjectType、CxMsObjectTypeManager、CxMsObjectData、CxIMsObjectDataSubject、CxMsObjectDataManager | 业务类型、数据、发布 |
| `cxcontext.h` | CxIGetSkv、CxISetSkv、CxGetSkvMap | 键值上下文和抽象读写 |
| `cxprofile.h` | CxSkv、CxSkverBase、CxSkverManager、CxSkverFactoryBase、CxSkverFactoryManager、CxSkverIni/Txt及Factory、CxProfileLoo | SKV配置及格式加载 |
| `cxprofile_skv_json.h` | CxSkverJson、CxSkverJsonFactory | JSON配置 |
| `cxprofile_skv_xml.h` | CxSkverXml、CxSkverXmlFactory | XML配置 |
| `cxdescribe.h` | CxDescribeBase、CxDescribeStrings、CxDescribeUnit/Catalog/Project/Manager、CxDescribeTemplate<T>、CxDescribeFactoryBase/Manager、CxDescribeTemplateFactory<T>、CxDescribeBoolean/DateTime/Double/Enum/Integer/String及各Factory；辅助UnitAttr/CatalogAttr | 类型与属性元数据、约束、分组和描述工程 |

### 4.D 信息、日志与资源（3）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxinterinfo.h` | CxInterinfo、CxInterinfoFilter、CxInterinfoOut_I/In_I、CxInterinfoOut/In、CxConsoleInterinfo、CxOutStreamBase、CxNoOutStreamBase、CxInterinfoOutStream/NoOutStream、CxEndFlag/CxLineFlag/CxLineEndFlag | 信息双向交互、平台分类、过滤、观察者和流式输出 |
| `cxlog.h` | CxLogFileTactics_I、CxLogManager、CxLogRealtime、CxLogThread、CxLogOutStream、CxLogNoOutStream | 日志策略、后台写入 |
| `cxresource.h` | CxResource | 资源管理 |

### 4.E 数据库、文件与序列化（8）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxdatabase.h` | CxDatabase、CursorBase、CxDatabaseManager、CxTableTemple1/2/3、CxDbDriverBase/Sqlite/Otl、CxField/Row/Table/Db | 数据库连接、CRUD及表结构辅助；部分辅助类型要核实现 |
| `cxdatabase_odbc.h` | CxDatabaseOdbcFactory | ODBC实例工厂 |
| `cxdatabase_sqlite.h` | CxDatabaseSqliteFactory | SQLite实例工厂 |
| `cxsqlite3template.hpp` | SQL::Con、SQL::PPSM及类型读写模板 | SQLite模板封装 |
| `cxrapidjson.h` | CxRapidJson | JSON解析/读写；不是正则表达式库 |
| `cxtinyxml.h` | CxTinyXml | XML及分层表 |
| `cxefile.h` | EFileElement、ExplainEfile、CxEfile | EFile格式 |
| `cxfile.h` | CxFile、CxFileSystem、CxDll、PathInfo | 文件、路径、动态库 |

### 4.F 进程、IPC（3）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxprocess.h` | CxProcess、ProcessInfo | 启停进程、枚举、资源统计 |
| `cxprocmutex.h` | CxProcMutex、CxProcMutexScope | 具名跨进程锁 |
| `cxsharememory.h` | CxShareMemory | Windows FileMapping / Unix共享内存操作 |

### 4.G 基础计算与平台辅助（10）

| 头文件 | 主要类/接口 | 能力 |
|---|---|---|
| `cxstring.h` | CxString、CxStringC | 字符串 |
| `cxencoding.h` | CxEncoding | 编码、Base64 |
| `cxtime.h` | CxTime | 日期和时间 |
| `cxuuid.h` | CxUuid | GUID/标识 |
| `cxcontainer.h` | CxContainer | STL集合助手 |
| `cxmath.h` | CxMath | 基础数学 |
| `cxcrc.h` | CxCrc | CRC/XOR/MD5等；非现代加密库 |
| `cxbitmap.h` | CxBitmap | 位图标记 |
| `cxmacro.h` | 基础宏，无独立类 | CX_LEN_*常量 |
| `cxwinreg.h` | CxWinReg、CxWinRegEx | Windows注册表 |

### 4.H 可选外部技术、集成与总入口（5）

| 头文件 | 主要类/接口 | 能力/限定 |
|---|---|---|
| `cxcurl.h` | CxCurlFtp | FTP封装 |
| `cxqtutil.h` | CxQString、CxQDialog、CxQWidget、CxQMutexScope | Qt工具，非通用无UI基础 |
| `cxlz4.h` | CxLz4 | **空类**；底层lz4源码随附，不等于ccxx压缩封装已实现 |
| `cxhiredis.h` | CxHiredis | Redis客户端 |
| `ccxx.h` | 集合包含头（非类） | 公共模块Include入口 |

**子目录完整性**：`crc/`含MD5实现；`cxlua/`（Lua运行基础及appenv/application/encoding/file/interinfo/json/log/odbc/string/time绑定）；`cxpython/`（Python模块/测试脚本）；其余`lz4/`、`mysql/`、`oci/`、`otl/`、`rapidjson/`、`sqlite3/`、`tinyxml/`包含第三方源码或集成头文件。不能将第三方类型误计为ccxx自研对象。

## 05. 核心机制与设计价值（要看.cpp执行链，不只看类名）

### 5.1 Application + Channel + Timer

- Channel封装Open/Close/Send、地址与路由、连接状态、Publisher/Subject通知；Tcpclient/Tcpserver/Udp/Serial实现自己的IO处理。
- `CxApplication::pushProcessCallBack(...)` 将一般通道事件排到业务线程并通过 `signalMainThread()` 唤醒；存在可选`ProcessQueue`独立处理，因此观察者的线程亲和性**与配置有关**。
- Timer通过后台扫描触发周期事件，再进入应用处理队列；同一间隔可共享TimerHost。源码包含有限Host/回调容量，非硬实时。
- `CxChannelManager / Factory / Road` 分别解决**管理/创建/路由**，不是三个重复Channel实现。

**保留思想**：IO自管理、接收处理与业务解耦、统一事件入口、Factory替换具体实现。**新平台需补**：明确回调线程、请求队列上限与背压、取消、重连、销毁、Metrics、错误码。

### 5.2 Describe + Context + Profile

- `CxIGetSkv / CxISetSkv` 用于统一键值读写契约；`CxSkv / CxSkverBase / Factory`承载Ini/Txt/JSON/XML介质。
- `CxDescribeBase/Unit/Catalog/Project`表达属性类型、默认值、只读/可见、非空、枚举、范围、外键等元数据，支持按描述定义配置和展示。
- `CxSkverManager`管理beginUse/endUse、共享引用及最后释放时的保存行为。

**保留思想**：配置可描述、类型/值/存储分离、Manager与Factory；**必须避免**：把旧`CxDescribeProject`当成x3唯一CxNode工程树，或把进程全局配置对象误当事务存储。

### 5.3 Business + Publisher

- `CxMsObjectTypeManager`管理业务类型；`CxMsObjectDataManager`管理数据及变化发布；`CxIMsObjectDataSubject`代表业务订阅者。
- `CxIChannelSubject`是通道通知，`CxInterinfoOut`是信息发布，三者不同，不能声称ccxx已有统一的跨专业持久EventBus。

**保留思想**：业务对象与原始数据隔离、状态变化向外发布、消费者不直接持有设备IO对象。**新平台要补**：数据身份、质量、时间戳、顺序、批量提交、异步快照与订阅释放。

### 5.4 Interinfo + Log

- `CxInterinfoOut/In`负责平台分类、type/source/reason过滤、双向消息和控制台输入。
- `CxLogManager`负责日志配置与文件策略，`CxLogRealtime / CxLogThread`支持同步/后台写日志。

**保留思想**：运行信息≠普通文件日志；分类与订阅先行，Sink/存储可替换。**新平台要补**：错误级别、事件关联ID、容量限制、停机Flush、跨日轮转与重入安全。

### 5.5 Process + ShareMemory + Thread

- `CxThread/CxMutex/CxSingleWait`等封装平台原语；`CxProcess/CxProcMutex/CxShareMemory`提供原生进程与IPC能力。
- 在新C#层，不应仅为API“对称”翻译成同名线程/锁/进程类；优先.NET已成熟的Task、ThreadPool、Monitor、Mutex、SemaphoreSlim、Process、MemoryMappedFile。

**保留思想**：进程隔离、Native数据共享、可控生命周期；**新平台需定义**：跨进程命名、共享区大小/布局、所有权、版本、内存释放、错误及进程重启协议。

## 06. 数据、消息、事件、事务与线程协作

### 6.1 明确不同职责：Transport / Packet / Protocol / Event / Transaction

~~~text
Channel/Transport（真实IO、发送/接收字节、连接状态）
    -> Packet/Framing（缓存、增量解析、拆包组帧、校验/同步）
    -> Protocol（Codec、命令/响应语义、规约会话状态）
       ├-> Event（主动上报/状态变化/错误分类、订阅发布）
       └-> Transaction（请求登记、关联匹配、等待/超时/取消/可选重试）
    -> Business / Professional（业务状态、流程、结果与专业发布）
~~~

**图是逻辑关系，不是所有消息依次必须经过Event再经过Transaction，也不是一层固定一线程。**
- **Transport** 不理解PLC/CT/设备语义；
- **Packet** 解决TCP/串口流中的半包、粘包、包边界、CRC等；UDP保留数据报边界，但上层仍可能分片重组；
- **Protocol** 解释报文、功能码、地址/寄存器、状态；可复用通用Parser/Codec，也可由插件自定义；
- **Event** 表达“发生了什么”，尤其设备主动上报；发布成功不等于操作成功；
- **通信Transaction** 表达某个请求与响应/超时/重试的全过程；与跨专业流程/工程编辑/数据库事务不是同一种事务。

### 6.2 执行策略与数据一致性

| 运行策略（候选） | 适用情况 | 需要保证 |
|---|---|---|
| Inline/Application | 轻量解析、单一致性域 | 不长期阻塞主业务循环 |
| Dedicated | 某规约/设备独占线程、强时序/SDK亲和 | 明确创建/取消/退出及队列上限 |
| SharedPool | 多通道无状态任务 | 有状态会话串行、顺序与关联ID |
| Execution Group | 多个专业子方面合并执行 | 一致性域写入责任和跨组消息协议 |
| ExternalProcess | Native CT/AI、高隔离业务 | IPC契约、超时、重启、资源回收 |

**原则**：执行线程、数据一致性域、Manager职责分别建模。每个会话内共享可变协议状态必须顺序化或有明确同步；接收缓冲交给异步消费者时应复制、租用并标记生命周期，不可裸持可复用buffer。

## 07. 跨平台、.NET便利API与扩展

### 7.1 .NET已有成熟基础能力——先用，少重写

| ccxx参考 | 在 .NET Framework 4.8 中可直接使用 | CxVisual值得补充的便利层 |
|---|---|---|
| CxThread/Mutex/Timer | Thread/ThreadPool/Task/Monitor/Mutex/SemaphoreSlim/System.Threading.Timer/Stopwatch | 取消/超时统一、线程上下文投递、任务诊断、可靠停止 |
| CxTime/CxUuid | DateTime/TimeSpan/Guid/Stopwatch | UTC、毫秒/微秒单位、单调耗时、时区转换说明 |
| CxProcess/CxSystem/AppEnv | Process/Environment/AppDomain/Path | 进程路径/检测/优雅退出与强制终止、部署目录 |
| CxSocket/Serial | TcpClient/TcpListener/Socket/UdpClient/SerialPort | 少量基础适配、错误恢复和标准化通信契约 |
| CxFile/Skv/Encoding | File/Directory/Stream/Encoding及选定JSON/XML库 | 文件原子写、格式兼容、路径/编码、配置校验 |
| CxShareMemory | MemoryMappedFile/Mutex | 大数据共享头、布局/租约/版本协议 |
| CxDatabase | ADO.NET各Provider | 会话/事务封装和工业数据操作便利API |

上述.NET类型是**库现有能力**，不表示x3已完成统一封装；不要为了静态化把有状态实例做成全局可变单例。非Framework原生的新NuGet依赖须按旧工程构建兼容性审查。

### 7.2 公开便利API的注释标准（必需）

所有新增对外入口应提供**简体中文XML文档注释**，可在IntelliSense中快速完整理解：`summary`用途与场景、`param`参数/单位/边界、`returns`返回语义、`exception`异常与取消、`remarks`线程/是否阻塞/生命周期/资源归属/协议约束、`example`典型调用。不要只写“功能：读取数据”。

例如以下是**候选文档风格，不是已有CxVisual签名**：

~~~csharp
/// <summary>
/// 从流中持续读取指定数量的字节，直到读满、取消或流结束。
/// 仅负责字节数量，不解析协议帧或匹配业务响应。
/// </summary>
/// <param name="stream">已打开且可读的流；本方法不拥有也不关闭它。</param>
/// <param name="buffer">调用方提供的目标缓冲区。</param>
/// <param name="offset">缓冲区开始位置，单位：字节。</param>
/// <param name="count">要求读取的字节数，单位：字节。</param>
/// <param name="token">取消标记；具体取消响应能力取决于底层流。</param>
/// <returns>读满后完成；若提前到流末尾，则抛出约定异常。</returns>
/// <exception cref="ArgumentOutOfRangeException">偏移或长度越界。</exception>
/// <remarks>不能假设一次 ReadAsync 就返回 count 字节。
/// 本方法是基础IO便利操作，不负责协议超时/响应匹配。</remarks>
/// <example>await ReadExactlyAsync(stream, buffer, 0, requiredLength, token);</example>
~~~

### 7.3 平台和第三方界限

ccxx C++11，有CMake及qmake配置、Windows/Unix/Qt条件编译、Native IPC与Lua/Python扩展。原生库可继续服务旧工业项目，但通过C#/Native互通时应优先明确C ABI或IPC，不直接对外暴露C++ std::string/std::vector、线程对象或可变裸指针。随附第三方库的许可证和部署依赖须分别核对。

## 08. CxVisual复用映射（**本档案重点**）

### 8.1 映射准则：不预设“谁拥有、谁使用”

**目标优先，不按ccxx类名分配Owner。**

实际审核顺序：**新平台要具备什么 → .NET已经有什么 → 旧C#系统已做什么 → ccxx哪些成熟行为可复用 → 还缺什么机制 → 哪个项目/子方面最合适**。

三类处理：
- **便利型（V）**：Framework已成熟的线程/锁/时间/进程/文件/Socket，优先用.NET，补少量高质量注释的Helper/Extension；
- **迁移型（C）**：cx-visual-x2/cx-vision-x2已有TCP、UDP、FrameParser、Modbus等实际C#实现，先审查、去重复并兼容迁入；
- **机制型（M）**：ccxx的Application主循环、Channel管理入口、Factory、Describe、Business变化发布、日志分层值得在新机制中保留；
- **Native型（N）**：有生产验证价值、重写成本高或涉及Native共享内存/高性能的代码，保留原生模块，按需封装ABI/IPC；
- **不复用（X）**：库本身已有更好实现，或功能/许可/平台不合适；明确不迁入，非遗漏。

状态独立表示：`仅有规格候选 / 旧代码可见 / x3源码已有 / x3编译通过 / x3测试通过 / 现场集成验收`。历史工程成熟≠x3可直接投入生产。当前目标项目/子方面为**关联候选，不是已裁决的Owner/Consumer**。

### 8.2 全能力映射表A：.NET成熟底座，以便捷为目标

| ccxx技术资产（源码） | CxVisual目标能力与项目候选 | 既有能力/待完善 | 建议 |
|---|---|---|---|
| CxGlobal/ValueType/FactoryTemplate | Foundation.Core（F01/F02等） | .NET泛型/异常/反射；已有CxNumericGuard与CxStableId等x3样例 | **V/M**：借鉴工厂机制，避免重复System类型 |
| CxString/CxContainer/CxMath | Foundation.Core / Numerics（F03等） | .NET String/Collections/LINQ/Math；只补工业常用计算和稳定格式转换 | **V**；先测试输入边界 |
| CxTime/CxUuid | Core、Infrastructure（F08/F19等） | DateTime/Guid/Stopwatch；明确UTC/单位/单调时间 | **V** |
| CxThread/Mutex/Semaphore/CxSafeQueue | Infrastructure（F19） | Task/ThreadPool/Monitor/SemaphoreSlim；需要按一致性域的顺序/有界队列便利 | **V/M**；线程执行组属Runtime候选 |
| CxProcess/CxSystem/CxAppEnv | Infrastructure / Lifecycle（M39/M40） | Process、Environment、Directory/Path；补进程信息/退出/部署约定 | **V/M**；不泛化为全局SystemManager |
| CxFile/CxFileSystem/CxDll | Foundation.Data（F18）、Infrastructure | .NET File/Directory/Stream/Interop；原子写、路径/文件锁、Native库加载便利 | **V** |
| CxEncoding/CxCrc/CxBitmap | Foundation.Data（F14/F20）或独立基础计算 | Encoding、位运算、Hash；补CRC、字节序/工业报文辅助 | **V/C**；CRC≠加密，旧格式Golden回归 |
| CxShareMemory/CxProcMutex | Transport（F17）与Infrastructure（F19）候选 | MemoryMappedFile/Mutex；需要SHM数据契约、版本/尺寸/租约/释放 | **V/N**；与cx-cct-x3共享接口对齐 |
| CxRapidJson/CxTinyXml/CxSkv | Foundation.Data（F14/F15/F18） | 现有JSON/XML/配置库；保持旧读写行为和未知字段兼容 | **C/V**；不另造全局配置单例 |
| CxDatabase/DatabaseManager | Mechanism.Data（M09/M10等） | ADO.NET、既有Provider；补受控事务、连接生命周期和工业查询便利 | **V/M** |
| CxWinReg/CxQtUtil/CxHiredis/CxCurlFtp/CxLz4 | 按应用需求评估 | Windows专属/Qt/Redis/FTP；CxLz4当前为空壳 | **X/V/N**；不为了清单完整硬迁 |

### 8.3 全能力映射表B：保留ccxx架构，迁移已有C#通信/业务机制

| ccxx成熟能力/关键类 | 候选子方面或项目 | 已找到的C#实现、现状 | 正式迁移/完善重点 | 分类 |
|---|---|---|---|---|
| CxApplication + CxAppEnv | Mechanism.Runtime M26～M34；Runtime.Host | x3 Runtime项目目前是骨架，Host有样例 | 主业务一致性域、运行生命周期、Execution Group、调度与诊断；不要把ccxx Main循环直接塞WPF线程 | **M/P0** |
| CxTimer / CxTimerManager | Infrastructure F19 + Runtime候选 | Framework已有Timer/Task | 统一定时管理入口、取消/归属、重入策略、调度误差监控；非硬实时 | **V/M/P1** |
| CxChannelBase + TCP/UDP/Serial | F17、Mechanism.Connectivity M13 | [x2 Channels](https://github.com/oudream/cx-visual-x2/tree/master/Platform/Channels)包含TcpClientChannel/TcpServerChannel/UdpClientChannel/UdpServerChannel；x3 Transport、Connectivity仍是骨架 | 优先复用.NET源码，抽象异步IO、断线重连、收发队列、状态、缓冲归属、诊断 | **C/M/P0** |
| CxChannelManager / Factory / Road | M13 Channel Foundation | x2有IChannel/CxChannelFactory/CxChannelSession及Descriptor/Health | 统一创建/注册/启停/查找/路由/诊断；Factory选择实现、Session明确资源归属 | **C/M/P0** |
| CxPacket5 / CxBuffer / CxLoopBuf | Foundation.Data F14、Transport F17、M14 Protocol候选 | x2有[CxFrameParser](https://github.com/oudream/cx-visual-x2/blob/master/Platform/Protocols/Contracts/CxFrameParser.cs)与ModbusTcpFrameParser | 增量解析、半包/粘包、长度/分隔/定长/校验、限额与失步恢复；避免双Parser | **C/M/P0** |
| ChannelPublisher / Subject | M12 Command Events、M13 Channel、M23 Triggers | x2 Channel订阅及协议Provider已有事件机制 | 分开传输事件、规约事件、业务事件；订阅注销、目标执行组投递、异常传播 | **M/P0** |
| CxPacket5 + Channel Send/Receive | M14 Protocol、M13 Channel | x2有[IProtocol/IFrameParser/IProtocolCodec/CxProtocolSession](https://github.com/oudream/cx-visual-x2/tree/master/Platform/Protocols/Contracts) | 可插拔Parser/Codec、状态会话、规约独立执行可选、原有协议驱动复用 | **C/M/P0** |
| Channel收发与请求等待 | M14 Protocol/通信Transaction候选 | x2 CxProtocolSession具SemaphoreSlim请求串行化和RequestAsync固定响应长度 | 通用事务管理：关联ID/超时/取消/重试/乱序/窗口；固定长度只作一种模式 | **C/M/P0** |
| CxMsObjectType/DataManager | Mechanism.Data M09/M10、Logic M12 | x3有CxNode工程对象样例，完整变量/数据机制未见 | 标准化业务值、质量、时间戳、变更批次、通知与执行组一致性；不复制第二工程树 | **M/P0** |
| CxDescribe* / Factory / Manager | Mechanism.Core M01～M08候选 | x3已有CxNode/Project/Page/Content/Symbol/Group/Element及序列化样例 | 统一属性描述、校验、类型约束、默认值；旧DescribeProject只是元数据格式，不是新Project | **M/C/P0** |
| CxContext / CxProfile / SkverManager | Foundation.Data F15、Mechanism.Core/Data候选 | .NET配置/JSON/XML和x2现有序列化 | 旧配置格式兼容、共享/释放语义、加载错误可观察、工程序列化唯一入口 | **C/M/P1** |
| CxInterinfoOut/In + CxLogManager | Foundation.Infrastructure F16、Mechanism.Runtime/M12候选 | x3 Infrastructure有CxLogRecord、ICxLogSink、Memory/File LogSink样例 | 统一日志/运行消息分类、订阅过滤、审计/实时/历史、队列背压、Flush | **C/M/P1** |
| CxProcess + ShareMemory | Runtime.Host、Mechanism.Runtime、专业CT3D/Vision | C#原生API及cx-cct-x3现有Native模块 | C ABI/IPC/共享内存版本与所有权、心跳、崩溃重启、GB级数据互操作 | **V/N/P1** |
| cxlua/cxpython | Mechanism.Logic M11/M12、专业按需 | .NET可用脚本机制；旧Lua/Python属于Native扩展 | 允许专业按需调用，不引入所有第三方运行时为强制依赖 | **M/N/P2** |
| 特定业务协议/驱动 | Mechanism.Connectivity M14/M15 与Professional.Automation | x2现有Modbus TCP/RTU、S7、MFL及驱动；cx-vision-x2含现场协议 | 通用契约留公共，具体厂商/设备语义保留专业或插件，按实际Owner后定 | **C/M/P0-P2** |

**注意**：本节排序是架构关联，不是提交“谁拥有/谁使用”的单向结论。x2实有的实现≠已迁入x3；对于ccxxNative功能与现有C#功能同时存在的情况，应优先选择行为可靠、维护成本较低且符合现有宿主环境的一种实现，**避免两份权威实例**。

### 8.4 通信机制的完整复用切面——避免F14/F17/M13/M14各造一套

| 层次 | 面向调用者应提供的能力 | 建议分界（候选） |
|---|---|---|
| **Transport** | TCP client/server、UDP、Serial；统一端点、异步Send/Receive、取消、连接/断链、原始字节数据 | F17提供基础描述/缓冲辅助，M13持有通道Session与管理入口；具体代码迁自x2 |
| **Packet/Frame** | 缓冲、粘包拆包、增量解析、定长/头长字段/分隔符/尾标记、CRC、报文边界及失步处理 | F14提供无状态编码/校验/格式运算；F17提供可复用字节缓冲及状态性组帧执行；M14装配具体规约解析器；**只允许一套公共Parser语义** |
| **Protocol** | 可插拔IProtocol/IFrameParser/IProtocolCodec、规约Session、命令、设备状态、可选处理线程 | M14公共规约契约与会话，M15驱动/Provider；专业协议实现由最终裁决确定 |
| **Event** | 已接收/已发送/连接变化/主动上报/告警、过滤、订阅与跨执行组交付 | 通信低层事件在M13/M14；通用事件命令/订阅在M12/M23；明确区分，不创两套EventBus |
| **Communication Transaction** | RequestId/TransactionId、请求入队、响应关联、超时/取消、可选重试、并发窗口、错误结果 | **待裁决**：M14最贴近协议Session；共用的时间/等待原语可下沉基础。不能与CxNode工程事务或Orchestration业务事务混为一谈 |
| **Execution/Management** | Channel/Protocol统一Manager、Factory、Session；Inline/Dedicated/SharedPool/ExecutionGroup选择；诊断监控 | M13/M14提供管理，Runtime提供执行策略；Manager ≠ Thread，运行方式由配置/一致性需求决定 |

#### 对现有C#代码的三项具体审计提醒

1. x2的 `CxFrameParser`是有容量上限的增量解析基类，解析失败会fault直到Reset，适合作公共语义参考；但其 `List<byte>.RemoveRange(0, consumed)` 在高频大包下应压测，不宜先断言高吞吐零拷贝。
2. x2 `CxProtocolSession.RequestAsync(message, responseLength)` 当前包含请求串行化；**响应固定长度不应成为所有规约唯一公共契约**。帧完整性可以由Parser决定；Modbus TCP等具关联ID的协议允许实现窗口，但必须保证请求/应答匹配。
3. 旧Channel的接收事件中缓冲有可重用情况：被投递给异步业务时需复制/租用，定义“谁持有缓冲、何时可回收”；不要假设每个回调永远拥有独立不可变byte[]。

### 8.5 使用真实场景检验映射（不做“空壳类通过”）

| 场景 | 数据路径及必须完整的能力 | 必须验证 |
|---|---|---|
| Modbus TCP读寄存器 | Channel→TCP MBAP分帧→Protocol寄存器语义→TransactionId关联→数据质量/业务发布 | 并发窗口、乱序应答、超时、断线重连、错误码 |
| Modbus RTU串口 | Serial→缓存/RTU时序→CRC→Protocol→串行请求应答→业务发布 | 半包、噪声、串口共享、收发转向、设备超时 |
| X-Ray设备主动上报 | TCP/UDP→Packet→Protocol→Event→专业ExecutionGroup | 无需伪造请求事务；高频上报、队列背压、顺序与内存复用 |
| CT图像与Native重建 | Channel/共享内存→Frame或SHM DataHandle→CT3D Native Worker→Result Event | GB级布局、进程恢复、内存所有权、无UI运行 |
| 多专业同运行时 | 两通道/不同协议→共享/独立处理组→Automation/HMI/Inspection状态联动 | 不同一致性域、受控设备写入、异常隔离、订阅释放 |

### 8.6 建议的映射验收口径

每个未来项目正式定案时，必须回答（本文件不预填已完成）：
- 是否已有.NET基础能力？已有C#实现在哪个仓库/文件？
- 这次引用ccxx是**复用源码、保留Native、迁移旧C#，还是仅借鉴机制**？
- 选择哪个基础能力作为唯一API？是否重复定义Parser/Channel/Project/Log？
- 调用者能否用少量配置和清晰注释完成“创建→执行→事件→停止→异常恢复”闭环？
- 所需线程/执行组、数据一致性、缓冲所有权、订阅释放及超时策略是否明确？
- 对应测试能否实际编译/运行？性能指标和现场设备是否已验证？
- 只有完成逐项审核才写入正式Owner，不把本表候选归属当定案。

## 09. 源码、构建、测试与风险证据

### 9.1 最短源码导航

| 主题 | 优先读 | 对照测试/来源 |
|---|---|---|
| 主线程与计时 | `cxapplication.h/.cpp`、`cxtimer.h/.cpp`、`cxthread.h/.cpp` | [test/application](https://github.com/oudream/ccxx/tree/master/test/application)、[test/timer](https://github.com/oudream/ccxx/tree/master/test/timer) |
| Channel | `cxchannel.h/.cpp`、具体tcpclient/tcpserver/udp/serial | [test/channel](https://github.com/oudream/ccxx/tree/master/test/channel)、x2 [Channels](https://github.com/oudream/cx-visual-x2/tree/master/Platform/Channels) |
| 规约与组帧 | `cxpacket.h`、`cxbuffer.h`；旧x2 CxFrameParser/CxProtocolSession/Modbus | x2 [Protocols](https://github.com/oudream/cx-visual-x2/tree/master/Platform/Protocols) |
| 配置/描述/业务 | `cxdescribe.*`、`cxprofile.*`、`cxcontext.*`、`cxbusiness.*` | 旧工程配置Golden文件；[ccxx test](https://github.com/oudream/ccxx/tree/master/test) |
| 日志与信息 | `cxinterinfo.*`、`cxlog.*` | 日志高并发/磁盘满/过滤测试 |
| IPC | `cxsharememory.*`、`cxprocess.*`、`cxprocmutex.*` | [test/sharedmemory](https://github.com/oudream/ccxx/tree/master/test/sharedmemory)、cx-cct-x3 ABI |
| 构建 | [CMakeLists.txt](https://github.com/oudream/ccxx/blob/master/CMakeLists.txt)、[ccxx.cmake](https://github.com/oudream/ccxx/blob/master/ccxx/ccxx.cmake)、ccxx.pri | CMake C++11、按需env/interinfo/simple/core，第三方配置单独处理 |

### 9.2 静态源码中已发现的重点审核项（不得写成生产故障事实）

| 风险位置（当前ccxx commit） | 需验证内容 |
|---|---|
| `cxchannel.h` 的 `CxChannelPublisher::removeObserver` | 仅 `std::remove` 未见 `erase`，是否造成订阅者未真正删除；销毁/重新注册场景优先测试 |
| `cxapplication.cpp` 回调队列 | 源码队列容量约12288项、单项数据上限约4MiB；满队列导致退出的处理、背压及异常必须明确 |
| `cxprofile.h` `hadLoaded()` | 返回 `_filePath.empty()`，与通常“已加载”语义相反；先审查实际调用 |
| `cxprofile.cpp` `beginUse(...)` | 加载失败可返回配置对象；调用者不能将“对象非空”当加载成功 |
| `cxinterinfo.cpp` 分发观察者 | 持锁调用观察者，回调重入与锁顺序风险 |
| `cxlog.cpp` 后台日志 | 待写缓冲持有FILE*，需测试轮转/关闭竞态、容量与Flush |
| `cxsharememory.cpp` Windows/POSIX分支 | 构造器实参、mmap失败、UnmapViewOfFile参数、匿名回退及同步Copy边界都需按平台复现 |
| `cxprocess.cpp` Unix分支 | execve失败后的退出码和Detached行为验证 |
| `cxsafethread.h` 队列操作 | 未见成员Mutex包裹push/pop，不能直接认定多生产者线程安全 |
| `cxlz4.h/.cpp` | 空壳封装，不计为已实现的统一LZ4功能 |

这些是审查固定commit的代码行为或潜在风险：**不代表历史多年产品一定部署相同版本、运行这些分支或出现事故。** 修复旧生产仓必须先取得授权、核实产品版本并做回归测试。

### 9.3 版本与完成状态

- 已核对：ccxx 55个顶层头文件及其代表类，52个顶层.cpp目录项，核心事件/配置/日志/IPC部分.cpp执行路径；x2 C# Channel/Parser/Session/Modbus代表代码；x3目标项目结构及V6.4子方面归属清单。
- 未在本轮完成：ccxx及x3在Windows/MSBuild/GCC环境真实编译，所有单元测试、性能压测、物理设备验收；未对所有单个函数或第三方依赖进行完整代码审计。
- 新x3目前的Foundation.Transport、Mechanism.Connectivity、Mechanism.Runtime、Foundation.Data属于工程骨架；Foundation.Infrastructure已有少量日志样例；不能把规划中的F14/F17/M13/M14候选类型写成已完成。
- 当最新规格/源码与本文不一致，**先读最新实际文件再标注差异和更新文档**；如出现Owner冲突，按四大文档和项目定案流程处理，不静默改变边界。

### 9.4 对后续ChatGPT/Codex的明确执行要求

**开工提示**：”先阅读 `DOCS/Codebases/01_ccxx.md` 的 00/02/03/04/08 章，再读取本轮CxVisual项目、V6.4/V4.9关联规格和相关旧C#代码，做能力级复用审计。优先.NET成熟能力和旧C#工程实现；Manager便利入口保留；线程按切面/一致性域配置；先确认唯一API/边界再写代码，编译测试按真实记录。“

**维护规则**：本文件保持**唯一入口**，无需拆回四份专项MD。第04章是ccxx事实索引，第08章是CxVisual复用判断，不在两处重复定义规范；每次只按实证更新当前仓commit、真正签名/已有能力和验证状态。原生产ccxx与新主仓各自独立，未经明确指示不擅自改旧系统。

---
*文档V2.0：2026-10-14，经逐源代码梳理并按“管理入口、切面执行、.NET便利优先、Packet/Protocol/Event/Transaction公共机制、单文档维护”讨论方向重整。*