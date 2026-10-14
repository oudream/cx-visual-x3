# CxVisual 平台灵魂与架构宪法

**版本：V1.0-Review**  
**日期：2026-09-17**

> 角色：CxVisual 最高级架构约束与评审基线。

## 0. 文档定位与使用方式

本文件不是功能清单，也不是某个模块的详细设计，而是 CxVisual 的最高级架构约束。它回答：平台是什么、什么应进入 Common、什么必须留在专业 Workbench、资源如何被建模、运行如何被隔离、跨域如何交互、如何验证设计、以及什么情况下允许修改平台根基。

规范词：**必须（MUST）** 表示架构红线；**应该（SHOULD）** 表示默认决策，偏离需要 ADR 说明；**可以（MAY）** 表示允许但不强制；**禁止（MUST NOT）** 表示任何项目不得绕开。

当前状态为 Review 基线：用于重新审查并反向约束 Common、Automation、Simulation、HMI、Vision、CT3D、Inspection 八本文档。若详细设计与本文件冲突，以本文件为优先，除非先通过 ADR 修改宪法。

## 1. 平台灵魂：CxVisual 到底是什么

CxVisual 不是“把很多工业软件放在一个壳里”，也不是“一个万能 Graph Runtime”。

**正式定义：** CxVisual 是一个以 **Versioned Resource Graph** 为工程核心、以 **Capability** 为扩展机制、以 **专业 Workbench** 为用户入口、以 **多种专业 Runtime** 为执行引擎、以 **Simulation / Diagnostics / Case Regression** 为质量闭环的统一工业工程与运行平台。

工程师面对的是资源、引用、规则、流程、页面、算法、产品与场景；Runtime 面对的是解析后的资源、Execution Plan、Provider、Worker、资源租约和运行上下文。Studio 负责“工程定义”，Runtime 负责“执行已发布定义”。

平台目标不是让所有行业对象都进入核心，而是让新的工业场景可以优先通过 **配置 → 表达式 → 规则 → Graph/Workflow → Script → Plugin/Native** 逐级解决，仅在长期重复且具有独立专业语义时扩展一级能力。

## 2. 一级产品结构：1 Platform + 6 Workbenches

CxVisual 一级结构固定为：

- **Common / Platform**：全平台机制与稳定契约。
- **Automation**：机器怎么连、怎么动、怎么采、怎么测；吸纳 PLC/ICS/SCADA/IoT、Motion、Robot、IO、Camera/Detector/X-Ray、DAQ/Test/Measurement/Trigger 等。
- **Simulation**：Virtual Device、Replay、Simulation Clock、Fault Injection、Hybrid Simulation、Virtual Commissioning、Digital Twin、Scenario Test。
- **HMI**：Page、Symbol、Binding、State、Animation、Action、Dashboard、Trend、专业 Viewer 与 Designer/Runtime。
- **Vision**：2D Image、ROI、传统视觉、AI、定位、测量、Calibration、Typed Vision Pipeline。
- **CT3D**：Projection、Geometry、Calibration、Reconstruction、Volume、3D Analysis 与专业 Viewer。
- **Inspection**：Product、Station/Point、InspectionPlan/Flow、Judge、Recheck、Review、Result、Trace、Recovery、MES 等产品级质量业务。

“Workbench”是用户的专业工作空间，不等同于代码包，也不等同于行业名词。SCADA、IoT、Robot、Motion、Manufacturing 等不因行业重要就自动成为一级 Workbench。

## 3. 三个正交视角：不要再用一条分类轴解释所有东西

平台必须同时维护三个正交视角：

1. **Application View（用户要做什么）**：Automation / Simulation / HMI / Vision / CT3D / Inspection。
2. **Resource View（工程里有什么）**：PLC、Axis、Robot、Camera、Page、Pipeline、Model、Product、Flow、Rule、Script、Case 等。
3. **Runtime View（运行时怎么执行）**：Control Flow、Event Runtime、Typed DataFlow、Worker、Heavy Job、Durable Workflow、Renderer、Shared Service 等。

例如 Robot：Application 属于 Automation；Resource 包含 Robot/Tool/Frame/Program；Runtime 可能是 Serialized Command Executor + Robot Provider。SCADA 则横跨 Automation（变量/报警/连接）、HMI（趋势/报警视图）、Common（Historian/安全/版本）。

**MUST NOT：** 因为某对象出现在多个场景，就简单提升为一级 Workbench；必须先辨别它属于哪个视角。

## 4. Resource 是工程世界的基本单位

**Resource** 是用户工程中可独立创建、保存、引用、版本化、比较、发布和复用的基本工程制品。

必须区分：

- **Resource**：独立制品，如 Page、AutomationFlow、VisionPipeline、Robot、Recipe、InspectionPlan、Script。
- **EmbeddedObject**：仅属于某 Resource 内部，如 Page 内 Rectangle、Pipeline 内 ThresholdNode、Flow 内 IfNode。
- **ResourceRef**：稳定引用，不通过对象指针或文件路径耦合另一个资源。
- **Descriptor**：平台识别资源类型、编辑器、图标、能力、Schema、Compiler/Runtime/Provider 等的元数据。
- **Artifact / DataHandle**：大数据或运行产物，如 Image、Projection、Volume、Report、Trace Bundle；不得直接塞入普通 Resource JSON。

工程关系应优先通过稳定 ID + Reference 表达，而非把所有对象深度嵌套成一棵巨型对象树。OPC UA 以 Node + Reference 表达对象关系的思想可作为参考，但 CxVisual 不照搬 OPC UA AddressSpace。

## 5. 工程树不是对象树

Studio 必须至少区分两个导航层：

- **Project Resource Explorer**：操作单位是 Resource，例如 Page、Flow、Machine、Connection、Pipeline、Product、Script、Case。
- **Document / Object Structure**：操作单位是当前 Resource 内部的 EmbeddedObject，例如 Page 元素、Graph Node、ROI、Layer、Group。

基础图元、Graph Node 等不因为“可选中”就自动成为 Project Resource。工程树不能退化为控件树，也不能将内部节点全部暴露成顶层文件。

## 6. 工程定义与运行状态严格分离，但生命周期按资源类型选择

原先“Definition 和 Runtime 永远一一对应”的说法过强。正确原则是：

> **Definition 是工程想要什么；Runtime/Runtime State 是现在正在发生什么。两者语义必须分离，但并非每个 Definition 都必须对应一个 XxxRuntime 类。**

资源生命周期至少分为：

- **Value Object**：Point3D、Pose、Transform、Color 等，自身即值，不需要 Runtime/Provider。
- **Definition Only**：DefectDefinition、Theme、部分 ROI/Template 等，运行时直接读取或生成 Result/Resolved Model。
- **Resolved Resource**：需要解析 ResourceRef、Binding、坐标、参数覆盖，形成 Resolved Model。
- **Compiled Resource**：Expression、Rule、AutomationProgram、VisionPipeline、CTPipeline、InspectionFlow 等先 Compile 为 Compiled Artifact / ExecutionPlan。
- **Runtime Resource**：Page、Task、某些长生命周期对象具有独立运行状态。
- **External Runtime Resource**：Axis、Robot、PLC、Camera 等通常形成 Definition + Runtime + Provider。
- **Shared Service**：Historian、Audit、PluginManager 等通常由多个 Definition/Profile 共享一个 Service，不要求一 Definition 一 Runtime。

每个 ResourceDescriptor 应声明自己的 LifecycleKind，以及可选的 Resolver、Compiler、RuntimeFactory、ProviderContract。

## 7. Definition / Resolve / Compile / Runtime / Provider 的统一生命周期模型

平台必须允许以下统一而非僵化的生命周期：

**ResourceDefinition →（Direct Use / Resolve / Compile）→ ResolvedModel / ExecutionPlan →（如需要）Runtime / Service →（如需要）Provider / Adapter**。

设计任何重要对象时必须回答五个问题：
1. 是否需要长期保存？若是，形成 Definition/Resource。
2. 是否需要解析引用、继承、覆盖或生成派生模型？若是，增加 Resolve。
3. 是否属于可执行 Graph/Program/Pipeline？若是，增加 Compile → ExecutionPlan。
4. 是否存在持续变化的运行状态？若是，增加 Runtime/RuntimeState。
5. 是否连接真实硬件、第三方库或外部系统？若是，增加 Provider/Adapter。

**MUST NOT：** 将 Designer 中的可变编辑对象直接拿去生产运行；Runtime 应消费已校验、已解析、必要时已编译的只读/稳定结构。

## 8. Common 只拥有“机制”，不拥有“专业语义”

Common 的判断标准：去掉 Automation/Vision/CT3D/Inspection 等专业名词后，该机制是否仍有完整意义？

Common 可以拥有：Resource/Ref、Descriptor、Action Contract、Command/Event Contract、Expression/Rule Engine、Graph Node/Port/Edge、ExecutionContext、ResourceLease、DataHandle、Security/Audit、Version/Manifest、Plugin/Capability Registry、Diagnostics/Trace、Historian基础、Deployment基础。

Common 不应拥有：Axis、MoveJ、RobotTool、Vision Threshold、FDK、MPR、CTGeometry、Inspection Recheck、Product Judge 等专业语义。

**架构门禁：** 任何功能进入 Common 前必须说明至少两个独立 Workbench 的复用价值，且接口不包含某一 Workbench 的专用数据结构。

## 9. 共用 Graph Model，不共用专业 Runtime 语义

Common 可以提供 GraphDefinition、Node、Port、Edge、SubGraph、Descriptor、Typed Contract、Graph Validation 基础，但各 Workbench 必须拥有独立专业执行语义：

- Automation：Control Flow / Event / Resource / Interlock / StateMachine / Cancellation。
- Vision：Typed DataFlow / Image / Batch / Stream / GPU / Backpressure。
- CT3D：Projection / Heavy Job / Volume / GPU / Worker / Reconstruction。
- Inspection：Durable Workflow / Product / Station / Checkpoint / Idempotency / External Side Effect。
- HMI：Binding / State / Event-Action / Renderer / Navigation。
- Simulation：Virtual Clock / Provider Routing / Replay / Deterministic Scenario。

**MUST NOT：** 因为界面上都是“节点+连线”，就让一个万能 GraphExecutor 解释所有专业流程。

## 10. 跨 Workbench 只能通过稳定契约交互

跨 Workbench 禁止直接持有对方内部实现类或 Vendor SDK。允许的主要“语言”包括：

- ResourceRef：引用谁。
- Action / Command：请求做什么。
- State：当前是什么。
- Event：刚发生什么。
- Stream：连续数据。
- DataHandle：大数据只读/受控句柄。
- Result：专业结果。
- Invocation：调用某专业 Flow/Pipeline/Job。
- ArtifactRef：运行产物引用。

例如 Inspection 调 Vision 应是 `RunVision(VisionPipelineRef, ImageHandle) -> VisionResult`，而不是直接调用 Halcon/OpenCV/TensorRT 类；HMI Button 也不得直接调用 Camera SDK。

## 11. Command / State / Event / Stream 四种语义必须分离

四种通道不可混成 UniversalEventBus：

- **Command**：“请做某件事”，通常要求结果、Timeout、Cancellation、权限、Resource Lease，关键命令不可丢。
- **State**：“现在是什么”，强调 Latest Value / Quality / Timestamp，可 Coalesce。
- **Event**：“刚发生了什么”，不可变，带 EventId/Source/Timestamp/Correlation，可按 QoS 排队或持久化。
- **Stream**：“连续数据”，如 Image/Projection/Signal，必须定义容量、背压、Ordering、Overflow/Retention。

Axis.MoveAbs 是 Command；Axis.Position 是 State；MoveCompleted 是 Event；Camera Frames 是 Stream。

## 12. 逻辑能力必须形成阶梯，而不是一上来就 Script

平台逻辑能力按复杂度和风险递进：

**Configuration → Expression → Rule → Graph/Workflow → Script → Plugin/Native Code**。

默认原则：能配置解决，不写表达式；能表达式/规则解决，不写脚本；能 Graph 表达，不写项目专用代码。Script 是逃生门，Plugin/Native 用于真正需要新算法、驱动、设备 Capability 或性能边界的场景。

Script 不得绕开 Resource/Action/Security/Version/Audit 直接操纵 UI 或 Vendor SDK。

## 13. Capability 优先于深继承

平台扩展应优先通过 Capability 描述“能做什么”，而不是通过巨大继承树猜测类型。

例如 Axis 可声明 IMovable/IHomeable/IStopppable；Robot 可声明 IRobotMotion/IFrameProvider/IToolManager/IIoProvider/ISafeStoppable；Trigger Device 可声明 IHardwareTrigger/IPositionCompare。

Graph Palette、Property、Runtime、Capability Check、Simulation Provider 应根据 Capability 发现能力。项目代码禁止出现大量 `if (device is VendorA/VendorB)` 分支。Vendor 差异必须被 Provider/Adapter 或 Capability Package 吸收。

## 14. 实时边界必须永久清晰

CxVisual Windows Runtime 可以提供 Async、Event、Cyclic、Priority、Deadline、Watchdog 等软实时能力，但不得假装承担硬实时闭环。

**Hard Real-Time**：Servo Loop、EtherCAT Cycle、Position Compare、Robot Trajectory Interpolation、Safety Loop 等必须下沉 PLC / Motion Controller / Robot Controller / FPGA / RTOS。

**Soft Real-Time**：CxVisual Automation Runtime 负责设备级流程、事件、等待、调度、资源、监控和策略。

**Business / Compute / UI**：Inspection / Vision / CT3D / HMI 可拥有独立 Worker/GPU/Job/Renderer，但不得阻塞硬实时控制。

任何需求声称“1ms/确定性/安全级”时，必须在架构评审中标注具体 Controller/RT 边界。

## 15. Runtime 必须脱离 Studio 独立生存

Studio 负责编辑、验证、编译、仿真、发布；Target Runtime 负责加载 Release Package 并自治运行。

生产运行必须满足：Studio 关闭、开发电脑离线、中心服务暂时断开、Remote Session 断开时，现场机台仍能按已发布配置继续运行。云端、管理端、AI助手、报表端不得成为现场生产的单点依赖。

运行时变更必须通过受控 RuntimeOverride/Teach/Recipe/ChangeSet，并具有范围、TTL、权限、Audit 与回写新版本的机制，禁止“现场改了但工程不知道”。

## 16. 发布的是 Resolved Manifest，而不是当前内存状态

所有生产 Task/Run 应能够回答：使用了哪个 Automation Program、Vision Pipeline、Model、CT Geometry/Calibration、Recipe、Inspection Plan、Plugin、Driver、Profile、Schema。

Task/Run 启动时必须 Pin 一个 **ResolvedManifest**，记录资源版本、依赖版本和必要 Hash。结果、Trace、Artifact 必须能回指该 Manifest。

发布流程应是 Definition → Validate → Resolve → Compile → Test Evidence → Release Package → Target Precheck → Stage → Verify → Atomic Switch / Rollback。

## 17. Schema、Migration、Version 是平台生命线

Resource 必须具有稳定 ResourceTypeId、SchemaVersion、ResourceVersion、StableId。加载旧工程时不得静默猜测修复；必须通过显式 MigrationStep，并保留备份和 Migration Log。

插件升级必须声明 ApiVersion/Capability/Schema 兼容性；ExecutionPlan 不应依赖 Designer 私有类。Release 中应有 DependencyManifest，可检查缺失插件、Provider Capability、目标平台版本、设备驱动版本。

## 18. Simulation 不是另一套工程

禁止维护 RealFlow 和 SimulationFlow 两套业务逻辑。正确模式是同一个 Resource/Flow，通过 Provider Router 绑定 Real / Virtual / Replay / Hybrid 实现。

Simulation 必须复用真实 Resource Contract、Action、State/Event/Stream 和结果契约，并提供 SimulationClock、Fault Injection、Replay、Virtual Commissioning、Scenario Test、Golden/Baseline。

如果某真实流程无法在 Virtual/Replay Provider 下运行，优先检查真实设计是否过度耦合 Vendor SDK 或现场全局状态。

## 19. Case 是正式平台制品，不是 examples 目录

CasePackage 同时是需求、示例、模板、Demo、培训、测试、回归和架构验证制品。建议包含 CaseManifest、SceneDefinitions、ResourceRefs、Automation/HMI/Vision/CT3D/Inspection/Simulation 资源、SampleData、FaultScenarios、GoldenTests、ExpectedMetrics、Documentation。

平台能力的晋升顺序应是：项目特例 → 可复用组件/Capability Package → 跨场景公共能力 → 极少数情况下的新 Workbench。

每个重大平台版本必须运行代表性 Case E2E Regression。

## 20. 新增一级 Workbench 的门槛必须很高

只有同时满足以下条件才允许考虑第 7 个 Workbench：

- 用户把它视为独立工程任务；
- 有稳定复杂的专业 Resource 模型；
- 需要独立编辑体验；
- 有现有 Runtime 无法自然承载的独立执行语义；
- 有自己的专业调试模式；
- 已在多个独立场景中重复出现。

Laser、Welding、Dispensing、AGV、SCADA、IoT 等优先作为 Automation Capability / Plugin / Case；Manufacturing 优先作为场景而非一级 Workbench。

## 21. Security、Safety、Audit 必须贯穿执行链

权限、安全与审计不是 HMI 的附加功能，而是从 Action/Command 到 Target Runtime 的全链路机制。

危险动作必须经过：Identity/Role → Mode → Permission → Resource → Interlock/Policy → Provider Capability → Execute → Audit/Trace。

远程工程默认 ReadOnly；写入需要 Target Runtime 最终裁决、TTL、审批/授权、审计。CxVisual 的软件 Interlock 不替代认证安全系统；真正 E-Stop/Safety Loop 必须由安全硬件/安全 PLC 承担。

## 22. Diagnostics 必须是设计的一部分，而不是出问题后加日志

每个核心 Runtime 至少应能回答：当前 State/Task/Node 是什么、为什么等待、资源 Owner 是谁、Queue Depth/Backpressure 是否异常、Deadline/Watchdog 是否超限、哪个 Provider/Worker 不健康、哪个 Result/Artifact 属于哪个 Correlation/Manifest。

统一基础包括 Log、Trace、Metric、Health、Snapshot、CorrelationId、Timeline。专业 Workbench 在此基础上增加专业诊断：Automation WaitGraph/ResourceLease，Vision Frame/Worker/GPU，CT Reconstruction/VolumeIO，Inspection Checkpoint/Idempotency/Recovery，HMI Binding/Viewer FPS。

观测必须有界，禁止 Diagnostics 自身阻塞生产执行路径。

## 23. 性能与大数据必须以 Handle / Queue / Budget 显式建模

Image、Projection、Volume、Mesh、Signal Block 等大对象跨节点/进程应通过 DataHandle/ArtifactRef，不应在对象图中反复深拷贝。

所有连续流必须明确 Capacity、Ordering、Overflow、Backpressure、Retention；所有 GPU/Worker/Station/Instrument 等稀缺资源必须有 Resource Capacity/Lease/Budget。

NoDrop 数据若下游处理能力不足，必须有背压、上游节流或持久化方案；不能通过“无限 Queue”掩盖设计缺陷。

## 24. Station、Geometry、Measurement 等跨域概念必须区分“公共基础”与“专业语义”

案例验证暴露了几个必须进一步收敛的跨域概念：

- **Station**：Automation 的 Physical/Machine Station 与 Inspection 的业务执行 Station 不能由两个 Workbench 同时“拥有”同一个类。建议 PhysicalStation 归 Automation/资源层，InspectionStation 通过 ResourceRef/Binding 指向物理站并增加业务 Capacity/Route 语义。
- **Geometry / Coordinate / Transform**：Point/Vector/Pose/Transform/Frame/Unit 等数学基础可考虑进入 Common.Geometry；RobotTool、CameraCalibration、CTGeometry 仍必须留在专业 Workbench。
- **Measurement**：通用 MeasurementValue（Value/Unit/Quality/Timestamp/Uncertainty/Source）可进入 Common Data Contract；具体 TestPlan、Vision Measurement、CT Measurement3D 留在专业域。
- **Thing/Twin**：Thing/Telemetry/Command 更接近 Automation/Edge Capability；通用 Resource identity/State/Telemetry contract 可在 Common；Digital Twin 的虚拟模型与仿真语义归 Simulation。

这些项在正式编码前需要 ADR 定稿。

## 25. 典型对象生命周期必须允许多种形态

设计不得强制所有对象都长成同一模板。推荐示例：

- Point3D：Value Object。
- DefectDefinition：Definition Only，运行时产生 DefectResult。
- ROISet：Definition → Resolve（如动态坐标）→ VisionRuntime 使用。
- Axis/Robot/Camera：Definition → Runtime → Provider。
- Page：Definition → Resolve Binding → HmiRuntime / Renderer。
- Expression/Rule：Definition → Compile → CompiledExpression/CompiledRule → Engine Evaluate。
- AutomationProgram/VisionPipeline/CTPipeline/InspectionFlow：Definition → Validate/Resolve/Compile → ExecutionPlan → 专业 Runtime。
- HistorianProfile：Definition/Profile → Shared HistorianService → Storage Provider。

这条用于防止“为了架构整齐而过度设计”。

## 26. 代表案例必须用来“攻击”架构，而不是只做演示

当前至少用以下 Case 做架构回归：

1. 普通 PLC + HMI 自动机：State/Mode、受控写、Alarm、Recipe、Wait/Recovery。
2. Robot Cell：Robot/Frame/Tool/Zone、Vision Guidance、Simulation、Teach/Interlock。
3. X-Ray 2D 在线检测：Motion/XRay/Detector/Trigger、Vision、Inspection、NoDrop Stream、节拍。
4. 工业 CT：Automation Scan → ProjectionSeries → CT3D → Inspection，验证 Geometry/Calibration/Heavy Job/Volume。
5. 气密/测试测量：Valve/Sensor/DAQ/Instrument → Measurement → Inspection Judge。
6. SCADA 集中监控：PLC/Variable/Alarm/Historian/Trend，验证数据与HMI边界。
7. IoT Edge：Thing/Broker/StoreForward/Twin/Operations，验证现场自治和断网补传。
8. 多工位检测线：Task/Station Scheduler、Partition、共享资源、MES 幂等、Recovery。
9. Virtual Commissioning / Replay：同一 Flow 在 Real/Virtual/Hybrid/Replay 下运行。
10. Remote Engineering：ReadOnly→Maintenance Session→受控写→Case收集→TTL退出。

每个 Case 必须完整走：工程编辑 → 保存/加载 → Validate/Compile → Runtime → 跨域调用 → Fault/Recovery → Debug → Simulation → Release → Trace/Result。

## 27. 当前架构验证结论与尚未关闭的问题

当前 1 Platform + 6 Workbenches 经上述案例初步验证总体成立，尤其是 Automation→Vision/CT3D→Inspection 的分层自然；SCADA/IoT 能被 Automation + HMI + Common 吸收，无需新增一级 Workbench。

尚未关闭、必须用 ADR 继续定稿的重点：

- PhysicalStation 与 InspectionStation 的归属与绑定模型。
- Common.Geometry 的边界：哪些数学/坐标类型进入 Common，哪些保留专业域。
- 通用 Measurement Contract 与各专业测量结果的边界。
- Thing/Twin 在 Automation、Simulation、Common 之间的最终所有权。
- Cross-Workbench Invocation 的统一接口与同步/异步/Job 语义。
- ResourceDescriptor/LifecycleKind/Compiler/Provider 的最终代码接口。
- ReleaseManifest 对 Driver/Firmware/External Service 版本的追踪粒度。

## 28. 明确禁止的反模式

以下做法一旦出现，应视为架构缺陷：

- Designer 对象直接进入生产 Runtime。
- HMI/Inspection/Script 直接调用 Vendor SDK。
- 所有消息都塞进一个 EventBus。
- 所有 Graph 都由同一个万能 Executor 解释。
- 为每台设备创建独占线程，或用 Task.Run 包装阻塞 API 假装异步。
- 业务代码随处 lock 共享设备，而不是 ResourceLease/ExecutionGroup。
- 用无限 Queue 解决吞吐不匹配。
- 把 Image/Volume 深拷贝通过每个 Node。
- Real 和 Simulation 维护两套 Flow。
- 项目为了赶进度直接修改 Common Kernel，而不先做 Plugin/Capability/ADR。
- 将云端/管理端作为现场机台运行必要依赖。
- 用软件 Interlock 冒充 Safety System。
- 运行版本无法追溯到 Pipeline/Model/Recipe/Calibration/Plugin/Driver。

## 29. 架构变更治理：ADR + Gate

任何改变以下内容的设计必须建立 ADR：一级 Workbench、Common 公共接口、Resource 元模型、Graph Contract、跨 Workbench 契约、ExecutionContext、ResourceLease、DataHandle、ReleaseManifest、Security/Remote、安全实时边界。

每个 ADR 至少记录：Context、Decision、Alternatives、Consequences、Compatibility/Migration、Cases Affected、Test Evidence。

架构 Gate 建议包含：
- 是否把专业语义误放 Common？
- 是否重复定义已有 Resource/Capability？
- 是否能通过 Reference/Action/Invocation 而非直接依赖实现？
- 是否支持 Save/Load/Version/Migration？
- 是否有明确 Runtime/Provider 生命周期？
- 是否支持 Simulation/Replay？
- 是否有 Fault/Recovery 与 Diagnostics？
- 是否能用至少两个 Case 证明通用性？

## 30. 八本文档与本宪法的关系

八本文档应服从同一宪法：

1. Common / Platform：只收公共机制和稳定契约。
2. Automation：物理世界、设备级流程与Automation Execution Kernel。
3. Simulation：Virtual/Replay/Hybrid/Fault/Scenario。
4. HMI：表现、Binding、Action、专业Viewer。
5. Vision：2D Typed DataFlow、模型、Worker、结果。
6. CT3D：Projection/Geometry/Reconstruction/Volume/3D Analysis。
7. Inspection：产品级 Durable Workflow 与质量闭环。
8. 总体架构/场景/案例：解释1+6、Scene、Case、能力归属与演进。

后续详细设计如果发现宪法缺陷，应先通过 Case + ADR 修改本文件，再同步修改受影响文档，避免各本文档自行演化形成冲突。

## 31. 结论：平台要长期守住的十条红线

1. Resource 是工程基本单位，EmbeddedObject 不冒充顶层 Resource。
2. 工程 Definition 与运行状态分离，但生命周期按资源性质选择，不做一刀切 Runtime 类。
3. Common 只拥有机制，不拥有专业语义。
4. 共用 Graph Contract，不共用专业 Runtime 语义。
5. 跨 Workbench 只通过稳定 Reference/Action/State/Event/Stream/DataHandle/Result/Invocation 契约。
6. Capability 优先于 Vendor 分支和深继承。
7. Hard Real-Time 下沉控制器，CxVisual 不越界承诺确定性实时。
8. Studio 与 Runtime 解耦，现场自治；发布的是 Manifest/Package，不是开发内存状态。
9. Simulation 使用同一工程与契约；Case 是正式平台制品和回归资产。
10. 新能力先 Case/Plugin/Capability，只有满足高门槛才修改 Common 或增加一级 Workbench。

## 附录 A：外部参考思想（非照搬）

- OPC UA Part 3 Address Space Model：Node、Attributes、References、TypeDefinition 的建模思想。https://reference.opcfoundation.org/specs/OPC-10000-3/4.4
- CODESYS Task Configuration：Cyclic/Event/Priority/Watchdog 等任务思想。https://content.helpme-codesys.com/
- Beckhoff TwinCAT Real-Time：实时任务、周期、优先级和控制器边界思想。https://infosys.beckhoff.com/
- ROS 2 Executors / Callback Groups：执行器与互斥/可重入回调组思想。https://docs.ros.org/
- Ignition Projects / Modules / System Architectures：工程资源、模块扩展、Gateway/项目部署思想。https://docs.inductiveautomation.com/