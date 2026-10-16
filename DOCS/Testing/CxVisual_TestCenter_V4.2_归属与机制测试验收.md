# CxVisual TestCenter V4.2 — Owner 依赖验证与真实机制回归

日期：2026-10-16。依据 V2.1 《FMP子方面归属与公共接口依赖设计》，以当前 cx-visual-x3 的生产源码为事实依据。

## 本次明确实施

- `Build/Verify-OwnerDependencies.ps1`：遍历 CxVisual*.csproj 的全部直接 ProjectReference，识别Owner、重复引用、路径异常、Foundation/Mechanism向上依赖、生产库反向引用Tests、全部项目依赖环；生成 `Output/Debug/x64/DependencyAudit.csv`。不修改任何生产代码或引用。
- `TCT.MVIS.001`：通过 `CxSceneProjection` 对真实 CxProjectDocument 投影、HitTest、移动图元后的重新命中验证。
- `TCT.MVIS.002`：通过 `CxVisualEditor` 真实 Add/Undo/Redo，断言场景投影与 Revision。
- `TCT.MDATA.001`：通过 `MHSConfigService` 的独立机器Scope、保存/待生效/任务边界生效及冻结快照测试。
- `TCT.MDATA.002`：验证无效范围的 Preview/Commit 拒绝，不提升配置版本。
- `TCT.MDATA.003`：两套 Machine Scope 的配置不串用。
- `TCT.MDATA.004` ~ `006`：新的生产 MDEExecutionDataScope 父子隔离、关闭顺序、Owned资源逆序释放。
- `TCT.MRK.003` ~ `004`：新的生产 MRKExecutionPlanCompiler 解析真实 Delegate、稳定执行与错误诊断。

全部用例只调用真实 Owner DLL，使用 TestCenter 的独立沙箱与断言；通过 `ObjectTree`、`Geometry`、`Runtime` Observation 展示真实信息。

## Owner / 测试矩阵

| 实际Owner | 已有可测API | 现阶段测试 |
|---|---|---|
| Foundation.Geometry | CxRect2D/CxAffine2D | 已接入 |
| Foundation.Infrastructure | FLDLogService/FCRWorker | 已接入 |
| Foundation.Data | FCMConfig*、IFDEExecutionDataScope | 已接入；运行Scope契约以真实Owner为准 |
| Mechanism.Core | CxProjectDocument/事务 | 已接入 |
| Mechanism.Visual | CxVisualEditor/CxSceneProjection | 本次接入 |
| Mechanism.Data | MHSConfigService/MHSConfigSection、MDEExecutionDataScope | 本次接入配置与父子数据作用域测试 |
| Mechanism.Runtime | MRKExecutionEngine/Trace/PlanCompiler | 已接入；本次增加编译与错误诊断测试 |
| Mechanism.Orchestration | Node/Port/Connection完整生产图编排 | **现有项目主要是工程骨架，待Owner实现** |
| Mechanism.Connectivity | Channel/Protocol/Session 生产实现 | **现有项目主要是工程骨架，待Owner实现** |
| Foundation.Numerics / Transport / Volume | 通用算法/传输/体数据运行实现 | **需逐项检查并按实际API落地，不能从需求表推断已经实现** |
| 统一 DataSpace/Heap/Stack 的完整目标体系 | 完整管理器、租约、对象/列表/Map、序列化与数据堆栈 | **当前仅已有 IFDEExecutionDataScope/MDEExecutionDataScope 父子Scope及Owned释放合同；不可当作完整数据堆/数据栈已完成** |

## 交付门槛

Windows CI 结构检查→Owner DAG审计→NET48 x64 MSBuild→独立Worker全用例执行→并行重复与历史证据。无Windows实测不可写通过；设备与现场验收不包括在模拟沙箱测试中。TestCenter仍是一个项目，依赖被测程序集，不向生产导入ITCT接口。

该矩阵反映**已经实现的对象**与**规格要求尚未实现的对象**，不将仅有接口/Viewer的项目算成完整功能。