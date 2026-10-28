# CxVisual 通用二维视觉工作台 V2.0——实施、源码Owner与旧成熟功能迁移台账

> **性质**：V2.0首版事实基线和后续实施记录入口。2026-10-28；审查时点master约6d0767a，提交期间有并行master，**任何“当前”必须重新读取HEAD**。本次仅新增11份设计/实施/验收文档与DOCS/00索引；没有改C#、没有运行新的Windows构建/CI、不声称实现260项。已查AGENTS、工程Skill、DOCS/00、DOCS/01、旧二维V1.0/V1.1、当前FIM/MVI/MPW/PVC/MEM/MRK/MDS核心源码；DOCS/02～04大型XLSX未逐Sheet内容核对，涉及Owner争议需要补查。ReferTo只读。

## 1. 权威对应文件和版本

- DOCS/Architecture/二维00：产品总纲、边界、八个反向Case。
- 二维01：26类×10项=260个稳定功能ID V2D-XX.YY；是目标，不是已实现声明。
- 二维02：26张关键UML关系图，E/R/N/P含义严格分开。
- 二维03—08：数据、交互、算法、结果/工程、运行、工作台详细设计及D03—D08验收。
- 本文件：按实际SHA、Owner和ReferTo记录迁移；Testing V2.0：测试与质量门禁。
- 历史真实记录仍保留：二维V1.0功能架构与验收、二维UML V1.1、ROI标注V1.1、P2功能和界面恢复及其专项Windows CI，不以V2.0覆盖历史结论。

## 2. Owner及当前源级真实状态

| 所在Owner/路径 | 已核实代码 / 可用范围 | V2.0差距与禁止重复 |
|---|---|---|
| Foundation/Imaging/Pixels | FIMImageBuffer：Gray8/16/Float32/Bgr24/Bgra32，构造复制，GetGrayValue/CopyPixels | Frame/元数据/大图租约；不在FIM依赖Vendor/WPF |
| Foundation/Imaging/Processing | FIMImageOperations、Filters、Analyzer、Measurement、MaskContours、ToneMapping、Blend等 | 完整算法目录、性能/专业同样本、Descriptor需按需挂载 |
| Foundation/Imaging/Regions | FIMPixelRegion | 像素范围与连续FGE几何需要清晰区分 |
| Mechanism/Visual/Imaging | MVIImageSession、Viewport、DisplayState/TransformState/ComparisonState、MVIImageLinePairGesture | 现有Original/Result双槽非完整产物集合；SourceVersion并非仅原图Revision |
| Mechanism/Visual/Imaging/Overlays | MVIImageOverlayItem/Store、AddRange及Changed | 临时Overlay已有；正式资源/Undo与多产物结果待集成 |
| Mechanism/Presentation.Wpf/Imaging | MPWImageViewerControl、RulerControl、PlotControl、SequenceReader | 真正工作台协同、性能与工具状态；不让大控件承担全部专业服务 |
| Professional/Vision | CxVisionInspectionRuntime、CxVisionGrayFrame、CxVisionInspectionResult、CxVisionDataFlow | 专业检测链已有CPU样例/真实执行；与FIM像素统一契约及一般Result/Pipeline未交付 |
| Mechanism/Core + Visual | MEMEngineeringSession、MVIVisualEditor、MRCVisualItem等 | 工程编辑/Undo/保存已存在，但二维ROI/算法定义正式映射待完成 |
| Mechanism/Orchestration + Runtime | MOC编译基础、MRKExecutionPlan/ExecutionStep/Ports | Vision专业Pipeline模型/编译接线待开发，不建第二执行器 |
| Mechanism/Data | MDSDataContext/Heap/Stack/Frame与FDT类型适配 | 正式Image/Mask/Result数据合同、Artifact和流资源预算须证实 |
| Foundation日志及平台公共机制 | FLDLogScope、配置/变量/消息/命令等已有 | 执行Trace、权限、操作审计应集成现有Owner |
| Tests/TestCenter/Manual/Imaging | TCTManualImageViewerFrame、Layout、TCTImageViewerSmoke | 真实WPF控件测试存在，正式Studio Vision完整装配未落地 |

**状态词**：存在源码=E；实现部分功能=部分；自动测试通过须具体SHA；专业设备实测还需HIL；N/P设计不等于代码存在。Vision完整工作台不允许只用UI按钮数量当完成率。

## 3. 26类实施缺口与首个可交付工作包

| 能力ID | 初始代码基础/缺口 | 首个必须实现与验收 |
|---|---|---|
| 01 | FIM RAW及MPW常见文件；设备/内存/网络不足 | 支持源标识、明确来源所有权和坏帧反证 |
| 02 | FIM像素格式/Stride；元数据与专业帧分裂 | FIM与CxVisionGrayFrame同样本合同审计 |
| 03 | MVI双槽、部分图像序列读取；完整集不足 | 帧Ref/顺序/时间戳与预加载预算 |
| 04 | FIM防修改复制；大图/共享句柄不足 | 峰值内存、分块与受控Lease专项 |
| 05 | MPW WPF显示/LUT/原图/结果 | Gray16与大图显示性能 |
| 06 | MVI Viewport/变换、标尺 | 多DPI、复杂变换坐标回环 |
| 07 | 并排/叠合/滑动/差异已有基础 | 注册配准及SourceRef对齐 |
| 08 | 多种ROI/图层/样式已有 | 洞/复合Mask/工程ROI映射 |
| 09 | 编辑控制柄/四阶段LinePair/交互已存在 | Esc/失捕/多工具交互规范 |
| 10 | FIMOperations基础处理存在 | 运算入出契约、参数范围及批调用 |
| 11 | FIMFilters基础增强存在 | 质量指标、复杂增强/专业比对 |
| 12 | 阈值、RegionGrow、MaskContours已有 | 掩膜与可编辑轮廓/洞一致性 |
| 13 | 灰度分析、直方图、剖面已有 | 纹理/频域及多通道分析 |
| 14 | FIM测量、两线、Spacing已有 | 实际边缘拟合、误差与不确定度 |
| 15 | 比较基础；注册配准未完成 | 一个真实图像配准+Transform质量 |
| 16 | PVC真实CPU阈值/连通域链 | 专业真实同图、缺陷证据与跨Inspection接口 |
| 17 | 仅预留AI类型化边界 | 先做模型/结果合同，不声称推理 |
| 18 | 已有MRK Ports、平台Capability | 1个可发现操作/参数Schema/类型化结果 |
| 19 | CxVisionDataFlow创建MRK Plan；无统一VisionPipeline编辑 | 2—4节点真实Pipeline及中间结果 |
| 20 | MRK/F19/MDS已有 | 真实有界FrameStream/Batch/取消/背压 |
| 21 | CxVisionInspectionResult及TCT小范围结果；无通用RunSet | 一个Run同时输出图+Mask+Geometry+Measure |
| 22 | PNG/CSV等初步接口 | 来源/版本/编码/报告一致性和回读 |
| 23 | MEM/MRC/MTC已存在，二维未完整映射 | ROI正式资源/Undo/Save/Open闭环 |
| 24 | TCT工作台+MPW、Studio现有Dock Shell | 统一正式Vision Workspace及API |
| 25 | PVC与CT3D/Automation接口基础；全场景未验收 | 稳定Invocation/ResultRef跨专业Case |
| 26 | 专项FIM/WPF/Windows CI、FLD基础 | 所有Case多级测试和真实性能阈值 |

以上为类别层“首个工作包”，不能替代01中的260项细粒度状态。每个二级能力必须单独更新。

## 4. ReferTo旧功能复用审查优先级

| 历史参考 | 新Owner复用 | 后续审查重点 |
|---|---|---|
| ReferTo/Vision/CxECTImageWinForms/Controls/ImageViewerControl.cs | MPW/MVI | 旧菜单、图元/工具/导航、图窗交互等价 |
| ReferTo/Vision/CxECTImageWinForms/Models/ImageDocument.cs、ImageViewportState.cs | MVIImageSession/Viewport | 源身份/版本、窗口/视口、图像生命周期 |
| ReferTo/Vision/CxECTImageWinForms/Services/ImageStatisticsService.cs、ImageMeasurementService.cs | FIM | 原始灰度、剖面、测量正确性 |
| ReferTo/Vision/Halcon/HWindowControlView.cs、HalconImageCanvas.cs | MVI/MPW+PVC | 复杂ROI、Ls2Ls、专业工具、HObject所有权 |
| ReferTo/Vision/Halcon/HalconHelper_Gray.cs、HalconHelper_Base.cs | FIM基础、PVC SDK专业 | 灰度统计、图像转换及专业算法的边界 |
| ReferTo/Vision/CxECT2DViewer | 正式Vision工作台与MPW | 原有成熟功能列表逐项保真和界面流程 |
| x2/其他ReferTo现役算法/图元/工程 | MEM/MRC/PVC等对应Owner | 先验证旧行为，后重构；不得直接复制全部旧框架 |

旧仓成熟代码的多年业务使用记录不等于x3集成后的自动验收；有些旧功能属专业回调，并非基础SDK方法。审查以具体功能行为、数值、界面和版本为单位。

## 5. 必须先消除的五项架构风险

1. **双图槽不等于多产物**：MVIImageSession当前Original/Result承载浏览，算法中间结果需要独立ResultSet/ArtifactRef，不允许再逐个增Result2/Result3。
2. **两种真实像素类型并行**：FIMImageBuffer与CxVisionGrayFrame需严格比较位深/Stride/所有权/拷贝/性能和数据转换；禁止无论性能还是类型语义都未审就添第三类。
3. **ImageViewer与TestCenter文件偏大**：MPWImageViewerControl 2300+行，TCT Frame超1100行，后续任务从公共Owner拆出真正业务行为并做WPF回归，不只是机械拆文件。
4. **运行时与工程混淆**：VisionPipeline若成为Resource必须从MEM/MOC编译到MRK，而不能另建专业Graph和Undo。
5. **临时ROI/结果与正式工程边界**：MVIOverlayStore.AddRange不是工程事务；要通过MEM BeginEdit/Commit和保存重开证明正式映射。

## 6. 分阶段交付和真实依赖

| 阶段 | 交付功能与范围 | 结束门槛 |
|---|---|---|
| P0 合同与现有闭环 | V2D-02/08/09/14/18/21对象一致性、源身份/结果语义、UML反证；保持原FIM/MVI功能 | 两算法同输入输出、过期失败不污染、真实WPF专项 |
| P1 统一操作与结果 | Operation+参数Schema+多类型RunResult、结果表/图窗联动、Descriptor先审既有平台 | 一个Run图+Mask+Measure及Trace，CI数值/UI双通过 |
| P2 工程和实验 | VisionPipeline MEM Resource、2—4节点MRK/MDS执行、中间产物、ROI→MEM | 参数重跑、Undo、Save/Open、发布Plan、版本拒绝 |
| P3 序列、实时和大图 | FrameRef/数据集、批量、有界流、资源预算、必要GPU/SDK线程 | 真实大图与序列压测、背压反证、资源释放 |
| P4 专业和协作 | 配准、拟合、AOI/X-Ray算法、Inspection/CT3D/HMI稳定合同 | 同样本HALCON/旧Viewer/真实机台HIL |
| P5 体验和扩展 | Studio正式Vision Workspace、SDK成熟、AI Capability预留或按授权实现 | 多宿主功能一致、客户用例、可复现报告 |

P0—P5不是六套执行机制，也不是只写计划后停止；每阶段应选取最短真实纵向闭环并在当前Owner上继续开发。实现前确认master SHA、Owner API、现有测试与ReferTo差异，再提交中文commit。

## 7. 源码与验收台账模板（每个260二级ID一行）

| 字段 | 填写准则 |
|---|---|
| 功能ID/用户故事 | 01唯一编号+场景与API输入输出 |
| Owner/实际类/旧来源 | 新仓具体文件与ReferTo行为路径 |
| 实施状态 | 目标/已审/已编码/编译通过/自动通过/UI闭环/专业现场通过 |
| 设计关系 | 02图ID及03—08专题章节 |
| 交付证据 | SHA、提交链接、对应构建、数值/反证/WPF/设备测试结果 |
| 现存问题/下一步 | 具体失败信息、阻塞与责任Owner |
| 兼容/安全 | Schema/迁移、资源/线程、SDK/权限及回滚 |

## 8. Windows CI的严格证据说明

历史记录：Imaging Windows #40619073480（代码SHA 60992cb5）全解决方案构建、结构检查及真实Windows WPF控件无头Smoke成功，但**默认ArchitectureSmoke因其他F17跨进程NamedPipe Bulk测试失败，不能写全CI绿**；同SHA的TestCenter Windows #40619073429在其对应专项成功。这些仅对应各自SHA和当时Workflow；不是本次V2.0文档提交后的重新测试。最新master的其他CI状态会变化，应以对应commit的Actions实际日志核验。文档提交不触发构建也不等于新的源代码质量验收。

## 9. 需要在首次代码阶段形成的证据

至少一项：真实FIM单图+ROI算法的输入/参数/多类型输出合同、从TCT/MPW可操作、同值单元测试、异常反证、Windows CI、结构与Owner检查。其后逐次增加Vision专业算法节点、MEM工程持久化和专业Result；不要为了支持未来全部260项先创建一批无使用者的空接口。

## 10. 设计先行的真实Owner复用审查与裁决

已核实以下真实源代码锚点：MPMTypeDescriptor/MPCPortDescriptor/IMPMTypeProvider，MEMContent/MOLWiringDefinition，MOCEngineeringCompiler/MOCCompiledPlan/MOCSourceMap，MRKExecutionInput/Output与MRKExecutionPlan，FDTDataAdapterRegistry/MDSDataContext/MDSDataBudget/MDSBoundedByteStream，CxVisionDataFlow/CxVisionThresholdProvider/IPVCComputeModule。上述**源码存在不等于完整Vision全类运行已验收**。

| V2.0原候选 | 深化裁决 |
|---|---|
| VisionPipelineDefinition | 先采用MEMContent+专业TypeId+MOL定义，不批准第二持久化Graph |
| VisionTypedPort/ParameterSchema | 先复用MPC/MPM/现有MRK类型端口；只添加必要Vision语义 |
| VisionOperationDescriptor/Factory | 先复用MPMTypeProvider/能力Resolver、FIM静态API；经两算法消费评审再考虑专业Descriptor |
| VisionGraphExecutor/DataHeap | 禁止新增；正式Plan使用MOC/MRK/MDS/F19 |
| VisionRunResult/ResultItem | 可作为专业结果候选，但先复用FDT/MDS数据/Artifact引用，禁止以Overlay保存历史结果 |
| VisionSceneProjection/Controller | 只当工作台投影职责，非必然新增Facade/Controller类 |
| FrameRef/FrameStream | 候选专业数据语义；不把MDS有界字节块冒充完整工业帧流 |
| FIMImageBuffer与CxVisionGrayFrame | 先做8/16位、Stride/字节序/复制与同样本数值互通审查，不急于第三种原始像素类 |

设计推进顺序：先完成Owner/接口与依赖核对→Image/Operation/Result最短链→MEM/MOL/MOC/MRK完整执行→工程ROI持久化与复核→批量/实时/大图及专业SDK。对应00四个Gate、02 X01—X07以及03—08专题；后续真正编码仍须固定提交SHA、Windows CI与用户操作/专业数据验收。此节只记录设计审查事实，未运行本轮Windows构建。

## 11. 三核心契约的源码审查收敛与实现前置条件（2026-10-28）

本轮重新读取最新master相关代码后确认：Foundation.Imaging的FIMImageBuffer公共构造复制像素，具有Stride/Gray16小端/GrayFloat32和色彩灰度语义；FIMPixelSpacing使用独立X/Y毫米每像素；PVC有真实CxVisionGrayFrame、CxVisionInspectionResult和CxVisionDataFlow。工程/流程层已有MPMTypeDescriptor/MPCPortDescriptor、MOCCapabilityDescriptor/MOCCapabilityCatalog、MOLWiringDefinition、MOCFlowBuilder和MOCEngineeringCompiler.Compile；运行层已有MRKExecutionInput/Output、MDSDataContext、FDTDataAdapterRegistry。

**补充裁决**：工程MPCPortDescriptor的TypeId是字符串，运行MRKExecutionInput的ValueType是CLR Type；参数MPMValue支持工程标量/资源引用而不是承载Image/Mask运行像素。因此Vision首先需要真实的专业TypeId与运行数据映射/校验，而非另造端口/参数管理系统。MOC的Catalog展示与Provider实际RuntimeAvailable必须分开。

| 工作包 | 最低可验证交付 | 新增类型批准条件 |
|---|---|---|
| W-DATA-01 | FIM与CxVisionGrayFrame Gray8/16、Stride、颜色/所有权和内存副本对照 | 同样本有不兼容且FDT适配无法安全覆盖 |
| W-DATA-02 | ImageFrame来源/FrameId/代次/Mask空间/Artifact状态模型 | 实际两种源（文件与相机或回放）共享稳定合同 |
| W-ALG-01 | MPM专业vision.* TypeId+参数Schema、MOC Capability目录及Runtime真实可用性 | 无法用现有MPM/MPC表达的专业字段明确且可测试 |
| W-ALG-02 | 工程Data/Control线→MOC→MRK具名输入输出，算法错误/取消可追溯 | 先用已有MOL+MRK，禁止新VisionGraphExecutor |
| W-RES-01 | 一个Run含Image/Mask/Geometry/Measurement并能按SourceRef定位 | 专业Run/ResultItem对象确需持久身份，现有结果无法组合满足 |
| W-RES-02 | ROI显式Promote、MEM撤销/保存重开、历史Run稳定 | 不允许引入第二工程树或ViewerUndo |
| W-UI-01 | 同一工作台算法可无头和WPF调用，Result表与图窗联动 | 真实两个消费者再决定是否增加专业Facade |
| W-RUNTIME-01 | 真实有界帧身份、Backpressure与资源归还 | MDSBoundedByteStream的字节块接口无法单独承载Frame语义 |

**未完成**：W-DATA/W-ALG/W-RES等为实施门禁而非已提交的C#功能；本轮文档更改没有运行Windows CI或实机。后续新增源代码必须在此表记录Owner、真实路径、对应版本、Commit SHA及数值/WPF/CI/SDK证据。

## 12. P0 实际代码提交：FIM与Vision专业灰度帧安全互通（2026-10-28）

> **固定代码证据SHA**：[`c424abcf004d670790a68d6c6c6976f175cf7b5b`](https://github.com/oudream/cx-visual-x3/commit/c424abcf004d670790a68d6c6c6976f175cf7b5b)。**固定Windows CI**：[Imaging Windows #40657978967](https://github.com/oudream/cx-visual-x3/actions/runs/40657978967)，job `imaging-foundation` 成功。其他Agent并行提交使master随后前进，本表证据**仅适用于c424abcf**，不是对未来HEAD的保证。

| 文件（真实源码） | Owner/作用 | 本次实现/范围 |
|---|---|---|
| `Professional/Vision/CxVisionImageInterop.cs`（新） | PVC专业层，复用FIM与FDT | `GetPixelDepth`、`ToGrayFrame`、`ToImageBuffer`、`ToCpuInspectionInputs`；仅Gray8/16，去Stride Padding，16位小端，防御性复制和最大3200万像素约束 |
| `Professional/Vision/CxVisual.Professional.Vision.csproj` | 既有专业项目 | 增加已有Foundation.Imaging的单向依赖及新文件编译项，没有新增csproj |
| `Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionImageInteropTests.cs`（新） | 既有ArchitectureSmoke真实集成测试 | Gray8/16往返、非紧排Stride、输入副本、FDT缓冲隔离、Unsupported与Depth拒绝、FIM输入经现有MRK到PVC真实16位区域检测/GraySum |
| `Tests/ArchitectureSmoke/Program.cs` | 已有测试入口 | 将新测试同时挂在`--imaging`和默认架构回归，不新增第二测试运行器 |

**已验证**：该SHA完整Windows .NET Framework4.8 x64构建、Verify-Structure/Verify-OwnerDependencies、真实WPF二维图窗、`--imaging`及默认ArchitectureSmoke、WPF三维切片均在上述CI成功。新互通功能通过Foundation.Imaging→Professional.Vision→现有MRK/MDS安全输入→专业真实检测结果闭环；但未将新的ResultItem通用结果模型、MEM/MOL正式VisionPipeline、FrameRef/Stream或UI算法实验面板实现为源码。

**复制与性能诚实记录**：FIMImageBuffer公共CopyPixels产生防御副本；CxVisionGrayFrame构造也复制ushort像素；ToCpuInspectionInputs通过FDTReadOnlyBuffer再次捕获紧排RAW。故此次交付并非零拷贝，内存/持续帧吞吐仍需单独压测。FIM的GrayFloat32/BGR/BGRA在本CPU灰度桥中被拒绝，不虚称专业运行可支持这些格式；不会因通用图像显示支持这些格式就暗中降位深。

**后续第一步建议**：在不新增平行框架前提下，从已运行的真实MRK检测结果建立可追踪的专业Run/ResultItem最小语义，再评审并接入MPM/MOC工程Capability与WPF工作台。任何新合同仍需Owner审查及至少两真实消费方。

## 13. P1 第一批真实结果投影：共用MRK与Studio（2026-10-28）

> **源码落点**：从新文件`Professional/Vision/CxVisionRunSummary.cs`、`Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionResultTests.cs`到`Applications/Studio/StudioShell.ModuleCenter.cs`的正式改动。固定代码阶段检查提交[0a5e658](https://github.com/oudream/cx-visual-x3/commit/0a5e658ccf96015e6c7eea77380e873e43d59058)；对应[Imaging Windows #40660774027](https://github.com/oudream/cx-visual-x3/actions/runs/40660774027)已触发，**登记时处于排队状态，不能视为构建/测试通过**。

| 新增/修改 | Owner | 本批真实范围 |
|---|---|---|
| `CxVisionRunSummary`（新） | Professional.Vision | 固定RunId来自真实MRK FlowId，来源SourceId由调用方提供，时间/Revision/计划指纹/Provider版本按可得事实记录，返回只读结果项 |
| `CxVisionResultItem` / `CxVisionRegionSnapshot`（新） | Professional.Vision | DetectionSummary、Region、Statistic、FlowDecision四类**现存算法真实证据**；Region深层快照、集合只读、ItemId随Run唯一 |
| `FromCpuFlow`（新） | Professional.Vision ↔ MRK | 仅从Completed+已提交的`Inspection/GraySum/Passed`读取；失败/取消不允许伪造成功结果 |
| `FromInspection`（新） | Professional.Vision ↔ Studio已编译MOC流程 | 接真实检测StepResult及FlowId、MOC指纹/能力版本；若没有GraySum/Passed导出则不伪造这些结果项 |
| Studio的`StudioCpuVisionReport.Evidence`及区域绑定 | Applications.Studio | 基于共用只读Region结果投影生成原候选区域表；保留原`Inspection`公开行为和原WPF布局 |
| ArchitectureSmoke和Imaging Windows Workflow | Tests与既有CI | 新增`--vision-results`、并纳入`--imaging`及默认回归；Vision专业源码、测试、Studio相关改动触发相同Windows验收 |

**严格边界**：此批是对既有MRK CPU阈值检测输出的轻量聚合，**不是**完整七类RunResult；Image/Mask/ArtifactRef持久化、ReviewRecord、正式结果数据库、多专业算法Schema、Studio完整结果历史和工业硬件待实现。FlowDecision仅是既有CPU流程软件标志，**不能**代替Inspection产品OK/NG。当前SourceId为调用者声明的来源字符串，不等同于来源文件Hash或已签名FrameRef。

**测试状态**：新代码与测试已提交；在#40660774027实际完成前不提升G3/G4证据等级，后续更新固定SHA/Windows结果；新增结果合同仍需实际WPF交互与专业大图性能专项。

## 14. P2 Image/Mask真实产物的MRK最小闭环（2026-10-28）

**真实代码提交**：[c98d933](https://github.com/oudream/cx-visual-x3/commit/c98d933bd31a688e083c88a815dae59751d8c48c)；真实像素/并发测试：[8d5c027](https://github.com/oudream/cx-visual-x3/commit/8d5c027e87b8360246ca5e71a91db157d2512aaf)；FDT载荷估算正反例补强：[9316a139](https://github.com/oudream/cx-visual-x3/commit/9316a13903a7bd69d9c1f71c9d8fe2354d408b46)。**固定Windows CI**：[Imaging #40662918228](https://github.com/oudream/cx-visual-x3/actions/runs/40662918228)，本章节初次登记时仍处于排队，**不作为已验收证据**。上轮P1 [#40660774027](https://github.com/oudream/cx-visual-x3/actions/runs/40660774027) 已独立核实成功，不得拿来证明本次P2新增代码通过。

| 实际文件/Owner | 已提交源码行为 | 限制及安全合同 |
|---|---|---|
| `Professional/Vision/CxVisionDataFlow.cs` | 保留原`CreateCpuInspectionPlan(options,revision)`二参签名和默认3个出口，新增显式`CreateCpuInspectionPlanWithArtifacts(options,revision)`；在原Decode→Preprocess→Inspect节点内部产出处理图与阈值Mask | 无新Executor/Graph/Heap；只在明确选择时额外分配处理图和Mask，避免所有默认调用被迫承担大图副本 |
| `Professional/Vision/CxVisionDataFlow.cs` | 当前Flow的FDT注册`FIMImageBuffer` immutable image adapter、`Stride×Height`大小估算；额外MRK具名导出`PreparedImage`及`ThresholdMask` | 图像由现有FIM私有像素持有，无法经公共API修改；该逻辑仅计托管实际像素载荷，不代表GPU/SDK峰值 |
| `Professional/Vision/CxVisionRunSummary.cs` | `CxVisionResultKind.Image/Mask` 与`CxVisionResultItem.Image`；仅对真实MRK正式提交且格式/尺寸匹配的两项追加同RunId/SourceId/稳定端口身份 | Run内引用，不等于磁盘ArtifactRef、文件持久化、FrameLease或通用多平台结果仓；未新增第二种基础像素类 |
| `Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionArtifactsTests.cs` | 真实MRK灰度图/Mask数值、Gray16带Stride输入、掩码与接受Region不同、失败不发布、默认路径兼容、并行Run隔离、FDT大小估算 | 被`--vision-results`、`--imaging`及默认ArchitectureSmoke自动包含；对应Windows CI需按本轮SHA记录 |

**Mask的具体语义**：`ThresholdMask`为**Preprocess之后、四邻接/面积规则筛选之前**的整图逐像素二值0/255阈值命中；它可能比`Inspection.Regions`包含更多孤立区域。用`ThresholdPixelCount`同样本交叉核对，禁止将该Mask称作已接受缺陷实例分割。

**内存与生命周期**：额外输出确实引入托管像素分配和FIM拷贝，FDT只按实际`Stride×Height`逻辑载荷估算，完整CPU工作区与GPU/SDK暂存另计。结果投影没有创建全局缓存、长期保留策略或资源数据库；消费者保存`CxVisionRunSummary`时会持有图像对象直到释放引用。后续完整Image/Mask Artifact索引、跨Run持久、显式租约、Studio图窗产品化显示仍待实现和实测。

**后续方向**：结合已有MVI/MPW图窗用`ResultItem.Image`投影真实图像及Mask；再按MEM/MOC正式资源版本和RunRef实现浏览/错误状态/导出，先做同源图注册与取消/旧回写安全，不能把Viewer缓存当ResultStore。

## 15. P3 TestCenter 真正可交互的CPU检测、Mask/图像与轮廓测量闭环（2026-10-28）

**实施状态：源码已提交，Windows完整验收尚待本轮固定SHA的CI。** 代码提交：[`ee62cdf`](https://github.com/oudream/cx-visual-x3/commit/ee62cdf54488084df4a6135663e60998ab975f5f)（真实MRK调用/来源令牌/取消）、[`cd38af2`](https://github.com/oudream/cx-visual-x3/commit/cd38af252fff40661d7155bae3d2e8b7bb509cd6)（处理页操作按钮）、[`63d3945`](https://github.com/oudream/cx-visual-x3/commit/63d39450648ce689f7c0ece492088f914936e677)与[`6d64979`](https://github.com/oudream/cx-visual-x3/commit/6d649797ba88f7ba60c95fd0ba3388499401d0ab)（类型化结果列表、输出页）、[`a9bd0dc`](https://github.com/oudream/cx-visual-x3/commit/a9bd0dc9bb023055043dd85dabac67bc4f7d7c53)（Mask→轮廓→面积真实WPF操作测试）、[`4f91211`](https://github.com/oudream/cx-visual-x3/commit/4f91211fb3b14fcd2ad9009d3f143228996ee5a6)（带孔洞面积与有效Spacing下mm²显示）。固定Windows [Imaging #40666425061](https://github.com/oudream/cx-visual-x3/actions/runs/40666425061) 在本节首次登记时仍排队，**不冒充CI全绿**。

| 实体Owner和文件 | 真实调用/可见操作 | 明确限制 |
|---|---|---|
| `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs` | 新`RunCpuVision`：`FIMImageBuffer`原图→`CxVisionImageInterop.ToCpuInspectionInputs`→`CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts`→现有`MRKExecutionEngine`→`CxVisionRunSummary.FromCpuFlow`→`MPWImageViewerControl.ShowResult` | 仅CPU Gray8/Gray16 RAW像素；无相机SDK、AI硬件或实时流 |
| `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerLayout.cs` | 处理页灰度阈值/最小面积/执行/取消；输出页类型化结果ListBox及Run项查看 | 不新增第七工作台或第二结果Storage |
| `TCTManualImageViewerFrame.cs` | `_cpuSourceId`本次Viewer来源代次、`MVIImageSession.SourceVersion`、原图引用及CTS校验；旧异步结果不能覆盖新图；原图/显示结果被替换时清理证据 | SourceId是**TestCenter会话临时令牌**，不是摄像机硬件SN、内容Hash或可持久FrameRef |
| `TCTImageViewerSmoke.cs` | 真实WPF点击CpuVisionRun，核对Gray16输入、0/255 Mask、Area=7 Region、Image/Mask结果项选择；再点击既有ExtractMaskContours核对轮廓面积7；重载相同FIM图后清空旧结果 | WPF测试仍需本SHA的Actions实际运行；不能把代码存在当作UIA验收 |
| `TCTManualImageViewerFrame.cs`既有FIM轮廓功能 | FIMImageMaskContours.Trace保留外环/孔洞；所有环SignedArea求和得到真实像素面积；有FIMPixelSpacing时用sx×sy换算mm²；否则拒绝伪物理单位 | 无配准或畸变矫正的完整3D/非线性校准；mm²仅按用户当前的独立像素间距 |

**跨模块CI阻塞信息**：早期P2相关[Imaging #40663246695](https://github.com/oudream/cx-visual-x3/actions/runs/40663246695)为failure：`Professional/Vision/CxVisionDataFlow.cs`曾有CS0136局部变量同名错误；当前master已将lambda参数改为`imagePixels`，需以修复后的固定SHA实际CI验证。后继[Imaging #40665129173](https://github.com/oudream/cx-visual-x3/actions/runs/40665129173)因并行`Foundation.Transport`的`ConfigureSendAdmission`缺失（CS0103）失败，不属于此轮Vision Owner，此文件不代替其Owner跨模块修复。以上两种错误和CI状态不能混为一谈。

**下一阶段仍未完成**：工业FrameRef/Artifact持久化、正式Studio端图像算法实验UI、RunItem持久审计/人工ReviewRecord、真正设备HIL、AI推理及工业性能长时验证。现阶段测试页已可操作，但不能称通用二维视觉工作台全部260项能力已完成。


## 16. P3 继续实装：参数代次失效、结果槽定向清理与带孔洞FIM测量（2026-10-28）

**真实代码提交（均已进入master）**：

- [becc0505850493a27adf302bc37f3d64fe4ffeaf](https://github.com/oudream/cx-visual-x3/commit/becc0505850493a27adf302bc37f3d64fe4ffeaf)：CPU阈值/最小面积变动即取消旧MRK Run、清除旧专业证据与本工作台拥有的MVI Result槽；新Run启动前同样清理旧结果。新增MVIImageSession.ClearResult()，保留原图、ROI/标注与其它Owner结果，不建立新Viewer/缓存。
- [a287f45d2cda4a6004d3e9c9ff8250361c4333f1](https://github.com/oudream/cx-visual-x3/commit/a287f45d2cda4a6004d3e9c9ff8250361c4333f1)：现有ArchitectureSmoke MVIImageSessionTests补充ClearResult首次清理、重复幂等、Observer仅一次、SourceVersion递增及原图激活两种状态；已接入原--imaging与默认测试入口。
- [3b15550baacc72db8b06cd8678982eae9d23c497](https://github.com/oudream/cx-visual-x3/commit/3b15550baacc72db8b06cd8678982eae9d23c497)：轮廓像素/毫米面积调用既有FIMImageMeasurement.GetPolygonAreaPixels/GetPolygonAreaSquareMillimeters，保留FIMImageMaskContours.SignedArea判别外环与孔洞；TestCenter真实WPF点击对3×3 Gray16环形图执行MRK，验证Mask中心孔洞、两个MVI多边形、9−1=8px²、非等距Spacing 0.2×0.4下0.64mm²，并核对原图与结果槽不被改写。

| 修改归属与真实文件 | 正向与反向合同 | 当前证据 |
|---|---|---|
| MVI：`Mechanism/Visual/Imaging/MVIImageSession.cs` | ClearResult有结果时版本+1/单次Changed/回原图；无结果时幂等不发事件；不会清原图或既有Overlay | 源码G2，Windows G3/G4待对应CI |
| TestCenter：`Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs` | 监听阈值与最小面积编辑，取消旧任务/清列表/清本Owner的Result；其它结果槽不被误清；重跑仅接受当前Request、SourceId、SourceVersion | 源码G2，实际WPF自动测试待执行 |
| TestCenter：`Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs` | 真实按钮与列表；Gray16阈值70000拒绝，阈值10→11的Region面积7→6和新RunId；参数12取消在途Run，旧结果不得晚到；环形Mask带孔洞测量回归 | 测试代码已纳入，不能以“有断言”冒充已执行 |
| ArchitectureSmoke：`Tests/ArchitectureSmoke/Integration/MVIImageSessionTests.cs` | 无界面MVI幂等、代次、观察者和原图保护回归 | 需实际Windows结果 |

**固定工作流追踪**：首次参数清理源码 [Imaging #40672434612](https://github.com/oudream/cx-visual-x3/actions/runs/40672434612)、[TestCenter #40672434709](https://github.com/oudream/cx-visual-x3/actions/runs/40672434709)；加入MVI无头断言后 [Imaging #40672538011](https://github.com/oudream/cx-visual-x3/actions/runs/40672538011)；含孔洞测量最新代码 [Imaging #40672752671](https://github.com/oudream/cx-visual-x3/actions/runs/40672752671)、[TestCenter #40672752691](https://github.com/oudream/cx-visual-x3/actions/runs/40672752691)。**首次登记时以上均queued，尚无本SHA的自动测试成功证据。** 较早 [Imaging #40671851110](https://github.com/oudream/cx-visual-x3/actions/runs/40671851110) 实际success但其SHA早于上述改动，不得用于宣称新P3已通过。

**当前限制**：这里只完善TestCenter CPU试验的Result槽和测量状态，不等于持久Artifact/FrameRef/ReviewRecord、正式Studio全面工作区、孔洞复合ROI工程持久化、工业现场HIL或全类260项完成；现场操作与性能G5/G6继续独立验收。

## 17. TestCenter二维工作台可操作性修复：窗位、灰阶、工具分类（2026-10-28）

**反馈证据**：人工截图明确反映窗宽/窗位、Gamma/亮度/对比度使用不便，以及“显示”页彩色 ROI/测量/标注/缺陷按钮无法选择工具。实际源码证实旧四按钮仅调用`SetSelectedOverlayColor`，必须预先选中一个Overlay；这与用户想先选择绘制类别的意图不一致。原灰阶底层`MVIImageDisplayState`和`FIMImageToneMapping`存在，不另建图窗或图像处理副本。

**代码提交**：[593e007d6ba0ba6e4a99bcf6548e5eabf4354d7b](https://github.com/oudream/cx-visual-x3/commit/593e007d6ba0ba6e4a99bcf6548e5eabf4354d7b)（增加四个真正的绘制分类按钮，并与唯一MPW.ToolMode/ComboBox及高亮同步；改色操作迁到单独“选中对象改色”；窗位输入数字边界/Enter/自动实际灰度范围；Gamma/亮度/对比度/反色/自动对比度交互入口；MVI SetTone整组参数单次通知减少大图多重重绘；真实WPF/ArchitectureSmoke反例）、[4dd52be](https://github.com/oudream/cx-visual-x3/commit/4dd52be557a6c7c1f10e17fb58367f3b2afe7da5)（修正ResetDisplay滑块刷新顺序）、[1a8a288](https://github.com/oudream/cx-visual-x3/commit/1a8a28875ee69cbfb560cc6b9c4f9a5c60cb5a64)（数值像素与重置滑块专项断言）、[9394e3b](https://github.com/oudream/cx-visual-x3/commit/9394e3b0d8c88bdaad4483460307daf3bb41eea5)（WPF测试MPW命名空间编译引用）。

| 源码和归属 | 实际行为 | 范围限制 |
|---|---|---|
| `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerLayout.cs` | 彩色分类按钮`SelectMode.ROI/测量/标注/缺陷`分别选择矩形ROI/距离/点标注/缺陷框；已有改色`OverlayColor.*`继续处理已选图元样式；显示分类与图层可见性独立；滑块Thumb拖动结束、键盘和输入框Enter执行 | 不把色块当作完整图元编辑器，不替代已有测量/标注子工具 |
| `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs` | 窗位明确检查已加载灰度图、数字有限性及上限大于下限；自动对比度以已存在MVI与FIM显示处理为主；Gamma等输入有合法性拒绝与真实可见结果；复位后滑块与当前参数一致 | UI显示变换不改变原像素；彩色不支持单通道窗位；大图真实P95仍需专项 |
| `Mechanism/Visual/Imaging/MVIImageDisplayState.cs` | `SetTone`先校验全部参数后原子更新，最多发一次Changed；无变化不发；与原逐项API共存 | 只改状态通知粒度，不引入新缓存或第二绘制内核 |
| `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs`、`Tests/ArchitectureSmoke/Integration/MVIImageDisplayTests.cs` | 真正WPF按钮点击+参数变更+原像素反证，错误窗下限“b”、非法Gamma、四分类选中互斥与高亮、输出PNG数值差异、Reset Slider；MVI单次通知与非法无部分写入 | 测试源码存在不代表Windows已执行通过 |

**固定SHA的Windows追踪**：[Imaging #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082)及[TestCenter #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116)对应9394e3b，首次登记时in_progress（未取得成功/失败结论）。任何后续CI需在完整Job日志明确证明WPF和--imaging运行通过后才能登记G3/G4，人工截图反馈只证明缺陷场景而非复测通过。

### 17.1 实际Windows结果与Gray8补丁（固定SHA，2026-10-28）

- **已实际通过的Windows证据**：[Imaging #40683606082](https://github.com/oudream/cx-visual-x3/actions/runs/40683606082) 对应代码SHA `9394e3b0d8c88bdaad4483460307daf3bb41eea5`，job `imaging-foundation` **success**，包括Windows x64全项目构建、结构/Owner DAG、真实二维WPF ImageViewer和PNG回环、FIM/MVI专项与默认回归、三维WPF切片。此证据仅证明该固定SHA，不覆盖之后新增的位深默认值修复。
- **实际失败但非Vision Owner**：[TestCenter #40683606116](https://github.com/oudream/cx-visual-x3/actions/runs/40683606116) 对应同SHA，Windows构建后在 `Verify real Windows UI Automation user workflow` 失败；实际日志报 `Channel UIA did not verify real TX/RX/error evidence from Connectivity; ... viewerPresent=False`，属于Channel工作区UIA/可见性失败，不是本轮二维WPF像素测试失败。保留完整失败，不通过跳过测试伪装成功。
- **后续真实代码提交**：[eb9d453](https://github.com/oudream/cx-visual-x3/commit/eb9d4532d21cb57debd5e4ef182dceb448de2675)（新Gray8原图默认0..255，防止沿用Gray16的65535窗口）、[884b75c](https://github.com/oudream/cx-visual-x3/commit/884b75cc1a9e1002f7a59fecc884f1635fe0863e)（真实Gray8 WPF PNG与原像素反证）、[3661cca](https://github.com/oudream/cx-visual-x3/commit/3661ccad0bcbd882499ac261bdecf97ad3440079)（载入新图同步Gamma/亮度/对比度文本、滑块及反色/自动对比度控件）。对应 [Imaging #40683998349](https://github.com/oudream/cx-visual-x3/actions/runs/40683998349) / [TestCenter #40683998312](https://github.com/oudream/cx-visual-x3/actions/runs/40683998312) 初次登记时均in_progress，不冒充新SHA验收成功。

## 18. 二维图窗WinForms交互迁移：窗宽/窗位双滑条、精确ROI取窗、选色对话框（2026-10-28）

**人工问题依据**：新截图中“显示”页彩色ROI/测量/标注/缺陷两组按钮功能重复、缺少颜色对话框；WPF窗位页只有上下限输入，无法像ReferTo成熟WinForms的窗宽/窗位数值和拖动条操作；灰阶要对齐Gamma 0.1..3、亮度 -255..255、对比度0..3，并保留自动灰度拉伸/反色。

**当前实装（真实源文件）**：

- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerLayout.cs`：移除显示页两组彩色类别及固定改色按钮，保留图层勾选和完整工具选择，区域/测量/标注专业入口照旧；窗位主面板换成窗宽/窗位TextBox+WPF Slider，拖动结束/键盘提交，应用窗位勾选、框选自动取窗、刷新窗值、恢复自动；原下限/上限入口收进折叠高级页；灰阶亮度用用户习惯的 -255..255、显示范围端点及有效步长。所有控件有AutomationId。
- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs`：窗宽窗位中心值映射到既有`MPWImageViewerControl.SetDisplayWindow(low,high)`，新图像按Gray8/Gray16/Float32更新滑条默认范围；框选像素ROI调用`FIMImageAnalyzer.Calculate`，所选可见面积Overlay调用`FIMImageAnalyzer.AnalyzeMasked`+真实`ContainsRoiPixelCenter`，不能用外框冒充不规则ROI；无ROI时全图自动对比度；亮度UI 8bit级与MVI [-1,1]显示量换算；原图不改变；区域页选中图元时打开系统`System.Windows.Forms.ColorDialog`，取消不写入，修改后仍走唯一MVI OverlayStore，保留原Alpha。
- `Tests/TestCenter/CxVisual.TestCenter.csproj`：增加标准.NET Framework 4.8 `System.Drawing`/`System.Windows.Forms`引用以复用WinForms色板，未添加新NuGet/第二Viewer。
- `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs`：移除过时彩色按钮假设，改为验证滑条/数值同步、Thumb.DragCompleted真实事件、ROI 1..6取窗、自动拉伸、Gamma/亮度因子、Gray8上下限及颜色属性入口。Windows CI仍需固定SHA验证。

**本轮源码提交**：[1b28918](https://github.com/oudream/cx-visual-x3/commit/1b2891802b3273be7253f94170df9d63ce3028b7)（UI）、[52a5f86](https://github.com/oudream/cx-visual-x3/commit/52a5f865416f16794118c86f9fa1b77c01ba2374)（运行状态及WinForms对话框）、[e68af77](https://github.com/oudream/cx-visual-x3/commit/e68af7762356b7fcad611a480218cda88e10ef24)（引用）、[e5fcbc7](https://github.com/oudream/cx-visual-x3/commit/e5fcbc7b1d15adb6e656b7e382911f4060e49b11)（WPF行为测试）、[92986dc](https://github.com/oudream/cx-visual-x3/commit/92986dcdf5100f121b9e12c00f51f561caabe61b)（亮度映射修正）、[60171f0](https://github.com/oudream/cx-visual-x3/commit/60171f068cfac7cc57e1114c59b01ce0821a5ff2)（Thumb实际拖动反例）、[e77cae8](https://github.com/oudream/cx-visual-x3/commit/e77cae8d91d14f2f54358c3c912dc93605029745)（CI发现WPF测试重名CS0128后修复）。

**CI诚实记录**：提交`92986dc`的 [Imaging #40687303441](https://github.com/oudream/cx-visual-x3/actions/runs/40687303441) 和 [TestCenter #40687303519](https://github.com/oudream/cx-visual-x3/actions/runs/40687303519) 确认Windows编译失败，`TCTImageViewerSmoke.cs`测试`argbField/applyProperties`与后文同域变量冲突CS0128；修复固定SHA`e77cae8`已提交，其 [Imaging #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) 与 [TestCenter #40687637624](https://github.com/oudream/cx-visual-x3/actions/runs/40687637624) 初次登记时仍queued/in_progress，**暂不宣称新版本CI通过、UI实际截图通过或工业现场验收**。

### 18.1 Windows固定证据与颜色对话框窗口Owner补正

- [Imaging Windows #40687637635](https://github.com/oudream/cx-visual-x3/actions/runs/40687637635) 固定`e77cae8d91d14f2f54358c3c912dc93605029745`，**success**：Windows x64全项目编译、结构DAG、真实WPF二维ImageViewer PNG回环、FIM/MVI专项+默认回归、三维切片工作台及截图上传均成功。之前测试局部变量重名CS0128已修复并由本固定SHA构建实际验证。
- 后续 [4d6c558](https://github.com/oudream/cx-visual-x3/commit/4d6c558cddf78e8c9af367e27aca731b03bf59aa)：标准`ColorDialog`使用真实WPF窗口HWND作IWin32Window Owner，避免被主窗口遮挡；手动窗位与全图自动拉伸切换时同步MVI与UI勾选。其 [Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944) / [TestCenter #40687957971](https://github.com/oudream/cx-visual-x3/actions/runs/40687957971) 初次核查尚在执行，不能继承旧固定SHA成功结论。

### 18.2 WinForms四图层颜色选择的准确语义与修复（后续Owner决议）

**更正本节18开头的过渡描述**：用户反馈的第一组彩色ROI/测量/标注/缺陷分类按钮确实与专业工具操作重复，已删除；**第二组“设ROI色/设测量色/设标注色/设缺陷色”必须保留**，其WinForms真义是各图层通用颜色而非“当前选中图元”颜色。前一版将两组均移除并只保留选中对象颜色设置不够完整，已通过本次提交纠正。

- [57c13d0](https://github.com/oudream/cx-visual-x3/commit/57c13d0b91267d31bd736ca86ac9ce5171859a72)：`MPWImageViewerControl`在原Viewer内维护轻量四图层颜色预设，`GetOverlayLayerColor/SetOverlayLayerColor`可无选中项设置；对已有未锁定Overlay保持Id/坐标不变并改色，新增使用默认Argb的Viewer图元应用用户预设；显式指定颜色的专业产物不被默认色覆盖。不建第二Viewer或第二OverlayStore。
- [a9f0b9f](https://github.com/oudream/cx-visual-x3/commit/a9f0b9f8e65ba7638ca152f5883e7dfe6c1b78f3)：显示页只恢复第二组4个颜色操作按钮，真正系统`ColorDialog`，颜色同步于按钮背景；区域/测量/标注工具仍以专业页签和唯一ToolMode选择。
- [ba5e241](https://github.com/oudream/cx-visual-x3/commit/ba5e241d14a77cada2a2d6509c97e748d03a37bd)：四个图层颜色对话框无须选中图元，显式指定WPF窗口HWND Owner，选中后同时应用现有与未来默认绘制；取消无数据写入。
- [788d5fe](https://github.com/oudream/cx-visual-x3/commit/788d5fec78111784052ef9080c72a42bcf369e1a)：真实WPF测试确认第一组分类已移除，第二组四按钮仍存在；API测试更新已有ROI颜色保Id，之后新ROI继承用户选色；当前图元独立属性编辑照旧。

前一固定SHA [Imaging #40687957944](https://github.com/oudream/cx-visual-x3/actions/runs/40687957944) **success**，但不覆盖本四图层新能力；最新对应 [Imaging #40688664400](https://github.com/oudream/cx-visual-x3/actions/runs/40688664400) 和 [TestCenter #40688664402](https://github.com/oudream/cx-visual-x3/actions/runs/40688664402) 首次登记queued，实际验收须以Job结论为准。

### 18.3 鼠标真实绘制与色板初始值一致性补正

源码审计发现鼠标交互结束的`MPWImageViewerControl.CompleteDraw`原来直接调用`_overlays.Add`，绕过`AddOverlay`的新默认色分派，导致API创建多边形有效但真实鼠标创建依旧沿用红色构造默认。现 [0eeedc0](https://github.com/oudream/cx-visual-x3/commit/0eeedc0aa0843c4f886156bab0068c38600b91a0) 已将鼠标完成绘制统一进入`AddOverlay`。另外 [ec1e044](https://github.com/oudream/cx-visual-x3/commit/ec1e0445251bbb1c6097fbf4c257039e08019551) 保证未手动改色前新图元采用显示页四图层默认色，而不出现按钮示蓝绿、图元默认红的冲突；明确非默认ARGB仍保留。 [9d9a3a1](https://github.com/oudream/cx-visual-x3/commit/9d9a3a14458a89efd2bbd454f428187f3ed2dc8d) 修正默认色一致后先前WPF颜色回归的旧色比较前提。须以最新SHA重新执行Windows CI，不能使用788d5fe之前的记录覆盖这些增量。

## 19. CASE-ARCH-01首包：MRK独立面积测量及无界面预览入口（2026-10-28）

**范围与语义**：在已有CPU灰度检测、FIM→Vision互通、Image/Mask/Region结果之上，新增**独立MeasureAcceptedRegions MRK步骤**。正式端口`AcceptedRegionPixelArea`是经过Min/MaxDefectArea筛选的Region像素面积总和（单位px²），不等于筛选前`ThresholdMask`非零像素数，不是无标定物理mm²，也不替代Inspection质量判定。结果回到同一`CxVisionRunSummary`的类型化`Measurement`项，沿用MRK真实FlowId作为RunId、已有SourceId和只读Items。

**真实变更**：
- [404d9d3](https://github.com/oudream/cx-visual-x3/commit/404d9d39c9b653081292f331d0b5fc70ff9bd70c) `Professional/Vision/CxVisionDataFlow.cs`：增加显式`CreateCpuInspectionPlanWithMeasuredArtifacts`，其余两个工厂签名和默认输出数完全不变。新增MRK数据端口从`Inspect.Result`读取面积筛选后的Region，溢出/不一致拒绝；经MDS提交及原有失败Abort屏障而不是从Viewer数轮廓冒充正式测量。
- [dd5506f](https://github.com/oudream/cx-visual-x3/commit/dd5506fffa303804358ede9c6106f162c2bd5d3d) `Professional/Vision/CxVisionRunSummary.cs`：末尾追加枚举`Measurement`避免更改旧序号，核对输出类型、Area、Mask和接受Region面积，总量一致才追加具名Item；原旧计划依旧不出现Measurement。
- [f2d55bb](https://github.com/oudream/cx-visual-x3/commit/f2d55bbd612280e0fbabb1c64db0ca27d4127ea5) `CxVisionDataFlow.RunCpuInspectionPreviewAsync(FIMImageBuffer,Options,SourceId,CancellationToken)`：真正**无界面**公开调用入口，仅复用当前MRK引擎、FDT/FIM输入、MDS输出、取消和StopAsync。不是MEM/MOC正式工程发布API，UI仍负责比对源代次，不能作为设备现场授权。
- [c25e50d](https://github.com/oudream/cx-visual-x3/commit/c25e50d96f91f20458a1dfb5db02835f0a89ecd0) `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs`：替换页面内复制的MRK生命周期代码，转调上述无界面API，保留旧Run清除、SourceVersion/Request校验和MVI结果槽安全。
- [047a855](https://github.com/oudream/cx-visual-x3/commit/047a85587fac8f2830d1d9080047221a2f4c3791)、[a34e034](https://github.com/oudream/cx-visual-x3/commit/a34e03411fa715ff04a3c64e783ba7d30d97e0dd)：真实MRK Mask 4命中 / Region面积3差异、空区域面积0、坏输入原子失败、带Stride无界面API、位深/SourceId/预取消。
- [5b34232](https://github.com/oudream/cx-visual-x3/commit/5b3423258a96db58f808e755c4338b45464414b0) `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs`：真实WPF点击CPU、浏览Measurement结果，验证同RunId、端口名、7 px²数值，点击测量不破坏当前Mask和Gray16原图。

**边界**：当前是单图CPU正式MRK子链及一个测量节点，尚非MEMContent/MOL中增强→分割→区域→测量的四种通用MPM能力。FrameRef/ImageSet/ArtifactRef持久化、单位/标定、跨Run ReviewRecord、多Provider、工业批量/设备/HIL、Studio正式多节点工程编辑与发布仍属于未完成，不将这些P设计标为G2。

### 19.1 固定提交Windows回归证据（首轮）

`a34e03411fa715ff04a3c64e783ba7d30d97e0dd` 的 [Imaging Windows #40693675846](https://github.com/oudream/cx-visual-x3/actions/runs/40693675846) **success**，包含.NET Framework4.8 x64全项目构建、结构与Owner DAG、真实WPF二维图窗/PNG回环、ArchitectureSmoke `--imaging`及默认回归、三维切片WPF交互；本次新`MeasuredArtifactsRemainSourceCorrect`和`SharedHeadlessPreviewApi`纳入现有`--imaging` Run路径。因此该固定SHA可以作为算法测量与无界面API的Windows自动行为证据，**不等于**完成MEM/MOC四种可编排专业节点，也不证明工业HIL/大图性能。
同期 [Runtime Windows #40693675869](https://github.com/oudream/cx-visual-x3/actions/runs/40693675869) 对同SHA **success**，包含完整解决方案构建及统一Runtime专项。不同固定SHA的[TestCenter Windows #40693629251](https://github.com/oudream/cx-visual-x3/actions/runs/40693629251) 在首次登记仍in_progress，待单独完成UIA核实。
