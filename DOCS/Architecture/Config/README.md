# CxVisual｜开发者期望的极简配置使用体验 V1.1

> 日期：2026-10-15
> 身份：**API 目标样例 / 需求设计**，不是现有 V7.8 功能声明、也不是正式 API 冻结。
> 承接：《CxVisual_统一配置管理与开发者体验_需求基线_V1.0.md》。
> 前提：配置归属仍服从 Foundation / Mechanism / Professional，不额外建立项目真源。
> 目标：L1 的使用体验足够好，同时为 L2–L4 预留同一模型的扩展。

## 一、开发者最希望只写这点代码

**定义完配置类以后，新增一个算法或设备，开发者不应该编写 ConfigRepository、JsonLoader、ConfigEditor、独立 ConfigChangedEvent、全局单例或重复注册菜单。**

```csharp
// 统一入口：已在平台装配阶段自动扫描并注册 [ConfigSection] 配置类。
CxConfigSection<XRayLineConfig> cfg = CxConfig.ForMachine("Machine-A")
    .Open<XRayLineConfig>();

// 整体 / 截面 / 单值
XRayLineConfig all = cfg.Get();
CaptureConfig part = cfg.Section(x => x.Devices.Detectors["Detector-01"].Capture);
int ms = cfg.Value(x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);

// 默认、上一保存、上次有效、最新保存、当前有效、来源、版本一次取得
CxConfigValueInfo<int> info = cfg.Explain(
    x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
Console.WriteLine($"{info.DefaultValue} / {info.PreviousSavedValue} / " +
                  $"{info.PreviousEffectiveValue} / {info.LatestSavedValue} / {info.EffectiveValue}");

// 修改三处：无需手写保存/校验/差异/通知代码
using (CxConfigEdit<XRayLineConfig> edit = cfg.BeginEdit("优化检测"))
{
    edit.WorkingCopy.Devices.Detectors["Detector-01"].Capture.ExposureMs = 120;
    edit.WorkingCopy.Vision.Algorithms["VoidDetection"].Threshold = 2100;
    CxConfigCommitResult result = await edit.CommitAsync(cancellationToken);
}

// 变更订阅，提供旧值、新值、默认值、来源及版本；IDisposable 取消订阅
using (IDisposable subscription = cfg.Subscribe(
    x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs,
    CxConfigChangeStage.Effective,
    change => Console.WriteLine($"{change.OldValue} → {change.NewValue}")))
{
    // 订阅作用域内处理配置生效通知。
}
```

这些为**候选接口名**，其中 `CxConfig`、`CxConfigSection<T>`、`[ConfigSection]`、`[ConfigItem]`、`CxConfigValueInfo<T>`、`CxConfigChangeStage` 等应与现有工程的 F/M/P API 统一审核。不是要求按示例名称机械新增多个项目或第二套配置对象树。

## 二、丰富数据类型、五六层嵌套的真实业务模型

详见 `01_XRayLineConfig.cs`。覆盖：

| 需求 | 模型体现 |
|---|---|
| 基本数值 | `int`、`double`、`decimal`、`bool`、`string` |
| 枚举、时间、URI | `TriggerMode`、`PixelFormat`、`TimeSpan`、`DateTimeOffset?`、`Uri` |
| 常用集合 | `List<T>`、`Dictionary<string,T>`、`double[]` |
| 自定义结构 | `NumericRange`、`RoiConfig`、`DetectorCalibrationConfig` |
| 多层结构 | `XRayLineConfig → Devices → Detectors["Detector-01"] → Capture → Crop → Width` |
| 动态实例 | `Detectors["Detector-01"]` / `["Detector-02"]`，以及 `Algorithms["VoidDetection"]` |
| 稳定对象标识 | `AxisConfig.Id`、`DefectRoiConfig.Id` 配 `[ConfigKey]`，列表重排仍可解释差异 |
| 元数据 | 名称、单位、最小最大值、编辑器、下一任务生效策略 |

大型图像、CT 投影、体数据、GPU 缓冲和复杂二进制对象 **不作为普通配置值深拷贝或持久化**；这里只保存它们的资源引用、参数和验证约束。

### 自动发现应是什么体验

1. 开发者只编写带 `[ConfigSection]` 的配置类型，构建并加载专业程序集。
2. 平台插件宿主统一扫描（程序集加载/卸载时同步更新注册），建立配置 ID 和类型映射，检测重复、冲突与版本迁移需求。
3. 首次打开工程或机台，自动创建安全默认配置并校验；关键设备配置缺失则提示并阻止危险启动。
4. Studio 根据属性类型和元数据自动生成导航、搜索、表单、枚举、范围、单位、List/Dictionary 子表及来源链诊断；专业无需编写通用 ConfigPage。
5. 专业可替换个别复杂属性的编辑器，但不得重做通用存储和变更系统。

## 三、同一条配置路径：读整体、截面、单值、元信息

```csharp
// 深层字符串路径应该只作为外部 API/诊断与导入导出备用；C# 优先 Lambda 强类型访问。
XRayLineConfig root = cfg.Get();
DetectorConfig detector = cfg.Section(x => x.Devices.Detectors["Detector-01"]);
CaptureConfig capture = cfg.Section(x => x.Devices.Detectors["Detector-01"].Capture);
int exposureMs = cfg.Value(x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
CxConfigValueInfo<int> detail = cfg.Explain(
    x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
```

**语义约束**：`Get()/Section()` 返回脱离配置真源的读取副本，普通对象可写属性不代表修改属性就能持久化；写回一律走 `BeginEdit()/CommitAsync()`。单值读取及任务快照需避免重复复制整个深层对象。热路径通过已固定的不可变快照或编译路径访问器取得，不能每帧做反射查找、JSON 反序列化或磁盘 IO。

## 四、上次值/默认值/保存值/生效值：绝不能混用

| 字段 | 定义 | 例子：Detector-01 / ExposureMs |
|---|---|---|
| `DefaultValue` | 类型/默认层定义的基础值；不是当前有效配置 | 80 ms |
| `PreviousSavedValue` | 最新保存版本之前那一保存版本中的值 | 100 ms（仅演示） |
| `PreviousEffectiveValue` | **最近一次生效切换前**的有效值；不是当前草稿或编辑副本 | 80 ms（仅演示） |
| `LatestSavedValue` | 已通过校验、保存成功的**最新提交值**，可尚未生效 | 120 ms |
| `EffectiveValue` | 指定运行作用域当前允许采用的有效值 | 100 ms（等待下一任务） |
| `EffectiveSource` | 当前有效值来源层，如设备模板/机台/运行覆盖 | 设备模板（仅演示） |
| `SavedVersion` | 最新已保存配置修订号 | v19 |
| `EffectiveVersion` | 当前运行作用域采用的修订号 | v18 |
| `ApplyPolicy` | 即时、下一帧、下一任务、重连、重启等 | NextTask |

例如提交 ExposureMs 由 100 到 120：保存成功后 `SavedVersion=v19`、`LatestSavedValue=120`；如果应用策略为 `NextTask`，正在执行的任务仍以 `EffectiveVersion=v18`、`EffectiveValue=100` 为准。只有任务边界真正应用后才发布 **Effective** 阶段变更：`OldValue=100`、`NewValue=120`。截图示例的 80/100/120、v18、Machine Profile 仅作展示，不能写死为平台所有来源的优先级。

**历史扩展**：`PreviousSavedValue` / `PreviousEffectiveValue` 只保留最近一次保存/生效的前值；任意旧版必须通过带版本号的历史查询接口，不能靠单个 `Previous` 替代版本库。区分未设置与设为 null 的状态，并规定初次生效时 `HasPreviousValue=false`。

## 五、订阅：单值 / 配置截面 / 整体 / 事件阶段

详见 `02_DeveloperUsage.cs`。希望统一具有：

```csharp
// 生效通知：适合设备、算法、任务 Runtime 的安全边界
IDisposable a = cfg.Subscribe(
    x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs,
    CxConfigChangeStage.Effective,
    change => ApplyExposure(change.NewValue));

// 保存通知：适合 Studio/诊断、审批后刷新，不意味着设备立即修改
IDisposable b = cfg.SubscribeSection(
    x => x.Vision.Algorithms["VoidDetection"],
    CxConfigChangeStage.Committed,
    change => AuditChangedPaths(change.ChangedPaths));

// 订阅者必须 Dispose；不要隐式永久挂在静态事件上。
a.Dispose();
b.Dispose();
```

`ApplyExposure(...)` 和 `AuditChangedPaths(...)` 是调用方示意；设备 IO 不应在配置通知线程直接执行，应由 Runtime 根据安全联锁与生效策略调度。通知包含路径、Old/New、默认值、版本、来源、相关事务号、时间戳、提交/生效阶段。一个事务中的多个字段变更拥有同一提交版本与事务 ID；截面通知一次批量给出完整变更集，不是无序的多个碎片事件。

## 六、批量编辑与原子提交

```csharp
using (CxConfigEdit<XRayLineConfig> edit = cfg.BeginEdit("更新机台 M-A 的检测方案"))
{
    edit.WorkingCopy.Devices.Detectors["Detector-01"].Capture.ExposureMs = 120;
    edit.WorkingCopy.Vision.Algorithms["VoidDetection"].Threshold = 2100;
    edit.WorkingCopy.Vision.Algorithms["VoidDetection"].MinAreaMm2 = 0.30m;

    CxConfigPreview preview = edit.Preview(); // 自动差异、范围、引用和跨字段校验
    if (preview.Validation.IsValid)
    {
        CxConfigCommitResult result = await edit.CommitAsync(cancellationToken);
        if (!result.Success) Console.WriteLine(result.ErrorMessage);
    }
}
```

不能以“修改 `cfg.Get()` 返回对象属性”代替事务提交。要求旧版本冲突检测（乐观并发）、变更预览、必要时权限审计、先验证再持久化、持久化失败继续保留上一稳定版本、崩溃恢复、撤销/恢复可追溯。Studio Undo/Redo 是编辑事务操作，不等于删除已经使用过的历史版本。

## 七、任务快照：保证采图/算法/CT 一致性

```csharp
using (CxConfigSnapshot<XRayLineConfig> pinned = cfg.CaptureForTask("Scan-001"))
{
    int exposure = pinned.Value(x => x.Devices.Detectors["Detector-01"].Capture.ExposureMs);
    int threshold = pinned.Value(x => x.Vision.Algorithms["VoidDetection"].Threshold);
    Console.WriteLine("任务使用配置版本：" + pinned.Version);
    // 同一任务中全部读取固定使用该版本；后续提交不会改变 pinned。
}
```

`CaptureForTask` 是 L3 目标，但 **L1 底层模型就要保证以后能添加固定快照，而不改专业类的数据定义**。默认 `Get/Value` 返回的“有效值”具体作用域必须显式可查；后台任务不能偷偷拿编辑草稿或最新保存但尚未生效的值。

## 八、开发者应当负担多少代码？

| 任务 | 开发者理想投入 | 平台负责 |
|---|---|---|
| 定义新配置 | 1 个 POCO + 少量特性 | 自动发现、初始化、校验、存储、编辑 UI |
| 读单个值 | **1 行** | 强类型、Scope 解析、来源覆盖 |
| 读截面 | **1 行** | 安全读取复杂嵌套树 |
| 查四类状态及来源 | **1 行获取对象** | 保存/生效历史、诊断 |
| 订阅变化 | **1 次调用 + IDisposable** | 生命周期、旧/新值与阶段、批量一致性 |
| 改几个值 | **编辑作用域 + 属性赋值 + 一次提交** | 差异、校验、保存、冲突、通知 |
| 自动生成 Studio 配置 UI | **0 行专业 UI 样板** | 元数据/树/表单/集合编辑器/搜索 |
| 固定任务参数 | **1 次 Capture** | 快照、一致性、生效策略、版本追踪 |

## 九、功能不等于一堆新类（最小核心机制）

在最新 F/M/P 体系中，应优先考虑在现有能力对象上复用/融合这些职责：

1. **定义与 Schema**：ConfigDescriptor、MemberDescriptor、类型约束和自动发现（只需要一套配置身份与 Schema）。
2. **存取与版本**：ScopeResolver、Provider/Persistence、版本历史、迁移与恢复（来源链可诊断）。
3. **工作副本与变更**：快照、EditTransaction、Validation/Diff/Commit、统一变更通知。
4. **运行生效与发布**：ApplyPolicy、RuntimeSnapshot、TaskPin、发布/兼容/回滚（L3/L4 在同一模型上增加）。
5. **Studio 投影**：公共配置编辑器、集合/嵌套类型编辑和诊断；不产生第二套持久化模型。

这些均为**能力职责词**，不是要求逐项新建对应类或 `.csproj`。配置应链接到现有 Project / Resource / Device / Professional 领域身份，避免 `ConfigurationProject` 成为第二棵工程对象树。

## 十、建议的第一批验收

1. 仅添加 `XRayLineConfig` 类，宿主自动发现，Studio 导航与编辑器出现；探测器集合、ROI、Axes、Algorithms 都能增删改。
2. 含枚举、列表、字典、数组、TimeSpan、Uri、decimal、Nullable 的保存→重载→强类型取值不丢失、不串值。
3. `Get`/`Section` 不允许直接修改真源；同一配置值可通过 lambda 强类型读取、按路径定位和元信息解释。
4. 有效值诊断正确区分 80/100/120 ms、来源链、版本和应用策略；支持运行覆盖不存在的情况。
5. 单值订阅旧/新值正确；截面订阅把同一提交的全部变更汇聚为一批；退订后没有通知。
6. 多字段提交包含三项修改且具备原子性；失败、类型校验失败、冲突时有效配置与旧持久版均保持不变。
7. Studio 编辑 Undo/Redo、提交差异预览、搜索与定位达到普通配置 0 专业 UI 样板代码。
8. 当前采图任务固定上一有效版本，配置提交并在下一任务切换后才产生生效通知；无重建线程上直接 IO。
9. 断电/磁盘错误/版本迁移错误/插件卸载/实例删除均有验证；无订阅泄漏和失效配置残留。
10. 用 .NET Framework 4.8 + 当前实际工具链完成真正的 Build/Test/运行和数据重放，再标记 PASS；本设计稿不宣称已经实现。

**设计准则**：专业开发者只描述配置数据与专业特殊约束，通用加载、保存、发现、查找、事务、快照、通知、版本与 Studio UI 必须可复用，使用时尽量一行完成一项操作。
