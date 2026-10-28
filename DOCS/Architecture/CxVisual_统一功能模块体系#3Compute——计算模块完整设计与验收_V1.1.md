# CxVisual 统一功能模块体系#3Compute——计算模块完整设计与验收 V1.1

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


## 2026-10-27 专项深化：通用计算能力目标与可验收需求

> **状态：【目标规范增补】**。承接 UM-CP01～CP10，不将下面的 CP-Rxx 当作已实现 API/算法。本模块是**算法与计算能力的可调用单元**，不是新建“图像处理平台/AI引擎/数值库/流程调度器”。已经属于 Imaging、Numerics、MDS、MRK 或 Professional.Vision/CT3D 的能力必须直接复用其 Owner。

### CP-1 最终期望和双角色使用

**终态目标**：一个完整的专业 Compute 体系，可以接入普通数据、表格/信号、图像、点云、网格、CT 体、GPU Tensor 和时序流，并向业务输出可信、可复现、可诊断的结果。横跨工业机器视觉、X-Ray/CT、AOI、计量分析、AI 推理、3D 测量、科学计算和工程业务规则；不把“计算能力”缩成单个 \`ExecuteAsync\`。

- **开发者**：普通 C# 方法/函数先单独调用；需要时发布成带参数 Schema、输入输出端口、资源需求和版本的 Capability；复杂算法只管理自身上下文（模型/GPU/native 资源），避免重复造 MDS、调度和 Job Registry。测试可在脱离 Studio、Connector 的环境运行。
- **工程使用者**：Studio 按专业领域、数据类型、性能、设备、版本搜索能力；拖入算子后自动展示参数校验、输入输出数据与单位；可预览结果、比较算法/模型、配置 CPU/GPU/批量/超时、查看耗时/内存/版本/质量指标；运行时能定位“哪个输入、哪个配置、哪个算子”产生最终结果。
- **专业人员**：算法支持无 UI 的 SDK 使用、MOL/MRK 编排和服务化复用三种路径；同一结果定义与核心算法一致，UI 不私藏另一套计算实现。

### CP-2 详细能力清单

| ID | 核心能力域 | 应具备的完整范围与行为 |
|---|---|---|
| CP-R01 | 纯函数、类型与描述 | Scalar/Vector/Matrix/统计、常用规则、插值与数学；输入输出强类型、null/NaN/Inf/范围约束、版本和可重入/线程安全 Trait。 |
| CP-R02 | 信号与时序 | 滤波、频谱/FFT、频域/时域特征、峰值、周期、插值、重采样、事件窗口与质量检测；采样率、单位、时标不可丢。 |
| CP-R03 | 二维图像基础 | 支持原有 Imaging Owner 提供的灰度/彩色/多位深、Stride、ROI、空间标定；曝光/伽马/直方图/几何变换/滤波/形态学/二值化/边缘/分割等处理。 |
| CP-R04 | 2D 测量与计量 | 与“图像处理”分开提供边缘/轮廓/孔径/尺寸/距离/角度/面积、直线/圆/椭圆拟合、像素到世界坐标、标定和不确定度；结果具备单位与坐标基准。 |
| CP-R05 | 2D 分析、识别与检测 | 模板匹配、定位、特征/分类、OCR/读码、缺陷提取、目标计数、检测规则及阈值判定；质量指标与误检/漏检来源可记录。 |
| CP-R06 | AI 智能能力 | 检测/分割/分类/OCR、异常检测、去噪、超分辨率、增强、自动聚焦等按 Provider 真正支持；模型签名/预处理/后处理和版本固定，不能把未加载模型当可用。 |
| CP-R07 | 3D 数据处理 | PointCloud、Mesh、Voxel、Surface、Depth Map 的类型/坐标/单位；体素化、法线、平滑、采样、切片、分割、表面提取、配准及不同形态之间受控转换。 |
| CP-R08 | 3D 测量与分析 | 点/线/面测量、曲率/表面测定、拟合/基准建立、体积/孔隙、最小距离、形位公差、CAD/参考网格差异及偏差热图；坐标变换链和公差约定明确。 |
| CP-R09 | CT/X-Ray 专业计算 | 投影校正、坏点/暗场/平场、几何标定、重建前预处理、FBP/FDK/迭代重建等调用已有 CT Native Owner；每次任务固定 Geometry/角度/投影序号/校准版本。 |
| CP-R10 | 工业数据分析 | 统计汇总、质量趋势、SPC、异常/阈值监控、报表数据、批次对比、特征提取与可解释决策，不在 Compute 内重建 MES/业务服务。 |
| CP-R11 | 多输入/输出与数据绑定 | 多图像/多帧、图像+标定、体积+ROI、图像+AI模型、表格+规则等可组合；命名端口、缺失值处理和多输出成功提交，失败不能将部分数据标记为终态成功。 |
| CP-R12 | CPU/GPU/Native | CPU/GPU/其他加速器的能力发现与选择、加载/预热、线程/上下文安全、ABI/位数/版本校验、回收；切换加速方式必须记录实际后端，必要时设容差。 |
| CP-R13 | 内存/大型数据 | 统一 MDS DataRef/Lease/只读与 Copy-on-Write；CT 大体积采用分块、文件级引用、共享内存受控交接，禁止将大图/Volume 放入 Message Payload。 |
| CP-R14 | 批处理与流计算 | 单次、批量、滑窗、连续帧、投影序列、流式累加；队列容量与 NoDrop/Drop 策略按数据意义配置，能测算时延、吞吐和峰值内存。 |
| CP-R15 | 异步、取消与进度 | 短计算同步返回；长计算以可查询的 Operation/Job 关联、分阶段进度和终态记录；取消 GPU/Native 工作要有真实取消或“不支持安全取消”的明确状态。 |
| CP-R16 | 可靠性与隔离 | 计算超时、Native 崩溃/GPU OOM、数据失效、输入版本错、在途模型热更新都清晰失败或走显式降级；不跨 Run 污染结果或缓存。 |
| CP-R17 | 精度、复现与验证 | 输入快照、参数、模型、数值后端、算法版本、坐标/单位和随机种子进入结果来源；支持 Golden 数据、容差/重复性、真值比对以及误差预算。 |
| CP-R18 | 专业扩展 | 开发者可直接复用 OpenCV、Halcon、ITK/VTK、PCL、Native CT、其他算法库；严格核实许可证、ABI/运行依赖和真实能力，避免在模块体系重写同名核心。 |
| CP-R19 | 能力发现与使用 | MFM/MOC 目录暴露 CapabilityId/分类/端口/设备要求/数据类型/同步模式/副作用/可用性，MRK 查询并绑定实际算法 Provider；一个模块可提供多个算法。 |
| CP-R20 | 观测、测试与质量治理 | 算法输入尺寸/位深、模型与校准版本、资源峰值、GPU 占用、耗时阶段、错误码、实际质量指标和输出哈希/来源链可追踪；未验证结果不得默认为工业合格。 |
| CP-R21 | 隔离外部副作用 | Compute 默认无设备控制、服务发布、数据库写入；需要这些行为时显式声明并交给 Driver/Connector/Service/M12，不允许纯计算偷偷控制机台。 |
| CP-R22 | 安全、资源公平及降级 | 用户选择的 GPU/CPU、显存/内存限额、并发/公平性、优先级来自现有 Runtime/资源治理；没有可用后端应拒绝或经配置降级，不隐式切换算法语义。 |

### CP-3 输入/输出合同：不做万能 object

| 数据类别 | 输入必需条件 | 输出及原始 Owner |
|---|---|---|
| Scalar/Vector/Matrix | Shape、数值类型、单位、校验范围 | 强类型 Result / MDS |
| Signal/TimeSeries | SampleRate、时钟域、时标、缺样规则 | Feature/FilteredSignal / MDS |
| Image/Projection | Width/Height/Depth、PixelFormat、Stride、ROI、曝光/帧序/校准 | 新图/Mask/Measurement/Defect，归 MDS/Imaging |
| PointCloud/Mesh/Volume | 坐标系、世界/像素单位、拓扑或体素间距、方向矩阵、数据 Lease | 新 Surface/Volume/偏差量、专业数据 Owner |
| AI Tensor/Model | Layout、Shape、dtype、预处理、ModelVersion/Hash、GPU/CPU 依赖 | Detection/Segmentation/Confidence/Feature |
| Batch/Stream | 帧序、窗口、队列/丢帧策略、Run/Generation | 类型化批/流结果、进度和质量追踪 |

所有输出必须能回答：**哪个算法版本、输入 Ref/版本、参数快照、单位/坐标、计算后端、RunId、质量/置信度产生了它**。多输出失败按 MDS/MRK 的提交语义处理；“某图像已经产生”与“通知消费者”不是同一个操作。

### CP-4 与源码及接口的严格对应

当前已有：\`IPVCComputeModule.ExecuteAsync(IMDSDataContext,CancellationToken)\`、\`PVCComputeModuleConfiguration\`、\`PVCComputeModuleProfile\`、\`PVCComputeDevicePreference\`；它们只证明基本模块契约和配置存在，**不等于 CP-R01～R22 各算法已经实现**。应优先将计算能力公开到既有 \`MOCCapabilityCatalog\`，直接复用专业 Imaging/Numerics/CT 数据与运算接口，不创建 \`UniversalComputeEngine\` 或第二个 \`JobScheduler\`。

**目标 API 形态**（查重后复用）：普通 \`TResult Evaluate(TInput input, Options)\`；可选 \`IComputeCapability<TIn,TOut>.ExecuteAsync(input, DataContext, ct)\`；长作业复用现有 MRK Operation/Job 观察；单流和多流需要显式流背压契约。算法类型/参数/端口定义与运行 Provider 可以分离，Provider 不应另存第二套 Schema。

### CP-5 双角色标杆工作流

1. **AOI 二维检测**：选择 Camera Frame → 去噪/校正 → 定位/分割 → 测量尺寸 → 缺陷分析/AI 判断 → 输出 DefectList/Mask/Measurement → M36 通知；Studio 可观察每个算法的结果/耗时/标定版本，原图由 MDS Owner 保护。
2. **CT 重建与三维分析**：Projection + Geometry → 投影校正 → 重建 Volume → 表面提取 → 配准 CAD → 缺陷体积/几何公差 → 结果与 Viewer 引用；显示数据质量、体素、坐标和 GPU 预算；分块处理不能偷换计算精度。
3. **多模型 AI 产线**：同一 Frame 按模型版本调用推理，CPU/GPU/批次能力匹配；模型替换后旧 Run 固定旧版本；聚合输出显式置信度/质量阈值，异常降级有审计。

### CP-6 TestCenter 专项验收（CP-A01～A12）

| 编号 | 正反证要求 | 层次 |
|---|---|---|
| CP-A01 | 最短强类型函数无 Host 可运行；对外暴露后 Studio/MOC 能按能力/端口发现 | G0/G1 |
| CP-A02 | 输入 dtype/Stride/单位/Shape/坐标与缺必需参数均有准确拒绝 | G1 |
| CP-A03 | 算法确定性/精度、边界、Golden 和跨后端容差可复现 | G1/G2 |
| CP-A04 | 多输入多输出成功/失败/取消，不跨 Run 发布部分结果 | G1 |
| CP-A05 | 共享图像/大体 Lease 生命周期有效，写入需要受控复制，超限失败 | G1/G2 |
| CP-A06 | AI 模型缺失/版本不兼容/显存不足、热更新/多 Run 场景不串扰 | G1/G2 |
| CP-A07 | CT 投影缺角、错误几何、重建输出尺寸与质量均能明确诊断 | G1/G2 |
| CP-A08 | 3D 测量/拟合/配准的坐标、单位、公差及误差统计有真值样本 | G1 |
| CP-A09 | 连续帧/NoDrop/背压场景峰值内存与吞吐保持有界 | G1/G2 |
| CP-A10 | 长作业进度、取消、超时/终态与外部数据释放一致 | G1/G2 |
| CP-A11 | 开发者 SDK、Studio 可视编排、服务化调用同数据得兼容结果 | G1 |
| CP-A12 | 性能基线、算法正确率/稳定性与生产样本误差预算分别留证 | G2/G3 |

**实施分段**：P0 先完整函数/端口/参数/结果合同并贯通真实 2D 图像算子；P1 AI 与 CPU/GPU/多流/资源；P2 CT/3D 表面、配准、计量与专业算法；P3 长稳、多模型、跨专业质量验收。优先复用已存在专业代码，不能按此阶段划分假定 CT/AI 等当前为零或已全部完成。


## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。

---

## V1.1 本轮补充：专业计算全类、类型与质量合同

> 【新目标，不是已实现】保留 UM-CP01～10、CP-R/CP-A 全部原有条款与原 2026-10-27 深化；本节新增 CP-X 验收。示意职责不得未经 DOCS/02～04 核验而擅建平行系统。

### CP-7 专业计算覆盖清单

| 能力族 | 典型输入与执行 | 输出与工业质量 | 复用归属 |
|---|---|---|---|
| Numerics/基础数学/统计 | Matrix、FFT、优化、信号滤波、拟合、插值、聚类 | 精度、残差、数值条件、NaN/Inf 策略、收敛 | 现有基础数值库/C++ |
| 几何/规则/数据分析 | 坐标变换、3D 几何、测量公差、规则表达、时序聚合 | 单位、坐标空间、不确定度、版本、追溯 | FDT/MDS 与专业算法 Owner |
| 2D 机器视觉 | ROI、图像增强/去噪、分割、定位、匹配、标定、识别、检测/测量 | ImageRef、Defect/Feature、标定/ROI/置信度 | FIM/MVI/PVC、ReferTo HALCON |
| AI | 分类、检测、实例/语义分割、超分、去噪、模型推理前后处理 | Tensor、Mask、Score、ModelHash、LabelSchema、版本 | 专业 Compute/GPU，不建第二 AI 调度器 |
| CT | Projection 校正、几何标定、FBP/FDK/迭代重建、Volume 处理 | Geometry、VolumeRef、VoxelSpacing、收敛/伪影/精度 | cx-cct-x3、cx-vision-x2、CT3D Owner |
| 3D/CAD | 配准、拟合、表面测定、点云/网格、CAD/SolidWorks 偏差、公差分析 | Transform、Surface、PointCloud、DiffMap、残差 | 专业几何/CT3D、Native 复用 |

普通独立 C# 算法函数 L0 可直接使用，无须创建 Module 实例；仅当需要模型/GPU Context/Native 状态、缓存或流式资源时使用有状态 Compute。算法不拥有设备采集、MRK 流程、MOC 能力目录或 MDS 数据生命周期。

### CP-8 强类型调用及结果原子发布

一个能力应声明 CapabilityId/Version、一个或多个 Input/Output 的 FDT/CLR 类型、形状/维度、位深/Stride、坐标系、单位、可空/必填、数据量/引用所有权、参数范围/默认值、算法/后端版本、是否有副作用、并发、取消和超时能力。不允许通用 object 成为无校验算法输入或错误时悄悄返回空数组。

执行流程：校验输入 Schema、Generation 与数据租约→确认 CPU/GPU/Native/模型资源和预算→固定模型/算法版本→执行实际算法/报告进度→校验多个输出的数值与尺寸→通过 MDS 正式上下文一次性发布成功结果→资源回收。第二个输出失败不可使第一个输出被误报成完整成功；需要 Partial 时须声明特定结果语义。CancellationToken 取消等待不等于 GPU/Native 操作已经停止，不允许提前释放仍被使用的显存。

数值验收必须包含输入数据集 SHA、Golden/容差、数值稳定性、坏矩阵/溢出/NaN、CPU/GPU/线程归约差异、模型版本与输出有效域。性能报告记录输入尺寸/批次、硬件/驱动/后端、冷/热启动、P50/P95/P99、吞吐、峰值内存/显存和复制次数；不是把配置中的 MemoryBudgetMb 当作性能通过证据。

### CP-9 两角色使用与跨模块闭环

开发者：先单测纯 C# 计算函数→按需发布 IMOCCapabilityProvider 与端口→使用 PVCComputeModuleConfiguration 的 Device/MaxConcurrency/MemoryBudgetMb/TimeoutMs/ProviderTypeId/ExecutionGroupRef→通过已有 IMDSDataContext、MRK Capability Resolver 执行并提交强类型结果。Studio 用户：按类别搜索算法→看输入输出、Provider/模型版本/资源与可用性→选择 Image/Volume/Variable Ref→验证单位、数据类型、ROI、参数→构建/运行→预览数值结果、缺陷/测量、进度、时延、错误与基准比较；不可用算法不应有伪造预览。

跨模块：Driver 输出 FrameRef/ProjectionRef→MDS→Compute 真算法→MDS 结果→Service 受权访问；Simulation 生成数据必须保留 Virtual 来源标签；MES 上传成功与算法 Result 的有效性是两条不同证据链。

| 用例 | 输入/负例 | 断言 |
|---|---|---|
| CP-X01 | 纯函数正常、空/NaN 输入 | 无 MRK 也能正确测试，错误可定位 |
| CP-X02 | 两输入三输出、错误类型/单位 | 类型端口校验，不能万能 object 绕过 |
| CP-X03 | 输出二失败/中途 Cancel | 结果不半提交、无旧 Run 数据污染 |
| CP-X04 | 多消费者 ImageRef 与失效 Lease | 原图不可被私有修改，资源回收到零 |
| CP-X05 | 标定 2D 测量、标准 Phantom/CT、CAD | 单位/坐标/数值误差在明示阈值内 |
| CP-X06 | 病态矩阵/不收敛拟合/缺失投影 | NumericalFailure/Quality，不输出伪正常值 |
| CP-X07 | CPU/GPU、双版本模型/多 Batch | 版本锁定、误差与隔离、资源峰值可验 |
| CP-X08 | 模拟 OOM/Timeout 后连续 1000 次调用 | 不死锁、无显存/Native 句柄持续泄漏 |
| CP-X09 | 多线程、流式背压与高并发 | 明示拒绝/限流/顺序/丢弃语义 |
| CP-X10 | Studio 发现、配置、调用、取消、诊断 | 复用 MOC/MRK/MDS，UIA 实际证据 |

### CP-10 真实源码对照与阶段门禁

Professional/Vision/Modules/PVCComputeModuleContracts.cs 当前仅显示 IPVCComputeModule.ExecuteAsync(IMDSDataContext,CancellationToken)；它是执行入口，未自证多输入多输出强类型端口、质量与计算后端。PVCComputeModuleConfiguration.cs 的 CPU/GPU 与预算是真实 FCM 配置，并非 GPU 执行、模型缓存、负载控制已被实现。MOC 的 MOCCapabilityDescriptor/Ports、MOCCapabilityCatalog/RuntimeAvailable 与 IMRKCapabilityResolver 已有，应原位扩展而非重造目录。ModuleFoundationSmokeTests 的 Increment 与配置测试源码不等于 CT/AI/3D 算法验收。

P0 实际数值/2D 算子贯通 L0→MOC→MRK→MDS→TestCenter（含负例与 Golden）；P1 AI/GPU/流与资源预算；P2 CT/Volume/3D/CAD；P3 工业数据精度与 G3 性能长稳。任何 ReferTo 专业算法需逐文件核对依赖、许可、ABI 与单测后正式迁移。
