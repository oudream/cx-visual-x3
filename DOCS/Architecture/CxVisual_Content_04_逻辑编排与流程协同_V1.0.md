# CxVisual Content 体系 #4——逻辑定义、真实编排、子流程调用与运行语义 V1.0

> 目标设计；覆盖 CNT-R31～R42。唯一逻辑定义规范仍归工程组织 V1.2 的 MOL、Decision/MOC V2.0；真实执行语义归 MRK 运行体系。Content 为可复用编排的**边界**，而非另一个调度框架。

## 01. 一眼理解流程作用

Content 有责任、依赖、输入输出、作用域与版本；仅当有可执行 Logic 时才成为 Flow 的定义来源。Logic 的结构/连线定义通过工程的 MEMContent Aspect 保存；MOC 读取固定 Revision 编译为 Plan/SourceMap；MRK 使用独立 Invocation/Frame/资源调度实际运行。

~~~mermaid
flowchart TB
 C[MEMContent Public Contract] --> L{Has executable MOL?}
 L -->|No| V[Visual/Data/Static content: no Flow]
 L -->|Yes| S[Read one Logic Aspect from MEM Snapshot]
 S --> D[Decision/MOC Validate+Compile+SourceMap]
 D --> P[Fixed MRK ExecutionPlan]
 P --> R[Host + MRK Invocation/Flow]
 R --> X[Professional capability/resolver]
 R --> M[MDS Frame/Heap/Committed outputs]
 R --> T[Trace+SourceMap back to Definition/InstancePath]
~~~

不得让逻辑块修改 MEM 自身的父子树；MOLLink 不因为 Scene 坐标变化而变化；Reference 不因放在 Page 上就主动运行。

## 02. 唯一 Logic Aspect：V1/V2 现状与真实限制

- schema1：MOLLogicDefinition/MOLControlBlock，适合 Sequence、Branch、Loop、Try/Exit/Wait、StateMachine 等结构化区域。
- schema2：MOLWiringDefinition，适合 Blocks/Control/Data/Stream Links、端口与 FlowInputs/FlowOutputs、布局等自由连线表达。
- **同一个 Content 只能有一种权威载荷**；不得同时持有可写 V1/V2 或静默双向镜像；转换须显式语义等价验证/IdMap/Undo/Save/Open。
- MOCFlowBuilder.InvokeContent(MEMContent) 已有源码，会读取当前工程快照、V2 FlowInputs/Outputs；对于 V1 有公用 Port 描述的部分场景提供静态端口入口，但 V1/V2 跨 Schema 的最终真实运行编译能力不得不经测试直接宣称完整。
- V2 区域语义复杂时优先使用结构化 V1 Content 组合；确实需要原地编辑结构化区域时才提出最小类型化扩展，禁止复制 ControlTree+Graph 的两份执行顺序。

## 03. Flow 与 Content 的边界合同

| 问题 | Owner/合同 |
|---|---|
| 谁定义“做什么”？ | Content Logic Aspect（MOL Block、Expression、Port、Link） |
| 谁知道子流程有哪些公开输入输出？ | 子 MEMContent/公开契约与 V2 FlowInputs/Outputs |
| 谁检查缺失能力、资源和版本？ | Decision/MOC/专业静态描述 |
| 谁编译计划与返回原点映射？ | MOC 的 Build/SourceMap |
| 谁决定具体工作并发/取消/重试/等待？ | MRK + F19 + 专业受控策略 |
| 谁拥有 Frame、Heap、待提交/已提交结果？ | MDS，按 Flow/Invocation 作用域 |
| 谁能授权真实 PLC/相机/运动副作用？ | 可信 Host + 专业/设备安全 Owner |
| 谁呈现当前步骤及状态？ | Studio/Workbench 根据 Trace+SourceMap 投影 |

## 04. 需要支持的语义全景

控制：Sequence/Dependency、If-Else/Switch、Loop/Foreach、Parallel/Join、Try/Catch/Finally、Break/Continue/Return、Wait/Timer/Event、State/Transition/Guard、Call/Invoke、异常/补偿。数据：强类型输入输出、参数映射、Stream、局部变量、具名 Export、结果屏障、版本/质量。执行策略：超时、取消、幂等/重试禁止、资源、优先级/执行组、恢复与 Trace。**是否已实现要逐一标记**，文档罗列语义不代表 MRK 支持全部组合。

不要以“万能 Step”抹平设备等待、GPU 计算、图像流水线与 HMI 订阅的差异；专业提供能力，MRK 提供统一工作推进事实。

## 05. 三层子 Content 实际调用模型

~~~text
RootInspection Content
  Logic: Acquire → Invoke(Detect Content) → Invoke(Judge Content) → Export(Result)
Detect Content (shared definition)
  Public Input: ImageHandle, RecipeRef
  Public Output: Defects, Score
  Logic: Preprocess → AIDetect → Threshold → Export
Judge Content (shared definition)
  Public Input: Defects, PolicyRef
  Public Output: Decision
  Logic: Rule → optional Review → Commit
~~~

Root 拥有的是引用/调用关系，不是 Detect/Judge 的运行值。各子调用根据 ReferenceId/调用位置产生独立 Invocation/Frame；两个不同父 Flow 同时复用 Detect 时不得共享 Pending、循环计数、LastResult。未显式 Export 的内部值不可从父级读取。

## 06. 运行数据与失败传播（开发必须明确的契约）

1. 调用准备：解析固定版本、公开 Port/Schema 与必要输入；失败不能偷偷使用上轮值。
2. 调用进入：建立子调用身份、Frame/Scope/资源预算；外部调用可带 CorrelationId，但不以 Name 合并。
3. 传值：定义 Value/Handle 的复制、借用、Move、有效期、质量、单位与坐标语义；专业大对象不复制。
4. 成功退出：仅按定义将公开 Outputs Commit/Export 给父；子私有值不泄露。
5. 失败退出：Pending 不 Commit；异常/取消/超时/Unknown 分开；资源在实际终结后归还。
6. 追踪：SourceMap 固定 ContentId、InstancePath、Block/Port、Plan；Trace 记录 Flow/Invocation/Iteration/Step；一处定义多轮执行不能被字典最后值覆盖。
7. 恢复：外部危险副作用不因软件超时或重放而自动重复；必须核对设备最终状态/幂等合同后决定。

## 07. 触发、事件与多入口的边界

Content 可以定义可公开的业务操作/事件/数据响应，但**不可假定现有 MOL 每个 Content 已支持任意多个独立可编译 Entry**。第一阶段仍沿用现有合法入口与公开 Port，由已有 Flow/命令请求显式选择并调用；如真实案例证明需要多操作入口，应先在 #9/ADR 中评估“一个定义多个公开操作映射到多个 Flow 入口”或“多个子 Content Facade”两种方案，优先后者复用，禁止空造 ContentOperationEngine。

用户按钮请求 → Symbol Event → Content 已定义映射 → Command Host 授权 → Professional 动作 → MDS/Trace 状态反馈。编辑期端口连通仅证明静态关系，绝不能直接执行。

## 08. 复用、并行、迭代和安全

- 多级 Content：路径包括 Project/Def/Ref/Call/Iteration；递归展开环与非终止流程需单独验证。
- 并行调用：每个子 Invocation 独立 Frame，合流/取消余支按 MRK 实际完成状态清算；不能提前释放共享资源。
- 策略：Retry/Timeout/Compensate 优先使用现有 MOLStepPolicy/MRK/专业安全，不在 Content 复制一层“重试引擎”。
- Flow 引用真实设备动作时，只接受受控 Host 发布/授权；没有真实 Ready/回读不能标 Completed。
- Studio 关闭/失焦/换页，不影响 Runtime.Host 的生产控制流程。

## 09. 工程用户和代码用户同一语义

Studio：Content → Logic Tab → 搜 Capability/子 Content → 放置类型化 Block → Control/Data/Stream 连线 → 公开 Input/Output → Validate/Build → 模拟 → 同图 Trace/断点。C#：已有 MOCFlowBuilder.Edit(session,content, action) 和 MOLLogicCodec.Set/Get 路线，与 Studio 操作同一 Logic Aspect/事务；不要另建 UI Workflow JSON。实际签名、边界和反例从最新源码查证后引用。

## 10. 六种必须明确报错的不可行情况

V1/V2 非无损转换；缺必填/错类型端口；不存在或版本不匹配的 ContentRef；未公开私有输出；未授权/不可重试设备动作；不可表达的动态跨 Schema 区域。诊断必须提供 Def/Ref/Block/Port/原因/建议，而非悄悄转成无条件 Action DAG。

## 11. 验收清单（CNT-T31～T42）

同一定义两调用不同输入/结果；三级调用与保存重开；V1/V2 往返非等价拒绝；动态循环多轮与独立 Trace；分支/Join 正确；取消/失败未 Commit；事件请求无授权不得产生副作用；MOC 只读编译不改 Revision；同Plan并发Flow结果隔离；运行/SourceMap 双向定位；跨Schema已支持范围正证与不支持反证；旧流程回归持续通过。
