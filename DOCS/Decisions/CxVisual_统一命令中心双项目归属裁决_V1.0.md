# CxVisual 统一命令中心——双项目归属裁决 V1.0

> 日期：2026-10-24；用户明确裁决：命令中心相关类集中在基础一个、机制一个既有项目。
> 重构前基线：`27d36de98aefa9d6468a1667222c053e47fc2e45`；编译/专项回归以重构后具体提交的 Windows CI 为准。

## 一、唯一 Owner（不增加项目）

| 项目 | 目录 | 正式类/接口及职责 |
|---|---|---|
| `CxVisual.Foundation.Data` | `Foundation/Data/Commands/` | `FDCCommandDefinition`、`FDCCommandParameterDefinition`、`FDCCommandDefinitionCatalog`：命令定义、输入/输出统一Schema、只读目录快照 |
| `CxVisual.Foundation.Data` | 同上 | `FDCCommandValueSnapshot`、`IFDCCommandValueBinding`：值快照隔离、通用读写绑定接口 |
| `CxVisual.Mechanism.Runtime` | `Mechanism/Runtime/Commands/` | `MRKCommandCenter`、`MRKCommandRegistry`、`MRKCommandRouter`：入口、注册、查找、目标路由、请求受理 |
| `CxVisual.Mechanism.Runtime` | 同上 | `MRKCommandContracts` 内的 Request/Response/Options/Execution/Handler/Context/Policy/Interceptor/Metrics：操作生命周期与状态 |
| `CxVisual.Mechanism.Runtime` | 同上 | `MRKCommandMdsBinding`：用现有 MDS 变量/节点/FlowHeap Owner 做命令参数读写映射 |

**职责界限：** Foundation 不能引用任何 Mechanism；机制可以引用现有 MDS、MRK 执行与资源管理，但不新建第二个堆、变量仓、流程执行器或消息总线。专业 Automation/Vision/CT3D 等只注册 Handler，Studio/MOL 调用相同的 CommandCenter。

## 二、迁移与兼容

| 旧代码（物理删除） | 新代码（唯一编译处） |
|---|---|
| `Mechanism/Logic/Commands/MCECommandDefinition.cs` | `Foundation/Data/Commands/FDCCommandDefinition.cs` |
| `Mechanism/Logic/Commands/MCECommandDefinitionCatalog.cs` | `Foundation/Data/Commands/FDCCommandDefinitionCatalog.cs` |
| `Mechanism/Data/Variables/MDSCommandValueBinding.cs` | `Mechanism/Runtime/Commands/MRKCommandMdsBinding.cs` |

公开类型的变更：`MCECommandDefinition` → `FDCCommandDefinition`、`MCECommandParameterDefinition` → `FDCCommandParameterDefinition`、`MCECommandDefinitionCatalog` → `FDCCommandDefinitionCatalog`，命名空间由 `CxVisual.Mechanism.Logic.Commands` 改为 `CxVisual.Foundation.Data.Commands`。MDS适配器更名 `MRKCommandMdsBinding` 并放在 `CxVisual.Mechanism.Runtime.Commands`。

**注意：这属于未发布新平台的公开源码接口变更**；旧二进制调用方需重新编译，用户已指示优先收敛；未建立兼容双实现。

`Mechanism.Logic` 与 `Mechanism.Data` 项目仍留在28项目解决方案中，但其独立命令实现和适配器文件不再编译。MOL工程逻辑定义仍在原Owner，MDS变量和数据堆也仍在原Owner。

## 三、正式API示例

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Mechanism.Runtime.Commands;

// 以下位于一个 async Task 方法内。
FDCCommandDefinition definition = new FDCCommandDefinition(1, "Multiply", "倍乘",
    new[] { new FDCCommandParameterDefinition("X", typeof(double), true) },
    new[] { new FDCCommandParameterDefinition("Y", typeof(double), true) });

using (MRKCommandCenter center = new MRKCommandCenter())
using (center.Register(new MRKCommandDescriptor("algorithm", definition),
    new MRKDelegateCommandHandler((context, input, ct) =>
        Task.FromResult(new MRKCommandHandlerResult(
            new Dictionary<string, object> { { "Y", (double)input["X"] * 2 } })))))
{
    MRKCommandResponse result = await center.InvokeAsync(
        new MRKCommandRequest("algorithm", "Multiply",
            new Dictionary<string, object> { { "X", 3.0 } }));
}
```

## 四、实际验收门槛

1. 旧三处源码文件从Git仓库物理移除，新的两个项目中无重复类。
2. 更新对应四个 `.csproj` 的显式Compile项，移除 Runtime → Logic 多余项目依赖；不调整无关项目。
3. `Tests/ArchitectureSmoke` 原交互契约与命令中心专项测试引用新 FDC/MRK Owner，并继续测试参数、并发、MDS、广播、安全。
4. Windows：`CxVisual.Unified.sln` x64 Release Rebuild，运行 `--interaction-stage1`、`--command-center`、默认 ArchitectureSmoke；检查对应SHA CI成功证据。
5. **不在这次归属重构中冒充完成**：跨进程命令传输、真正设备联锁、Studio可视化编辑、持久任务恢复、授权集成。

## 五、后续路线

先完成本次两项目归一及CI稳定，再基于 `Mechanism.Runtime.Commands` 继续抽取确有独立价值的 Dispatcher/Executor、可靠生命周期与多场景使用入口，保持数据/工程/运行唯一真源。

## 六、双项目稳定后继续补齐的命令发现与版本门禁

命令专属代码依然只在两个既有项目内。Registry 对同一 Target 的 CommandId 强制唯一；Router 支持 `ResolveById(Target,CommandId)` 和 `Discover(commandId:, schemaVersion:)`。MRKCommandRequest 追加可选 `expectedSchemaVersion=0`（0 为兼容原默认、不执行版本检查；大于0时必须与发布的 FDCCommandDefinition.SchemaVersion 一致）。版本不符在 Handler 执行前返回 `Rejected/SchemaVersionMismatch`，`InvokeBoundAsync` 也透传版本约束。不把这个进程内契约检查称为已实现跨进程协商。
