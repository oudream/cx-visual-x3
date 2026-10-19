# CxVisual 工程构建体系 V1.2 — 自由连线三阶段开发记录

> 范围：仅实施 V1.2 第9章约30分钟×3 的最小纵向闭环，并非32方面496项的完整平台交付。
> 基线：原有 MEM/MPC/MOL/MOC/MRK/MDS，不新增csproj/工程真源，master 中文提交。

## 阶段一：自由连线定义与文件往返

- Core 中的 `MOLBlockEndpoint`、`MOLLink`、`MOLWiringDefinition` 和 `MOLWiringCodec`。
- 采用同一 `cx.logic.structured` Logic Aspect 及SchemaVersion=2，保留 V1 结构化文件读写与原编译方式；编辑时原子替换，不同时存储V1、V2两份权威定义。
- Action块类型化Port定义、Control/Data连线验证、相同TypeId/计量参考检验、重复ID与非法方向/环路/悬空/缺必须Port的失败保护。
- 新增真实工程保存/重开、BlockId/LinkId稳定性集成测试。
- 验收：2026-10-19 Windows 工程组织CI success，记录于 https://github.com/oudream/cx-visual-x3/actions/runs/39202895166 。

## 阶段二：自由连线编译、执行与SourceMap

- `MOCEngineeringCompiler` 检测 V2 Codec，Control边生成依赖，Data边生成具名 MRK StepResult/StepOutput 输入绑定；不另建MRK执行器。
- V2持久文件重载后完成CPU三节点执行；Trace回指Content+BlockId。
- 拒绝未注册能力、不支持的有条件边、动态环、复杂FanIn（需要显式聚合器）；不将不支持的Flow语义偷偷当Sequence。
- 验收：2026-10-19 Windows 工程组织CI success，记录于 https://github.com/oudream/cx-visual-x3/actions/runs/39203209574 。

## 阶段三：开发者公共编辑API、WPF/Studio薄接续

- `MOCFlowBuilder.Edit(session,content,Action<MOCFlowBuilder>)` 将Action、Port、Link编辑写入同一V2 Content Logic，严格使用一次Core Session编辑事务及expectedRevision；非法操作不提交。
- Studio在原逻辑页签增加自由连接示例、添加能力、连线模式（选两块生成Done→Start连接）、Canvas V2实际连线投影以及基于原MOC/MRK的CPU仿真/Trace叠加。
- WPF STA验收加入 V2 自由连线真实FrameworkElement渲染。
- 这是“可编辑最小切片”，不是完整多Port拖放、参数属性面板、跨模型Workflow/StateMachine编辑器。

## 安全、兼容及继续工作的边界

1. **不动真实设备**：仅CPU模拟，不触发X-Ray/PLC/运动指令。
2. **不自动迁移旧V1**：V1仍可加载执行；Builder遇到V1要求显式转换或用户确认替换，不静默损失逻辑。
3. **V2首批限制**：仅Action与Control/Data有向无环连接；不支持条件分支、动态循环、Workflow、StateMachine、任意Data FanIn、复杂Join。保留G05后续各模型建设。
4. **Port运行值**：数据结果用StepResult或具名StepOutput绑定，运行实例数据仍由MRK/MDS负责；版本及身份随工程快照固定。
5. **Studio首版限制**：连线模式是固定Done→Start，不等于可自由选择所有类型化端口；尚需专业能力目录、参数/端口UI、完整撤销体验与十案例综合验收。
6. **测试范围**：ArchitectureSmoke真实V1/V2往返、编译、CPU执行、SourceMap、WPF STA。CI结论以对应提交实际Windows日志为准。独立TestCenter工作流可能存在其他项目的证据回放失败，不能冒充工程构建失败，也不能据此宣称全仓无故障。

## 下一阶段建议

继续原G01~G08路线，优先 G01 完整自由拖放与Port选择/参数映射、G02能力目录、G03错误定位和版本锁定、G04工程师真实交互；G05再处理大规模Region、DataFlow/Workflow/StateMachine。各阶段仍需 Windows CI + TestCenter真实证据。
