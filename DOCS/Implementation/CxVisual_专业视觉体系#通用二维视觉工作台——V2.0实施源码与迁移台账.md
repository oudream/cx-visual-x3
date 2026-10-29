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

## 20. CASE-ARCH-01第二阶段：工程化MOL检测→测量能力节点与公开输出（2026-10-29）

**实际开发范围**：在已有P3、MRK直接Plan和无界面API之上，进一步把RAW输入→CPU阈值检测→已筛选区域面积测量作为**唯一MEM/MOL工程中的3个真正节点**，MOC负责编译、来源图及软件发布，MRK负责运行、Trace和MDS具名结果；原2节点`CxVisionRawCpuRunner.ExecuteAsync`不强制升级，新`ExecuteMeasuredAsync`显式启用3节点。Studio专业CPU入口和TestCenter Compute共享同一Professional调用（无另建VisionGraph或执行器）。

**源码**：
- `Professional/Vision/CxVisionDataFlow.cs`：抽出内部`MeasureAcceptedPixelArea`，直接MRK Plan与MOC专业Measurement能力共用，单位仅px²。
- `Professional/Vision/CxVisionThresholdProvider.cs`：已有检测Provider声明专业`Inspection`输出端口；另外在同一Professional Owner新增`CxVisionAcceptedRegionMeasureProvider`，能力`vision.cpu.region.accepted-area`，输入`Inspection: vision.cpu.inspection.result`、输出`Area: long`，发布唯一MRK能力及版本标识；运行时验证真实`CxVisionInspectionResult`，不在Foundation/MOC重实现算法。面积走MDS `Data.Return("Area")`。
- `Professional/Vision/CxVisionRawCpuRunner.cs`：MEM/MOL创建三节点/两条真实Data连线（RAW→Inspect→Measure），明确声明`AcceptedRegionPixelArea`公开输出；原MOCPrepare/Stage、版本指纹、受限PlanSlot和MRK执行全部复用。运行验证StepResult、CommittedOutput、已接受Region合计一致并通过SourceMap Trace验证Measurement已完成。
- `Professional/Vision/CxVisionRunSummary.cs`：`FromInspection`新增可选测量字段，老调用仍得到原Item；测量项目与Region、阈值命中数、尺寸一致后才以同RunId发布Measurement。
- `Applications/Studio/StudioShell.ModuleCenter.cs`和`Tests/TestCenter/Manual/Module/TCTCpuComputePanel.cs`：实际CPU RAW入口改为显式测量工程版，界面显示px²面积；并未改动MFM/MPM公共注册中心。
- `Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionArtifactsTests.cs`：创建真实内存工程、从RAW路径执行3节点、核对Trace与端口和测量值、MEM Save/Open确认三块及连线、旧2节点兼容；`Tests/TestCenter/Verification/TCTCpuComputeUiSmoke.cs`补真实WPF新MeasureBlockId/Trace/Measurement证据断言。

**现实边界**：`vision.cpu.inspection.result`属于现有MOC专业端口TypeId，在当前MOC编译器对非基础CLR类型采用`object`承载，Provider运行时对实际`CxVisionInspectionResult`做强类型拒绝；尚不等于跨所有Professional CLR类型的静态编译类型注册。RAW内容现由一次调用的Provider克隆持有：MEM Save/Open只证明流程定义持久化，不代表重新打开后无需重新绑定RAW就能执行、生产发布或长时复现。下一阶段需通过正式工程输入ResourceRef/Frame/Artifact/Calibration与版本管理解决，不允许第二套缓存或推断设备已验收。

**Windows状态**：初次CI在[Imaging #40809026028](https://github.com/oudream/cx-visual-x3/actions/runs/40809026028) / [Engineering #40809025895](https://github.com/oudream/cx-visual-x3/actions/runs/40809025895) 因新增回归对脏工程`Dispose`触发`Unsaved changes`失败；[Studio S1 #40808893404](https://github.com/oudream/cx-visual-x3/actions/runs/40808893404) 因旧测试误设专业输出端口数为0失败。已在[be62449](https://github.com/oudream/cx-visual-x3/commit/be624496c2360f37485d61455402c986040303b0)及[13f5b42](https://github.com/oudream/cx-visual-x3/commit/13f5b42c2949dd24e518e2f3fc4e52ada13dac60)修正，保留正确强类型输出断言。后续MDS正式具名输出与运行提交一致性补充见最新代码。**新固定SHA必须重新跑CI，不能从先前的Windows成功推断全部通过。**

### 20.1 工程节点首轮Windows结果（固定SHA）

`13f5b42c2949dd24e518e2f3fc4e52ada13dac60` 的 [Engineering Organization Windows #40809379292](https://github.com/oudream/cx-visual-x3/actions/runs/40809379292) **success**，以及 [Module Center CPU RAW E2E #40809379183](https://github.com/oudream/cx-visual-x3/actions/runs/40809379183) **success**：修正测试脏工程释放后，正式MEM/MOC测量工程与旧2节点回归已在Windows执行，现有CPU工作流基本兼容。请勿把这两个成功扩大解释为随后`Area`具名MDS导出补丁、完整Studio/TestCenter UIA也已通过。

同SHA [Studio S1 #40809379277](https://github.com/oudream/cx-visual-x3/actions/runs/40809379277) **failure**，实际日志停在“配置组合未使用可响应布局的统一1/2/3列容器”，属于并行配置呈现Owner/UI断言，与本次CPU输出类型断言并非同一失败点。本阶段不跨Owner擅改它。更新后的MOC具名Area输出（`6df4f808`～`5ec2ef62`）须以其独立固定SHA的Imaging/Runtime工作流重新验收。

### 20.2 MDS跨步骤冻结门禁的实际修复

增加`Area`具名MRK/MDS输出后，Windows [Module Center CPU RAW #40809580828](https://github.com/oudream/cx-visual-x3/actions/runs/40809580828) 实测失败，日志为`类型尚未注册安全复制/冻结能力：CxVisual.Professional.Vision.CxVisionInspectionResult`，说明原两节点返回值可直接返回，但Inspect→Measure跨步骤需要正式FDT对象安全复制，不能传播可变Region。修复复用已有`CxVisionDataFlow.CopyResult`深拷贝和容量估算，在`CxVisionThresholdProvider.ExecuteAsync`正式注册`FDTDataKind.Object`适配器；没有绕开MDS的冻结检查。提交：[92f5eb6](https://github.com/oudream/cx-visual-x3/commit/92f5eb60b63f169cee0b358dec686de1d4c29f96)、[f1b7e86](https://github.com/oudream/cx-visual-x3/commit/f1b7e86927f4efd927793f5f7d62a0af4776c8e1)、[d047c0c](https://github.com/oudream/cx-visual-x3/commit/d047c0c04f8fab7f61031f9a2477903bca2e7e58)；[564b5d7](https://github.com/oudream/cx-visual-x3/commit/564b5d7d9516646cd668f165c3cb96226b42eb89)修复测试失败路径时脏工程Dispose掩盖源异常的问题。

对应修复的 [Imaging #40810246586](https://github.com/oudream/cx-visual-x3/actions/runs/40810246586)、[Engineering #40810246877](https://github.com/oudream/cx-visual-x3/actions/runs/40810246877) 和 [Runtime #40810246928](https://github.com/oudream/cx-visual-x3/actions/runs/40810246928) 初次登记queued/in_progress，不得以修复代码取代CI实际通过。

### 20.3 已保存工程重新绑定RAW后复跑（2026-10-29）

在`Professional/Vision/CxVisionRawCpuRunner.cs`新增`ExecuteSavedMeasuredAsync(session,contentId,vision,path,width,height,token)`。明确要求调用者提供重新打开的MEM Session、唯一工程ContentId、专业Provider和RAW源；先读取现有`MEMProjectSnapshot/MOLWiringDefinition`，检查3个正式节点、两条Data边、公开`AcceptedRegionPixelArea`、预定义宽高等真实约束，再复用原`MOCProjectRelease.Prepare/Stage → MRKPlanSlot → MRKExecutionEngine`，**不添加页面，不修改工程Revision，不创建第二存储**。每次执行生成新MRK FlowId；RAW内容仍来自本次显式绑定，不视为持久Frame/Artifact，也不绕过MHS/Host正式授权。

源码 [5719775](https://github.com/oudream/cx-visual-x3/commit/57197758127966df79ad7dcd7cc8b3b0662bfc44)；架构正反例 [9f7cbb3](https://github.com/oudream/cx-visual-x3/commit/9f7cbb3e042051335b7bd210844b05a6fd2b66f9)。首次Windows日志显示新增负例以5×3文件配6×3输入，先于工程检查被RAW长度门禁拒绝，不能期待工程专用异常；[dceed1d](https://github.com/oudream/cx-visual-x3/commit/dceed1d7a8ceb0b6685615ebf8b83245947553ef) 将其改为真实18字节的6×3 RAW，以独立证明已保存5×3工程拒绝参数冲突。实际 [Imaging #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554) / [Engineering #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) 的最终固定SHA结论须查Actions，文档首次记录时仍in_progress。

另有真实TestCenter Compute UIA负例重试时，将前一次“CPU检测失败”的旧状态误当第二次运行的完成状态；[2b110f5](https://github.com/oudream/cx-visual-x3/commit/2b110f5429bba324a57c5e024d49dd615dfcf97e) 改为等待**新Run正式产生LastReport**，而不是旧失败文本，避免误判或取到旧宽度6的异常。固定[Compute UIA #40811544416](https://github.com/oudream/cx-visual-x3/actions/runs/40811544416)待结果；其同SHA [Four G1 #40811544542](https://github.com/oudream/cx-visual-x3/actions/runs/40811544542) 已success，但不同工作流不互相替代。

### 20.4 固定SHA的重新运行实际结果

- **正式CPU WPF操作**：[TestCenter Compute UIA #40811544416](https://github.com/oudream/cx-visual-x3/actions/runs/40811544416) 对应`2b110f5429bba324a57c5e024d49dd615dfcf97e`，**success**。同SHA [TestCenter Module Center Four G1 #40811544542](https://github.com/oudream/cx-visual-x3/actions/runs/40811544542) **success**。此前连续两个UIA工作流失败是旧错误状态被当作新执行完成，修复后实际操作闭环通过。
- **保存重开后显式重绑**：[Imaging Windows #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554) 与 [Unified Runtime Windows #40811670497](https://github.com/oudream/cx-visual-x3/actions/runs/40811670497) 都针对固定`dceed1d7a8ceb0b6685615ebf8b83245947553ef`，**success**。Imaging在实际Windows x64构建、`--imaging`和默认架构回归中执行Save/Open后不新建页、重绑真实RAW、重新MOC/MRK执行、尺寸不匹配拒绝。Runtime同SHA构建与编排专项也成功。
- [Engineering Organization Windows #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) 对同固定SHA首次核查仍in_progress，需单独等待真实作业结果。以上通过是自动行为等级，尚未替代真人工业现场、帧存储Artifact及完整通用Vision四节点实验工作台。

### 20.5 最新Engineering总体门禁的跨Owner阻断

对固定`dceed1d7a8ceb0b6685615ebf8b83245947553ef`，[Engineering Organization Windows #40811670459](https://github.com/oudream/cx-visual-x3/actions/runs/40811670459) 最终**failure**。日志明确是`Foundation.Transport.FTRNamedPipeServerChannel.OnCloseAsync`第164行关闭具名管道时的`System.ObjectDisposedException: Safe handle has been closed`，堆栈由`FTRNamedPipeBulkTests.MultiplePeersAndIndependentSends`触发。此处属并行FTR/Transport Owner，非Vision算法、MEM保存重开或MOC具名Area输出的失败；保持失败不跳过、不擅自覆盖Owner源码。同期固定SHA的[Imaging #40811670554](https://github.com/oudream/cx-visual-x3/actions/runs/40811670554)与[Unified Runtime #40811670497](https://github.com/oudream/cx-visual-x3/actions/runs/40811670497)为success，作为本阶段Vision专项和Runtime独立门禁成功证据。后续需要Transport Owner修复NamedPipe句柄关闭竞态，再重新执行完整Engineering最终门禁。

## 21. CASE-ARCH-01第三阶段：FIM增强→检测/连通区域→面积测量的正式四节点工程（2026-10-29）

**既有Owner直接复用**：中值滤波不是在Vision重新实现，算法严格调用`Foundation.Imaging.FIMImageFilters.Apply(FIMNeighborhoodFilter.Median, radius)`，位深、边界复制由现有FIM负责；专业能力声明仅使用原`MOCCapabilityDescriptor/MPCPortDescriptor`，编辑与持久化依旧`MEMContent/MOLWiringDefinition`，执行仍经`MOCProjectRelease.Prepare/Stage`、`MRKExecutionEngine`和`MDSDataContext`。新增能力`vision.cpu.image.median-enhance`，输入`RawPixels:byte[]`、`Width:int`、`Height:int`、`Radius:int`，公开`EnhancedPixels:byte[]`；Radius合法范围1—3，ReleaseVersion编码位深与半径，非法参数在编辑/执行前失败，不降位深、不假造AI/GPU。

**实际提交**：
- [ba0064a](https://github.com/oudream/cx-visual-x3/commit/ba0064a2833a1e346fcd338c1be8149dbbba8679)、[8f59c44](https://github.com/oudream/cx-visual-x3/commit/8f59c44f97d3086cfbd5d5d7599f7f2633950523)：`Professional/Vision/CxVisionThresholdProvider.cs`同Owner内添加`CxVisionMedianEnhanceProvider`，复用FIM核并用MDS具名EnhancedPixels输出，半径作为正式MOL/版本固定参数。
- [04da0f8](https://github.com/oudream/cx-visual-x3/commit/04da0f8298d28366f122aadbc1803534dd6b9d73)：`CxVisionRawCpuRunner.ExecuteEnhancedMeasuredAsync`及`ExecuteSavedEnhancedMeasuredAsync`提供四节点原工程/重开显式重绑；保留旧2/3节点API，MOC编译并发布两条具名中间/最终输出（EnhancedPixels和AcceptedRegionPixelArea），不建第二Graph。
- [a55d094](https://github.com/oudream/cx-visual-x3/commit/a55d0948b396d9242701653ecb242f3f93c85ca3)：ArchitectureSmoke真实7×7 Gray8样例，3×3亮块及孤立亮点，经FIM半径1中值后仅余5个接受像素，EnhancedImage和直接FIM结果逐像素相同；检查公开端口、MOL四块三条Data连接、Trace、输入不变、Save/Open无编辑重绑、错误半径拒绝及Revision不变。
- [cca7515](https://github.com/oudream/cx-visual-x3/commit/cca75150396edf8f3406d4405cb5e13b2c38d280)、[5880d88](https://github.com/oudream/cx-visual-x3/commit/5880d88204ea1db0122bd57647306f6e04c60e3b)、[2547fbf](https://github.com/oudream/cx-visual-x3/commit/2547fbf6d27b2b8e9c3fba3f9b315d9840688350)：增强图像作为只读FIM Image项加入`CxVisionRunSummary`，与Measurement/Region具有同一FlowId/SourceId。
- [beb5882](https://github.com/oudream/cx-visual-x3/commit/beb588280b9d9ef6321c6dbf16a4b1ec91b715fd)、[49b40da](https://github.com/oudream/cx-visual-x3/commit/49b40da48099219a8114b2ddd3b671981aa9e57a)：TestCenter Compute同一WPF操作页选择临时中值半径0/1/2/3（0=旧三节点，1—3=四节点），真正RAW工程运行；UIA点击Run，验证5px²、Trace、增强图及未修改输入RAW；该参数尚未被宣称为MHS持久配方。

**第一轮实际验证**：固定`a55d0948b396d9242701653ecb242f3f93c85ca3`的[Imaging #40820286578](https://github.com/oudream/cx-visual-x3/actions/runs/40820286578)、[Engineering #40820286876](https://github.com/oudream/cx-visual-x3/actions/runs/40820286876)、[Unified Runtime #40820286552](https://github.com/oudream/cx-visual-x3/actions/runs/40820286552)、[Decision #40820286603](https://github.com/oudream/cx-visual-x3/actions/runs/40820286603)、[Config L1 #40820286619](https://github.com/oudream/cx-visual-x3/actions/runs/40820286619)及[配置底座 #40820286566](https://github.com/oudream/cx-visual-x3/actions/runs/40820286566)均为**success**。注意后续增强ResultItem/WPF UIA的SHA不同，不能继承首轮通过；其独立UIA [#40820622091](https://github.com/oudream/cx-visual-x3/actions/runs/40820622091) 须单独验收。

**没有完成的部分**：四个工程节点中第三个仍是当前`CxVisionThresholdPipeline`的一体化“阈值+四连通区域”，不是独立的Threshold Mask和Region Component两个可复用Provider；FrameRef/ArtifactRef持久化、专业ROI、AI/GPU/设备/HIL、海量图像性能仍待实施。中值Radius仅是暂时执行界面参数，最终Recipe配置与权限归MHS正式Owner。

### 21.1 Gray16和原WPF Viewer复用补充（2026-10-29）

- [3ed5a15](https://github.com/oudream/cx-visual-x3/commit/3ed5a1526d21d3a1862d432384f47854943c47d1)增加7×7 Gray16小端RAW的真实四节点FIM中值滤波验证：中心4000灰度经中值保留、孤立60000被滤除、5px²测量、输出Gray16保持位深且输入文件字节不变。固定SHA [Imaging Windows #40820986353](https://github.com/oudream/cx-visual-x3/actions/runs/40820986353) **success**，[Engineering #40820986454](https://github.com/oudream/cx-visual-x3/actions/runs/40820986454) **success**。
- [2847c2c](https://github.com/oudream/cx-visual-x3/commit/2847c2c7af10e02c8fab197cc9a712f36fc509fc)在真实TestCenter Compute页使用原`Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl`/MVI图像会话，只有成功四节点增强才懒创建/显示预览；切换Module或发起新Run清空旧预览，不引入第二Viewer数据Owner，也不把RAW/增强图假称持久Artifact。[382a8f7](https://github.com/oudream/cx-visual-x3/commit/382a8f7cab5d7f466ac267ad0f73447be0ba91eb)验证图窗中真实像素和同Run增强结果一致。
- 初次登记时，包含UI预览检查的[Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787)仍pending，必须根据最后一轮Actions结果补登，不因前一版[Compute UIA #40820622091](https://github.com/oudream/cx-visual-x3/actions/runs/40820622091) success而继承界面图窗验收结论。

### 21.2 WPF实际验证进展

固定`49b40da48099219a8114b2ddd3b671981aa9e57a` 的 [TestCenter Compute CPU UIA #40820622091](https://github.com/oudream/cx-visual-x3/actions/runs/40820622091) 和 [Four G1 #40820622056](https://github.com/oudream/cx-visual-x3/actions/runs/40820622056) 均**success**：证明可选FIM中值参数的真实WPF操作、5px²测量及SourceMap。随后补原MPW图窗显示，固定`2847c2c7af10e02c8fab197cc9a712f36fc509fc` 的 [Compute UIA #40821068464](https://github.com/oudream/cx-visual-x3/actions/runs/40821068464) **success**：证明增强图窗加入后原WPF操作仍可执行。最后`382a8f7`另加控件ActiveImage像素级验证，当前[Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787)首次登记仍in_progress；不可用之前success代替这一新增断言。相同SHA [Module Center RAW #40821146764](https://github.com/oudream/cx-visual-x3/actions/runs/40821146764) **success**。

### 21.3 最新MPW增强图像像素级UIA完成

固定`a6f43b8d0016c4ca08e6aa95e7a6c1428d74fcec` 的[TestCenter Compute UIA #40821146787](https://github.com/oudream/cx-visual-x3/actions/runs/40821146787) **success**，包括`PreviewImage`从原`MPWImageViewerControl`实际读取像素(0,0)=0、(3,3)=210，对应同一MRK四节点增强结果；[Module Center CPU RAW #40821146764](https://github.com/oudream/cx-visual-x3/actions/runs/40821146764) **success**。此固定SHA只是追加Studio Decision文档及代码的并行提交，不扩大本轮Vision业务Owner。TestCenter全项目其他工作流仍独立验收。

## 22. 2026-10-29 用户真实6144×3072 Gray16大图四问题修复：显示、取消、窗位、直方图

**截图输入（人工报告，不代表CI截图）**：
1. X-Ray Gray16全图在大尺寸工作区域没完整适应、看起来被裁切；
2. VS暂停在`FIMImageAnalyzer.Calculate`的`OperationCanceledException`（取消本身是合法合同，不能将其改造成静默发布旧统计；是否VS首发/用户未处理取决于调试设置）；
3. 窗宽/窗位大数值输入框窄且缺可见两端范围信息；
4. 6144×3072、18874368像素、256档原始灰度直方图需要手动点且曾实际显示12542.9ms。

**定位与真实修改**：
- `Mechanism/Presentation.Wpf/Imaging/MPWImageViewerControl.xaml`（[6709e45](https://github.com/oudream/cx-visual-x3/commit/6709e453530d89b55ff02de019854876369901b4)）：原`PixelImage`/`SecondaryPixelImage`宽高直接参与Grid DesiredSize，原生大图可能撑大可用Viewport并使Fit和居中与物理裁剪错位。增加不参与测量的Canvas容器；原`MVIImageViewport.Fit`缩放矩阵及ROI/Overlay原坐标Owner不变。测试`TCTImageViewerSmoke`增加4096×2048实际视口比例/中心/矩阵反证（[ff9d6cd](https://github.com/oudream/cx-visual-x3/commit/ff9d6cd6e909c27b880b61f99704a16534ca3cfe)、[aa2ff1b](https://github.com/oudream/cx-visual-x3/commit/aa2ff1b8be33d8794025d7eb51739c407f9c9f3e)）。
- `Foundation/Imaging/Processing/FIMImageAnalyzer.cs`：Gray8/16`Analyze`和`Calculate`改为单遍逐行`CopyRow`、原始整数精确频数(256/65536槽)、ROI/Stride有效像素、频数计算均值和中心化总体标准差；256档由实际Min/Max聚合，常量图和取消不回退到全图双遍像素Welford。未改Float32/彩色旧行为。源码[bdc4514](https://github.com/oudream/cx-visual-x3/commit/bdc4514a1a979a49982b6964e803391e6505899d)、[83dd8ed](https://github.com/oudream/cx-visual-x3/commit/83dd8ed58276347e68569522c9ffca38063d2148)。
- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs`：自动后台统计（图片打开即自动触发，不抢占右侧当前页签），同图显示窗调整不重扫原像素；ROI/切图最新请求取消旧结果。并在`Task.Run`工作线程就地消费“已取消的旧请求”OCE，不把预期取消冒泡成VS用户未处理，仍保留FIM算法公共取消测试，失败不发布旧统计。源码[0ab264f](https://github.com/oudream/cx-visual-x3/commit/0ab264fdfa1909e98031499591a38853d4001b66)、[8ff63f3](https://github.com/oudream/cx-visual-x3/commit/8ff63f336b68b70736e88ad9c59cf155d2a57035)、[ff79ea2](https://github.com/oudream/cx-visual-x3/commit/ff79ea2e846fdc4ea57bc399f1f59c01456c5e0b)。
- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerLayout.cs`/`Frame.cs`：加宽窗宽/窗位数值框、动态显示Gray8/Gray16原始数值及滑条两端范围、AutomationId可测；保留手动/ROI/自动映射为显示操作，不修改Raw。源码[333c56c](https://github.com/oudream/cx-visual-x3/commit/333c56c658f85ecd750bedbe001576f651372a52)、[25f2d51](https://github.com/oudream/cx-visual-x3/commit/25f2d511f3ceef5986c6a4a7d0537b29d3ec071b)。
- 回归`Tests/ArchitectureSmoke/Integration/FIMImagingTests.cs`：带Padding Gray16、精确ROI、末端Bin、常量65535、取消；首次Windows CI报CS0128局部`padded`重名，已修复[788a7b0](https://github.com/oudream/cx-visual-x3/commit/788a7b0260d54f4fbd73047157b2b503eed3405a)；`TCTImageViewerSmoke.cs`在打开图后**无需点击**统计按钮检查256点、Gray8/16范围、4096×2048真实Fit、像素不变。注意UI实际临界效果需等最新固定SHA/工业大图人工截图确认。

**已核实**：修复变量冲突后的[Imaging Windows #40828384405](https://github.com/oudream/cx-visual-x3/actions/runs/40828384405) success；包含大图Fit新增回归的[Imaging #40828542464](https://github.com/oudream/cx-visual-x3/actions/runs/40828542464) success。最新打开即统计与取消修订后的CI仍须按新固定SHA结果核验；不能臆测6144大图实际耗时已降至某个毫秒数，也不把合成4096图布局测试当用户现场截图验收。

### 22.1 统计自动化与Mask轮廓取消域隔离

[Imaging Windows #40828887469](https://github.com/oudream/cx-visual-x3/actions/runs/40828887469) 对固定`8ff63f3` 的首次全WPF Smoke结果为**failure**，日志在`TCTImageViewerSmoke`“真实CPU Mask→FIM轮廓→像素面积7链路错误或破坏原图”，而原先未自动计算直方图时该链路曾通过。逐源核对发现原`_analysisCancellation/_analysisEpoch`同时控制FIM统计、ROI处理和轮廓处理；新自动统计在`Session.Changed/Overlays.Changed`调用`CancelAnalysis`，有可能把刚生成Overlay时仍在执行的轮廓任务误取消（真实跨功能生命周期耦合）。

已在[98912b4](https://github.com/oudream/cx-visual-x3/commit/98912b4c972c9737ceab09dc7f016da92de9b657)修复：统计独占`_histogramCancellation/_histogramEpoch`；统计重算只调用`CancelHistogram`、不取消Mask轮廓；原图真正变更仍保留`CancelAnalysis`，而`_publishingContours`守护内部AddOverlays/SelectOverlay，不因轮廓自身发布触发错误取消。原图、结果图和Run证据Owner不变。不删除失败记录、不放松旧Mask轮廓测试，后续[Imaging #40829484522](https://github.com/oudream/cx-visual-x3/actions/runs/40829484522)和[TestCenter #40829484729](https://github.com/oudream/cx-visual-x3/actions/runs/40829484729)必须重跑；首次登记in_progress。

### 22.2 已通过的取消域隔离固定SHA及窗值刻度

`98912b4c972c9737ceab09dc7f016da92de9b657` 的[Imaging Windows #40829484522](https://github.com/oudream/cx-visual-x3/actions/runs/40829484522) **success**，包括重新启用“自动直方图+专业CPU Mask/轮廓7px²同页”的真实WPF Smoke，确认分离统计与专业处理取消域后不再回归；`73ecd00be782463c3ada7095de410e400458e5a7` 的[Imaging Windows #40829769427](https://github.com/oudream/cx-visual-x3/actions/runs/40829769427) **success**，且保留手动ROI修改取消旧操作、内部发布轮廓不自取消。两次实际成功不可等价为真实现场6144×3072图像完整人眼截图。

[fb3bcc3](https://github.com/oudream/cx-visual-x3/commit/fb3bcc36116d74d9b1168db3915b9fb5bee8cc67)、[8a225fb](https://github.com/oudream/cx-visual-x3/commit/8a225fbf31003ffa922c19d7f6749f20b7d4546b)：追加每条窗宽/窗位滑条上下端点可见刻度，动态更新`255/65535`；[18f0c93](https://github.com/oudream/cx-visual-x3/commit/18f0c93d1a2eeef37fec398e7dce28f001071ce7) + [267db43](https://github.com/oudream/cx-visual-x3/commit/267db43581d6adfa4270ca9fda980df88464c7fe)补UIA端点绑定。新固定SHA必须重新通过Imaging Windows才能宣称这批刻度验收已完成。

## 23. V2D-12/21：复用FIM轮廓作为按需专业Run结果（2026-10-29）

- **代码固定提交**：[28e2f0d](https://github.com/oudream/cx-visual-x3/commit/28e2f0da4a17d04c9a45bdd111378dcb5845fae0)、[e7228b8](https://github.com/oudream/cx-visual-x3/commit/e7228b84f2d2925b96b2416338753663da8b9c0f)；**自动测试新增**：[d5ff3df](https://github.com/oudream/cx-visual-x3/commit/d5ff3df9342d021b2bb5d3cca6b6129f490e10fb)。
- **Owner与复用**：`FIMImageMaskContours.Trace`保有0/255掩码追踪及孔洞计算唯一算法实现，`FGEPoint2D`仍为既有几何坐标；PVC `CxVisionRunSummary.WithThresholdMaskContours`仅消费已提交MRK `ThresholdMask`，按需投影为只读`CxVisionContourSnapshot`及`CxVisionResultKind.Contour`，继承RunId、SourceId和原OutputPort，不新建MDS/Graph/工程状态。
- **设计边界**：不自动生成所有大图轮廓；`maxBoundaryEdges`最多2000000，支持CancellationToken；顶点为原图像素**边界**坐标，正面积外环、负面积孔洞。当前是**阈值Mask全部命中像素**的轮廓，不能冒充Inspection面积筛选后的区域或mm²标定结果；无Mask的旧Plan明确拒绝。图窗可选择消费这种产物，但本阶段没有声称完成Studio/MPW界面接线。
- **状态**：源码及正反自动测试文件已在master（G2）；截至本条登记，**Windows .NET48构建、Imaging --imaging专项、全回归和真实WPF/UIA未核验**，不可登记G3/G4/G5通过。后续须按包含上述代码的固定SHA复核工作流及实际数字输出。

## 24. V2D-12/18/19/21/23：独立Mask→Region→Measurement工程链（2026-10-29）

- **正式新增**：`Professional/Vision/CxVisionSeparatedProviders.cs` 两个Provider：`CxVisionBinaryThresholdProvider`接收RAW/宽高/固定阈值，复用FIM带取消阈值算法并产生原图尺寸0/255 Gray8 Mask；`CxVisionMaskRegionsProvider`消费真实Mask，复用`CxVisionThresholdPipeline`四邻域连通域与面积筛选，输出原`CxVisionInspectionResult`，不建第二套连通域逻辑。明确Mask包含筛选前全部命中像素，Region仅包含面积规则接收区域。
- **MOC/运行**：新增`CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync`及`ExecuteSavedSeparatedMeasuredAsync`，可选半径0(4节点)或1..3(5节点FIM中值)。使用既有MEM/MOL编辑、MOC FlowBuilder编译、MOCProjectRelease发布和MRK/MDS原子执行、Trace、具名`ThresholdMask`/`AcceptedRegionPixelArea`出口；既有2/3/4节点旧入口保持原签名和默认行为。新Runner以`SegmentBlockId`和同Run `ThresholdMask`返回真实节点/产物身份。
- **参数与版本**：分割节点持阈值/位深版本；区域节点持面积上下限和配置Revision形成的`RuleVersion`，作为正式MOC `string`参数，重开时与持久规则比较，不一致在编译发布前拒绝。自定义Mask TypeId由MOC映射`object`并由PVC进行FIMImageBuffer/Gray8/0或255强校验；不把CLR类型冒充公共TypeId。
- **专业结果**：复用`CxVisionRunSummary.FromInspection`，增加可选同Run `ThresholdMask`投影；可继续按需调用已存在`WithThresholdMaskContours`获得孔洞/轮廓。未另建Result仓库。
- **关键提交**：[Provider首次实现](https://github.com/oudream/cx-visual-x3/commit/9c278278670ff294afbfe38ad1b4d8d58594393f)、[MEM/MOC完整执行](https://github.com/oudream/cx-visual-x3/commit/a75b2d23a138de9d9bb8a9efdee2e3f6c31b2521)、[Mask Result投影](https://github.com/oudream/cx-visual-x3/commit/34c531e2d29b0166da9034574b89f79fba0214af)、[工程规则固定](https://github.com/oudream/cx-visual-x3/commit/755d8491e49c6a9358f5a49095ea1dbb7d89f6da)、[实测用例源码](https://github.com/oudream/cx-visual-x3/commit/4ef84c0d5001399fc1fbc41fa5a4f52820150598)、[Mask端口编译映射](https://github.com/oudream/cx-visual-x3/commit/a7636b1e31dd5bf14277fbc968e604b5caacc975)。
- **严格状态**：提交了正式代码和可执行测试，但尚未取得匹配最新SHA的Windows MSBuild/ArchitectureSmoke/UIA结果，暂记G2（新代码在仓库）而非G3/G4/G5。分割/连通域尚未接入Studio通用可视算子面板；32M像素中间副本/峰值和实际设备样本性能待测；整套`VisionRunResult`、长期Artifact/Frame引用及实时流仍非已交付。

## 25. V2D-01/12/19/21/24：FIM内存图像直通拆分式工程与TestCenter（2026-10-29）

- **共享专业API**：`CxVisionImageInterop.ToPackedRawPixels`沿用现有ToCpuInspectionInputs字节紧排逻辑，支持Gray8/Gray16与Stride跳Padding，防御复制、不改原图、不落临时RAW；`CxVisionRawCpuRunner.ExecuteSeparatedMeasuredImageAsync`直接接受FIM图像、真实SourceId和已有MEMEngineeringSession，走已实现的MOC编译、发布、MRK四/五节点执行，不添临时执行器；`CxVisionDataFlow.RunSeparatedProjectPreviewAsync`在PVC内部管理临时MEM预览Session，返回同Run`CxVisionRunSummary`，预览工程不持久化、不是工业发布。
- **TestCenter真实操作**：`TCTManualImageViewerLayout`增加`TCT.Imaging.CpuVisionSplitRun`和`TCT.Imaging.CpuMedianRadius`控件；0对应无增强四节点，1～3对应五节点FIM中值。老`CpuVisionRun`继续可用。WPF图窗仅使用原`MPWImageViewerControl.TryShowResult`和MVI SourceVersion原子验证；图像、结果清理、取消、参数变更仍复用现有控制与CPU证据列表，未创建第二套Viewer状态。
- **测试**：`RuntimeVisionArtifactsTests.SeparatedMemoryImagePreview`核对带Stride Gray16同样本拆分/旧MRK数值、Mask和测量、来源身份、原像素不变、错误位深、空SourceId、非法增强半径及已取消请求。`TCTImageViewerSmoke`在真实WPF窗口点击拆分模式和可选增强、核对Mask像素/面积、ResultItem、原16位像素保留和配置变更后旧Run失效。
- **代码提交**：[内存紧排](https://github.com/oudream/cx-visual-x3/commit/0589c35a0dc7d003516925a25dd98b3d4bcfedf3)、[专业工程直入](https://github.com/oudream/cx-visual-x3/commit/cbec73a9d096e8e34aaceee6ebf8ac4686ba228d)、[共享预览](https://github.com/oudream/cx-visual-x3/commit/01042934e6f63f791b358907bc2ddf9c34ce7ce1)、[TestCenter入口](https://github.com/oudream/cx-visual-x3/commit/4818293e047b840118e65093788fa1e55e4c9fa7)、[Headless测试](https://github.com/oudream/cx-visual-x3/commit/5138ed22d3324971bea1183ed7a639cadf4bcf1d)、[WPF测试](https://github.com/oudream/cx-visual-x3/commit/36c5eadbf4e7df6fd30a7e1a6d4c1ded0a0ee278)。
- **证据边界**：上述文件已提交，但未获得匹配SHA的Windows 2022 / .NET Framework 4.8 x64 编译及WPF smoke日志，暂为G2，不得宣称G3/G4/G5。32M像素上限、临时MEM实例创建成本、大图副本峰值与正式长生命周期数据租约还须单独验收。

## 26. V2D-12/21/24：正式Run轮廓的TestCenter结果与只读Overlay投影（2026-10-29）

- **历史架构不改**：PVC原有`CxVisionRunSummary.WithThresholdMaskContours`依靠FIM轮廓、FGEPoint2D、同Run来源、孔洞符号与只读`CxVisionContourSnapshot`；本次不新建第二OverlayStore、Run仓库、MOC或MEM资源类型。
- **用户可见入口**：TestCenter二维工作台输出页新增`CpuGenerateContours`按钮（`TCT.Imaging.CpuGenerateContours`）；必须已有绑定当前Original的真实CPU Run，异步按需计算，在回到WPF时核验Run对象、SourceId、SourceVersion、分析代次与取消。重复生成不会重扫已有Contour；一次运行最多256项显示，单个MVI多边形上限10万顶点。
- **结果统一**：`PopulateCpuResultItems`保留原Image/Mask/Region/Measurement，新增Contour净带符号面积、外环/孔洞信息。单击轮廓结果在原图坐标创建（或复用）带`ExternalId=ResultItemId`的只读`MVIImageOverlayItem`；保持真实`RunId/SourceId`不变，防重复叠加且不把轮廓当MEM正式ROI。临时轮廓投影采用MVI已有`Overlays`和MPW Viewer。
- **生命周期**：重跑、切图、图窗结果被其它算法覆盖或CPU参数修改时，只删除当前Run记录的临时轮廓投影，并保留其它用户手绘ROI/Annotation；旧结果项和变更前轮廓不允许继续当作当前Run显示。取消或计算失败不得发布部分Contour项。
- **源码提交**：[TestCenter Run与Overlay](https://github.com/oudream/cx-visual-x3/commit/214f19bb04f6b0b139f53f929cf3669d87eef1fc)、[输出页按钮](https://github.com/oudream/cx-visual-x3/commit/d239d31a60caf65af708f35b31b0d1fdcfc72225)、[WPF正反测试](https://github.com/oudream/cx-visual-x3/commit/e1eabcddf7544cd314e4275b37340c269dacfe72)。
- **验收边界**：此前固定[Imaging 40865537994](https://github.com/oudream/cx-visual-x3/actions/runs/40865537994) jobs确认含Windows构建、WPF ImageViewer、`--imaging`/`--vision-results`/默认回归、3D WPF，均Success；**该Run早于本条轮廓UI改动**，不能充当本条G3/G4/G5验收。新修改有文件回读与静态检查，但仍须以对应SHA取Windows CI、真实WPF Screenshot和结果日志。

## 27. V2D-18/19/23/24：Studio MHS生效配置驱动独立四五节点工程（2026-10-29）

**真实代码（本轮）**：

- `CxVisionThresholdProvider.CaptureInspectionOptions()`：对已固定的专业参数进行防御复制；若来自`MHS Effective`，调用既有Provider版本/授权检查，参数变更或撤销后拒绝复用旧Provider。不返回MHS内部可变对象。
- `CxVisionRawCpuRunner.ExecuteSeparatedMeasuredProviderAsync`：用已确认的专业Provider及其快照直接执行已有MEM/MOC/MRK四/五节点链，而非用新的无MHS来源Provider替代原授权执行实例。兼容现有旧算法入口。
- `Applications/Studio/StudioShell.ModuleCenter.cs`：在已有MHS Effective、项目Scope、RAW文件参数、取消、执行Trace、区域结果和主流程编排入口旁新增`Studio.ModuleCenter.RunSeparatedRaw`和`Studio.ModuleCenter.MedianRadius`。0=RAW→Mask→Region→Area四节点，1～3=加FIM中值的五节点。运行后仍激活本MEM工程页，显示实际Flow/SourceMap/面积/节点身份；旧`RunRaw`保持兼容。未创建新的Studio工程树、运行引擎或WPF工作台。
- **Studio自测**：`Applications/Studio/StudioShellSelfTests.cs` 真实Gray8 5×3四节点Mask命中4、接受面积3px²、MOL 4块3数据边、Mask与Area具名输出；7×7中值5节点孤立噪点剔除，保留RAW字节；MHS配置快照不可变、变更Effective后旧Provider无法新建工程、非法半径零写入。测试与生产直接共用同一入口。
- **提交**：[Provider快照](https://github.com/oudream/cx-visual-x3/commit/48b80816f68046f495012caa2fb4bdf79847d4e7)、[受控拆分Runner](https://github.com/oudream/cx-visual-x3/commit/4efd2873a63dde1f81827d2eebf711b071b80afa)、[Studio入口](https://github.com/oudream/cx-visual-x3/commit/ac8f8573fdb77e14ce1e5dc508b15b915363e34d)、[Studio测试](https://github.com/oudream/cx-visual-x3/commit/af1c2ab36063f609fafbea08094f1ae9568f4a39)。
- **未宣称**：本次只完成代码与本地源码静态一致性检查（G2）。**针对这批最新提交的Windows x64/.NET48编译、Studio CPU-only和全量WPF测试日志尚未取得**；不可登记G3/G4/G5通过。设备驱动、HIL、生产发布授权与运行期恢复仍不在此交付范围。

## 28. V2D-18/19/24：Studio独立算子统一发现、MOC签名门禁和正式MEM节点插入（2026-10-29）

- **实现源**：`Applications/Studio/StudioShell.ModuleCenter.cs`重用既有MOC目录和`MFMModuleDescriptor`，将`CxVisionThresholdProvider`、`CxVisionBinaryThresholdProvider`、`CxVisionMaskRegionsProvider`、`CxVisionAcceptedRegionMeasureProvider`四类真实CPU软件能力登记为同一Compute模块，无第二能力注册表；页面自动展示端口描述、运行解析状态，不将软件Resolver视为设备授权。
- **单一编辑入口**：`MainWindow.AddStudioVisionCapabilityToFlow`接受原MEM工程、现有Content、已读取生效配置的Provider及Capability ID。先经`CaptureInspectionOptions`核对MHS Effective变更/撤权、现有MOC Catalog、运行解析和`MOCBindingContractInspector`静态合同，再调用唯一`MOCFlowBuilder.Edit`生成正式Block。阈值输入`Threshold`和区域输入`RuleVersion`来自有效配置并写入MOL持久参数；Mask/Inspection等图像实际输入不做隐式自动接线，工程师必须到原MOL编排界面完成连接和发布。
- **类型边界**：MOC此阶段对专业TypeId采用CLR `object`映射，只对白名单`ThresholdMask`/ `Inspection`的单个`MDC_BIND_OBJECT_UNVERIFIED`允许已存在的PVC强类型运行校验；任何其他`Rejected`或`Unknown`均拒绝修改MEM。不得冒称全自定义类型静态编译验证已经完成。
- **自测**：`StudioShellSelfTests`真实WPF模块中心过滤Compute得到四类真实Provider、验证其强类型语义端口；CPU-only独立MEM会话经生产同一入口插入分割、连通区域、面积三个节点，核实3个ID及阈值/区域规则固定、所有图像输入仍未自动连接。未知能力和MHS生效版本过期必须在工程Revision未变化前拒绝。
- **提交**：[统一目录及流程入口](https://github.com/oudream/cx-visual-x3/commit/e46e6b70db71f762d441cf28cf495a5dd28edb29)、[Studio目录测试](https://github.com/oudream/cx-visual-x3/commit/0516f34a3da5afa91d2be1259acc9bac469259b3)、[原子编辑单入口](https://github.com/oudream/cx-visual-x3/commit/6923e5fb570835097fc7c5efa1f912c1474a5846)、[真实MOL节点/拒绝测试](https://github.com/oudream/cx-visual-x3/commit/57cf1400f8c5aa6f40c42b120def906d45c27335)。
- **验收状态**：代码与测试源已写master，未取得与本提交匹配的Windows x64编译、Studio `--module-center-cpu-self-test`及全S1 WPF日志，现登记G2；完整四节点在Studio画布中的交互连线、配置Schema统一编辑、Image/Mask/Contour可视选择和产品级发布仍为下一阶段待完成。

## 29. V2D-02/04/12/21/24：Studio真实Run的Image/Mask/Contour/Measurement可视工作台（2026-10-29）

- **同源执行像素快照**：原PVC `CxVisionRawCpuRunner.ExecuteCoreAsync`新增可选`captureSourceImage`，仅Studio持有已固定MHS Effective的拆分式运行显式开启；在真实MRK完成后用本次`pixels`产生不可变`FIMImageBuffer`，赋值`CxVisionCpuRun.SourceImage`→`StudioCpuVisionReport.SourceImage`。没有任何磁盘RAW二次读取，避免文件被替换时“结果A、图像B”的错误；原Headless/旧CPU API默认不保留额外大图，原签名和测试不受影响。
- **共享WPF/MVI**：Studio原`MPWModuleCenterControl.HeaderContent`中增加可折叠`ResultPanel`、生产`MPWImageViewerControl`、`ResultItems`及`ShowRunOriginal`、`GenerateRunContours`两个真实操作控件。正式静态方法`PopulateStudioVisionResults`/ `SelectStudioVisionResult`负责同Run来源校验、Image/Mask结果显示、Region/Measurement数据与单位、Contour只读PolygonAnnotation；临时轮廓直接由`MVIImageOverlayStore`持有并以`ResultItemId`去重，仍无第二套图元或工程资源。
- **异步防护**：运行前、切换Effective配置时，旧列表和Viewer原图/Overlay清空；后台Contour提取使用100000边界预算及CancellationToken，回UI验证Run对象、工程ProjectId、源图引用和Session.SourceVersion，轮廓超过256项不发布。旧Run无SourceImage时仍可查看数值统计/测量，但不得伪造可显示的原图或Mask；修改或清空原图后必须拒绝以前的可视结果。
- **真实Studio STA自测**：模块中心存在`Studio.ModuleCenter.ResultPanel`及图窗/列表/轮廓按钮；`VerifyCpuVisionFileClosure`用真实5×3 RAW→MEM/MOC/MRK四节点，校验原图210/250、Mask命中、MPW Session实际切到Result、轮廓去重只读、过期Run/清空原图拒绝、旧无图Run数值兼容，以及RAW磁盘文件后来被另一7×7输入覆盖也不改变已提交Run原图。
- **提交**：[按需持原像素](https://github.com/oudream/cx-visual-x3/commit/c3d6457874a0883383373d0835e1c1a8e64e3d7c)、[同Run WPF投影](https://github.com/oudream/cx-visual-x3/commit/d634451d474c47f2759e0878ecc411224faac606)、[可操作Studio面板](https://github.com/oudream/cx-visual-x3/commit/cdfa9615c8aa485532f89c48af294e9ff9106f27)、[WPF源码测试](https://github.com/oudream/cx-visual-x3/commit/7f9a1038c262d4187fd57f15513188a0f12d5588)、[旧Run测量兼容](https://github.com/oudream/cx-visual-x3/commit/b33c9b10729b7effd6516d608ce0d0a02be0ba71)、[RAW不可漂移回归](https://github.com/oudream/cx-visual-x3/commit/42f78d265516ae9edefa95b6a883d65f8c6f7215)。
- **边界**：本批已将源代码及自测提交master，读取并静态检查核心源文件，尚无匹配当前SHA的Windows .NET48 Release/x64及真正STA WPF执行日志。此处暂记G2，不能宣称G3/G4/G5。结果预览仍是本Run内存对象，不是正式Artifact持久化；未来需要大图原图缓存/分块内存预算、Studio MOL画布完整端口连接及真正现场设备/HIL。

## 30. V2D-18/19/23/24：Studio从独立节点发现到已连线可编辑的真实视觉工程页（2026-10-29）

- **核实现有Owner**：`MOLFlowCanvas`、`MOCFlowBuilder`、既有`doc.flow`及资源Flow Tab已经支持类型化端口拖线、参数、节点位置、断线和工程保存/重开。本阶段不重写Flow UI或存储，不新增C#工程、Graph执行状态。
- **真实模板创建**：`MainWindow.CreateStudioSeparatedVisionFlow(MEMEngineeringSession,CxVisionThresholdProvider)`先通过MHS Effective固定专业配置和真实MRK版本Resolver预检，再用`MEMEngineeringSession.AddPage`及唯一`MOCFlowBuilder.Edit`创建3块：`CxVisionBinaryThresholdProvider` → `CxVisionMaskRegionsProvider` → `CxVisionAcceptedRegionMeasureProvider`。正式MOL保留2条Control+2条Data连线。阈值`Threshold`和区域`RuleVersion`进入MOL真实Parameters。
- **公共数据合同**：子流程公开`RawPixels:byte[]`、`Width:int`、`Height:int`三个输入和`ThresholdMask`、`Inspection`、`AcceptedRegionPixelArea`三个输出。无隐式RAW路径/设备连接或旁路发布；仍必须由原工程/Host绑定输入以及MRK授权。配置撤销/版本变更在新Page创建前拒绝。
- **Studio真实按钮**：模块中心`Studio.ModuleCenter.CreateVisionFlow`仅在显式读取MHS Effective、当前Project/Scope有效且无运行中流程时可用；创建后使用现有ActivePage、`RefreshScene`和`doc.flow`打开同一MOL画布。其后仍可拖线、改参数和保存。
- **真实测试源码**：`StudioShellSelfTests.VerifyCpuVisionFileClosure`检查3 Block/4 Link/3输入/3输出、MHS锁定值、`ValidateExecutable`、官方MOL Codec序列化往返、既有`MOCFlowBuilder.Move`继续编辑、真工程SaveAs→Close→Open复原；以`MOCProjectRelease.Prepare`编译并用`MRKExecutionEngine.StartFlow`外部绑定5×3 RAW核验真实Mask与3px²，MHS失效后必须拒绝且工程Revision不增加。
- **提交**：[三节点工程生成](https://github.com/oudream/cx-visual-x3/commit/07c4c845b19d4cf4f33f7d5fb0eb2dd557f5dbce)、[模块中心按钮](https://github.com/oudream/cx-visual-x3/commit/8e40f0c976a1bc350429ea52e9e8ec72566e63cf)、[完整MOL与失效测试](https://github.com/oudream/cx-visual-x3/commit/870d6fd2015672be5c17601dc30d5bbe9b974c31)、[磁盘重开测试](https://github.com/oudream/cx-visual-x3/commit/a533267d94b3559f48de802cdb2e618cc8e12459)、[MOC→MRK真执行回归](https://github.com/oudream/cx-visual-x3/commit/4bcf81097fdd7508454165c19589a063aa6e56fe)。
- **严格验收**：上述是已提交**生产源码及真实测试代码**，尚未拿到匹配此批SHA的Windows .NET Framework 4.8构建及S1 CPU STA/Headless运行成功证据；状态暂为G2。仍需真实CI G3/G4/G5；其他工作包括GUI输入绑定编辑便利性、工业大图预算、Artifact长期身份、现场硬件HIL。
