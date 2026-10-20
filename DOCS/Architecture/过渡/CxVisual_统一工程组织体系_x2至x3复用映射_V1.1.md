# CxVisual 工程组织：x2 → x3 复用映射 V1.1

> 2026-10-17；x2 本地与 GitHub master 均为 `2626734d0fd5e67ac50fd0621532173cec39b939`；x3 本轮已正常合并远端 `604718869e704d8ab943d6e593eeeb274afe657d`，组织 Core/Visual 源码相对 V1.0 审计基线未变。
> 配套[主文档](CxVisual_统一工程组织体系_完整架构设计与开发使用指南_V1.1.md)、[阶段指南](CxVisual_统一工程组织体系_H00至H07分阶段实施与验收指导_V1.1.md)、[H00 待裁决](CxVisual_统一工程组织体系_架构待裁决清单_V1.1.md)。**静态审查不等于本轮重跑 x2 测试，更不等于已迁移。**

> **2026-10-18 H01实施更新**：下面“x3现状”栏保留V1.1设计审查基线；当前实现已发生变化。D01～D04已确认，Group/Element共用MRCVisualItem，Content/Symbol采用MEM前缀。真实代码提交为4d9dfcb/c61bb6a，完整案例与测试见[H01报告](../Testing/CxVisual_工程组织体系_H01_实现与验收_V1.0.md)。未将x2整文件原样复制或重新运行x2测试；以下记录为行为/算法/反例的改造继承。

| R编号 | 本轮实际去向 | 已验证的继承行为 | 尚未迁移 |
|---|---|---|---|
| R01/R03/R04/R07/R08 | MEMNode、类型化拥有与MTCEditSession | 唯一Parent/统一遍历/合法结构、自动根、重复Id/循环/深度拒绝，父集合就是唯一存储 | 全DOM替换/全部导航选择器 |
| R09/R13/R14/R17/R18 | MEMProject/Page、Create/AddPage/资源投影 | 空工程、自动唯一根、定义资源和内嵌资源作用域；轻量后台Content | StartPage/工作环境/全部页面元数据 |
| R19/R23/R26/R28 | MEMContent/Symbol、MRCVisualItem/Group/Element | 共享定义最小使用记录、唯一RootGroup、局部坐标/父变换/负缩放/命中 | 公开参数、SVG、完整样式/逻辑Aspect、Group/Ungroup |
| R31/R35/R37 | IMEMResource/MEMResourceEntry、Session节点/引用/反向索引 | 资源原对象查询、Entry元信息快照、引用展开环、在用定义删除/改父保护、增量Id查询 | 外部库/固定版/依赖锁/实例链 |
| R41/R42/R49/R50 | Session.Edit/Undo/Redo/Watch/Dirty | 原子失败恢复、统一历史、通知锁外有序/异常不撤回、重入拒绝、版本冲突；旧Editor消费同一历史 | 长草稿/固定快照/完整UI传播 |
| R44/R45/R46 | MEMFileProjectStore、CxSerializer兼容 | 恢复Parent/索引后验证、真实流式文件、随机暂存/回读/可靠替换、取消/失败保护、旧schema1 | 未知插件保真、保存Token、完整x2 format6迁移 |

最重要的有意调整：x2的Group:Element改为已批准的共同VisualItem；字符串身份改为Guid；避免每次编辑复制/重建整个工程。未支持未知载荷时H01明确拒绝，不静默舍弃后宣布迁移成功。

## 1. 结论与复用分级

x2 的关键价值不只是六个类名，而是“强类型拥有集合→统一导航→结构校验→事务回滚→地址/索引刷新→提交后通知”的整套行为，以及可复用定义/实例、持久化与大量反例测试。x3 目前只实现基础树、矩形三操作与 schema1 XML，不能因已经有 CxNode 就舍弃这些能力。

| 标记 | 具体含义 |
|---|---|
| A 直接继承行为/算法 | 保留可证实规则、纯遍历/顺序/闭包算法和测试断言；仍需适配命名/程序集/ID，不表示整文件零修改复制或免测 |
| B 改造复用 | 迁移已有方法体/规则，替换可写集合、旧身份/反射/序列化/事件、线程或项目依赖；保留功能与失败边界 |
| C 重新设计必要部分 | 为新隔离草稿/保存 Token/固定快照等补齐 x2/x3 未具备的合同，必须说明为何无法直接采用旧实现 |
| D 不进入新主线 | 旧兼容入口只留迁移读取，或能力仍属原 Visual/Runtime/专业 Owner；不是宣称旧功能无价值 |

下面按关键属性/方法分组追溯，而非只比较类名。每行 X2-S 源码键在 §6 有固定提交与实际文件链接；x3 对应真实类型详见主文档 §4/§9/附录。目标 `MEM*/MRC*/MRI*` 等仍是规划；H00 的继承/资源接口未确认。

## 2. 对象、导航和结构修改

| 追溯 | x2 类型、关键属性/方法及已有行为 | x3 现状 | V1.1 目标 / 决策与验证 |
|---|---|---|---|
| R01 / S01 | CxNode：Id/Name/NodeType、ParentNode；ChildNodes 投影原强类型集合，不另存树 | CxNode 自己维护 Children，Parent 内部写；Id/Name 只读 | MEMNode 的唯一真源保留；选择一个内部存储，分类属性只投影；B；H01 验证多父/重复Id/枚举同序 |
| R02 / S01 | FirstChild/LastChild/PreviousSibling/NextSibling、OwnerProject/OwnerPage、RootNode、Address/Path | 只有 Find(Guid)/Parent/Children | 只读导航、定义地址与实例地址分开；A/B；H02 检查空树/断开节点/重排/共享定义无伪 Page |
| R03 / S01 | Ancestors/AncestorsAndSelf、Descendants/DescendantsAndSelf、NodesBeforeSelf/AfterSelf、Contains/GetRootNode、FindById/Name/Type、IEnumerable | Find 递归，全局第一组辅助 | 保存纯遍历与类型筛选语义；A；H02 固定版枚举/深树/重复名字不取首个身份 |
| R04 / S01,S04 | AppendChild/PrependChild/InsertBefore/After；CxNodeCollection 内部转调，非另一存储 | 无一般公开插入，仅 Element Add | MTCEditSession 的合法动作/相同内部拥有写入器；B；不把旧公开 DOM 写入口直接暴露到冻结节点；H01/H02 |
| R05 / S01,S06 | ReplaceChild/ReplaceChildren/ReplaceWith；Page/Symbol 根特殊处理，整片输入 forest 预检 | 无根替换/一般替换 | 初版不暴露任意根替换；有明确导入替换需求时迁移算法；B/D；H02/H05 保根、无半更新 |
| R06 / S01,S34 | Remove/RemoveChild/RemoveAll、RemoveOwnedForest；整体删除集合检查外部连接/引用，再修改；受保护根禁止删除 | RemoveElement 单对象补偿 | MTC DeletePolicy、预览影响、闭包删除；B；H02/H03 删除相互引用闭合子树可行、外部使用保护 |
| R07 / S03 | CxNodePolicy.CanAddChild/CanRemove/CanMove/CanConnect/CanReference；拥有环/引用展开环、单根、注册规则 | Allows 静态关系，无一般引用策略 | 保留无副作用预检；B；冻结规则目录，不复制全局可变 Rules 给不可信插件；H01/H03 |
| R08 / S02 | Project/Page/Content/Symbol/Group 覆盖 Enumerate/Insert/RemoveOwnedChildCore；Typed Lists 是真实拥有位置 | 六类空壳/通用列表 | 采用唯一内部模型＋只读 Pages/Contents/Symbols/VisualChildren；B；H01，禁止多 List 与 Children 双写 |
| R09 / S26 | Project.CreatePage(name,workbenchId)：默认尺寸、自动根/图层、StartPage；RemovePage 检查引用且不得删最后一页 | CreateSample 固定树 | 合法工厂和便利 API 改造复用；B；**最后一页限制不继承**，x3 要求零页面后台工程，入口引用仍保护；H01/H03 |
| R10 / S29 | CxNodeQuery.Match、SelectNodes/QuerySelectorAll/Closest、Role/Tag；SelectSingle/QuerySelector 取第一 | 无复杂查询 | 继承显式多结果筛选；A/B；身份查询禁止由 first 模糊命中；H02/H06；完整选择器语法非 H01 前置 |
| R11 / S30,S31 | CxNodeIterator 固定节点序列，Next/Previous/Rewind；TreeWalker 父子/兄弟移动 | 无 | H02 有限只读遍历，H06 按需要继承过滤/导航；A/B；不为简单遍历强制创建两个公共新类 |
| R12 / S35 | IsSameNode/IsEqualNode/CompareDocumentPosition；位标志和结构顺序；Equal 用 JSON 深比较 | 无 | 身份、内容比较、顺序分别定义；A/B；快照间不能以 CLR ReferenceEquals 判实体相同；H02/H05 |

## 3. 工程、资源、内容、符号与图元

| 追溯 | x2 关键属性/方法及已有行为 | x3 / V1.0 差异 | V1.1 继承及测试 |
|---|---|---|---|
| R13 / S10 | ProjectModel：ProjectId/Name/FormatVersion=6/Revision(long)、Pages/ContentDefinitions/SymbolDefinitions/Workbenches、StartPage/FallbackPage | x3 Guid/Revision int，只有 Page Children | MEMProject 强类型只读集合/入口引用；B；旧 long 超出 int 范围明确迁移拒绝/版本转换，不截断；H01/H04 |
| R14 / S10,S26 | FindPage/FindContentDefinition/FindSymbolDefinition/FindResource、FindSymbol/FindContent；Content 查询共享与页面内部 | x3 Find(Guid) 无资源分类 | 保留自然查找入口，IMEMResource＋统一目录推荐见 D01；B/C；缺失/重复/嵌入与共享范围测试 |
| R15 / S10 | Project 的 Variables/Resources/Events/Popups/Alarms/Templates/AssetIndex、Theme/默认页大小/作者等 | x3 无这些完整字段 | 按需元数据与定义引用映射，不全部直接作为 Core 专业属性；B/D；每类旧数据须有去向或保真 Issue，禁止静默删除 |
| R16 / S10,S11 | Project.Logic/Data/Collaboration；Workbench 记录 Logic/Data/Collaboration，页面以 WorkbenchId 关联 | 旧架构图画 Workbench 物理层，实际代码是登记 | 继承零页面后台能力/工作台登记，保留 x3 四 Aspect 原则；A/B；不增加第七节点/WorkbenchRef；H03/H07 |
| R17 / S10 | PageModel：RootContent 自动创建；Width/Height/Background、Layers/Events、WorkbenchId、PermissionKey/VisibilityExpression/ScreenIndex/PreserveRuntimeState | x3 Page 没有页面属性/唯一根 | RootContent 结构继承；显示/导航元数据映射到 Visual/Collaboration；B；H01 根、H03 保真、H06 页面显示；运行状态保留策略不是持久活动实例 |
| R18 / S10 | RootContent setter 拒绝隐式替换/降级；SetRootContent 显式事务；OnDeserializing 不伪造旧根 | x3 Load 不约束唯一根 | 保留旧格式必须显式迁移的规则；A/B；H04 多根/旧 Elements 原件与映射可审查 |
| R19 / S11 | ContentDefinition：OwnedContents、ContentReferences、SymbolReferences、VisualChildren、Layers；Bounds/Transform/Visible/Enabled/ZIndex/Clip/Layout | x3 Content 仅 Name/Children，V1.0 目标缺部分布局细节 | MEMContent 组合/作用域；可选 Visual 的布局/裁剪/图层；B；H03 全字段往返、无视觉不分配几何 |
| R20 / S11 | Content.Logic/Data/Collaboration 按需存在；Layout Absolute/HorizontalStack/VerticalStack/Grid，Spacing/Columns | V1.0 曾将无视觉能力也泛化到 Element | 保留 Content 业务组织；D03 推荐视觉 Element；B；H03/H07 分别验证无界面业务/布局不改逻辑顺序 |
| R21 / S11,S15 | ContentReference：InstanceId/Alias/TargetContentId/TargetPath、BindingContextId/ParameterOverrides、Bounds/Transform/Visible、ResolveContent | x3 无共享 Content 实例 | MRIReference Kind.Content＋Address＋MPMBinding＋实例 Visual；B；H03 双实例/嵌套/缺失/版本环，保留绑定上下文来源 |
| R22 / S11,S15 | SymbolReference：TargetSymbolId/DefinitionVersion、PropertyOverrides/Bindings/PublicPortBindings、Validate/ResolveSymbol | x3 Content 直接拥有 Symbol，无实例 | MRIReference Kind.Symbol＋公开映射/固定版；B；H03 目标版不支持须原样诊断，不自动升级/清空覆盖 |
| R23 / S13 | SymbolDefinition：Id/Name/Version/Category/Tags/Width/Height、RootGroup、PublicProperties/PropertyMappings、ThumbnailResourceId | x3 Symbol 空壳/允许多组 | MEMSymbol 资源定义＋唯一根/公开契约；B；H03 类别、缩略图引用、参数及几何往返 |
| R24 / S13,S18 | ReplaceRootGroup、Children 旧别名投影RootGroup；MapProperty/UnmapProperty、MapPort/UnmapPort、CreateInstanceSpec | x3 无公开契约 | MTC.Expose/Unexpose/SetOverride；B；旧别名只兼容读取，不给新 UI 可写 List；H03 内部目标/版本/映射删除保护 |
| R25 / S13 | Symbol.Logic/Data/Collaboration/Behavior；DeepClone(preserveChildIds) | 只把 Symbol 当图标会丢业务行为 | 保留可选能力/实例隔离；B；由专业/原运行解释，不移执行器到 Core；H03/H07 |
| R26 / S12 | ElementModel：Bounds/Transform/Style/LayerId/ZIndex/Visible/Locked/Selectable；TypeId/Name；Semantics/Behavior/Parameters/Connections | x3 CxElement 是矩形且 sealed | D02/D03 推荐视觉基类，普通视觉叶 Kind.Element；B；H01 基本矩形，H03/H06 持续继承交互/动态化契约 |
| R27 / S12 | CopyCommonTo/DeepClone：几何/样式、Enabled/ExecutionBinding、语义/行为/参数/连接独立复制 | x3 Add 持有 CxElement，仅少量字段 | B；H05 验证所有持久字段独立，不因重命名丢动画/脚本引用；绑定声明与活动执行对象分开 |
| R28 / S12,S14 | GroupElementModel : ElementModel；Children、UseLocalCoordinates/LocalWidth/Height、LocalBounds/GroupTransform/ChildToParent | x3 Group/Element 平级；V1.0 未充分继承 | D02/D07 待确认；当前x3 CxAffine2D只有Identity/Transform，不能假设完整Compose/Inverse；改造复用组领域坐标算法，新增组默认局部坐标，旧绝对坐标显式迁移；B；H03/H06 不变换两次 |
| R29 / S14 | Group/Ungroup：同父/至少两个/图层与编辑锁检查、世界仿射烘焙、在用端口/映射保护、失败恢复 | V1.0 只有 AddGroup，无用户分组闭环 | MTC.Group/Ungroup＋便利委托；B；H03 事务，H06 选择/画布；非可逆/奇异变换拒绝、原件不改 |
| R30 / S12 | SvgVectorElementModel 继承 Group、SanitizedSvg/SourceResourceId/ViewBox/FeatureReport；UnknownElement 原始载荷/校验/重水化 | x3 无 SVG/未知图元 | 迁移几何与未知保真要求；B/D；实际SVG渲染/Sanitizer仍Visual Owner，H04保真/H06显示；不在Core实现渲染器 |
| R31 / S24 | ResourceDescriptor：ResourceId/ResourceTypeId/DisplayName/Version/StorageLocation/FolderPath/ParentResourceId/SourceObject/Capabilities | V1.0 ResourceEntry 方案未区分展示与真源 | A/B；资源树只投影，Folder与ParentResourceId不成为第二拥有树；本地 SourceObject 不序列化为第二份对象 |
| R32 / S24 | IResourceFactory/CreateContext/Result、ResourceTypeRegistry Register/TryGet/Get/CreateBuiltIns、ProjectResourceCatalog.GetResources/Find、ResourceService.Create | x2 工厂/目录在 Studio，Core有独立资源定义；未发现通用 IResource | B/C；纯工厂/目录契约归MEM/MPM，专业实现/Studio编辑器仍原Owner；D01的IMEMResource是新提案；H03 |
| R33 / S36 | CxResourceDefinition：Id/Name/ResourceType/ProviderId/Capabilities/Configuration/Placement/Tags；Set/GetConfiguration/Validate；Registry.Register/Unregister/Find/QueryByCapability | 与 Page/Symbol 资源目录、物理文件不是同一概念 | B；工程设备/能力定义→Content/资源记录＋配置来源；**不复制第三配置系统**；H03/H07 |
| R34 / S23 | 物理 ResourceEntry：Id/Hash/RelativePath/OriginalName/Length/ImportedUtc；ResourceStore 的 Id==SHA256，Find/FindByHash/FindByName/Import/SaveIndex | V1.0 MEMResourceEntry 是逻辑登记，不能按同名直接套用 | 继承内容寻址/防损坏/相对路径规则；B；物理散列不能替代 Guid 节点Id；H04/H05 资源搬迁/去重/缺失回读 |

## 4. 引用、属性、端口、事件、克隆和持久化

| 追溯 | x2 属性/关键方法及已有行为 | x3 差距 | 目标与必须继承的失败边界 |
|---|---|---|---|
| R35 / S15 | CxReference：TargetId/TargetPath/VersionBinding；ReferenceCollection Add/Remove/ResolveAll；Resolver.Resolve/DetectCycles/ExplainFailure | x3 无引用/索引 | MRIReference/Resolve；资源/变量引用转 MPMBinding 但保留语义/来源；B；缺失/冲突Path与Id/错误类型/外部未知目标不能猜叶Id |
| R36 / S16 | CxNodeAddress.Parse/TryParse/Combine/Parent/Resolve；转义路径、Project/Page元数据核对 | x3 Guid Find，无实例链 | MRIObjectAddress 固定资源Id/对象Id/引用链；B/C；保留转义/歧义反例，Guid迁移后地址不依赖显示名/可变Parent路径 |
| R37 / S19 | ProjectObjectIndex/PageObjectIndex/SymbolObjectIndex：Rebuild/FindNode/Page/Content/Symbol、Resolve、Alias/Role/Tag/Search、实例查询；Page空间索引 | x3 递归Find/Scene平铺 | A/B；工程索引归MRI，空间索引/命中测试仍Visual；按Revision失效可重建；H02/H03/H06 |
| R38 / S27,S28,S32 | Metadata Get/Set/Merge/Clone；Property Type/Default/Constraints/Editor/Validate/Reset、Collection Set/Remove；Attributes 集合 | x3 无一般属性 | MPMValue/Binding/Descriptor＋稳定MemberId；B；继承默认显式性/约束/编辑提示，禁止原样任意 object 和运行服务序列化；H03/H04 |
| R39 / S17,S18 | CxPort Kind/Direction/Multiplicity/Contract、Validate/Compatible；PortCollection Add/FindInput/Output/Remove；公共端口与MapPort | 工程端无，MRK运行端已有 | MPC契约＋公开映射＋MTU引用；B；工程不重做运行类型；H03 类型/方向/基数/坐标/在用删除 |
| R40 / S17 | PortAddress.Parse/Resolve、ConnectionResolver.Enumerate/Validate；视觉端口由几何定位 | x3无通用工程连线 | 稳定Endpoint/实例链与显示位置分开；B；独立页面共享数据用绑定，控制线/数据线分开；H03/H07 |
| R41 / S06,S07 | ThreadStatic StructureTransaction：输入forest验证、父列表/根/地址捕获、回滚、索引刷新、提交后发送通知；通知异常不撤销已提交事实 | x3逆操作补偿无锁，不能泛化并发原子性 | A/B/C；保留原子与通知规则，改成每Session写闸门/隔离草稿/Revision；ThreadStatic不能直接跨await复用；H02 |
| R42 / S08,S09 | Event Capture/Target/Bubble、StopPropagation/Immediate/PreventDefault；Observer.Observe/TakeRecords/Disconnect/Dispose | x3无通用节点事件 | 保留交互/通知需求，D05 分域；B；H02 ChangeSet订阅、H06交互传播；不将普通事件监听器作为授权策略 |
| R43 / S05 | Clone/ClonePreserveIds/CloneNode(deep,preserveId)/CloneAsNew；CopyPreserved、RekeyPairs；节点/端口/图层/数据声明、引用/映射/绑定重写 | x3无通用克隆 | 高价值B；M38 PreviewDuplicate/IdMap一次提交；新副本新Id、保留Id只供受控快照；未解析外部目标不“修复”；H05 |
| R44 / S05,S01 | Normalize/BindSubtree/VerifyOwnedTree/RefreshLocalReferencePaths；重建导航/索引、拒绝共享Child | x3 Load校验基础Parent与Id | A/B；内部恢复后验证，不给用户“Normalize修复错误并通过”后门；H01/H04 |
| R45 / S20 | NodeSerializer.Serialize/Deserialize/WriteNode/ReadNode；固定注册类型、同Converter、unknown/plugin分支；WriteTo/WriteContentTo 不关闭调用方Writer | x3 XML直接重建六节点 | B；封装/工厂/回读/流所有权继承，CLR类型查找不直接跨仓；H04/H05；未知载荷原字节保真 |
| R46 / S21,S22 | CxProjectSerializer Save/Load/SavePackage/LoadPackage→DirectoryProjectStore；stage/backup、拷贝资源、回读后切换、取消、失败恢复、拒绝reparse目录 | x3固定.tmp和降级先删旧件存在局限 | 高价值B/C；保存快照/Token/新包容器另实现，继承校验/备份/失败不丢旧件；不把双目录Move说成所有FS原子事务；H01基本保存/H04完整 |
| R47 / S37 | CxProjectManifest.FromProject/Validate/Clone：依赖插件/能力/类型/资源/目标、版本/运行要求，来自同一Project | x3无完整manifest | A/B；MEMSnapshot/依赖锁/执行Handoff，运行目标仍由原Owner解释；H04/H07 |
| R48 / S38 | ProjectFormatMigrator/ProjectMigrationReport、CxMigrationRegistry.BuildPath/Migrate，显式旧版本升级 | x3只收schema1 | B/C；x2 format6 与 x3 schema1 各独立来源，确定性字符串→Guid映射/保留来源；H04/H05 |
| R49 / S25 | UndoService：Execute/Undo/Redo、Nested Begin/Commit/Rollback、Merge、OwnerThread/Busy/Faulted/Changed；失败补偿、故障恢复确认 | x3每Editor私有栈，只记录自身矩形动作 | 保留测试与意图分组；B/C；M07唯一领域历史，M37 UI选择/预览，不照搬第二命令调度入口；H02/H06 |
| R50 / S39 | x2 编辑文档/多文档/DirtyTracker，保存失败保持脏状态、路径与磁盘事实、共享历史 | x3换Editor/固定XML路径 | B；Session统一Dirty/LastSavedRevision/Location；H04/H06；V1.1按Revision脏与x2按内容实际回退不同，明确策略不隐瞒 |

**复用优先顺序**：先迁移不变量及反例测试，再迁移方法体到唯一新提交/快照模型，最后适配 UI 与专业。不能将 x2 Platform 整体引用到 x3 两个 Core，不能将其中旧调度/数据栈覆盖新 MRK/MDS，也不能仅重命名类型就当作完成迁移。

## 5. 测试继承：已存在、已接入、未复跑分别说明

x2 `Tests/Tests/Tests.csproj` 有 `V13/**/*.cs` Compile，V13/Program.cs 明确登记多项测试委托；下表记录真实方法，不虚构为 NUnit 特性测试。存在源码和注册不等于本轮执行成功；本轮未构建/运行 x2，未复用其历史日志冒充新平台验收。后续按阶段将断言迁移到 x3 既有 Tests 工程。

| x2 实际文件/测试方法 | 必须迁移的断言 | 目标阶段 |
|---|---|---|
| CxNodeCloneTests：CxCloneModesRebuildOwnedTreesAndRemapLocalContracts | 深/浅复制、源不变、局部连接/内容引用、图层Id重映射 | H05 |
| 同文件：CxCloneWholeProjectAndWriteMethodsPreserveDefinitionState | 完整工程副本的跨定义引用、Port映射、部署选择及保存重开 | H04/H05/H07 |
| 同文件：CxNormalizeRepairsNavigationAndRejectsSharedChildren | Parent/索引重建，多父拒绝且不修改原归属 | H01/H02 |
| CxMoveNotificationTests：CxMoveObserversSeeCommittedOwnershipReferencesConnectionsAndIndexes | 两端观察者只看到最终归属、最新地址与新索引 | H02/H03 |
| 同文件：CxMoveSourceNotificationFailureKeepsCommittedStateAndNotifiesDestination | 通知异常不能半移动，目标仍收到通知 | H02 |
| 同文件：CxMoveObserverReentryCannotMutateCommittedTopology | 回调重入拒绝，通知后保护释放 | H02 |
| 同文件：CxMoveInvalidDestinationOrderRestoresBothParentsWithoutNotifications | 失败无通知/无索引变化/原状态相等 | H02 |
| CxNodeReferenceMutationTests：CxNodeReferenceCrossProjectMoveRejectsIncomingAndOutgoingDependencies | 不偷搬外部依赖；跨工程改为导入计划 | H03/H05 |
| 同文件：CxNodeRemoveAllRejectsExternalReferenceWithoutPartialRemovalOrNotifications | 全量删除影响预检、原子拒绝 | H02/H03 |
| 同文件：CxNodeSingleRemovalEnforcesMandatoryRootPolicyForEmptyDefinitions | 空页面/符号的受保护根也不能删除 | H01/H02 |
| CxCloneReferenceIdentityTests：CxCloneReferencePreservesConflictingPathAndTargetIdWithoutRepairingIt | 矛盾地址原样诊断，不猜目标 | H03/H05 |
| 同文件：CxCloneAmbiguousIdOnlyReferenceDoesNotGuessItsLocalScope | 歧义不重映射成错误对象 | H05 |
| CxMoveAddressIdentityTests：CxMoveDoesNotRebindForeignNativeHmiOrLogicAddressesByLeafId | 同叶Id的外部地址不被本地Move重写 | H03/H05 |
| CxEditingFoundationTests：CxEditingNestedTransactionsUndoRealObjectsInReverseOrder | 一个意图一历史、逆序撤销真实对象 | H02 |
| 同文件：CxEditingFailedCompensationFaultsUntilExplicitRepair | 逆操作异常不能静默继续可信历史 | H02 |
| 同文件：CxEditingDocumentSaveFailuresKeepDirtyPathAndDiskFacts | 保存失败不能清脏/改路径/丢旧磁盘 | H04/H06 |
| 同文件：CxEditingEightCaseProjectPreservesGeometryLogicAndConnectionsAfterEditSaveLoad | 组合工程保存重开保持几何/逻辑/连接 | H04/H06 |
| CxImportExportFoundationTests：CxImportExportCancellationPreservesOldTargetAndCleansStage | 取消前后提交点、旧件及临时件 | H04/H05 |
| 同文件：CxImportExportWorkspaceCopiesResourcesAndKeepsMissingResourceFailureAtomic | 静态依赖复制/缺失原子拒绝 | H04/H05 |
| 同文件：CxImportExportEightCasesRoundTripAndRemainEditableAsOneProject | 八案例同一工程往返仍可编辑 | H05/H06 |
| CxArtifactTests：CxArtifactChecksumAndManifestDetectCorruptionAndEscapingUris | 散列/manifest/路径逃逸拒绝 | H04/H05 |
| CxDataCloneIdentityTests：CxDataBindingMoveTracksPathGuardsRemovalAndRollsBack | 声明绑定迁移而非活跃数据克隆 | H03/H05 |
| CxBaselineCompareTests：CxCompareIgnoresObjectKeyOrderButKeepsArrayOrderAndNullPresence | 比较不丢拥有顺序/null显式性/未知字段 | H04/H05 |
| CxBulkMutationTests：CxBulkSiblingInsertFailureRestoresAllParents | 批量插入失败恢复所有父/地址、无通知，未附着对象保持未附着 | H02 |
| CxNodeRoundTripTests：CxNodePageRootReplacementIsAtomicAndRoundTrips | 根替换影响预检/原子回读，旧字段与根身份保持 | H01/H04 |
| 同文件：CxNodePropertiesProjectExistingFieldsAndPersistCustomValues | 原字段访问器与自定义属性不双写，不丢默认/类型 | H03/H04 |
| 同文件：CxNodeValidationReportsStructuralDefectsWithoutMutation | 验证只报告，不修坏原件再假通过 | H01/H02 |
| CxPublicContractTests：CxPublicPortsCoverEventCommandAndResourceContracts | Event/Command/Resource公开契约、兼容和基数 | H03 |
| 同文件：CxSymbolMappingsResolveAndSurviveCloneAndEditUndo | 公开属性/端口映射在克隆、编辑和撤销后正确 | H03/H05 |
| CxUnknownFoundationTests：CxUnknownNodePreservesArbitraryMetadataFutureFieldsAndDateStrings | 未知扩展/日期字符串不被规范化损坏 | H04 |
| 同文件：CxUnknownNodeCannotSilentlyCompileAsExecutableNoop | 缺插件不能以空操作“成功执行” | H04/H07 |
| 同文件：CxUnknownNodeCorruptSaveLeavesLastGoodWorkspaceUntouched | 坏未知负载保存失败保留最后完整工程 | H04 |


测试源链接（与上表方法名共同构成证据）：

- [CxArtifactTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxArtifactTests.cs)
- [CxBaselineCompareTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxBaselineCompareTests.cs)
- [CxCloneReferenceIdentityTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxCloneReferenceIdentityTests.cs)
- [CxDataCloneIdentityTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxDataCloneIdentityTests.cs)
- [CxEditingFoundationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxEditingFoundationTests.cs)
- [CxImportExportFoundationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxImportExportFoundationTests.cs)
- [CxMoveAddressIdentityTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxMoveAddressIdentityTests.cs)
- [CxMoveNotificationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxMoveNotificationTests.cs)
- [CxBulkMutationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxBulkMutationTests.cs)
- [CxNodeCloneTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxNodeCloneTests.cs)
- [CxNodeReferenceMutationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxNodeReferenceMutationTests.cs)
- [CxNodeRoundTripTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxNodeRoundTripTests.cs)
- [CxPublicContractTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxPublicContractTests.cs)
- [CxUnknownFoundationTests](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/CxUnknownFoundationTests.cs)

## 6. 固定源码证据索引

以下链接固定到 x2 审计提交；行号在本轮由本地源码定位。历史架构文档 V1.3 的“Element是叶子”描述的是语义种类，实际 Group 继承 Element，二者须同时记录。历史 Workbench 树图与实际 Project 工作台登记不同；以实际代码为复用事实，以 x3 当前批准原则为目标边界。

| 键 | 固定源码 / 起始公开声明 |
|---|---|
| S01 | [Platform/Core/CxNode.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNode.cs#L9) |
| S02 | [Platform/Core/CxNodeOwnership.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeOwnership.cs#L8) |
| S03 | [Platform/Core/CxNodePolicy.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodePolicy.cs#L8) |
| S04 | [Platform/Core/CxNodeCollection.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeCollection.cs#L6) |
| S05 | [Platform/Core/CxNodeCloning.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeCloning.cs#L12) |
| S06 | [Platform/Core/CxNodeMutationTransaction.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeMutationTransaction.cs#L7) |
| S07 | [Platform/Core/CxNodeStructureNotifications.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeStructureNotifications.cs#L8) |
| S08 | [Platform/Core/CxNodeEvents.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeEvents.cs#L9) |
| S09 | [Platform/Core/CxNodeMutationObserver.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeMutationObserver.cs#L6) |
| S10 | [Platform/Core/Projects.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/Projects.cs#L8) |
| S11 | [Platform/Core/UnifiedObjectModel.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/UnifiedObjectModel.cs#L10) |
| S12 | [Platform/Core/Elements.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/Elements.cs#L11) |
| S13 | [Platform/Core/Symbols.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/Symbols.cs#L8) |
| S14 | [Platform/Core/CxGroupOperations.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxGroupOperations.cs#L8) |
| S15 | [Platform/Core/CxNodeReferences.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeReferences.cs#L10) |
| S16 | [Platform/Core/CxNodeAddress.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeAddress.cs#L9) |
| S17 | [Platform/Core/CxNodePorts.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodePorts.cs#L9) |
| S18 | [Platform/Core/CxDefinitionMappings.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxDefinitionMappings.cs#L9) |
| S19 | [Platform/Core/ProjectObjectIndex.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/ProjectObjectIndex.cs#L7) |
| S20 | [Platform/Persistence/Project/CxNodeSerializer.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Project/CxNodeSerializer.cs#L11) |
| S21 | [Platform/Persistence/Project/CxProjectSerializer.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Project/CxProjectSerializer.cs#L11) |
| S22 | [Platform/Persistence/Package/DirectoryProjectStore.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Package/DirectoryProjectStore.cs#L15) |
| S23 | [Platform/Persistence/Resources/ResourceStore.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Resources/ResourceStore.cs#L12) |
| S24 | [Host/Studio/Application/Studio/Resources/ResourcePlatform.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Host/Studio/Application/Studio/Resources/ResourcePlatform.cs#L20) |
| S25 | [Platform/Workbench/Editing/CxUndoService.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Workbench/Editing/CxUndoService.cs#L12) |
| S26 | [Platform/Core/CxProjectDefinition.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxProjectDefinition.cs#L9) |
| S27 | [Platform/Core/CxNodeMetadata.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeMetadata.cs#L10) |
| S28 | [Platform/Core/CxNodeProperties.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeProperties.cs#L13) |
| S29 | [Platform/Core/CxNodeQuery.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeQuery.cs#L8) |
| S30 | [Platform/Core/CxNodeIterator.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeIterator.cs#L8) |
| S31 | [Platform/Core/CxNodeWalker.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeWalker.cs#L7) |
| S32 | [Platform/Core/CxNodeAttributes.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeAttributes.cs#L10) |
| S33 | [Platform/Core/CxPublicContracts.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxPublicContracts.cs#L10) |
| S34 | [Platform/Core/CxNodeForestMutation.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodeForestMutation.cs#L6) |
| S35 | [Platform/Core/CxNodePosition.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxNodePosition.cs#L9) |
| S36 | [Platform/Core/CxResources.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxResources.cs#L10) |
| S37 | [Platform/Persistence/Project/CxProjectManifest.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Project/CxProjectManifest.cs#L10) |
| S38 | [Platform/Persistence/Project/ProjectMigration.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Project/ProjectMigration.cs#L8) |
| S39 | [Platform/Workbench/Editing/CxDocumentHost.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Workbench/Editing/CxDocumentHost.cs#L12) |

补充：[Platform/Core/CxUnknownNode.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxUnknownNode.cs)。

补充：[Platform/Core/CxPublicContracts.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Core/CxPublicContracts.cs)。

补充：[Platform/Persistence/Project/CxMigration.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Persistence/Project/CxMigration.cs)。

补充：[Platform/Workbench/Editing/CxDirtyTracker.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Platform/Workbench/Editing/CxDirtyTracker.cs)。

补充：[Host/Studio/Application/Studio/Documents/DocumentPlatform.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Host/Studio/Application/Studio/Documents/DocumentPlatform.cs)。

补充：[Tests/Tests/Tests.csproj](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/Tests.csproj)。

补充：[Tests/Tests/V13/Program.cs](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Tests/Tests/V13/Program.cs)。

历史设计：[CxVisual_统一对象与多Aspect架构设计基线_V1.3.md](https://github.com/oudream/cx-visual-x2/blob/2626734d0fd5e67ac50fd0621532173cec39b939/Archs/CxVisual_%E7%BB%9F%E4%B8%80%E5%AF%B9%E8%B1%A1%E4%B8%8E%E5%A4%9AAspect%E6%9E%B6%E6%9E%84%E8%AE%BE%E8%AE%A1%E5%9F%BA%E7%BA%BF_V1.3/CxVisual_%E7%BB%9F%E4%B8%80%E5%AF%B9%E8%B1%A1%E4%B8%8E%E5%A4%9AAspect%E6%9E%B6%E6%9E%84%E8%AE%BE%E8%AE%A1%E5%9F%BA%E7%BA%BF_V1.3.md)。它记录Graph退出、四Aspect、RootContent/RootGroup与Content/Symbol复用；Workbench图示、旧DataRef默认和实际代码/x3新决议须分别辨析。

## 7. 迁移提交必须带出的证据

每个移植方法在提交说明或阶段验收中列：R编号、来源SHA/文件/成员、目标Owner/FQN、保留行为、刻意改变行为、相关测试结果和未知限制。字符串 Id 若可解析为非空Guid则保留值，否则用确定性映射且保存原文；**不能统一 Guid.NewGuid 后丢掉引用或来源**。同旧Id在不同工程使用包含来源工程身份的键，冲突/重复Id拒绝，不静默合并。

未知扩展、Bounds/Transform/Style/图层、绑定/参数/端口、公开映射、行为/脚本引用、页导航、专业能力/资源依赖均须有明确去向。旧字段无法表达时报告并保留原载荷；不能仅用“新格式没有这个字段”作为删除理由。未确认 D01/D02/D03 的结构不得先写成新代码；本轮复用分析是后续实施依据，不是迁移完成报告。
