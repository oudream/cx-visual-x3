# CxVisual 统一功能模块体系#3Compute——计算模块完整设计与验收 V1.0

> **文档类型**：专业模块完整目标规格；**生成日期**：2026-10-26；**来源基线**：旧完整规格 V1.2（承接 V1.1 六篇十八章及附录）、五类裁决 V1.2、Connector 裁决 V1.3、公共交互 V1.4。  
> **性质**：完整目标设计与验收规范，不是源码已实现声明；代码事实须以最新 `master`、实际 Windows CI 和设备 HIL 为准。  
> **原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **范围**：纯/有状态/异步计算，CPU/GPU/AI/CT。  
> **平台**：`cx-visual-x3`，.NET Framework 4.8 / C# / WPF；保持 Foundation → Mechanism → Professional 单向依赖及现有项目 Owner。  
> **状态标识**：【裁决】架构已确认；【继承要求】原规格完整保留；【目标】待实现；【源码已有】需以对应 SHA 核实；【待验】缺 CI/HIL/长稳证据。  

## 文档来源、优先级与版本冲突

1. `CxVisual_统一功能模块体系——完整设计与验收_V1.2.md`（个人文件库，约 226 KB，继承 V1.1 全文）：保留需求编号 `UM-*`、`TFM-*`、`TFC-*`、`TFD-*`、能力/类型表、两角色案例与所有质量边界。
2. `DOCS/Architecture/CxVisual_统一功能模块体系——五类模块与设备驱动架构裁决_V1.2.md`：正式五类、Driver 归位，无第六模块。
3. `DOCS/Architecture/CxVisual_统一功能模块体系——通道规约与数据映射架构_V1.3.md`：**优先覆盖 V1.2 的 Connector 单组/Advanced 旧条款**；当前只有 `MCNConnectorConfiguration.Channel` 和 `.Protocol` 各一个；多对端属 SubChannel。
4. `DOCS/Architecture/CxVisual_公共机制体系#统一交互——四大能力与双层总线完整设计及验收_V1.4.md`：Variable/Data/Message/Command 独立 Owner、全局交互、双层总线，**并非已完成代码的证明**。
5. `AGENTS.md`、工程 Skill、`DOCS/00～04`：工程约束与类型查重；实现前必须逐对象核对 DOCS/02～04 表格及最新真实源码。

**覆盖规则**：五类优先于早期四类；V1.3 的“一 Connector 一 Channel 一 Protocol”优先于 V1.2 的 `Standard/Advanced`、多个 `Channels/Protocols`；驱动 SDK 归 Driver 优先于早期“全部归 Connector”；来源附录为了完整追溯而保留旧表述，**冲突时以本文件前部裁决条款为准**，不能从历史引用恢复已被否决的结构。

## 专业模块的通用操作与对外能力

任何模块均应明确提供：**模块身份**（ModuleId/Kind/SchemaVersion）、**提供者能力**（CapabilityId/版本/Ports/Traits/Inputs/Outputs）、**可配置性**（唯一 FCM Section）、**运行生命周期**（确有实例才创建）、**诊断**（Status/Health/Error/Trace）、**依赖**（Resource/Owner）、**产生/处理接口**（Produces/Handles 四类交互）。

**不要求各模块新建通用 `IModuleBus` 或巨大 `Execute(object)`；具体签名优先复用现有接口。** 设计示例中的新增接口、事件、字段必须经 `DOCS/02～04` 查重、源码核实和 ADR 冻结，不能直接宣称当前可编译。

## 01. 运算能力模型

**【裁决】普通可重入纯函数无需创建模块实例**；只有模型预热、GPU Context、缓存、长作业或持续运行资源才需要生命周期。Compute 负责计算结果本身，外部设备、命令、副作用由 Connector/Driver/Service 明确拥有；MOL/MRK 定义执行编排，不建立第二个计算调度中心。

| 计算类别 | 输入输出与典型实现 | 运行约束 |
|---|---|---|
| 值/规则计算 | C#、表达式、简单几何/检测规则 | 强类型、确定性、同步直调 |
| 批量图像 | OpenCV/Halcon/自研 Vision，8/16/32 位及多通道 | ROI/Stride/单位/坐标与 GPU/CPU |
| AI 推理与后处理 | 多模型、分割、分类、检测、超分、去噪 | 模型版本固定、资源并发、任务批处理 |
| CT/3D 重建 | ProjectionSeries、Geometry、FDK/迭代重建、Volume | 数据量/内存预算/GPU Lease/异步 Job |
| 流计算 | 图像帧、产线信号、投影序列 | 有界流、背压/NoDrop/Drop 与质量标记 |

## 02. 对外 API、结果及资源

| 合同 | 具体要求 |
|---|---|
| `CapabilityId` / Port | Input/Output Schema、类型/范围/尺寸/单位、版本、是否纯函数、耗时与副作用 |
| 直接调用 | 普通函数可单测，不强制统一 `Execute(object)`；需要工程发布时投影 MOC Capability |
| `MDSDataContext` | 节点输入 Get、局部 Set、成功 Return；多输出原子可见 |
| 长作业 | JobId、进度、取消、超时、阶段结果、结果 Ref/Artifact、终态与诊断 |
| 资源 | CPU、GPU、显存/内存、Workgroup、Worker、线程亲和、任务优先级与容量 |
| 精度与质量 | 误差、基准、坐标/单位、位深/Endian/Stride、确定性或非确定性容差 |

## 03. 工业视觉/CT 的数据闭环

在采图 Driver 正式产生 `ImageHandle` 后，Compute 只消费合法 Lease，不更改生产者内存；算法输出 DefectResult/Measurement/Mask/Volume/ReportData 并由 Owner 提交，再发布轻量完成事件。投影流 NoDrop 时预算不足必须背压/节流或落盘，而非无限缓存。CT 3D 处理需声明像素空间/世界空间、标定、体素尺寸、坐标方向、缓存预算和分辨率；支持文件级引用，不把 8 GiB Volume 隐藏在普通 Message Payload。

## 04. 开发者与 Studio 用户

开发者：先实现可直接单测的强类型 C# 方法，再可选提供描述和 Provider；专业 C++/CUDA/Halcon 通过已验证 ABI/安全资源访问融入，不把未知原生 DLL 称为已迁移。编排者：按输入输出筛选算法、选择资源、验证精度/参数，保存计划、运行、Watch 输入输出/耗时、从 SourceMap 定位失败。动态模型与配置切换必须 Pin 版本，不改变途中的 Run。

## 05. 计算反例

无合适显存不能返回空结果却标成功；取消 GPU Job 不能在显存仍使用时释放 Lease；模型文件缺失不能被“默认阈值模型”悄悄替代；设备控制不得藏在名为 Evaluate 的纯 Compute 中；同一图像供多个消费者不能共享可写裸数组。

## 统一交互：谁产生、谁处理、如何装配

| 输入/输出 | 产生者与处理者 | 由谁登记/通知/调度 | 关键安全及成功语义 |
|---|---|---|---|
| Variable Commit/Changed | 设备/业务/规约提交，HMI/Protocol/流程处理 | M09 原子提交与订阅；运行装配按映射连接 | 质量、SourceId/WriterId、Previous/Revision；变化不同于提交 |
| DataAvailable | Camera/AI/CT 创建，合法消费者按引用读取 | MDS/专业 Owner；Run/Generation/DataHandle 与租约 | 提醒为元信息，不广播 Image/Volume 本体 |
| Message | 任意合法模块发 Topic+Payload，多订阅者处理 | M36 发布/订阅；订阅取消和背压 | Publish/Deliver/Handle 分开；消息不可私自转成动作 |
| Command | HMI/MRK/业务发起，唯一 Handler 处理 | M12 注册精确目标；MRK Operation 协调 | 授权/资源/幂等/执行/现场确认各有结果，超时为 Unknown |

- 同一个模块可同时生产、消费、处理并再产生对象；处理关系**必须**有明确配置、代码注册、订阅、绑定或命令 Target。
- 直接映射留在对应 Owner：`MPADataMappingExecutor`、M36 订阅、M12 Handler、MDS/MOL 绑定各有正式职责；不建立万能 MappingManager。
- **动态能力发现**：`MFMModuleDescriptor.CapabilityIds`、`MOCCapabilityCatalog`、`IMRKCapabilityResolver` 等已有描述与目录为首选；模块可选择专业 Provider，但实际可用性应检查配置、依赖、Provider 状态和执行时权限。
- Connector/Protocol 可处理四大变化：变量订阅出站、数据发布引用出站、消息订阅出站、Command Handler 定向出站；入站由 Protocol 解析后调用真实 Owner。装配由运行宿主按映射创建并在 Stop 释放订阅，**不能让 Connectivity → Mechanism.Data 产生循环依赖**。

## 双角色使用合同

| 环节 | 软件开发者 | 工程编排用户（Studio/TestCenter） | 平台落实方 |
|---|---|---|---|
| 定义能力 | 提供 `CapabilityId/Version` 与参数/结果契约、真实处理者 | 浏览五类模块和 Produces/Handles | MFM/MOC/MEM |
| 配置 | 定义一份正式 FCM Schema 和专业校验 | 分类表单创建、验证、保存、加载、比较 | FCM/MHS/MPW |
| 交互 | 用现有 Owner API 发布值/消息、数据句柄或受控命令 | 选择来源、目标、触发、参数和订阅关系 | M09/MDS/M36/M12 + 绑定 |
| 调用 | L0 原生函数；L1 强类型 Provider；L2 模块能力；L3 工程运行调用 | 在 MOL 中拖入能力，MOC 校验后 MRK 执行 | 专业 Owner / MRK |
| 诊断 | 收到有意义的错误码、Operation、Trace、版本 | 查看状态、时序、故障原因及证据 | 已有日志/Trace/TestCenter |
| 安全 | 声明副作用、幂等、取消、资源及物理确认能力 | 有授权才执行真实设备动作，离线可仿真 | Target Runtime 与专业 Provider |

## 专项 TestCenter 验收清单

| ID | 场景 | 必要证据 |
|---|---|---|
| CP-P01 | 纯函数直接测试与工程调用 | 同输入类型/版本结果一致，不强迫实例化 |
| CP-P02 | 错误图像位深/Stride/颜色格式 | MOC 拒绝或运行清晰报错，绝不产生伪结果 |
| CP-P03 | 多输出失败与取消 | 原子输出、无部分发布、资源回收明确 |
| CP-P04 | GPU 显存不足/模型不兼容 | 降级策略可见，不能假成功或丢版本 |
| CP-P05 | NoDrop 流受背压 | 有界队列、吞吐/延迟/内存稳定证据 |
| CP-P06 | 真实 CT Projection→Volume→Viewer | Geometry、尺寸/精度、VolumeRef/生命周期与吞吐日志 |
| CP-P07 | AI 模型热更新与多个 Run | 旧 Run 继续用固定版本，新 Run 使用新 Manifest |

## 与总体规范的关系及修改门禁

- **共性模型、两角色、版本、能力发现、统一交互、总体测试**以 `CxVisual_统一功能模块体系#0总体架构——完整设计与验收_V2.0.md` 为唯一解释。
- 本文是该模块专有行为与验收的权威文件；遇到旧源稿的相反描述，必须按文首覆盖表处理，并在文档里保留来源追溯。
- 任何新增正式接口/类名前须检查 `DOCS/02～04`、最新源码、现有 Owner、`ReferTo`；不能用概念示意代码替代编译测试。修改后同步更新总体文档的能力归属表与本模块 TestCenter 专项验证。


## 原 V1.2 来源条款、需求与案例（逐节承接）

> **来源历史提示**：下列原文保留其需求编号、专业能力和案例，便于追踪。凡涉及旧 `Advanced` 多组、历史“相机 SDK Connector”、未查重 API/状态的文字均只是历史上下文；**本文件前部的现行裁决优先**。

## 09 ComputeModule——计算模块

### 09.1 正式职责

对一个或多个**已拥有或被合法引用**的输入执行可验证的计算、处理、规则求值及专业算法，输出一个或多个类型化结果。涵盖纯函数、配置化计算、状态/资源型算法、GPU/Native、批处理与异步重建；不另建 Processing/Algorithm/Logic/Script/Control 模块类型。

### 09.2 需求 UM-CP01～CP10

| ID | 能力要求 | 典型场景与行为 | 必须验证 |
|---|---|---|---|
| UM-CP01 | 最短普通调用 | 普通 `Func`、静态/实例函数、业务类可直接单测，选择性导出为能力 | 无强制 Host、Connect、Project 构造 |
| UM-CP02 | 多种计算 | 数学/矩阵/字符串/统计/信号/几何/数据清洗/规则求值 | 数值精度、空值、边界、版本一致 |
| UM-CP03 | 专业算法 | Vision 2D、AI 推理、CT 重建、3D 测量与其他专业算法 | 类型/空间/单位/位深约束明确；不能假数据冒充 |
| UM-CP04 | 输入输出与参数 | 多输入、多输出、可选参数、批量、配置快照、端口发布 | 无错连；计算失败无部分成功输出 |
| UM-CP05 | CPU/GPU/Native | 引用已有 CPU/Native GPU 算法、型号/依赖加载与资源策略 | 位数/ABI、设备选择、显存预算、释放 |
| UM-CP06 | 同步异步/作业 | 快函数直接返回，慢算法 `Task/Job`，可取消/进度/超时 | 取消与任务终态可查，旧结果不写新 Run |
| UM-CP07 | 可变数据安全 | Image/Volume/Tensor、共享只读、修改显式复制、可控缓冲 | 原图不被隐式篡改、Lease 不越寿命 |
| UM-CP08 | 规则与表达式计算 | 条件/判定/阈值/业务计算复用 MOL 表达语义 | 不新建 Graph、循环、分支执行引擎 |
| UM-CP09 | 模型与缓存复用 | AI 模型预热、CT 算法状态、线程安全缓存、配置版本固定 | 模型按声明的共享/隔离策略使用，无全局串扰 |
| UM-CP10 | 性能与可诊断 | 耗时、批量大小、GPU/CPU、复制次数、缓存命中、精度 | 真实结果核对+资源/峰值观察；不以吞吐代替准确率 |

### 09.3 三种计算粒度与工作模型

1. **表达式级**：`Clamp`、比较、字符串格式、四则运算，直接由已有表达式/规则机制或普通函数提供；**不必**建独立 Module 实例。
2. **算法级**：图像滤波、缺陷判定、坐标拟合、多输入输出统计，发布为 ComputeModule 的一个或多个 Capability；参数采用统一配置。
3. **资源级**：AI GPU 模型、FFT 规划器、CT 重建 Context 等拥有资源/异步行为；Provider 管理资源状态，业务计算仍由原专业实现执行。

### 09.4 计算质量与结果合同

- 精度：声明数值类型、舍入与误差容许、像素/毫米/角度单位、坐标变换；纯函数尽量确定性，涉及并行/GPU 非确定性时明确误差与种子。
- 数据：允许流式分块处理，但中间可变数据不能直接无保护共享。输出是类型化结果、受控 DataHandle 或明确失败状态。
- 配置：例如阈值、模型/版本、GPU 选择、迭代次数必须在一次运行中固定；新配置不改变在途 Run。
- 可取消：算法对 CPU 轮询取消或 GPU 作业使用受支持的取消点；请求取消不代表底层 GPU 已停止，也不等于 Output 已安全释放。
- 外部副作用：Compute 默认无外部设备控制；若算法写文件/调用服务，必须声明副作用并优先交给已有文件/Connector/Service Owner，而不是隐蔽在规则内部。

### 09.5 与已实现源码的连接点

现有 `Professional/Vision/CxVisionDataFlow.cs` 使用 `MRKExecutionPlan`、`ctx.Data.Get<T>`、`ctx.Data.Return`，对 8/16 位 RAW 做 CPU 解码、预处理和检查；它是**真实生产 Owner 代码路径**，适合作为首批 Compute 审核/用例，但不能据此宣称 AI、GPU、CT 全部已被统一 Compute 体系覆盖。CT 原生 `cx-cct-x3` 可通过已审核 C ABI/SHM 边界直接复用，必须验证实际依赖与 ABI 版本。

### 09.6 开发者与 Studio 示例

开发者先写 `PixelRule.Evaluate` 并单测，再将端口契约暴露给 MOC；Studio 用户将 `Frame/Threshold/BadLimit` 连接到能力节点，运行后看到 `BadPixels/Accepted` 及计算耗时。若图像类型、位深不匹配，构建阶段失败；若运行期发现资源不足，不发布假的 `Accepted=true`。

### CASE-FM-E：AI 检测服务和重复调用

**目标**：`Compute(Inference)` 通过 `Service(AIInference)` 对多个检测流程提供长期模型服务。Developer 使用单一推理函数合同、受控模型加载/GPU 池，Service 对内公开同步/异步接口和请求队列。Studio 选择模型版本、配置单/批输入、查看队列/就绪状态/显存/结果与错误。

**测试条件**：10 并发请求、两个模型版本、重启/模型替换时请求固定版本；参数非法、依赖模型缺失、显存溢出、调用者取消、重复请求幂等。一个 Client 超时绝不在另一个 SN 上提交结果；没有真正加载模型时服务不得报告 Ready。

### CASE-FM-F：完全无通道的逻辑、数据计算与内部服务

**目标**：证明“不需要 Channel 并不意味着缺一种新模块”。Compute 以输入字典/类型化数据执行质量判定规则，Service 供内部查询、订阅判定结果；MDS 保存合法执行数据。开发者直接调用算法，也可用统一能力接口发布；工程用户在 Studio 拖节点与连端口。

**典型规则**：`Accepted = (DefectCount == 0) && (Area <= Limit) && (ImageQuality == Good)`；规则的判断计算属于 Compute，流程 If/Else 和结果下发的节点关系属于 MOL/MRK。可选把结果发给外部 MES 时才追加 Connector。

**负例**：输入类型不匹配、缺某个必须参数、无权访问变量来源、配置版本改变/运行未固定、Service 关闭后仍尝试调用，均须有准确诊断；**不允许**因为“无 Channel”自动创建一个永远返回成功的 EmptyChannel。

## D.1 示例使用规则

- **示例类型 A：标准 C# 可独立使用**，无需引用不存在的 Module API；可抽离到当前 C# 7.3/.NET 4.8 工程中单测，但本次并未编译执行。
- **示例类型 B：伪 API/目标集成方式**，用于确认“开发者最短路径”；不得作为当前 SDK 复制运行。
- **示例类型 C：Studio 操作验收步骤**，为未来真实界面目标；现有 UI 是否每个按钮都可操作，须在具体提交实际验证。

## D.2 示例 A：完全无通道的检测规则 Compute

```csharp
using System;

// 独立于 CxVisual 模块框架的普通业务对象
public sealed class DefectSummary
{
    public int DefectCount { get; set; }
    public double LargestArea { get; set; }
    public bool ImageQualityGood { get; set; }
}

public sealed class JudgeOptions
{
    public int MaximumDefects { get; set; }
    public double MaximumArea { get; set; }
}

public sealed class JudgeResult
{
    public bool Accepted { get; set; }
    public string Reason { get; set; }
}

public static class QualityRule
{
    public static JudgeResult Judge(DefectSummary input, JudgeOptions config)
    {
        if (input == null) throw new ArgumentNullException("input");
        if (config == null) throw new ArgumentNullException("config");
        if (config.MaximumDefects < 0 || config.MaximumArea < 0)
            throw new ArgumentException("判定参数不能为负值");
        if (input.DefectCount < 0 || input.LargestArea < 0 ||
            double.IsNaN(input.LargestArea))
            throw new ArgumentException("检测结果数据无效");

        if (!input.ImageQualityGood)
            return new JudgeResult { Accepted = false, Reason = "图像质量不合格" };
        if (input.DefectCount > config.MaximumDefects)
            return new JudgeResult { Accepted = false, Reason = "缺陷数量超过阈值" };
        if (input.LargestArea > config.MaximumArea)
            return new JudgeResult { Accepted = false, Reason = "缺陷面积超过阈值" };
        return new JudgeResult { Accepted = true, Reason = "OK" };
    }
}

// 普通开发者直接使用：
// JudgeResult result = QualityRule.Judge(
//     new DefectSummary { DefectCount=0, LargestArea=0.05, ImageQualityGood=true },
//     new JudgeOptions { MaximumDefects=1, MaximumArea=0.1 });
```

**期望导出**：能力 ID `Inspection.QualityJudge`，输入 `DefectSummary`、`JudgeOptions`，输出 `Accepted`、`Reason`，并由专业类型适配注册明确 Copy/Immutable 语义。这个导出可以采用现有 `IMOCCapabilityProvider`+MOC 端口契约，再由 MRK DataContext 适配；不可在新模块体系里平行存储同一 Port 定义。

**必须验证**：两个 Run 各有自己的配置/数据，`MaximumArea` 无效被拒绝，结果不触发设备下料/放行副作用，只有 MOL/MRK 的授权动作能力才能真正操作设备。

## D.3 示例 B：异步 CT 计算与独立 Service 的接口边界（C# 示意合同）

```csharp
// 【以下代码示意业务接口，不是当前正式 CxVisual 类型】
// 业务的输入输出/异步语义独立于 Module 技术实现。
public interface IReconstructionComputer
{
    System.Threading.Tasks.Task<ReconstructionResult> ReconstructAsync(
        ReconstructionRequest request,
        System.Threading.CancellationToken cancellationToken);
}

public interface IReconstructionQueryService
{
    System.Threading.Tasks.Task<JobTicket> SubmitAsync(
        ReconstructionRequest request,
        System.Threading.CancellationToken cancellationToken);
    System.Threading.Tasks.Task<JobStatus> GetStatusAsync(
        string jobId,
        System.Threading.CancellationToken cancellationToken);
}

// ReconstructionRequest/Result、JobTicket/Status 属于此示意合同的数据 DTO；
// 正式产品中须复用实际 CT ABI/DataHandle/MRK Job/Result，不能直接复制建立第二套 Job Manager。
```

算法独立实现 `ReconstructAsync`，服务入口只承诺受理/JobId/查询/结果访问，不等于每次请求创建一个新的计算框架。真实 `cx-cct-x3` 重建可能通过 C ABI + SHM Adapter 实现，必须确保跨进程数据所有权和终态一致。

## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。
