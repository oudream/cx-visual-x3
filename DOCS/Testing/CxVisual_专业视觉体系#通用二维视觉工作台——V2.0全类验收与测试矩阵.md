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

## 13. P2 Image/Mask真实执行/安全合同的专项登记（2026-10-28）

**代码**：[c98d933](https://github.com/oudream/cx-visual-x3/commit/c98d933bd31a688e083c88a815dae59751d8c48c)、[8d5c027](https://github.com/oudream/cx-visual-x3/commit/8d5c027e87b8360246ca5e71a91db157d2512aaf)、[9316a139](https://github.com/oudream/cx-visual-x3/commit/9316a13903a7bd69d9c1f71c9d8fe2354d408b46)。最新固定[Windows Imaging #40662918228](https://github.com/oudream/cx-visual-x3/actions/runs/40662918228) 初次登记为**queued（未执行完成）**。本章测试仅是已提交可执行回归源码，CI未有结果时不可称G4通过。

| ID/能力 | 已编码正反验收 | 当前证据限制 |
|---|---|---|
| ART-01 / V2D-02,10 | Gray8真实Preprocess回传FIM Image，数值与源图一致；输入不变 | 需Windows运行结果 |
| ART-02 / V2D-12,21 | Gray8 0/255 ThresholdMask四像素命中，Region只接受面积3的候选连通区域，阈值与面积过滤严格分义 | 需Windows运行结果 |
| ART-03 / V2D-02 | FIM Gray16带Stride输入、紧排MRK、处理图保留16位500/500，Mask为Gray8 255/255 | 需Windows运行结果 |
| ART-04 / V2D-20,21 | 原MRK API二参签名及3输出不变；Opt-in仅增加2个具名数据出口 | 需Windows编译及回归 |
| ART-05 / V2D-21 | RunId/SourceId/ItemId与类型端口一致，两个FIM Image产物可以从ResultItem访问且像素不可修改 | 需Windows结果 |
| ART-06 / V2D-26 | Invalid RAW失败无任何正式CommittedOutputs，不能冒充成功Run | 需Windows运行结果 |
| ART-07 / V2D-20 | 两个Flow共享Plan并行执行但Source/Mask/ItemId各自独立 | 需Windows运行结果 |
| ART-08 / V2D-04,26 | `MRKFlowResult.TryEstimateCommittedBytes`对FIM Image/Mask经FDT专业估算且包含有效像素载荷 | 需Windows运行结果 |
| ART-09 / V2D-24 | `--vision-results`、`--imaging`、默认ArchitectureSmoke均挂载本次测试 | WPF二维/三维CI仅证明共享回归，不代表新图像产物已在Studio或TestCenter中可视操作 |

**工业验收仍未覆盖**：单独的FrameId/FrameLease、磁盘Artifact持久化、Mask与正式ROI坐标配准、WPF可视化用户交互、AI/GPU/厂商设备HIL和长期运行内存。已有的MDS容量统计不等于这些全部满足。

**ART-10补充（源码已提交，待Windows验证）**：对应提交 [`97630512`](https://github.com/oudream/cx-visual-x3/commit/97630512a2c3d7d958d6fad325a61ab494ae6810)，新增`RuntimeVisionArtifactsTests.MviImageMaskProjection`：同一真实Gray16 MRK Run产生Gray8 Mask→经`CxVisionRunSummary`按RunId/SourceId/尺寸验证→提交现有`MVIImageSession.SetResult`→Original/Result双槽切换，原16位像素不变；载入新原图后自动清空旧结果。此测试是**MVI非WPF状态集成**，不能冒充TestCenter/Studio鼠标可视操作或Viewer级自动防错源校验；执行前上层仍须显式检查SourceId与坐标。对应[Imaging Windows #40663246695](https://github.com/oudream/cx-visual-x3/actions/runs/40663246695)，首次登记时排队，尚无通过结论。

## 14. P3 二维专业CPU图像/Mask实际WPF交互与来源安全反证（2026-10-28）

**代码已提交，当前仍待Windows执行证明**：[最新Imaging #40666425061](https://github.com/oudream/cx-visual-x3/actions/runs/40666425061)（`4f91211`），初次登记为queued。不要套用P1成功Run #40660774027的证据到P3新增WPF交互。

| ID | 实际测试/真实操作 | 通过条件 | 现阶段 |
|---|---|---|---|
| UI-VIS-01 | `TCTImageViewerSmoke.Run`真正点击`CpuVisionRun` | 使用4×4 Gray16真实MRK，处理图保留原像素1..16；Mask中10..16七个命中且候选区域面积7 | 源码测试待Windows |
| UI-VIS-02 | 输出页`CpuResultItems`实际点击Image/Mask/Region | Image恢复Gray16；Mask为Gray8/0/255；选择Region不会改写图像 | 源码测试待Windows |
| UI-VIS-03 | Mask→`ExtractMaskContours`按钮 | 使用既有FIM追踪真实边界、MVI写入PolygonAnnotation、SignedArea=7，原16位图不变 | 源码测试待Windows |
| UI-VIS-04 | 同一FIM图像对象重新载入 | 原结果槽、结果列表与本次`CpuEvidence`清空；旧Run不写回 | 源码测试待Windows |
| UI-VIS-05 | 用户换图/连续执行 | CTS取消、原图引用+SourceVersion+SourceId+Request身份多条件阻止过期提交 | 源码及测试部分覆盖，长时竞态压测待补 |
| UI-VIS-06 | 缺失标定与孔洞面积 | 无有效Spacing不得出现mm²；有Spacing时使用sx×sy乘真实有符号环面积 | 源码已实现，另需专项mm²/孔洞UI反证 |
| UI-VIS-07 | CPU原图非法类型及参数 | 原图限Gray8/16；阈值范围按位深，MinArea[1..10000000]；非法调用不发布新Run | 源码已实现，另需WPF反例确认 |
| UI-VIS-08 | 旧本轮CI failures | #40663246695有Vision CS0136；#40665129173有FTR CS0103。不能写作P3测试通过或同一个Owner失败。 | 失败根因日志已查，待对应Owners及新CI确认 |

**边界**：`TestCenter`仍是人工/自动化专业测试工作区；正式Studio可视面板、本次SourceId的硬件稳定身份、持久ArtifactRef、可恢复历史Run、几何配准、工业SDK/HIL/AI均不在上述测试通过范围内。


## 15. P3参数编辑、MVI槽生命周期与孔洞测量专项反证（2026-10-28）

**对应源码固定SHA**：[becc050](https://github.com/oudream/cx-visual-x3/commit/becc0505850493a27adf302bc37f3d64fe4ffeaf)、[a287f45](https://github.com/oudream/cx-visual-x3/commit/a287f45d2cda4a6004d3e9c9ff8250361c4333f1)、[3b15550](https://github.com/oudream/cx-visual-x3/commit/3b15550baacc72db8b06cd8678982eae9d23c497)。本节更新UI-VIS-05/06/07的**可执行源码覆盖**，不把此前“待补测试”按文字改成已执行通过。

| 用例ID / 关联能力 | 真实行为与要求 | 反向判据 | 状态 |
|---|---|---|---|
| UI-VIS-09 / V2D-16,21 | 已有7px CPU Run后把Gray16阈值改为70000，MVI应回原图、Result置空、专业Run/结果列表失效，Original引用与像素保持 | 无效输入不得启动或重用原Run；源图不变 | 自动WPF用例已编码；待CI |
| UI-VIS-10 / V2D-12,16,21 | 同原图阈值10→11，真实再次点击CPU执行，RunId必须变化、Region由7px变6px、原图Gray16的像素10不变 | 不得复用旧Image/Mask或旧RunId | 自动WPF用例已编码；待CI |
| UI-VIS-11 / V2D-20,21,26 | 新Run在途时修改阈值至12，CTS取消并清Result；Dispatcher允许晚到回调后仍无旧Evidence/结果项 | 旧异步结果不得覆盖本次当前图 | 自动WPF用例已编码；待CI及更强压力测试 |
| UI-VIS-12 / V2D-08,12,14 | 3×3真实MRK CPU环形Mask有8px命中和一个孔洞；FIM轮廓外环9px²、孔洞−1px²；FIM非等距0.2×0.4测量合计0.64mm² | 不得把带孔洞区域按外框9px²报告或修改原图/结果Mask | 自动WPF用例已编码；待CI |
| MVI-CLR-01 / V2D-05,21 | MVIImageSession.ClearResult首次清理返回true、代次+1、Observer通知一次、激活原图且保留Original | 再次调用必须返回false且不再改变代次/通知；在Original激活时同样不得清源图 | ArchitectureSmoke已编码；待CI |
| CI-VIS-09 / V2D-26 | [Imaging #40672752671](https://github.com/oudream/cx-visual-x3/actions/runs/40672752671)与[TestCenter #40672752691](https://github.com/oudream/cx-visual-x3/actions/runs/40672752691)匹配提交3b15550 | 无完整成功日志则不可登记G3/G4或G5 | 初次登记queued，G2源码 |

**特别说明**：现有面积测量按图像格点与各向异性像素间距计算，未引入畸变标定或几何配准，不能视为完整工业计量G6。WPF自动点击可用于G4/G5部分自动证据；G5人工实际体验、UIA可访问性、现场硬件和长时性能仍需单独记录。

## 16. 用户截图反证：显示参数和绘图类别真实WPF验收（2026-10-28）

本组UI专项对应源码提交 [9394e3b](https://github.com/oudream/cx-visual-x3/commit/9394e3b0d8c88bdaad4483460307daf3bb41eea5)，不得用前一批已通过构建替代。新WPF断言按真实Routed Button.Click执行，不虚构真人鼠标/设备HIL。

| 用例 | 正反行为 | 代码状态 / 验收等级 |
|---|---|---|
| UI-UX-01 / V2D-08,09 | 显示页四个分类`SelectMode.ROI/测量/标注/缺陷`点击后必须对应RectangleRoi/Distance/Point/DefectBox，边框高亮与Viewer.ToolMode一致；仍有原`OverlayColor.ROI`选中图元改色回归 | 已编码G2；WPF Windows待结果 |
| UI-UX-02 / V2D-05 | 实际点击Window按钮以1..16窗位映射Gray16样本，测量中间像素产生有限Gray8显示值且原位深像素不变；将下限改成“b”后操作必须拒绝并通过AutomationProperties报告具体输入错误，不破坏旧映射 | 已编码G2；WPF Windows待结果 |
| UI-UX-03 / V2D-05 | 点击AutoWindow进入自动灰度范围+自动对比，避免16位图实际窄灰度区间在0..65535默认窗口下视觉发黑 | 已编码G2；WPF Windows待结果 |
| UI-UX-04 / V2D-05 | 输入Gamma=2、亮度=0.1、对比度=1.5并点击ApplyDisplay，值与真实输出PNG像素必须改变；勾选反色应立即提交；非法Gamma保留旧状态并有可查询WPF错误；Reset恢复IsIdentity且滑块1/0/1 | 已编码G2；WPF Windows待结果 |
| UI-UX-05 / V2D-05,26 | MVI.SetTone一次设置所有显示参数只发一个Changed；幂等无通知；非法NaN完整拒绝且无部分状态发布，随后Reset可以恢复 | 已编码G2；ArchitectureSmoke Windows待结果 |

**匹配工作流**：[Imaging #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082)、[TestCenter #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116)；首次登记为in_progress，尚不可写G3/G4/G5通过。后续需要核实WPF像素PNG与Auto Contrast失败日志、最终用户手工完整操作、工业大图性能，不得将单张截图当作验收完成。

### 16.1 固定SHA执行结果与新Gray8反证

- `9394e3b0d8c88bdaad4483460307daf3bb41eea5`：[Imaging Windows #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082) **success**：VS/.NET48 x64全项目构建、结构与依赖验证、真实WPF ImageViewer/PNG roundtrip、FIM/MVI专项/默认回归通过。此为UI-UX-01～05在该固定SHA的实际自动运行证据；不等于人工视觉体验或工业G6。
- 同SHA [TestCenter Windows #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116) **failure**：步骤`Verify real Windows UI Automation user workflow`报Channel Connectivity `viewerPresent=False`、TX/RX/error无证据；未见本轮二维图窗编译/PNG测试阻断。须由Channel/TestCenter Owner核查，不修改或静默跳过。
- **UI-UX-06**：Gray8原图重新载入后`WindowHigh=255`而非65535，真实点按Window并导出PNG黑0白255，原图255保持；新图同步Tone/CheckBox/Slider避免已输入非法旧值继续残留。代码 [884b75c](https://github.com/oudream/cx-visual-x3/commit/884b75cc1a9e1002f7a59fecc884f1635fe0863e)、[3661cca](https://github.com/oudream/cx-visual-x3/commit/3661ccad0bcbd882499ac261bdecf97ad3440079)。对应[Imaging #40683998349](https://github.com/oudream/cx-visual-x3/actions/runs/40683998349)，初次登记in_progress，尚未提升此新增项G3/G4。

## 17. WinForms交互等价专项：WW/WL滑条、自动取窗、灰阶与选色（2026-10-28）

| 用例 | 真实正反测试约束 | 当前状态 |
|---|---|---|
| UI-UX-07 | 取消显示页重复4彩色分类和4固定色块，保留可访问工具选择及各专业图元工具；图层勾选只影响可见性 | WPF反例源码G2 |
| UI-UX-08 | Gray16 4×4样本窗宽15、窗位8.5产生原值10像素的有效灰度显示；非法数字必须拒绝、旧参数不被部分修改 | WPF与原像素反例源码G2 |
| UI-UX-09 | WPF真实窗宽/窗位滑条设置12、6并用Thumb.DragCompleted提交，实际MVI WindowWidth/Center同步；原像素保持 | WPF事件反例源码G2 |
| UI-UX-10 | 框选ROI(0..1,0..1)，真实统计1..6得到窗宽5、窗位3.5；关闭ROI后全图自动拉伸；需补多边形/锁定/大图性能 | WPF ROI专项源码G2 |
| UI-UX-11 | Gamma滑条Thumb释放后变更MVI Gamma=2.5，亮度UI 26级映射26/255，非法Gamma保留旧值；反色及复位正常；Gray8换图后滑条/窗范围无旧状态 | WPF专项源码G2 |
| UI-UX-12 | 区域页选中图元有真正Windows系统颜色对话框入口，取消不改变MVI，对象颜色/线宽/标签仍由原MVI属性流程提交 | 对话框接入源码G2，人工验收待证据 |
| CI-UX-17 | [Imaging #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) / [TestCenter #40687637624](https://github.com/oudream/cx-visual-x3/actions/runs/40687637624)，固定SHA`e77cae8d91d14f2f54358c3c912dc93605029745`；先前92986dc的Windows失败是测试局部重名CS0128，已改源码，不等于已重新运行通过 | 初次登记queued/in_progress |

只改变展示/交互；不把Gamma、亮度、ROI自动取窗结果当成原始像素或算法Artifact。Windows截图、实际鼠标拖动和高位深大图性能仍需另证。

### 17.1 CI已执行结果补记

固定`e77cae8d91d14f2f54358c3c912dc93605029745`的 [Imaging Windows #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) 结论**success**，Windows构建/WPF ImageViewer/png roundtrip/FIM MVI/default/3D slice全部job步骤通过；UI-UX-07～11有此固定SHA的自动测试证据，UI-UX-12原色板对话框仍需现场人工选择确认。
最新Owner修复提交[4d6c558](https://github.com/oudream/cx-visual-x3/commit/4d6c558cddf78e8c9af367e27aca731b03bf59aa)绑定ColorDialog到真实WPF窗口，触发[Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944)和[TestCenter #40687957971](https://github.com/oudream/cx-visual-x3/actions/runs/40687957971)，待实际结束后再更新此固定SHA结论。工业长时、大图P95和人工拖拉手感仍独立验收。

### 17.2 UI-UX-07/12 对应用户截图修正：第一组分类删除，第二组色板保留

- **UI-UX-07修订**：原第1组四个彩色工具类别冗余且已删除，仍须保持WPF可访问的工具ComboBox、顶部高频工具、专业ROI/测量/标注/缺陷工具，图层可见性独立。
- **UI-UX-12修订**：原第2组四图层颜色按钮不应删除；应不依赖选中对象而打开WinForms系统`ColorDialog`，取消不变，确认后改变现有同层未锁定图元与后续默认绘制，不改变明确指定的算法产物ARGB，保留原图及Id。当前选中对象单独颜色编辑仍在区域属性页。
- 测试源码 [788d5fe](https://github.com/oudream/cx-visual-x3/commit/788d5fec78111784052ef9080c72a42bcf369e1a)：WPF四入口存在、移除第一组按钮、`MPW.SetOverlayLayerColor`改变现有和后续多边形ROI且Id不变，并维持属性编辑；系统模态色板仍需现场鼠标选择作人工确认。
- 前版 [Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944) success不等于新色板四入口已过。新固定`788d5fe`对应 [Imaging #40688664400](https://github.com/oudream/cx-visual-x3/actions/runs/40688664400) / [TestCenter #40688664402](https://github.com/oudream/cx-visual-x3/actions/runs/40688664402)，初次登记queued；未经实跑不得标记G4完成。

### 17.3 图层颜色视觉一致性补充约束

- 新增默认ROI/测量/标注/缺陷图元（包括鼠标`CompleteDraw`、开发者`AddOverlay/AddPolygonRoi`）应统一进入MPW当前层默认色，不得出现按钮与新对象颜色不同；未显式选择色也需与预设色相符。
- 显式指定非默认ARGB的外来算法产物不得被UI预设改写；修改同层颜色更新现有未锁定对象且保留Id/几何，锁定对象不因UI设置而被改色；普通`MVIImageOverlayItem`本身的属性身份与基础默认值不变。
- 源码：[0eeedc0](https://github.com/oudream/cx-visual-x3/commit/0eeedc0aa0843c4f886156bab0068c38600b91a0)、[ec1e044](https://github.com/oudream/cx-visual-x3/commit/ec1e0445251bbb1c6097fbf4c257039e08019551)、[9d9a3a1](https://github.com/oudream/cx-visual-x3/commit/9d9a3a14458a89efd2bbd454f428187f3ed2dc8d)；Windows最新固定SHA待实际CI执行结论，原色板模态操作仍待人工截图。

## 18. CASE-ARCH-01首包：已筛选面积与共享专业预览的正反验收

- **ARCH-A04 / RES-01首个小闭环**：采用真实MRK `Decode→Preprocess→Inspect→MeasureAcceptedRegions`及已有Statistics/Decision，带图像/Mask和独立的具名`AcceptedRegionPixelArea`输出；Gray8样本5×3共4个阈值命中，其中孤立点被MinArea=2拒绝，Measurement=3 px²。该阶段不是完整四种可编排MPM/MOL算法。
- **ARCH-R02**：无区域仍合法提交0 px²，保留独立RunId；坏RAW长度、失败Run禁止Image/Mask/Measurement部分发布，原旧Plan输出数量不变。重复Run ItemId唯一，同Run来源与输出端口不混。
- **ARCH-D02 / ARCH-Q03首包**：FIM 5×3 Gray8带7字节Stride在`RunCpuInspectionPreviewAsync`无UI运行，Mask孤立命中、已接受区域测量、原像素保持；非法位深、空SourceId和预取消拒绝。Windows命令由ArchitectureSmoke `--imaging` 或 `--vision-results`载入上述测试。
- **UI-UX-13**：TestCenter真实WPF按钮运行专业无头API，输出列表包含Measurement 7 px²（原实验Gray16样本），选中测量不更改正在查看的Mask，原图保留，参数代次变化使旧Run失效。用`TCTImageViewerSmoke`做实际控件反证。

源码提交：[404d9d3](https://github.com/oudream/cx-visual-x3/commit/404d9d39c9b653081292f331d0b5fc70ff9bd70c)、[dd5506f](https://github.com/oudream/cx-visual-x3/commit/dd5506fffa303804358ede9c6106f162c2bd5d3d)、[047a855](https://github.com/oudream/cx-visual-x3/commit/047a85587fac8f2830d1d9080047221a2f4c3791)、[f2d55bb](https://github.com/oudream/cx-visual-x3/commit/f2d55bbd612280e0fbabb1c64db0ca27d4127ea5)、[c25e50d](https://github.com/oudream/cx-visual-x3/commit/c25e50d96f91f20458a1dfb5db02835f0a89ecd0)、[a34e034](https://github.com/oudream/cx-visual-x3/commit/a34e03411fa715ff04a3c64e783ba7d30d97e0dd)、[5b34232](https://github.com/oudream/cx-visual-x3/commit/5b3423258a96db58f808e755c4338b45464414b0)。

**新提交时尚未宣称Windows通过**；需检查固定SHA [Imaging #40693675846](https://github.com/oudream/cx-visual-x3/actions/runs/40693675846)、后续触发的TestCenter Windows任务和运行日志，不能继承旧版本success，未知则保留待验状态。

### 18.1 已完成的固定SHA Windows验证

`a34e03411fa715ff04a3c64e783ba7d30d97e0dd`：
- [Imaging Windows #40693675846](https://github.com/oudream/cx-visual-x3/actions/runs/40693675846) **success**：Windows x64全Solution构建、Owner DAG、真实WPF Viewer/PNG、ArchitectureSmoke `--imaging`和默认回归、3D切片/截图上传均成功。新MRK测量步骤和无界面API正反数值用例实际随`--imaging`执行；本项可标G3/G4（该固定SHA及具体用例）。
- [Unified Runtime Windows #40693675869](https://github.com/oudream/cx-visual-x3/actions/runs/40693675869) **success**：相同SHA，Windows全构建与Runtime专项。
- TestCenter完整工作流是独立G4/G5验证门禁；[TestCenter #40693629251](https://github.com/oudream/cx-visual-x3/actions/runs/40693629251)属稍早`c25e50d`固定提交，首次登记in_progress；完成后需核查实际WPF UIA结果。人工颜色对话框、工业Frame/Artifact/四节点工程发布与HIL不得借此视为已过。

## 19. CASE-ARCH-01 第二阶段MEM/MOC测量节点正式工程验收（2026-10-29）

| ID | 可执行正反场景 | 验收状态 |
|---|---|---|
| ARCH-O01 | 真实MEM工程按RAW→Inspect→Measure产生三个MOL块、两条Data边及MPC专业类型端口；测量Capability可被MOC描述与MRK解析；不创建新的工程树/执行器 | 源码与回归已提交，最新CI待验证 |
| ARCH-O02 | `MOCProjectRelease.Prepare/Stage`核对RAW SHA/阈值Provider配置版本/测量算法版本，受限PlanSlot激活；MRK真实完成两个专业步骤，SourceMap包含Measure BlockId | 已编码，待固定SHA Windows |
| ARCH-O03 | 5×3 Gray8 RAW中4个阈值命中、3个被面积规则接收；工程正式`AcceptedRegionPixelArea`的MDS CommittedOutput必须为3px²，与Measure StepResult/Region合计和同Run证据一致；无毫米标定不发布mm² | 已编码，待固定SHA Windows |
| ARCH-O04 | 工程`SaveAs`/重新`Open`后MOL3节点、Data边和公开Area输出保持；**仅验证定义保存**，不冒充RAW文件/Provider绑定自动持久化或生产恢复 | 已编码，待固定SHA Windows |
| ARCH-O05 | 原`ExecuteAsync`仍为2节点且无`Measurement`；编辑脏工程显式`Close(true)`不触发不必要的存储阻断 | CI首版暴露清理缺陷，已修正，待复跑 |
| UI-COMP-01 | Studio共享CPU执行入口、TestCenter Compute真实按钮使用同一Professional`ExecuteMeasuredAsync`；TestCenter UIA必须看到MeasureBlockId、3px²与对应完成Trace | 已编码，待新版UIA |

**首批真实CI失败必须保留**：[Imaging #40809026028](https://github.com/oudream/cx-visual-x3/actions/runs/40809026028)、[Engineering #40809025895](https://github.com/oudream/cx-visual-x3/actions/runs/40809025895)、[Runtime #40809025929](https://github.com/oudream/cx-visual-x3/actions/runs/40809025929) 失败堆栈`Unsaved changes; save or explicitly discard`（新增旧2节点工程用例的清理问题）；[Studio S1 #40808893404](https://github.com/oudream/cx-visual-x3/actions/runs/40808893404)旧能力目录断言0个输出已过时。修复已提交，但不能以修复提交替代最新固定SHA的成功日志。

本阶段没有完成泛化MPM算法Schema、ImageSet/FrameLease、ROI工程资源提交、ArtifactRef/Review、完整四算子与设备/GPU/AI，也没有实物现场证据。

### 19.1 固定SHA首批结果与后续补丁范围

`13f5b42c2949dd24e518e2f3fc4e52ada13dac60`： [Engineering Organization #40809379292](https://github.com/oudream/cx-visual-x3/actions/runs/40809379292) success，[Module Center CPU RAW #40809379183](https://github.com/oudream/cx-visual-x3/actions/runs/40809379183) success；本次工程定义、编译、保存重开、Area计算与旧API兼容正反用例完成实跑。后续具名MDS `Area` 的`CommittedOutput`一致性是再增加的代码，不以此固定SHA宣称成功。
同固定SHA [Studio S1 #40809379277](https://github.com/oudream/cx-visual-x3/actions/runs/40809379277) failure，日志为配置布局列容器要求，非CPU专业合同。其Owner需独立跟进；保持失败记录，不关闭总体G5门禁。

### 19.2 ARCH-O03 强化为FDT冻结反证

真实Windows [Module Center #40809580828](https://github.com/oudream/cx-visual-x3/actions/runs/40809580828) 在新增Area公开输出后触发`CxVisionInspectionResult`无安全复制/冻结适配器并按平台策略失败；这是数据合同门禁有效的证据，不可绕过。已在Professional CPU Provider使用已有深拷贝+FDTDataKind.Object/真实容量估算注册，以真实MRK跨步骤复制后再发布Area；测试失败场景在`564b5d7`增加`Close(true)`兜底，以免`Unsaved changes`掩盖真正MRK异常。最新[Imaging #40810246586](https://github.com/oudream/cx-visual-x3/actions/runs/40810246586)/[Engineering #40810246877](https://github.com/oudream/cx-visual-x3/actions/runs/40810246877)初次登记仍待运行，不提升G3/G4。

### 19.3 ARCH-O06 保存重开、输入重绑定和用户界面重试状态反证

| ID | 正反验收语义 | 固定证据状态 |
|---|---|---|
| ARCH-O06 | 同一已保存MEM工程`SaveAs/Close/Open`后，显式重新绑定RAW + Provider，仍用**同一3块MOL**触发MOC版本检查、MRK计算与SourceMap；得到新RunId、3 px² Measurement及完整Trace；工程Revision/Dirty不改变，且不生成第二页 | [dceed1d](https://github.com/oudream/cx-visual-x3/commit/dceed1d7a8ceb0b6685615ebf8b83245947553ef)已补自动反证，Windows最新结果待查 |
| ARCH-O07 | 已保存5×3工程重绑真实6×3合法RAW（18字节），明确拒绝工程宽高不匹配；与**非法RAW长度**两种错误分开，拒绝后不修改工程Revision | [Engineering #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) / [Imaging #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554)待最终执行结论 |
| UI-COMP-02 | 故意以6×3解释15字节RAW触发负例，必须等待异步处理真正结束；改回5×3再点Run，不能把旧错误状态/取消状态当作新Run完成，必须等到新LastReport | [2b110f5](https://github.com/oudream/cx-visual-x3/commit/2b110f5429bba324a57c5e024d49dd615dfcf97e)、[Compute UIA #40811544416](https://github.com/oudream/cx-visual-x3/actions/runs/40811544416)待最终结果 |

**边界**：工程重开不是无需资源重绑定的生产重放，也不保存RAW像素于MEM；持久ArtifactRef/FrameRef/ReviewRecord、跨设备调度与长时HIL仍未完成。

### 19.4 G3/G4实际回归结果（2026-10-29）

`ARCH-O06/O07`针对固定`dceed1d7a8ceb0b6685615ebf8b83245947553ef`：[Imaging #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554) **success**（实际Windows构建、`--imaging`、保存重开显式重绑成功、合法18字节6×3输入与保存5×3工程不匹配被拒绝，工程Revision不变），[Unified Runtime #40811670497](https://github.com/oudream/cx-visual-x3/actions/runs/40811670497) **success**。同SHA工程专项[Engineering #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) 首次登记in_progress，不据此称其success。

`UI-COMP-02`固定`2b110f5429bba324a57c5e024d49dd615dfcf97e`：[TestCenter Compute UIA #40811544416](https://github.com/oudream/cx-visual-x3/actions/runs/40811544416) **success**， [Four G1 #40811544542](https://github.com/oudream/cx-visual-x3/actions/runs/40811544542) **success**。明确以等待新`LastReport`终结负例之后的真实重跑，不能以旧错误文本为条件伪造完成。

### 19.5 全Engineering门禁未通过的真实阻断

固定`dceed1d7a8ceb0b6685615ebf8b83245947553ef`：[Engineering #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) 为**failure**（`FTRNamedPipeBulkTests.MultiplePeersAndIndependentSends`→`FTRNamedPipeServerChannel.OnCloseAsync`→`ObjectDisposedException: Safe handle has been closed`），不得声称全Engineering验收已通过，归属独立Foundation.Transport Owner；此次未修改传输源码。相同SHA [Imaging #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554) 和 [Runtime #40811670497](https://github.com/oudream/cx-visual-x3/actions/runs/40811670497) 为success；其证明范围为对应实际执行的视觉/RUNTIME测试，不替代全局门禁、现场硬件或工业稳定性。

## 20. ALG-G03四节点FIM中值增强工程与实际WPF结果验收（2026-10-29）

| ID | 真实断言 | 结果 |
|---|---|---|
| ALG-M01 | FIM Median半径1处理7×7 Gray8原始样本：孤立250亮点消失、3×3亮块经中值成为5像素连通区域；四节点MOC处理图逐像素等于同参数FIM直接调用，输入RAW不变 | `a55d094`固定SHA [Imaging #40820286578](https://github.com/oudream/cx-visual-x3/actions/runs/40820286578) success |
| ALG-M02 | MOC增强Provider需公开RawPixels、Width、Height、Radius输入及EnhancedPixels输出；参数0/4拒绝，半径1/2 ReleaseVersion不同；MEM/MOL四块三条Data边、两公开输出 | `a55d094` 同SHA Imaging与Engineering success |
| ALG-M03 | 四节点源图/增强图/Inspection/Measure共用真正RunId、SourceId、MOC SourceMap；Measurement=5px²而非滤波前10个亮像素，公开正式MDS增强图和面积输出 | 增强图RunSummary投影已提交，更新后的固定SHA CI待独立确认 |
| ALG-M04 | MEM SaveAs/Close/Open后，显式新Provider和RAW绑定重复运行不改Revision；错误半径/大小必须明确拒绝且不污染工程 | `a55d094` Imaging / Engineering success |
| UI-ALG-M01 | TestCenter Compute可手动选择中值0或1—3；真实WPF用户Run产生新四节点FlowId、Trace、增强图与5px²，0保持旧三节点兼容 | [Compute UIA #40820622091](https://github.com/oudream/cx-visual-x3/actions/runs/40820622091) 首轮登记待验，不能冒称已通过 |

仍未通过独立四节点的“增强→阈值Mask→区域组件→测量”所有细分专业算子目标：目前是RAW入口 + 中值增强 + 一体化阈值与连通域 + 面积测量四个真实工程节点。无需将设计候选的四个CapabilityId冒充现有实现。

### 20.1 Gray16与MPW图像预览新增反证

- **ALG-M05**：Gray16小端7×7 RAW，中心4000与孤立60000构造，通过实际FIM Median1和三节点下游产生同Run Gray16图/5px²面积，源RAW原始字节不变。固定`3ed5a1526d21d3a1862d432384f47854943c47d1`，[Imaging Windows #40820986353](https://github.com/oudream/cx-visual-x3/actions/runs/40820986353) success，[Engineering #40820986454](https://github.com/oudream/cx-visual-x3/actions/runs/40820986454) success。
- **UI-ALG-M02**：TestCenter Compute选择中值增强后，原`MPWImageViewerControl`创建并显示已提交的同Run FIM图；Run切换/模块切换清除旧预览，半径0仍走三节点。Smoke断言实际显示图像在(0,0)=0、(3,3)=210。固定提交`382a8f7cab5d7f466ac267ad0f73447be0ba91eb`，后续合并新HEAD的[Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787)首轮待验，绝不将既有纯逻辑UIA通过等价为新图窗验收。

### 20.2 四节点操作页固定SHA实测

- 固定`49b40da48099219a8114b2ddd3b671981aa9e57a`：[Compute UIA #40820622091](https://github.com/oudream/cx-visual-x3/actions/runs/40820622091) success / [Four G1 #40820622056](https://github.com/oudream/cx-visual-x3/actions/runs/40820622056) success，真实WPF切换半径、RAW→FIM增强→区域/面积数值验证成功。
- 固定`2847c2c7af10e02c8fab197cc9a712f36fc509fc`：[Compute UIA #40821068464](https://github.com/oudream/cx-visual-x3/actions/runs/40821068464) success，MPW Viewer加入后真实Compute UIA未回退。
- 增量图窗`PreviewImage`像素校验后的[Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787)首次登记in_progress；[Module Center RAW #40821146764](https://github.com/oudream/cx-visual-x3/actions/runs/40821146764) success。不同固定SHA不能相互冒用完成结论。

### 20.3 UI-ALG-M02最终固定SHA核实

`a6f43b8d0016c4ca08e6aa95e7a6c1428d74fcec`：[TestCenter Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787) **success**，真正原MPW/MVI图窗像素级验证通过；[Module Center CPU RAW #40821146764](https://github.com/oudream/cx-visual-x3/actions/runs/40821146764) **success**。以上属WPF UIA + CPU软件自动测试，不等于真实相机/AI/GPU/工业设备现场HIL，也未实现独立分割与区域Provider。

## 21. UI-IMG-22～26：6144×3072大图显示、滑条和自动直方图缺陷回归（2026-10-29）

| ID | 自动化正/反场景 | 固定证据与限制 |
|---|---|---|
| UI-IMG-22 | MPW原图/双图均不允许像素`Image.Width/Height`撑大`ViewportRoot`；4096×2048实际WPF窗口测量Fit=Min(viewW/W,viewH/H)、ImageTransform矩阵及中心偏移，图像可完整进入视口 | [Imaging #40828542464](https://github.com/oudream/cx-visual-x3/actions/runs/40828542464) success，用户6144真实机器截图仍待复核 |
| UI-IMG-23 | Gray16滑条0～65535、Gray8为0～255；足够宽数字输入、可见范围文字、数值同步，窗口操作不得修改原始像素 | 已新增AutomationId和WPF Smoke；最新固定SHA CI待完成 |
| UI-IMG-24 | 打开FIM原图即自动排入异步256档直方图，不需按钮也不跳右侧页签；ROI/换图旧请求作废，同图显示窗/Gamma变化不重扫原像素 | [Imaging #40828384405](https://github.com/oudream/cx-visual-x3/actions/runs/40828384405) success（较早版本统计页自动）；完整“开图即计算”后续CI待验 |
| FIM-HIST-01 | Gray16带行Padding、ROI闭区间、65535常量直方图、Bin末端、准确均值/方差和预取消；Gray8和非整数通道旧结果兼容 | [Imaging #40828384405](https://github.com/oudream/cx-visual-x3/actions/runs/40828384405) success，首次旧SHA编译CS0128已修正 |
| UI-IMG-25 | Task.Run内显式捕获用户切图/变ROI造成的预期取消，不发布失败旧统计；公共FIM算法继续按正式合同抛OCE供上层处理 | 源码已提交，最新CI待验；若VS设置全局“在抛出时中断OperationCanceledException”，仍可能按调试器设置停在Throw |
| UI-IMG-26 | 6144×3072=18874368原始像素，Gray16统计扫描变成逐行一遍、65536整数频数再聚合256档，不用显示窗计算原像素；实际大图性能需要有机器基准值 | 算法优化代码和测试已提交，不臆造毫秒性能指标 |

**仍需**：用户实际6144×3072源图的Fit截图、完整最新SHA Windows UIA和性能实测；上一工作流success不能自动推断当前最终HEAD验收通过。

### 21.1 UI-IMG-27：自动直方图不得误取消专业Mask轮廓

固定`8ff63f336b68b70736e88ad9c59cf155d2a57035` 的[Imaging #40828887469](https://github.com/oudream/cx-visual-x3/actions/runs/40828887469) **failure**，WPF旧CPU Mask轮廓7px²回归受影响；不允许跳过轮廓验收。修复`98912b4c972c9737ceab09dc7f016da92de9b657`将自动Histogram的CancellationTokenSource/Epoch与Mask轮廓、区域操作分开，守护轮廓发布期间的内部图层选中；真实原图变更仍使旧处理请求失效。

**最新待复测**：[Imaging #40829484522](https://github.com/oudream/cx-visual-x3/actions/runs/40829484522) / [TestCenter #40829484729](https://github.com/oudream/cx-visual-x3/actions/runs/40829484729)。必须同时通过自动直方图、Gray16统计、407? 无此项、CPU Mask→轮廓7px²和原像素保持反证，才将UI-IMG-27标完成。

### 21.2 自动统计与轮廓原有G4回归重新通过

[Imaging #40829484522](https://github.com/oudream/cx-visual-x3/actions/runs/40829484522) 固定`98912b4` **success**；[Imaging #40829769427](https://github.com/oudream/cx-visual-x3/actions/runs/40829769427) 固定`73ecd00` **success**。覆盖大图Fit、原像素不变、自动直方图、ROI/Mask轮廓与7px²面积、取消域互不抢占。额外窗宽/窗位滑块端点直接标注且带自动化ID的新增代码[267db43](https://github.com/oudream/cx-visual-x3/commit/267db43581d6adfa4270ca9fda980df88464c7fe)还需以自身或后继固定SHA Windows实测验收，不能继承前述成功。

## 22. V2D-12/21按需Contour结果：真实MRK测试代码与未验门禁（2026-10-29）

**新增自动回归源码**：`Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionArtifactsTests.cs`，固定提交[d5ff3df](https://github.com/oudream/cx-visual-x3/commit/d5ff3df9342d021b2bb5d3cca6b6129f490e10fb)，相关实现[28e2f0d](https://github.com/oudream/cx-visual-x3/commit/28e2f0da4a17d04c9a45bdd111378dcb5845fae0)。这些是**可运行的测试代码，不是已取得Windows通过结果**。

| 编号 | 真实正反场景 | 当前证据级别 |
|---|---|---|
| CT-01 | 真实MRK阈值Mask（3个相连及1个孤立像素）按需生成2个闭环、净4px²；与筛选后3px² Region明确不同 | G2测试已提交，G3/G4待执行 |
| CT-02 | 所有Contour的RunId/SourceId/OutputPort继承真实MRK结果，ItemId唯一，原7项Run保持只读不被追加修改 | G2测试已提交，G4待执行 |
| CT-03 | 公开点集只读，试图Clear必须拒绝；同一已投影Run重复调用不重复加入结果项 | G2测试已提交，G4待执行 |
| CT-04 | 边界预算超限拒绝、非法上限拒绝、预取消令牌直接抛OperationCanceledException，不能产出截断假轮廓 | G2测试已提交，G4待执行 |
| CT-05 | 真实MRK 3×3环形阈值Mask产生+9外环与-1孔洞，两者净面积8px² | G2测试已提交，G4待执行 |
| CT-06 | 旧无Mask MRK Plan保持5项默认结果且拒绝生成Contour；不强制额外处理和内存成本 | G2测试已提交，G4待执行 |

**门禁**：Windows Release/x64编译，`CxVisual.Tests.ArchitectureSmoke.exe --imaging`及默认回归；必要时TestCenter WPF中验证Contour选择与显示，再登记G5。未实施设备HIL/G6；当前仍是同Run内存结果，不是持久ArtifactRef、完整FrameRef、Region过滤后轮廓或独立MOC轮廓Capability。
