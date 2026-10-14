# CxVisual 统一配置 L1 · 实现基线与TDD验收（2026-10-14）

> 本文记录**仓库代码中已实现的接口和验收用例**；不是L2～L4发布、实时配置与运行生效已经完成的声明。唯一Owner按现有28项目，不新增项目。Windows/.NET Framework 4.8、C# 7.3，使用传统非SDK.csproj。

## 1. 开发者体验

```csharp
public sealed class ReconOptions
{
    [CxConfigItem("GPU序号", Category = "重建", Min = 0)]
    public int GpuIndex { get; set; }
    [CxConfigItem("迭代次数", Category = "重建", Min = 1, Max = 2000)]
    public int Iterations { get; set; } = 100;
}

CxConfigManager config = new CxConfigManager(configRoot);
config.Register<ReconOptions>("CT.Reconstruction");
config.Initialize(); // 首次创建默认配置，后续加载；损坏或不兼容文件绝不偷偷覆盖
ReconOptions snapshot = config.Get<ReconOptions>("CT.Reconstruction"); // 防止外部偷改
config.Update<ReconOptions>("CT.Reconstruction", x => x.Iterations = 200); // 校验+版本+通知，暂不写盘
config.Save("CT.Reconstruction"); // 有脏版本才持久化
config.UpdateAndSave<ReconOptions>("CT.Reconstruction", x => x.Iterations = 300);
// WPF应用可以调用：CxConfigEditorDialog.Edit<ReconOptions>(owner, config, "CT.Reconstruction");
```

- 安全关键配置：`Register<T>("Machine.Safety", requireExisting: true)`，缺失时拒绝自动给默认值。
- 复杂约束：`Register<T>("Network", validator: x => ...)`，在属性范围校验后执行。
- 并发：`Update(id, edit, expectedRevision)`支持乐观版本；跨进程保存时锁定 `.lock` 文件并校验磁盘Revision，冲突拒绝覆盖。
- 通知：`Subscribe<T>(id, (snapshot,change)=> ...)` 返回 `IDisposable`，调用方需在生命周期结束时Dispose。订阅者得到独立快照，回调异常由`NotificationFailed`报告，不能回滚已经提交的内存更新。
- 编辑：通用WPF编辑器支持数字、布尔、字符串、枚举、分类/标题/范围；复杂集合显示不可直接编辑，专业编辑器扩展留到后续。
- 文件：`<Id>.json`，文档有Id/SchemaVersion/Revision/Data，单文件同目录临时文件写入+排他锁+Windows File.Replace并生成`.bak`；不是多文件事务。
- L1不包含在线机台运行生效、跨进程发布、高频参数路径。现阶段`Get<T>`是克隆快照，**不应在硬实时循环内高频调用**；L3接入常驻无锁快照。

## 2. Owner及源码

| Owner | 本次真实文件 | 职责 |
|---|---|---|
| Foundation.Data F15 | `Foundation/Data/CxConfigJsonFile.cs` | JSON文档、克隆、文件读写、磁盘版本冲突、原子替换和备份 |
| Mechanism.Core M04 | `Mechanism/Core/CxConfigDescription.cs` | 属性元数据、范围、必填、UI无关描述 |
| Mechanism.Core M07 | `Mechanism/Core/CxConfigManager.cs` | 注册、默认初始化、内存草案、版本提交、通知、保存 |
| Presentation.Wpf | `Mechanism/Presentation.Wpf/CxConfigEditorDialog.cs` | 公共配置编辑器，不修改持久化/运行态边界 |
| Studio | `Applications/Studio/StudioDemoConfig.cs` 与按钮 | 实际可点击的配置演示 |
| ArchitectureSmoke | `Tests/ArchitectureSmoke/ConfigL1Tests.cs` | 可执行行为验证 |

优先与V6.4 F15的`ConfigKey/ConfigSchema/SettingsSnapshot`及M04属性元数据进一步去重；本轮公开类型是可用L1 API实现，不擅自宣称所有候选已获批。

## 3. 测试驱动及门禁

先提交TDD测试合同，再提交实现。架构烟测入口调用`ConfigL1Tests.Run()`。用例包含：
1. 默认配置自动写盘和重载；2. 强类型Get隔离；3. 无效修改拒绝和Revision不变；
4. 预期版本冲突；5. 多实例隔离；6. 变化通知及Dispose；
7. 已提交未保存与保存版本分离；8. 专业跨字段校验；9. JSON损坏/未来Schema不覆盖；
10. 保存失败不发布新状态、不破坏旧文件；11. 跨进程磁盘版本冲突；
12. 关键文件缺失拒绝启动；13. 编辑器描述与范围校验。

建议Windows环境运行：

```powershell
.\Build\BuildAndTest.ps1 -Configuration Release
# 或 .\Build\BuildAndTest.ps1 -Configuration Debug -ShowGUI
```

GUI手工闭环：Studio→点击`配置编辑 (L1)`→编辑合法参数→验证并保存→关闭重开→检查值；尝试越界值显示错误并保持旧有效版本；两窗口/两进程并发更新应阻止旧版本覆盖。

**范围限制**：测试程序是有断言的零第三方测试框架烟测，不等同覆盖率证明；CI与Windows执行结果以GitHub Actions和本地MSBuild真实日志为准。不能把“写了测试”写成“测试通过”。

## 4. L2～L4不可破坏的契约

SchemaVersion、配置逻辑Id、Revision、强类型有效快照、草案修改、受控提交、保存与订阅在L1建立。未来Scope/Recipe、Manifest、RuntimeApplied版本、实时参数必须在这些身份和版本语义基础上扩展，不另外实现一套平行ConfigManager；现场设备的运行生效成功与文件保存成功严格分离。


## 5. L1专业配置复用验收（第二轮：2026-10-14）

本轮在原有28项目内增加**三种专业强类型配置案例**。它们使用同一个`CxConfigManager`、同一套F15 JSON存储、M04描述校验、M07内存提交/通知、WPF通用编辑器；没有自建`ConfigHelper`或专用保存器，也没有新增项目：

| 专业Owner | 源码文件 | 已验证的规则 |
|---|---|---|
| Professional.Automation | `Professional/Automation/CxAutomationDeviceOptions.cs` | 主机名必填，端口/站号/超时范围，启用自动重连时等待不少于500ms；多个设备实例隔离 |
| Professional.Vision | `Professional/Vision/CxVisionInspectionOptions.cs` | 8/16位灰度与阈值匹配，缺陷面积上下限，开启AI时模型名必填 |
| Professional.CT3D | `Professional/CT3D/CxCtReconstructionOptions.cs` | FOD/FDD几何一致性，投影数量、像素尺寸、GPU、重建模式和迭代参数 |

**说明**：这些是以既有工业业务为参照设计的**集成验收样例配置类型**，不等于从CxVisionX2或CT生产程序完整迁移了所有设备/算法参数，更不能直接用它们驱动真实机台或替代设备安全联锁与标定配置。

Studio工具栏增加“Studio配置 / Automation设备 / Vision检测 / CT3D重建”下拉选项，选择后点击“编辑所选配置 (L1)”即使用统一编辑器，保存到用户可写的`LocalAppData/CxVisual/Configs/Samples`目录。不同专业通过Id和类型隔离，可分别编辑保存、关闭重开。

### 5.1 新增自动化验收

- `Tests/ArchitectureSmoke/ConfigProfessionalL1Tests.cs`：真实专业类注册/初始化/保存重载、多实例隔离、跨字段校验、无效变更Revision不变、枚举UI元数据、no-op/订阅异常隔离。
- `Tests/ArchitectureSmoke/ConfigWpfL1Tests.cs`：Windows STA创建三个专业配置窗口，检查自动生成控件；将Vision位深改为8位、阈值设为256，**模拟点击保存并验证错误显示且不提交**；将阈值改为200且启用AI并设置模型名，模拟点击保存后重载文件验证结果。
- 编辑器新增`CxConfigEditorDialog.CreateWindow<T>`供创建/复核控件；对专业开发者保留简洁的`Edit<T>(owner,manager,id)`便利入口，内部仍经`UpdateAndSave`受控提交。
- Windows命令：`CxVisual.Tests.ArchitectureSmoke.exe`执行原有及三专业测试，`CxVisual.Tests.ArchitectureSmoke.exe --config-wpf`执行真实STA窗口操作测试。
- 独立Windows CI已验证Release/x64完整解决方案编译、三专业行为、原有配置行为、架构检查和WPF模拟操作均通过。证据：[Windows Config L1 CI](https://github.com/oudream/cx-visual-x3/actions/runs/38571307896)。

### 5.2 本轮明确未覆盖

真实Studio工具栏人工操作/交互式体验尚需现场人工确认；复杂对象/列表、专业定制UI，Schema迁移、跨多个文件事务、L2作用域/模板、L3高频参数、L4运行发布尚未实现。配置保存不等于工业设备已经生效。

CI中另外一条并行F16日志FoundationSmoke仍遇`SynchronousLog/Sync/Mes-*.log`文件占用失败，被作为**非阻断的独立诊断**保留；配置专项的成功不等于整个共享烟测已全部通过。F16原因与修复归属日志专题，不在配置分支另改日志实现。
