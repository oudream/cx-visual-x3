# F16 · 独立同步专有文件日志 V1（CxVisionX2 重构）

## 来源与归属

- 参考：`cx-vision-x2/Common/CommonInterfaces/ImportantEventLog.cs`、`LogHelper.cs` 的 `EventLogHelper` 和 `LOG_TYPE`。
- V6.4 `F_Infrastructure` 已有候选：`FLDEventLogHelper`、`FLDImportantEventLog`、`FLDImportantEventLogEntry`、`FLDLOG_TYPE`。
- Owner：`Foundation/Infrastructure` F16。与异步 `FLDLogService` 完全独立，**不共用Writer、队列、线程或实时LogEvent**。

## 程序调用

```csharp
FLDEventLogHelper.Init(logRoot, retentionDays: 70, flushOnWrite: true);

FLDEventLogHelper.Information(FLDLOG_TYPE.Mes, "Upload", "MES", "上传成功");
FLDEventLogHelper.Error(FLDLOG_TYPE.Panda, "Request", "Panda", "接口失败");
FLDEventLogHelper.SetLevel(FLDLOG_TYPE.PLC, FLDLogLevel.Warning);

// 可扩展专用日志类型，不必修改已有枚举：
FLDEventLogHelper.Information("CT3DRecon", "Reconstruction", "CT3D", "完成");

// 包含明确的操作者和额外字段，可请求更强磁盘刷新：
FLDEventLogHelper.Write(FLDLOG_TYPE.Important,
    new FLDImportantEventLogEntry {
        Level = FLDLogLevel.Warning, EventType = "ConfigChanged",
        Source = "CT3D", User = "operator01", Description = "配置变更"
    }, flushToDisk: true);

FLDEventLogHelper.Release();
```

## 行为与边界

- **真正同步**：调用线程执行`FileStream/StreamWriter`写入；默认每条记录`StreamWriter.Flush()`；可选`FileStream.Flush(true)`，不承诺在任意硬件断电场景保证持久化。
- **文件独立**：每个类别在`logRoot/<类别>-yyyyMMdd.log`中按本地日期追加；惰性创建文件；不会发送到普通异步日志或WPF实时事件。
- **级别控制**：全局及类别级阈值；被过滤则返回false；I/O错误直接向调用方抛出；绝不报告写入成功。
- **生命周期**：显式Init、Flush、Release；同一类别复用Writer；跨日先释放旧Writer，在同一锁内切换、写入和Flush。
- **历史清理**：只清理当前类别、严格以`<类别>-yyyyMMdd.log`匹配的到期文件；不能删除别的日志。
- **兼容迁移**：`FLDLOG_TYPE`保留Mes、Panda、Statistics、PLC、HLib、Radiation、ResFeeback等旧值及拼写；`Relase()`作为带Obsolete标记的兼容入口。
- **上下文**：Entry支持EventType、Source、Description、User、MemberName、ExtraData。User须由业务明确传入；禁止硬编码“admin”。日志文本转义控制字符，但不自动脱敏Token，请调用方控制敏感信息。

## 测试与验收

- `FoundationSmoke/SynchronousLogTests.cs`：MES/Panda隔离、同步可读、门限过滤、用户和键值字段、无事件推送、跨日关闭旧Writer、并发200条、保留只清理自身、非法文件名、I/O异常传播、Release和重新Init追加。
- `FoundationWpf`：同步MES、同步Panda两个操作按钮，直观验证同步落文件但不在LogViewer显示。
- 目标环境Windows VS2022 / MSBuild 17 / .NET Framework 4.8：执行 `Build\BUILD.cmd` 再手动启动 `CxVisual.Tests.FoundationWpf.exe`。
- **未在当前运行环境完成Windows .NET Framework 4.8真实编译、CLI/WPF动态验收**。源码/工程结构检查并不等于Windows验收通过。

注：FLDImportantEventLog名称沿用V6.4，但本实现可用于MES、Panda等任意同步专有事件，不专指“重要事件”；业务完成、安全联锁的确认不能由日志写入成功替代。
