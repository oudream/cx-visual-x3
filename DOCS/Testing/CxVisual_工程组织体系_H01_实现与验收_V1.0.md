# CxVisual 工程组织体系 H01：实现与验收 V1.0

> 2026-10-17～18；H00 D01～D04已由用户明确确认。最终采用MEMContent/MEMSymbol，MRCVisualItem为MRCElement/MRCGroup共同基类。直接master开发，不新增csproj。**H01限定核心闭环已实现并验证，完整工程组织体系尚未完成；全仓额外F16诊断不全绿，人工交互仍未全面验证。**
>
> 源码提交：`4d9dfcb`（核心闭环）、`c61bb6a7521217f44dcbc2f4152f11fc99bdba76`（异常/并发/变换兼容加固）；此前用正常合并`c4315ed`同步远端master `47e3a900db3d04c829e9a1a87ac02aeac3c045e4`。本报告最后一次更新只修改文档，不改变受测源码。
>
> **Windows CI补验（2026-10-18）**：H01/H02已共同推送至master `7549446074a704ef093766ef057e8e7e85af4d45`，[工程组织专项Windows验收](https://github.com/oudream/cx-visual-x3/actions/runs/39085237960)、[配置/架构门禁](https://github.com/oudream/cx-visual-x3/actions/runs/39085237948)、[统一运行门禁](https://github.com/oudream/cx-visual-x3/actions/runs/39085238002)实际success。下文“未推送/待CI”为首轮时点记录，本条覆盖其当前状态；F16额外诊断和人工/像素限制仍按实际记录。
>
> **WPF像素补验**：上述专项CI job `117550274278`真实输出`PASS H01 raster pixels`、STA保留几何/命中/拖动通过；本机H03第一批的h03-visual-wpf.log也补验像素通过。早期透明位图故障日志保留，不再把像素项标为当前未验证；这仍不代表全部人工键鼠体验验收。

## 1. 第一批：真实核心闭环

状态：**已实现并验证A/B/C闭环、旧D回归及第二批异常/并发/规模验证**。A为最小工程保存重开；B为多Page/共享Content/Symbol/引用；C为加载后增加/修改/删除再保存；D为旧工厂、矩形命令、领域历史及schema1 XML兼容。完整x2 JSON format6文件导入未实现，不计入D已通过范围。

| 文件/Owner | 实际行为 |
|---|---|
| Foundation/Core/FOIStableId.cs / F01 | 非空Guid、解析/格式化/值相等，default禁止作为有效身份使用；旧CxStableId保持兼容 |
| Mechanism/Core/Engineering/MEMNode.cs / M01 | 轻量身份/导航；Project/Page/Content/Symbol类型化拥有与只读视图；Page/Content/Symbol实现IMEMResource；引用独立于拥有树 |
| Engineering/MRCVisualItem.cs / M02,M04 | Element/Group共同视觉属性；局部平移/缩放/旋转、父链组合、准确逆变换命中；按需Binding声明，不求值 |
| Engineering/MEMEngineeringSession.cs / M01,M03,M07 | 唯一树与增量Id索引、资源查询、受控编辑、同一领域历史/观察、Save/Open/Close |
| Engineering/MTCEditSession.cs / M07 | 创建/命名/删除/改父/变换/绑定/引用一批原子提交；局部逆操作回滚，无每次全树复制 |
| Engineering/MEMFileProjectStore.cs / M01 | H01 format2单ZIP内project.xml，流式读写；schema1 XML兼容；随机同目录临时件/Flush/回读/可靠替换 |
| Core/Cx*.cs兼容面 | 同一实体/同一Session，CxNode只读兼容基类无实例字段；不维护第二棵树。旧内嵌Symbol仅作为显式legacy兼容结构 |
| Mechanism/Visual/CxVisualEditor.cs、CxSceneItem/Projection / 原M37/M16 | 去除私有领域撤销栈，消费同一Session；保留旧矩形命令，复用局部变换投影/命中 |

普通用法与可执行案例：[H01QuickStart.cs](../../Examples/EngineeringOrganization/H01QuickStart.cs)。所有方法为当前真实API；与V1.1候选签名不同处以本报告及源码为准。

```csharp
var p = MEMEngineering.Create("工程");
var page = p.AddPage("操作页");
var content = p.AddContent(page.RootContent, "工位");
var rectangle = p.AddRectangle(content, "状态灯", 20, 40);
p.Rename(rectangle, "运行状态");
p.SaveAs(path);
p.Close();
var loaded = MEMEngineering.Open(path);
var sameId = loaded.Find(rectangle.Id);
```

基础便利方法直接返回已提交对象；失败抛明确异常。复杂Edit返回MTCEditResult<T>，不成功时Value无效，ErrorCode/Message说明拒绝原因。对象是只读**实时身份**，不是深度冻结快照；并发写入由Session串行化，显式expectedRevision冲突必须重试。不要在另一线程边枚举实时Children边编辑；H01未提供公共固定版组合读取API，固定快照/异步保存时继续编辑属于后续H04。Edit回调同步执行，不跨await、不保留到回调之外，不允许在其中保存文件。

### 1.1 最小模型与索引的实际合同

```text
MEMNode（Id / Name / TypeId / Kind / Parent，仅身份和导航存储）
├─ MEMProject：唯一工程根；Pages / Contents / Symbols / Resources为只读分类投影
├─ MEMPage : IMEMResource：唯一RootContent
├─ MEMContent : IMEMResource：Content与VisualItem拥有集合；按需References
├─ MEMSymbol : IMEMResource：唯一RootGroup
└─ MRCVisualItem：共同几何、可见/可选中、变换与按需Binding
   ├─ MRCElement → MRCRectangle（叶）
   └─ MRCGroup → Children<MRCVisualItem>（混合、多层嵌套）
```

MEMNode不存子List/字典/端口/事件/设备或WPF字段；叶共用静态空集合。CxNode只保留无状态兼容表面；旧CxProject等就是上述同一实体，没有同步双树。容器按需分配，Parent只能由统一内部写入器维护。按Id查询走Session字典，改名/几何移动不遍历全树；添加/删除/改父只检查祖先及受影响子树。底层顺序List跨容器删除/重排仍可能移动同容器后续槽位，不承诺任意改父都是O(1)。

Project.Resources只列独立Page/Content/Symbol；`FindResource(id,true)`可找嵌入Content，StorageRootId随拥有关系推导。`GetResourceEntry(id)`按需生成不可写元数据快照，改名后旧快照不自行更新；节点仍是资源真源。没有另一份可写资源目录树。

MRIResourceReference是Content下的从属记录：自己的Id表示使用位置，TargetId指同工程Project直接拥有的Content/Symbol定义。Node索引、引用索引与反向使用索引统一维护；定义仍在用时不能删除/降为嵌入；拥有边与引用边共同检测展开环。它不是MRK运行实例，也不复制目标Children。

### 1.2 实际公开API速查

| 用途 | 当前API与行为 |
|---|---|
| 创建/打开 | MEMEngineering.Create(name)、Open(path)、OpenAsync(path,CancellationToken)；不自动建页面 |
| 便利创建 | Session.AddPage(name)、AddContent(parent,name)、AddSymbol(name)、AddGroup(parent,name)、AddRectangle(parent,name,x,y,width,height)；矩形传Page时自动取RootContent |
| 导航/资源 | Session.Find(id)、FindReference(id)、GetResourceEntry(id)；Project.Pages/Contents/Symbols/Resources及FindPage/FindContent/FindSymbol/FindResource |
| 普通修改 | Rename、Move（本地X/Y）、Resize、Rotate、Scale、SetVisibility、SetBinding、Reparent、Remove；内部调用同一Edit |
| 复杂批次 | Edit(intent,Action/Func)；Edit(intent,expectedRevision,Func)；返回Success/Value/Revision/ErrorCode/Message/RequireValue |
| 历史与观察 | Undo/Redo、CanUndo/CanRedo、Watch、LastObserverError、ClearHistory；一批一历史，通知异常不撤销事实 |
| 引用复用 | PlaceSymbol(content,definition,name)、PlaceContent(content,definition,name)；参数/端口映射未实现 |
| 保存与关闭 | SaveAs(path)、Save()、SaveAsync(token)、Close(discardChanges=false)、Dispose；脏关闭拒绝，明确Close(true)才丢弃 |
| 检查 | Validate().IsValid/Issues、Count/Revision/Location/IsDirty/IsClosed |

Group和Element共享Move/Resize/Rotate/Scale/SetVisibility/SetBinding/HitTest。Group.Resize改变本地缩放，不改所有后代；旋转围绕本地原点，有限角归一到360度内；非零负缩放合法。`TransformPoint`组合父变换，`PositionAfterWorldDelta`将画布拖动换回父局部坐标。H01没有自动计算组包围内容的布局/分组解组命令。

SaveAs写真实format2：一个ZIP入口`project.xml`，包含同一工程树、基本字段、绑定与引用。旧CxDocumentSerializer.Save显式写schema1 XML，两者共用加载/校验器。不是V1.1候选的多清单完整包。加载上限为200万节点、256层、512MiB XML/解压入口，创建/移动同样限制深度及节点数；未知类型/字段拒绝，未注册自定义派生对象拒绝加入，避免无声丢失载荷。保存锁住Session，随机同目录临时件、Flush、回读XML后替换；首次保存使用不覆盖的Move。保存Token与跨进程冲突检测后续补齐。

## 2. 本批真实验证

- 测试先加入ArchitectureSmoke，初次构建按预期失败（MEMEngineering/MRCElement等尚不存在），日志h01-red.log。
- 真实项目构建：MSBuild ArchitectureSmoke /t:Build /m:1 Debug/x64 /p:UseSharedCompilation=false，**exit0**，h01-build.log。
- ArchitectureSmoke真实执行：**exit0**，输出`PASS H01 real cases A/B/C, ownership, transactions, references, transforms, persistence`及原配置/运行/矩形回归通过，h01-architecture.log。
- 验证包含保存/关闭/重开/修改/再次保存、资源同一身份、引用保护/往返、跨容器移动、事务拒绝、Undo/Redo、索引一致性、Group局部变换。

以上是第一批时点记录。第二批及最终验收见下表；本机日志位于忽略的`Output/Debug/x64/OrganizationGuideAudit/`，不是远端CI。

### 2.1 第二批：先失败，再修复，再回归

| 测试/日志 | 观察结果与对应修复 |
|---|---|
| h01-adversarial-red.log | 真实运行exit1：depth budget rollback失败；补齐编辑/加载一致的256层预算 |
| OrganizationAdversarialTests | 持有过期Edit、跨线程写Edit、应用异常回滚、版本冲突、四并发写者160次提交及通知顺序、引用双向环、在用定义改父拒绝、观察者抛异常、保存失败/取消/脏关闭、重复Id/损坏/未知字段/非法包 |
| h01-adversarial-green.log / h01-final-architecture.log | exit0；修复SaveAsync在编辑回调中逃逸、通知投递竞争、返回收据Revision竞争、未知字段静默忽略；全部进入常规ArchitectureSmoke |
| h01-wpf-red.log | exit1：共享符号定义误进页面场景；修正为页面投影，隐藏组裁剪后代 |
| OrganizationWpfTests / h01-wpf-green.log | exit0；真实Windows STA/Window/SceneView布局与生产OnRender保留几何、旋转形状/准确命中、拖动坐标转换验证；**位图透明，像素断言未完成，明确输出UNVERIFIED** |
| OrganizationH01Tests.Snapshot | 真实文件重开后比较每个节点Id/Kind/TypeId/Name、父/子顺序、资源根与作用域、所有几何/可视状态、案例声明的绑定及完整引用记录，不只比较节点数 |

### 2.2 最终命令、结果与证据

以下在源码`c61bb6a`对应实现上执行（构建日志中的临时WPF项目属于MSBuild生成，不是新增项目）：

| 命令 | 退出/结果 | 本机日志 |
|---|---|---|
| VS2022 `Bin/amd64/MSBuild.exe CxVisual.Unified.sln /t:Restore,Build /m:1 /nr:false /p:Configuration=Debug /p:Platform=x64 /p:UseSharedCompilation=false` | 0，31个现有工程构建；既有配置/执行未用变量及TestCenter旧API警告保留 | h01-solution-build.log；最终增量h01-final-build.log |
| FoundationSmoke.exe --concurrency | 0，PASS F19 concurrency tests | h01-foundation-concurrency.log |
| ArchitectureSmoke.exe | 0，H01 A/B/C/异常及原Core/Visual/配置/数据/MRK回归PASS | h01-final-architecture.log |
| ArchitectureSmoke.exe --organization-wpf | 0，真实STA/保留几何通过；像素未验证 | h01-wpf-green.log |
| ArchitectureSmoke.exe --organization-stress | 0，1千/10万/100万真实图元、实际文件往返 | h01-final-stress.log |
| Roslyn csc.exe /target:exe /platform:x64 /langversion:7.3 /reference:Core.dll /reference:Foundation.Core.dll H01QuickStart.cs，再运行 | 0；PASS H01QuickStart nodes=8 revision=9 | h01-example-build.log / h01-example.log |
| Runtime.Host.exe demo；save；load | 0；Add→Move→Undo→Redo、旧schema1文件往返及矩形场景保留 | h01-runtime-legacy.log |
| Build/Verify-Structure.ps1；Verify-OwnerDependencies.ps1 | 通过；31项目/77引用，零非法上行依赖/环 | DependencyAudit.csv及实际命令输出 |
| git diff --check | 0；CRLF规范化提示不是内容错误 | 提交前命令输出 |
| FoundationSmoke.exe（无参数额外诊断） | **1，存在问题**：SynchronousLogTests.cs:49，写流未关时File.ReadAllText共享冲突 | h01-foundation-all.log |

32位MSBuild曾出现XAML System.DateTimeConverter/JIT加载错误，日志h01-wpf-green-build.log/h01-wpf-rebuild.log；改用原生64位MSBuild后完整方案成功，没有为绕过问题修改XAML或系统引用。F16文件共享失败在既有[统一执行测试矩阵](UnifiedExecution_TDD_Matrix_V0.1.md)及CI全量诊断分工中已有记录，本轮未修改F16。**不能将F19必需门禁通过写成Foundation所有测试通过。**

本轮读取GitHub基线`47e3a90`的[配置底座独立CI](https://github.com/oudream/cx-visual-x3/actions/runs/39041714319)与[Windows构建/冒烟CI](https://github.com/oudream/cx-visual-x3/actions/runs/39041714417)，均为success；它们只证明基线。当前H01提交仅在本地master提交，未推送，因此没有本次远端CI结果，不把旧CI当作新验收。

### 2.3 需求 → 类型/方法 → 文件 → 实现/证据

测试文件均在现有`Tests/ArchitectureSmoke/`；生产路径前缀为`Mechanism/Core/Engineering/`，除表中特别注明。

| 需求/验收 | 对象/实际方法 | 文件 | 状态与验证依据 |
|---|---|---|---|
| ORG-H01-01 身份 | FOIStableId构造/TryParse/default拒绝 | Foundation/Core/FOIStableId.cs | 已实现并验证，Adversarial.IdentityAndTransactionLifetime |
| 02～03 空工程、自动根、合法嵌套 | MEMEngineering.Create、Edit.AddPage/Content/Group/Rectangle/Symbol | MEMNode.cs / MTCEditSession.cs | 已实现并验证，H01 A/B及嵌套测试 |
| 04 多父/重复Id/环/保护根 | InsertOwned、CheckNew、CheckReference、Reparent/Remove | MEMNode.cs / Session/Edit文件 | 已实现并验证，H01拒绝批次、Adversarial展开环/深度 |
| 05 一意图一修订、失败恢复 | Edit、局部MTCMutation日志、Undo/Redo | MEMEngineeringSession.cs | 已实现并验证，回调异常/版本冲突/过期事务/160并发提交 |
| 06 创建/重开/修改再保存 | SaveAs/Open/Save、逐字段Snapshot | MEMFileProjectStore.cs | 已实现并验证，真实A/B/C与公开示例 |
| 07 旧工程与矩形命令 | Cx工厂/文档/Serializer、VisualEditor、Host | Core/Cx*.cs、Visual/CxVisualEditor.cs | 已实现并验证，原REAL矩形测试与Host旧XML；不是x2 JSON导入 |
| 08～09 文件失败、未知格式、取消 | Save/Load、字段与预算检查、可靠发布 | MEMFileProjectStore.cs | 已实现并验证，真实IO失败、坏包、DTD、未知字段、取消、目标保护 |
| 10 只读导航/资源唯一身份 | Pages/Resources/FindResource/GetResourceEntry | MEMNode.cs / MEMEngineeringSession.cs | 已实现并验证，同一实体/只读集合设计、Entry快照；深度冻结全树后续 |
| 轻量节点与增量索引 | 不预分配可选集合；Find、AddIndex/RemoveIndex | MEMNode.cs / MEMEngineeringSession.cs | 已实现并验证，反射字段检查＋百万对象真实基准 |
| 统一视觉操作与局部坐标 | MRCVisualItem、Group、TransformPoint/HitTest/PositionAfterWorldDelta | MRCVisualItem.cs | 已实现并验证，负缩放/旋转/隐藏/同值无新修订、组千次移动 |
| 六角色完整工程工作流 | 核心定义/索引/保存为共同基础 | 本报告及V1.1六角色章节 | **部分实现**：开发者核心闭环可用；编排用户保留原矩形命令，完整工程树/实例UI未完成；其余专业/调试/生产/维护完整流程留原阶段 |

### 2.4 规模基准的解释

每级N为矩形数，另有Project/Page/RootContent/Group共4节点。一个批次创建；内存为GC.GetTotalMemory(true)相对创建前基线的托管保留量，含对象、索引、容器及历史，不是MEMNode的精确单对象大小或进程峰值。移除历史使用公开ClearHistory，意味着此前批次不能再撤销。查询10万次按步长混合访问；分别移动叶和含全部图元的Group一千次；保存/加载真实压缩工程；GC统计含测试主动收集。无外部设备/业务插件/大引用图，本机单轮观察不作为生产吞吐承诺。

最终数据见下方CSV，单位列名明确。百万级属于独立`--organization-stress`，普通CI不强制每次运行。Group移动后子项本地坐标不变，保存重开后继续验证身份/索引/位置和Validate。

```csv
elements,create_ms,live_bytes_with_history,live_bytes_after_clear_history,mixed_query100k_us,leaf_move1k_us,group_move1k_us,save_ms,load_ms,file_bytes,gc0,gc1,gc2
1000,0,432432,215472,5401,786,904,6,3,30253,4,4,4
100000,103,42131416,20282048,23703,806,783,369,316,2934577,34,14,8
1000000,1360,411009192,194619816,36312,860,832,3739,3430,29405710,273,69,11
```

百万图元清历史后约185.6MiB，保留创建撤销历史约392.0MiB；此时工程容器/索引同样计入。创建小于1毫秒以0显示，不表示零耗时；混合查询时大索引缓存局部性不同，不由单次时间推导所有硬件的恒定性能。

## 3. x2实际改造复用与有意变化

继承x2的单父/保根/引用展开环规则、批量修改失败恢复、通知在提交之后、克隆/加载恢复导航等反例设计。节点强类型拥有集合仍是唯一存储，资源分类是投影；组局部坐标/父变换语义沿用CxGroupOperations。没有将x2 Platform、JSON框架、旧运行/数据栈整体引入x3。

有意变化：最终用户选择共同MRCVisualItem，而非x2的Group:Element；名称前缀与Guid使用新规范；H01用局部逆操作日志及Session锁保证统一编辑，不实现候选全树隔离草稿。旧格式1 XML受保护根须合法，错误多根明确拒绝。旧Cx名称保留；API兼容为源码级，不承诺未发布程序集的二进制继承布局兼容。

## 4. 明确未完成

完整H02（长草稿/自动合并/全部专业修改）、完整H04（保存Token/跨进程协调/固定快照并发保存/未知插件保真/完整x2 format6迁移/账户授权）、H03公开参数/端口映射与实例覆盖、H06完整WPF工程编辑与实例渲染、H07编译/生产接续均未宣称完成。当前仅可信本机创作API，不是面向不可信远端的授权服务。

H01允许基本资源引用定义/独立实例Id及往返，不表示共享Symbol已经有完整实例渲染/参数化运行。绑定只保存来源声明，求值仍归既有配置/数据/运行。工程关闭不启动或停止设备。

## 5. 两轮自审与下一阶段准备

第一轮审查结构与失败边界：最终继承/前缀与用户确认一致；资源投影不复制真源；Core不引用Visual/WPF/Runtime/Professional；Session统一领域历史，无Editor第二撤销栈。发现并修复深度预算、通知竞争、收据Revision、首次保存竞态、未知字段/自定义类型数据丢失风险。已验证限定源码/用例；没有宣称磁盘掉电恢复、跨进程锁或所有极端数值变换。

第二轮审查易用性与消费方：独立编译运行H01QuickStart；普通操作无需维护Parent/Children/Id索引；复杂操作同一Edit；旧Cx读取/编辑/Host闭环实际运行。补齐共享定义不直接投影、隐藏祖先、旋转几何及父坐标拖动适配。当前Studio仍是样例画布，不冒充完整多页编排器；WPF位图/人工键鼠、真实硬件均待验证。旧公共派生类型/未发布程序集二进制继承布局不保证无改动兼容，仓内调用均重新编译验证。

H03启动前建议：以本轮资源Id/使用记录/反向索引为基础，先测试公开参数/端口与实例覆盖语义，再接既有MRK/MDS合同；不要先做第二Resource树或执行器。先明确实例视觉位置、参数与SourceMap如何附在MRI使用位置，补在用端口/参数删除保护与旧字段迁移。H02/H04未完成项须随这些需求补齐，不得把本轮最小引用记录等同于可运行组件实例。

**后续授权更新（2026-10-18）**：用户明确要求H01完成后自动继续H02～H07，每阶段真实实现、案例、测试、Windows CI、中文提交和文档更新。本报告是H01阶段证据，不是整个任务结束；不再等待阶段确认。新增工程组织Windows CI工作流会执行完整构建、既有回归、实际WPF检查、1千/10万基准和独立开发者案例。第一次推送前上文“尚无本次远端CI”仍为准确状态，取得实际结果后补记链接。
