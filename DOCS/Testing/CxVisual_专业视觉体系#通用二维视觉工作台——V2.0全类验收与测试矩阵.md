# CxVisual 通用二维视觉工作台 V2.0——全类验收、UML反证与测试矩阵

> **测试策略与尚未执行状态**：2026-10-28制定全类质量门禁，覆盖26类/260二级能力、26张UML和8个Case。本版仅增文档，不等于260项已测试。源代码、专业SDK、现场数据与Windows CI按对应固定SHA填写证据，不允许将既往Smoke成功泛化至目标全功能。

## 1. 七级证据及状态定义（每个V2D-XX.YY独立记录）

| 级别 | 达标条件 | 不允许误报 |
|---|---|---|
| G0 需求定义 | 功能编号、用户场景、输入输出、对象和Owner明确 | 不是实现 |
| G1 设计审核 | 设计与UML、线程/生命周期/参数/异常审查通过 | 不是代码 |
| G2 源码实现 | 当前master真实编译纳入的类与使用入口存在 | 不是可用性 |
| G3 Windows编译 | 固定SHA .NET Framework4.8 x64完整相关解决方案构建 | 不是运行测试 |
| G4 自动数值/行为 | 对应功能正反例、取消/版本/资源、专项/默认回归通过 | 不是物理人机体验 |
| G5 用户操作闭环 | 真实WPF控件/UIA/API与结果/导出，人工交互按需补齐 | 不是设备/长期性能 |
| G6 专业/工业验收 | 代表数据、厂商SDK/许可证、真实相机/探测器及现场HIL、性能 | 不能仅凭模拟截图标记 |

状态必须独立记录：Spec、Review、Coded、BuildPassed、AutoPassed、UiPassed、FieldPassed、Blocked、NotApplicable（附理由）。CI BuildFailed不抹掉已存在源码，但最新高等级必须有匹配SHA和正/反日志。

## 2. 单项统一验收字段

V2D能力ID / 来源需求ID / Scene与角色 / 输入样本/格式/单位/坐标 / 参数与默认/非法 / 输出Schema / SourceRef、RunId、ArtifactRef / Owner和实际路径 / UML ID / 契约版本 / 成功值与容差 / 空结果 / 无效输入 / 异步取消/版本冲突 / 性能预算 / WPF/SDK步骤 / 负向安全/资源反证 / Windows SHA+workflow / 专业设备+环境 / 结论与复测日。

每个二级功能至少要求**一条正例、一条反例、一个用户或开发者可调用入口**；涉及耗时/资源/运行状态者补取消和内存；涉及工程持久性者补保存重开、Undo/Redo和冲突；涉及专业算法者补真实样本和同图对照。

## 3. 26类测试总矩阵（目标，未作为已通过结果）

| 一级能力 | 主要测试重点 | UML | 最小质量证据 |
|---|---|---|---|
| V2D-01 输入接入 | 格式/坏帧/大小/时间戳/Provider故障 | C01/C02/SQ01 | 文件+内存+未实现设备项明确分级 |
| V2D-02 数据模型 | 8/16/float通道、Stride、坐标、元数据 | C01/C02 | 原像素等值及非法大小反证 |
| V2D-03 序列与数据集 | 顺序、时戳、坏帧、可取消预取 | C02/ST04 | 多帧单源与多源保持身份 |
| V2D-04 大图/生命周期 | 4300²、大图峰值/多副本/Lease | C02/DP02 | Windows压力报告+泄漏检测 |
| V2D-05 显示与渲染 | 窗位/伪彩/Mask/GPU回退 | C03/P02 | 原像素不变+截图回环 |
| V2D-06 视口坐标 | Flip/Rotate/Zoom/Spacing、多DPI | C03/SQ02 | 原图↔视口↔物理误差容差 |
| V2D-07 多图比较 | 同步/Swipe/Blend/注册配准 | C03/P02 | 双Pane真实像素/SourceRef |
| V2D-08 ROI标注 | 全形状/孔洞/图层/锁/ID | C03/C08 | 数值区域真实、显示/保存一致 |
| V2D-09 编辑交互 | 端点/控制柄/四点State/Esc | C04/ST01/SQ02 | 重合拒绝/失捕/选中一致 |
| V2D-10 基础处理 | 裁剪/拼接/插值/类型/溢出 | C01/C05 | 数值真值、原图不变 |
| V2D-11 增强修复 | 滤波/参数/边界/质量 | C05 | 基准结果+异常/性能 |
| V2D-12 分割区域 | 阈值/区域生长/Mask/洞/轮廓 | C01/C05 | Gray16/复杂ROI/面积真值 |
| V2D-13 图像分析 | Stats/Histogram/Profile/频域/纹理 | C05/C07 | 准确采样与数据/图表一致 |
| V2D-14 测量计量 | 角/距/圆/亚像素/标定 | C07/SQ04 | 非等距/未标定/拟合残差 |
| V2D-15 匹配配准 | 点、变换、残差、失败判定 | C05/SQ03 | 真实配准/逆变换/容差 |
| V2D-16 工业检测 | 候选区域/分类/专业阈值/证据 | C05/C07 | 专业同样本+结果一致 |
| V2D-17 AI智能视觉 | 模型/预后处理/设备/追溯 | C05/DP02 | 现阶段只核接口，不冒充模型实测 |
| V2D-18 能力/参数 | Descriptor/Schema/TypeId/Provider | C05/C06 | 合法可发现、非法拒绝、版本正确 |
| V2D-19 算法实验 | 端口连接/中间结果/断点/重跑 | C06/ST02/SQ03 | 2—4节点真实链可复现 |
| V2D-20 批量实时 | 顺序/取消/背压/Worker/预算 | ST02/ST04/SQ06 | 长时压力、Drop策略、租约释放 |
| V2D-21 结构化结果 | 多类型Run/稳定Id/Trace/复核 | C07/SQ04 | 不同面板同一结果和错误状态 |
| V2D-22 报告交换 | 图片/CSV/JSON/PDF/Report回读 | C07/SQ04 | 字节/编码/单位/版本/安全 |
| V2D-23 工程版本 | MEM资源/编辑/Undo/Save/Open | C08/ST03/SQ05 | 冲突零提交、资源可恢复 |
| V2D-24 工作台/API | Studio/TCT/嵌入/独立/快捷键 | P02/DP01 | 真实操作/API/多DPI/UIA |
| V2D-25 跨专业 | Automation/CT3D/Inspection/HMI | P03/SQ06/DP02 | 稳定合同/版本/权限和HIL |
| V2D-26 质量治理 | Trace/资源/权限/构建/回归 | P01/DP02 | 对应SHA、长时和现场证据 |

**关键规则**：上述一级行只定义测试重点；必须从01逐一扩展为260个V2D-XX.YY条目和实际测试ID，不能因一级某个Smoke成功将其10条全部勾选。

## 4. 八个端到端Case及可观测反证

| Case ID | 可复制执行步骤 | 必须失败的反例/判据 |
|---|---|---|
| CASE-01 单图 | Gray16→ROI→分析/测量→导出 | Gray16调窗不得改变原图；坏ROI拒绝 |
| CASE-02 算法实验 | 预处理→分割→轮廓→拟合→多类型RunResult | 改参数旧Run不能回写；非法端口拒绝 |
| CASE-03 配准比较 | 两图→Transform→同步视口→差异统计 | 错尺寸/缺Transform/坐标空间误用拒绝 |
| CASE-04 序列批次 | 10/100/1000帧→Batch→汇总/错误明细 | 乱序/取消/坏帧不能错误标成功 |
| CASE-05 在线视觉 | 设备帧→队列→Vision→Inspection/HMI | 满载明确背压/Drop；掉线/恢复/安全 |
| CASE-06 X-Ray/CT投影 | 多帧投影→局部缺陷/灰度→CT3D引用 | FrameId和CalibrationRef不丢/不混 |
| CASE-07 正式工程 | ROI/Pipeline→编辑→保存→关闭→重开→Undo/发布 | 版本冲突/缺引用/权限拒绝无半提交 |
| CASE-08 AI预留 | 图像→传统增强→模型Provider合同→TypedResult | 模型缺失有明确拒绝；无实际模型不标FieldPassed |

**时间预算和性能指标**：大图、帧率、GPU和内存上限依具体客户硬件和图像样本定基线；先量化峰值、P50/P95延迟与CPU/GPU占比，不允许以虚构“60fps”宣称工业可靠。

## 5. UML正反测试要求

| UML族 | 主反证 |
|---|---|
| U01—U03 | 每个用户任务有端到端入口；TCT不作为唯一算法Owner |
| P01—P03 | csproj依赖DAG、跨专业不持Vendor/WPF类型 |
| C01—C08 | 所有权/稳定Id/深浅拷贝/ROI与结果工程边界 |
| ST01—ST04 | 无效转移、取消、重入、超时、源切换时状态清理 |
| SQ01—SQ06 | 晚到结果拒绝、Observer提交后异常、MEM原子Commit、MDS Pending Abort、背压 |
| DP01—DP02 | Studio退出独立Runtime、SDK加载/授权/设备可用性、故障回退 |

## 6. 真实Windows执行与CI登记

现有可核实旧命令：Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe --imaging；CxVisual.TestCenter.exe --imaging-viewer-smoke（须通过Windows进程返回码、120秒内结束和截图/数值日志验证）；工作流.github/workflows/imaging-foundation-windows.yml。真实Windows.NET48 x64构建、Owner DAG、专项FIM/MVI、WPF及默认回归和TestCenter UIA为不同门禁。统一测试需保留已有行业/图元/运行回归，不可为单一绿灯把失败Step永久跳过。

**历史说明**：Imaging #40619073480 对应60992cb5，Windows构建和WPF控件Smoke成功，但默认测试出现F17.PIPE_BULK子进程退出失败，整体Job failure。TestCenter #40619073429 对应同SHA success。上述均非本轮V2.0提交的测试。代码更新后重新查对应SHA的Jobs/Steps/日志，再填写本台账；未知就写“未执行”。

## 7. 证据记录表模板（逐V2D二级功能扩展）

| V2D-ID | 用例/数据集Hash | 功能代码SHA | BuildRun | NumericTest | WPF/UIA | Performance | SDK/HIL | 结论 |
|---|---|---|---|---|---|---|---|---|
| V2D-XX.YY | 待登记 | 待实施 | 未执行 | 未执行 | 未执行 | 未执行 | 未执行 | G0（需求目标） |

结论要求标实际执行时间、图像规格和依赖版本；每次实现新增案例或更改数值语义，更新该行，不修改既往证据为新的虚构成功。

## 8. 验收执行顺序

①先审源码与Owner/ReferTo → ②针对该能力写数值/反例 → ③实现真实API → ④接WPF用户操作或无界面调用 → ⑤Windows .NET48 x64专项/全回归 → ⑥记录SHA/错误/性能 → ⑦专业同样本与设备HIL（适用时）。不能把“UML图写好”当作G2，也不能把“CI成功”当作完整260项FieldPassed。

## 9. 先行架构评审的四道Gate与反证（2026-10-28）

| Gate | 必须完成的设计证据 | 后续代码正反验收 |
|---|---|---|
| A 核心对象/Owner | 00§11和02 X01—X04；MEM/MOL/MPM/MOC/MRK/MDS唯一权威 | 不存在第二持久Graph/History/DataStack；依赖DAG合格 |
| B 数据/交互 | ImageBuffer与CxVisionGrayFrame关系、SourceRef/ROI/Viewport | Gray8/16与Stride/单位/双Pane；切图后旧结果拒绝 |
| C 算法/结果/工程 | MPM端口→MOC编译→MRK/MDS执行→RunResult→MEM显式工程提交 | 类型非法、取消/失败/空、历史Run稳定、Undo/Save/Open |
| D 批量实时/跨专业 | FrameLease/字节预算/DropNoDrop、Provider版本/权限、Inspection契约 | 帧队列压力、设备异常、SDK/GPU/内存泄漏与HIL |

**状态严格区分**：图和合同形成只意味着架构设计可审，不意味着新类型存在、编译或现场通过。本轮架构修订未执行Windows CI，也未运行新的专业算法测试。后续实际测试须逐V2D-XX.YY填SHA、样本规格、反例和运行证据。

## 10. CASE-ARCH-01 核心合同的架构级反证矩阵（新增设计；未执行）

目标：不是把260项目标一口气实现，而是先用一个Gray16增强/分割/区域/测量四节点工作链，验证三条对象契约能否支撑Studio/WPF/API/无头Runtime。以下测试编号为设计标识，不代表代码或测试用例已创建。

| 测试ID | 对象关系 | 输入/操作 | 期待结果和拒绝条件 | 后续验证方式 |
|---|---|---|---|---|
| ARCH-D01 | FIM像素格式 | Gray16有Stride Padding | 原像素不被LUT或ROI更改；Padding不计入统计 | FIM数值单测 |
| ARCH-D02 | FIM/PVC互通 | 同一Gray8、Gray16图经两个像素表达 | 每像素/阈值/区域结果等价或给明确映射说明 | 专业数值测试 |
| ARCH-D03 | Frame身份 | SourceGeneration变更后旧帧晚到 | 旧结果Stale，不覆盖新图 | 异步竞争测试 |
| ARCH-D04 | 非等距标定 | sx≠sy做距离/角度/面积 | 真实物理数值；无标定禁填mm | FGE/FIM计量测试 |
| ARCH-A01 | MOC能力可用性 | Catalog有能力描述但Provider缺失 | 保持可发现、明确Unavailable/Rejected | Catalog/Resolver测试 |
| ARCH-A02 | MPM/MRK端口映射 | 工程TypeId合法、执行CLR类型不匹配 | Compile/Run按错误阶段明确拒绝，不混用object | MOC/MRK契约测试 |
| ARCH-A03 | 参数范围 | Gray16阈值超限/NaN/非法ROI | 前置验证失败，不产生部分结果 | 参数Schema反例 |
| ARCH-A04 | 四节点执行 | 增强→Mask→Region→Measure | 具名输出、中间数据、SourceMap和Trace正确 | 真实MOC/MRK集成 |
| ARCH-A05 | 取消 | 计算中修改原图或参数、取消Token | PendingAbort，旧Run不污染新展示 | 并发/取消测试 |
| ARCH-R01 | 多类型结果 | 单Run有Image、Mask、Region、Measure、Trace | RunId/ResultItemId唯一且可复核 | 专业结果单测 |
| ARCH-R02 | Empty与失败 | 无区域与算子执行错误 | Empty成功与Failed/Cancelled分离 | 结果状态测试 |
| ARCH-R03 | 显示投影 | 结果Mask属于图A但当前选图B | 不叠错图；提示Stale/NeedsRegistration | WPF集成/UIA |
| ARCH-R04 | Run版本 | 参数重跑、旧结果再查看 | 原Run只读，新Run独立；参数与Provider版本可追踪 | 结果版本测试 |
| ARCH-R05 | 正式工程 | Overlay→Promote→Commit→Undo→Save/Open | 工程稳定ID、Revision/SourceRef和样式保持 | MEM/MRC/WPF端到端 |
| ARCH-R06 | 人工复核 | 更改缺陷复核意见 | ReviewRecord派生，原算法Result不可变 | 权限审计与结果测试 |
| ARCH-Q01 | 大图预算 | 4300² Gray16及多个中间Mask | 计入副本、CPU/GPU/SDK/队列开销；超限受控拒绝 | Windows性能/资源 |
| ARCH-Q02 | 真正连续流 | 满队列/设备掉线/重新连接 | 明确背压/Drop/NoDrop、Frame代次和Lease归还 | 回放+真实HIL |
| ARCH-Q03 | 无头与UI | 同样本独立API及MPW工作台 | 算法数值和结果身份语义一致；无头不依赖Dispatcher | SDK+WPF真实控件 |
| ARCH-Q04 | 发布和回滚 | 新旧已发布Plan/能力版本不同 | Run固定Revision、发布失败不破坏现役Plan | MOC/MRK发布测试 |
| ARCH-Q05 | 错误诊断 | 端口错误/资源过载/SDK异常 | ErrorCode、Node/Port、RunId、Trace可复查 | FLD/运行单测 |

### 10.1 评审签收格式

每个测试在实施时写：实际输入文件及Hash、运行环境、Owner/类路径、实际输出/容差、异常或性能截图/日志、Git SHA、Windows工作流链接、测试日期、是否专业设备HIL。架构P设计只能列为G0/G1，不能列G2源码或G4/G6通过。MDS预算只证明当前已提交托管载荷，不证明所有GPU/SDK工作缓存；完整相机流不能通过MDSBoundedByteStream的基本字节块测试替代。

## 11. 固定SHA实证：FIM↔Vision数据合同第一条闭环（2026-10-28）

**代码提交**：[`c424abcf004d670790a68d6c6c6976f175cf7b5b`](https://github.com/oudream/cx-visual-x3/commit/c424abcf004d670790a68d6c6c6976f175cf7b5b)。**真实CI**：[CxVisual FIM MVI Imaging Windows Acceptance #40657978967](https://github.com/oudream/cx-visual-x3/actions/runs/40657978967)，Windows 2022 / VS MSBuild / .NET Framework 4.8 x64，全job **success**。

| 关联能力/反证 | 对应实际测试行为 | 固定SHA证据级别 |
|---|---|---|
| V2D-02 / ARCH-D01：Gray8 Stride | 3×2 Gray8/Stride=5；去Padding后往返逐像素一致 | G4 自动测试通过 |
| V2D-02 / ARCH-D02：16位小端 | 2×2 Gray16/Stride=6，500/500/0/0，像素值和紧排字节正确 | G4 自动测试通过 |
| V2D-02 / 所有权 | 输入byte[]变更、CopyPixels和FDTReadOnlyBuffer外部副本变更均不污染原图 | G4 自动测试通过 |
| V2D-18/20：现有MRK集成 | ToCpuInspectionInputs→CxVisionDataFlow.CreateCpuInspectionPlan→MRK，ConfigRevision=57、GraySum=1000、两个阈值像素、一个面积2连通域 | G4 自动测试通过（仅此CPU算法） |
| V2D-02/18：错误格式 | null、GrayFloat32、Bgr24、与已编译Plan不匹配的位深均明确拒绝 | G4 自动测试通过 |
| 结构/回归 | Windows x64完整构建、Owner/DAG、二维WPF Viewer、`--imaging`、默认ArchitectureSmoke、三维WPF Viewer | 对应CI所有步骤success；并非新桥的WPF交互闭环 |

**未验证/不可提升等级**：大图/32M性能与所有工作副本峰值、完整Frame/Mask/Artifact身份模型、SourceGeneration失效/设备Lease、通用VisionRunResult、正式工程Pipeline、专业SDK/AI推理、实时相机流、独立UI算法工作流及现场HIL。现有WPF回归只证明旧图窗未退化，**不是新专业输入桥接功能已经通过WPF人工操作**。不得按整个一级类V2D-02或V2D-20统一标为G4；仅上述受覆盖的细能力/案例有效。

## 12. P1真实结果投影测试登记（2026-10-28）

> 代码实施提交[0a5e658](https://github.com/oudream/cx-visual-x3/commit/0a5e658ccf96015e6c7eea77380e873e43d59058)，[Imaging Windows #40660774027](https://github.com/oudream/cx-visual-x3/actions/runs/40660774027)已触发，**本节首次登记时CI仍排队，未记通过**。明确按固定SHA复核并更新，不将测试文件存在冒充运行成功。

| 关联V2D/反证 | 新增真实用例 `RuntimeVisionResultTests` | 预期 |
|---|---|---|
| V2D-21 / RES-01 | `RealMrkResultProjection` | 同一真实MRK FlowId生成检测摘要、阈值统计、Region、GraySum和FlowDecision五项；ItemId唯一 |
| V2D-21 / RES-02 | `IndependentRunsAndStudioProjection` | 同源重跑产生不同Run/ItemId，旧数据不受新Run影响 |
| V2D-21 / RES-03 | 所有结果项带SourceId与RunId；区域像素边界/面积 | 不继承可变`CxVisionDefectRegion`引用 |
| V2D-21 / RES-04 | `EmptyAndFailedFlow` | 无缺陷返回合法空区域摘要，损坏RAW的MRK失败不允许产生正式结果 |
| V2D-21 / RES-05 | 本次模型从MRK`GetCommittedOutput<T>`读取 | 不绕过已存在MDS/FDT提交屏障，集合只读 |
| V2D-24 / API兼容 | `FromInspection`服务MOC编译后的Studio真实StepResult | 无GraySum/Passed来源时不伪造；真实MOC指纹与Provider版本从Studio传入 |
| V2D-26 / 结构回归 | `--vision-results`、`--imaging`及默认ArchitectureSmoke、完整Windows构建及WPF | 需对应Actions结果；现场UI/性能待额外验证 |

**尚未覆盖**：Image/Mask/Contour/Measurement/Report/AI等完整多类型Run，持久ArtifactRef，自动复核审计，配准坐标转换、异步旧结果过期策略，工业设备HIL及GPU/长时稳定性。已有四类Kind不能被宣传为完整26类能力已经交付。
