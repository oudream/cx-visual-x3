# CxVisual.TestCenter 基础/机制真实接口测试补充 V4.1

此文配套《CxVisual_TestCenter_FMP子方面归属与公共接口依赖设计_V2.1》及V4.0测试底座规格。它记录生产接口测试**能真实验证什么**和**尚未验证什么**，不替代生产Owner规格。

## 新增测试对应真实被测对象

| TestId | 所属Owner | 真实类型 | 断言与证据 |
|---|---|---|---|
| TCT.FCR.001 | Foundation.Infrastructure | FCRWorker | Invoke真实返回42、完成状态与快照统计；Runtime Observation |
| TCT.FCR.002 | Foundation.Infrastructure | FCRWorkHandle | Submit抛出的异常被封装为Failed并可读取；Runtime Observation |
| TCT.FLD.001 | Foundation.Infrastructure | FLDLogService | 写入、Flush、Snapshot及Dispose，独立沙箱；Runtime Observation |
| TCT.FCM.001 | Foundation.Data | FCMConfigCatalog | Attribute字段元数据、单位、检索与真实Markdown导出；ObjectTree Observation |
| TCT.FCM.002 | Foundation.Data | FCMConfigValidationEngine | 合法配置通过，[Range]非法范围报告错误；ObjectTree Observation |
| TCT.MRK.001 | Mechanism.Runtime | MRKExecutionEngine / MRKExecutionTrace | DAG依赖计算、真实FlowResult、Trace递增序号；Runtime Observation |
| TCT.MRK.002 | Mechanism.Runtime | MRKExecutionPlan | 编译循环依赖拒绝 |

新增源码: `Tests/TestCenter/Cases/TCTPlatformRealCases.cs`。它们都利用已存在的公共生产类型，没有引入临时业务Provider。每个TestId是独立黑盒测试；实际业务/专业设备能力并未因其通过而被验证。

## 依赖边界

单项目 `CxVisual.TestCenter`（WPF / NET48, x64）额外引用:
- `CxVisual.Foundation.Infrastructure`：日志与并发；
- `CxVisual.Foundation.Data`：配置定义、Schema与校验；
- `CxVisual.Mechanism.Runtime`：ExecutionPlan/Flow/Trace。

原有Geometry/Mechanism.Core引用保留。依赖方向只有Tests → 生产Owner；**生产Owner没有引用测试中心**。基础方面子项目并非都必须引用Infrastructure。

## 命名

所有自有测试类型 `TCT`，自有接口 `ITCT`；真实Owner的`FCR/FLD/FCM/MRK`前缀保持原样。CapabilityId暂以测试专用记录为主；未经审核不宣称已与能力规格表的正式ID完成映射。

## 验收与限制

Windows CI `TestCenter Windows .NET48`：
- 31项目、Debug|x64编译通过；
- 38项子进程独立执行全部通过；
- Repeat=3、MaxConcurrency=3，114 Attempt 全部通过；
- JUnit、History JSON自动归档。

这证明**公共测试底座及这批API输入输出可以回归**，不证明：
- Graph绘图/TypedPort/Compile的全部生产语义已接入；
- Channel/Protocol原始帧的实际收发与报文解码已接入；
- Runtime真实设备场景、生产并发压力、断线重试、现场运控安全验收；
- 运行界面WPF在生产机器上的完整人工可视和交互验收；
- 性能基准、长时间压力和所有Foundation/Mechanism能力清单100%覆盖。

上述都应在对应Owner生产能力真实存在时继续注册具体TestCase并接入相应Viewer，不应为填满功能表创造假数据。
