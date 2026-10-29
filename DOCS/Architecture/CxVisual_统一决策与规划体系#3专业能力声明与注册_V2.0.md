# CxVisual Decision——#3 专业能力声明、注册及轻量接入 V2.0

> **目标设计，不代表Attribute/注册简写已实现**。本文指导 Vision/CT3D/Automation/Inspection/HMI/Simulation 专业开发者以最少重复代码获得编排、运行、观察与Decision辅助。唯一模块分类仍是 MFM Connector/Driver/Compute/Simulation/Service，遵循[功能模块总纲](CxVisual_统一功能模块体系%230总体架构——完整设计与验收_V2.0.md)。Constraint/Policy语义归[#2](CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md)，API签名归[#1](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)。

## 01. 开发者应完成的三件事

1. **实现专业功能**：普通C#同步/异步函数、有状态Provider或设备驱动，逻辑与框架解耦可单独测试；不因进入平台改变算法核心。
2. **声明最少必要事实**：身份/版本/输入输出/参数/类型/效果、必要时约束、资源、幂等/安全或模拟模型；能够从C#类型/FCM Schema可靠推导的不要重复手填。
3. **注册一次**：用标准能力注册入口将专业描述和实际执行者关联；Core/MFM/MPC/MOC消费同一只读目录投影，Decision只读快照做判断，MRK/Host验证版本后执行。**不建立DecisionCapabilityCatalog或第二个端口库**。

~~~mermaid
flowchart LR
  A["普通方法/有状态Provider/设备/仿真模型"] --> B["声明：注解 / Fluent / 显式Descriptor"]
  B --> C["统一Capability注册与签名校验"]
  C --> M["MFM模块/能力 + MPC端口/FCM配置真源"]
  M --> D["Decision只读快照：过滤、约束、建议"]
  M --> E["Runtime Resolver：实际调用/授权"]
  D --> S["Studio/SDK：可选、可编排、可说明"]
  E --> T["MRK/MDS：执行/Trace/结果"]
  T -.->|"证据"| D
~~~

## 02. 四个渐进层级：不应要求人人编写全面元数据

| 等级 | 由开发者提供 | 平台允许提供 | 若未提供信息 |
|---|---|---|---|
| L0 纯方法 | 输入、参数、返回值 | 直接函数调用/专业单测 | 不自动发布到统一目录 |
| L1 可编排能力 | Stable CapabilityId、版本、CLR/FDT端口、描述、注册执行者 | MOC发现、画布节点/基本端口检查、MRK运行/默认Trace | 未知形状/安全不自动匹配 |
| L2 工程辅助 | 单位/图像格式/形状/可空/范围、FCM Schema、纯规则、资源/副作用 | Symbol/Content/Capability筛选、引导、构建验证、策略检查 | 无专业事实的项显示Unknown |
| L3 高级决策 | 实测模型/预算估算/质量证据、虚拟模型、可重试条件/失效策略 | 比较/优化、资源规划、场景推演、虚拟验证 | 不假装具备自动仿真或最优选择 |

L0→L1应是**可选发布**，不是所有数学函数都变模块；L2/L3按专业真实需要扩展，不在属性上堆砌几十个必填项。

## 03. 优先复用真实代码（2026-10-29取样）

| 当前真实类/API | 作用与不足 | 设计改造方向 |
|---|---|---|
| `FCMConfigSectionAttribute/FCMConfigItemAttribute` + `Range` / `FCMConfigValidator` | 已有自动Schema/验证，Vision/CT参数使用 | 参数/单位/依赖只读映射到Decision；不在新Attribute重复 |
| `MFMModuleDescriptor`、`MFMModuleKind` | 模块Id/Kind/Version/CapabilityIds | 扩展元数据和能力只读关联，普通函数可无模块实例 |
| `MOCCapabilityDescriptor/IMOCCapabilityProvider` | Id/Name/Category/Version/MPC Ports 的发现 | 补充TypeId、输入输出/资源/副作用语义投影，继续唯一目录 |
| `IMRKCapabilityResolver` / `IMRKVersionedCapabilityResolver` | 真实Delegate及版本解析 | 从纯静态Descriptor拆真实执行绑定；不让Decision依赖MRK |
| `CxVisionThresholdProvider` | 同时实现 MOC描述与MRK resolver，已具双通路 | 用于首个“最小注册”保真黄金样例 |
| `PVCComputeModuleConfiguration`、`CxCtReconstructionOptions` | 已有资源/重建设置与校验 | FCM继续唯一配置，不能重复生成参数数据库 |
| `PSMSimulationModule` | 仿真模型/时钟原型，MRK决定何时推进 | 通过兼容业务能力契约提供受控Virtual/Replay |

**切记**：`RuntimeAvailable` 在当前 MOC 目录中仅说明 Resolver 能解析；不能当作设备 Ready/Supported/本次 Authorized。Automation 旧 `Porting/PACModbusTcpDriver.cs` 标明未包含项目，不能作为正式Driver已运行证据。

## 04. 注解、Fluent和显式Descriptor的职责差别

- **注解**：适用普通静态方法、简短用途/分类/纯类型合同；标识和配置描述可缓存反射结果，不扫描未知第三方程序集自动执行。
- **Fluent注册**：适用委托、复杂异步/泛型/生命周期、运行时实例、自定义资源/后端；显式绑定Provider实例和配置生效Revision。
- **显式Descriptor + Provider**：适用Native SDK、可变端口/重载、跨进程/设备、仿真替代、多能力有状态模块；性能/权限和资源需要能力专属合同。
- **冲突处理**：三种输入只可归一为**一个**批准后的CapabilityDescriptor及版本指纹；同Id不同Schema不静默覆盖，注册重复明确报错。FCM参数定义是唯一来源，能力端口由MPC合同维护，不在三种形式各存一份可写Schema。

### 拟议的最小注解例（**不是现有SDK**）

~~~csharp
// Proposed: 不要求用户继承Decision或MRK类
[CxCapability("vision.defect.detect", Version = "1.0", Name = "缺陷检测")]
public Task<DefectResult> DetectAsync(
    [CxPort(Format = "Gray16")] FIMImageBuffer image,
    DetectOptions options, CancellationToken cancellation)
{
    return _algorithm.DetectAsync(image, options, cancellation);
}
~~~

~~~csharp
// 已有真实的FCM风格，可与拟议注解互补
[FCMConfigSection("Vision.Detect")]
public sealed class DetectOptions
{
    [FCMConfigItem("阈值")]
    [Range(0, 65535)]
    public int Threshold { get; set; }
}
~~~

~~~csharp
// Proposed：简写注册，不是目前已有API
registry.AddCapability<DefectDetector>();
// 或手工：registry.Add("vision.defect.detect", descriptor, provider);
~~~

**自动推导仅限可靠信息**：CLR基本类型、Task/取消签名、FCM范围和描述；物理单位、ROI坐标、像素位深/Stride、所有权/借用规则、数据时限、DeviceId、真实精度、幂等、副作用、显存需求、线程亲和，必须由Owner显式给出或可验证模型提供，缺失时Decision返回Unknown。

## 05. 五类模块与六大专业的正交关联

| ModuleKind | 典型专业使用 | 必须额外考虑 |
|---|---|---|
| Compute | Vision/CT3D/Inspection/AI/基础计算 | 强类型数据、GPU/CPU、资源/模型生命周期 |
| Connector | Automation/数据通讯/设备接入 | Channel/Protocol、网络断线、数据质量/重连 |
| Driver | PLC/相机/运动/探测器 | 设备句柄、线程亲和、排他、联锁和授权 |
| Simulation | 虚拟PLC/视觉场景/机器人模型 | 虚拟时钟、种子、Scenario、Real/Virtual隔离 |
| Service | HMI业务、Inspection报告、长服务 | 对外能力、周期/依赖/故障与权限 |

HMI的 MEMSymbol 不等于 MFM 模块实例；将 Symbol 放入页面后是工程引用/绑定动作，不能误当成执行计算。一个模块可发布多个Capability；一个可直接调用的函数可无模块实例。

## 06. 标准化描述合同（专业不用自己维护多套模型）

| 段 | 必需/可选 | 平台消费 |
|---|---|---|
| Identity | CapabilityId/Version/Name/Category/Origin、SchemaHash 必需 | MOC索引、变更检测、帮助说明 |
| Port | Name/Direction/TypeId/必填 必需；Shape/Format/Unit/Coordinate/Ownership 视专业 | Core连接/MOL语义、Decision筛选/MDS数据校验 |
| Params | FCM Section/Schema/ApplyPolicy/Default及Validator 可选 | 配置表单、Save/Effective区别、参数引导 |
| Trait | Pure/Stateful/SideEffects/Idempotent/ThreadSafe/Cancelable/Isolated 可选但危险动作强制 | D02硬限制、D06策略、MRK绑定 |
| Resource | ExclusiveGroup/MemoryModel/GpuModel/Concurrency/LatencyEvidence 可选 | D07/D08估算（缺证据标Unknown） |
| Recovery | RetryEligibility/Compensation/Reconciliation/FailoverConditions 可选 | D06建议与Host恢复门禁 |
| Simulation | CompatibleCapabilityId/ModelVersion/Scenario/Clock/Seed/Faults 可选 | D09虚拟验证，只在能力匹配时应用 |
| Diagnostics | StableErrorCode/SourceIdentity/ResultProvenance/Artifacts 可选 | Debug/Trace/结果定位与反馈 |

运行端需要强类型输入输出、明确数据租约和进程边界；对 GB 级 FIM/FVO 仅传受控引用/句柄及尺寸元数据，禁止为了Decision匹配复制全量数据。版本化 Schema 生效由 Core/FCM/MHS/Host 唯一管理。

## 07. 注册后的自动消费链（实际使用路径）

1. 注册完成：校验身份/版本/端口/参数Schema一致，重复ID冲突拒绝；给出可读失败原因。注册成功**不代表生产就绪**。
2. Studio搜能力：MOC统一索引返回 Described；Decision按当前端口/场景/资源/目标给每个候选 Applicable/Rejected/Unknown 与解释，用户可展示被过滤项。
3. 插入节点：Core执行编辑事务，使用MOL/MPC端口，支持Undo/Redo和Revision更新；不存在第二份持久Graph。
4. 试运行：Host在开发/模拟模式配置合适Provider，MRK统一执行，MDS显式具名数据结果，Trace/SourceMap显示状态；生产模式需新的授权和安全检查。
5. 故障诊断：依据专业错误码及MRK实际状态定位端口/BlockId/InvocationPath，Decision只能分析有证据的替代与修复。
6. 评估优化：D03/D04/D07/D08在专业提供真实性能/质量数据后才推荐候选；否则明确“待验证”，不用默认分数伪造能力。

## 08. 对具体专业要提供的最少补充

- **Vision/AI**：至少图像/Mask/ROI/类型/位深/坐标、模型Hash/参数Revision、结构化结果与引用；可选精度/耗时测量、虚拟图像流模型。
- **CT3D**：至少Projection/Volume Shape、spacing/单位、几何一致性、分块可行性和计算设备后端；可选耗时与峰值内存模型。
- **Automation**：设备命令是否有副作用、可重试/确认逻辑、权限/安全前置、独占设备组；没有安全描述不得自动发布危险控制节点。
- **Inspection**：判定输入、阈值及质量规则、产品/批次/检验身份、结果证据；NG命令不应由推荐器直接执行。
- **HMI**：MEMSymbol公开的属性/端口/绑定及质量/权限，不能将任意设备写入作为普通Button的无条件Action。
- **Simulation**：虚拟能力契约、时钟推进、状态空间、故障输入、可观测输出、模型适用范围；不能用代码反射凭空得到高保真物理模拟。

## 09. 开发者验收红线（REG）

| ID | 验收操作 | 最低通过条件 |
|---|---|---|
| REG-01 | 普通纯函数直接调用 | 不依赖Core/Decision/Studio/Runtime初始化 |
| REG-02 | 一个能力一次显式注册 | Studio和SDK查询到同一CapabilityId/版本/端口签名 |
| REG-03 | 注册重复/Schema冲突 | 诊断拒绝，不以最后注册者覆盖 |
| REG-04 | 参数带FCM范围/单位 | 表单、API验证与Decision适用规则一致，Save与Effective清楚 |
| REG-05 | Gray16输入/Gray8候选 | 过滤并给真实格式原因，不能无说明强转 |
| REG-06 | 同一Provider执行委托 | MRK真实调用且SourceMap/具名输出与诊断可读 |
| REG-07 | 设备无现场授权/缺模型 | Declared≠Resolved≠Authorized，Unsafe不得放行 |
| REG-08 | Simulation切换 | 同一业务接口可替代时版本/场景一致，真实命令调用次数=0 |
| REG-09 | 新增另一个专业能力 | 不要求修改Studio工具箱里的硬编码专业类型分支 |
| REG-10 | 兼容旧 `CxVisionThresholdProvider` | 不丢现有MOC目录/执行ID，测试Old/New行为一致 |

效果度量：新增专业能力所需的**额外声明行数、手动Adapter数量、首次可编排操作数、诊断定位步骤、编译与运行前拦截错误比例**；在固定夹具中对照未接入Decision的实现，不在设计文档写虚构收益百分比。

---

## 10. V2.0审评补充：真正“一次声明、多处使用”的最小切片

**对现状的修正理解**：现有 `MFMModuleDescriptor` 仅持ModuleId/Kind/CapabilityIds；`MOCCapabilityDescriptor` 另定义MPC端口及Version；`MRKResolvedCapability` 持有CLR输入输出与Delegate；`FCMConfigSchemaBuilder`从属性生成配置Schema。当前不能宣称它们已由一次自动注册保证一致。参见[架构审评 ARC-01/02/05](CxVisual_统一决策与规划体系%23V2全类反推与OOP-GoF-AOP架构审评_V1.0.md)。

~~~mermaid
sequenceDiagram
  actor Dev as 专业开发者
  participant Cr as 受控Composition Root/Host
  participant Meta as MFM/MPC/FCM + 能力描述真源
  participant Dec as Decision静态消费
  participant MRK as MRK实际Resolver
  Dev->>Cr: 注解/Fluent/显式Descriptor + Provider绑定（一次注册动作）
  Cr->>Meta: Normalize/Validate ID+Version+Ports+ConfigSchema
  Meta-->>Cr: Immutable Descriptor/Fingerprint
  Cr->>MRK: 注册真实执行绑定（不传入Decision）
  Cr-->>Dec: 只读CapabilitySnapshot
  Dec-->>Dev: 可见性/适用性/约束理由
  MRK-->>Dev: 仅授权后Run/Result/Trace
~~~

**四个硬门禁**：
1. Attribute、Fluent、Descriptor只是不同的作者输入，优先合并已有FCM/MPC合同，不能三份Schema相互覆盖；同Id/Version但端口不一致必须诊断拒绝。
2. 必需对齐：`CapabilityId/Version/SchemaFingerprint`、端口MemberId/Direction/Required/TypeId、CLR `Type` 映射、具名Data.Return输出、Effective配置Revision、线程/数据所有权与专业副作用。未知单位/格式/设备安全条件必须显式Unknown。
3. 只读目录支持**离线发现**，无需在查询候选时调用Runtime Resolver；Runtime绑定独立由Host装配并做真实权限、资源、版本复核。`RuntimeAvailable`仅代表旧Resolver成功，不能当已授权。
4. L0无需注册；L1最低元数据获得发现/编排与MRK基本运行；L2明确约束/专业参数得到引导；L3必须提供测量/模型才可比较、推演、优化。不能用注解自动宣称真实仿真或AI模型质量。

**最小可验证实施切片**：以真实 `CxVisionThresholdProvider` 作为旧黄金，增加一个第二专业（例如CT3D纯CPU、Automation虚拟设备），用同一个规范化描述完成 Headless/Studio 的端口发现、一次连线合法性、MRK执行、SourceMap定位。要求目录和执行的端口/类型/版本一致，且没有新Decision→MRK编译依赖。新增专业不得修改Studio内置专业类型Switch。详见REV-02/03。


---

## 11. 首批实际可用：MOC离线目录与运行可用性解耦（部分实现）

**源码实装**：保留原`MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider>, IMRKCapabilityResolver)`，增加只接收providers的重载；同一个原有`MOCCapabilityDescriptor`可以在**无Resolver实例**的编辑/SDK阶段被枚举和搜索。目录Item新加`AvailabilityKnown`，明确区分“没有检查”与“检查过且不可解析”；不以`RuntimeAvailable`冒充设备Ready/Authorized。新增`MDCDecisionService`消费原目录/原MPCPortDescriptor，未建第二Registry。

**仍未完成**：Attribute/Fluent自动发布、FCM Schema自动归一、MPC TypeId↔MRK CLR精确映射/SchemaFingerprint、完整专业资源/安全/仿真模型、Runtime纯绑定接口、六专业实际接入；这些继续标为设计/待开发。首批过渡代码位于Orchestration程序集，不代表最终项目已迁移。具体API见[#1 §10](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)。


## 12. 一次声明的签名一致性安全门禁（阶段性实现）

为已存在的`MOCCapabilityDescriptor`与`MRKResolvedCapability`添加`MOCBindingContractInspector`显式一致性检查：静态MPC端口标识、必填与CLR类型不一致时报告结构化错误，不再只能依靠执行时抛类型错误；专业语义TypeId的映射由Host/专业Owner显式提供，并拒绝通过宽泛`object`输入伪称已验证。这是**运行绑定侧的检查器**，不是新的属性注册器，也不是统一配置Schema。对现有Vision测量Provider，`Inspection`输入的MPC语义类型与MRK宽泛`object`目前只能被标为`Unknown`，须在后续Provider兼容迁移中按实际调用方收敛。离线发现与Runtime解析依然分层，真正Attribute/Fluent统一注册仍未完成。
