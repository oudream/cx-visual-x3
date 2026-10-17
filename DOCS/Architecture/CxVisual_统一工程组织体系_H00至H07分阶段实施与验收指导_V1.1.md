# CxVisual 工程组织体系 V1.1：H00～H07 分阶段实施与验收指导

> 2026-10-17；本计划延续 V1.0 的 H01～H07，仅增加 H00 定案、补足最小闭环与各阶段可执行标准。**本轮只交付文档，没有阶段代码完成声明。**
> 依据：[V1.1 主文档](CxVisual_统一工程组织体系_完整架构设计与开发使用指南_V1.1.md)、[x2 逐项复用映射](CxVisual_统一工程组织体系_x2至x3复用映射_V1.1.md)、[待裁决清单](CxVisual_统一工程组织体系_架构待裁决清单_V1.1.md)。当前 H00 未确认，H01～H07 均未开始；后续执行前必须重新核对 master。

## 0. 每个阶段都必须执行的规则

1. 读取根 AGENTS.md、DOCS/00 入口、cxvisual-engineering 及本阶段。记录 `git status --short --branch`、HEAD、远端 master SHA；保留他人未提交内容。直接 master、中文主题提交，不新建项目/分支。
2. 先迁移本阶段涉及的 x2 反例断言，看到新能力测试真实失败，再实现闭环。旧测试中的 JSON/字符串 Id/可写列表适配为 x3 公共 API；不能靠放宽断言或 Mock 真源宣称通过。
3. Foundation.Core 只放通用身份基础，Mechanism.Core 放组织模型/定义/资源/事务/格式。使用既有 Foundation.Data/FCM/FDT，依赖不能倒置；不引用 Visual/WPF/Runtime/Professional 实现。H06/H07 按各自原 Owner 修改消费方。
4. 文件名与主类型对应，传统 csproj 显式 Compile；.NET Framework 4.8，使用当前真实可编译语言版本验证。本指南示例按 C#7.3 可表达形态设计，不先引入未经验证的新语言特性。
5. 组织回归优先接入 `Tests/ArchitectureSmoke`，通用身份边界可接入 `Tests/FoundationSmoke`；不新增测试项目。可以新增测试源文件，但必须登记现有入口实际调用它们。NUnit/TestCenter 是既有可选呈现/汇总入口，不以其占位截图或一次 Mock 结果替代真实测试。
6. 本文类型/方法都是目标；当前 API 见主文档 §4。共享基础签名见主文档 §5：MEMEngineering.Create/OpenAsync、Session.Edit/BeginEdit/Save/Close、MTCEditResult/Issues、Handle/Address。修改该合同必须同步案例与复用映射，不能在实现里悄悄选另一套名称。
7. 阶段验收至少保留命令、退出码、实际断言、日志路径、SHA、未验证限制。H01/H02 的单位测试不代表 H03～H07 完成。所有“门槛通过”均指后续真实代码完成后的验收，不是本文已执行。

### 共用验证命令与提交准入

在 VS2022/.NET Framework 4.8 Windows x64 环境执行（输出路径以当前 Build props 为准）：

```powershell
& 'D:/Software/MicrosoftVisualStudio/2022/Community/MSBuild/Current/Bin/MSBuild.exe' `
  CxVisual.Unified.sln /t:Restore,Build /m:1 /p:Configuration=Debug /p:Platform=x64
& ./Output/Debug/x64/CxVisual.Tests.FoundationSmoke.exe --concurrency
& ./Output/Debug/x64/CxVisual.Tests.ArchitectureSmoke.exe
& ./Build/Verify-Structure.ps1
& ./Build/Verify-OwnerDependencies.ps1
git diff --check
```

如果完整方案被依赖还原/环境阻断，应记录真实错误，再构建直接受影响的现有项目及 ArchitectureSmoke；只能报告“已通过的限定检查＋全方案阻断”，不能宣布完整验收通过。并发/运行代码即使未修改，相关既有回归仍须实际运行，避免持久化/基础类型变动破坏其它体系。远端 CI 只引用**本提交**实际结果，未推送时明确尚无新 CI。

## H00：核心架构定案（新增前置阶段）

**目标/边界**：解决 D01～D04 的结构/API 决策并记录 D05/D06/D07 路线；不是建类或改业务代码。无前置代码阶段。

**输入**：x2 `2626734` 的 CxNode/Projects/UnifiedObjectModel/Elements/Symbols/NodePolicy/Ownership/ResourcePlatform；x3 当前 Core/Visual 与四大基线；本轮复用映射 R01～R50。

| 必须定案的类型与项目 | 应确认的属性/行为 |
|---|---|
| FOIStableId / Foundation.Core F01 | Guid 字节身份、空值/解析/default规则；兼容 CxStableId，不把项目语义下沉 |
| MEMNode、MEMProject、MEMPage / Mechanism.Core M01 | 唯一拥有存储、Project 分类只读集合、Page 自动唯一根、零页面工程合法 |
| IMEMResource、MEMResourceScope、MEMResourceEntry / M01 | D01 接口/元数据结合；Standalone/Embedded/PinnedExternal、StorageRootId、目录去重，外部记录无第二节点树 |
| MRCContent/MRCSymbol/MRCElement/MRCGroup / M02 | D02 Group继承、D03 Element定位、Content轻量业务与Symbol可选行为；Group.Kind不因继承误编码 |
| MEMEngineeringSession/MTCEditSession / M01,M07 | D04 便利面与统一提交器；禁止直接修改冻结实体与 List；一用户意图一历史 |

**操作顺序**：核对上述真实源码→比较三角色影响→确定推荐/反例→负责人确认 D01～D04→将最终 UML/签名同步三份文档→写明确认日期/依据→才进入 H01。

**开发者试读案例**：用推荐 AddPage/AddRectangle 或基础 Edit 两种方式写出简单工程；读 Project.Pages/Resources/FindResource；解释“实例参数只改一个使用位置”和“后台服务没有页面”。工程编排试读：新建页、拖入两实例、分组/解组、保存；不得要求用户手填根/Guid/Token。

**设计/TDD 准备**：ORG-H00-01 检查两张 UML 只有一个最终推荐并标识批准状态；02 检查同一页面可同时以 Node/Resource 查询且无复制；03 检查视觉基类与无视觉业务无冲突；04 检查便利面与底层 API 同一收据/授权/历史；05 检查 R01～R50 的每项继承功能有阶段或明确不迁移理由。设计检查不能冒充行为单元测试。

**完成标准**：D01～D04 有真实确认记录，D05/D06/D07 有Owner与后续门槛；对象关系、公开命名和最小保存格式没有阻断 H01 的未决项。当前没有达到此标准，不能用本轮提交自动将 H00 置完成。

## H01：身份、合法模型与创建—编辑—保存—重开完整闭环

**目标**：新开发者不用 CreateSample/XML 手写就能构造工程；创建 Project→Page/Content→视觉对象→修改→保存→加载后逐字段一致。保留 V1.0 H01 名称，同时前移 H04 的**最小文件闭环**；H04 仍负责完整资源/扩展/并发/迁移保障。

**前置**：H00 的 D01～D04 已确认；若最终批准与推荐不同，先更新本节再编码。范围包括内存单次 Edit、明确修订和基本失败原子性；不含复用实例、通用端口、完整撤销、跨工程导入或专业执行。

| 类型 / 路径及Owner | 属性、方法与本阶段具体行为 | 复用依据 |
|---|---|---|
| `Foundation/Core/Identity/FOIStableId.cs` / F01 | Value/NewId/Parse/TryParse/Equals/ToString；非空Guid、default拒绝、N格式；保留旧 CxStableId 转换 | 改造当前 x3，旧 IdUtil 不另建一套随机身份 |
| `Mechanism/Core/Engineering/MEMNode.cs` / M01 | Id/Name/Kind/ParentId、只读Children；唯一Id/单父/无拥有环；不公开 Attach/集合写入 | R01/R04/R07/R08 |
| MEMProject/MEMPage / M01 | Project分类集合/Find；Page WorkbenchId/RootContentId、默认页面元数据；自动根与保根校验；空后台工程合法 | R09/R13/R14/R17/R18 |
| IMEMResource/MEMResourceScope / M01 | 按 D01 最终合同实现只读资源能力；普通嵌入内容随所在资源保存，目录不重复节点 | R31～R34＋新能力接口 |
| MRCContent/MRCSymbol/MRCElement/MRCGroup / M02 | 自动RootGroup、合法视觉包含、Element与Group继承按D02；本阶段可不开放所有Symbol编辑，但加载/验证不能接受非法根 | R19/R23/R26/R28 |
| MEMEngineering/MEMEngineeringSession / M01 | Create/OpenAsync、Project/GetSnapshot、Revision/Location/IsDirty、GetHandle/Read、SaveAs/Save/Close；默认注册 core.canvas/core.rectangle 的合法值 | x3工厂改造，R09/R46/R50 |
| MTCEditSession/MTCEditResult/MTCEditException / M07 | Edit、AddPage/GetRootContent/AddContent/AddElement、Rename/SetProperty、Commit/Dispose；先隔离构建再一次提交，失败原件不变 | R04/R07/R41；x3既有批次校验作兼容输入 |
| MRIObjectHandle/MRIObjectAddress / M03 | 本会话句柄、最小Project/资源/对象地址；失败句柄不可用；本阶段不宣称支持外部库/完整实例链 | R02/R36 |
| MPMValue/Binding、MVEIssue/Report / M04,M08 | 矩形有限坐标/正尺寸、基本文本/数值类型与Literal来源；可定位结构错误；非视觉Content不要求矩形 | R26/R38 |
| MEMFileProjectStore/IMEMProjectStore、Snapshot/SaveResult/Result / M01 | `.cxproj` 最小 ZIP/XML格式2：工程/页/根/矩形与基本属性/Revision；唯一临时文件、基本回读/可靠替换、失败不清脏；旧 schema1 原读取入口保留 | R18/R44/R45/R46；完整 Token/未知负载/依赖能力留 H04 |

**实施顺序**：先测 ID/根/非法包含→最小冻结模型与兼容适配→Create/Edit/类型默认值→真实文件序列化/加载→Save/Close 失败路径→公共完整示例→现有回归。只有本阶段支持的数据能写入该格式；遇到未实现扩展须明确拒绝，不能丢字段再保存成功。

**开发者案例**（目标片段；完整失败/关闭程序见主文档 TARGET-01）：

```csharp
// [阶段目标 H01；当前未实现]
var project = MEMEngineering.Create("H01工程");
var created = project.Edit("创建页面和内容", e =>
{
    var page = e.AddPage("core.canvas", "操作页");
    var area = e.AddContent(e.GetRootContent(page), "设备区");
    var lamp = e.AddElement(area, "core.rectangle", "状态灯");
    e.SetProperty(lamp, "x", MPMBinding.Literal(MPMValue.Number(20)));
    e.Rename(lamp, "运行状态");
    return lamp;
});
if (!created.Success) throw new InvalidOperationException(created.Issues[0].Code);
var saved = await project.SaveAsAsync(path, CancellationToken.None);
if (!saved.Success) throw new InvalidOperationException(saved.Issues[0].Code);
var opened = await MEMEngineering.OpenAsync(path, null, CancellationToken.None);
if (!opened.Success) throw new InvalidOperationException(opened.Issues[0].Code);
var lampAgain = opened.Value.Read(opened.Value.GetHandle(created.Value.ObjectId));
if (!lampAgain.Success) throw new InvalidOperationException(lampAgain.Issues[0].Code);
// 集成测试继续逐字段比较全树/名称/矩形/修订；会话均按显式策略关闭。
```

**TDD（均为待开发验收项）**：

| ID | 真实断言/反例 |
|---|---|
| ORG-H01-01 | ID往返/相等；空Guid/default/坏字符串不接纳 |
| ORG-H01-02 | Create为零页合法、Revision0且未保存；AddPage恰好一个根；新增Content可嵌套 |
| ORG-H01-03 | Page/Content/Group添加图元合法；Element叶子拒绝孩子；Symbol恰好一RootGroup |
| ORG-H01-04 | 同一对象多父/重复Id/拥有环/删除唯一根拒绝，前后全树比较相等 |
| ORG-H01-05 | 一次创建+修改只增加一次Revision；名称/位置/尺寸真实变化；失效句柄不可读 |
| ORG-H01-06 | 保存真实文件→关闭→加载，全树Id/Kind/父子次序/属性/Revision一致，加载后还能修改再保存 |
| ORG-H01-07 | 保留旧schema1矩形读取/既有REAL-01～03受支持行为；非法多根明确报告，未批准迁移不静默修复 |
| ORG-H01-08 | 目录不存在可创建；无权路径/损坏包/未知格式/写入异常不清脏、不改Location、不丢原件 |
| ORG-H01-09 | 已有目标文件不被首次SaveAs静默覆盖；取消在提交点前保持旧件 |
| ORG-H01-10 | 复制后修改快照集合/值不能影响会话；Project强类型导航与统一目录不产生重复身份 |

**客观完成**：上述断言在真实程序集与临时真实文件上通过；完整示例独立编译执行；既有Smoke/Owner检查通过或明确记录外部阻断。不是只有接口/工厂Mock/内存序列化字符串。H01 阶段报告必须写明 H04 尚未覆盖的未知插件、并发保存、完整迁移等能力。

**禁止越界**：不运行设备，不新增专业根枚举，不搬 WPF/Runtime；不为两个Core之间凑数量增设无意义包装。用户审阅 H00 前不执行本阶段。

## H02：一般编辑、地址、事务、历史与提交观察

**目标/前置**：在 H01 真正保存重开的模型上实现完整领域编辑一致性；一次用户意图只提交/撤销一次；观察者只见最终事实。依赖 H01。引用/跨工程完整语义在 H03/H05；H02 不因此忽略原子删除/占用预留规则。

| 类型 / Owner | 主要属性/方法、具体行为 | 复用/改造 |
|---|---|---|
| MTCEditSession / M07 | BaseRevision/State、BeginEdit/Preview/Commit、Reparent/Remove/Rename/SetProperty；一次成功提交，过期草稿无修改；回调异常/Dispose丢弃草稿 | R04～R07/R41；x2 ThreadStatic改成每Session闸门，禁止跨await环境事务 |
| MTCChangeOperation/MTCChangeSet / M07 | KindCode/Target/Payload、Before/AfterRevision、Forward/Inverse/Intent/Actor/影响地址；完整逆值、稳定操作编码 | x3三操作适配；R41/R49，不能把历史当可靠WAL |
| Session Undo/Redo/Watch / M01,M07 | 单历史、当前版重校验、Undo/Redo生成新Revision、新编辑清redo；通知锁外有序、回调错误不回滚；订阅可释放 | R07/R41/R42/R49；M37只消费同一领域历史 |
| MRI地址/内部索引 / M03 | 定义Id稳定；Find/导航/按类型/名/角色/标签，Revision绑定；一般移动不以路径猜身份 | R02/R03/R10/R11/R12/R36/R37；复杂选择器可以增量开放，缺能力显式说明 |
| MEM快照/工厂/策略 / M01,M02 | 深冻结、唯一根、合法集合顺序；内部真源整体交换/索引同步；Read返回节点或从属记录视图 | R01/R08/R44；保留可重建索引 |
| MVE问题/权限上下文 / M08,M01 | 版本冲突/重入/外来句柄/过期/拒绝/历史故障可定位；读、提交均授权 | x3缺口补齐；不自建账号体系 |

**实施顺序**：先落单一原子提交器及反例→一般结构动作→导航/索引→ChangeSet/通知→逆操作/UndoRedo→长编辑/线程与拒绝→保存重开与旧Editor兼容回归。先决定旧 CxProjectDocument/Session.Apply 如何转发到同一实体；不能同时存在旧树、新树、两个提交器或双撤销栈。

开发者案例：在同一长编辑中重命名、改父、预览后提交；用户取消或版本冲突均保持旧结构，成功后可以整体撤销。

```csharp
// [阶段目标 H02；project、item、newParent均来自同一已打开会话]
using (var subscription = project.Watch(change => RefreshLater(change.AfterRevision)))
using (var edit = project.BeginEdit("整理设备区", project.Revision))
{
    edit.Rename(item, "一号设备");
    edit.Reparent(item, newParent, 0);
    var preview = edit.Preview();
    if (!preview.CanSave) return;
    var committed = edit.Commit();
    if (!committed.Success) return;
    // UI在回调外需要撤销时调用project.Undo(project.Revision)，检查返回结果。
}
// RefreshLater为宿主排队刷新，不在观察回调内重入Edit。
```

| ID | TDD/集成/异常断言 |
|---|---|
| ORG-H02-01 | Rename/Reparent/Remove真实修改；相同父内重排index语义明确；非法目标无半更新 |
| ORG-H02-02 | 一批前两步成功、第三步策略拒绝：原树/地址/索引/Revision/历史/通知全部不变 |
| ORG-H02-03 | 同一BaseRevision两个草稿，仅一个提交成功；另一个有org.version.conflict，不覆盖新值 |
| ORG-H02-04 | 回调抛异常、用户取消、未Commit Dispose 均不改变真源；临时句柄失效 |
| ORG-H02-05 | Undo/Redo恢复所有持久字段和次序，新Revision可保存重开；新编辑清redo，空编辑不增版 |
| ORG-H02-06 | 观察者看到最终归属与刷新索引；一个抛错仍通知其他订阅；不会半移动/回滚已发布事实 |
| ORG-H02-07 | 同步回调重入Edit拒绝；后续正常编辑仍可行；Watch.Dispose后不再接纳新通知 |
| ORG-H02-08 | 外来/已关闭会话句柄、已删除对象、旧快照的写入意图均受控拒绝 |
| ORG-H02-09 | 多线程竞争遵守同一写闸门；并行快照读取安全；无锁内外部Provider/网络IO |
| ORG-H02-10 | 保存边界后新编辑与Undo/Redo正确保持脏状态；不误撤销磁盘保存或配置生效 |
| ORG-H02-11 | 旧矩形操作与新接口修改同一实体；旧Editor不再独立回放领域逆命令；既有UndoRedo测试保留 |
| ORG-H02-12 | 人为故障注入逆操作拒绝/失败，报告历史不可用并阻断不可信继续；不能损坏工程再报成功 |

**完成标准**：所有一般动作、完整逆操作、拒绝边界及真实保存重开通过；迁移 x2 移动/通知/事务关键反例；源状态、序列化快照、索引和通知四者一致。若不能验证旧 Visual.Editor 接续，只能报告 H02 Core完成、H06消费适配未验收，不能称用户编辑全闭环完成。

**依赖/禁止事项**：当前基本属性和结构范围可完成，不声称已覆盖未来专业不可逆动作；未到 H03 不能假装通用实例/端口删除保护已实现。M07领域历史与M37用户意图呈现分工按H00确认，禁止Core引用Visual。

## H03：资源、Content/Symbol复用、公开属性与端口契约

**目标/前置**：H01/H02已完成；D01/D02/D03定案，组变换的D07依赖方案已确认。完整承载共享定义、嵌套、两个使用实例、资源范围、公开边界和端口；新增所有持久字段必须同时往返，不能等 H04 才保证不丢数据。

| 类型/Owner | 关键属性/方法及行为 | 复用 |
|---|---|---|
| IMEMResource/MEMResourceEntry、Project资源投影 / M01 | Id/Type/Scope/StorageRoot/版本散列/依赖；Pages/Contents/Symbols/Resources/FindResource，外部记录与根去重 | R13～R16/R31～R34，D01 |
| MRCContent/MRCSymbol、MRIReference / M02,M03 | 可选四Aspect、公开属性/端口；引用Id/目标版/覆盖/实例布局；PlaceContent/PlaceSymbol/Retarget | R19～R25/R35 |
| MPMTypeDescriptor/Property/Value/Binding/Aspect、IMPMTypeProvider / M04 | 注册冻结类型/工厂、默认定义/缺失参数、类型单位约束、值来源；Literal与Config/Variable/Resource/Member分开 | R20/R26/R27/R38；复用FCM/FDT |
| MPCPort/Endpoint/Connection、MRCPublicMapping、MTUContractReference / M05,M02,M06 | Define/Expose/Connect/Disconnect/Unexpose；方向/基数/类型/单位/时空来源；连接唯一拥有，跨资源通过公共绑定/调用 | R24/R39/R40 |
| MRIResolver/派生索引 / M03 | Resolve/FindUsages/环检查/影响；固定版、实例链、嵌入作用域、外部只读包 | R21/R22/R35～R37 |
| MTC Group/Ungroup与实例编辑 / M07,M02 | 同一草稿分组/解组、SetOverride/Clear、视觉布局属性变更；一次提交/可逆；根与映射保护 | R28/R29及已批准继承方案 |

基础依赖核查：Core→Foundation.Geometry目前不存在；D07建议H03只增加对这个现有项目的必要引用、复用CxAffine2D/Point2D载体，不修改第三项目源码。当前CxAffine2D只具Identity/Transform，不能假设Compose/Inverse已实现；组领域坐标算法改造x2并测试世界变换，通用几何扩展仍需F05独立任务。未确认D07或无法满足两Core实现边界，不得宣称完整Group/Ungroup已通过。

**实施顺序**：资源能力/分类→定义与引用→双实例/公开参数→默认工厂/类型→端口/公开映射→依赖/环/删除→持久化全字段→组/局部变换→专业Provider样例。使用 core 符号面板验证复用，不以伪Driver宣称IoT已接入。

**开发者案例**：主文档 CASE-04 的 Motor 两实例，SetOverride(a,"Device",MPMBinding.Resource(deviceA))；只改 a，b和定义不变；RootContent下再嵌套Content，实例地址不同。后台Service Content不创建页面。每一步结果和保存重开都检查。

**TDD**：ORG-H03-01 同资源不同入口同一Id/无第二树；02 独立/嵌入/外部作用域；03 双实例覆盖隔离；04 展开环/缺版/错类型/私有成员拒绝；05 端口方向/类型/基数/单位拒绝；06 删在用定义/端口/公开映射拒绝；07实例链端点解析；08 Provider异常/循环初始化/注册冲突原子拒绝；09 Symbol唯一根和禁止Group包含业务引用；10四Aspect/公开映射/连接/布局往返；11 Group/Ungroup世界变换等价与奇异变换/跨图层拒绝；12已声明但缺能力允许保存、所需Run验证失败。

**客观完成**：Motor双实例、后台Content、局部组变换和端口契约完整用例，源码级与真实文件回归均通过；R19～R40的本阶段去向有证据。复杂UI/完整运行翻译未完成不能宣布生产可用。

## H04：完整文件、未知保真、保存并发、迁移与维护

**目标/前置**：扩展 H01 基本IO及 H03全部持久定义；冻结快照、可靠保存/Token、未知内容、完整依赖/权限/关闭与显式旧格式迁移。依赖 H01～H03；H05需复用这里的安全存储与兼容基础。

| 类型/Owner | 具体合同 | 复用 |
|---|---|---|
| IMEMProjectStore/MEMFileProjectStore / M01 | Load/Save固定Snapshot、期望StoreToken；新路径拒绝覆盖、随机临时件、回读、提交点取消、恢复旧件；Stream编解码不关闭调用方流 | R34/R45/R46 |
| Snapshot/ResourceEntry/SaveResult / M01 | Revision/RequiredExtensions/依赖锁/Hash/Token/SavedRevision；保存旧快照后新编辑仍脏 | R13/R34/R47/R50 |
| MPMAspect/Value/未知原负载 / M04 | 未知TypeId/Schema/原字节/散列保真，Required决定运行阻断；不任意反射实例化 | R30/R38/R45 |
| IMTIMigration/迁移报告/确定性IdMap / M38 | x3schema1与x2format6明确来源；原件备份/预览/未支持字段保留，超界long/矛盾Id/不支持降级拒绝 | R18/R36/R43/R48 |
| Session Save/SaveAs/Close/Dispose / M01 | 成功才换Location/Token；Open/Closing/Closed与三关闭策略；失败可修复，关闭不停止宿主在途任务 | R46/R50 |
| IMEMAccessPolicy、外部Resolver / M01,M03 | 每次读取/保存/导出/准备运行授权；固定版只读包先锁外解析；未知依赖有可定位Issue | R31/R35/R47，新增安全边界 |

**顺序**：全字段格式/未知载荷→文件验证与资源相对位置→Token/并发保存→错误/取消/恢复→生命周期与权限→schema1迁移→x2format6迁移预览/保真样本。若只支持子集x2语义，报告具体不支持项；拒绝或保真封存，不“升级成功”后丢字段。

**开发者案例**：两个创作会话打开同一路径，A保存后B因Token冲突失败；B的修改/脏状态/Location仍在；用户比较并重新加载后另存。另一个用例：卸载符号插件后打开，未知载荷可原样保存，准备运行明确失败。

**TDD**：ORG-H04-01全定义/实例/连接/元数据往返；02未知插件字节/散列不变；03外部资源相对地址在目录搬迁后可解析；04截断/损坏/路径穿越/DTD/解压预算拒绝；05并发保存顺序与Token冲突；06保存中继续编辑仍脏；07失败/取消提交点正确；08Replace不可用保留旧件；09三关闭策略及已接纳保存协调；10权限拒绝无泄漏；11schema1与x2golden样本显式迁移/原件不改/新Guid映射一致；12多根/溢出版/不支持降级拒绝；13加载快照/保存结果可定位原路径与版本。

**完成标准**：真实本地文件故障注入与依赖搬迁通过、迁移差异清单可审查；不以单个序列化字符串或x2旧日志验收。网络盘原子性、跨进程锁、断电恢复按实际环境独立记录，未测不宣布支持。配置/数据/Runtime实例不纳入工程包。

## H05：导入导出、克隆、模板、提取与跨工程复用

**目标/前置**：H01～H04完成；复用 x2 最成熟的克隆重映射/依赖闭包能力，所有转移一次预览、一次提交。范围不含在线多人协同和分布式事务。

| 类型/Owner | 属性/方法与行为 | 复用 |
|---|---|---|
| MTITransferPlan/Result / M38 | SourceHash/BaseRevision/Roots/IdMap/ExternalPolicy/Issues；预览固定源与目标，陈旧计划拒绝 | R43/R45/R48 |
| Session PreviewImport/Import、PreviewDuplicate/Duplicate / M38门面 | 新拥有对象/引用/连接/图层/声明Id；内部地址/公开映射重写，外部Reject/Pin/IncludeClosure | R21/R22/R35/R43；改造RekeyPairs |
| Export/ExtractReusable、MRCPublicMapping / M38,M02 | 固定闭包只读导出；提取Content＋公开边界＋原位置替换引用一事务；不能按最终叶Id猜目标 | R24/R29/R43/R45 |
| 内部迁移/相对静态资源适配 / M38,M01 | 不复制运行对象/服务；未知载荷保真；来源工程/旧Id映射、散列/许可/路径约束 | R27/R30/R34/R46/R48 |

**顺序**：保留Id快照与新副本明确分开→克隆拥有森林→映射端口/图层/引用/数据声明→依赖闭包与外部策略→预览/提交→导出包/模板新工程→提取与逆操作→跨工程样本回归。

**开发者案例**：将Cabinet Content与两个Motor实例导出；另一空工程PreviewImport，看到IdMap和固定共享依赖，再Import到Project/目标Content；保存重开后分别修改实例。模板Create+Import产生新ProjectId，SaveAs保持原ProjectId。

**TDD**：ORG-H05-01原件不变/新副本新Id；02内部引用/端口/图层/配置来源声明正确映射；03未解析/矛盾/外部地址原样诊断；04三种外部策略；05重复Id/缺依赖/源变动/目标陈旧拒绝；06取消/资源丢失无部分导入；07整体撤销恢复；08提取公开映射重接与反向还原；09深/浅行为/参数/未知字段覆盖；10八案例定义包往返保持可编辑；11输入森林包含自身祖先/重复选择拒绝；12流所有权与包大小限制。

**完成标准**：x2克隆/地址身份/导入导出反例断言迁移，至少双工程+依赖静态文件真实包验证；每项不支持内容可定位，不能通过空Exporter或Mock依赖图验收。不得整体引入x2旧Graph或运行对象注册表。

## H06：Visual/Studio 消费接续与工程用户体验

**目标/前置**：H01～H05已完成；H00 D05交互事件合同明确。用户在界面与代码间自然接续同一工程，常用任务不填内部Id。此阶段修改**原有** Mechanism.Visual(M16/M17/M37)、Mechanism.Presentation.Wpf 和 Applications/Studio，不搬到 Core。

| 原Owner类型/区域 | 需要改造的属性/方法/行为 | 复用与边界 |
|---|---|---|
| 当前 CxVisualEditor / M37 | Document/session绑定、CanUndo/Redo、Add/Move/Remove/Undo/Redo委托唯一提交；选择/导航/预览仍UI意图 | R49/R50；不建立第二领域历史 |
| 场景投影/空间索引 / M16,M17 | 当前Page/实例上下文、位置/变换/图层/锁定/可见性、Group与SVG遍历/命中、无视觉Content不出假矩形 | R26～R30/R37；Core不渲染 |
| Studio ViewModel/资源浏览器/属性面板 | Pages/Contents/Symbols/Resources统一检索，类型库、公开参数与来源、实例下钻/返回、问题跳转、保存未保存提示 | R13～R24/R31/R38/R50 |
| 交互事件适配 / M37,Presentation | Capture/Target/Bubble、取消默认行为、选择/拖动/连线预览，提交后Watch仅作事实刷新 | R42；不让PreventDefault回滚已提交事务 |
| 导入/迁移/依赖检查UI / Studio | Preview差异/IdMap/缺依赖、引用与副本区分、保存/生效/运行/发布独立状态 | H04/H05 API；不自写文件/关系集合 |

真实消费入口：[CxVisualEditor.cs](../../Mechanism/Visual/CxVisualEditor.cs)、[CxSceneProjection.cs](../../Mechanism/Visual/CxSceneProjection.cs)、[CxSceneItem.cs](../../Mechanism/Visual/CxSceneItem.cs)、[StudioViewModel.cs](../../Applications/Studio/StudioViewModel.cs)。当前Projection.Project遍历整个Document，SceneItem只绑定CxElement矩形；改造时必须明确当前Page/实例上下文、保留旧矩形兼容，不假设它已经支持图元多态/业务连线。

**顺序**：现有矩形回归→同Session历史/页面定向→资源库/实例→属性来源/端口映射→分组/变换/选择→事件传播→导入迁移与错误反馈→代码↔界面双向接续验收。

**用户案例**：模板建页→拖Motor两次→只给第一实例选设备→分组两个视觉图元→撤销/解组→保存重开→定位一个缺失公开绑定→代码重命名后界面刷新。所有拖拽的中间帧仅预览，松手一次提交，Esc无提交。

**TDD/集成**：ORG-H06-01 UI命令只走唯一提交；02当前页添加/选择隔离；03定义修改与实例覆盖区别；04拖拽取消/Undo/Redo；05Group世界变换/嵌套命中/图层锁；06事件阶段/取消/释放订阅；07问题定位到正确实例；08保存失败/脏关闭重试；09代码保存GUI重开与反向读取；10缺插件保真/禁运行提示；11重复打开资源文档共享同一Session历史；12高频变更合并刷新且不丢提交事实。

**客观完成**：headless单元测试＋真实Windows WPF操作或可复现UI自动化，附具体路径/截图或日志/输入步骤。仅ArchitectureSmoke通过不能宣布WPF体验完成。性能记录数据量、加载/保存/投影条件与结果，不虚构固定毫秒承诺。

## H07：工程到原运行/专业/生产体系的接续

**目标/前置**：核心H01～H05；交互验收依赖H06。H00 D03和专业模型/翻译约定已确认；保持 Orchestration(M18～M25)、Runtime(M26～M34)、Data(M09/M10/M35/M36)、Lifecycle(M39/M40)、Professional 和宿主的原Owner。

| 类型/原Owner | 需要实现/改造的具体合同 | 依赖/禁止 |
|---|---|---|
| MEMExecutionHandoff/PrepareRun / Core M01 | 固定Snapshot/入口/目标/依赖锁/配置绑定引用；验证后交宿主，不启动设备 | 不把Func/CLR活动对象写进包 |
| 工程定义翻译与SourceMap / Orchestration、专业，接 Runtime M29 | 按模型把Logic/Data/能力声明转换到已有MRK编译输入；地址+资源版+InstancePath→StepId；同一目录/编译器，不创建平行Graph工程 | 具体翻译类型需按相应Owner真实API设计，非本文件虚构“通用Compiler已完成” |
| MRK Builder/Compile/Session/Debug/PlanSlot / Runtime | 复用真实MRK入口/授权接纳/计划租约/Trace/停止排空；补来源映射消费 | 不新建第二执行引擎 |
| FCM/MHS/MVB 与 MDS/FDE/MDE / 原Data Owner | 保存的绑定→固定配置来源/数据端口/作用域；普通Get/Set/Return，显式Heap共享 | 不克隆在途Heap/Stack，不把每次端口强制DataRef |
| 专业Provider/能力/设备完成条件 / Professional | Vision采集检测、CT重建/大数据、IoT长订阅、Inspection判定/HMI/Simulation；失败/取消/真实Readback | 不以CPU Vision代替全专业与硬件安全 |
| 包/插件/部署与生产宿主 / Lifecycle,Applications | 固定包/目标/权限/产品配方/模式、Stage/Activate、在途版保留/回滚与设备确认分开 | Core Close不自动停止设备，不把Load XML当生产切换 |

当前真实接续类型应直接核对这些文件，而非仅使用抽象“运行服务”名称：

- [MRKExecutionCompiler.cs](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)：`MRKStepCompileRequest` 的 Id/CapabilityId/SubflowId/Dependencies/Inputs/执行策略；`IMRKCapabilityResolver`；`MRKExecutionPlanCompiler.Compile(string,IEnumerable<MRKStepCompileRequest>,IMRKCapabilityResolver)`；`MRKCompileResult.Plan/Diagnostics/Succeeded`。
- [MRKExecutionComposition.cs](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs)：`MRKFlowCompileDefinition.Id/Steps`；`IMRKFlowCompileCatalog.TryGet`；`MRKExecutionOrchestrationCompiler.Compile(string,IMRKFlowCompileCatalog,IMRKCapabilityResolver)`。把工程Logic定义翻译为这些输入，子流程展开后也须保留实例来源，不能只记录原始局部StepId。
- [MRKRuntimeSession.cs](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs)：`MRKRuntimeSession.Start/TryStartFlow/TryStartFlowWithControl/Snapshot/TryActivatePendingPlan/StopAsync`；会话快照的Id/State/ActiveFlows/PlanRevision保持原运行语义。
- [MRKExecutionPublishing.cs](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs)：`MRKExecutionPlanPublisher.CompileAndStageBlueprint/CompileAndStageOrchestration/TryActivate/StageRollback`、`IMRKPlanPublicationPolicy`和`MRKPlanPublicationReceipt`；正式工程发布仍须包/权限/目标验收。

SourceMap可先采用宿主持有的 `IReadOnlyDictionary<string,MRIObjectAddress>`（固定Plan身份＋展开后的StepId作键、地址含实例链），与固定快照/计划收据一起持有；不必为此新建第二权威工程树或通用Compiler接口。扩展编译器以保留展开来源时须给出具体方法/测试，不能以“存在字典”冒充完整SourceMap。

**顺序**：从已保存重开工程生成Handoff→最小纯CPU Content双实例翻译与SourceMap→接代码共源MRK→Vision/PLC-HMI/CT各异常边界→调试定位→配置/数据版本与生产切换→部署与真实设备验收。每个专业单独记录范围，不强迫所有模型压成DAG。

**开发者/用户案例**：保存一个两工位Inspection工程后重开，选择固定目标/配方→检查→原Runtime启动；从FlowId跳到某工位某实例的节点/端口；改草稿不影响在途版；关闭Studio不隐式停止任务；明确停止后等待专业完成/失败回执。

**TDD/集成**：ORG-H07-01固定快照与原源码编排同义；02重复组件实例SourceMap正确；03缺能力/错版/目标拒绝不启动；04配置来源/数据Return失败不提交；05长期服务/事件/流与控制推进不混淆；06CT大数据延迟加载/取消与跨页面绑定；07调试Trace/等待原因定位；08安全点/授权切换在途版隔离；09关闭编辑不丢宿主运行；10设备超时/未确认/取消非成功；11三专业组合真实异常用例；12包/插件卸载/迁移/回滚证据。

**完成标准**：分开报告工程表达/编译/虚拟运行/实际目标/真实硬件五层证据；六专业覆盖矩阵逐项有用例或明确未完成。没有硬件/目标部署证据时不得称生产验收完成，不能用Mock/内存PlanSlot替代正式发布恢复。

## 8. 每阶段交付与自审模板

```text
阶段：H0x；基线：master / <实际SHA>；确认的H00项：...
实现范围：公开类型/Owner/文件/真实行为；未实现：...
复用：Rxx / x2来源SHA与成员 → 新FQN；刻意改变的语义与原因
测试：ORG-H0x-xx → 实际测试方法/入口；命令/退出码/日志
兼容：旧Cx API、schema1/x2format6/Id/字段保真；不支持项及诊断
用户闭环：创建/编辑/保存/加载/复用/撤销中本阶段支持的真实操作
依赖：Owner DAG、项目数、外部SDK/包变化；无平行真源
未验证：UI/远端CI/网络盘/硬件/生产恢复等具体限制
提交：少量中文master提交；阶段状态只有在上述客观标准满足后更新
```

第一轮自审检查架构/复用/关系/依赖/遗漏；第二轮自审让新开发者和编排用户照案例走一遍，检查隐藏前置条件、未定义API、错误/取消与保存重开。阶段未完成就记录失败，不通过删断言或减少文档承诺来“完成阶段”。
