# Foundation F16/F19 + WPF公共控件 + FoundationWpf 测试里程碑

## 实施范围与唯一Owner

- F16: `Foundation/Infrastructure`；统一 `FLDLogRecord`，有界异步 `FLDLogService`、简洁 `FLDDedicatedLogger`、`FLDRollingFileLogSink`。
- F19: `Foundation/Infrastructure`；`FCRSystemMonitor`、`FCRSystemSnapshot`、`FCRProcessSnapshot`。CPU使用两次采样差分，权限不足的指标以null/Warning展示。
- WPF: `Mechanism/Presentation.Wpf`只承载 `LogViewerControl`、`SystemMonitorControl`，不在Foundation引用WPF。
- 测试：`Tests/FoundationSmoke`自动CLI断言、`Tests/FoundationWpf`独立WPF人工/压力测试。项目数从28到29，唯增测试项目，不修改F/M/P 135子方面。
- 配置仍归 `Foundation/Data`，本次不迁入INI/JSON/YAML格式服务。也不迁入设备通信或系统守护/自动重启功能。

## 专有日志（最小可用方案）

`FLDLogService.ForDedicated("M13", "PLC01").Warning("timeout")`。
- 同一条记录先写入公共 `Common/runtime-YYYYMMDD-NNN.log`，有专有名称再追加 `Dedicated/PLC01-YYYYMMDD-NNN.log`；`IsImportant`额外写入 `Important/important-YYYYMMDD-NNN.log`。
- 仅允许ASCII字母数字、点、下划线和短横线作为专有名称，防止路径遍历/意外文件覆盖。
- 最大文件8 MiB，Common/Dedicated 7日保留，Important 70日示例值（工业现场需按制度配置，不能默认代表审计合规）；清理严格匹配本日志名称+日期+分卷，拒绝碰触其它日志。
- 有界队列满时，普通日志立即拒绝并增加DroppedCount；重要日志最多等待2秒，仍可能失败，调用方必须处理false。不要把日志写入当成安全联锁/审计事务成功。
- Flush使用队列屏障等待，Dispose完成Adding后Drain；LastFailure保存真实存储/订阅异常，避免递归日志。
- 跨日/滚动时先关闭旧Writer，再创建新Writer。文件追加，不默认覆盖既有文件。

## 系统监控

- Windows .NET Framework 4.8：`GetSystemTimes` CPU累计差、`GlobalMemoryStatusEx`物理内存、`DriveInfo`本地固定盘、`NetworkInterface`状态和 `Process` 采样。
- 进程CPU占用使用`TotalProcessorTime`差除以时间和CPU逻辑数；按PID+可读的StartTime避免PID复用导致虚假跳变。
- 支持同步Snapshot、Start/Stop、周期Sampled、SamplingFailed以及Dispose；不进行杀进程、故障重启或业务健康决策。
- 只作资源监测，Windows权限、远程进程信息不可读取时必须提供空值/警告，不能伪造数据。

## Windows CLI验收

环境：VS2022/MSBuild17，Windows x64，.NET Framework 4.8 Developer Pack。
```powershell
.\Build\BUILD.cmd
.\Output\Debug\x64\CxVisual.Tests.FoundationSmoke.exe
.\Output\Debug\x64\CxVisual.Tests.FoundationWpf.exe
```
独立GUI：日志/专有文件按钮、重要事件按钮、Flush、系统/进程页面、后台压力测试1K和10K、非法日志路径拒绝。

必须观察：生成公共/专有/重要3类日志文件，日志轮转保留其他日志，关闭后文件可删除，UI不卡死，进程/CPU/内存信息，窗口关停线程释放。

额外验收（尚未有真实结果）：Windows实际编译、CLI退出码0、WPF启动与交互、长时间压力、磁盘满/拒绝写入、跨UTC日切换、10K/s、进程异常权限与关停竞态。
Linux工具环境没有Windows MSBuild/WPF，不应对这些项标记“PASS”。

## 本次与旧版相容原则

- 保留旧 `FLDLogRecord(string,string,string)` 和 `IFLDLogSink`、`FLDFileLogSink`、`FLDMemoryLogSink`；不修改应用的已有调用入口。
- 不复制旧 `ImportantEventLog` 的未释放Writer跨日切换、删除公共目录中无关文件、吞掉异常等问题。
- 不把上一版PR #1直接合并进master；本次从独立基线用一个功能提交构成审阅变更，避免按单文件多Commit。


## F16命名修订与实时日志（V6.4对齐）
公开日志基础类型：`FLDLogHelper`（静态便捷入口）、`FLDLogLevel`、`FLDLogRecord`、`FLDLogScope`、`FLDLogEventHandler`；状态服务：`FLDLogService`及`FLDDedicatedLogger`等为运行实现候选，已统一FLD前缀。原`Cx...`开发预览名不作为正式API。
示例：`FLDLogHelper.Initialize(logRoot);` → `FLDLogHelper.Info("启动");` / `FLDLogHelper.ForModule("CT3D.Recon").Information("完成");` / `FLDLogHelper.ForDedicated("M13", "PLC01").Warning("超时");` → `FLDLogHelper.Stop();`。
`FLDLogHelper.LogEvent`推送实时记录，WPF直接订阅`FLDLogService.RecordReceived`，后台线程回调应Dispatcher切换。
按`CallerFilePath`+规则匹配自动补模块，显式module优先，保留`CallerMemberName`。F19原规范只有FCR并发对象；本次`FCRSystemMonitor`等属于**待补录候选**，不能说已在V6.4表中定案。
