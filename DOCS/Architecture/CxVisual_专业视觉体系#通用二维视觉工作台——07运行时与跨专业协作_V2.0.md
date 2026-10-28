# CxVisual 通用二维视觉工作台——07 专业运行、批量实时与跨Workbench协作 V2.0

> 范围V2D-01设备/网络接入、03/04序列大图、16/17专业算法、18—20执行、25跨专业、26诊断。现有基础：MRKExecutionPlan/MRKExecutionStep、MRKExecutionInput、MDSDataContext、F19并发与资源、CxVisionDataFlow真实CPU检测Plan、FLD、MEM/MOC/配置/权限。**统一VisionPipeline生产发布、完整相机/网络帧流及GPU Worker并未由本版验证**；具体运行API要与实际源文件和DOCS/02—04核对。

## 1. 四种不同运行形态及共享合同

| 模式 | 输入与驱动 | 时序/资源语义 | 应复用的机制 |
|---|---|---|---|
| 交互单图 | UI/SDK选择Image+ROI+Params | Latest请求、取消、可视回馈 | MVI请求版本、MRK/MDS可按需要接入 |
| 离线批量 | 图像集/文件序列+已验证Recipe | 有序、并发限额、失败清单和汇总 | MRK Plan/并发策略、MDS数据作用域 |
| 在线实时 | 相机/探测器FrameStream | 明确Capacity、Backpressure/Drop/NoDrop、时限 | Automation Provider + F19/MRK/MDS |
| 专业后台Job | 大图拼接、配准、专业SDK/AI | 长任务、Worker、Checkpoint视需求 | 统一Runtime/Worker机制，不建立独立JobScheduler |

共用算法Operation、参数Schema、类型化输入输出与结构化结果，但**不强迫四种模式拥有相同排队政策、UI刷新率、故障恢复或发布生命周期**。交互式操作失败不得伪作线上业务NG，在线流程需要可定义的错误处理和安全边界。

## 2. MEM/MOC/MRK/MDS各自角色

~~~mermaid
flowchart LR
  D[MEM持久Vision定义] --> C[MOC验证/解析/编译]
  C --> E[MRK ExecutionPlan/Revision]
  I[ImageRef/FrameRef/Params] --> M[MDS DataContext]
  E --> R[MRK Scheduler/ExecutionGroup]
  M --> R
  R --> P[FIM/PVC Operation]
  P --> O[NamedTypedOutputs]
  O --> M
  M --> V[Vision Result/Artifact]
  V --> T[Trace/Inspection/HMI]
~~~

- **MEM**：唯一工程定义/版本和资源引用，制作期可变；不直接调度或执行。
- **MOC**：验证引用、专业参数/端口，解析/编译不可变执行计划及SourceMap；Vision提供专业语义补充，不能复制MOC自身。
- **MRK/F19**：执行阶段、并发、资源/专用线程、取消与策略，不通过WPF事件Loop执行算法。
- **MDS/FDT**：类型化端口和Invocation数据帧、Pending/Commit/Abort、运行数据及大块DataHandle；不建Vision专属Heap/Stack。
- **FLD/诊断**：从请求到节点到结果贯穿CorrelationId与RunId，含资源占用、异常、取消及时间线。
- **Provider**：CPU、GPU、HALCON/OpenCV、相机等在正式专业/设备Owner内，依能力与许可证检查选择，不在通用层硬编码Vendor判断分支。

## 3. 类型化数据流和发布语义

每个Vision节点输入必须声明TypeId、像素格式/通道、ROI坐标空间、可空性和上游来源；输出是具名端口，可为Image、Mask、Geometry、Measurement、FeatureTable或DetectionEvidence。编译期静态检查端口匹配；运行时仍检查动态尺寸、标定、设备/SDK状态和资源预算。结果发布应具有单次Invocation一致性：节点的Pending结果要么按MDS提交规则发布，要么Abort，不允许异常后混合旧数据。中间数据不能因图窗关闭就被不受控清理；UI只持有允许的引用或租约。

## 4. FrameStream、背压与Drop/NoDrop

~~~mermaid
sequenceDiagram
  participant A as Automation采集
  participant Q as 有界Frame队列
  participant R as MRK/Vision执行
  participant D as MDS/Artifact
  participant T as Trace
  A->>Q: FrameId,Timestamp,Lease
  alt 未超容量
    Q->>R: 交付任务/资源租约
    R->>D: Commit TypedOutputs
    R-->>Q: 释放BufferLease
    R->>T: Latency/QueueDepth/ResultStatus
  else 队列满
    Q-->>A: 上游背压或已批准DropPolicy
    Q->>T: Overload/FrameLoss指标
  end
~~~

必须分别声明：
- PreviewLatest：可合并丢弃过期显示帧，但不能影响正式检测事件。
- ProcessAll/NoDrop：必须能在采集端节流或持久化；上游不可阻塞且无磁盘保证时，不可能无条件NoDrop，应报告能力不满足。
- DropOldest/DropNewest：仅对业务允许的数据流使用，记录丢帧数和时段。
- Backpressure/Bounded：队列最大帧数与最大字节数、单帧最坏尺寸、工作线程数、GPU/SDK资源上限；不可只限制条数忽略字节。
- Out-of-Order：同一Stream的FrameId、采集时间、完成时间、发布顺序不同，必须声明处理结果排序政策。
- Cancel/Stop：停止后释放每个Lease，等待中断/拒绝的节点得到状态事件，不假装不可取消同步SDK能强制停止而不隔离进程。

## 5. 版本与运行可复现

运行启动时Pin ResolvedManifest：VisionPipeline/参数Recipe/ROI/Calibration/Model/Provider/Plugin/API版本及必要Hash。RunId/CorrelationId关联输入FrameId/ArtifactRef/配置版本、每个节点的输出及Trace。Studio的编辑事务不直接修改运行中Plan；现场修改应走已有受控Override/Teach/Recipe与权限/有效期/审计。离线回放与现场真实流使用同一个专业定义和Provider Router，不建RealFlow与SimulationFlow两套业务逻辑。

## 6. 跨Workbench职责矩阵

| 方向 | Vision接受/交付 | 唯一业务Owner与防重 |
|---|---|---|
| Automation → Vision | 相机/探测器帧、触发元数据、设备能力与状态 | 设备驱动、Channel/Protocol及采集归Automation/功能模块体系 |
| Vision → Inspection | 具类型DefectCandidates、Measurement及VisionEvidence/ArtifactRef | Product Judge、Recheck、批次追溯、MES归Inspection |
| CT3D ↔ Vision | Projection/Calibration/ROI/Measurement及图像结果引用 | 体重建、投影几何、3D测量归CT3D；Vision不复制FDK |
| HMI ↔ Vision | 命令/状态/结果引用与Viewer投影 | HMI无需Vendor SDK和内部可变Plan |
| Simulation ↔ Vision | 同样ImageFrame/Invocation合同、虚拟采图/Replay | 模拟Provider/Clock不另写算法 |
| HALCON/OpenCV/AI | 专业Provider输入输出与能力检验 | Professional拥有SDK适配；FIM只收算法独立的数学 |

跨专业传递ResourceRef、Result、State、Event、Stream、Invocation和ArtifactRef；Command/State/Event/Stream必须分语义，避免UI按钮直接操纵驱动或共享对象指针。所有对象Version/Schema/Permission与超时、取消语义写入合同。

## 7. 可靠性、隔离与安全故障策略

| 故障 | 必须的操作和痕迹 |
|---|---|
| 帧格式损坏/超预算 | 在入队/执行前拒绝；保留SourceId与错误码 |
| 算法异常/超时 | 节点失败、Abort Pending、报告Trace，不污染有效结果 |
| SDK阻塞/不可中断 | 专用线程/Worker隔离；如无法安全中断，按故障策略释放外层等待，不声称强制杀线程 |
| GPU不可用/显存不足 | 支持降级的算法显式Fallback并记录结果差异；不支持则拒绝 |
| 中途切换工程Revision | 进行中Run继续Pin版本，新的Run按发布策略使用新版本 |
| 采集失联/断网 | Provider状态/重连与可恢复范围明确，已完成证据不丢失 |
| 资源Lease遗失/过期 | 资源状态诊断与隔离，不能无限挂起或由UI私自释放 |
| 权限/安全互锁 | 命令/调用从Studio到目标Runtime都执行相应授权；安全闭环由PLC/控制器负责 |

## 8. 工业性能与观测项

至少观测Decode、QueueWait、Preprocess、Algorithm、Postprocess、ResultPublish、Display延迟；CPU/GPU时长、峰值内存/显存、队列字节/帧数、Drop/Retry/Cancel比率和产物存储耗时。目标吞吐由客户机台条件、图像规格、并发预算测试后给出，并区分预览FPS与真实检测吞吐；不以合成4×4图像测试推断4300²实况性能。

## 9. D07验收

| ID | 证明目标 |
|---|---|
| D07-01 | 已有CxVisionDataFlow在MRK/MDS执行真实CPU算法、输出类型及正确异常 |
| D07-02 | Vision专业节点/参数合同类型错误、缺Provider、资源超限拒绝 |
| D07-03 | 单图/批量/在线三模式调用同一Operation，结果身份一致 |
| D07-04 | 任务版本Pin，参数变化与旧异步结果不污染 |
| D07-05 | 有界帧队列逐字节预算/Drop/NoDrop/顺序/取消压力 |
| D07-06 | 专用SDK Worker/GPU不可用/失联及恢复反证 |
| D07-07 | Simulation回放和现场Provider同合同，对比结果容差 |
| D07-08 | Vision→Inspection、CT3D、HMI稳定引用、进程/版本边界 |
| D07-09 | Studio关闭后Runtime自治运行、发布回滚/安全/追踪 |
| D07-10 | 专业相机、X-Ray、网络/共享内存等真实硬件HIL和许可证 |

不把工具类或架构类图中的部署箭头当成任何已完成的现场验证。