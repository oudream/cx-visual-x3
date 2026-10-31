# CxVisual Symbol 体系——实施分解、源码基线、任务与验收台账 V2.0

> 2026-10-31；**设计阶段初始台账，非已完成开发报告**。依据 [#0 总纲](../Architecture/CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)和 [#7 API](../Architecture/CxVisual_Symbol_07_全类UML与API合同_V2.0.md)；每个任务完成时必须更新最新 master SHA、源码清单、Tests、Windows CI 链接与遗留项。
> 本轮只新增文档，不修改 C#、csproj 或项目结构。已有源文件是“可复用底座”而非“Symbol 体系完整验收”。

## 00. 正式范围、负责人/Owner 与本轮状态
| 指标 | 当前事实 | 未达到的目标 |
|---|---|---|
| 工程版式 | 26 工程、.NET Framework 4.8/WPF、Foundation/Mechanism/Professional | 不允许因此任务新增第 27 个工程 |
| 共享资源 | `MEMSymbol/RootGroup`、`MEMContent/MRIReference` 与资源中心真实存在 | 无损 Group→Symbol 提升与统一孵化 |
| Type 注册 | `IMPMTypeProvider/MPMTypeDescriptor/Session.RegisterTypes` 已存在 | Attribute/入口函数/Fluent 一次声明、一致发现 |
| Capability 目录 | `IMOCCapabilityProvider/MOCCapabilityCatalog` 已存在且支持离线读 | Type↔Capability 关联与专业模板自动匹配 |
| Visual 端点 | 四向矩形边中点 + `ConnectVisual/ReconnectVisual` | FixedLocal/真实轮廓 Floating/FreeLocal |
| Studio | `doc.symbol-center` 三来源分类、类型化创建/导入/实例覆写已见代码 | 高质量孵化/公开端口/状态行为全闭环 |
| Runtime/专业 | MOC/MRK/MDS/MHS 与部分 HMI Faceplate、Vision Provider 有基础 | 共同标准合同 + 工业设备完整验收 |

**Owner**：Core（对象/端口/编辑/迁移），Visual（几何与视图投影），Decision（MOC目录/匹配/编译），Runtime/专业（执行/安全），Presentation.Wpf（共享 UI），Studio（创作与装配），TestCenter（证据）。负责人的姓名、时间和具体工单后续由项目负责人分配；不在本台账编造人名/完成日期。

## 01. 当前证据索引：已核实源码，不代表全部覆盖
| 编号 | 路径 | 可用基础 | 不能认定 |
|---|---|---|---|
| E01 | `Mechanism/Core/Engineering/MEMNode.cs` | 独立 Symbol、唯一 RootGroup、Content/Project | 完整组件化编辑 |
| E02 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | Group/Element 层级、变换、MPM 合同 | Group 就是可发布 Symbol |
| E03 | `Mechanism/Core/Engineering/MPMTypeDescriptor.cs` | DescribeTypes/RegisterTypes/FindType/RegisteredTypes | 不产生 Revision 的全局自动类型安装 |
| E04 | `Mechanism/Core/Engineering/MTCEditSession.Contracts.cs` | ExposeProperty/ExposePort、DefinePort/Connect/SetOverride | 自动边界公开无损 |
| E05 | `Mechanism/Core/Engineering/MTIPackages.cs` | Import/Export/ExtractReusable | Group→Symbol `ExtractReusable` |
| E06 | `Mechanism/Decision/MOCCapabilityCatalog.cs` | MOC 静态查询、Version/Port、Search | 已具一键注解全局注册 |
| E07 | `Mechanism/Visual/Editing/MVIVisualAnchors.cs` | 四向固定视觉点和路由辅助 | 真实轮廓附着 |
| E08 | `Mechanism/Visual/MVIVisualEditor.cs` | ConnectVisual/ReconnectVisual/路由修改 | 任意点、实例私有 Port 外连 |
| E09 | `Applications/Studio/StudioShell.SharedResourceCenters.cs` | 工程资源/包/类型三来源、实例与覆写 | 七页签孵化与版本发布 |
| E10 | `Professional/HMI/PHMFaceplateAuthoring.cs` | 电机/阀门共享 Faceplate/在线点绑定 | 实机命令安全闭环 |
| E11 | `Mechanism/Presentation.Wpf/Modules/MPWModuleCenterControl.cs` | 只读能力目录 UI | 持久全局组件库 |
| E12 | `.github/workflows/testcenter-windows.yml` | Windows .NET48 build/部分 WPF smoke 回归入口 | 当前 HEAD Symbol 全目标通过 |

## 02. 工作分解：先打通纵向，再精化孵化

### D0：基线、差异与 Owner 决议
| 项 | 执行要求 |
|---|---|
| D0-1 | 读取最新 master `AGENTS`/Skill/00、工程组织 V1.2、Decision #3/#4、图元编辑 V1.2，并**逐 Sheet** 比对 DOCS/02～04 中 M01/M02/M03/M04/M05/M07/M38、Visual、Decision、专业对象 |
| D0-2 | 固定当前 API/Member/Kind/Namespace、物理工程 DAG；建立“真实源码 vs 本系列候选”差异表 |
| D0-3 | 明确 Type↔Capability 关联的正式持久归属、端点附着的 Core 最小 Schema、注册 Facade 放哪一个现有项目；审议 `MEMSymbol.References` 与历史 Content 复合约束的 C01 差异 |
| D0-4 | 运行现有工程/Visual/Studio/HMI/Decision 黄金回归并留固定 SHA 证据 |
| 产物 | 本台账更新证据清单 + 决策记录 + 提议 API 签收/拒绝表 |
| 完成门禁 | 不得在 Excel/现有代码未核实时新增 20 个类或新工程 |

### D1：两个跨体系真实黄金样例
| 项 | 执行要求 |
|---|---|
| D1-1 | 从 `PHMFaceplateAuthoring` 开始贯通 Motor Definition→双实例覆写/数据→Content Event→受权 Host 软件级拒绝/执行→运行状态投影 |
| D1-2 | 选择 Vision ROI/Inspection **或** Automation 虚拟点位作为第二专业；同一 MPM/MPC/MOC 发现、类型匹配、MOL Build/SourceMap/运行结果 |
| D1-3 | 补足缺失状态/能力实际 ID、两实例 InstancePath/Runtime Scope，禁止在 Studio 直接调用专业 DLL 绕过 MOC |
| 产物 | 可运行正反案例、SourceMap/Trace、软件级 TestCenter、专业 owner 证据 |
| 完成门禁 | 真正复用已有引擎；无设备不填写 HIL 通过 |

### D2：一次声明与同源发现
| 项 | 执行要求 |
|---|---|
| D2-1 | 实现最小 `RegisterSymbols` 显式入口/可信 Provider 解析；优先无反射入口，次阶段添可选 Attribute |
| D2-2 | 规范化并校验 `MPMTypeDescriptor`、`MPCPortDescriptor`、`MOCCapabilityDescriptor`、FCM Schema 和 CLR/Host 约束；同 ID 不同 Schema 拒绝 |
| D2-3 | 在 `MPWModuleCenterControl`/`doc.symbol-center` 增只读 Type↔Capability/包/工程资源来源投影和关联搜索 |
| D2-4 | 插入用户批准的 `RegisterTypes`/AddTypedSymbol/PlaceSymbol 的显式安装流程，浏览不改 Revision |
| 产物 | 入口与 Provider 兼容、普通 HMI + 第二专业不改 Studio 特例、REG-01～16 |
| 完成门禁 | 一个能力单一元数据/Port；无第二 Catalog/Registry；失效原因可解释 |

### D3：Group→Symbol 一次事务提升
| 项 | 执行要求 |
|---|---|
| D3-1 | MVI/Studio 选区和 Visual/MPM/MPC/Aspect/Asset 依赖边界快照，含 Group 嵌套变换 |
| D3-2 | Core/MTI 预览 Definition/RootGroup/IdMap/PublicMapping/ExternalConnection/InstancePlacement |
| D3-3 | “复制/替换”分别执行，替换模式单一 MTC 事务/一次 Undo，失败保留原 Group 及全部线和 Binding |
| D3-4 | UI 自动提议但不私自公开敏感内部成员；保存重开、跨项目 Export/Import 回归 |
| 产物 | G01 + OBJ/UI 相关通过、源码覆盖及迁移边界 |
| 完成门禁 | 复杂 Group/外部连接可解释处理，**任何不支持情形拒绝而不是丢内容** |

### D4：视觉附着与连接契约
| 项 | 执行要求 |
|---|---|
| D4-1 | Core 审核 `MPCConnection` 端点持久元数据、旧 Schema、MTI 重映射、Clone、版本；
| D4-2 | Visual 使用 FGE 真实轮廓实现 FixedLocal/FloatingOutline/FreeLocal 的纯求解，旋转、非均匀缩放、Bezier/Arc、多轮廓负例 |
| D4-3 | SceneView 端点可视/吸附/HitTest/鼠标拖拽与已有四向快捷箭头自然共存 |
| D4-4 | Definition 与实例的 Public Port 限制、重连 ID、Undo/Redo/保存重开/旧四向视觉一致 |
| 产物 | G01 复杂连线 + CON/UI/GOV 锚点测试 |
| 完成门禁 | 旧工程四向位置不变、新端点不产生 WPF 私有永久状态 |

### D5：Studio 孵化与专业/脚本/版本体验
| 项 | 执行要求 |
|---|---|
| D5-1 | 按 #4 七页签填充可用功能，不以占位按钮冒充专业绑定 |
| D5-2 | 受限声明式状态规则 Good/Bad/Unknown、事件请求到授权 Host；脚本 R2/R3 需独立安全审评 |
| D5-3 | 资源来源/依赖影响/SchemaHash/对比与迁移预览，补齐所需 Core 最小合同 |
| D5-4 | 相同 MPW UI 在 Studio/TestCenter，共用项目操作语义、不同授权/测试会话 |
| 产物 | G02/G07 和错误分类；WPF 真实输入、DPI/焦点/性能证据 |
| 完成门禁 | 用户无需 GUID/类全名手工填绑定；非法专业命令不会到设备 |

### D6：工业交付与持续演进
| 项 | 执行要求 |
|---|---|
| D6-1 | 全 Case G01～G08、版本迁移、旧工程、跨项目包复现 |
| D6-2 | Windows .NET48 x64 Debug/Release、结构/DAG、自动测试、WPF UIA、规模基准、跨进程与崩溃恢复 |
| D6-3 | 真实 PLC/X-Ray/运动/CT/Inspection 按专业安全门禁单独 HIL；无设备报告 NotRun |
| D6-4 | 收敛最终类清单/Owner、工程内文档导航、用户教程与已验证案例 |
| 产物 | 完整测试报告 + 固定 SHA CI + 现场验收和未测项 |
| 完成门禁 | 任何未覆盖/Unknown/伪成功都是阻塞或显式范围外，不能整体宣称工业完工 |

## 03. 实施次序与并行开发接口
| 主线 | 必须先具备 | 可并行的安全工作 | 禁止同时修改导致的风险 |
|---|---|---|---|
| Core/MPM/MPC | D0 Schema Owner | 类型注册与提升预览 | 端点持久格式与 Type/Port 变化冲突 |
| Visual/MVI | Core 最小 Attachment 合同 | 纯轮廓求解/渲染预览 | 另存 Port/Connection 或偷偷改 ID |
| Decision/MOC | MPM/MPC 类型化描述 | 只读 join/搜索/候选解释 | Decision 增 Runtime 编译依赖 |
| Studio/MPW | 可查询/事务 API | 控件分层/用户流程/快捷键 | WPF 主导修改 MEM/运行状态 |
| Professional/Runtime | CapabilityId/Port/Host 安全模型 | HMI Motor/Vision/CT3D/Automation 验收 | UI 直接调用设备绕过发布与授权 |
| TestCenter | 共同接口与测试夹具 | 测试分类/报告/按固定 SHA 验证 | Mock/软件测试冒充 HIL |

**项目持续并行**：每次工作包开始先查询最新 master/Owner 改动；不要仅靠某旧固定 SHA 绿灯；完成后文档台账记录实际 commit 和可能与其他开发线冲突的接口文件。

## 04. 编码与兼容规范
- C# .NET Framework 4.8、VS2022/WPF、现有 26 工程和命名前缀；不引入最新 C# 语言/依赖导致无法在现有 MSBuild 编译的写法。
- 遵守当前 `Build/Verify-Structure.ps1` 和 `Build/Verify-OwnerDependencies.ps1`；真正构建需 Windows runner。
- Core 修改对 MTC Edit/Undo、XML/Schema、MTI 导入/复制、MPC Port/Connection、MPM Type 注册进行成套回归；不得以类级单测代替工程打开/保存。
- UI 新操作在 `SceneView`/`MVIVisualEditor`/`StudioShell` 的现有用户动作入口接线，不新建独立项目或临时真源。
- 新增通用类前先核对 DOCS/04 各类目标/API 和真实代码；枚举/错误名应复用仓库真实规范。
- 软件开发者入门文档必须给**已存在的可编译 API** 例子；提议 SDK 在正式实施前均标“候选”。

## 05. Windows/CI 验收入口（经实际文件核实）
实际 GitHub Workflow：`.github/workflows/testcenter-windows.yml`。现有步骤包括：
```powershell
./Build/Verify-Structure.ps1
./Build/Verify-OwnerDependencies.ps1
msbuild 'CxVisual.Unified.sln' /m:1 /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /nologo /v:minimal
# 当前工作流里存在 --graphics-viewer-smoke、--ui-smoke、--ui-automation 等
```
新增 Symbol 用例应进入相应 ArchitectureSmoke / TestCenter 实际项目，**不要把上面命令写成已在本次文档提交 SHA 通过**。文档提交可验证 Markdown 结构；业务代码没有变动，不意味着目标编译/测试已完成。

## 06. 验收记录模板（每一次阶段结束必须填写）
```text
工作包：D? / SYM-...
Base SHA / Final SHA:
Owner / Changed Files:
新增/复用实际 API:
目标 vs 实际差异:
本地测试：命令、环境、通过/失败及输出
Windows CI：Workflow URL、Job/Conclusion、SHA
GUI：UIA/真实键鼠/DPI/截图/失败
专业：Real/Virtual/Replay + HIL设备序列/批准者/安全确认
兼容：旧项目/旧锚点/MTI包/多实例保存重开
未完成与阻塞：编号、严重度、再现方法
允许对外宣称的完成等级：G0/G1/G2/G3
```

## 07. 现阶段总评
- **已验证存在的基础**：MEMSymbol、MPM 类型注册、MPC 端口和公开映射、MVI 四向连接、MOC 只读目录、Studio Content/Symbol 中心、MTI 包、HMI Faceplate 专业用例。
- **明确尚未完成**：统一 SDK 注解/入口注册、跨目录完整关联与实例化、Symbol 孵化器七页签、Group→Symbol 原子转换、任意/轮廓附着和全专业绑定。
- **没有本次新测试数据**：不宣称本轮 Windows CI/UIA/HIL 通过；实现开始前先查最新测试与源码。
- **先行最小目标**：D0→D1（纵向验证 Symbol 对 Content/编排价值）→D2→D3；D4～D6 依据真实边界分阶段实现。
