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
