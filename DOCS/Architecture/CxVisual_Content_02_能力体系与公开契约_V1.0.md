# CxVisual Content 体系 #2——能力组合、公开契约与统一发现 V1.0

> 状态：目标合同；主需求 CNT-R13～R24。依据：Symbol #2/#3/#5 V2.0、Decision #3 注册 V2.0、MFM 功能模块、MPM/MPC/FCM 及 MOCCapabilityCatalog 真实 API。勿重复创建 ContentCapabilityCatalog。

## 01. 两层能力，不可混淆

**内部能力**：Content 使用的已有专业算法、设备动作、服务、子 Content、Symbol 交互、计算/数据转换与组合逻辑；不必全部公开。**外部能力**：Content 自己明确提供给调用者的功能、输入输出、事件、状态、配置及约束。一个 Content 可以内部使用 20 项 Capability，却仅公开 Inspect(Product,Recipe) / Cancel(RequestId) / Result / Alarm。不要自动把每个内部节点变成对外接口。

Capability 是有稳定身份与端口的可调用合同；Module 是 Connector/Driver/Compute/Simulation/Service 的实现载体；Content 是编排和封装的工程定义；Symbol 是呈现与交互入口。一个 Capability 可以被多个 Content/Symbol 消费；一个 Content 可以组合多个 Capability。纯静态 Content **无需**注册可执行 Capability。

## 02. 必须能表达的外部合同

| 元素 | 合同 | 行为和错误 |
|---|---|---|
| Property | MemberId/TypeId/Default/ReadOnly/Public/Override | 声明/配置，实例覆盖需合法 |
| Input/Output | PortId/MemberId/Kind/Direction/Multiplicity/Required | 类型、单位、时基/坐标/质量及传输规则 |
| Operation（语义） | OperationId/CapabilityId/Version/Input/Output/Effects | 是否可调用需显式注册/映射，不由 UI 命名推断 |
| Event/Command | 来源、参数、请求/应答、关联身份 | Event 请求不等于命令已完成 |
| State/Observation | 状态来源、质量、失效回退、可见性 | Current Value 不进 Definition |
| Config | SchemaRef、Default、Override、Saved/Effective | 保存值不等于设备生效 |
| Dependency | Provider/Module/Package/Resource/Permission/Version | 未安装与未知需独立报告 |

以上语义尽可能由 MPMProperty、MPCPort、MPMAspect、MRCPublicMapping、FCM Schema 及专业描述派生。任何新增 Operation 对外可持久合同若现有 Port/Capability 引用无法表达，须在 #9/ADR 举反例并经 Core Owner 决议，不能先发明 ContentOperationRepository。

## 03. 公开契约应如何获得

1. 新建：作者选择公开 Property/Port/Operation，内部默认私有。
2. 从内部子树提升：识别穿越边界的 Data/Control/Event/Resource 连接，提出候选映射，用户确认后一次事务。
3. 从 Capability 组合：依据可信 MOCCapabilityDescriptor/MPC Port 自动建议 Type/Direction/Required；不复制另一份可漂移 Schema。
4. 从模板/包：读取稳定版本/来源/依赖，缺失类型先保真/诊断、不可静默降为 object。
5. 从专业：专业可提供适配层解释单位、坐标、设备安全、资源预算，Core 仍只保持通用合同。

**不可直接公开**子 Content 私有步骤结果、MDS Frame、未校验动态回调、设备 SDK 指针、密钥和物理资源句柄。

## 04. 能力发现到执行的六段结果

Described（有静态描述）→ Discoverable（只读可检索）→ Installed（项目已纳入依赖）→ Resolved（版本/实现可定位）→ Ready/Authorized（Host/专业确认）→ Completed/Observed（该次调用可验证）。这些可不是一条线性单枚举，须按各 Owner 返回多维状态。Decision 静态匹配依据已知约束返回 Allowed/Rejected/Unknown；静态通过不表示现实设备 Ready。

只读目录应聚合 MEM 注册类型/Project Definition/MTI 包、MFM Module、MOC Capability Descriptor、专业 Provider 的**只读投影**，并保留 TypeId/DefinitionId/CapabilityId 的显式多对多映射。浏览不调用 session.RegisterTypes，后者属于工程修改。推荐专业轻量注册路径优先显式 Provider/入口，注解和 Fluent 属 Symbol/Decision V2 提议，尚非已实现。

## 05. 组合能力的三种形态

- **Facade**：隐藏多个内部能力，只公开一个 Inspect Operation，错误和结果与内部调用链可追溯。
- **Pipeline**：输入/输出相连，控制与数据经 MOL/MOC 校验，显式设置 Export。
- **Orchestrated Unit**：含条件/循环/等待/状态/事件/补偿等更长业务；需要 Runtime Flow，且专业实际副作用须由 Host 验证。

没有“Content 自己执行代码”的第四种隐式通路。简单场景可由 SDK 直接组合已有 Capability，不强制走复杂 Studio 工程。

## 06. 端口与映射细则

- 所有公共端口提供稳定 MemberId/PortId、类型、方向、多重性、必填、单位、坐标系、时间基准/频率、质量、数据引用语义；缺失的专业维度明确 Unknown。
- MPCConnection 表达静态关系；MOLLink 表达逻辑 Control/Data/Stream 关系；VisualAttachment 只影响显示几何，不能混用。
- 数据映射必须指定来源与目标，公开到私有成员通过现有 Mapping 验证；运行前检查 Scope、Version、Required、类型、单位和专业约束。
- 对外 Output 只有经过对应执行语义提交/发布屏障，才成为父 Content 的可见结果；失败/取消不泄漏 Pending。
- 专业可提供显式 Adapter（单位、几何、图像格式、坐标变换），不能把转换默认为总可行。

## 07. API 使用样例（真实与候选严格区分）

现有：MPMTypeDescriptor 用于声明 Content Kind；IMPMTypeProvider.DescribeTypes 供可信注册；MEMEngineeringSession.AddContent / PlaceContent；MOCFlowBuilder.Step(MOCCapabilityDescriptor) 和 InvokeContent(MEMContent) 已有源码入口，后者要求工程快照和可支持的 Logic 声明。**不保证**所有 Schema/所有执行场景可编译成功。

~~~csharp
// 语义演示；具体接线和端口请调用已存在的 Builder 方法，以最新签名为准。
MEMContent child = session.AddContent(session.Project, "Preprocess");
MRIResourceReference use = session.PlaceContent(parent, child, "Preprocess@StationA");
// Place 是定义期的使用位置；并不会自动使 MRK 执行子流程。
~~~

候选便捷创作体验应是 “搜索 Capability → 预览公开 Port 与前置条件 → 引用/映射 → 验证 → 提交”，底层仍调用已有 MPM/MPC/MOC/MTC，不另外增加可写目录或随意反射 DLL。

## 08. 错误与演进

错误必须定位 Definition/InstancePath/MemberId/CapabilityId/SchemaVersion/Source。公开接口变更区分：兼容扩充（可选字段/新非必填端口）；潜在破坏（必填、类型/单位、权限/副作用变化）；必须重新绑定（删除/改名/重排语义）；不支持/未知应阻止自动迁移。对外已发布契约不得因作者移动内部图元或改显示名失效。

## 09. 验收（CNT-T13～T24）

两来源同 Capability 合同一致；仅发现不动 Revision；未安装/版本缺失可解释；公开私有拒绝；错误单位/坐标/方向拒绝；双实例同定义且不同端口绑定；复合能力的输入输出保真；无逻辑 Content 不生成执行器；失败结果不可伪装 Completed；Type↔Capability 映射一致；对外契约升级分析与回滚可执行。
