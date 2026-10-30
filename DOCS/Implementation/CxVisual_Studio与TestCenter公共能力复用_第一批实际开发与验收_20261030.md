# CxVisual Studio 与 TestCenter 公共能力复用——第一批真实开发与验收（2026-10-30）

> **定位**：用户裁决“TestCenter 有的，抽取到某个项目大家共用；没有的补齐”。本批优先完成共用基础、消息/命令两项最明显缺口，明确其他大型体系仍需按各 Owner 实际开发。
>
> **证据等级**：已提交源码、Windows 编译日志以及固定 SHA 的 CI；“提交”不等于“成功验收”。本文件不声称完整五大模块/十项Decision工业完成。
>
> **架构约束**：不新增第27个项目，不建立第二套 MDS/MNM/MRK、MEM 资源仓、MOC 能力目录、TestCenter Runner；Runtime/专业服务不能依赖 TestCenter，Studio 不引用 TestCenter EXE。

## 1. 归属裁决与真实复用清单

| 体系 | 唯一生产 Owner | 共用 WPF 承载 | Studio | TestCenter | 当前范围 |
|---|---|---|---|---|---|
| 变量 | Mechanism.Data.Variables（MDS） | `Mechanism.Presentation.Wpf/Variables/MPWVariableWorkspaceControl` | `StudioShell.Variables.cs` | `TCTManualVariableFrame` 已用公共控件 | 有真实 Scope 注入与可选授权写入 |
| 运行数据 | Mechanism.Data.RuntimeData（MDS） | `Mechanism.Presentation.Wpf/RuntimeData/MPWDataLifecycleWorkspaceControl` | `StudioShell.RuntimeData.cs` | `TCTManualDataLifecycleFrame` 已用公共控件 | Heap/生命周期/工程策略；Stack需Run上下文 |
| 消息 | Mechanism.Data.Messages（MNM） | **新增 `Interaction/MPWMessageCenterControl.cs`** | **新增 `doc.messages` 与可信 Host Bind** | 人工消息页保留测试功能，嵌入同控件与同一MNM实例 | Publish/Subscribe/过滤/计数/有界观察日志；不含Broker桥接 |
| 命令 | Foundation.Data.Commands（FDC）+ Mechanism.Runtime.Commands（MRK） | **新增 `Interaction/MPWCommandCenterControl.cs`** | **新增 `doc.commands` 与可信 Host Bind** | 人工命令页保留正式TCT/IPC/多Operation测试，附共用MRK目录/Schema/操作视图 | 发现/Schema输入/真实Operation调用/查询/取消；不替代Host授权 |
| 五类模块 | MFM/MOC/FCM/MHS/Professional | `Mechanism.Presentation.Wpf/Modules/MPWModuleCenterControl.cs`、`MPWModuleCenterPages.cs` | 原有 `doc.modules` | 原有各 Module 人工页/五类配置 | 共用表单/目录；五大专业G1/设备G2/G3另验 |
| 测试体系 | `Tests/TestCenter`（TCT Runner/Catalog/Evidence） | 复用公共控件及专业Viewer | 仅打开或关联正式测试/报告 | 保留Runner、Worker、UIA、Case和正式结果Owner | 不迁移测试Runner到生产Mechanism |
| Decision | `Mechanism.Decision` + Runtime单向执行边界 | 待独立的只读分析/规划公共视图 | 流程/模块中心已有局部前置校验 | Decision实际专项测试已独立 | 十能力的Studio可视化与工业闭环仍未完成 |

**关键选择**：复用的是“真实MNM/MRK Owner上的管理/可视操作”，不是复制 `TCTManualMessageFrame` 的自建测试总线或 `TCTManualCommandFrame` 的演示/命名管道测试Host。两套人工测试的额外行为只属于 TestCenter。

## 2. 本批代码（真实提交，保持26项目）

1. `Mechanism/Presentation.Wpf/Interaction/MPWMessageCenterControl.cs`：可复用观察/订阅/发布/指标控件；`Bind(MNMMessageCenter, bool allowPublish=false)`，每个视图自己释放订阅，不 Dispose 宿主；窗口默认禁止发布。有界250条观察记录。消息交付不能替代命令受理/现场ACK。
2. `Mechanism/Presentation.Wpf/Interaction/MPWCommandCenterControl.cs`：可复用 MRK Router.Discover + FDC输入 Schema编辑 + InvokeAsync/Query/Cancel；`Bind(MRKCommandCenter, bool allowInvoke=false)`；JSON输入受到实际FDC合同约束，禁止仅凭Caller字符串自动授予设备权限；未知外部副作用不自动重试。
3. `Mechanism/Presentation.Wpf/CxVisual.Mechanism.Presentation.Wpf.csproj`：新增两个 Compile；增加已存在的 Runtime 项目引用和 .NET 4.8 内置 `System.Web.Extensions`，无 WPF→Tests 反向依赖；Runtime 本身不依赖 WPF，依赖图未新增环。
4. `Applications/Studio/StudioShell.InteractionCenters.cs`：Host注入真实MNM/MRK实例、默认只读；业务实例归宿主，Studio仅Bind；`doc.messages` / `doc.commands` 两个独立页面。
5. `Applications/Studio/StudioShell.cs`、`StudioShell.ResourceTree.cs`、`CxVisual.Studio.csproj`：九菜单资源组、工程数据与交互目录右键、Dock唯一ContentId、布局恢复、关闭时释放视图。
6. `Tests/TestCenter/Manual/Message/TCTManualMessageFrame.cs` 和 `Manual/Command/TCTManualCommandFrame.cs`：实际嵌入公共控件并绑定既有真实测试会话，不改原自动化控件ID；命令共享页签移到末尾保留既有序号；关闭测试会话先释放公共视图后释放测试Owner。
7. `Applications/Studio/StudioShellSelfTests.cs`：共用控件、真实Host注入、默认只读拒绝、真实订阅数量、关闭释放与工程Revision不受影响的WPF STA案例；由原 S1 工作流执行，不建第二Runner。

## 3. 第一批用户操作合同

- Studio打开“消息中心”但**没有绑定Host** → 显示“未绑定”，不能发布；Host调用 `AttachStudioMessageCenter` 后订阅才能生效。运行数据来自Host，不因Studio关闭而重建/删除。
- Studio打开“命令中心”但**没有绑定Host** → 不显示虚构设备命令；Host调用 `AttachStudioCommandCenter` 后能发现真实Descriptor，默认只读。显式授权视图执行仍需MRK接收Host批准，设备安全联锁独立处理。
- TestCenter人工消息/命令页只创建其人工独立会话，选“共享公共中心”使用和该会话**同一个**MNM/MRK。没有第二个消息总线或命令注册仓。
- 工程树中的“数据与交互定义”只是导航；实时消息队列、运行Operation不能被作为MEM资源保存。

## 4. Windows CI实错与修复记录

- 早期工作流的 `MPWMessageCenterControl.cs` 报 `CS0246 MNMPublishReceipt`，系遗漏 `Foundation.Data.Messages` using，已修复并同时补Studio自测所需公共命名空间。
- 后继工作流暴露 `Thickness(double,double)` 不存在及 `MRKCommandResponse QueryOperation()` 不可直接作为 `Action`（CS7036/CS0407），已修复为四参厚度与 Action lambda。
- **修复本身未证明编译成功**。须以最新包含这两次修复的固定 SHA 工作流实际结果为准，并打开 GitHub Job Logs 检查下一层错误，直到 Windows 构建、Studio STA、TestCenter原有人工/正式回归通过。

### 第一批 CI 实际证据更新（2026-10-30）

- 提交 `ec3ae8df`：[Studio Windows S1 #41075147663](https://github.com/oudream/cx-visual-x3/actions/runs/41075147663) 已完成**全解决方案.NET48编译**并执行真实 WPF STA，日志明确 `PASS: CAT-06 Studio与TestCenter共用MNM/MRK WPF交互中心`。该项证明复用控件、Host注入、只读、订阅释放在固定SHA可用。
- 同一次 Studio S1 后续的真实 Vision CPU 运行结果/Trace 断言失败（软件运行仍显示Plan Revision 189，未观察到预期结束），因此**整条 Studio S1 是 Failure**；不把 CAT-06 局部 PASS 夸大为 Studio 全绿。该问题归原 Vision/MRK 工作包排查，须记录同SHA复现，不得随意改测试为忽略。
- 此后的 `ed393a17` 正式测试补充了真实 MNM 发布订阅、MRK软件型倍增 Handler、FDC JSON入参、Operation查询及 Host Detach，在 [Studio S1 #41075308993](https://github.com/oudream/cx-visual-x3/actions/runs/41075308993) 同样显示 `PASS: CAT-06`，整条工作流仍被后续 Vision CPU 断言阻断。
- TestCenter 全量与命令专项若还在运行/未有最终结果，仍标注 **待固定SHA绿色**，不能引用旧SHA代替本批验收。
- 额外修复全量解决方案 `Tests/ArchitectureSmoke/Integration/HmiScadaPipeTests.cs` 局部变量 `rejected` 与外层同名造成的 CS0136；只重命名局部，测试语义未变。

## 5. 未完成清单与后续按Owner推进

| 工作包 | 明确尚未完成 | 下一验收门槛 |
|---|---|---|
| DIC-01 消息/命令中心 G1 | 本批新增控件待Windows最终编译和真实WPF回归，尚无设备/跨进程现场授权 | 同 SHA Studio STA+TestCenter UIA；Host关闭/重绑定；批量/异常/权限负例 |
| DIC-02 工程定义与运行联动 | 四中心统一 Project/Host/Scope/Run 切换、跨中心Trace/Operation定位、消息/命令Schema工程资产 | 按唯一 Owner 完整软件业务链及跨Scope隔离 |
| MOD-01 五类模块专业G1 | Connector/Driver/Compute/Simulation/Service各专业真实Provider接入和Studio全链路非全部验收 | MFM能力发现→MHS Effective→MEM引用→MOC/MRK真实结果；各类软件G1、设备G2单列 |
| DEC-01 Decision Studio视图 | 十能力对应的上下文候选、约束、策略、影响分析/可解释建议、可视化回链没有全量公共工作区 | 对应真实MDC/MOC静态源Map与Studio正反操作，绝不模拟后端推演 |
| TCT-01 公共测试入口 | TestCenter单独Runner保留；Studio测试证据导航及结果只读引用 | 根据正式Run/Attempt/Artifact ID定位结果，不复制Runner |
| G3/HIL | 多Host/MQTT/PLC/X-Ray等真实现场安全及长期性能 | 真实授权、联锁、未知副作用对账、性能数据和现场签字 |

## 6. 入口和本批完成定义

- 以 `DOCS/Architecture/CxVisual_平台公共能力总目录_V1.0.md` 为产品公共中心归属，以数据与交互 #0～#4、功能模块 #0～#5、Decision #0～#6、TestCenter V4.1 分别为能力合同。
- 代码和已实现级别必须采用 **固定 SHA → Windows .NET48 编译 → STA/UIA → 真实软件案例 → 必要HIL** 逐级宣布，不以演示数据、静态截图或存在按钮认定完成。
- 本批目标只是**第一批真实共用层和缺失的Studio消息/命令操作页**；后续 DIC/MOD/DEC/TCT各项保留清晰边界和真实Owner，不另建项目。
