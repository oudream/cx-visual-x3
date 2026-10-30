# CxVisual 真编排 B1～B6 整体验收与实机边界 V1.0

> 2026-10-30；本文件是**验收入口/缺口台账**，不是替代 Core、Decision、MRK、MDS 或专业 Provider 的新架构。每条通过结论必须绑定真实固定 SHA 和 CI Run；后续 master 的改变须重新验证。
>
> 工程原则：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 判定等级

- **源码存在**：能从最新 master 找到 Owner 与真实入口；不等于测试通过。
- **Windows 自动化通过**：固定 SHA 的 .NET Framework 4.8 x64 CI 通过；仅覆盖 CI 实际运行的测试。
- **现场通过**：在目标机台使用实际设备/驱动、PLC、检测原图、MES 和安全链验证；必须另附测试记录，软件仿真不能替代。
- **未验证/阻断**：相关测试排队、失败、缺设备或没有原始记录。绝不可改写成“完成”。

## 2. 六包统一真源与验收入口

| 包 | 真源/Owner | 现有可执行验证 | 完整判定条件 |
|---|---|---|---|
| B1 工程真源 | MEM/MOL；MOCFlowBuilder | OrganizationLogicIntegrationTests；真实 .cxproj Save/Open | 同一 Project/Content、V1/V2、公开 Port、引用及 Revision 往返 |
| B2 计划交接 | Decision 静态 Plan；MRK 侧 MOCManifestRuntimeBinding | MDCDecisionTests；`--orchestration-vertical` | 来源闭包、静态合同、版本/权限失效拒绝，不能伪称完全脱离 Runtime |
| B3 专业执行 | Professional Vision；Runtime/MDS | G06CpuVisionThroughMOC；RuntimeVisionDataFlowTests | 真实软件算法输入/输出与 SourceMap；现场设备 Driver 单独验收 |
| B4 数据控制 | MRK/MDS/FCR | `--orchestration-industrial` | 取消、资源限流、断点、异常隔离、数据作用域和无泄漏 |
| B5 Studio 调试 | Studio WPF 消费 MRK Trace/SourceMap/有界数据摘要 | `--shell-self-test` 的 B5 与 P0-4 断言 | 活跃 Flow 断点、单步、继续、取消、可观察数据及错误工程拒绝 |
| B6 工业验收 | Runtime.Host、CI、现场 Owner | `unified-runtime-windows.yml`；20k MDS soak；真实 Host 子进程 Journal 反例 | 固定 SHA 全门禁、签名发布/防回滚/恢复；实机验证按第 4 节 |

## 3. 软件 CI 运行命令与证据

在 Windows x64 按真实解决方案构建完成后：
```powershell
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --orchestration-vertical
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --orchestration-industrial
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --runtime-data-soak 20000
```

- Workflow：`.github/workflows/engineering-organization-windows.yml`、`decision-windows.yml`、`unified-runtime-windows.yml`、`studio-shell-windows.yml`。
- Studio S1 必须执行真实 WPF STA 自测并检查 B5 调试断言，而非只统计总 PASS 数。
- Runtime.Host 真实独立进程必须验证：正常跨进程重启/恢复；Journal 缺失拒绝；未结案 Flow/外部状态未知拒绝；Journal 不被篡改；签名发布及防回滚。
- MDS 的 20,000 帧资源释放门禁验收生命周期和内存预算；**不能**单独用它证明设备吞吐量、采图速度或现场稳定性。

### 固定提交已取得的历史证据

- `7e8dda67`：Engineering、Decision、Runtime、MDS RuntimeData Windows CI 成功，含 B4 工业专项。
- `2790de87`：Studio S1 / Engineering 成功，含活跃 Flow 断点/单步/继续/取消。
- `3cdb6299`：Runtime 成功，含 MDS 20,000 帧长稳。
- `ae56ba57`：Runtime 成功，含 Host 五轮跨进程运行恢复。
- `98294ee3`：Runtime 成功，含 Host 未结案/未知外部操作 Journal 的独立进程拒绝反例。
- 新的 B5 运行数据摘要与 72 节点有界观察以**对应 CI 最终结果**为准；本文件编写时不得预先视为通过。

## 4. 实机验收矩阵：没有现场记录的项目维持未验证

| 现场场景 | 必须记录的证据 | 安全要求 |
|---|---|---|
| 真实采图及多探测器竞争 | 原始图像/元数据、Trigger/FlowId、时间戳、结果对应关系 | 拒绝跨产品串帧、重复触发和错误输出 |
| PLC 触发、反馈、断链、超时 | PLC 事件日志、握手序号、设备侧状态、MDS/MRK 轨迹 | 未确认动作不可自动重发或误判成功 |
| 运动控制与急停、安全门/联锁 | 控制器实际回读、硬件安全链记录、恢复前检查 | 软件停止不能替代硬件安全链 |
| MES 离线、超时、失败重试 | 请求ID、业务流水、确认ACK、结果及重试轨迹 | 未确认提交需人工/系统对账，禁止重复记账 |
| 运行中取消和任务切换 | 设备侧终态、MRK取消、资源租约释放、下一任务结果 | 对正在进行的实际动作不得伪造撤销 |
| 断电/进程崩溃与重启 | 原始 Journal、重启日志、外部实际状态、人工核对记录 | 未结案外部动作必须 Fail-Closed |
| 配置 Effective 与版本切换 | MHS Saved/Effective、Provider ReleaseVersion、签名发布记录 | 进行中任务不得混用新旧生效配置 |
| 长稳与吞吐 | 目标产线条件、批次、并发、内存/CPU/GPU、丢帧、错误率 | 由机台规格定义阈值，禁止凭空指定合格数 |

机台设备型号、驱动/SDK、PLC 协议、现场安全设计、真实异常注入权限及性能阈值由设备负责人提供并在测试记录中锁定。没有实际硬件时，只能完成**软件层验收**；不能声称实机联锁与工业生产验收通过。

## 5. 关闭规则

1. 以最新 master 的完整 CI 固定 SHA 记录所有需要的成功证据；CI 失败先修复，不用旧 SHA 成功掩盖新 SHA 失败。
2. 为每个现场场景保存执行者、设备序列/版本、日期、输入、预期、实际、日志和异常恢复结果。
3. 对真实设备相关项目保留“现场未验证”，直到设备负责人明确签署；不以模拟 Provider 的结果代替。
4. 真编排不新建第二套工程定义、运行数据、执行调度或设备控制 Owner；ReferTo 只作成熟实现复用来源。
