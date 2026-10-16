# CxVisual · 统一配置开发者体验（三阶段收口 V2.3）

> 依据 V1.3 双项目 API 和案例审查、V2.2 收口；实际用例代码：`Tests/ArchitectureSmoke/ConfigDeveloperExperienceTests.cs`。
> 唯一归属：`Foundation/Data/Configuration`、`Mechanism/Data/Configuration`；没有第三个配置项目或第二套 ConfigManager。

## 第一阶段：字段语义、安全与来源

- `Get/Value/Section` 读取当前有效配置；`GetLatestSaved` 读取待生效保存版。
- `ExplainState` 的 Defaulted/Present 应按**具体字段的持久审计路径**判断。改了备注不应使曝光字段丧失默认来源；历史数据没有证据时不得虚构。
- `FCMConfigCatalog` 对 `[FCMConfigSensitive]` 和含敏感子字段的父对象/字典设置 `IsSensitive`，隐藏 `DefaultJson`；自动 Markdown 同样脱敏。
- 授权会话查询 `GetStatus`、`ReadCommittedAfter` 需 Audit 权限且满足字段敏感读取权限；`PrepareValue` 每次读取都重新授权。

## 第二阶段：简单开发者入口（现有机制的便捷封装）

```csharp
// Config 类型只需标注 [FCMConfigSection]，可使用任意合法深层 POCO、集合或验证 Attribute。
var cfg = service.ForMachine("Machine-A").Open<V13ConfigSample>();
int exposure = cfg.Value(x => x.ExposureMs);
var state = cfg.Explain(x => x.ExposureMs);  // 默认/前值/保存/生效/版本/来源
Func<int> readLive = cfg.PrepareValue(x => x.ExposureMs); // 预编译一次
using (var watch = cfg.Watch(x => x.ExposureMs, change => OnEffectiveChanged(change)))
{
    await cfg.UpdateAsync(x => { x.ExposureMs = 120; x.Note = "检测"; }, "调整采图", token);
}
// 已保存不一定已生效，实际任务边界由运行宿主显式触发。
```

### CT 多模型联合配置

```csharp
var group = service.OpenAtomicGroup("CT.Scan", register => {
    register.Register<V13ConfigSample>("Capture");
    register.Register<V13AsyncConfig>("Reconstruction");
}); // 一次注册并 Seal
await group.UpdateAsync(edit => {
    var capture = edit.Get<V13ConfigSample>("Capture");
    capture.ExposureMs = 130;
    edit.Set("Capture", capture);
}, "联合修改", token);
group.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
using (var shot = group.CaptureForTask("CT-001")) { /* 同一物理有效版本 */ }
```

### 原子多层配置

```csharp
var profile = new MVBConfigAtomicLayerProfile<V13ConfigSample>(service, "Recipe-A", specs);
await profile.UpdateAsync(edit => {
    edit.Set("Template", x => x.ExposureMs, 100);
    edit.Set("Machine", x => x.ExposureMs, 155);
}, "配方联合变更", token);
profile.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
using (var shot = profile.CaptureForTask("Batch-001")) {
    Func<int> pinned = shot.PrepareValue(x => x.ExposureMs);
}
```

以上 `service`、`specs`、`token`、`OnEffectiveChanged` 由调用方提供；测试中的 V13ConfigSample/V13AsyncConfig 是现存可编译 POCO。

## 第三阶段：四个可执行案例、并发性能基准

| 场景 | 测试关注 |
|---|---|
| 普通开发者 | `UpdateAsync`、`Watch`、字段级默认来源、保存/生效分离、重启来源保留 |
| X-Ray 双探测器 | Dictionary 动态成员、深层截面、批量更新、可变返回对象隔离、最新有效值读取 |
| CT 长任务 | 两种配置类型聚合成同一版本、一次应用、任务快照不随未来版本漂移 |
| 五层产品配方 | Template/Project/Machine/Product/Override、优先级、删覆盖后来源回退、旧任务固定 |

另有敏感字段 Catalog/Markdown/审计权限测试，以及 `PreparedReadAndConcurrency`：
预编译标量循环读取，输出 `CONFIG_BENCHMARK` 吞吐和耗时；并发读写时只允许看到完整旧/新有效版本。
**性能结果是可重复基线，不人为承诺毫秒阈值、无锁或硬实时。**

## 真实验收入口

- `Tests/ConfigFoundationSmoke/Program.cs`：独立构建两个 Data 项目及配置测试。
- `Tests/ArchitectureSmoke/Program.cs`：完整架构回归。
- GitHub Actions：配置公共底座独立编译与验收、CxVisual Config L1 Windows Build and Smoke、TestCenter Windows .NET48。

## 边界（未因这次三阶段而宣称全部完成）

Studio 完整属性面板、YAML/INI 原注释无损迁移、跨机 ReleaseManifest/ACK、实机 PLC/探测器验证、进程间持久化并发事务、工业硬实时性能保证，仍需要单独验收。不要把便捷入口和 CI 绿色等同于 V1.3 全部 Gate 通过。
