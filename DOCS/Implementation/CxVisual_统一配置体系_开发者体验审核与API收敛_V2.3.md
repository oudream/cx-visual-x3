# CxVisual 统一配置体系：开发者体验审查与 API 收敛 V2.3

> 依据：V1.3 的六种角色、27 组职责、24 项需求及当前 `master` 的真实代码。
> 决议：只在 `Foundation/Data/Configuration` 和 `Mechanism/Data/Configuration` 两个目录维护底座；不引入第二套管理器，不创建新解决方案项目。
>
> 区分：当前真实可用 API（已进入源码）与下一阶段目标 API（**尚未实现**）。

## 1. 开发者能否简单使用——结论

- **日常单实例：基本简单**。POCO+Attribute、`ForMachine().Open<T>()`、`Value/Section/Get`、`SetValueAsync` 可以直接调用。
- **多字段和订阅：已简化**。新增 `UpdateAsync(change, reason, token)`、`Watch(selector, callback)`（只看 Effective）、`WatchSaved`（只看 Committed），在原事务/通知管线之上封装，不创造第二种提交。
- **复杂日常操作：尚未够简洁**。跨层模型需组装 `MVBConfigLayerSpec`；聚合模型需手工 `Register<T>/Seal` 且成员由字符串 key 引用；诊断及历史分散在多个方法；授权编辑需要打开完整 WorkingCopy。
- **性能：不可直接假设**。当前 `Get/Value` 使用 JSON 副本及 Expression Compile；真正高频线程须以 `CaptureForTask` 取得固定副本/版本，尚需基准测量。
- **动态配置编辑：未完整**。SchemaCatalog 遇 IEnumerable 会在所属字段停止，动态列表、字典成员尚不具备统一编辑描述和类型化集合命令。

## 2. 实际可编译调用方式（当前版本）

```csharp
[FCMConfigSection("Vision.Capture")]
public sealed class CaptureConfig
{
    [FCMConfigItem("曝光", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
    public int ExposureMs { get; set; } = 80;
    public int Gain { get; set; } = 1;
}

// 初始化：宿主创建并共享一个服务，专业插件只引用服务接口/能力。
MHSConfigService service = new MHSConfigService(configRoot);
MHSConfigSection<CaptureConfig> cfg = service.ForMachine("Machine-A").Open<CaptureConfig>();

int exposure = cfg.Value(x => x.ExposureMs);
CaptureConfig isolated = cfg.Get();
FCMConfigValueInfo<int> detail = cfg.Explain(x => x.ExposureMs);

using (IDisposable sub = cfg.Watch(x => x.ExposureMs,
    change => Console.WriteLine(change.OldValue + "→" + change.NewValue)))
{
    MHSConfigCommitResult saved = await cfg.UpdateAsync(model =>
    {
        model.ExposureMs = 120;
        model.Gain = 2;
    }, "调整采图", cancellationToken);

    // NextTask: 保存成功不代表已生效；真实运行调度器在合适边界调用。
    MHSConfigApplyResult applied = cfg.TryApplyPendingAtBoundary(
        FCMConfigApplyPolicy.NextTask, null);

    using (MHSConfigSnapshot<CaptureConfig> task = cfg.CaptureForTask("Scan-001"))
    {
        int pinned = task.Value(x => x.ExposureMs);
    }
}
```

**说明**：`configRoot` 与 `cancellationToken` 由宿主提供，`CaptureConfig` 为示例 POCO；本例不含设备驱动或联锁。若需要保存值而非生效值订阅，使用 `cfg.WatchSaved(...)`。

## 3. 当前已修复的 P0/P1 易用性与安全问题

| 分类 | 改进 | 行为约束 |
|---|---|---|
| P1 少写代码 | `UpdateAsync(Action<T>)` | 与现有草稿事务同一套验证、CAS、审计、策略 |
| P1 常用订阅 | `Watch` / `WatchSaved` | 默认 Effective，未生效保存不会错误通知运行线程 |
| P0 最小权限 | 只读者可打开混合敏感模型，并按路径读非敏感字段 | `Get` / 全模型快照仍强制 `ReadSensitive` |
| P0 元数据保护 | 敏感属性及含敏感子项的父节点不导出默认值 | `FCMConfigFieldInfo.IsSensitive` 标识脱敏 |
| P0 状态/审计保护 | `GetStatus`、`ReadCommittedAfter` 增加完整读取检查 | Audit 本身不授予读取原始敏感配置权限 |

## 4. 仍应优化的日常 API——目标而非现有实现

### P0 基础安全和正确性（先做）

1. **跨进程生效 CAS**：有效标记有预期版本、独占锁、崩溃恢复；适配一台机两个上位进程同时 Apply。
2. **固化层级定义**：各层名称/优先级/用途、成员类型和策略随保存修订入档，重启发生定义漂移时拒绝误解释。
3. **敏感字段全链路**：Schema、日志、Diff、订阅、校验错误文本、备份、导入导出统一敏感策略；不能只靠 SDK 使用约定。
4. **不可伪造的权限边界**：非可信插件只能拿授权能力；不允许同时拿裸 Service；跨进程调用另用宿主身份认证。

### P1 让开发者不关心内部复杂度

5. **Scope/读取诊断统一**：建议 `cfg.Explain(selector)` 内完整返回值状态/默认/来源/之前保存/之前生效/候选及各版本，不要求开发者组合多个诊断对象；`ExplainState` 可作为简单投影。
6. **聚合成员强类型化**：目标 `group.Get<CaptureConfig>(key)` 的 key 不再散落字符串，使用成员描述/注册合同或泛型组定义，注册后自动 Seal；只复用 `MHSConfigAtomicGroup` 真源。
7. **层级档案入口收敛**：开发者不应为了改机台曝光理解两种 LayerProfile 的差别；一处发现层级方案、一次打开及管理，保留独立/原子两种已定义语义但明确配置策略。
8. **编辑模型覆盖动态集合**：Schema 中的 Dictionary/List/Set 提供 Key、元素类型、增加/删除/排序、校验位置、来源和默认状态；Studio 按通用描述生成控件，而不是写特例。
9. **规则依赖与注册前类型验证**：所有注册的配置类型经 TypeCatalog 和依赖图统一验证；校验执行器按字段变更触发受影响规则，跨字段/异步规则有明确预算。
10. **高频只读视图**：工控热路径避免每次 JSON 克隆/重新 Compile；先明确不可变视图与生命周期，再做性能基准，禁用越过有效版本的直接写入。

## 5. 使用体验验收（比“有多少类”更有效）

1. **初次接入**：只需 1 个配置 POCO、宿主共享 Service、1 行 Open；不需要手写文件 IO、JSON、反射或 UI 目录。
2. **日常修改**：一行单值、一个 Action 多字段；返回 `Success/ErrorCode/ChangeSet/Revision/PendingApply`；失败不污染原配置。
3. **订阅和取版本**：1 行 Watch；Old/New/Default/版本正确；NextTask 保存后不提前通知 Effective。
4. **多层/多模型**：采用强类型成员 + 明确原子边界；调用方不用关心持久 Journal 的具体文件。
5. **编辑器**：六层嵌套、字典动态成员、可空字段、正则、自定义规则、Undo/Redo、来源与敏感脱敏可直接从 Schema/EditorModel 驱动。
6. **安全**：只有 Read 可读普通字段；全模型含敏感部分要求 ReadSensitive；撤销权限后续调用和订阅均无信息旁路。
7. **性能和可靠性**：多进程并发、磁盘写失败、断电点模拟、配置迁移、慢消费者以及长期任务版本固定都有可重复运行结果。

## 6. 实施取舍

先完成 P0 的并发生效、定义漂移保护和安全覆盖测试，再完成 P1 的“统一诊断、强类型组、动态集合编辑、增量校验和高频读取”。
Legacy 格式迁移、Studio 窗口、真实设备 Readback、远程 ReleaseManifest 不进入本轮开发者基础 API 完成定义，也不可标记为已通过。

审核原则：不能新增一个开发者 API 就新增一套管理体系；每个 API 应当缩短典型用法并且保留现有运行语义及回归验证。
