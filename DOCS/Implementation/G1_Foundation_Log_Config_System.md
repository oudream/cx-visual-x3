# G1 · 基础公共能力第一批：日志、配置、系统环境

> 代码分支：`feature/foundation-logging-g1-20261014`（审核用，未合并）。
> 依赖基线：`DOCS/Codebases/01_ccxx.md`、`ccxx/02_元数据与配置.md`、`ccxx/03_工业信息与日志.md`、工程规范V1.0，及当前x3源码。
> `02_device_communicator.md`、`03_gcl3.md`仍在其他对话整理，不把未提交的结论视为已审核事实。

## 1. G1 日志已落实的范围

Owner：`CxVisual.Foundation.Infrastructure` (F16)。在现有四个类型（`CxLogRecord`、`ICxLogSink`、`CxMemoryLogSink`、`CxFileLogSink`）上增量实现，兼容原三参数记录、单参数文件Sink、参数为空的内存Sink。

- 新增`CxLogLevel`：Debug/Information/Warning/Error/Critical。
- `CxLogRecord`：显式ModuleId、CorrelationId、Source、IsImportant、异常文本与可读结构化输出；旧`ToString`形式保留。
- `CxLogService`：实例级路由；Severity阈值；多Sink+实时订阅；回调在内部锁外；单个Sink/Observer失败不阻断其他接收者，并将所有异常汇总抛出。调用方不得让控制/安全链上的记录异常变成未处理异常。
- `CxMemoryLogSink(maxRecords)`：容量约束、过期数量与快照。为保持老入口兼容，无参数构造仍不设上限；新生产代码须使用正数容量。
- `CxFileLogSink(path,structured)`：新增结构化输出；旧构造/文本格式保留；同步AutoFlush而非已实现的异步/轮转。
- `CxImportantEventFileSink`：独立文件，仅持久化显式`IsImportant`的记录；普通日志与重要事件路径由宿主配置，避免按源文件名猜专业分类。

## 2. 尚不能宣称完成的能力

- 尚未实现：有界异步写队列/丢弃与退避策略、按日/大小轮转、Retention、查询索引、操作审计的完整不可抵赖保证、敏感数据脱敏、UI过滤界面、Native ccxx日志桥接。
- 未修改：cx-vision-x2、ccxx、gcl3、device_communicator仓库；不直接复刻它们的全局静态运行管理器。
- 当前环境未运行Windows MSBuild、WPF界面、磁盘满/掉电测试、10000条/秒负载与故障恢复测试。当前提交只是G1功能候选，验收状态待真实命令输出确认。

## 3. 第二批：配置体系（F14/F15/F18 → M04/M07/M39）

1. `Foundation.Data`：INI/JSON/XML格式Codec、路径规则、文件原子写入。纯格式读写不拥有业务工程`CxNode`。
2. `Mechanism.Core`：Property/Metadata/Type定义与有效性；唯一工程真源，不另建旧`CxDescribeProject`业务树。
3. `Mechanism.Lifecycle`：SchemaVersion与Migrator、备份/预览/回滚。
4. 宿主层：明确`DataRoot`、`AppData/Configs`及优先级；跨版本共享文件须检查前后兼容。
5. Golden测试：中文/Unicode、未知字段、空section、错误格式、写入中断、两个进程并发变更及版本冲突。不得把`load()`失败误判为已初始化成功。

## 4. 第三批：系统信息与进程基础（F19 → Runtime Host/Mechanism.Runtime）

- Foundation提供平台/CPU/内存/进程基础**信息读取及结果值**；系统调用失败返回明确诊断，不控制设备。
- Runtime/宿主管理AppData定位、单实例、工作线程及原生Worker启动/停止、取消、健康检查、资源预算和权限。
- 与ccxx的Process/Thread/SharedMemory功能优先通过已有稳定C ABI、IPC或服务化适配，绝不跨.NET长期保存未定义生命周期的C++对象指针。
- 单机多版本`App-Vx.y`平级、共享`AppData`；不新增第29个项目。
- Golden测试：无权限数据根、磁盘满、进程退出、共享内存版本不匹配、多线程关停、非法路径及信息采样失败。

## 5. 本批次验收门禁

- `CxVisual.Foundation.sln`与`CxVisual.Unified.sln`在Windows x64、.NET Framework 4.8、VS2022/MSBuild17下真实编译；
- `Tests/FoundationSmoke`返回0，包含等级过滤、回调中注销、异常汇总、容量淘汰、重要事件落盘；
- 额外需实现并验证：多线程写入、每秒高峰吞吐、日志切换/持久性以及运行态异常隔离。
- 仅在保存实际Build.log、测试命令与输出、版本环境之后，才把相应项目标为“已验收”。

