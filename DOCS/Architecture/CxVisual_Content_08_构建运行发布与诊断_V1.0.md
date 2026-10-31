# CxVisual Content 体系 #8——验证、构建、发布、运行、诊断与治理 V1.0

> 目标合同；关联 CNT-R37～R42、R49～R54、R67～R72。决策/执行细节分别以 Mechanism.Decision、MRK 运行体系、Host、MDS、MHS 和专业安全规格为准；本专题只制定 Content 到这些 Owner 的真实交接。

## 01. 从定义到运行的九阶段

| 阶段 | 输入→输出 | Owner | 禁止误报 |
|---|---|---|---|
| 1 定义冻结 | MEM Snapshot/Revision + ResourceRefs | MEM | 可保存 ≠可执行 |
| 2 类型与依赖解析 | MPM/MPC/MOL/FCM/MFM + Version | Core/Decision | 找得到 ≠已安装 |
| 3 静态校验/决策 | 类型/约束/资源/目标/Unknown 与原因 | Decision | Allowed ≠真实设备 Ready |
| 4 构建 | Logic/Capability → Plan+SourceMap | MOC | 构建成功 ≠已发布 |
| 5 生产准入 | 版本/依赖/有效配置/安全权限 | Host/MHS/专业 | 审批≠真实执行 |
| 6 激活装载 | 固定 Plan/Resolver/Config/Deployment | Host/MRK | Staged≠Active |
| 7 触发调用 | Flow/Invocation/Frame/Lease | MRK/MDS/F19 | Accepted≠Completed |
| 8 观察与诊断 | Trace/State/SourceMap/质量/审计 | MRK/MDS/专业 | UI 看到绿色≠成功 |
| 9 停用/升级 | Drain/SafePoint/VersionPin/Rollback | Host/专业 | 软件取消≠硬件安全停止 |

简单静态 Content 可停在可视投影/数据订阅阶段，不强迫构建 MRK Plan。高价值工业任务每一层都必须提供可追溯状态/原因。

## 02. 基本身份与 SourceMap 追踪

~~~text
ProjectId / ContentDefinitionId / DefinitionVersion / Revision
  + ReferenceId / InstancePath / PublicMemberId / PortId / MOLBlockId
  + CapabilityId / CapabilityVersion / ConfigEffectiveRevision
       → BuildId / PlanId / SourceMap
       → HostId / FlowId / InvocationId / Iteration / StepId
       → DataRef / CommandOperationId / TraceId / Result
~~~

同一 Content 定义在工位 A/B、两个并发产品或 Loop 第 n 轮会产生多条运行记录。Trace 只读映射回定义时必须同时使用实例链/调用和迭代路径，不得仅凭 Block 名称或字典单键覆盖。

## 03. Decision 与 Runtime 的版本边界

Decision 只消费只读 Source、约束、专业静态能力和快照，给出候选、拒绝/未知/可解释原因及静态 Plan。真正的 Resolver、任务、线程、Lease、恢复、Journal 由 Runtime/Host/专业负责。当前合并后的 Mechanism.Decision 对 Runtime 仍存在过渡编译依赖，后续解耦须按既有 Owner 裁决处理，不通过 Content 另添一个 PlanWriter 绕开。

## 04. 发布、依赖锁与在途安全

发布包需冻结工程 Revision、资源包/Schema/Capability/Module 版本、配置有效修订、运行目标、必要权限/设备约束与来源指纹。对于正在运行的 Plan，新工程编辑并不替换其实际定义；更新须 Stage→验证→授权→SafePoint Activate，失败保留旧版或进入受控退化。外部设备 Unknown 状态不允许自动重试危险操作。

MHS Config Saved、Effective、设备实际回读分开；源 Content Public Port 发生破坏性升级，引用方必须升级/绑定/验证后方可发布。离线 MTI 包成功导出不等于远端机台已部署。

## 05. 错误与恢复的完整分类

- 定义错误：非法 Parent/Ref/Port/Schema/Aspect，原 MEM 事务拒绝且无半成品。
- 静态错误：缺能力/版本/单位/坐标/依赖、Plan 不可编译，返回 Def/Block/Port 与解释。
- 准入错误：权限/安全/Config Effective/Provider Ready 不通过，只能拒绝或降级为软件仿真，不自动写设备。
- 运行错误：调度拒绝、超时、取消、异常、部分完成、下游失败、资源耗尽、外部 Unknown 均需不同状态。
- 观察错误：Trace Gap、Source 过期、订阅断开/旧 Run 回调，不可用 UI 缓存掩盖。
- 恢复风险：Journal 重放需要校核外部动作执行结果和幂等令牌，不能把重启视为“重新做一次”。

## 06. 同图调试、诊断与权限

Studio/Workbench 用 MOCSourceMap 对原 Content Logic/Visual 视图做运行 Overlay；从 Trace 跳转 Content/Ref/Port、对应 Capability/设备和配置有效版本；从错误返回可修复的源定义位置。断点与单步必须遵守生产权限/安全点，不能默认停 PLC 或取消非合作硬件任务。关闭 Studio 不影响 Host 生产任务，远端 Trace 缺口明确显示 Reconnect/Unknown。

## 07. 性能与稳定性指标如何写进实施

每个黄金用例记录：首次 Build/增量 Validate 时间；Content 定义与 Ref 数量；嵌套层级及环检查预算；Flow 吞吐/P95/P99；MDS 内存预算/Handle 数与释放；UI Frame/DPI/多窗口；消息丢失/Trace Gap；跨进程重连与恢复结果；不同硬件配置。**阈值必须按专业目标/设备基线设定**，不能由本文凭空保证 30ms、1GB 或99.99%。

## 08. 交付/测试矩阵关联

E0 文档设计、E1 源码声明、E2 Headless 行为、E3 Windows CI/UIA、E4 HIL/生产。每次阶段验收记录确切 Commit SHA/分支/运行命令/工作流 URL/测试脚本/产物/失败原因；旧绿灯只说明旧 SHA。跨专业验证必须同时覆盖正常、坏类型、缺资源、断连、权限拒绝、超时、版本升级、多实例和清算，不可删除反例换通过。

## 09. 运行主线与设备主线的责任边界

专业能力不必通过 Content 才能在 C# 直接调用；只有声明编排/发布/运行治理目标时才通过该链。设备 PLC 安全联锁和硬急停不由 Content/Flow 图证明；MDS/DataHandle 内存所有权不由 Content Dispose 猜测；SourceMap/Trace 证据不以 UI 文本替代。若业务需求超出既有 MRK/专业合同，应在其 Owner 规格提交增强与跨层测试，而不是在 Content 内部补旁路引擎。

## 10. 验收出口（CNT-T55～T60、CNT-G05/G08）

一次保存/验证/构建/发布/运行返回分级状态；SourceMap 在两父多层/循环/并发下定位正确；已发布 Plan 不随工程草稿变化；错配置和无授权禁止实机操作；失败/取消不泄漏输出并保守清算；失联/旧 Trace 正确退化；远端恢复/设备安全需要独立 E4 实测后才能宣称工业交付。
