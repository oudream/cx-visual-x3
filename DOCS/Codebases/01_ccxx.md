# 01 · ccxx 跨平台 C/C++ 基础库技术资产档案（V1.0）

> **文档用途**：CxVisual 七仓代码资产的首份专项档案。让新的 ChatGPT / Codex 会话在 1～3 分钟内了解 ccxx 是什么、能力在哪里、怎样复用、哪些结论尚待验证。  
> **指定核心目录**：[oudream/ccxx/tree/master/ccxx](https://github.com/oudream/ccxx/tree/master/ccxx)（与根目录、third-party、sample/test 区分）。  
> **本次源码基线**：`oudream/ccxx` / `master` / [`26cecfb02e861ce6b821b33350493bac4793e997`](https://github.com/oudream/ccxx/commit/26cecfb02e861ce6b821b33350493bac4793e997)；HEAD提交时间 2023-01-12 UTC；核对日期 2026-10-14。新任务须重新读取 HEAD。  
> **可靠性与限制**：仓库README、目录清单、代表性头文件和部分实现源码已检查；未逐行审核所有 130 个核心目录文件；本次未执行真实编译、单测、性能测量或设备测试。负责人说明其约一年时间集中开发，已多年用于多个实际项目；README另记述项目持续演进多年，二者表达的时间口径不同。  
> **归属**：这是 C++ 工程资产，不是 CxVisual 新增的 C# 子项目，也不是仅供学习的演示仓库。

## 1. 核心结论（首先阅读）

**ccxx 是跨平台C/C++公共基础设施与应用运行能力库。** 不止有字符串/文件等工具，还包含运行环境、主线程事件循环、带工厂的通信Channel、线程与定时器、进程/共享内存、统一日志/信息输入输出、配置/描述/数据操作、脚本封装等可组合能力。应当区分纯计算工具、带生命周期的资源对象、进程级运行机制三种形态。

项目在 [README](https://github.com/oudream/ccxx/blob/master/readme.md) 的设计宣言为：**轻量、精简、单文件/模块优先、模块尽量少互相依赖、可以按需抽取复用**；并倡导 unit test、benchmark 和跨环境验证。今后讨论 CxVisual 中的基础能力，先检查ccxx是否已经做过，再决定独立调用、Adapter、概念复用或重构。**不强制将多年成熟C++代码全部改写成C#。**

## 2. 实际源码组织与构建入口

| 范围 | 证据 | 说明 |
|---|---|---|
| `ccxx/` 核心目录 | [core](https://github.com/oudream/ccxx/tree/master/ccxx) | 直接子项：130个文件，其中55个`.h/.hpp`、53个`.cpp/.cxx`，此外10个子目录；这些是目录枚举数，**不是类型/有效功能/代码行统计** |
| `ccxx/ccxx.h` | [汇总入口](https://github.com/oudream/ccxx/blob/master/ccxx/ccxx.h) | 主头汇总绝大部分公共模块 |
| `ccxx/ccxx.cmake` | [CMake 模块配置](https://github.com/oudream/ccxx/blob/master/ccxx/ccxx.cmake) | 定义完整core、`ccxx_env`、`ccxx_interinfo`、`ccxx_simple` 等分级文件组，按需使用，不能仅凭分组名称断言无依赖 |
| `ccxx/ccxx.pri` | [qmake 配置](https://github.com/oudream/ccxx/blob/master/ccxx/ccxx.pri) | Qt qmake集成及Win32/Win64、Unix链接条件 |
| 根 `CMakeLists.txt` | [构建入口](https://github.com/oudream/ccxx/blob/master/CMakeLists.txt) | CMake、C++11、simple/all配置选择、3rd/gTest/测试构建组织 |
| `test/` | [测试源码](https://github.com/oudream/ccxx/tree/master/test) | 47个直接测试/实验子目录，包含application、channel、database、sharedmemory、thread、timer、socket、encoding等；**存在测试目录≠当前全部通过** |
| `sample/`、`benchmark/` | [sample](https://github.com/oudream/ccxx/tree/master/sample) / [benchmark](https://github.com/oudream/ccxx/tree/master/benchmark) | 简单程序、命令、Qt/Lua/Python示例，以及字符串性能实验 |
| `3rd/` 与核心目录内第三方子树 | [3rd](https://github.com/oudream/ccxx/tree/master/3rd) | 可选/随附第三方源码，不自动归为ccxx自研类，移植时逐项核对许可证及版本 |
| 根 `LICENSE` | [MIT License](https://github.com/oudream/ccxx/blob/master/LICENSE) | 仓库声明MIT；**第三方依赖的许可证仍需分别核对** |

核心目录下有 `crc/`、`cxlua/`、`cxpython/`、`lz4/`、`mysql/`、`oci/`、`otl/`、`rapidjson/`、`sqlite3/`、`tinyxml/`。部分目录是随附第三方源码/头文件，不能全部算成自研模块。

## 3. 能力索引：实有模块、接口及其用途

以下“已见源码”指找到相应头文件的**声明和对应源码路径**；**不表示本次已完成运行验证**。为便于未来快速索引，路径都相对于仓库的 `ccxx/` 核心目录。

| 能力簇 | 源文件及关键API / 类型（已见源码） | 典型复用方向 |
|---|---|---|
| 平台/基础类型 | [`cxglobal.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxglobal.h)：平台、编译器检测，公共类型、异常、工厂模板；`cxcontainer.h`：`CxContainer` | Foundation.Core 的值、集合/跨平台契约；谨慎隔离全局宏 |
| 字符串/时间/UUID | `cxstring.h`：`CxString`转换/格式化；`cxtime.h`：`CxTime`时间戳/日期；`cxuuid.h`：`CxUuid` GUID生成 | Foundation.Core/Data/Infrastructure |
| 数学/二进制缓冲 | `cxmath.h`：`CxMath`最大公约数、最小公倍数等；`cxbuffer.h`：`CxBuffer::toBuf/fromBuf`、位与地址缓冲处理；`cxbitmap.h` | Foundation.Numerics / Transport；**这里不是一整套几何/矩阵引擎** |
| 校验/编码 | `cxcrc.h`：`CxCrc`，MD5、XOR等；`cxencoding.h`：`CxEncoding` GB2312/UTF8/Base64 | 数据校验、编码适配；**MD5及固定XOR不能被当作现代安全加密方案** |
| 文件/动态库/资源 | [`cxfile.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxfile.h)：`CxFile`、`CxFileSystem`、`CxDll`；`cxresource.h`：`CxResource` | Foundation.Data/Infrastructure、跨平台文件与Native加载适配 |
| 程序环境 | [`cxappenv.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxappenv.h)：`CxAppEnv`部署/配置/日志/数据目录、命令行参数、进程单例检查 | App/Runtime Host 的Native程序组织参考，非纯数学基础 |
| 事件循环 | [`cxapplication.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxapplication.h)：`CxApplication::init/exec/processEvents/pushProcessCallBack/signalMainThread`，启动/停止函数注册 | Mechanism.Runtime/宿主运行、事件调度模型 |
| 多线程与同步 | [`cxthread.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxthread.h)：`CxThread`、`CxJoinableThread`、`CxDetachedThread`、`CxMutex`、`CxConditional`、`CxSemaphore`、`CxBarrier` 等 | Foundation.Infrastructure，尤其Native执行线程 |
| 定时器 | [`cxtimer.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxtimer.h)：`CxTimer`、`ICxTimerNotify`、`CxTimerManager` | 周期任务/业务心跳/运行循环 |
| 网络Socket | [`cxsocket.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxsocket.h)：`CxSocket`、`CxIpAddress`、`CxNetwork`、`CxListenSocket`等 | Foundation.Transport的Native网络能力 |
| 统一通道 | [`cxchannel.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxchannel.h)：`CxChannelBase`、`CxIChannelSubject`、`CxChannelManager`、`CxChannelRoad`、`CxChannelFactoryBase` | Mechanism.Connectivity及Automation通信Provider |
| 通道具体实现 | `cxchannel_tcpclient.h`、`cxchannel_tcpserver.h`、`cxchannel_udp.h`、`cxchannel_serial.h`、`cxchannel_none.h` | TCP客户端/服务端、UDP、串口；可作为Native Adapter来源 |
| 进程/IPC | [`cxprocess.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxprocess.h)：`CxProcess`启动/停止/枚举及资源统计；`cxprocmutex.h`：`CxProcMutex`；[ `cxsharememory.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxsharememory.h)：`CxShareMemory` | Foundation.Infrastructure、Runtime.Host、跨进程Native服务集成 |
| 信息与日志 | [`cxinterinfo.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxinterinfo.h)：`CxInterinfo`、`CxInterinfoOut/In`、`CxInterinfoFilter`；[`cxlog.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxlog.h)：`CxLogManager`、`CxLogRealtime`、`CxLogThread` | 事件/日志分类、过滤、实时订阅、异步落盘 |
| 配置与上下文 | [`cxcontext.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxcontext.h)：`CxIGetSkv`、`CxISetSkv`；[`cxprofile.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxprofile.h)：`CxSkv`、`CxSkverBase`、`CxSkverManager`及Ini/Txt工厂；`cxprofile_skv_json/xml.*` | Foundation.Data配置与对象数据交换 |
| JSON/XML结构化数据 | [`cxrapidjson.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxrapidjson.h)：`CxRapidJson::load/save/loadTable`；[`cxtinyxml.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxtinyxml.h)：`CxTinyXml`多层表数据 | 文件序列化、表数据交换；README个别文字把`cxrapidjson`描述成正则，**以真实源码为准：这里是JSON封装** |
| 类型描述/元数据 | [`cxdescribe.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxdescribe.h)：`CxDescribeBase/Unit/Catalog/Project/Manager`、属性可见性、范围、外键、类型描述 | 研究 Mechanism.Core 属性/元数据、编辑器对象描述，但**不得直接与CxNode工程树等同** |
| 统一数据库 | [`cxdatabase.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxdatabase.h)：`CxDatabase`、`CxDatabaseManager`、Cursor、表增改查、SQL；`cxdatabase_odbc/sqlite.*`，另有OCI实现文件 | Mechanism.Data的数据访问/存储Provider；需审查SQL、会话与事务语义 |
| 业务对象/数据通知 | [`cxbusiness.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxbusiness.h)：`CxMsObjectTypeManager`、`CxMsObjectData`、`CxMsObjectDataManager`、观察者 | 面向SCADA/IoT对象数据建模的可复用思想；不是新CxVisual Project/Page/Element |
| 嵌入脚本 | [`cxlua/`](https://github.com/oudream/ccxx/tree/master/ccxx/cxlua)：Lua对appenv/application/encoding/filesystem/log/JSON/ODBC/string/time的包装；[`cxpython/`](https://github.com/oudream/ccxx/tree/master/ccxx/cxpython)：Python扩展示例 | Mechanism.Logic/Automation脚本或Native服务适配，需验证各自依赖 |
| 其他可选集成 | `cxhiredis.h` Redis客户端包装；`cxlz4.h/.cpp`预留文件；`cxpacket.h`包结构；`cxqtutil.*`Qt工具；`cxcurl.*`等 | 先检查对应实现是否完整及编译条件；**头文件/空壳不代表功能完备** |

快速打开源码的方式：在 `https://github.com/oudream/ccxx/blob/master/ccxx/` 后追加上表的文件名；例如 `cxchannel.h`。

## 4. 值得单独研究的四项系统架构

### 4.1 `CxApplication` + `CxChannel` + `CxTimer`：应用事件循环

README明确说明：Channel可开独立线程进行收发/监听，接收数据后把事件推入主线程事件循环。源码对应到：
- `CxApplication::pushProcessCallBack(...)` + `CxApplication::signalMainThread()`；
- `CxChannelBase::threadEventNotify` / `processThreadNofity` 和 `CxIChannelSubject::channel_receivedData`；
- `CxTimer` / `CxTimerManager` 的超时通知。

**价值**：把通信输入、事件通知、定时器和业务处理组织为有序运行机制；支持工业进程的运行/通信解耦。**限制**：不能仅凭README推断整个系统在任意回调中都不需要锁，仍须验证发送队列、订阅列表、关闭流程、信号与线程退出时序。

### 4.2 `CxChannelBase` + 工厂/观察者 + Road

- Channel抽象统一参数上下文、`open/close/send`、连接变化、数据通知；
- 工厂创建 `CxChannelTcpclient/Tcpserver/Udp/Serial` 等实现；
- `CxChannelRoad` / `CxChannelRoadManager` 负责关联远端地址、来源ID、通道与发送路径。

**价值**：Mechanism.Connectivity定义中立Channel/Protocol/Driver契约时可复用既有成熟分层；不要让Foundation.Transport反向依赖专业协议与设备业务。

### 4.3 `CxIGetSkv/CxISetSkv` + Profile + Describe

- `CxIGetSkv/CxISetSkv`：对象与配置介质之间的键值读写契约；
- `CxSkverBase` + 工厂：Ini/Txt/JSON/XML等加载保存；
- `CxDescribeBase/Unit/Catalog/Project`：描述模型、目录、属性约束、外键、可读写/可见等元信息。

**价值**：可用于审核CxVisual的可配置、可描述、可持久化能力。要区分“老系统数据描述模型”和“新平台的唯一CxNode工程对象”。

### 4.4 `CxInterinfo` + `CxLog` 与运行诊断

- `CxInterinfoFilter` 按 type/source/reason过滤；
- `CxInterinfoOut/In` 提供双向信息输出与命令输入；
- `CxLogRealtime/CxLogThread` 区分实时与后台日志处理。

**价值**：不要重新把分类日志和工业运行消息设计成仅`WriteLine(string)`；但新平台应明确session、结构化字段、资源释放、容量与订阅生命周期。

## 5. CxVisual复用映射（建议，不等同于已批复的Owner）

| 目标项目 | ccxx参考与复用模块 | 推荐方式 |
|---|---|---|
| `CxVisual.Foundation.Core` | `cxglobal/cxcontainer/cxstring/cxtime/cxuuid` | 无状态函数按需移植/适配；不整体引入全局宏 |
| `CxVisual.Foundation.Numerics` | `cxmath/cxbuffer/cxbitmap/cxcrc` | 数值及字节算法可验证后复用；坐标/几何另依新规格 |
| `CxVisual.Foundation.Data` | `cxfile/cxcontext/cxprofile/cxrapidjson/cxtinyxml` | 数据介质与序列化转换；保持现有格式兼容 |
| `CxVisual.Foundation.Infrastructure` | `cxthread/cxtimer/cxprocess/cxprocmutex/cxlog` | 复用C++ Native执行实现或借鉴生命周期/日志结构 |
| `CxVisual.Foundation.Transport` | `cxsocket/cxbuffer/cxsharememory` | 传输/IPC底层技术；区分契约与执行实例 |
| `CxVisual.Mechanism.Connectivity` | `cxchannel* / CxChannelRoad / Factory / Observer` | 强烈建议先研究已有完整Channel分层，采用Adapter/服务层集成 |
| `CxVisual.Mechanism.Data` | `cxdatabase/cxprofile/cxbusiness` | 数据接入和模型事件，按Owner重构而非整体搬迁 |
| `CxVisual.Mechanism.Core` | `cxdescribe/cxcontext/cxbusiness` | 属性元数据/配置思想复用；唯一CxNode始终由新Core维护 |
| `CxVisual.Mechanism.Runtime` | `cxapplication/cxtimer/cxlog/cxprocess` | Native进程/服务运行参考；不将其全局循环嵌入WPF UI线程 |
| `CxVisual.Mechanism.Logic` 及 `Professional.Automation` | `cxlua/cxpython/cxchannel` | 原有脚本与工业接口按需封装，留意现场安全及部署版本 |

**不直接对应的方面**：ccxx不是专门的CT重建、AI视觉、三维几何或WPF图形系统；不能凭仓库包含Qt/OpenCV头或示例就宣称这些专业能力已由ccxx完整实现。

## 6. 稳定复用的工程门禁与需核查点

1. **模块粒度优先**：先选择`ccxx_simple`、`ccxx_env`等依赖子集，或保持C++静态/动态库；跨到C#时以明确C ABI/IPC/服务接口桥接，管理线程/内存/错误码/字符编码/调用约定。
2. **生命周期必验**：Channel在独立线程运行，必须测试重复open/close、断网重连、Observer移除、Timer停止、程序退出、Callback仍排队以及资源竞争。
3. **源码级待核问题（非已发布缺陷结论）**：[`cxchannel.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxchannel.h) 中 `CxChannelPublisher::removeObserver` 调用了 `std::remove`，但该行未见`erase`，应通过完整实现和单测确认订阅者是否真的从容器移除；[`cxprofile.h`](https://github.com/oudream/ccxx/blob/master/ccxx/cxprofile.h) 中 `hadLoaded()` 返回`_filePath.empty()`，需核对方法名与实际语义。**暂不直接对生产仓做修改**。
4. **版本与平台**：根CMake用C++11；有Windows、Unix、macOS等构建分支，但各目标的当前编译/运行兼容需实际测试。老配置示例可能含历史Qt/Python/OCI/ODBC依赖路径，禁止照搬到x3。
5. **“有接口”不等于“可用实现”**：例如`cxlz4.cpp`很小、`cxpacket.cpp`很小，须定位真实头内实现或可选实现；不可凭文件名宣称完整压缩/协议栈已验收。
6. **许可证与安全**：根MIT不自动覆盖随附第三方；MD5/XOR不作为新平台加密与密码保护基础；实际对外发布前审核版本、依赖、许可证及已知问题。
7. **真实验证**：目前已有大量`test/`源码，但本次未执行。移植时按目标功能跑相关测试、增加跨平台/跨语言回归、压力和异常恢复；保留结果与commit证据。

## 7. 后续聊天快速检索与实施入口

**若项目是通信连接**，立即看 `cxapplication.h/cpp`、`cxchannel.h/cpp`、`cxchannel_tcpclient.*`、`cxchannel_tcpserver.*`、`cxchannel_udp.*`、`cxchannel_serial.*`，再查看 `test/channel/`。

**若项目是运行、日志、生命周期**，看 `cxthread.*`、`cxtimer.*`、`cxprocess.*`、`cxappenv.*`、`cxinterinfo.*`、`cxlog.*`，再查看`test/application/`、`test/timer/`等。

**若项目是数据/描述/工程属性**，看 `cxcontext.*`、`cxprofile.*`、`cxdescribe.*`、`cxdatabase.*`、`cxbusiness.*`，再查看`test/database/`、`test/skv/`等。

**每轮输出**：原文件及签名 → 新CxVisual目标项目/Owner → 复用方式（直接/Native封装/Adapter/重构/仅参考）→ 与旧代码的兼容性 → 编译/测试/异常证据 → 待定问题。新分支/commit变更时先重新核查相应源码。

## 8. 建议的下一步（无需现在修改生产代码）

- **P0 通信主链路**：以 `CxApplication + CxChannelBase + CxChannelTcpclient/Udp/Serial` 做详尽事件与生命周期图、对照Mechanism.Connectivity，验证关闭、重连、回调及对象释放。
- **P0 描述/持久化机制**：以 `CxDescribe + CxProfile + CxIGet/SetSkv` 审核CxVisual属性元数据、类型描述、配置和保存加载，避免两套权威模型。
- **P1 多语言复用契约**：梳理`cxsharememory`和公共线程/日志的Native边界，和 `cx-cct-x3` 现有 C ABI 规范对比。
- **P1 工程验证**：为计划复用的模块建立 CMake Windows x64 与 Linux 构建证据，补足断网/重连/关闭/性能基准；不要求未经任务授权就修改旧仓。

---

**更新原则**：这份档案是“可核实事实 + 源文件索引 + 明示建议”，不能替代四大规格、GitHub实时源码或CI日志。每次扩大审计范围时在此记录新来源、commit、实测信息和已裁决Owner，绝不把推测写成已实现。