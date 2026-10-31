# CxVisual Content 体系 #5——数据、配置、资源与跨方面协同 V1.0

> 目标合同；覆盖 CNT-R43～R54。依照平台公共能力总目录 V1.0、数据与交互体系、配置体系 V2.4、运行时数据体系 V3.0；本文件只规定 Content 怎样**声明和使用**，不重复实现 MDS/MHS/MNM/命令中心/资源租约。

## 01. 四类不能混淆的值

| 类型 | 定义中保存 | Runtime 所属 | 注意 |
|---|---|---|---|
| Property/参数 | Schema、Default、Binding、公开可覆盖策略 | 使用时解析值 | 改默认值不等于实时设备生效 |
| Variable/Observation | VariableRef、DataBinding、质量/时效策略 | MDS/变量中心、专业数据来源 | 实时值和旧值不进工程 |
| Flow Data | Input/Output/Type/Scope/Handle 声明 | MDS Frame/Heap/Stack、专业 Handle Owner | Pending/Committed/私有值隔离 |
| Config | FCM Schema Ref、工程覆盖、来源、目标 | MHS Saved/Effective、Host/专业应用 | Saved ≠ Effective ≠设备确认 |

不能为了跨专业方便直接给全进程一个 GlobalHeap 并使所有 Content 可写；需要共享时需定义 Owner、Scope、权限、引用寿命、隔离、预算及跨进程协议。

## 02. 数据流生命周期

Declare Type/Port → Bind Source → Validate Type/Unit/Frame/Time/Quality → Resolve actual DataRef → Borrow/Transfer/Copy → Process → Commit/Export → Observe → Release。数据来源、生产者/消费者、有效期和质量是独立维度。Image/Volume/Tensor 使用句柄和元数据，通常不经 WPF 帧帧拷贝；使用后必须按专业数据 Owner 回收引用。

在两个并发 Flow 中，FrameKey/InvocationId、Pending、Committed 独立；同名局部变量不是同一地址。异步旧结果必须核查 Run Generation/SourceId 后才能投影当前图像。

## 03. 配置的五级语义

1. Type/Provider Default：FCM 或 MPM 定义的类型与默认值。
2. Shared Definition：Content 复用定义内可配置引用及公开成员。
3. Instance Override：MRIResourceReference 中属于某次使用位置的公开字段覆盖。
4. Saved/Approved：MHS 保存、审批和修订记录；可能尚未生效。
5. Effective/Runtime Snapshot：Host/专业真正采用的有效配置、设备/模型绑定与版本 Pin。

求值优先级必须由正式 FCM/MHS/MPM 各 Owner 明确，不能随意声称“实例值总是覆盖安全设置”；安全强制项需要策略与权限验证，设备确认还要另行回读。面板应分别展示 Default/Override/Saved/Effective/Observed 与差异原因。

## 04. 资源与依赖分类

| 资源类别 | 应保存的 Content 声明 | 真正 Owner / 运行期行为 |
|---|---|---|
| Symbol/子 Content/Template | DefId、RefId、版本/来源、公开映射 | MEM/MTI/Resource |
| Capability/Module | CapabilityId/ModuleId、Version、端口合同 | MFM/MOC/专业 Provider |
| Camera/Detector/PLC/Robot | Asset/TargetRef、安全与配置要求 | Host/Driver/Automation 设备租约 |
| Image/Volume/ROI/Model | Asset/Schema、Coordinate/Unit/Handle规则 | Foundation Imaging/Volume、Vision/CT、MDS |
| 外部文件/模型/校准 | 受控版本/哈希/URI/授权信息 | 专业/文件与制品 Owner |
| Message/Command/Service | 类型、Topic/Target、权限、请求应答协议 | MNM/Command/Services/Host |

依赖需要检测存在性、版本、信任、能否静态匹配、真正 Ready 及生效。资源锁和租约不可由简单 XML Reference 伪造；本地与跨进程/跨 PC 的能力等级分别验收。

## 05. 跨方面协同事件闭环

HMI Motor 示例：

~~~mermaid
sequenceDiagram
  actor U as 操作员
  participant S as Symbol Instance
  participant C as Content Contract
  participant H as Authorized Host
  participant D as Professional Driver
  participant M as MDS/Trace
  U->>S: Start Click
  S->>C: StartRequested(RequestId/InstancePath)
  C->>H: Command + verified target/config
  H->>H: permission/interlock/resource/idempotency
  alt allowed
    H->>D: Execute
    D-->>H: Accepted or actual result/Unknown
  else rejected
    H-->>M: Rejected/Reason
  end
  H-->>M: Operation/Trace/observed state
  M-->>S: quality-bound status projection
~~~

“用户点击成功”、“请求被接受”、“设备已执行”、“回读已确认”是不同事实；Bad/Stale/Unknown 必须退化显示。撤销 Studio 中的事件绑定不能撤销已经执行的设备动作。

## 06. 质量、坐标、时基与专业约束

Vision：ImageSourceId/RunId、ROI 的 World/Image/Pixel 坐标、标定与像素格式；CT3D：VolumeRef/Spacing/Origin/WorldToVoxel/TransferFunction 及 GPU 预算；Automation：点表、采样周期、实时互锁、协议确认；Inspection：产品 SN、判定版本、缺陷来源、复判审计；Simulation：Virtual/Real/Replay 的源与时钟。缺失转换必须由明确 Adapter/专业能力提出，不得静默降级类型为 object、无单位数字或没有来源的图像。

## 07. Collaboration 的作用域

Content 可持有显式 Connection、Topic、Command Schema、ServiceRef、ProviderRef 与 Resource Contract，但实际连接生命周期属于对应 Owner。Message 是信息投递、Command 有执行/副作用语义、Variable 是状态观测、Flow 有推进与结果事务；不能用一个“OnChanged”回调假充全部工业业务。

跨 Project/Host/PC 的 Content 并非“拿到引用就能调用”。需要远端发现、版本、认证、网络、超时/重试幂等、数据传输、权限与故障恢复，若未完成应显示不可用/Unknown，禁止暗中退回本地 Mock 后宣称远端通。

## 08. 故障及回收规则

- 取消时仅停止可合作的任务；外部设备动作是否已执行必须独立确认。
- 大数据被多个子 Content 共享时，最后使用方/原 Owner 的寿命管理遵守租约与预算，不借共享 Parent 直接 Dispose。
- Run 结束不自动关闭 Project Shared Device/长期 Service；Studio 关闭视图需要释放 UI 订阅，不得中断生产 Host。
- MHS Saved Revision 变更不改在途 Flow 的 Effective Config Pin。
- 跨进程断线后 UI 只读/Unknown，重连需核查 Source/Run Generation，不直接覆盖新值。

## 09. 静态到实际的证据等级

Schema Known → Binding Valid → Resource Resolved → Config Effective → Provider Ready → Authorized → Real Operation Confirmed。每层可记录原因、版本、时间戳及信息来源；不通过 Decision 静态规划就假装设备获授权。

## 10. 验收清单（CNT-T43～T54）

不同 Override 不污染 Definition、配置 Saved/Effective 不混、无授权命令不执行、坏质量退化、Image/Volume 租约过期拒绝、错误坐标/时基拒绝、双 Flow Frame 隔离、消息与命令语义不混、跨 PC 断线 Unknown、取消后设备实际状态核对、运行资源清算、历史配置与新计划互不污染。每项同时记录软件正反例与真实专业 HIL 是否可用。
