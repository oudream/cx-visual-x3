# CxVisual Content 体系——完整实施工作包、真实源码基线与验收台账 V1.0

> 2026-10-31 初始台账。**这份文档属于未来开发工作合同，不是已完成报告**。基准：DOCS/00 当前 26 工程（F8/M7/P6/应用2/智能1/测试2）；C#7.3/.NET Framework4.8/WPF。实际施工需记录最新 master SHA，跟进并行提交和 CI。需求/行为见 [#0～#9](../Architecture/CxVisual_Content_00_总体架构与完整需求_V1.0.md)，正反例见 [Testing](../Testing/CxVisual_Content_完整测试与验收矩阵_V1.0.md)。

## 00. 代码基线：什么已经存在，不能夸大什么

| 已核查的源码基础 | 可承接 | 不证明 |
|---|---|---|
| MEMProject/Page/Content/Symbol、MRI Ref | Content 已有权威对象、嵌套/引用、公开定义入口 | 所有复杂 Content 制品/实例已可无损迁移 |
| MEMEngineeringSession、MTC、MPM/MPC | 创建、类型化/公共合同、编辑事务 | Studio 一站式能力创作全部完成 |
| MOLLogicDefinition/WiringDefinition、MOCFlowBuilder/BuildService | 逻辑 V1/V2、某些子 Content 调用/端口、构建路径 | 跨Schema/所有分支/完整现场执行均已验收 |
| MOC CapabilityCatalog、MFM、专业 Provider | 静态发现与部分真实能力 | 一次声明/注解/Fluent/跨专业通用模板已发布 |
| MTI 包/ExtractReusable、Impact | 资源转移/提取基础/依赖分析 | 任意场景选区无损升级发布 |
| MDS/MRK/MHS/FCM/MNM、专业 | 数据运行/有效配置/真实交互机制的既有基础 | 设备安全/跨 PC/工业 HIL 全闭环 |
| Studio Content/Symbol 双中心、ResourceDocuments | Dock/三来源/类型创建/包/引用/覆盖/编辑导航 | Content 孵化/八案例全部 UI 闭环 |

“源码存在”“本次 CI 通过”“专业实测”必须独立登记。文档创建本身不会触发或完成 .NET 构建验收。

## 01. D0 基线/Owner/冲突清算（优先）

范围 CNT-R01～R72。读取 DOCS/02～04 每个相关 Sheet，核对 26 工程最新源码与 Symbol V2.0；对 #9 全类映射逐一记录真实路径/签名/增强/候选、不做项；对照八个黄金案例分析实际缺口。出口：完整 Owner/Schema/代码状态矩阵、最新 master HEAD、可复现回归基线、负责人批准的 ADR。不得为了改 UI 先引入 ContentManager。

## 02. D1 纵向黄金样例，优先证伪（G01/G02/G05）

先做纯视觉 Data-only、HMI Motor Event→Host→State 和三级 Inspection 子流程，从同一 MEM/MPM/MOL 模型贯穿 Studio/SDK→Decision/Runtime→TestCenter；暴露真实编译/实例/权限/保存缺口。出口：每例正证/负例、Run/Trace/InstancePath、保存重开和固定 SHA Windows 自动测试；没有硬件只报 E2/E3。

## 03. D2 创建/提取/模板/资源复用（R01～R12）

补齐空白/类型化/模板/共享资源/能力装配/外部包/选区提取的**统一 Preview→Validate→Commit**；利用 MTC/MTI 的 IdMap、AspectRemapper、引用分析与 Undo。出口：任意转换不可证明无损时精确拒绝；外链/Port/Binding/Logic 不丢；单事务可撤销；双实例定义隔离、跨项目包往返。不得另造 ResourceStore。

## 04. D3 能力/公开合同/四方面组合（R13～R30）

提供能力目录只读联动、内部能力装配和公共 Port/Property/Event/Capability 关联、四 Aspect 同源编辑/检查、专业 Viewer 插槽；优先增强 MPM/MPC/MOC 现有类。出口：无 Visual/无 Logic 合法，公开私有检查、单位坐标不匹配拒绝、Group/Symbol/Content 合法引用、能力已声明但不可运行显示 Unknown。

## 05. D4 MOL/Decision/MRK 真流程（R31～R42）

对照已有 schema1/schema2、InvokeContent、FlowInputs/Outputs、SourceMap、Frame/Export、异常/循环/并行/Wait/State、跨Schema 断点；补真实反证而不是“搭新的 Graph”。出口：三级调用、同 Content 两引用/同 Plan 并发、多轮 Loop、分支/异常/取消、结果 Commit 和调用路径追踪通过固定 SHA Windows；跨Schema不支持范围明示/拒绝。

## 06. D5 MDS/MHS/专业资源与工业安全（R43～R54）

完整 Typed DataBinding、质量/坐标/时基、Image/Volume Handle/预算、Instance Override、Saved/Effective、消息命令/设备 Target、Real/Virtual/Replay隔离、跨进程断连与旧回调丢弃。出口：坏质量、缺授权、失效租约、旧 Run、未知设备执行/取消均正确退化；实机安全需 E4 另报。

## 07. D6 Studio 与专业工作台（R55～R66）

在既有 AvalonDock ResourceDocuments 与共享 MPW 控件中完善 Overview/Outline/Visual/Logic/Capability/Data/Collaboration/Config/Testing，完成创作/导航/Inspector/Tooltip/诊断/Undo/键盘/DPI/多屏。六专业给出真实 Provider/Viewer/配置映射。出口：一条自然任务无需手写 JSON，Studio 与 SDK 保存同一 Content/Flow，UIA 真实走查，六专业重要路径至少 E2。

## 08. D7 综合发布/性能/兼容/验收（R67～R72）

打通 G07/G08 资源升级、受控发布/安全点、Trace/SourceMap、并发与设备异常、跨PC/多 Host、资源预算和稳定性；压测按真实负载/硬件记录，不先拍阈值。出口：固定 SHA 的统一 CI+UIA，旧 Symbol/工程/包兼容，专业 HIL 单列，所有高危负例持续保留。

## 09. 分阶段依赖与并行接口

D0 → D1（确定真实缺口）→ D2/D3（可按 Owner 并行，接口先对齐）→ D4/D5（共享公共 Port/Schema，不互改真源）→ D6 → D7。每个工作包内循环：设计对照→小功能的正反测试→实现→构建→回归→同 SHA CI→验收记录→下一项。并行期间每次提交前重读 master 并检查 DOCS/00/ADR 和相关对象，不能凭旧代码签名写“完成”。

## 10. 任务台账字段（后续每项必须填）

| ID/范围 | 当前状态 | 主责 | 交付证据 | 关闭条件 |
|---|---|---|---|---|
| D0 规格/对象审计 | 待执行 | Core/架构 Owner | 逐 Sheet 溯源、真实源码记录 | 负责人批准关键 ADR |
| D1 黄金纵向验证 | 待执行（既有局部基础） | MEM+Decision+MRK+专业 | G01/G02/G05 保存/构建/运行/反例 | E2+E3 固定 SHA，HIL 另列 |
| D2 提取/复用 | 待执行（已有 MTI 部分能力） | Core/MTI/Studio | IdMap/Aspect/Undo/Package | 无损或精确拒绝 |
| D3 能力与契约 | 待执行（已有 MPM/MOC） | Core/Decision/专业 | 双来源/公开/隔离 | 需求 R13～R30 正反例 |
| D4 真编排 | 待执行（已有 MOL/MOC/MRK） | Core/Decision/Runtime | 子调用/SourceMap/Trace | R31～R42 全部软件反证 |
| D5 数据资源 | 待执行（已有各Owner机制） | MDS/MHS/Host/专业 | 质量/租约/授权/配置证据 | R43～R54 隔离与失败 |
| D6 Studio/六专业 | 待执行（双中心已有基础） | Studio/MPW/专业 | UIA/同源/真实Provider | R55～R66/E3 |
| D7 交付 | 待执行 | 相关Owner/测试 | CI/性能/跨PC/HIL | R67～R72 证据分级 |

初始“待执行”指本 **Content 专项工作包** 未正式实施/验收，不表示表中已存在的具体源码为零。不得将其它专题历史通过的同名能力自动归档为本轮通过。

## 11. 每次实施验收记录模板

日期/Branch/Parent SHA/Commit SHA/工作包/需求ID/测试ID/源码文件/变更动机/类与 Owner 差异/实现说明/被拒绝的替代方案/测试命令/输入及实际输出/失败反例/Windows Actions URL及结论/UIA或设备环境/性能基线/已知限制/剩余事项/下一步。可追溯证据必须直接链接有效 Commit 和测试，避免把编辑日志当作编译成功。
