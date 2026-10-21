# CxVisual 工程呈现化体系——完整需求、角色期望与使用样例、总体详细设计 V1.0

> 版本：V1.0｜编制：2026-10-20｜性质：**目标规格与设计基线（不是实现/验收报告）**
>
> 范围：把原工程呈现化需求 EP01～EP15、此前 A/B/C 真实场景、开发者与工程用户期望，以及共享 Studio / F-M-P / 工程与运行统一原则整理为**一份可独立阅读、可供后续全类架构反推的文档**。
>
> 资料：`DOCS/Implementation/CxVisual_工程呈现化体系_30分钟分轮实施与验收_V1.0.md`、工程可视化 A/B/C 审计与 A+B 工作包、`DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`、当前 `Applications/Studio`、`Mechanism/Core`、`Mechanism/Visual` 及开发者示例。
>
> **事实界限**：本稿在现有 EP01～EP15 追踪索引基础上扩写为详细规格；原始用户需求全文未作为逐字源提供，不应把本稿冒称为该原文的无遗漏转录。标注“目标”的行为不等于仓库已有代码。R01～R04 有已提交实现但**未获得同 SHA 完整 Windows 编译、WPF STA 和端到端验收证明**。
>
> **2026-10-20 融合修订说明**：结合《临时聊天2》的 Studio 工作区决议和已核读的《CxVisual_Studio_一级菜单与上下文Ribbon设计规范_V1.0.docx》，保留本稿原有十五组需求、六角色与五场景，重点强化中央配置/图元/流程三种编辑器、九项菜单和专业 Ribbon、资源/对象分层导航、Docking、主题及协同验收。**Studio V1.1 最终完整原稿尚未获取，因此只融合已经确认的信息，保留待核差异。**本修订继续维护一份综合文档，不另建平行规格。

---

# 第一部分｜完整需求

## 1. 定义、目标和裁决原则

### 1.1 正式定义

**工程呈现化体系**是 CxVisual 统一工业工程平台面向用户的**工程表达、编辑、交互、观察和追溯能力**：将工程对象、图形、逻辑、数据、专业资源、构建结果、运行状态及调试证据，以相互关联、可编辑、可定位的界面呈现出来。

它**大于“画图软件”**：不止 Shape/Widget/HMI；它也**不是新的工程内核或运行内核**：不重复实现资源真源、逻辑语义、调度、执行、数据堆栈和专业算法。它为这些既有能力提供一致、可发现、低成本的使用方式。

完整用户闭环：

> 创建/打开工程 → 组织资源与定义 → 可视编辑/配置/编排 → 校验构建 → 仿真或授权运行 → 观察状态和数据 → 断点/Trace/定位 → 修改/重建/再次运行 → 保存/发布与审计。

### 1.2 最高目标与边界

平台理念：**“各方面都尽能力展现，便捷友好尽支撑各专业达到目标。”**

- **能力充分**：Foundation、Mechanism 和专业 Workbench 在各自 Owner 下完整呈现可复用的功能；不为了“最少类”削弱能力。
- **使用简便**：软件开发者调用清晰统一的核心 API；工程用户通过 Studio 完成真实工程，不要求理解底层 28 个项目的分工。
- **自由组合**：同一资源/属性/数据/逻辑可服务 HMI、Automation、Vision、CT3D、Inspection、Simulation；专业可扩充但不复制基础机制。
- **工程唯一**：MEM 的 Project / Page / Content / Symbol / Group / Element、ResourceRef、连接和稳定身份是工程事实；MPM Schema 统一属性语义；MTC 负责修改事务与历史。
- **语义/执行分离**：MOL 定义逻辑，MOC 验证编译并生成可执行计划及 SourceMap，MRK 执行/控制/调试，MDS 持有运行时数据，专业 Provider 与真实设备交互。
- **工程与运行隔离**：运行态帧、Watch、临时高亮、Trace、预览动画不得写入 MEM Revision；未经授权不得产生设备控制动作。
- **UI 不成为真源**：Studio / WPF 只负责用户交互、布局、编辑投影和呈现；不得建立第二持久化 Document、Graph、变量仓库或 Undo 栈。
- **真实验收**：有功能声明、有 UI、有测试代码、Windows 编译通过、自动测试通过、现场实机安全验证属于不同证据级别。

### 1.3 使用对象与适用场景

统一支持六个专业 Workbench：**Automation、Simulation、HMI、Vision、CT3D、Inspection**。SCADA、IoT、Motion、Robot、X-Ray 等可以由多个专业共同实现，不因其重要而强行升为新的一级 Workbench。

典型业务包括工业设备 HMI、PLC/运控调度、X-Ray 2D 在线/离线检测、AI/CV 算法组合、CT 采图与重建、三维可视分析、复测追溯、MES 集成和虚拟设备联调。

### 1.4 需求记法与验收规则

本部分保留 **EP01～EP15** 编号；每项按“能力要求 / 核心交互 / 失败边界 / 验收观察点”描述。**必须**属于目标行为，不自动表示已完成。跨项共用身份、Undo、版本、权限、诊断和保存重开；不为每项重复建工程真源。

## 2. EP01～EP15 详细需求

### EP01｜工作区、资源导航与工程生命周期

1. **EP01-01 工程操作**：新建、打开、关闭、保存、另存、导入导出、最近项目、错误文件处理；未保存更换工程需要确认，用户取消不得丢失更改。
2. **EP01-02 两层导航**：Project/Resource Explorer 展示可独立管理的 Page/Content/Symbol/Flow/专业资源；Outline/Object Tree 展示当前资源内部 Group/Element、流程节点、ROI 等；内部节点不自动变成顶层 Resource。
3. **EP01-03 多文档**：同一工程多个资源 Tab；打开、激活、关闭、脏标记、锁定、分屏/回到上次工作位置；多个专业编辑器共享工程身份。
4. **EP01-04 定义与实例**：从实例跳转到定义、查看全部引用、回到原实例，定位选中对象及所在页；Definition 的修改影响复用，Instance 覆盖只作用于当前实例。
5. **EP01-05 统一查找**：工程/资源/对象/变量/属性/文本/错误搜索，GoToDefinition、FindUsages、History、Back/Forward，支持跨画布双向定位。
6. **EP01-06 Workspace**：Workbench 切换只改变用户视图与工具供给，不另开第二工程；面板停靠与布局按用户/Workbench 恢复。
7. **EP01-07 版本可见**：明确展示 MEM Revision、未保存状态、已构建计划版本、实际运行计划版本；新编辑必须提示运行计划是否过期。
8. **EP01-08 编辑器分型**：资源选择应进入**配置组合面板**或其专业默认编辑器；进入图元资源内部采用**图元主画布**，进入逻辑/流程定义采用**流程主画布**；专业 Viewer 可扩展；资源不必一律使用绘图画布。
9. **EP01-09 组合配置布局**：多子对象资源支持 1×N/2×N/3×N 自适应卡片布局；常规桌面倾向 2×N，实际列数依复杂度与宽度决定，仍待 V1.1 细化。
10. **EP01-10 Shell 统一**：固定一级菜单、动态上下文 Ribbon、可停靠面板与多 Tab 管理；工程、运行和布局偏好必须分开保存，所有命令由原 Owner 执行。
11. **反例/验收**：保存重开资源身份和打开位置一致；资源删除被引用时禁止静默留下悬空引用；配置/图元/流程三形态切换不创建第二工程状态；断链、版本不符或面板重建可定位、可恢复。

### EP02｜图元、场景和资源画布

1. **EP02-01 原生九图元**：Line、Rectangle、Circle、Ellipse、Polyline、Polygon、Arc、Bezier、Path；曲线用真实几何参数与路径数据，不用图像替代编辑对象。
2. **EP02-02 扩展元素**：Text、Image、受限安全 SVG、Group、Symbol、Widget 与专业 Viewer 占位；支持复用、层次、嵌套坐标和引用。
3. **EP02-03 画布辅助**：网格、标尺、参考线、选中框、旋转/缩放手柄、控制点、Snap 可视反馈、缩放/平移与世界坐标查看。
4. **EP02-04 精确表现**：保留线帽/拐角/填充规则/透明/绘制顺序/剪裁/嵌套变换，高 DPI、不同屏幕缩放下命中与视觉一致。
5. **EP02-05 资源容错**：图片、字体、SVG 缺失显示稳定占位与诊断；大资产不得直接复制成运行帧多个工程节点。
6. **反例/验收**：九 Shape 全部创建、修改、保存、重开、命中一致；恶意 SVG 外链、脚本、DTD、过大数据被拒绝或安全降级。

### EP03｜精细图形编辑与工程事务

1. **EP03-01 基础操作**：创建、重命名、单选/多选/框选、移动、旋转、缩放、等比约束、对齐、分布、吸附、裁剪、图层前后置。
2. **EP03-02 路径编辑**：Arc 角度/半径/扫向、Bezier 锚点/控制柄、Path 多子路径、开闭、段转换、插入/删除节点与局部编辑，确保稳定几何语义。
3. **EP03-03 组织操作**：Group/Ungroup、创建可复用 Symbol、Definition 编辑、实例摆放与复用、剪切复制粘贴、Duplicate、图层锁定/隐藏。
4. **EP03-04 选择一致**：Canvas、Outline、Property、专业预览同一对象/引用身份；批量操作中不允许意外退化为“仅最后一个被选对象”。
5. **EP03-05 修改权威**：拖动过程可暂存交互预览，完成时统一由 MEM/MTC 事务提交；一次用户动作应对应易理解的 Undo 单元；事务失败不部分修改。
6. **EP03-06 快捷操作**：多层快捷键路由和冲突处理、Esc 取消、键盘精调、上下文菜单、Undo/Redo 后视图与模型同步。
7. **EP03-07 Undo 边界**：图元/流程画布的持久化编辑必须走原 MEM/MTC 可撤销事务；配置组合面板不强制逐键输入构建完整 Undo，但提交后仍遵守原工程/配置 Owner 的事务及历史规则。
7. **反例/验收**：非法几何、不可选对象、锁定对象、跨不同父级组合、过期 Revision 和中断手势都不污染工程。

### EP04｜统一 Schema 属性编辑

1. **EP04-01 Schema 来源**：只读取 MPM Descriptor / 专业注册的受控元数据，支持 String、Number、Integer、Decimal、Boolean、Enum、Color、Brush、列表/字典、结构/资源引用等目标类型；不能在 UI 另建第二份“真”Schema。
2. **EP04-02 元信息**：名称、类型、默认值、必填、范围、枚举候选、单位、格式、说明、来源与只读性按 Schema 呈现；平台通用信息由公共编辑器承担，专业属性贡献扩展模板。
3. **EP04-03 绑定来源**：常量、变量、工程资源、对象成员、受控表达式/配置来源；来源切换可见、保留来源不可被编辑器静默改为常量。
4. **EP04-04 定义/实例**：定义值、Schema 默认、实例覆盖必须分清；实例恢复默认等于清除覆盖继承定义，定义恢复默认等于撤销显式绑定。
5. **EP04-05 多选**：同 Schema 的可编辑属性可以批量设置；不同值呈现 Mixed（数字/文本不填假值，Boolean 三态）；一次操作使用单个事务，Undo 一步恢复各对象原值。
6. **EP04-06 可编辑边界**：私有定义属性不向外部实例暴露；只读属性需 Core 执行校验、UI 同步禁用；过期/跨工程/类型冲突拒绝，不以按钮禁用代替机制。
7. **EP04-07 错误与搜索**：即时格式提示、提交时权威验证、定位失败对象/字段；大量属性支持过滤分组、搜索、帮助与按域展示。
8. **反例/验收**：两个 Symbol 实例独立属性，任意超范围、非法资源、混合值误提交、跨定义错误选择均不变更 Revision；保存重开还原值和来源。

### EP05｜样式、主题和工程素材

1. **EP05-01 共性样式**：填充/描边、线宽/线型/线帽、透明、字体、渐变、图片画刷、视觉层、样式复制及复用。
2. **EP05-02 主题**：Theme 资源、Token、样式引用与预览；允许专业主题/状态色板，但不能在每个 Workbench 自建一套全局主题仓。
3. **EP05-03 最终值解析**：默认 → 定义 → 实例 → 状态 → 动画构成目标优先级；运行帧临时覆盖只影响呈现，不写工程。
4. **EP05-04 素材**：静态资产受 MEM 资源身份与内容打包管理，支持导入、替换、查找引用、依赖、缺失提示、尺寸/类型限制与缓存释放。
5. **反例/验收**：主题切换和状态叠加不破坏源值；复制 Symbol 不错误共享可变实例状态；不执行 SVG 脚本/远程引用。

### EP06｜真实交互 Widget 与组合控件

1. **EP06-01 基础 Widget**：Button、TextBox、CheckBox、Slider、ProgressBar、ComboBox 映射为真实 WPF 可交互组件，不是仅绘制控件外观。
2. **EP06-02 属性**：尺寸、布局、文案、范围、选项、状态、Enabled、ReadOnly、焦点、Tab 顺序、编辑回显及样式。
3. **EP06-03 双向交互**：用户输入、程序值更新、校验反馈和变更来源区分；不得把程序刷新误判为用户触发设备命令。
4. **EP06-04 复合控件**：Panel、Tab、List、Tree、Table、Chart、Trend、Gauge、Alarm、Dashboard 等由基础能力组合和专业扩展，不强迫全部沉入工程核心。
5. **反例/验收**：设计/预览/运行下行为区分，焦点与绑定稳定，非法输入、控件销毁后的订阅回收和重复加载可复测。

### EP07｜运行数据绑定、状态与动画

1. **EP07-01 可绑定对象**：变量、端口、配置、表达式、专业数据、能力输出、图元属性和 Widget 属性；必须有实例上下文与可追踪来源。
2. **EP07-02 数据质量**：类型/单位/方向/格式转换、空值、默认、时间戳、质量、超时、失联与过期处理可配置并可见。
3. **EP07-03 单向/双向**：渲染方向与用户输入方向按契约区分，运行源禁止未经许可反向写入。
4. **EP07-04 状态机制**：状态条件、优先级、组合与冲突解决，图元/Widget 可根据 MDS 实时数据更新外观及文本。
5. **EP07-05 动画机制**：持续时间、延迟、循环、反向、缓动、停止/恢复、状态切换；运行帧不污染项目 Undo/Revision。
6. **反例/验收**：两个电机实例接不同数据源，同一 Symbol 显示不同值/状态，任一断线不串到另一实例；Preview 回 Design 后恢复定义外观。

### EP08｜事件、交互和授权命令

1. **EP08-01 事件来源**：鼠标、键盘、Widget、状态、定时器、生命周期与专业事件，带事件来源、时间、实例身份、用户动作/程序动作区别。
2. **EP08-02 动作出口**：导航、属性变更、命令请求、流程调用、专业命令、诊断定位；参数与权限统一校验。
3. **EP08-03 安全**：Design 禁止真正设备动作；Preview 默认仿真；Run/Debug 通过 MRK/专业 Provider 和授权/联锁/审计后才可发控制动作。
4. **EP08-04 事件链**：路由、节流、取消、重复点击/重入、错误反馈、Trace 身份映射。
5. **反例/验收**：无授权、掉线、互锁不满足或异常处理时绝不“假成功”；模拟事件不当作设备指令验收。

### EP09｜逻辑流程编排与专业资源关系

1. **EP09-01 同源定义**：MOL 逻辑语义通过工程原有对象/引用承载；画布节点的坐标是编辑元数据，不是调度顺序和第二 Graph 真源。
2. **EP09-02 多级流程**：主流程、子流程、子子流程、多个专业子流程及跨专业调用；支持入参、出参、上下文、异常和取消语义。
3. **EP09-03 节点与连接**：从已注册能力/专业节点工具箱拖入，支持类型/方向/单位/多重度约束、连接提示、参数映射。
4. **EP09-04 逻辑要素**：条件 if/else、Switch、循环、并行、等待、事件触发、重试、超时、异常、补偿/终止；不同类型语义要准确区分而非仅有图标。
5. **EP09-05 导航**：展开/折叠、嵌套浏览、双击调用进子流程、面包屑返回、HMI关联跳转、上下游影响定位。
6. **反例/验收**：调用不存在的子流程、参数缺口、循环无退出保护、错单位、跨资源越界均有构建前可定位提示。

### EP10｜验证构建、诊断和发布边界

1. **EP10-01 MOC 分工**：审核工程引用、类型、端口、能力、逻辑控制边界、权限依赖等，生成当前 Revision 对应的可执行计划及 SourceMap；**不是设备调度器**。
2. **EP10-02 构建入口**：Studio 一键校验、构建进度、错误/警告列表、定位对象及修复建议、增量影响范围。
3. **EP10-03 版本锁**：Project Revision / 定义与实例版本 / 能力依赖版本 / 编译计划指纹可追踪；工程新修改后标记已运行计划“过期”，不默默热替换。
4. **EP10-04 目标区别**：模拟计划与真实生产发布流程、授权、安全点、回滚语义明确，不将“编译通过”等价于“可操作现场设备”。
5. **反例/验收**：缺能力、错 Port、版本不符、映射遗漏能跳到主/子流程具体节点，绝不产生可误用的“成功计划”。

### EP11｜运行、仿真和工程呈现

1. **EP11-01 MRK 运行控制**：启动、停止、暂停、继续、取消、超时及故障恢复；界面显示实际运行会话和计划版本。
2. **EP11-02 真实状态观察**：运行块状态、实例运行状态、调用关系、MDS 输入/输出、节点栈、全局堆、端口数据和专业结果按权限订阅。
3. **EP11-03 HMI 运行帧**：统一源驱动图元、Widget、状态动画、告警，不把实时数据反写工程。
4. **EP11-04 Simulation**：虚拟设备、回放、仿真时钟、故障注入、隔离实例与一致可重现的测试输入。
5. **EP11-05 可靠性**：线程调度、取消与释放、UI 降采样、窗口关闭时订阅清理、大流量数据按句柄而非盲复制。
6. **反例/验收**：运行失败/超时必须如实呈现；重开 Studio 不能凭空复用已失效会话；暂停不应绕过专业设备实时性和联锁。

### EP12｜统一调试和故障追踪

1. **EP12-01 断点**：条件断点、命中、暂停、继续、单步进入/越过/跳出；MRK 真控制状态决定能否执行，不由图形按钮自说自话。
2. **EP12-02 Watch**：局部数据、输入/输出、全局数据、堆栈、调用链、质量与时间，按快照/实时流及权限区别。
3. **EP12-03 Trace**：执行时间线、异常与耗时、CallPath、节点 SourceMap、父/子映射、与 HMI/专业对象互相跳转。
4. **EP12-04 调试后修复**：定位到定义 → 修改工程事务 → 重新构建 → 明确停旧换新/授权切换 → 再次执行；不得热改尚在用的不可变计划。
5. **反例/验收**：从故障 AI 子流程节点找到调用它的主流程、对应检测工位和 HMI 状态灯；找不到时显示“缺映射证据”而非跳错对象。

### EP13｜专业 Workbench 和扩展边界

1. **Automation**：设备/通道/PLC/轴/IO/Robot/触发时序及点检等专业资源与编辑器。
2. **HMI**：Page、Symbol、Widget、Binding、State、Animation、Dashboard，复用通用 Scene/属性/事件框架。
3. **Vision**：图像、ROI、传统算法/AI、模型、标定、任务编排、结果图层和专业 Viewer。
4. **CT3D**：投影/几何/校准/重建/Volume、三视图/MPR/三维 Viewer；体数据用专业数据契约/句柄和 Viewer，不当成普通 Shape。
5. **Inspection**：产品/工位/点位、检测方案、判定、复测、审核、结果与 MES 追溯。
6. **Simulation**：Virtual Device、Replay、Fault Injection、Scenario/Test；复用统一运行与日志机制。
7. **验收**：增加一个专业 Editor/Viewer 不增加第二工程树、第二属性真源、第二调试器或第七个“专门为本行业”的 Workbench。

### EP14｜软件开发者可用的公共 API 和扩展

1. **EP14-01 工程 API**：项目、资源、定义/实例、稳定身份、引用、属性、事务、保存加载、查询/变更订阅。
2. **EP14-02 绘制与编辑 API**：Geometry、Shape、Group、Symbol、样式、素材、Widget、选中/变换/碰撞/路径操作；核心不依赖 WPF。
3. **EP14-03 逻辑与运行 API**：MOL 声明/关系、MOC 构建结果、MRK 命令/会话/调试、MDS 数据只读观察；权限由原 Owner 执行。
4. **EP14-04 专业注册**：Descriptor/Schema、Editor/Viewer、工具箱/Top Command 贡献、专业校验、Provider 和安全能力注册。
5. **EP14-05 易用性**：常用场景应由少数清晰步骤达成，错误诊断包含对象身份和修复提示；不能逼每个专业重写 Shell 与基础交互。
6. **EP14-06 Studio 专业贡献接口**：专业注册受控 Editor/Viewer、配置模板、Schema属性编辑模板、Toolbox/Ribbon/菜单命令与诊断定位适配；生命周期由统一 Shell 管理，不直接持有主窗口或新建持久化真源。
6. **验收**：有可编译的独立开发者示例，且从代码生成的工程能在 Studio 打开、编辑、保存并与原工具链共用模型。

### EP15｜质量、安全、可靠性和证据

1. **EP15-01 正确性**：事务原子性、Undo/Redo、保存重开、跨资源引用、稳定 ID 与 SourceMap、失效资源、版本冲突与并发修改。
2. **EP15-02 性能**：复杂画布、重复 Symbol、超多属性与流程节点、大量状态刷新、长时运行、CPU/GPU/内存/句柄/DPI 实测；指标须由专业场景和机器基线确认，不凭空承诺固定 FPS/延时。
3. **EP15-03 安全**：不可信 SVG/XML/媒体、权限、配置和秘密、错误脚本/表达式、无授权命令、工程版本与运行计划隔离。
4. **EP15-04 可诊断性**：错误具备对象、成员、来源、计划版本、调用链、时间、严重度及可定位跳转；日志和 Trace 可导出审计。
5. **EP15-05 测试等级**：纯 Core/Geometry 单测、真实 WPF STA、跨专业 API/Studio 联合、Windows 构建/CI、压力测试和真实设备现场授权验收分级记录。
6. **EP15-06 质量门**：所有场景的“设计/运行/调试”不能通过 mock 日志或示例按钮来签收；每项附真实 SHA、执行环境、命令、结果、失败反证和剩余问题。

## 3. 跨需求的统一易用性标准

- 一个工程、一套资源导航、一套属性机制、一套项目事务、一个可定位诊断中心；专业差异以贡献而非复制实现。
- **Top 是统一操作入口**：菜单、Ribbon、快捷键、上下文命令由统一命令路由，按当前 Workbench / Resource / Selection / RunMode 动态贡献；不把所有专业命令堆成永远可见的一排按钮。
- **左侧资源编辑体验**：跨 Canvas / Outline / Properties 同步选中；资源/对象分层，Definition/Instance/References 明确；可拖动或双击到对应专业 Editor。
- **多资源并行**：同一工程 HMI、逻辑、AI 检测、CT Viewer 可以同时打开 Tab/Dock；保持跨视图导航回退历史、Dirty 和焦点。
- **面板停靠**：Project Explorer、Toolbox、Properties、Variables、Diagnostics、Watch、Trace 按上下文展示、停靠、浮动、自动隐藏，布局按用户/Workbench 保存恢复。
- **少步骤、不藏能力**：高级功能通过上下文、搜索、更多属性和专业贡献呈现；简单动作默认路径短，复杂功能不因“界面简洁”被阉割。
- **配置/图元/流程三种主编辑器**：选中资源与进入资源内部定义是不同操作；相同 Shell、不同编辑形式，具体遵循 §12§，其中配置组合面板不强求画布式 Undo。
- **两体系分工**：Studio 统一界面体系提供 Shell、导航、菜单、Ribbon、Docking 与编辑器宿主；工程呈现化负责其上的资源/图形/逻辑/运行/调试真实能力，避免重复架构。

---

# 第二部分｜角色期望与完整使用样例

## 4. 六种角色与其成功标准

| 角色 | 想完成的事 | 必须感受到的简便能力 | 最怕的失败 |
|---|---|---|---|
| **R1 平台/基础开发者** | 接入可复用通用能力，明确 Owner | 稳定 Core API、统一事务、可测试的 headless 能力、少量适配 | 为功能建第二工程树、第二运行仓、交叉依赖 |
| **R2 专业开发者** | 把 X-Ray/AI/CT/PLC 专业能力交给工程师 | 声明 Schema、工具箱、Editor/Viewer、能力执行契约即可进入共享 Shell | 每个专业从零开发属性框、资源管理、调试、权限 |
| **R3 工程设计/编排用户** | 无需写底层代码完成复杂工程 | 拖放/编辑/绑定/主子流程/构建/导航/批量修改/诊断一体 | “有按钮但不生效”，多层跳转迷路，专业配置散落 |
| **R4 运行操作员** | 看清设备、检测状态、操作授权指令 | HMI 实时、报警、权限、工位/批次/产品、可明确停止与反馈 | 显示旧数据当实时、预览误发指令、告警无法追溯 |
| **R5 调试维护工程师** | 快速定位故障并复现、修复、恢复 | 从 HMI 异常走到流程子节点、变量/堆栈/Trace、可回退重试 | 日志只有字符串，计划/工程版本对不上，调试影响生产 |
| **R6 测试/质量/交付人员** | 确认工程确实可运行且安全 | 实际案例、仿真故障注入、可重复回归、版本与证据链 | 只验演示图、旧CI成功冒充当前版本、现场联锁未验证 |

角色通用要求：**能发现 → 能理解 → 能操作 → 能得到真实反馈 → 能追溯 → 能复现**。专业开发者提供“什么可用”，平台提供“如何便捷调用”，工程用户只操纵专业概念而非机制内部类。

## 5. 使用样例 U1：软件开发者——创建真实可复用工程

**目标**：用现有 MEM/MPM/Visual API 创建电机图元与两个实例；保存后由统一 Studio 继续编辑，而不是生成 Studio 私有格式。

**前置**：使用当前 `CxVisual.Mechanism.Core`、`CxVisual.Foundation.Geometry` 真实接口；下列代码是已核对现仓 API 的**工程定义段示意**，需放入已引用 Core 的 C# 项目，文件路径由调用者提供。完整可运行参考：`Examples/VisualAuthoring/EngineeringVisualQuickStart.cs` 和 `Examples/EngineeringOrganization/H03ContractsQuickStart.cs`。

```csharp
using (MEMEngineeringSession project = MEMEngineering.Create("产线工程"))
{
    MEMPage page = project.AddPage("监控首页");
    MEMSymbol motor = project.AddSymbol("电机组件");
    project.AddShape(motor.RootGroup, "机身",
        MRCShapeDefinition.Rect(100, 60), 0, 0);

    project.DefineProperty(motor,
        new MPMPropertyDescriptor("设备名",
            MPMValueKind.String, false, true,
            MPMBinding.Literal(MPMValue.String("未指定"))));

    MRIResourceReference a =
        project.PlaceSymbol(page.RootContent, motor, "电机A");
    MRIResourceReference b =
        project.PlaceSymbol(page.RootContent, motor, "电机B");
    project.SetOverride(a, "设备名",
        MPMBinding.Literal(MPMValue.String("MotorA")));
    project.SetOverride(b, "设备名",
        MPMBinding.Literal(MPMValue.String("MotorB")));

    if (!project.Validate().IsValid)
        throw new InvalidOperationException("工程校验失败");
    project.SaveAs(filePath);
}
```

**预期行为**：两个实例引用同一 Symbol 定义；修改 A 的公开覆盖不影响 B；打开该 `cxproj` 能找到资源定义与实例，查看 Schema、Undo/Redo、保存重开；UI 不重新持久化一套 Shape 数据。

**专业能力扩展示意（目标契约，不是已存在的完整 SDK）**：专业开发者向平台注册可见 ResourceDescriptor + Schema + Editor/Viewer + Toolbox/Command 贡献 + 逻辑 Port/能力 + Runtime/Provider 安全策略。注册成功后资源在 Project Explorer 出现，操作在 Top/右侧按上下文出现，构建与调试复用 MOC/MRK。

**反例**：定义成员标为私有/只读而实例仍能改、UI 编辑无 Undo、资源已删但仍保存成功，均不符合预期。

## 6. 使用样例 U2：工程用户——工业电机 HMI 从设计到运行（B 场景）

**任务**：建立“产线设备总览”，设计一个电机 Symbol，在页面放入“电机A / 电机B”，绑定不同运行源；状态灯和操作按钮能正确显示与受控请求。

| 步骤 | 工程用户操作 | 系统真实责任与可见反馈 |
|---|---|---|
| 1 | 新建工程和“设备总览”页面 | MEM 创建 Page/Resource，Top 显示 Dirty/Revision |
| 2 | 在画布创建电机机身、文字、状态灯、Button/Slider | MRC Geometry/Style + 真实 WPF Widget 投影；Outline 选中同步 |
| 3 | 选择图元成 Group，再“转换/创建 Symbol” | 单份 MEMSymbol 定义；编辑 Definition 不创建多个副本 |
| 4 | 通过右侧 Schema 声明/编辑设备名、速度、运行状态 | MPM 统一属性、类型、默认、公开、只读、范围；错误可定位 |
| 5 | 从资源树放置两次，改实例名与属性 | 两个 MRI 实例 ID；A/B 只共享定义，不共享可变覆盖 |
| 6 | 绑定 A→MotorA.Running、B→MotorB.Running | MDS/专业来源按实例解析，展示连接/质量/时间/失联 |
| 7 | 设置 Fault/Run 状态与闪烁动画 | 运行帧求值，绝不修改工程 Revision |
| 8 | 设置 Button 事件为“请求启动” | Design/Preview 不发送设备指令；Run 经授权/联锁与专业 Provider |
| 9 | 预览并注入 A 故障、B 正常，再保存重开 | A 变红、B 正常；数据/事件不串线，订阅可清理 |
| 10 | 构建后授权运行并检查 Trace | MOC 计划版本可见，MRK/专业侧确认动作结果，日志可返回到该实例 |

**Studio 界面操作链补充**：资源树选择电机配置→中央配置组合面板；进入电机 Symbol 定义→中央图元主画布→右侧 Schema/Style；返回 HMI 页面放置两次→选择实例显示对应覆盖与 MDS 绑定。Group/Element 属于该资源的 Object Outline，不被重复包装成顶层资源；从实例跳到共享定义后可返回原页面与位置。

**验收场景 B**：一个电机 Symbol、两实例、各自状态与变量、Theme/状态动画、真实 Widget、合法命令/拒绝命令、保存重开、Revision 不被运行帧污染，**缺一项不签收**。

## 7. 使用样例 U3：X-Ray 2D 检测工程——主流程与多个专业子流程（C 场景）

**任务**：将设备采图、图像预处理、传统算法、AI 检测、综合判定、MES 和 HMI 放在同一工程，工程用户从页面即可定位流程故障。

**工程资源树（概念性组织）**：

```text
产线检测工程
├─ HMI／检测总览、工位详情、报警与复测
├─ Automation／探测器、触发控制、PLC、MES连接
├─ Vision／图像预处理流程、CV缺陷算法、AI缺陷模型
├─ Inspection／产品方案、点位、判定规则、复测策略
├─ Logic／检测主流程、AI子流程、CV子流程、复测子流程
└─ Simulation／虚拟PLC、图像回放、故障注入用例
```

这里的文件夹是 **Studio 导航分组/专业视角建议**，真实存储应受原 MEM Resource/Content 组织能力约束，**不得因为该示意新增持久化资源类型或第二层 Graph 真源**。

**主流程**：工件进入 → 读取 SN/配方 → 检查状态与触发条件 → 探测器采图 → 图像预处理 → 并行/顺序调用 AI 子流程与 CV 子流程（由逻辑配置决定）→ 汇总缺陷 → 产品/点位判定 → MES 回传/异常重试 → 更新 HMI 和 Trace。

**AI 子流程**：入参图像句柄/模型版本/ROI/任务上下文 → 推理调度 → 缺陷列表与置信度 → 异常与耗时信息。模型不应被复制进每个图元；大图走专业数据句柄。

**CV 子流程**：入参图像/ROI/检测参数 → 预处理 → 测量/几何阈值 → 缺陷结果 → 诊断与性能数据。

**工程用户完整操作**：
1. 在 Project Explorer 新建/打开上述资源，使用专业 Toolbox 选择采图、算法、判定与通信能力。
2. 在中央逻辑页签创建主流程；双击调用块进入 AI、CV 子流程，设置入参/出参 Port、类型、单位、映射；可返回主流程且保留选择历史。
3. 在中央 HMI 页签放置工位灯、结果表、报警控件，绑定到对应运行输出；在右侧选择 ROI、模型、算法参数。
4. 点击 Top “校验/构建”：MOC 对端口缺口、能力版本、引用作用域等给结构化诊断；点击错误定位到具体子节点。
5. 在 Simulation 切换回放图像与虚拟 PLC，运行 MRK，观察主流程与 AI/CV 子流程节点、输入/输出、堆栈、Watch 和结果 Viewer。
6. 在 AI 子流程故意给出不兼容模型/坏参数；通过 Trace/SourceMap：**AI失败节点 → AI调用块 → 检测主流程 → HMI工位状态**。
7. 修改参数 → MEM 事务更新 → 再构建 → 明确发布/切换运行计划 → 重跑 → 比较结果；保存重开保持映射和定义。
8. 真实机台启动另经设备授权/安全互锁确认，不能因仿真通过自动发送运控/曝光指令。

**Studio 界面操作链补充**：资源树先选检测方案/设备算法配置，中央显示配置组合面板；进入主流程后切成流程画布；双击 AI/CV/复测子流程分别打开独立 Tab；图像结果通过专业 Viewer 查看。活动 Tab 控制专业 Ribbon 和右侧属性，底部 Watch/Trace 对同一实例显示数据。故障可按 SourceMap 回到 AI 子流程节点→父流程调用→HMI 工位；不能用几张孤立的可视化截图代替真实导航。

**验收场景 C**：必须有**一张主流程 + 多张独立专业子流程**、实际可引用的 Port、MOC 计划、MRK 观察与 Trace 定位、HMI 双向追踪、错误后完整重跑证据；一张“看起来复杂的编排图”不能代替执行事实。

## 8. 使用样例 U4：CT 采图/重建/可视分析的专业 Viewer

**任务**：在同一工作区设置 CT 采图任务、设备/几何校准、投影采集、重建流程、三视图 MPR 与体绘结果；从结果异常定位到原始投影/几何/运控环节。

- 工程用户：选 CT 工程资源 → 配置采图与校准 → 在流程编辑器排列 Acquisition/Correction/Reconstruction/Post-process → 构建、仿真、授权运行 → CT Viewer 查看轴向/冠状/矢状与三维 → 追踪当前重建任务的 SourceMap/Trace。
- 专业开发者：声明图像/Projection/Volume 数据契约（尺寸、像素/体素格式、坐标系、内存所有权、生命周期、错误码）；专业 Viewer 用 MPR/3D 适配，数据采用句柄/共享内存等受控方式传递。
- 平台边界：Scene 的 Shape 与 CT 的数 GB Volume 不采用同一种可变元素数组存储；CT3D 仍共享 Project、资源身份、Tab、诊断、构建和调试基础体验。
- 验收：打开/切换 Viewer 时不复制巨量体数据到 MEM；重建参数/结果有稳定工程与运行版本关系；异常可由 Viewer 定位到任务/输入源和运行节点。

## 9. 使用样例 U5：调试维护与质量验收人员

**故障演练**：“某工位 HMI 亮红灯，AI 判定服务失联”。

1. 操作员点击 HMI 故障态 → 查看报警详情（实例/时间/数据质量/计划版本），未授权用户只能观察。
2. 维护人员打开 Trace → 查看主流程调用链、AI 子流程节点、关联输入/输出和完整错误来源；Watch 不直接修改生产堆栈。
3. 选择回放数据或隔离的仿真会话 → 复现故障 → 比较修复前后输出 → 重新构建/验证变更影响。
4. 发布/恢复需满足生产权限与设备安全边界；错误的模型版本/缺 Port/超时均应明确拒绝。
5. QA 固定工程版本、计划版本、案例输入及结果证据，记录 Windows CI 与现场机台测试各自状态；最终验收不能仅写“截图漂亮”。

---

# 第三部分｜总体与详细设计

## 10. 体系总览：一套工程，多个专业呈现，多个运行引擎

### 10.1 统一架构视图

```text
[软件开发者 API]                           [工程用户 Studio]
       │                                         │
       └─────────────────┬───────────────────────┘
                         ▼
          [统一 Workbench Shell / 命令路由]
       Top Ribbon | Project Explorer | Resource/Outline
       Editor Tabs/Docking | Properties | Diagnostics/Watch
                         │ UI 只做命令与投影
           ┌─────────────┴─────────────────┐
           ▼                               ▼
     [设计/工程世界]                    [运行/观察世界]
 MEM唯一工程树+ResourceRef             MDS运行时数据/质量/快照
 MPM Schema/属性/绑定                   MRK会话/执行/断点/Trace
 MTC受控事务/Undo/Redo                  专业Provider/Viewer
 MOL逻辑与专业流程语义                       ▲
           │                                │
           └─────► MOC验证、编译、计划 ──────┘
                    SourceMap/版本
                   ▲              ▲
          Foundation通用能力    Professional能力实现
         Geometry/Data/Math等  Automation/HMI/Vision/CT3D/
                              Inspection/Simulation
```

**设计定案**：工程呈现化是穿越这些既有 Owner 的**共性用户体验/适配体系**，不是新增一个持有所有状态的超级核心项目。**Studio 是界面容器和命令/停靠规范，工程呈现化负责容器里真实的工程能力及生命周期**，同一 Resource/Definition/Instance/RunSession 和 SourceMap 贯穿两者，禁止“工程可视化 Shell”和“Studio Shell”重复建设。

### 10.2 项目、责任与依赖

| 层与 Owner | 权威责任 | 不允许承担的责任 |
|---|---|---|
| Foundation.Geometry / Foundation 其他基础子方面 | 点、矩形、曲线、Path、坐标/变换、色值/基础算法等纯数据与计算 | WPF 事件、MEM 工程事务、现场设备通信 |
| Mechanism.Core（MEM/MPM/MTC/MOL 对应已有子方面） | 组织、引用、Schema、逻辑语义、工程事务、持久化、身份 | 宿主控件状态、实时变量堆或真实设备驱动 |
| Mechanism.Visual | 场景投影、图形编辑、可视事件语义、运行帧呈现转换 | 第二工程文件、第二 Undo 栈、设备直接控制 |
| Mechanism.Presentation.Wpf | SceneView、真实 WPF Widget、鼠键命中/焦点、专业 Viewer 宿主适配 | 持久化业务真源或独立的可执行流程 |
| Applications.Studio | Workbench Shell、导航、命令上下文、停靠布局、属性编排 UI、调试观察 UI | 不经 MEM/MOC/MRK 私改工程/运行 |
| MOC/MRK/MDS 现有 Mechanism Owner | 构建计划/SourceMap、运行控制与调试、运行数据堆栈/观测 | 用画布节点坐标控制真实执行；把运行快照写进 MEM |
| Professional 各 Workbench | PLC/X-Ray/AI/CT/检测等专业 Resource/Schema/Viewer/Provider/安全规则 | 平台通用的第二个 Project/Undo/Trace 系统 |

保留既定 .NET Framework 4.8 + WPF、现有 28 项目与 Owner 依赖 DAG。为获得简单 UI 可以增加**薄适配对象**，但不能凭界面需要随意增加实体项目或复制领域真源。

## 11. 核心对象、身份和生命周期设计

### 11.1 Resource 与内部 Object 分离

- **Resource**：可独立管理、保存、引用、编辑并复用的工程单元；由 MEM 合法 Resource/IMEMResource 与专业声明映射。
- **Embedded Object**：Group、Element、ROI 内节点等，归所属资源与原父级拥有；只在当前资源的 Outline/Editor 展示。
- **Reference/Instance**：用稳定 ID 指向定义并携带允许的覆盖/摆放；删除/替换需查引用与影响范围，不能凭名称连接。
- **ViewState**：选中 ID 集、视口缩放/偏移、面板停靠、激活 Tab、编辑游标是用户工作区状态，**非 MEM 资源真源**。
- **Runtime Observation**：MDS 值、MRK Trace、快照、诊断结果属于运行侧或观测会话，有 SourceMap 指向工程身份但不反写定义。

### 11.2 三种版本必须分别显示

1. **工程定义 Revision**：由 MEM/MTC 提交与历史变化控制；“未保存”需对照磁盘状态。
2. **构建计划版本/指纹**：由 MOC 绑定对应工程 Revision、能力/依赖版本，错误构建不得标为可执行。
3. **运行实例/会话版本**：由 MRK 报告实际执行的计划及独立执行数据；工程继续编辑不应悄悄改变该运行实例。

工程资源与运行实例并非一对一；值对象/只定义资源/需编译资源/外部 Provider 服务分别按专业契约决定生命周期。

### 11.3 定义、实例覆盖与只读

有效属性值按目标规则从 Schema 默认、定义绑定、实例覆盖进行解析；运行状态覆盖另进呈现计算。**清除实例覆盖 ≠ 将默认值复制进每个实例**。

同一 Symbol 双实例可以拥有独立数据源、状态、位置和公开属性。私有成员对实例不开放。`MPMPropertyDescriptor.IsReadOnly` 已见于当前源码的 R04 变更；必须由原 MTC/Core 拒绝非法显式写入，而不是只用 WPF Disable。版本/身份过期时必须整体拒绝事务。

## 12. Studio 统一界面架构与工程呈现化的协同设计

> **融合依据与局限**：本章承接《临时聊天2》关于 Studio V1.1 的已恢复决议，并核读《CxVisual_Studio_一级菜单与上下文Ribbon设计规范_V1.0.docx》。**尚未取得《CxVisual Studio 统一界面架构与完整设计规范 V1.1》的完整目录/最终原文**，所以不冒称已按其逐章合并。已定原则、当前讨论倾向和待核事项分别列明。Studio 统一界面体系主责 Shell 与布局；工程呈现化体系主责工程能力如何被有效呈现和使用。

### 12.1 协作边界：Studio Shell 与工程呈现化不是两套平台

**Studio Shell 负责**固定菜单/上下文 Ribbon、导航框架、Document Tab、Dock/Float/AutoHide、通用编辑器宿主、Workspace 布局偏好、命令与键盘入口。**工程呈现化负责**把工程资源、配置、图元、流程、构建、运行、数据和调试能力接入这些容器，完成可编辑、可保存、可执行、可定位的闭环。二者必须共用 MEM/MPM/MTC/MOL/MOC/MRK/MDS 和专业 Owner。

共同上下文包括：`ProjectContext`（工程/Revision/Dirty）、`WorkbenchContext`（专业工具贡献）、`ResourceContext`（资源及编辑器类型/Definition-Instance）、`SelectionContext`（对象/混合多选）、`RunContext`（Design/Preview/Run/Debug/计划与权限）。这些仅是**上下文职责概念**，后续全类架构应优先复用真实类，而不是照名称新增平行状态对象。

### 12.2 整体布局和六个可复用容器

| 布局 | 组成 | 呈现化协同规则 |
|---|---|---|
| **顶部** | 固定九项主菜单、通用工具、上下文 Ribbon、保存/构建/运行/调试及版本状态 | 一个 Command 经实际 Owner 执行；按活动资源/模式/权限切换 |
| **左侧** | Project/Resource Explorer、当前资源的 Object Outline、Toolbox，可用多个面板 Tab | 资源树不直接列 Group/Element；对象树只负责当前编辑资源内部对象 |
| **中央** | Document Area；配置组合面板、图元主画布、流程主画布；专业 Viewer 扩展 | 多 Tab、同一工程和对象 ID；编辑器切换保留上级/返回上下文 |
| **右侧** | Properties、Binding、Style、State、Animation、Event、专业参数 | 统一 Schema/绑定/定义实例覆盖，按选择的资源与对象显示 |
| **底部** | Diagnostics、Build、Runtime Data、Ports、Watch、Stack、Breakpoints、Trace、Logs、TestCenter | 按 SourceMap 与运行实例精确定位，分清工程与运行帧 |
| **布局与主题** | Dock/Float/AutoHide、布局保存/恢复、浅色/深色模式 | 工作区偏好不污染 MEM Revision；浅色参照 TestCenter 白底、深色参照 VS 深灰 |

原聊天提到“主页结构参照图4、整体风格参照图1”；由于当前没有取得那几张图的最终标注版，**布局关系可承接，视觉像素细节仍待 Studio V1.1 原图复核**。深色不是一律蓝黑，需符合既定 VS 深灰取向。

### 12.3 资源树与对象树：不同层级、同一对象身份

1. **Resource Explorer**：Project/Workbench 下可独立创建、引用、保存或打开的 Page、Content、Symbol、Flow 及专业 Resource。专业文件夹可以是导航分组，不自动变成新的持久化 Graph。
2. **Object Outline**：当前 Resource 内 Group、Element、Flow Node、ROI、图层和连接；与中央 Editor 互相高亮/选择，避免 Group/Element 充斥资源树。
3. **点击资源节点**：自动激活/打开其匹配的配置组合面板或专业编辑器；**进入图元/流程定义**才切换对应主画布，不必把一切资源都打开为画布。
4. **Definition/Instance**：从页面中的 SymbolRef/ContentRef 进入共享定义、查全部引用，再返回实例原页/位置；原 ID、继承/覆盖、引用关系不丢失。
5. **跨画布导航**：从诊断/Trace → 主/子/子子流程节点 → HMI 页面/实例；跳转依赖稳定 ID 和 SourceMap，而非标题字符串或屏幕位置。

### 12.4 中央编辑器三种主形态与专业 Viewer

| 形态 | 何时出现 | 需要实现什么 | 工程真源与 Undo |
|---|---|---|---|
| **A 配置类组合面板** | 当前资源为设备、参数、检测方案、算法配置或组合资源 | 多个配置子面板、属性、引用/集合、校验、专业配置项 | 原 MEM/配置 Owner；按真实提交事务，不建独立 Form 仓 |
| **B 图元主画布** | 进入 HMI Page、Symbol 或图形资源设计 | Shape/Group/Widget/样式/绑定、图层、鼠键、手柄 | Geometry/Visual 投影 + MEM/MTC 工程事务、Undo/Redo |
| **C 流程主画布** | 进入主流程/专业子流程、逻辑节点设计 | 节点、Port、控制/数据关系、参数映射、进入/返回 | MOL/MEM 定义为真源，MOC/MRK 决定构建与执行语义 |

**专业 Viewer 属于可插入的 Editor/预览能力**：Vision 图像/ROI、CT 的 MPR/Volume、Inspection 结果、运行 Trace 可以在中央 Tab 或配置组合中加载 Viewer。它们不是第四套公共工程树；体数据、算法运行结果不被强塞进 MRC Shape。

**操作直觉**：资源树选“探测器配置”→ A；进入“设备总览页面”→ B；双击“AI 子流程”→ C；打开“重建结果”→ CT 专业 Viewer；可以同时保留多个 Tab。

### 12.5 配置组合面板：1×N / 2×N / 3×N 与提交边界

- **初步布局偏好是 2×N**：常规桌面双列组合；窄窗口、长表单可 1×N；宽屏/短卡可 3×N。列数不是硬编码的唯一答案，也不是已经最终定案的 V1.1 规则。
- 同资源内多个子对象/子配置通过卡片标题、折叠、筛选、分组、定位和校验组织；避免弹出大量孤立窗口。专业只贡献配置表单/Schema，Shell 提供宿主和共同上下文。
- 保存与取消明确区分**字段草稿、已经按 Owner 事务提交的工程值、来自 MDS 的运行观察值**。
- **Undo/Redo**：图元与流程的持久化可视编辑明确应可撤销。配置类组合面板**不强制每次键入、焦点移动都要有独立 Undo 记录**；但已经写入 MEM 的修改仍应符合原 MTC/配置机制的提交和撤销规则，绝不在 UI 另做一套历史。

### 12.6 主菜单九项固定、上下文 Ribbon 动态贡献

已核读的 Ribbon V1.0 正式建议固定一级菜单为：

**文件 ｜ 编辑 ｜ 视图 ｜ 工程 ｜ 调试 ｜ 运行 ｜ 发布 ｜ 工具 ｜ 帮助**

| 一级菜单 | 全局稳定职责 | 专业协同 |
|---|---|---|
| 文件 | 新建/打开/保存/另存/导入导出/关闭 | 不承载算法或设备控制按钮 |
| 编辑 | Undo/Redo、复制粘贴、删除、查找 | 当前 Editor 决定可撤销的工程动作 |
| 视图 | 缩放、面板、布局、主题、窗口 | 图元/流程/Viewer 按上下文贡献视图选项 |
| 工程 | 资源/依赖/配置、验证、构建 | MOC 接构建命令，MEM 执行修改 |
| 调试 | 仿真/回放/断点/Watch/单步/Trace | MRK 真接口和安全权限 |
| 运行 | 运行会话、启动/停止、结果/状态 | 设备操作经 MRK/专业授权/互锁 |
| 发布 | 构建成果版本/制品、签名、部署、回滚、记录 | 与构建、生产运行分开 |
| 工具 | 选项/扩展/测试/开发者工具 | 不堆积所有专业功能 |
| 帮助 | 文档、关于、诊断指南 | 不修改工程或设备 |

“绘图”由早期永久一级菜单改为 **HMI/Screen/Symbol 上下文 Ribbon**；Vision、CT3D、Inspection 及 Automation 下 Motion/Robot 等随活动 Document/Workbench 提供专业 Ribbon 页签，**不把 Motion/Robot 因为拥有 Ribbon 而当成新增一级 Workbench**。

统一命令路由：`活动 Project + Workbench + Resource/Editor + Selection + RunMode + Permission → 可见/可用/诊断 → 原 Owner 执行 → UI 结果与刷新`。菜单、Ribbon、快捷键和右键共享同一命令语义；禁用须说明原因；不直接让 Ribbon 操作 Vendor SDK。

**三个维度不能混淆**：九项菜单是信息架构；`Design / Preview / Run / Debug` 是工作模式；`Build / Publish` 是工程生命周期动作，并非第五、第六工作模式。

### 12.7 多 Tab、Docking、布局恢复与颜色主题

1. 支持面板停靠、浮动、自动隐藏、Tab 合并、分组/关闭/重开、默认布局恢复；中央可以并排打开 HMI、主流程、AI/CV 子流程、Viewer。
2. 当前激活 Tab 决定专业 Ribbon、工具箱、Schema、底部 Watch/Trace 的焦点，关闭 Tab **不得删除对应工程资源**；跨工程切换时释放旧观察订阅与 Viewer 句柄。
3. 用户 Workspace 布局与工程持久化分开存放；保存布局不应增加 MEM Revision，资源/编辑操作则必须服从原 Owner 的工程保存。
4. **实现状态更新（S1）**：此前 AvalonDock 曾讨论 4.x/5.x 选型；最新 `Applications/Studio` 已接入 **Dirkster.AvalonDock 5.0.1** 与 `MainWindow` partial Shell（见 §26），但真实 Windows 运行、主题一致性、许可证确认及全路径人工验收仍需同 SHA 证据。
5. 浅色以 TestCenter 白底为基调、深色以 VS 深灰为基调；需涵盖菜单、焦点、禁用、混合值、错误/告警、专业 Viewer 和高 DPI，不仅换背景。

### 12.8 资源→编辑→构建→运行→调试的统一交互

`资源树选择 → 选择Editor类型 → 激活Document Tab → Shell发布上下文 → 左/右/底面板协同 → 原Owner提交/查询 → 其它视图重投影`。

- **工程编辑**：Schema 校验/权限/Revision 由 MEM/MPM/MTC 决定，ViewModel 只维护投影和未提交草稿；非法输入和过期资源不能产生半个事务。
- **跨编辑器共享身份**：选择内部 Node 与选择 Resource 不同；Definition/Instance、多个子流程、数据端口的来源要始终明确。
- **构建**：MOC 返回版本化验证/计划和 SourceMap；构建成功不代表设备发布/授权运行成功。
- **观察**：MRK/MDS 返回真实会话/数据质量/时间戳/Trace；“运行观察”属性不能误当“定义编辑”，运行帧不修改 MEM。
- **故障定位**：从子流程/专业算法失败节点跳到父流程调用、所属工位及 HMI；导航 Back 保留来源 Tab/选择上下文。
- **生命周期**：关闭 Tab、切换工程、退出 Run/Debug 清理订阅、Viewer 与旧运行实例；操作失败给出可定位诊断，不伪造成功。

### 12.9 两类完整 UI 路径与实体验收

**示例 I：电机配置与 HMI**

资源树选“电机设备配置”→中央 A 组合面板（建议 2×N）→配置通信、变量、设备参数→进入“电机 Symbol”中央 B 图元画布→右侧设置样式/Schema→放两个 Symbol 实例→观察不同 MDS 值→从实例入定义再返回→预览不发真实设备指令→保存重开与 Undo/Redo 确认。

**示例 II：X-Ray 主流程与 AI/CV 子流程**

资源树选择“检测方案”→中央 A 组合面板→双击“检测主流程”进入 C 流程画布→进入 AI/CV/复测子流程的各个 Tab→选图像结果激活专业 Viewer→底部 Build/Watch/Trace 显示真实诊断/执行信息→点击子流程失败项按 SourceMap 回父流程调用/HMI 工位→修改工程→新 Revision/MOC计划→再执行；不能只做孤立截图或虚构 Trace。

**UI 验收基准**：三形态真实可切换；中央和左右/底上下文同步；固定菜单与专业 Ribbon 不冲突；导航回退正确；布局重启可恢复；Undo/Redo、保存重开和运行帧分离；拒绝无权限设备动作；Windows WPF STA 和真实场景验证。现有 EP01–EP15 / R01–R04 有代码不等于此处全部实现。

### 12.10 待和 Studio V1.1 原稿核对的决议

| 问题 | 已讨论的方向 | 目前不可冒称已定案的细节 |
|---|---|---|
| 主页结构与风格 | 主页结构参考图4、总体风格参考图1；深 VS 灰 / 浅 TestCenter 白底 | 最终图纸、尺寸、主题 Token |
| 配置组合面板 | 1×N/2×N/3×N，2×N 为倾向 | 自动排列阈值、配置类全量种类 |
| Undo/Redo | 图元/流程重点支持、配置组合不强制逐键历史 | 各配置 Owner 的编辑提交颗粒度 |
| Docking | S1 已用 AvalonDock 5.0.1 接入 Studio | 真实 WindowsCI、主题/许可证与多屏/浮动稳定性仍待核 |
| 一级菜单与 Ribbon | V1.0 已核读九菜单+专业上下文 | V1.1 最终命令/分组清单及变更 |
| 两体系接口 | 本稿明确 Shell vs 工程能力边界 | 后续与 V1.1 最终逐章核对/ADR |

## 13. 图元编辑、场景投影与专业 Viewer 设计

### 13.1 单一几何计算源

Foundation.Geometry 负责数学/Path/Transform/命中基础；Mechanism.Visual 将定义和实例解析成无 WPF 的 SceneItem/操作结果；WPF SceneView 负责绘制和输入。多层嵌套时保留定义坐标、实例局部坐标、场景世界坐标、屏幕 DPI 变换链。

**编辑协议**：命中 → 检查权限/锁/上下文 → 交互预览 → 计算真实几何变更 → MEM/MTC 原子提交 → 新 Revision → Scene/Outline/Properties 重投影 → Undo/Redo 使用同一历史。Esc/失焦/错误结束预览不得提交脏数据。

### 13.2 复用和专业 Viewer

基础 Shape/Text/Image/Widget 使用通用场景层；HMI 图表、Vision 图像/ROI、CT MPR/体绘可使用专业 Viewer 实现适配。对外仍是同一稳定 Resource ID、调试与运行计划映射；不能强迫 3D Volume 进入二维 Path 数据结构。

## 14. 属性、绑定、主题和交互设计

### 14.1 属性编辑流水线

`Selection → Resolve Definition / Instance → MPM Schema + 有效绑定 → UI模板/元信息 → 用户编辑/提示 → MEM MTC验证/事务 → Scene重投影`。

- 根据 Schema 类型选 UI：Boolean 复选框/混合三态，Numeric 带范围提示，Enum 下拉，Resource 选择当前工程资源，复合值由专业安全 Editor 注册。
- 编辑器显示“当前来源、继承默认、实例覆盖、有无混合值、只读”。
- 当现有能力不足以支持所需 Schema（List/Map/Brush/Expression 等），必须明确标记“目标/缺实现”，不得直接把它当成字符串保存而破坏类型。
- 异常/过期/非法跨工程资源引用由 MEM/MPM 最终验证；即时 UI 提示只是提升体验。

### 14.2 样式解析与主题

目标求值：`Theme token / 默认 → Definition style → Instance override → 状态样式 → Animation临时帧`。每层有“查看来源”能力；Style 编辑写工程事务，预览和状态动画只更新投影帧。

### 14.3 绑定与运行观察

- 绑定定义保存“从何处取值和所需契约”，**不是**保存运行时值。
- Runtime Adapter 在当前工程实例上下文按类型/质量/时间戳解析 MDS 与专业来源，形成只读 UI Frame；可批量、限频、撤销订阅。
- 双向 Widget 需明确用户输入与程序更新来源；写入需经过权限/专业 Command 或允许的配置事务，不能直接写 MDS/设备共享内存。
- 当前运行数据无效时显示 Disabled/Unknown/Stale/Error 等有意义的可见状态，不用旧缓存伪装正常。

### 14.4 事件请求与执行链

`用户/程序事件 → 捕获与路由 → 实例身份/参数/权限 → Command/逻辑请求 → MRK/专业安全策略 → 真执行结果/拒绝 → 诊断/Trace/HMI反馈`。

所有真实设备命令与 UI 输入解耦；无授权或实时性不满足时拒绝。高速 CT 运控触发/曝光等需要专业实时控制卡/设备侧同步，不经 WPF 鼠标事件承担硬实时控制。

## 15. MOL 编排、MOC 构建、MRK 执行与调试的统一呈现

### 15.1 职责链

| 阶段 | 唯一语义 Owner | Studio 提供什么 |
|---|---|---|
| 定义逻辑 | MOL / MEM 工程中承载的专业资源 | 节点/Port/关系编辑、主子流程、参数对照、导航、诊断 |
| 建立计划 | MOC | 校验/构建入口、进度、结构化错误、计划版本、SourceMap 可视化 |
| 执行控制 | MRK + 专业 Provider | 已授权的 Start/Stop/Pause/Resume/Cancel，运行状态和失败反馈 |
| 运行数据 | MDS / 专业数据服务 | 局部栈、全局堆、端口、HMI/Widget/Viewer 只读投影与质量信息 |
| 调试追溯 | MRK Trace / SourceMap | 断点、Watch、调用栈、子→父→HMI跳转、时间线、重新定位 |

**MOC 不是执行调度器**：它把合法工程定义验证为执行计划；**MRK 不是逻辑设计器**：它执行和观察经批准的计划；**Studio 不是运行主控**：它发受控请求并呈现实际结果。

### 15.2 执行和调试操作语义

1. 用户切换/选中流程时，画布按资源 ID/逻辑节点 ID 呈现，不将屏幕位置作为程序顺序。
2. Build 显示工程 Revision、目标环境、依赖指纹与诊断；构建成功后由 MRK/专业授权发布，不因“Build绿”直接驱动设备。
3. Run/Simulation 选择模式和会话；不同模式的硬件权限边界在 UI 和 Core 同时可见。
4. Watch/Trace 保留工程 SourceMap、实例路径、嵌套调用路径（主→子→子子）、事件时间；定位失败应显示缺失的地址。
5. 调试修改需形成新 MEM Revision，MOC 生成新计划。旧会话是否可切换受原 MRK 与专业安全点决定，不允许 UI 直接替换正在运行的对象图。
6. 大量事件和高频帧通过有界缓冲、降采样、时间窗口呈现；日志审计源应保真，UI 批处理不允许抹掉必要故障证据。

## 16. 四工作模式及状态转移

| 模式 | 工程修改 | 可做操作 | 绝不能做 |
|---|---|---|---|
| **Design** | 允许 MEM/MTC 事务 | 图元编辑、Schema、绑定/事件定义、逻辑编排、构建 | 以设计点击发送设备现场命令 |
| **Preview** | 不允许运行帧改工程 | 交互预览、仿真数据、状态/动画检查 | 将程序刷新冒充真实现场控制或修改 MEM Revision |
| **Run** | 按生产安全策略限制 | 执行已授权的编译计划、状态/告警/数据观察 | 用新编辑覆盖正在运行的旧计划 |
| **Debug** | 调试目标与编辑资源分离 | 断点、单步、Watch、Trace、错误定位与安全复现 | 绕过 MRK 会话或专业联锁调试真实设备 |

**Build/Publish 是阶段化工作流，不是第五个工程模型模式**。切换模式必须检查未保存/未构建/计划已过期/会话未释放/未授权情况；返回 Design 后临时状态不得滞留到源定义。固定菜单的“调试/运行/发布”是**不同功能入口分组**，不能错误地当成三个新增运行模式。

## 17. 专业扩展的集成方式

1. **注册发现**：专业声明可编辑 Resource/能力的稳定类型、Schema、工具箱、Editor/Viewer、Top 命令、诊断/校验与运行适配。
2. **编辑契约**：专业 Editor 获取受控工程读快照与原 MEM 事务操作，而非直接改内部集合。
3. **数据契约**：专业图像/Volume/点云/设备数据使用符合生命周期的 DataHandle、版本、单位/坐标和错误契约；MDS 负责相关运行观察，UI 不接管所有权。
4. **执行契约**：MOC 校验专业能力引用，MRK 运行经授权计划，专业 Provider 处理真实设备及安全互锁。
5. **诊断契约**：专业返回结构化 Issue/Trace，具工程对象 ID、流程位置、运行实例、数据质量；可由统一 Diagnostics UI 定位。
6. **兼容与测试**：专业扩展缺失时显示诊断占位，能够查看工程但禁止不安全编辑/运行；专业卸载/资源失效释放会话与 UI 订阅。

**禁止**“每新增一个工作台就复制一套项目树、图元库、属性保存、流程调试器”；共享能力要尽量供给充分，专业仍可按语义深度发挥自身能力。

## 18. 安全、性能和诊断的落地设计

### 18.1 原子性与可靠性

- 每次工程修改通过 MEM/MTC 乐观版本和事务，非法输入不修改 Revision；跨实例批量操作任何一项失败时整体回滚。
- 预览与运行数据通过只读帧/快照给 UI；窗口关闭时取消订阅，线程调度异常不泄漏跨会话数据。
- 同名不同 ID、跨工程 ResourceRef、已删除资源、版本过期或私有 Port 不允许 UI 自动“猜对”。

### 18.2 安全边界

- SVG/XML/压缩资源以可信解析器和内容限制处理，阻止外部实体、脚本、远程路径与过大输入。
- Design/Preview 的事件不触发真实曝光、轴运动、PLC 输出；生产 Run/Debug 权限与联锁必须由专业内核验证。
- Trace/日志/配置中的凭据或敏感字段按权限屏蔽，不以“能显示”作为允许泄露依据。

### 18.3 性能边界与测量

- Scene 投影缓存按 MEM Revision / 资源 ID / 视口状态失效；运行帧按实例/数据版本和专业质量分离。
- 大体积数据仅缓存句柄、缩略图、切片/结果元信息；不按 Widget 刷新频率重复读取整个 Volume。
- UI 采用有界通知、调度线程统一刷新与限频；操作手感、界面延迟、长时内存/句柄、DPI/触屏与大量节点性能均在真实目标机实测，**暂不设未经验证的虚假数字预算**。

### 18.4 可观察性

每项错误至少可关联：`IssueCode、Severity、Project/Resource/Node/Member ID、Revision、PlanVersion、RunSession、CallPath、时间、修复建议`（相关字段按实际可得）。支持从诊断定位源码/资源/节点，从 Viewer/HMI 反向定位相同问题，不靠字符串模糊搜索。

## 19. 需求、角色、设计和最终验收矩阵

| 验收路径 | 主要需求 | 角色 | 主要工程实体/Owner | 必须产生的证据 |
|---|---|---|---|---|
| **A 九Shape/精细编辑** | EP01/02/03/04/05/15 | R1/R3/R6 | Geometry、MEM/MTC、Visual、WPF Studio | 真鼠键精细操作、Undo/Redo、保存重开、异常与 Windows STA |
| **B 电机 Symbol 双实例** | EP04/05/06/07/08/13/15 | R2/R3/R4/R6 | MEM/MPM、Visual、MDS、HMI专业 | 两实例独立绑定、真实 Widget/动画、授权拒绝、Revision稳定 |
| **C X-Ray 主/AI/CV 多子流** | EP01/07/08/09/10/11/12/13/15 | R2/R3/R4/R5/R6 | MOL/MOC/MRK/MDS、Vision、Inspection、Automation | 可执行计划、子流调用、Trace/Watch、SourceMap-HMI定位、修复重构建 |
| **CT3D 专业 Viewer** | EP01/07/10/11/12/13/15 | R2/R3/R5 | CT专业、MDS、MRK、Viewer适配 | 大数据句柄、MPR/3D、几何参数、重建Trace与专业异常 |
| **开发者 SDK 易用性** | EP01/04/09/13/14/15 | R1/R2/R6 | 原 Core/Visual/Runtime/专业接口 | 外部独立示例建真工程、可打开保存、无UI核心测试 |
| **安全/交付** | EP08/10/11/12/15 | R4/R5/R6 | 专业权限/设备联锁、MRK、Audit | 无授权拒绝、仿真≠现场、真实机台授权与审计 |

### Studio Shell 交互补充验收

| 跨界面路径 | 对应需求 | 必须核验 |
|---|---|---|
| 资源树→配置组合面板 | EP01/04/14 | 1×N/2×N/3×N合适布局、同Owner保存与失败回滚 |
| 资源树→图元/流程画布→对象树 | EP01/02/03/09 | 同一工程ID、真实Undo、双向选中和返回 |
| 文档Tab→动态专业Ribbon | EP01/08/13/14 | 当前Workbench/资源/模式/权限切换命令实际变化 |
| Docking/浅深主题/布局恢复 | EP01/13/15 | 浮动停靠、重启恢复、订阅释放、DPI和深灰/白底样式 |
| AI节点→父流程→HMI故障定位 | EP09/10/11/12 | 真实SourceMap和Trace，计划与实例身份正确 |

### A/B/C 最终验收不可互相替代

- **A**：九个 Shape、复杂 Bezier/Path、多选/精确变换、层级锁定、Undo/Redo、真实鼠键、保存重开全过。
- **B**：单份可复用电机 Symbol、真实 Widget、两个各自绑定的实例、状态/动画、合法/非法事件、工程不被运行数据污染全过。
- **C**：主流程 + 多个专业子流程（至少 AI 与 CV）、参数/Port、MOC 计划、MRK 执行与仿真、Watch/Trace/SourceMap、HMI 跳转、调试后修复重建全过。
- **综合**：在同一工程中以上三个场景跨资源协作，纯 Core/Windows WPF/专业运行和设备现场分级签收；任何一项“仅源码存在”都不视为最终通过。

## 20. 已核实事实、设计目标与待裁决项

| 项目 | 当前可证明的层级 | 本稿如何对待 |
|---|---|---|
| MEM 工程、MPM 属性与资源、MTC 事务、现有 Visual/WPF Studio 基础能力 | 仓库源码存在、已有示例与测试代码 | 用为既有 Owner/API 的依据；仍需同 SHA Windows 实测 |
| EP01～EP15 需求追踪、R01～R04 的阶段实施记录 | 仓库文档和已提交源码 | 用作需求来源与已有纵向切片；不是功能全部完成证明 |
| 当前 R04 同定义多实例批量、Mixed、MPM IsReadOnly | 当前源码已声明，配有 Core/WPF 测试代码 | 目标的部分实现；不得声明已在 Windows 通过 |
| 停靠工作区、动态 Top Ribbon、全搜索与全部专业 Schema 编辑器 | 最新 Studio S1 已接入固定九菜单、动态 Ribbon 基础、AvalonDock 5.0.1 和主题/布局；配置组合真实 Schema、资源ID文档、专业 Viewer 尚未全量 | 属于**部分源码已实现、需CI与专业UI集成验证**，不将已有 S1 演示入口误报为全部 EP 功能完成 |
| 《Studio V1.1/V1.2》完整章节与 S1 Docking 事实 | 尚未取得界面规范 V1.1/V1.2 全文，S1 源码已确认选择 AvalonDock 5.0.1 | 已选择的 NuGet 版本不再列为待决；仍须按最新规范原稿及真实 Windows CI 验证 |
| 六专业 Workbench 的全生命周期集成与 A/B/C 最终验收 | 平台宪法、需求与真实业务场景目标 | 需逐项实现、运行/调试/真实设备验证 |
| 跨定义同名兼容 Schema 批量编辑、表达式/复合值编辑器、完整用户布局持久化 | 需在下一阶段进行详细契约/性能裁决 | 未定细节不能通过 UI 猜测行为；必须兼顾安全与简便 |

**全类架构按三个阶段并入本稿第四部分**：阶段一已完成源码归属与现有类型基线；阶段二做完整差距反推及类型归属裁决；阶段三完善属性/方法契约和端到端验收。设计、代码声明和实际Windows/设备验收等级严格区分。

---

# 第四部分｜全类架构反推（三阶段实施）

> **实施声明**：本部分是对原文需求、角色和设计的反推；不另起一份并行工程模型。阶段一为**现有类型与归属基线**，阶段二进行增量类型/接口和依赖裁决，阶段三完善属性/方法与端到端验收。所有阶段按实际源码重检，出现并行 `master` 更新应复核 SHA。

## 21. 三阶段范围、交付和完成判据

| 阶段 | 工作内容 | 直接成果 | 当前状态与完成判据 |
|---|---|---|---|
| **阶段一：真实现状与归属基线** | 从现有 `.csproj`、源码声明、项目目录对照；核对 **F/M/P/Applications** 唯一 Owner；列工程呈现化关联现有类/接口/枚举及 EP/角色、复用方向，记录缺口和证据等级 | `22～25` 已核查类型台账与覆盖差距，不改代码 | **本轮已完成“关联类型现状审计”**：逐条可回源码；不以阶段一替代全库所有私有辅助类型扫描或 Windows 验收 |
| **阶段二：缺失/扩展类型反推和归属裁决** | 从 EP01～EP15 × 六角色 × U1～U5 × Studio Shell 的最新源码反推差距；逐项“直接复用/增强/必要新增/重复取消”，定接口、枚举、依赖、关键场景 | §26～34§：原位复用增强表、候选协同类型、明确禁建清单、唯一 Owner/依赖 DAG、场景与需求反证 | **本轮完成架构候选归属裁决**；具体新类型最终签名/可合并性及实际源码实施归第三阶段，Windows测试待验 |
| **阶段三：全类规格与验收映射** | 对已核现有关键API与N01～N20新增职责提出属性、方法、生命周期、线程、安全、序列化及测试合同；双角色API/Studio路径和类→EP→案例→验收 | `35～42` 现有真实API抽样+20候选详细契约、去重/依赖、两种角色与15组验收门禁；其余305型全成员级复核另列 | **本轮完成可实施的协同契约和验收矩阵**；现有305全公开重载及专业真机尚待逐项审计/执行，不冒称全类成员已经100%审核 |

## 22. 阶段一核对方法、项目归属及状态定义

- **核对范围**：截至本阶段读取的当前 `master`，按真实 `.csproj` 编译清单和源码 `class/interface/enum/struct` 声明，检查与 EP01～EP15 **直接相关的公共可复用类型、编辑器、运行数据和专业接入类型**。同一个 `partial` 类型只记一行；不重复列私有嵌套实现/自动生成 `AssemblyInfo`。这份台账是“工程呈现化的现状基线”，**不是 28 项目所有类型的绝对全量扫描**。
- **归属来源**：实际 `.csproj` 的 `AssemblyName` 与源码物理路径决定已实现类型的项目归属；`DOCS/ProjectCatalog.csv` 仅做辅助对照，部分 `Assembly-only skeleton` 旧标记已与当前源码不一致，**不能直接用作最新完成度**。项目/子方面 ID 仍以四大统一规格和最新批准架构定案为准，表中 ID 是该项目在现有目录表的归属范围，并非逐类精确子方面裁决。
- **状态口径**：台账所有列出的类型都已在当前源码文本中找到声明（`S1=源码声明存在`）；**不据此宣称 `S2=已在Studio形成完整操作`、`S3=同SHA Windows编译/自动测试通过`、`S4=实际专业/硬件场景验收通过`**。复用建议不是实施完成。表中“现状处理”描述后续动作，不暗示完成。
- **类型形态**：类（含 `abstract`/`static`/泛型）、接口、枚举、结构体分别显示；完整的 `public`/`internal` 可见性、方法/属性和重载属于阶段三逐一校核对象。表内个别静态帮助类亦记作类，不为它们创建服务实例。

### 22.1 当前真实项目—方面—范围对照

| 方面 | 物理项目 / 现有子方面范围 | 此次审计定位 |
|---|---|---|
| Foundation | `CxVisual.Foundation.Geometry`（F05/F06/F07） | 纯几何、Path、变换/命中计算，不承担 WPF 和工程事务 |
| Mechanism | `CxVisual.Mechanism.Core`（M01～M08/M38） | MEM 工程/MPM Schema/MTC事务/MOL 定义/MRC 图元语义；不引入第二工程真源 |
| Mechanism | `CxVisual.Mechanism.Visual`（M16/M17/M37） | 场景投影、编辑行为、可视运行帧，必须可脱离 WPF |
| Mechanism | `CxVisual.Mechanism.Presentation.Wpf`（无独立 M-ID） | WPF 技术适配、控件、画布/Viewer 宿主组件，不持有 MEM 真源 |
| Mechanism | `CxVisual.Mechanism.Data`（M09/M10/M35/M36） | MDS 运行时数据、配置与受控操作，注意同项目不同目录职责不混淆 |
| Mechanism | `CxVisual.Mechanism.Orchestration`（M18～M25） | MOC 编译、诊断、计划/SourceMap，不是执行器 |
| Mechanism | `CxVisual.Mechanism.Runtime`（M26～M34） | MRK 运行、执行、调试/审计会话，不是逻辑编辑器 |
| Professional | Automation（P01～P18）、HMI（P19～P29）、Vision（P30～P42）、CT3D（P43～P53）、Inspection（P54～P65）、Simulation（P66～P73） | 专业能力的唯一 Owner；专业编辑入口由 Shell 装载 |
| Applications | `CxVisual.Studio`（无独立子方面 ID） | 公共 Shell、资源编辑/导航、命令/停靠与各机制的 UI 薄适配；不是第四架构方面 |

## 23. 阶段一已核源码类型明细

### 23.1 01 Foundation 几何与变换

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `FGEPoint2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEVector2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGESize2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGERect2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGESegment2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGECircle2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEEllipse2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEPolyline2D` | 类 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEPolygon2D` | 类 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEArc2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEBezier2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEValidation` | 类 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs)；**S1**；原位复用 |
| `FGEPathVerb` | 枚举 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs)；**S1**；原位复用 |
| `FGEPathCommand` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs)；**S1**；原位复用 |
| `FGEPath2D` | 类 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs)；**S1**；原位复用 |
| `FGEPathHandleKind` | 枚举 | `CxVisual.Foundation.Geometry` | EP03；R1/R3 | [源码](../../Foundation/Geometry/Paths/FGEPathEditing.cs)；**S1**；原位复用 |
| `FGEPathEditing` | 类 | `CxVisual.Foundation.Geometry` | EP03；R1/R3 | [源码](../../Foundation/Geometry/Paths/FGEPathEditing.cs)；**S1**；原位复用 |
| `FGETransform2D` | 结构体 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Transforms/FGETransform2D.cs)；**S1**；原位复用 |
| `FGEFillRule` | 枚举 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Operations/FGEGeometryAlgorithms.cs)；**S1**；原位复用 |
| `FGEGeometry2D` | 类 | `CxVisual.Foundation.Geometry` | EP02/03；R1/R3 | [源码](../../Foundation/Geometry/Operations/FGEGeometryAlgorithms.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.2 02 MEM 工程、身份、历史

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MEMNodeKind` | 枚举 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMResourceScope` | 枚举 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `IMEMResource` | 接口 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMNode` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMProject` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMPage` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMContent` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMSymbol` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MRIReferenceKind` | 枚举 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMResourceEntry` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MRIResourceReference` | 类 | `CxVisual.Mechanism.Core` | EP01/03/14；R1/R3 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs)；**S1**；原位复用 |
| `MEMEngineering` | 类 | `CxVisual.Mechanism.Core` | EP01/03/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs)；**S1**；原位复用 |
| `MEMEngineeringSession` | 类 | `CxVisual.Mechanism.Core` | EP01/03/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs)；**S1**；原位复用 |
| `MTCEditResult` | 类 | `CxVisual.Mechanism.Core` | EP01/03/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs)；**S1**；原位复用 |
| `MTCChangeSet` | 类 | `CxVisual.Mechanism.Core` | EP01/03/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs)；**S1**；原位复用 |
| `MVEValidationReport` | 类 | `CxVisual.Mechanism.Core` | EP01/03/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs)；**S1**；原位复用 |
| `MTCEditSession` | 类 | `CxVisual.Mechanism.Core` | EP03/04/15；R1/R3 | [源码](../../Mechanism/Core/Engineering/MTCEditSession.cs)；**S1**；原位复用 |
| `MTCChangeKind` | 枚举 | `CxVisual.Mechanism.Core` | EP03/04/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs)；**S1**；原位复用 |
| `MTCChangeOperation` | 结构体 | `CxVisual.Mechanism.Core` | EP03/04/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs)；**S1**；原位复用 |
| `MTCEditState` | 枚举 | `CxVisual.Mechanism.Core` | EP03/04/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs)；**S1**；原位复用 |
| `MTCEditDraft` | 类 | `CxVisual.Mechanism.Core` | EP03/04/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs)；**S1**；原位复用 |
| `IMEMProjectStore` | 接口 | `CxVisual.Mechanism.Core` | EP01/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs)；**S1**；原位复用 |
| `MEMSaveSnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs)；**S1**；原位复用 |
| `MEMSaveResult` | 类 | `CxVisual.Mechanism.Core` | EP01/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs)；**S1**；原位复用 |
| `MEMStoreConflictException` | 类 | `CxVisual.Mechanism.Core` | EP01/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs)；**S1**；原位复用 |
| `MEMFileProjectStore` | 类 | `CxVisual.Mechanism.Core` | EP01/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MEMFileProjectStore.cs)；**S1**；原位复用 |
| `MEMProjectSnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/12/15；R1/R5/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs)；**S1**；原位复用 |
| `MEMNodeSnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/12/15；R1/R5/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs)；**S1**；原位复用 |
| `MPMPropertySnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/12/15；R1/R5/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs)；**S1**；原位复用 |
| `MRCVisualSnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/12/15；R1/R5/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs)；**S1**；原位复用 |
| `MRIReferenceSnapshot` | 类 | `CxVisual.Mechanism.Core` | EP01/12/15；R1/R5/R6 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs)；**S1**；原位复用 |
| `MRIObjectAddress` | 类 | `CxVisual.Mechanism.Core` | EP01/12；R1/R3/R5 | [源码](../../Mechanism/Core/Engineering/MRIObjectAddress.cs)；**S1**；原位复用 |
| `MRIObjectHandle` | 类 | `CxVisual.Mechanism.Core` | EP01/12；R1/R3/R5 | [源码](../../Mechanism/Core/Engineering/MRIObjectAddress.cs)；**S1**；原位复用 |
| `MEMAccessOperation` | 枚举 | `CxVisual.Mechanism.Core` | EP01/08/15；R1/R4/R6 | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs)；**S1**；原位复用 |
| `IMEMAccessPolicy` | 接口 | `CxVisual.Mechanism.Core` | EP01/08/15；R1/R4/R6 | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs)；**S1**；原位复用 |
| `MEMAccessRequest` | 类 | `CxVisual.Mechanism.Core` | EP01/08/15；R1/R4/R6 | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs)；**S1**；原位复用 |
| `MEMCloseMode` | 枚举 | `CxVisual.Mechanism.Core` | EP01/08/15；R1/R4/R6 | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs)；**S1**；原位复用 |
| `MEMAssetReference` | 类 | `CxVisual.Mechanism.Core` | EP01/05；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MEMAssetReference.cs)；**S1**；原位复用 |
| `MTIExternalPolicy` | 枚举 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs)；**S1**；原位复用 |
| `IMTIAspectRemapper` | 接口 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs)；**S1**；原位复用 |
| `MTITransferResult` | 类 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs)；**S1**；原位复用 |
| `MTITransferPlan` | 类 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R6 | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs)；**S1**；原位复用 |
| `MTIExtractionResult` | 类 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTIPackages.cs)；**S1**；原位复用 |
| `MTIEngineeringTemplates` | 类 | `CxVisual.Mechanism.Core` | EP01/05/15；R1/R3/R6 | [源码](../../Mechanism/Core/Engineering/MTIPackages.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.3 03 MPM、端口及逻辑定义

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MPMValueKind` | 枚举 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `MPMValue` | 类 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `MPMBindingKind` | 枚举 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `MPMBinding` | 类 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `MPMPropertyDescriptor` | 类 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `MPMProperty` | 类 | `CxVisual.Mechanism.Core` | EP04/07；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs)；**S1**；原位复用 |
| `IMPMTypeProvider` | 接口 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs)；**S1**；原位复用 |
| `MPCPortDescriptor` | 类 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs)；**S1**；原位复用 |
| `MPMTypeDescriptor` | 类 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs)；**S1**；原位复用 |
| `IMPMDefinition` | 接口 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2 | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs)；**S1**；原位复用 |
| `MPMAspectKind` | 枚举 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2 | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs)；**S1**；原位复用 |
| `MPMAspect` | 类 | `CxVisual.Mechanism.Core` | EP04/14；R1/R2 | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs)；**S1**；原位复用 |
| `MPCPortDirection` | 枚举 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MPCPortKind` | 枚举 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MPCPortMultiplicity` | 枚举 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MTUContractReference` | 类 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MPCPort` | 类 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MPCEndpoint` | 类 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MPCConnection` | 类 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MRCPublicMappingKind` | 枚举 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MRCPublicMapping` | 类 | `CxVisual.Mechanism.Core` | EP09/14；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MPCPort.cs)；**S1**；原位复用 |
| `MOLControlKind` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLBranchMode` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLLoopMode` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLExitKind` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLExpressionSource` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLComparison` | 枚举 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLExpression` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLBranchCase` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLLoopDefinition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLControlBlock` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLLogicDefinition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLLogicCodec` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLBlockEndpoint` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLLink` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLBlockPosition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLSelectionGroup` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLStateDescriptor` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLStateTransition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLStateMachineDefinition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLStepPolicy` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLWiringDefinition` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |
| `MOLWiringCodec` | 类 | `CxVisual.Mechanism.Core` | EP09/10/12；R1/R2/R3/R5 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.4 04 MRC 可视工程定义

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MRCVisualItem` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs)；**S1**；原位复用 |
| `MRCElement` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs)；**S1**；原位复用 |
| `MRCRectangle` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs)；**S1**；原位复用 |
| `MRCGroup` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R2/R3 | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs)；**S1**；原位复用 |
| `MRCShapeKind` | 枚举 | `CxVisual.Mechanism.Core` | EP02/03；R1/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs)；**S1**；原位复用 |
| `MRCShapeDefinition` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs)；**S1**；原位复用 |
| `MRCShapeElement` | 类 | `CxVisual.Mechanism.Core` | EP02/03；R1/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs)；**S1**；原位复用 |
| `MRCTextElement` | 类 | `CxVisual.Mechanism.Core` | EP02/05/06；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs)；**S1**；原位复用 |
| `MRCImageElement` | 类 | `CxVisual.Mechanism.Core` | EP02/05/06；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs)；**S1**；原位复用 |
| `MRCSvgElement` | 类 | `CxVisual.Mechanism.Core` | EP02/05/06；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs)；**S1**；原位复用 |
| `MRCWidgetTypes` | 类 | `CxVisual.Mechanism.Core` | EP02/05/06；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs)；**S1**；原位复用 |
| `MRCWidgetElement` | 类 | `CxVisual.Mechanism.Core` | EP02/05/06；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs)；**S1**；原位复用 |
| `MRCColor` | 结构体 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCBrushKind` | 枚举 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCGradientStop` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCBrushDefinition` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCVisualStyle` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCVisualStateDefinition` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCAnimationEasing` | 枚举 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCAnimationDefinition` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCVisualEventAction` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCVisualBehaviorDefinition` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCVisualAppearance` | 类 | `CxVisual.Mechanism.Core` | EP05/07/08；R1/R2/R3 | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs)；**S1**；原位复用 |
| `MRCTransform` | 结构体 | `CxVisual.Mechanism.Core` | EP02/03；R1/R3 | [源码](../../Mechanism/Core/Engineering/MRCTransform.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.5 05 Visual 场景编辑与行为

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `CxSceneItem` | 类 | `CxVisual.Mechanism.Visual` | EP02/07；R1/R3 | [源码](../../Mechanism/Visual/CxSceneItem.cs)；**S1**；原位复用 |
| `CxSceneProjection` | 类 | `CxVisual.Mechanism.Visual` | EP02/05/07；R1/R3 | [源码](../../Mechanism/Visual/CxSceneProjection.cs)；**S1**；原位复用 |
| `CxVisualEditor` | 类 | `CxVisual.Mechanism.Visual` | EP02/03/14；R1/R3 | [源码](../../Mechanism/Visual/CxVisualEditor.cs)；**S1**；原位复用 |
| `MVIShapeCatalog` | 类 | `CxVisual.Mechanism.Visual` | EP02；R1/R3 | [源码](../../Mechanism/Visual/Catalog/MVIShapeCatalog.cs)；**S1**；原位复用 |
| `MVIVisualHandleKind` | 枚举 | `CxVisual.Mechanism.Visual` | EP03；R1/R3 | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs)；**S1**；原位复用 |
| `MVIVisualHandle` | 类 | `CxVisual.Mechanism.Visual` | EP03；R1/R3 | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs)；**S1**；原位复用 |
| `MVIVisualEditing` | 类 | `CxVisual.Mechanism.Visual` | EP03；R1/R3 | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs)；**S1**；原位复用 |
| `MVIVisualRuntimeEvaluator` | 类 | `CxVisual.Mechanism.Visual` | EP07；R1/R4/R5 | [源码](../../Mechanism/Visual/Runtime/MVIVisualRuntimeEvaluator.cs)；**S1**；原位复用 |
| `MVIVisualActionInvocation` | 类 | `CxVisual.Mechanism.Visual` | EP08；R1/R2/R4 | [源码](../../Mechanism/Visual/Runtime/MVIVisualEventRouter.cs)；**S1**；原位复用 |
| `MVIVisualEventRouter` | 类 | `CxVisual.Mechanism.Visual` | EP08；R1/R2/R4 | [源码](../../Mechanism/Visual/Runtime/MVIVisualEventRouter.cs)；**S1**；原位复用 |
| `MVIEventPhase` | 枚举 | `CxVisual.Mechanism.Visual` | EP03/08；R1/R3 | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs)；**S1**；原位复用 |
| `MVIInteractionKind` | 枚举 | `CxVisual.Mechanism.Visual` | EP03/08；R1/R3 | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs)；**S1**；原位复用 |
| `MVIInteractionEventArgs` | 类 | `CxVisual.Mechanism.Visual` | EP03/08；R1/R3 | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs)；**S1**；原位复用 |
| `MOLBlockLayout` | 类 | `CxVisual.Mechanism.Visual` | EP09；R1/R3 | [源码](../../Mechanism/Visual/MOLFlowLayout.cs)；**S1**；原位复用 |
| `MOLFlowLayout` | 类 | `CxVisual.Mechanism.Visual` | EP09；R1/R3 | [源码](../../Mechanism/Visual/MOLFlowLayout.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.6 06 WPF 真实界面适配

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `SceneView` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP02/03/06；R3 | [源码](../../Mechanism/Presentation.Wpf/SceneView.cs)；**S1**；原位复用 |
| `MOLFlowCanvas` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MOLPortVisual` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MOLBlockTemplateSelector` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MOLSelectionChangedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MOLPortLinkRequestedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MOLPortLinkRejectedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP09/12；R3/R5 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs)；**S1**；原位复用 |
| `MPWShapeRenderer` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP02/05；R3 | [源码](../../Mechanism/Presentation.Wpf/Renderers/MPWShapeRenderer.cs)；**S1**；原位复用 |
| `MPWVisualAssetRenderer` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP05；R3 | [源码](../../Mechanism/Presentation.Wpf/Renderers/MPWVisualAssetRenderer.cs)；**S1**；原位复用 |
| `MPWWidgetEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP06/08；R3/R4 | [源码](../../Mechanism/Presentation.Wpf/Widgets/MPWWidgetPresenter.cs)；**S1**；原位复用 |
| `MPWWidgetPresenter` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP06/08；R3/R4 | [源码](../../Mechanism/Presentation.Wpf/Widgets/MPWWidgetPresenter.cs)；**S1**；原位复用 |
| `MPWConfigEditorControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs)；**S1**；复用配置控件；尚未等同统一组合面板 |
| `MPWConfigEditorDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs)；**S1**；复用配置控件；尚未等同统一组合面板 |
| `MPWConfigLayerOverview` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigLayerOverview.cs)；**S1**；泛型配置呈现基础；中央组合宿主待核 |
| `MPWConfigLayerDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigLayerOverview.cs)；**S1**；泛型配置呈现基础；中央组合宿主待核 |
| `MPWConfigAtomicGroupControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigAtomicGroupControl.cs)；**S1**；原位复用 |
| `MPWConfigAtomicGroupDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP01/04；R2/R3 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigAtomicGroupControl.cs)；**S1**；原位复用 |
| `LogViewerControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP12/15；R5/R6 | [源码](../../Mechanism/Presentation.Wpf/LogViewerControl.xaml.cs)；**S1**；原位复用 |
| `SystemMonitorControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | EP11/15；R4/R5 | [源码](../../Mechanism/Presentation.Wpf/SystemMonitorControl.xaml.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.7 07 MOC 构建与SourceMap

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MOCDiagnosticSeverity` | 枚举 | `CxVisual.Mechanism.Orchestration` | EP10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCBuildService.cs)；**S1**；原位复用 |
| `MOCDiagnostic` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCBuildService.cs)；**S1**；原位复用 |
| `MOCBuildResult` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCBuildService.cs)；**S1**；原位复用 |
| `MOCBuildService` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCBuildService.cs)；**S1**；原位复用 |
| `MOCStepOrigin` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCSourceMap` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCObservedStep` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCTraceView` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCCompiledPlan` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCFlowBuilder` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCEngineeringCompiler` | 类 | `CxVisual.Mechanism.Orchestration` | EP09/10/12；R1/R3/R5 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs)；**S1**；原位复用 |
| `MOCCapabilityDescriptor` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/13/14；R1/R2/R3 | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs)；**S1**；原位复用 |
| `IMOCCapabilityProvider` | 接口 | `CxVisual.Mechanism.Orchestration` | EP10/13/14；R1/R2/R3 | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs)；**S1**；原位复用 |
| `MOCCapabilityCatalogItem` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/13/14；R1/R2/R3 | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs)；**S1**；原位复用 |
| `MOCCapabilityCatalog` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/13/14；R1/R2/R3 | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs)；**S1**；原位复用 |
| `MOCProjectReleaseCandidate` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/15；R1/R6 | [源码](../../Mechanism/Orchestration/MOCProjectRelease.cs)；**S1**；原位复用 |
| `MOCProjectRelease` | 类 | `CxVisual.Mechanism.Orchestration` | EP10/15；R1/R6 | [源码](../../Mechanism/Orchestration/MOCProjectRelease.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.8 08 MRK 执行、调试与发布

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MRKFlowState` | 枚举 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKStepState` | 枚举 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKExecutionContext` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKFlowResult` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKFlowHandle` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `IMRKExecutionEngine` | 接口 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKExecutionEngine` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs)；**S1**；原位复用 |
| `MRKExecutionStep` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs)；**S1**；原位复用 |
| `MRKLoopRegion` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs)；**S1**；原位复用 |
| `MRKControlRegion` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs)；**S1**；原位复用 |
| `MRKExecutionPlan` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs)；**S1**；原位复用 |
| `MRKRuntimeSessionState` | 枚举 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs)；**S1**；原位复用 |
| `MRKRuntimeSessionSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs)；**S1**；原位复用 |
| `MRKRuntimeSession` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R4/R5 | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs)；**S1**；原位复用 |
| `MRKExecutionTransition` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs)；**S1**；原位复用 |
| `MRKTraceBatch` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs)；**S1**；原位复用 |
| `MRKExecutionTrace` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs)；**S1**；原位复用 |
| `MRKExecutionDebugController` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs)；**S1**；原位复用 |
| `MRKExecutionOperations` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs)；**S1**；原位复用 |
| `MRKFlowMetadata` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs)；**S1**；原位复用 |
| `MRKInvocationContext` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs)；**S1**；原位复用 |
| `MRKStepObservation` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs)；**S1**；原位复用 |
| `MRKFlowSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs)；**S1**；原位复用 |
| `MRKPlanPublishAction` | 枚举 | `CxVisual.Mechanism.Runtime` | EP10/11/15；R1/R4/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs)；**S1**；原位复用 |
| `IMRKPlanPublicationPolicy` | 接口 | `CxVisual.Mechanism.Runtime` | EP10/11/15；R1/R4/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs)；**S1**；原位复用 |
| `MRKPlanPublicationReceipt` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11/15；R1/R4/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs)；**S1**；原位复用 |
| `MRKExecutionPlanPublisher` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11/15；R1/R4/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs)；**S1**；原位复用 |
| `MRKExecutionPlanLease` | 类 | `CxVisual.Mechanism.Runtime` | EP11；R1/R4 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs)；**S1**；原位复用 |
| `MRKExecutionPlanSlot` | 类 | `CxVisual.Mechanism.Runtime` | EP11；R1/R4 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs)；**S1**；原位复用 |
| `MRKJournalKind` | 枚举 | `CxVisual.Mechanism.Runtime` | EP11/12/15；R1/R5/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs)；**S1**；原位复用 |
| `MRKJournalRecord` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12/15；R1/R5/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs)；**S1**；原位复用 |
| `IMRKExecutionJournal` | 接口 | `CxVisual.Mechanism.Runtime` | EP11/12/15；R1/R5/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs)；**S1**；原位复用 |
| `MRKRecoveryDecision` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12/15；R1/R5/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs)；**S1**；原位复用 |
| `MRKFileExecutionJournal` | 类 | `CxVisual.Mechanism.Runtime` | EP11/12/15；R1/R5/R6 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs)；**S1**；原位复用 |
| `MRKInputSource` | 枚举 | `CxVisual.Mechanism.Runtime` | EP09/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs)；**S1**；原位复用 |
| `MRKExecutionInput` | 类 | `CxVisual.Mechanism.Runtime` | EP09/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs)；**S1**；原位复用 |
| `MRKExecutionOutput` | 类 | `CxVisual.Mechanism.Runtime` | EP09/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs)；**S1**；原位复用 |
| `MRKExecutionPortException` | 类 | `CxVisual.Mechanism.Runtime` | EP09/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs)；**S1**；原位复用 |
| `MRKExecutionGroupDefinition` | 类 | `CxVisual.Mechanism.Runtime` | EP11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs)；**S1**；原位复用 |
| `MRKExecutionGroupSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | EP11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs)；**S1**；原位复用 |
| `MRKExecutionGroups` | 类 | `CxVisual.Mechanism.Runtime` | EP11；R1/R5 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs)；**S1**；原位复用 |
| `IMRKCapabilityResolver` | 接口 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)；**S1**；原位复用 |
| `MRKStepCompileRequest` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)；**S1**；原位复用 |
| `MRKCompileDiagnostic` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)；**S1**；原位复用 |
| `MRKCompileResult` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)；**S1**；原位复用 |
| `MRKExecutionPlanCompiler` | 类 | `CxVisual.Mechanism.Runtime` | EP10/11；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs)；**S1**；原位复用 |
| `MRKFlowCompileDefinition` | 类 | `CxVisual.Mechanism.Runtime` | EP09/10；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs)；**S1**；原位复用 |
| `IMRKFlowCompileCatalog` | 接口 | `CxVisual.Mechanism.Runtime` | EP09/10；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs)；**S1**；原位复用 |
| `MRKExecutionOrchestrationCompiler` | 类 | `CxVisual.Mechanism.Runtime` | EP09/10；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs)；**S1**；原位复用 |
| `MRKCodeFlowBuilder` | 类 | `CxVisual.Mechanism.Runtime` | EP11/14；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs)；**S1**；原位复用 |
| `MRKCodeRuntimeBuilder` | 类 | `CxVisual.Mechanism.Runtime` | EP11/14；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs)；**S1**；原位复用 |
| `MRKCodeRuntimeBlueprint` | 类 | `CxVisual.Mechanism.Runtime` | EP11/14；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs)；**S1**；原位复用 |
| `MRKDevelopmentRuntime` | 类 | `CxVisual.Mechanism.Runtime` | EP11/14；R1/R2 | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.9 09 MDS 运行数据及配置

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `MDSDataHeap` | 类 | `CxVisual.Mechanism.Data` | EP07/11；R1/R4/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataHeap.cs)；**S1**；原位复用 |
| `MDSDataStack` | 类 | `CxVisual.Mechanism.Data` | EP07/11/12；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataStack.cs)；**S1**；原位复用 |
| `MDSDataFrame` | 类 | `CxVisual.Mechanism.Data` | EP07/11/12；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataStack.cs)；**S1**；原位复用 |
| `IMDSDataContext` | 接口 | `CxVisual.Mechanism.Data` | EP07/11；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataContext.cs)；**S1**；原位复用 |
| `MDSDataContext` | 类 | `CxVisual.Mechanism.Data` | EP07/11；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataContext.cs)；**S1**；原位复用 |
| `MDSBindingSource` | 枚举 | `CxVisual.Mechanism.Data` | EP07/11；R1/R2/R4 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs)；**S1**；原位复用 |
| `MDSDataBinding` | 类 | `CxVisual.Mechanism.Data` | EP07/11；R1/R2/R4 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs)；**S1**；原位复用 |
| `IMDSExternalBindingResolver` | 接口 | `CxVisual.Mechanism.Data` | EP07/11；R1/R2/R4 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs)；**S1**；原位复用 |
| `MDSDataBindingResolver` | 类 | `CxVisual.Mechanism.Data` | EP07/11；R1/R2/R4 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs)；**S1**；原位复用 |
| `MDSDataLocation` | 枚举 | `CxVisual.Mechanism.Data` | EP11/12；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs)；**S1**；原位复用 |
| `MDSDataQuery` | 类 | `CxVisual.Mechanism.Data` | EP11/12；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs)；**S1**；原位复用 |
| `MDSDataSnapshot` | 类 | `CxVisual.Mechanism.Data` | EP11/12；R1/R5 | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs)；**S1**；原位复用 |
| `MDSDataBudget` | 类 | `CxVisual.Mechanism.Data` | EP11/15；R1/R6 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBudget.cs)；**S1**；原位复用 |
| `MDSDataLoadState` | 枚举 | `CxVisual.Mechanism.Data` | EP11/13；R1/R2 | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs)；**S1**；原位复用 |
| `IMDSDataFileLoader` | 接口 | `CxVisual.Mechanism.Data` | EP11/13；R1/R2 | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs)；**S1**；原位复用 |
| `MDSDataFileLoader` | 类 | `CxVisual.Mechanism.Data` | EP11/13；R1/R2 | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs)；**S1**；原位复用 |
| `MDSLimitedBinaryFileLoader` | 类 | `CxVisual.Mechanism.Data` | EP11/13；R1/R2 | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs)；**S1**；原位复用 |
| `MHSConfigEditorField` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigEditorModel.cs)；**S1**；原位复用 |
| `MHSConfigCollectionItem` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigEditorModel.cs)；**S1**；原位复用 |
| `MHSConfigEditorModel` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigEditorModel.cs)；**S1**；原位复用 |
| `MVBConfigLayerOverrides` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigLayerSpec` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigLayerOrigin` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigLayerValueInfo` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigResolved` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigLayerChange` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigTaskSnapshot` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigLayeredProfile` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigLayeredProfile.cs)；**S1**；原位复用 |
| `MVBConfigAtomicLayerEdit` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigAtomicLayerProfile.cs)；**S1**；原位复用 |
| `MVBConfigAtomicLayerProfile` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MVBConfigAtomicLayerProfile.cs)；**S1**；原位复用 |
| `MHSConfigAuthorizedSession` | 类 | `CxVisual.Mechanism.Data` | EP04/08；R1/R4 | [源码](../../Mechanism/Data/Configuration/MHSConfigAuthorizedSession.cs)；**S1**；原位复用 |
| `MHSConfigAuthorizedSection` | 类 | `CxVisual.Mechanism.Data` | EP04/08；R1/R4 | [源码](../../Mechanism/Data/Configuration/MHSConfigAuthorizedSession.cs)；**S1**；原位复用 |
| `MHSConfigAuthorizedEdit` | 类 | `CxVisual.Mechanism.Data` | EP04/08；R1/R4 | [源码](../../Mechanism/Data/Configuration/MHSConfigAuthorizedSession.cs)；**S1**；原位复用 |
| `MHSConfigService` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigService.cs)；**S1**；原位复用 |
| `MVBConfigScopeResolver` | 类 | `CxVisual.Mechanism.Data` | EP01/04；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigService.cs)；**S1**；原位复用 |
| `MHSConfigApplyReview` | 类 | `CxVisual.Mechanism.Data` | EP04/10；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigApply.cs)；**S1**；原位复用 |
| `MHSConfigApplyDecision` | 类 | `CxVisual.Mechanism.Data` | EP04/10；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigApply.cs)；**S1**；原位复用 |
| `MHSConfigApplyResult` | 类 | `CxVisual.Mechanism.Data` | EP04/10；R1/R3 | [源码](../../Mechanism/Data/Configuration/MHSConfigApply.cs)；**S1**；原位复用 |
| `MDEExecutionDataScope` | 类 | `CxVisual.Mechanism.Data` | EP11/12；R1/R5 | [源码](../../Mechanism/Data/Execution/MDEExecutionDataScope.cs)；**S1**；原位复用 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.10 10 专业方面（已核现有代码）

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `CxAutomationDeviceOptions` | 类 | `CxVisual.Professional.Automation` | EP13/14；R2/R3 | [源码](../../Professional/Automation/CxAutomationDeviceOptions.cs)；**S1**；仅配置模型/规则源码，完整设备编辑与运行未据此成立 |
| `CxAutomationDeviceOptionsRules` | 类 | `CxVisual.Professional.Automation` | EP13/14；R2/R3 | [源码](../../Professional/Automation/CxAutomationDeviceOptions.cs)；**S1**；仅配置模型/规则源码，完整设备编辑与运行未据此成立 |
| `CxVisionPixelDepth` | 枚举 | `CxVisual.Professional.Vision` | EP13/14；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionOptions.cs)；**S1**；原位复用 |
| `CxVisionInspectionOptions` | 类 | `CxVisual.Professional.Vision` | EP13/14；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionOptions.cs)；**S1**；原位复用 |
| `CxVisionInspectionOptionsRules` | 类 | `CxVisual.Professional.Vision` | EP13/14；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionOptions.cs)；**S1**；原位复用 |
| `CxVisionGrayFrame` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionDefectRegion` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionInspectionResult` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionInspectionTask` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionInspectionRuntime` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionRawFrameReader` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2/R3 | [源码](../../Professional/Vision/CxVisionInspectionRuntime.cs)；**S1**；原位复用 |
| `CxVisionDataFlow` | 类 | `CxVisual.Professional.Vision` | EP07/13；R2 | [源码](../../Professional/Vision/CxVisionDataFlow.cs)；**S1**；原位复用 |
| `CxVisionThresholdProvider` | 类 | `CxVisual.Professional.Vision` | EP11/13；R2 | [源码](../../Professional/Vision/CxVisionThresholdProvider.cs)；**S1**；原位复用 |
| `CxCtReconstructionMode` | 枚举 | `CxVisual.Professional.CT3D` | EP13/14；R2/R3 | [源码](../../Professional/CT3D/CxCtReconstructionOptions.cs)；**S1**；仅选项/规则源码，非完整CT MPR/重建工作台 |
| `CxCtReconstructionOptions` | 类 | `CxVisual.Professional.CT3D` | EP13/14；R2/R3 | [源码](../../Professional/CT3D/CxCtReconstructionOptions.cs)；**S1**；仅选项/规则源码，非完整CT MPR/重建工作台 |
| `CxCtReconstructionOptionsRules` | 类 | `CxVisual.Professional.CT3D` | EP13/14；R2/R3 | [源码](../../Professional/CT3D/CxCtReconstructionOptions.cs)；**S1**；仅选项/规则源码，非完整CT MPR/重建工作台 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

### 23.11 11 Studio 应用与呈现桥接

| 名称 | 形态 | 所属项目（唯一物理Owner） | 需求/角色 | 来源与现状处理 |
|---|---|---|---|---|
| `StudioViewModel` | 类 | `CxVisual.Studio` | EP01/02/03/04；R3/R5 | [源码](../../Applications/Studio/StudioViewModel.cs)；**S1**；现有单体ViewModel；拆分职责待阶段二裁决 |
| `StudioPropertyItem` | 类 | `CxVisual.Studio` | EP01/04/09；R3 | [源码](../../Applications/Studio/StudioViewModel.Engineering.cs)；**S1**；已有投影/局部属性编辑；R01-R04源码存在但未本轮验收 |
| `StudioPortItem` | 类 | `CxVisual.Studio` | EP01/04/09；R3 | [源码](../../Applications/Studio/StudioViewModel.Engineering.cs)；**S1**；已有投影/局部属性编辑；R01-R04源码存在但未本轮验收 |
| `StudioConnectionItem` | 类 | `CxVisual.Studio` | EP01/04/09；R3 | [源码](../../Applications/Studio/StudioViewModel.Engineering.cs)；**S1**；已有投影/局部属性编辑；R01-R04源码存在但未本轮验收 |
| `MainWindow` | 类 | `CxVisual.Studio` | EP01/09/10/11/12；R3/R5 | [源码](../../Applications/Studio/MainWindow.xaml.cs)；**S1**；现有示例式Shell，九菜单/完整Docking尚未据此核定 |
| `MOLBlockViewModel` | 类 | `CxVisual.Studio` | EP11/12；R3/R5 | [源码](../../Applications/Studio/MOLRuntimeOverlay.cs)；**S1**；原位复用 |
| `MOLRuntimeOverlay` | 类 | `CxVisual.Studio` | EP11/12；R3/R5 | [源码](../../Applications/Studio/MOLRuntimeOverlay.cs)；**S1**；原位复用 |
| `MOLVisualTraceBindingResolver` | 类 | `CxVisual.Studio` | EP11/12；R3/R5 | [源码](../../Applications/Studio/MOLRuntimeOverlay.cs)；**S1**；原位复用 |
| `RelayCommand` | 类 | `CxVisual.Studio` | EP01/14；R1/R3 | [源码](../../Applications/Studio/RelayCommand.cs)；**S1**；原位复用 |
| `StudioDemoConfig` | 类 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |
| `StudioQuality` | 枚举 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |
| `StudioDetectorSetting` | 类 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |
| `StudioWorkflowStep` | 类 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |
| `StudioCalibration` | 类 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |
| `StudioRichConfig` | 类 | `CxVisual.Studio` | EP04/13；R3 | [源码](../../Applications/Studio/StudioDemoConfig.cs)；**S1**；Studio示例数据定义；不得当成通用专业契约 |

> 此表以源码声明建立 Owner/复用基线；是否达到 API 易用、真实编辑、运行、Windows 构建和 A/B/C 场景效果仍需独立证据。

## 24. 阶段一覆盖总结与重要缺口（不是第二阶段类名定案）

| EP 组 | 源码可复用 Owner / 已核类型基础 | 当前尚不能据源码确认的闭环 | 阶段二必须做的核查 |
|---|---|---|---|
| EP01 / EP14 工作区及 SDK | MEM工程/ResourceRef、StudioViewModel/MainWindow、WPF配置控件 | 完整九项菜单、上下文 Ribbon、资源树/对象树隔离、三编辑器宿主、Docking/布局恢复、面向专业的受控注册 SDK | 薄 Shell/编辑器/上下文契约的最小必要增构与Owner裁决 |
| EP02 / EP03 / EP05 / EP06 图形控件 | FGE几何、MRC形状样式、Visual投影/编辑、WPF Scene/Widget | 九图元精细编辑的完整鼠键、DPI、资产安全/主题、专业控件完整性 | 按现有行为先复用，不在Studio复制 Geometry/Widget实现 |
| EP04 / EP07 / EP08 属性绑定与事件 | MPM Schema、MDS绑定、Visual RuntimeEvaluator/EventRouter、StudioPropertyItem | Enum/集合/资源/表达式完整模板，多选跨定义兼容，真实设备授权安全链 | 显式只读/权限/数据质量/来源的横向契约差距审计 |
| EP09 / EP10 流程与构建 | MOL逻辑、WPF MOLFlowCanvas、MOC编译/SourceMap | 主/子/子子流程完整可编排操作、端口导航、Plan/SourceMap与各编辑器双向定位 | 现有MOL/MOC为权威，避免第二持久化Graph和第二编译器 |
| EP11 / EP12 运行调试 | MRK执行引擎/会话/Trace、MDS堆栈、Studio运行叠加 | 统一 Watch/Breakpoints/Trace UI、真实诊断导航、合法发布/设备联锁链 | 优先适配既有运行/数据契约，禁止Studio私自执行设备命令 |
| EP13 专业扩展 | Automation选项、Vision检测运行/Provider、CT3D选项；现有MOC能力目录 | Professional.HMI / Inspection / Simulation `.csproj` 当前仅编译 `AssemblyInfo.cs`；并非真实业务完整实现，CT3D现仅选项/规则不足以证明MPR/体绘 | 专业 Editor/Viewer/安全 Provider 按六专业 Owner 分别盘点，先考虑旧六仓能力复用 |
| EP15 质量与交付 | Core结果/快照/事务、MOC诊断、MRK日志、Studio测试源 | A/B/C + Windows WPF STA + 同SHA CI + 实机权限安全尚无本次统一验收证据 | 阶段三建立每类/方法的测试追踪，不依赖旧绿色记录 |

### 24.1 未发现统一 Shell 类型时如何处理

- **阶段一历史事实**：当时 `Applications/Studio/CxVisual.Studio.csproj` 编译清单以原 `MainWindow`、`StudioViewModel`、`MOLRuntimeOverlay`、`RelayCommand` 等为主，尚无通用 Document/EditorHost/ContextRibbon/DockLayout 独立类型；**随后 S1 新增了 `StudioShell.cs`（MainWindow partial）、`StudioViewModel.Shell.cs` 和 Studio 自测，已有九菜单/基础Ribbon/布局/主题能力**。最新情况以 §26～34 为准，不能延用本条旧判断认定 S1 尚未开展。
- `Mechanism/Presentation.Wpf` 已有配置控件、SceneView、MOLFlowCanvas，阶段二必须优先判断能否以少量适配整合**配置组合面板 / 图元主画布 / 流程主画布**，而不是因为需要统一界面就重做三套 View。
- `Professional.HMI`、`Professional.Inspection`、`Professional.Simulation` 的当前 `.csproj` 编译清单仅含 `Properties/AssemblyInfo.cs`。这**不能证明旧仓或其它当前项目缺乏对应业务功能**，只是当前这些 Professional 工程的事实状态；后续专业能力复用由阶段二验证。
- **待裁决候选职责（不是类名定案）**：活动文档和资源类型路由、编辑器生命周期、跨视图选择/定位、统一 Schema 模板、动态 Ribbon/命令/权限、Dock 布局、专业 Viewer/结果订阅、诊断 SourceMap 导航。需逐职责确定已有承载点、必要接口和归属，不能跳过重复性审计。

## 25. 阶段一交付统计、依赖纪律与阶段二进入条件

| 统计维度 | 本轮实际值 | 口径 |
|---|---:|---|
| 已核工程呈现化相关类型 | **305** | 源码声明型别逐条入账；`partial` 去重；含类/接口/枚举/结构体 |
| 覆盖的物理项目 | **11** | 1个Foundation + 6个Mechanism + 3个Professional + Studio（具体以下表为准） |
| 源码文件路径 | **89** | 表内每项可点击到声明源文件，不含未采样私有辅助实现 |
| 需求 | **EP01～EP15** | 按主需求+角色映射，未据此认定验收成功 |
| 已实现类型 S1 | **305** | 仅代表源码中存在声明和物理 Owner |
| 本轮新建业务类/项目 | **0** | 本阶段只更新文档，不改代码、解决方案、依赖 |
| Windows/MSBuild/WPF STA同 SHA验收 | **本轮未运行** | 禁止以静态清点冒称通过 |

### 25.1 每个项目的已核声明数量

| 项目 | 本轮审计类型数 | 状态说明 |
|---|---:|---|
| `CxVisual.Foundation.Geometry` | 20 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Core` | 111 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Visual` | 15 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Presentation.Wpf` | 19 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Orchestration` | 17 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Runtime` | 53 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Mechanism.Data` | 39 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Professional.Automation` | 2 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Professional.Vision` | 11 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Professional.CT3D` | 3 | **源码已定义**；本阶段不对完整功能/CI签收 |
| `CxVisual.Studio` | 15 | **源码已定义**；本阶段不对完整功能/CI签收 |

### 25.2 向阶段二交接的强制规则

1. 为每个已列类确认是否有 **可用API/调用链/已跑测试**，不能因为单独文件/接口/空壳就批准复用完整性；后续若发现更多相关内部类型，追加到此现状表并核实际路径。
2. 以 EP01～EP15/六角色/U1～U5/Studio `12` 的**缺口功能**反推“必须承担的职责”，逐个与这里的 305 个已核类型比较后再提出新增；禁止预设‘先创建349个类’或复制旧版 Graph。
3. 每个候选给出 **复用 / 原位增强 / 必须新增 / 废弃重复 / 待调查** 决定、唯一Owner、项目与目录、依赖方向、实例/定义/运行生命周期；Studio薄适配优先。
4. 阶段三再对保留类型写**完整属性/方法签名、返回与异常、并发/线程/内存所有权、工程序列化、测试矩阵**；本阶段不凭摘要猜方法签名。
5. 第一阶段已完成历史快照；第二阶段在§26～34§完成需求差距与唯一Owner候选裁决；**全类架构的完整签名和验收仍未总体定案**。项目数/代码现状会随并行 master 演进，后续须先复核提交/差异；无法获得可靠CI就保留待验。

---
## 26. 第二阶段现状修订、目标与事实边界

> **阶段二归属裁决基线**：本轮重新读取 `master` 的 Studio 源码、`.csproj`、S1 实施验收文档及第一阶段类型台账。第一阶段的 305 个类型是**当时的历史快照**，不是最新 `master` 永不变化的计数。此阶段只提交架构文档，不新增项目或业务源码。

### 26.1 新 `master` 变化必须覆盖第一阶段旧判断

- **已存在**：`Applications/Studio/StudioShell.cs` 是 `MainWindow` 的 **partial 扩展文件，而不是独立 `StudioShell` 类**；已出现 AvalonDock `LayoutDocument`/`LayoutAnchorable`、固定九菜单/动态 Ribbon 组装、浅深主题、布局 XML 校验/保存/恢复、基于已知 `ContentId` 的有限文档打开；`StudioViewModel.Shell.cs` 为原 `StudioViewModel` partial；`StudioShellSelfTests.cs` 是源码级测试入口。参考 [Studio S1实施验收](../Implementation/CxVisual_Studio_S1_统一Shell与AvalonDock_实施验收.md) 与 [StudioShell源码](../../Applications/Studio/StudioShell.cs)。
- **版本裁决更新**：S1 `.csproj`/实施记录和 Hello PoC 已接入 **Dirkster.AvalonDock 5.0.1**，不再把“4.x 或 5.x 选哪个”当成尚未发生的软件选择；**是否在最终 Windows 环境充分通过、与 Studio V1.2 全部交互一致，仍需同 SHA CI/人工证据**。
- **Studio V1.2**：S1 记录提到《Studio 统一界面架构与完整设计规范 V1.2》及 `DOCS/界面设计` 图稿；本轮未取得该 V1.2 的可核全文，因此优先以**已获取的代码和 S1 实施文档**为事实，不冒称 V1.2 已逐条审核。
- **阶段一纠偏**：§24.1§ 关于“缺独立 Shell 类型”的判断属于当时真实源码快照，不能按原句外推今天尚未实现 Docking/Shell；S1 已落地一部分。**但 S1 当前基于 `doc.home / doc.visual / doc.flow / doc.config` 等已知 ID，尚不能证明任意资源ID文档生命周期、专业 Viewer 注册和跨子流程跳转完整**，需要后续能力补齐。
- **状态**：本轮对 S1 只确认 GitHub 源码/实施文档和 workflow 路由，**没有运行 Windows/MSBuild/真 STA，也没有取得其最终通过日志**。不将源码声明当成验收通过。

## 27. 第二阶段的判定原则与归属

| 分类 | 判定条件 | 实施方式及证据 |
|---|---|---|
| **直接复用（R）** | Owner 已有权威实体、能力合同语义吻合 | 以后通过现有方法调用；若具体场景缺测试，补测试而非复制类 |
| **原位增强（E）** | 类/界面已有，缺高级编辑、事件、性能、适配或安全信息 | 在原项目/原类增构，可抽内部辅助但不换唯一真源 |
| **拟新增（N）** | 需要跨多个现有对象的**新UI协调责任**，当前 `.csproj`/代码未找到同名承担者 | 保留候选名、形态/唯一Owner/最小接口责任；阶段三确认完整签名、可见性和是否合并类型后再开发 |
| **明确排除（X）** | 会产生第二工程、Schema、运行仓、Undo、重复几何/调度或现有Shell同名容器 | 禁止新建，转入现有Owner的适配/增强 |
| **待专业调查（Q）** | 仅凭当前 x3 不能证明完整专业合同或硬件验收，涉及外部成熟代码复用 | 先读真实专业/七仓源码与测试，再定具体专业类和WPF适配 |

**依赖底线**：Foundation纯基础；Mechanism.Core/Visual/MOC/MRK/MDS继续 headless 或按其技术适配边界工作；`Mechanism.Presentation.Wpf` 不反向承载工程真源，也不直接耦合专业驱动；`CxVisual.Studio` 是 App 组合根，依赖允许的项目，专业逻辑**不引用 Studio**。专业 UI 工厂契约若需 WPF 类型，仅放 WPF 技术适配层；专业纯业务模型留 Professional，具体 WPF Viewer 由宿主适配器装配。后续按实际 `.csproj` 核验依赖 DAG，不凭文档擅自添加引用。

## 28. 既有类型的复用与原位增强裁决

> 下面每行引用第一阶段 §23§ 中一组已有类；**原位增强**代表需要补行为，不表明现有类为空壳。第 305 类型中没有列在此表的，仍按第一阶段原Owner保留，不等于要移除。

| ID | 裁决 | 唯一Owner项目 | 已有类 / 接口 | 需求 | 具体复用及新增能力边界 |
|---|---|---|---|---|---|
| F01 | 直接复用 | Foundation.Geometry | FGEPoint2D/FGERect2D/FGEPath2D/FGETransform2D/FGEPathEditing | EP02/03 | 纯几何、Path控制点及坐标变换；不得在WPF另写几何引擎 |
| C01 | 直接复用 | Mechanism.Core | MEMProject/MEMPage/MEMContent/MEMSymbol/MRIResourceReference | EP01/14 | Resource、定义/实例、工程唯一身份 |
| C02 | 直接复用 | Mechanism.Core | MEMEngineeringSession/MTCEditSession/MTCEditDraft | EP01/03/04/15 | 保持单一提交、Revision、Undo/Redo及错误回滚 |
| C03 | 直接复用 | Mechanism.Core | MEMFileProjectStore/IMEMProjectStore/MEMAssetReference | EP01/05/15 | 保存、资产引用/打包；布局偏好另存 |
| C04 | 原位增强 | Mechanism.Core | MPMPropertyDescriptor/MPMTypeDescriptor/MPMBinding/MPMProperty | EP04/07 | 沿用Schema和只读/来源，审核Enum、复合类型、单位/质量/表达式合同 |
| C05 | 直接复用 | Mechanism.Core | MPCPort/MPCConnection/MOLLogicDefinition/MOLWiringDefinition | EP09/10 | 真实Port与逻辑定义，不复制Canvas Graph |
| C06 | 直接复用 | Mechanism.Core | MRCVisualItem/MRCGroup/MRCShapeDefinition/MRCShapeElement | EP02/03 | 共享视觉对象和形状定义，稳定ID |
| C07 | 原位增强 | Mechanism.Core | MRCVisualStyle/MRCVisualStateDefinition/MRCAnimationDefinition/MRCVisualBehaviorDefinition | EP05/07/08 | 补状态组合优先级、主题引用及生命周期验证，不复制样式仓 |
| C08 | 直接复用 | Mechanism.Core | MRCWidgetElement/MRCTextElement/MRCImageElement/MRCSvgElement | EP02/05/06 | 源定义是MEM对象，Presenter仅显示 |
| C09 | 直接复用 | Mechanism.Core | MEMAccessRequest/IMEMAccessPolicy/MRIObjectAddress | EP01/08/12/15 | 用现有权限和对象地址进行跳转/控制校验 |
| V01 | 原位增强 | Mechanism.Visual | CxVisualEditor | EP01/02/03 | 沿用OpenDefinition/ReturnToPrevious/选择/编辑/Undo，补跨编辑器联动而非重建Editor |
| V02 | 原位增强 | Mechanism.Visual | CxSceneProjection/CxSceneItem | EP02/05/07 | 状态投影、增量失效与性能观察；运行帧不写MEM |
| V03 | 原位增强 | Mechanism.Visual | MVIVisualEditing/MVIVisualHandle/MVIShapeCatalog | EP02/03 | 精细Path/Arc/Bezier、命中/DPI/多选细节沿原Owner扩展 |
| V04 | 原位增强 | Mechanism.Visual | MVIVisualRuntimeEvaluator/MVIVisualEventRouter | EP07/08 | 绑定失效/数据质量、事件权限及取消/节流仅强化通用可视机制 |
| V05 | 直接复用 | Mechanism.Visual | MOLFlowLayout/MOLBlockLayout | EP09 | 流程布局只映射MOL节点，不产生第二执行顺序 |
| W01 | 原位增强 | Mechanism.Presentation.Wpf | SceneView/MPWShapeRenderer/MPWVisualAssetRenderer | EP02/03/05 | WPF真实编辑与安全资产渲染，深浅主题和DPI补测 |
| W02 | 原位增强 | Mechanism.Presentation.Wpf | MOLFlowCanvas/MOLPortVisual | EP09/12 | 多级子流程/Port交互/焦点同步，由MOL与MOC负责语义 |
| W03 | 原位增强 | Mechanism.Presentation.Wpf | MPWWidgetPresenter | EP06/08 | 六基础Widget真实交互/双向绑定与输入回显，控件不直接写设备 |
| W04 | 原位增强 | Mechanism.Presentation.Wpf | MPWConfigEditorControl/MPWConfigLayerOverview<T>/MPWConfigAtomicGroupControl | EP01/04/13 | 现有配置控件可重用；需要通用中央组合宿主，但不得复制Config数据模型 |
| O01 | 直接复用 | Mechanism.Orchestration | MOCBuildService/MOCBuildResult/MOCDiagnostic | EP10/12 | 统一构建与结构化诊断入口 |
| O02 | 原位增强 | Mechanism.Orchestration | MOCEngineeringCompiler/MOCSourceMap/MOCCompiledPlan/MOCStepOrigin | EP09/10/12 | 加强AI子流程→父流程→HMI映射和版本链；MOC不执行调度 |
| O03 | 直接复用 | Mechanism.Orchestration | MOCCapabilityCatalog/IMOCCapabilityProvider/MOCProjectRelease | EP10/13/14 | 专业能力发现和工程发布候选复用；不创Studio私有Catalog |
| R01 | 直接复用 | Mechanism.Runtime | MRKExecutionEngine/IMRKExecutionEngine/MRKExecutionPlan | EP11/12 | 运行由MRK真实引擎控制，Studio禁止私设运行器 |
| R02 | 原位增强 | Mechanism.Runtime | MRKRuntimeSession/MRKExecutionDebugController/MRKExecutionTrace | EP11/12 | 断点、Watch、Trace、授权状态的观察合同与取消/释放 |
| R03 | 直接复用 | Mechanism.Runtime | MRKExecutionPlanPublisher/IMRKPlanPublicationPolicy/MRKExecutionPlanSlot | EP10/11/15 | 部署/切换服从已存在的计划与授权策略 |
| R04 | 直接复用 | Mechanism.Runtime | MRKExecutionPlanCompiler/MRKExecutionOrchestrationCompiler/MRKDevelopmentRuntime | EP09/11/14 | 代码/工程执行合同原位复用，不添加新的Runner或编排引擎 |
| D01 | 直接复用 | Mechanism.Data | MDSDataHeap/MDSDataStack/MDSDataContext/MDSDataSnapshot | EP07/11/12 | 实时值/快照/数据质量唯一来源，Studio只订阅和可视投影 |
| D02 | 原位增强 | Mechanism.Data | MDSDataBinding/MDSDataBindingResolver/IMDSExternalBindingResolver | EP07/08 | 按实例来源解析、断线/过期/错误状态与双向授权写边界 |
| D03 | 直接复用 | Mechanism.Data | MHSConfigService/MHSConfigEditorModel/MVBConfigLayeredProfile<T> | EP01/04/13 | 配置Schema、差异来源及受控提交；不和MPM强行合并 |
| D04 | 直接复用 | Mechanism.Data | MHSConfigAuthorizedSession/MHSConfigApplyResult/MVBConfigAtomicLayerProfile<T> | EP04/08/10 | 配置层级/原子组/权限按原数据Owner保持独立合同 |
| S01 | 原位增强 | CxVisual.Studio | MainWindow（含StudioShell.cs的partial） | EP01/14 | 最新代码已有AvalonDock5.0.1、九菜单、主题/布局/文档；不再建议新建StudioShell |
| S02 | 原位增强 | CxVisual.Studio | StudioViewModel/StudioViewModel.Shell.cs | EP01/04/09 | 减少MainWindow直接业务路由，保留原工程会话与配置上下文 |
| S03 | 原位增强 | CxVisual.Studio | StudioPropertyItem/StudioPortItem/StudioConnectionItem | EP04/09 | 复用R01–R04 Schema/多选/只读投影；补跨编辑器上下文和复杂Editor |
| S04 | 直接复用 | CxVisual.Studio | MOLRuntimeOverlay/MOLVisualTraceBindingResolver | EP11/12 | 已有流程运行高亮与Trace源映射，用于真实观察接线 |
| S05 | 直接复用 | CxVisual.Studio | StudioShellSelfTests/RelayCommand | EP01/15 | S1源码级WPF自测及命令包装已有；CI通过以真实日志为准 |
| P01 | 原位增强 | Professional.Automation | CxAutomationDeviceOptions/CxAutomationDeviceOptionsRules | EP08/13 | 只复用设备配置模型，真实控制/联锁继续走专业Owner |
| P02 | 原位增强 | Professional.Vision | CxVisionInspectionRuntime/CxVisionThresholdProvider/CxVisionDataFlow | EP11/13 | 真实算法Provider与数据句柄关联；AI/ROI专业能力仍需审计 |
| P03 | 原位增强 | Professional.CT3D | CxCtReconstructionOptions/CxCtReconstructionOptionsRules | EP13/14 | 已有重建选项/规则；不得推断CT MPR/体绘或实机重建已集成 |

## 29. 必要的增量协同类型候选：形态、归属及契约边界

> **设计裁决**：这 20 个是跨现有机制的必要职责候选，**不是已创建的源码或保证最终数量的新类型**；名称为当前拟定案名，第三阶段在确认签名、项目依赖与可合并性后才能批准创建。公共接口只在真实跨资源/跨专业复用时引入，内部简单投影优先 internal/sealed/value 类型。不得仅为了表格数量机械建类。

| ID | 形态 | 候选类 / 接口 | 项目（唯一拟议Owner） | 对应EP | 必要职责 | 开发者与Studio使用效果 | 不可越界 |
|---|---|---|---|---|---|---|---|
| N01 | 结构体 | StudioDocumentKey | CxVisual.Studio | EP01/14 | (ProjectId,ResourceId,EditorKind,InstanceContext)唯一文档键，替代仅doc.home/visual/flow/config固定ID | 资源文档激活、关闭重开、Dirty以及定义/实例切换不串位 | 不得写成新的工程ResourceId |
| N02 | 枚举 | StudioEditorKind | CxVisual.Studio | EP01/13 | 区分Home/Configuration/Visual/Flow/ProfessionalViewer等编辑模式；仅UI分类 | 资源类型路由与可用工具选择 | 不替代MEM ResourceKind或专业类型ID |
| N03 | 类 | StudioDocumentCoordinator | CxVisual.Studio | EP01/14 | 将现有MainWindow OpenStudioDocument与资源ID绑定；Tab/激活/关闭/恢复的单处协调 | U2/U3多资源切换、关闭重开、相同资源复用View | 使用AvalonDock，不自造Document树 |
| N04 | 类 | StudioEditorDescriptor | CxVisual.Studio | EP01/13/14 | EditorKind/资源类型/适用模式/工厂引用和能力标记的只读UI描述 | 三种中央编辑器选择与专业Viewer挂接 | 只是UI元数据，不保存MPM Schema |
| N05 | 类 | StudioEditorRegistry | CxVisual.Studio | EP01/13/14 | 将现有资源类型ID匹配已有Scene/Flow/Config WPF控件与专业适配器 | 无注册资源显示错误占位，不会崩溃或误用画布 | 注册结果由Shell使用，专业工程不反向依赖Studio |
| N06 | 类 | StudioSelectionCoordinator | CxVisual.Studio | EP01/03/04/09 | 共享Resource/Outline/Scene/Flow/Property的选中ID集和Revision上下文 | 同定义批量/跨类型只读/取消选择/切换Tab | 选择不是MEM持久化节点 |
| N07 | 类 | StudioNavigationHistory | CxVisual.Studio | EP01/09/12 | 记录源/目标文档键+对象地址+视图位置，提供Back/Forward | 实例→定义→子流程→父流程→HMI可往返 | 跳转使用MRI地址/MOCSourceMap，不以标题搜索 |
| N08 | 类 | StudioResourceTreeProjection | CxVisual.Studio | EP01/13 | 按MEM真实资源和专业分类生成资源树行、引用/查询信息 | 选项目/页面/设备/模型并打开对应编辑器 | 不将Group/Element当顶层Resource |
| N09 | 类 | StudioObjectOutlineProjection | CxVisual.Studio | EP01/02/03/09 | 当前资源内部Group/Element/FlowNode的只读层级投影 | Outline与Scene/FlowCanvas同步选择 | 对象树不是第二个MEM节点容器 |
| N10 | 类 | StudioCommandDescriptor | CxVisual.Studio | EP01/08/14 | 单一命令键、显示/禁用原因、操作权限/上下文及Owner入口 | 菜单/Ribbon/右键/快捷键同义 | 不是新的逻辑节点或执行命令存储 |
| N11 | 类 | StudioCommandRegistry | CxVisual.Studio | EP01/08/13/14 | 将现有MainWindow.OnStudioMenuClick/UpdateStudioRibbon收敛到命令索引和上下文匹配 | 动态Ribbon、运行/调试/发布权限区分 | 不可为设备控制创造绕过MRK/Provider的路线 |
| N12 | 结构体 | StudioCommandAvailability | CxVisual.Studio | EP01/08/15 | Visible/Enabled/DisabledReason/PermissionRequired等只读UI计算结果 | 不可运行时明确反馈原因而不是假成功 | 权限最终仍由MEM/MRK/专业Core校验 |
| N13 | 类 | StudioDiagnosticNavigator | CxVisual.Studio | EP10/12 | 从MOCDiagnostic/Trace与MOCSourceMap定位到StudioDocumentKey和MRI对象地址 | U3失败节点→AI子流→父流程→HMI工位 | 禁止按字符串推测地址或伪造SourceMap |
| N14 | 类 | StudioRuntimeObservationCoordinator | CxVisual.Studio | EP07/11/12 | 在运行会话下订阅MRK/MDS观察，分发到HMI/Flow/Watch，关闭时退订 | U2实例运行状态与U3断点/Watch/Trace | 不持有数据堆、执行线程或修改工程Revision |
| N15 | 类 | StudioViewSubscriptionScope | CxVisual.Studio | EP07/11/15 | 按Tab/RunSession聚合IDisposable订阅与专业Viewer资源句柄 | 关闭页签、项目切换、断连时零幽灵更新 | 仅负责UI生命周期；不替代MRK/专业资源租约 |
| N16 | 类 | MPWConfigCompositePanel | CxVisual.Mechanism.Presentation.Wpf | EP01/04/13 | 以已有MPWConfigEditorControl/AtomicGroup等组合1×N/2×N/3×N配置卡 | 资源配置组合面板可应用/取消/校验并保留原Owner提交 | 不得另建配置值字典或独立Undo栈 |
| N17 | 接口 | IMPWResourceEditorFactory | CxVisual.Mechanism.Presentation.Wpf | EP01/13/14 | 工厂通过受控上下文创建WPF资源编辑/Viewer元素并定义释放合同 | 三主编辑器+专业Viewer以组合根注册 | 接口留WPF适配层，不让Mechanism.Core引用WPF |
| N18 | 类 | MPWResourceEditorContext | CxVisual.Mechanism.Presentation.Wpf | EP01/13/14 | 只读持有资源类型/引用位置/工作模式/命令权限以及可查询的模型视图 | 专业Viewer只接受受控上下文，避免拿裸设备SDK | 不保存第二MEM快照/运行堆 |
| N19 | 类 | MPWSchemaPropertyEditorHost | CxVisual.Mechanism.Presentation.Wpf | EP04/13/14 | 通用属性UI宿主映射MPM与已有MHSConfigEditorModel，模板扩展复用 | Numeric/Boolean/Enum/Brush/Resource/集合属性由统一面板操作 | MPM工程属性与MHS配置数据仍保有不同Owner |
| N20 | 接口 | IMPWSchemaEditorFactory | CxVisual.Mechanism.Presentation.Wpf | EP04/13/14 | 专业复杂字段的类型化控件工厂/校验可用性，仅扩展UI | ROI/CT参数/高级配置编辑模板选择 | 不把WPF控件放入Foundation/Visual纯核心 |

### 29.1 必须明确的“最小新增”与可合并策略

- **文档/编辑器**：N01/N02/N03/N04/N05 用于把目前静态 `ContentId` 的 S1 页签扩展成任意合法 Resource/Instance 的共享文档；`StudioDocumentCoordinator` 可持有 Registry/Descriptor，不允许另创 MEM 资源树。若现有 `MainWindow` 部分职责足够，可将 N04/N05 合并为一个内部注册表，只保留对外必要契约。
- **选择/导航**：N06/N07/N08/N09/N13 处理跨 Resource/Scene/Flow/Trace 的身份传播与回退。真实 ID 从 MEM/MRI/MOC 获取；模型事实变化只来自事务，不将树投影持久化。
- **Ribbon/命令**：S1 `MainWindow.UpdateStudioRibbon` 已存在；N10/N11/N12 是为了把多个 `Click` 入口收敛为可测试的同一命令条件与执行路径，**不是再写第二套 Ribbon 画面**。优先将旧按钮迁入同一 Registry 后删除重复处理。
- **配置/属性**：N16/N19/N20 仅在 WPF 层创建可复用控件/模板宿主。MPM 工程属性与 MHS 配置的真实提交语义不同，**模板可复用，存储和Undo仍按原 Owner**。N17/N18 必须允许 Studio 组合根注册专业 UI Adapter，不能要求 `Professional.CT3D` 直接引用 Studio 应用程序集。
- **运行观测**：N14/N15 持有的是观测订阅句柄，不持有 MRK 的执行计划、MDS 的内存数据或真实硬件会话。关闭文档与重新构建/切换 Plan 时，必须先取消旧观察。

### 29.2 各类候选的先后依赖

```text
现有 MEM/MPM/MTC + MOL + Visual + MOC/MRK/MDS + MHSConfig
    │
    ├── (N01/N02/N04/N05) DocumentKey / Kind / EditorRegistry
    │       └── N03 DocumentCoordinator ──► N06 Selection + N07 Navigation
    │                                        └── N08 ResourceTree + N09 ObjectOutline
    ├── (N10/N12) CommandDefinition/Availability ─► N11 Registry
    │                                                └── S1 MainWindow Ribbon 原位增强
    ├── (N17/N18) WPF Editor Factory/Context ──► N16 ConfigComposite
    │                                        └─────► N19/N20 SchemaEditor Host/Factory
    └── (现有 MOCSourceMap/MRK/MDS) ────────► N13 DiagnosticNavigator
                                         └─► N14 Observation + N15 SubscriptionScope
最终由 Studio 组合以上薄协调器；不存在第二工程、第二执行器或第二堆。
```

## 30. 明确不建立的重复类型与错误方向

| ID | 禁止新增的类型/机制 | 必须复用的权威Owner | 判定理由 |
|---|---|---|---|
| X01 | 第二MEM Project/Resource树 | MEMProject/IMEMResource | 已禁止，唯一工程组织 |
| X02 | 第二Studio工程UndoManager | MEMEngineeringSession/MTCEditSession | 所有持久化编辑回到原事务与撤销 |
| X03 | 第二MPM Schema/PropertyStore | MPMPropertyDescriptor/MHSConfigEditorModel | 按工程/配置各自Owner区分，不创建第三仓 |
| X04 | 第二持久化流程Graph | MOLLogicDefinition/MOLWiringDefinition | 画布只存视图位置与受控定义 |
| X05 | Studio自有MOC编译器 | MOCEngineeringCompiler/MOCBuildService | 命令只调用已批准MOC构建 |
| X06 | Studio自有MRK Runtime/Debugger | MRKExecutionEngine/MRKExecutionDebugController | UI只发送受控命令与观察 |
| X07 | Studio私有MDS DataHeap/VariableStore | MDSDataHeap/MDSDataContext | 运行帧不进MEM |
| X08 | 新建StudioShell类替代MainWindow | MainWindow（StudioShell.cs partial） | S1已有真实实现；后续仅原位拆职责 |
| X09 | 独立StudioDockLayoutService立即复制现有布局 | MainWindow.SerializeStudioLayout/RestoreStudioLayout | 除非复杂度证据充分，否则原位增强；不创建第二配置 |
| X10 | 第二图元库/Path算法 | FGEPath2D/MRCShapeDefinition/MVIVisualEditing | 延用基础几何与视觉编辑 |
| X11 | 第二CapabilityCatalog | MOCCapabilityCatalog | 专业注册按MOC真能力目录；UI只贡献展示 |
| X12 | 第二主题/资源存储仓 | MainWindow.ApplyStudioTheme/MRCVisualStyle/MEMAssetReference | 用户布局/主题和工程样式分层，复用现有 |

## 31. 仍需专业源码和兼容性证据的事项

| ID | 待调查专题 | 当前可确认的事实 | 最终业务Owner | 下阶段必要核查 |
|---|---|---|---|---|
| Q01 | Studio V1.2全文与原稿图示 | Studio S1实施文档提及V1.2，当前尚未从仓库检索到其完整正文 | Studio Shell + UI总规范 | 获取全本后对照12章，不阻塞“唯一Owner”基本裁决 |
| Q02 | AvalonDock 5.0.1集成及CI | S1源码已接入Dirkster.AvalonDock系列5.0.1；运行验收日志本轮未核 | CxVisual.Studio / Windows workflow | 按最新SHA检查MSBuild、真STA、布局循环与高DPI |
| Q03 | 跨定义批量Schema兼容性 | R04已有同定义实例批量基础；跨定义安全交集和只读合同待核 | MPM + Studio | 类型、单位、必填、可见、权限和有效绑定冲突不能靠同名判断 |
| Q04 | MHSConfig与MPM的编辑器联用 | 配置分层和工程Schema分别在Data与Core；二者不是同一个存储模型 | Mechanism.Data/Core + WPF | 共享UI编辑模板，提交责任按Owner区分 |
| Q05 | 完整HMI/Inspection/Simulation专业资源 | 当前对应Professional项目早期编译清单未展示专业实体；不能推断旧六仓不存在 | 各Professional Owner | 先扫描主仓新提交及x2/旧生产系统，不能臆定领域类型 |
| Q06 | CT3D Viewer/大体积数据句柄 | CT3D当前仅核到Options/Rules；外部cx-cct-x3成熟资产尚未逐类型复核 | Professional.CT3D + 外部CT库 + WPF | 明确坐标、数据句柄、生命周期及线程，不把Volume放MEM |
| Q07 | Vision图像/AI/ROI专业编排 | Vision阈值和数据流基础已有，但完整AI/ROI链未由现有类型清单证明 | Professional.Vision + MOC/MRK/MDS | 逐专业API复用后再裁决具体Viewer/算法类型 |
| Q08 | 设备控制与安全互锁 | MEMAccessPolicy/MRKPlanPublicationPolicy存在，但实机PLC/曝光/运动授权未验证 | Professional.Automation及运行策略 | Design/Preview禁止真指令；Debug/Run须专业安全及实机签收 |
| Q09 | 运行帧失联/质量/限频合同 | 当前MDSBinding/VisualEvaluator存在，质量戳与UI负载压测缺实测 | Mechanism.Data/Visual + Studio | 按原数据查询快照扩展，避免再造数据事实 |
| Q10 | 工程组织精确子方面Owner编号 | 第一阶段仅按项目总子方面范围归属；各类型细到Mxx需核04最新规格 | 四大规格+现有.csproj | 第三阶段逐类定子方面、namespace与调用边界 |

## 32. EP01～EP15 → 原有类型/候选协同类型的全域映射

| 需求 | 核心角色/案例 | 复用/增强 ID | 新增职责候选 ID | 未完成的代表性操作验收 |
|---|---|---|---|---|
| EP01 | R3/R6；U2/U3 | C01/C02/S01/S02 | N01～N09、N16～N18 | 双层导航、多Tab、三形态配置/画布/Viewer、布局完整行为 |
| EP02 | R1/R3；U2 | F01/C06/C08/V01/V02/W01 | N05/N09/N17 | 九形状/路径/安全资产、真画布、嵌套坐标 |
| EP03 | R1/R3/R6；U2 | C02/V01/V03/W01/W02 | N06/N09 | 多选/路径/拖拽/Undo一次事务、DPI |
| EP04 | R1/R3；U1/U2 | C04/D03/D04/S03/W04 | N16/N19/N20 | Schema复合值/混合值、定义实例只读、配置组合 |
| EP05 | R2/R3；U2 | C03/C07/W01 | N19（样式入口沿用） | Theme/资产来源、状态层叠与安全校验 |
| EP06 | R2/R3/R4；U2 | C08/W03 | N17/N19 | 六基础Widget真实事件/绑定与焦点生命周期 |
| EP07 | R2/R3/R4；U2/U4 | C04/V02/V04/D01/D02 | N14/N15 | 运行帧、质量/断线、订阅/实例隔离 |
| EP08 | R2/R4/R6；U2/U5 | C09/V04/R03/S01 | N10～N12/N14 | 动作命令/权限/联锁/程序刷新来源 |
| EP09 | R2/R3/R5；U3 | C05/V05/W02/O02 | N05～N07/N09 | 主子子流程/Port/能力模板与导航 |
| EP10 | R1/R3/R6；U3 | O01/O02/O03/R03 | N10/N11/N13 | MOC诊断→编辑器定位，Plan/Revision锁 |
| EP11 | R3/R4/R5；U2/U3/U4 | R01/R02/D01/D02/S04 | N14/N15 | 真运行会话、数据质量/复用Viewer/限频 |
| EP12 | R3/R5；U3/U5 | R02/R03/O02/S04 | N06/N07/N13/N14 | 断点/Watch/SourceMap双向定位 |
| EP13 | R2/R3；U3/U4 | P01/P02/P03/O03/W04 | N04/N05/N17/N18/N20 | 专业Workbench UI贡献接口与外部资产审计 |
| EP14 | R1/R2/R3；U1 | C01/C02/C04/V01/O03/S01 | N01～N05/N10/N11/N17～N20 | 一条开发者API与Studio操作链，不复制专业SDK |
| EP15 | R5/R6；U5 | C02/C03/R03/S05 | N12/N13/N15 | 错误反证、退出释放、高DPI/Windows CI和实机分级验收 |

## 33. 按两种用户视角反证新增的合理性

### 33.1 软件开发者：最少调用闭环

1. 使用 **现有** `MEMEngineering.Create`/`MEMEngineeringSession` 创建资源、定义及实例，用 MPM 声明公开参数与 Port（EP01/04/14）；不要求专业程序调用 Studio ViewModel。
2. 根据源 Resource/专业类型，Studio 组合根通过 **候选** N04/N05 绑定到 WPF N17 工厂，由 N03 打开同一资源的不同 Editor Tab；业务开发者只提供专业描述/能力/Viewer Adapter，不直接管理 Ribbon、Docking、Project、Undo。
3. 修改持久化值走 MTC/MEM 或 MHSConfig 的既有提交机制；Build 返回 MOC 计划与诊断；Runtime由 MRK 启动，MDS 提供数据观察。候选 N13/N14 只把这些结果投影回统一 UI。
4. **失败反证**：没有注册专业 UI Editor 时应显示可定位“未提供编辑器”错误，但项目可按原 Core 保持完整，不得自动构造未知类型或丢失数据；无合法授权不能发真实设备命令。

### 33.2 工程编排用户：同一项目三种编辑器

1. 在左侧 Resource Explorer（N08）选“检测方案”，N03/N05 进入**配置组合面板**（N16）并显示原有 MHSConfig/MPM 的真实值和来源；不会弹出一串彼此不关联的表单。
2. 选择“HMI电机Symbol”进入已有 SceneView/CxVisualEditor 图元画布；N06 同步右侧属性与 Outline（N09），实例 A/B 的覆盖独立；点击 Definition 可用 N07 返回原实例。
3. 双击“检测主流程”进入 MOLFlowCanvas；进入 AI 与 CV 子流程，每个独立资源 Tab，共享 N03/N06/N07 的跳转及选择上下文；MOC按原 Owner 验证逻辑。
4. 点击 Trace 的错误节点，经 N13 用实际 MOCSourceMap 找到原 AI 子节点、父流程调用和 HMI 工位；N14观察真实 MRK/MDS 数据，关 Tab 由 N15 退订。
5. **失败反证**：不兼容多选必须可解释只读，未授权设备动作拒绝，布局保存不影响 MEM Revision，错误或过期计划不能伪造已发布/已运行。

### 33.3 形态、责任、测试层级区分

| 编排对象 | 当前存在的原始能力 | 阶段二协调候选 | 阶段三必须补的验收证据 |
|---|---|---|---|
| **A 图元画布** | FGE/MRC/CxVisualEditor/SceneView | N03/N06/N09/N19 | Path/Bezier/Arc真交互、Undo/Redo、DPI、保存重开、资源缺失与安全反例 |
| **B Symbol两实例** | MEMSymbol/MRIResourceReference/MPM/MDS/Widget | N06/N07/N14/N19 | 实例变量独立、真实Widget、权限/只读、配置与运行帧不串值 |
| **C X-Ray 主AI/CV多子流** | MOLFlowCanvas/MOCSourceMap/MRK/MDS/Vision provider | N03/N07/N13/N14/N15/N17 | 主+多个子流程、有效Port、真实Build/Run、Trace/Watch/SourceMap定位和重建 |
| **Studio Shell 基础** | S1 MainWindow partial + AvalonDock5.0.1 + 自测源码 | N01～N05/N10～N12 | 同SHA Windows S1真STA、编辑器资源实例身份、双屏DPI及布局恢复循环 |

## 34. 阶段二统计、定案边界和阶段三输入

| 项目 | 本阶段结果 | 状态说明 |
|---|---:|---|
| 已核既有类型处理组 | **38** | 逐项“直接复用/原位增强”，可映射到阶段一305型；不计为新增源码 |
| 新增跨模块协同职责候选 | **20** | 含类/接口/枚举/结构体，拟定单一项目Owner；待第三阶段核定最终命名/合并 |
| 明确禁止的重复机制 | **12** | 不创建重复持久化、编译、运行/Undo等核心 |
| 专业与集成待核议题 | **10** | 专业类型数待实际专业仓库审计，不凭本阶段强推 |
| 需求覆盖 | **15/15 EP** | 形成需求→既有类型/必要候选→用例/验收映射 |
| 本轮新增业务源码 / 项目 | **0 / 0** | 本次是**架构审计与归属裁决**，没有生成空类 |
| Windows 编译、STA、专业设备验收 | **未在本轮运行** | 保持证据等级未验，不虚报通过 |

### 34.1 本阶段裁决的性质

- **已确定**：第一阶段305型现有 Owner 不迁移；已有S1 Shell使用 AvalonDock5.0.1 事实更新；新增职责集中在**Studio应用协同与既有WPF适配层**；Foundation/Mechanism/Professional 核心无第二权威模型，28项目不新增。
- **类型候选待落码确认**：N01～N20 是完整需求的最小协同功能**候选集合**，并非一次创建20个 `.cs`，其独立形态、公有程度、泛型/工厂签名及并发/线程契约在第三阶段逐项合并审核。某些可以作为现有 `MainWindow` 或 `StudioViewModel` 的 private/internal 内嵌类型，以降低多类无意义抽象。
- **专业待核**：CT3D/Inspection/Simulation/HMI/AI Viewer、实机授权、安全与外部成熟系统复用不得在没有源码/设备证据时定具体算法/硬件类。
- **后续第三阶段**：按 `EP→使用样例→类型→属性/方法→Owner依赖→测试` 分层对照，对 R/E/N 每个保留类型写**真正签名、返回、异常、线程、生命周期、序列化、版本、安全、可测性**；核实当前最新 Studio V1.2，并安排依赖/Windows STA/专业实测。

---

## 35. 第三阶段执行边界与真实源码新变更

> **第三阶段交付性质**：在第一阶段305项真实类型归属、第二阶段38组复用增强/20候选/12项禁止重复机制的基础上，制定**可实施的全类契约与证据矩阵**。**既有 API** 仅据当前源码陈述；**拟定 API** 是未来开发契约，并非已创建、已编译或验收通过。此阶段不增加项目、不改C#业务源码，不推测尚未获取的Studio V1.2全文。

- **新增源码复核（2026-10-21）**：`Applications/Studio/StudioShell.ResourceTree.cs` 已实现 `MainWindow.RefreshStudioResourceTree()`、搜索/资源选择路由；`StudioShell.GraphicsTree.cs` 已实现 `RefreshStudioGraphicsTree()` 和 `tool.graphics` 独立停靠面板、对象选择及焦点恢复。二者均是**现有MainWindow partial**，不得误写为新的实体类；`StudioShellSelfTests` 是阶段一快照之后新增的真实类（源码存在，Windows测试状态另核）。
- `StudioShell.cs` 已有 `OpenStudioDocument(string id)`、`CloseStudioDocument(string id)`、`ApplyStudioTheme(bool dark)`、`SerializeStudioLayout()`、`SaveStudioLayout(string path)`、`RestoreStudioLayout(string path)`；当前文档仍应区分基于已知 `ContentId` 的S1入口与任意工程ResourceId的多文档编辑器。
- **20候选重新判定**：N08 ResourceTree、N09 GraphicsTree 不再默认按新类落码，优先原位增强现有两个 partial 文件；N04/N05、N10/N11、N15 可按实际复杂度合并；N01/N02作为轻量值/枚举而非服务，N16～N20主要扩展原WPF项目。**设计对象数 ≠ 应立即新建的源码类数**。
- **生命周期**：权威工程状态只有MEM+MTC；当前编辑器、选择、停靠和布局是UI状态；MOC计划/SourceMap是版本化构建成果；MRK会话、MDS帧和Viewer句柄是短生命周期运行态。所有“属性/方法”遵循此划分。

## 36. 现有关键权威 API 的实际签名（源码核实）

> 本节列出决定分层依赖的**已读取源码中的真实入口**；未逐字列出的所有重载以后方 §43§ 逐类型源码契约盘点为准，不得据此推断没有别的现有方法。

| 已有类型/范围 | Owner | 已核实际API（重点入口） | 使用边界 | 依据 |
|---|---|---|---|---|
| `MEMEngineering` | Mechanism.Core | `MEMEngineering.Create(string, IMEMProjectStore=null, IMEMAccessPolicy=null): MEMEngineeringSession`；`Open(string, IMEMProjectStore=null, IMEMAccessPolicy=null): MEMEngineeringSession` | 工程会话创建、加载、原始权限 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MEMEngineeringSession` | Mechanism.Core | `Edit<T>(string, int expectedRevision, Func<MTCEditSession,T>):MTCEditResult<T>`；`Undo():MTCEditResult<bool>`；`Redo():MTCEditResult<bool>`；`Watch(Action<MTCChangeSet>):IDisposable`；`SaveAs(string):void` | 唯一事务、历史、回调、保存；Revision不能被运行帧修改 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MEMEngineeringSession（属性合同）` | Mechanism.Core | `DefineProperty(MEMNode, MPMPropertyDescriptor):MPMProperty`；`SetProperty(MEMNode,string,MPMBinding):void`；`SetOverride(MRIResourceReference,string,MPMBinding):void`；`ClearOverride(MRIResourceReference,string):void`；`ResolveParameter(MRIResourceReference,string):MPMBinding` | 定义/实例来源分离；只读校验由MTC/Core执行 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.Contracts.cs) |
| `MPMPropertyDescriptor` | Mechanism.Core | `MPMPropertyDescriptor(string memberId, MPMValueKind valueKind, bool required=false, bool isPublic=true, MPMBinding defaultBinding=null, double? minimum=null, double? maximum=null, bool isReadOnly=false)` | Schema唯一声明；IsPublic/IsReadOnly不是UI本地标志 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMBinding` | Mechanism.Core | `Literal(MPMValue):MPMBinding`；`Variable(string,string="value"):MPMBinding`；`Resource(Guid):MPMBinding`；`Member(MRIObjectAddress,string):MPMBinding`；`Expression(string):MPMBinding` | 持久绑定意图而非实时数据值 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `CxVisualEditor` | Mechanism.Visual | `OpenDefinition(Guid):void`；`ReturnToPrevious():bool`；`AddShape(string,MRCShapeDefinition,double,double):MTCEditResult<bool>`；`MoveSelection(IReadOnlyList<Guid>,double,double,bool=false):MTCEditResult<bool>` | 复用原MEM事务与历史；新增Studio导航不复制Editor | [源码](../../Mechanism/Visual/CxVisualEditor.cs) |
| `SceneView` | Mechanism.Presentation.Wpf | `SetSelectedVisualIds(IEnumerable<Guid>):void`；`SetScene(IList<CxSceneItem>,Guid?):void`；`SetViewport(double,double,double):void` | WPF图形输入、选中投影和绘制，Dispatcher线程调用 | [源码](../../Mechanism/Presentation.Wpf/SceneView.cs) |
| `MOLFlowCanvas` | Mechanism.Presentation.Wpf | `SetSelection(IEnumerable<Guid>):void`；`GetVisiblePorts():IReadOnlyList<MOLPortVisual>` | 流程画布为MOL投影，不持有第二份逻辑定义 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MPWConfigEditorControl<T>` | Mechanism.Presentation.Wpf | `SaveDraftAsync(CancellationToken):Task<MHSConfigCommitResult>`；`Dispose():void`；按源码提供受控构造器与UI模板 | 配置受控草稿/提交，不直接修改独立配置存储 | [源码](../../Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs) |
| `MHSConfigEditorModel<T>` | Mechanism.Data | `DescribeFields(...):IList<MHSConfigEditorField>`；`Preview(...):MHSConfigPreview`；`CommitAsync(...):Task<MHSConfigCommitResult>`；`Undo/Redo`（以源码重载为准） | 层级配置编辑，区别于MPM工程Schema | [源码](../../Mechanism/Data/Configuration/MHSConfigEditorModel.cs) |
| `MOCBuildService` | Mechanism.Orchestration | `Build(MEMProjectSnapshot, Guid contentId, IMRKCapabilityResolver):MOCBuildResult` | 构建返回Result、Diagnostics、Plan；不直接执行设备 | [源码](../../Mechanism/Orchestration/MOCBuildService.cs) |
| `MOCSourceMap` | Mechanism.Orchestration | `TryResolve(string compiledStepId, out MOCStepOrigin):bool`；`Find(Guid blockId):IList<MOCStepOrigin>` | 流程构建/执行与原对象定位依据，不按名称猜测 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MRKRuntimeSession` | Mechanism.Runtime | `MRKRuntimeSession(string id,MRKExecutionEngine,MRKExecutionPlanSlot,int maxInFlight=1)`；`Start():void`；`Snapshot():MRKRuntimeSessionSnapshot`；`StopAsync(bool cancel=false):Task` | 会话/计划由MRK持有，不为Studio建立执行器 | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs) |
| `MDSDataBinding` | Mechanism.Data | `MDSDataBinding(MDSBindingSource,Type,string sourceName=null,object constant=null)`；`MDSDataBindingResolver.Resolve<T>(...)`（依源码确切重载） | 变量/运行数据意图与质量标记分离 | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs) |
| `MainWindow（Studio Shell）` | CxVisual.Studio | `OpenStudioDocument(string):void`；`GetStudioContentIds():string[]`；`ApplyStudioTheme(bool):void`；`SaveStudioLayout(string):void`；`RestoreStudioLayout(string):void` | 当前S1存在，专业资源文档协调器不得取代其窗口/布局 | [源码](../../Applications/Studio/StudioShell.cs) |
| `MainWindow（资源/图形结构）` | CxVisual.Studio | `RefreshStudioResourceTree():void`；`RefreshStudioGraphicsTree():void` | 新增partial扩展：资源/图形结构各自投影，未新增独立工程对象 | [源码](../../Applications/Studio/StudioShell.ResourceTree.cs) |

## 37. N01～N20 候选类型的完整契约规格与裁决

> 下表**签名为“设计拟定”，不声称已有源码**。签名展示方法名、主要参数和返回，阶段三已将每项的数据、方法、失败边界、线程及生命周期明确到可实施层级；**源码最终重载应在创建前和实际现有类型/测试做签名冲突校验**。WPF特定的 `FrameworkElement`/`UserControl` 只允许 WPF 工程使用。

### 37.1 N01　`StudioDocumentKey`（struct）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**需求保留；未实施**，不是已实施/已验收 |
| 属性/状态 | ProjectId:Guid; ResourceId:Guid?; ReferenceId:Guid?; EditorKind:StudioEditorKind; StableKey:string（派生） |
| 核心方法（拟定） | Create(Guid projectId, Guid? resourceId, StudioEditorKind kind, Guid? referenceId=null):StudioDocumentKey; Equals(object):bool; GetHashCode():int |
| 不变量/异常/版本/线程 | 资源ID必须来自MEM；Home允许空ResourceId；不保存真实工程对象；稳定键不从名称生成；按值相等；跨项目不可复用；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP14；资源Tab去重、定义/实例、关闭重开、非法空或跨工程资源拒绝 |
| 验收用例ID | `StudioDocuments-01`（Windows STA / Core 组合测试待执行） |

### 37.2 N02　`StudioEditorKind`（enum）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**需求保留；未实施**，不是已实施/已验收 |
| 属性/状态 | Home; Configuration; Visual; Flow; ProfessionalViewer |
| 核心方法（拟定） | 由已注册资源类型匹配；不得直接根据名字猜。 |
| 不变量/异常/版本/线程 | UI分类而非MEMNodeKind/MRCShapeKind或专业ResourceKind，不加入MEM存储；未知类型必须给缺编辑器诊断；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13；三主编辑器+专业Viewer切换 |
| 验收用例ID | `StudioDocuments-02`（Windows STA / Core 组合测试待执行） |

### 37.3 N03　`StudioDocumentCoordinator`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**候选；优先从现有MainWindow提取**，不是已实施/已验收 |
| 属性/状态 | ProjectSession:MEMEngineeringSession（只读引用）; ActiveKey:StudioDocumentKey?; OpenDocuments:只读集合 |
| 核心方法（拟定） | Open(StudioDocumentKey key):StudioDocumentKey; Activate(StudioDocumentKey key):bool; Close(StudioDocumentKey key):bool; CloseAll():void; Find(StudioDocumentKey key):编辑器会话? |
| 不变量/异常/版本/线程 | 复用S1 AvalonDock LayoutDocument、MainWindow.OpenStudioDocument；公开数据不暴露可修改集合；切换/关闭不更改MEM Revision；重复打开同Key只有一个Tab；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP14；任意ResourceId、多个AI/CV子流及同资源复用 |
| 验收用例ID | `StudioDocuments-03`（Windows STA / Core 组合测试待执行） |

### 37.4 N04　`StudioEditorDescriptor`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**候选；可并入Registry**，不是已实施/已验收 |
| 属性/状态 | EditorKind:StudioEditorKind; ResourceTypeId:string; DisplayName:string; SupportedModes:只读模式集合; Priority:int |
| 核心方法（拟定） | Matches(string resourceTypeId, StudioEditorKind kind, string mode):bool; Describe():string |
| 不变量/异常/版本/线程 | 只读描述；不存MOL/MPM定义或Viewer实例；Descriptor冲突按明确优先级裁决，不静默选错专业；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13/EP14；专业编辑器发现与失败占位 |
| 验收用例ID | `StudioEditors-01`（Windows STA / Core 组合测试待执行） |

### 37.5 N05　`StudioEditorRegistry`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**候选；需要时与N04合并**，不是已实施/已验收 |
| 属性/状态 | Factories:只读注册目录; Project:现有上下文引用 |
| 核心方法（拟定） | Register(StudioEditorDescriptor descriptor, IMPWResourceEditorFactory factory):void; Resolve(resourceTypeId:string, mode:string):IMPWResourceEditorFactory?; Unregister(resourceTypeId:string):bool |
| 不变量/异常/版本/线程 | 重复注册拒绝/明示优先级；只存工厂引用；禁止保存工程字段；Factory失败必须销毁半初始化控件并给出诊断；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13/EP14；配置、Scene、Flow、CT专业Viewer适配 |
| 验收用例ID | `StudioEditors-02`（Windows STA / Core 组合测试待执行） |

### 37.6 N06　`StudioSelectionCoordinator`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**原位增强优先；独立化待定**，不是已实施/已验收 |
| 属性/状态 | SelectedResourceId:Guid?; SelectedObjectIds:IReadOnlyList<Guid>; SelectionRevision:int; ActiveDocument:StudioDocumentKey |
| 核心方法（拟定） | SetSelection(resourceId:Guid, ids:IEnumerable<Guid>, expectedRevision:int):bool; Clear():void; Changed:event |
| 不变量/异常/版本/线程 | 防重入；更新Canvas/GraphicsTree/Property同一选择；过期ID/跨资源混选拒绝；Selection不写MEM；WPF Dispatcher线程投影；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP03/EP04/EP09；多选同定义属性、结构树/场景双向选中 |
| 验收用例ID | `StudioSelection-01`（Windows STA / Core 组合测试待执行） |

### 37.7 N07　`StudioNavigationHistory`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**候选；优先复用CxVisualEditor内跳转栈**，不是已实施/已验收 |
| 属性/状态 | Current:StudioDocumentKey; BackEntries/ForwardEntries:只读；目标地址MRIObjectAddress |
| 核心方法（拟定） | Navigate(StudioDocumentKey key,MRIObjectAddress address):void; Back():bool; Forward():bool; ClearProject():void |
| 不变量/异常/版本/线程 | 栈只保存ID/视口/焦点，不保存MEMNode副本；若目标失效显示可诊断错误，不跳到同名对象；项目切换清除历史；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP09/EP12；实例→定义→AI子流→父流→HMI工位往返 |
| 验收用例ID | `StudioNavigation-01`（Windows STA / Core 组合测试待执行） |

### 37.8 N08　`StudioResourceTreeProjection`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**并入现有StudioShell.ResourceTree.cs为优先**，不是已实施/已验收 |
| 属性/状态 | Rows:按MEM资源身份只读生成; Filter:string; ExpandedIds:用户偏好 |
| 核心方法（拟定） | Project(MEMEngineeringSession session,string filter):只读投影; FindResource(Guid id):树行? |
| 不变量/异常/版本/线程 | 现有MainWindow.RefreshStudioResourceTree已可用；不复制MEM树，不含Group/Element；独立类只有复杂查询/自动测试证据充分才新增；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13；工程资源搜索、专业资源分类和引用导航 |
| 验收用例ID | `StudioTree-01`（Windows STA / Core 组合测试待执行） |

### 37.9 N09　`StudioObjectOutlineProjection`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**并入现有StudioShell.GraphicsTree.cs为优先**，不是已实施/已验收 |
| 属性/状态 | Rows:当前MEM资源内部Group/Element的只读投影; SelectedIds:只读 |
| 核心方法（拟定） | Project(Guid resourceId):只读对象树; Select(Guid id):bool; Highlight(IEnumerable<Guid>):void |
| 不变量/异常/版本/线程 | 已有RefreshStudioGraphicsTree和tool.graphics独立停靠面板；不重复造TreeView，不持久化图层；流程Outline节点可单独通过现有MOL视图投影扩充；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP02/EP03/EP09；Layer/Group/Element选择、画布焦点不被工具窗抢占 |
| 验收用例ID | `StudioTree-02`（Windows STA / Core 组合测试待执行） |

### 37.10 N10　`StudioCommandDescriptor`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**拟新增UI层命令描述**，不是已实施/已验收 |
| 属性/状态 | Id:string; Label:string; Group:string; Scope:Workbench/Resource/Mode; Shortcut:string?; Action:受控委托 |
| 核心方法（拟定） | CanExecute(只读当前上下文):StudioCommandAvailability; Execute(只读上下文):命令结果（用原Owner返回） |
| 不变量/异常/版本/线程 | UI描述不包含专业执行逻辑；命令Id稳定、重复拒绝；无权限不执行；不为Run/Debug创建第二运行引擎；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP08/EP14；菜单/Ribbon/右键/快捷键同一命令 |
| 验收用例ID | `StudioCommands-01`（Windows STA / Core 组合测试待执行） |

### 37.11 N11　`StudioCommandRegistry`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**增强MainWindow现有UpdateStudioRibbon，视复杂度分离**，不是已实施/已验收 |
| 属性/状态 | Commands:只读注册目录; ActiveContext:现有Studio上下文投影 |
| 核心方法（拟定） | Register(StudioCommandDescriptor command):void; Evaluate(string id):StudioCommandAvailability; Execute(string id):命令结果; GetRibbonGroups():只读集合 |
| 不变量/异常/版本/线程 | 有且仅有一条执行路径；专业只能贡献命令或绑定原Owner；原S1九菜单仍是UI宿主，不再新增第二套Ribbon；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP08/EP13/EP14；切换HMI/流程/配置时工具与权限自然变化 |
| 验收用例ID | `StudioCommands-02`（Windows STA / Core 组合测试待执行） |

### 37.12 N12　`StudioCommandAvailability`（struct）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**拟新增轻量值对象；可嵌于N10**，不是已实施/已验收 |
| 属性/状态 | Visible:bool; Enabled:bool; DisabledReason:string; RequiresPermission:bool |
| 核心方法（拟定） | Allowed():StudioCommandAvailability; Denied(string reason):StudioCommandAvailability |
| 不变量/异常/版本/线程 | 仅供UI；不代替Core权限验证；DisabledReason需具备可见诊断；不可变值类型；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP08/EP15；无权限/工程未构建/无资源时明确禁用原因 |
| 验收用例ID | `StudioCommands-03`（Windows STA / Core 组合测试待执行） |

### 37.13 N13　`StudioDiagnosticNavigator`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**拟新增跨机制UI协调**，不是已实施/已验收 |
| 属性/状态 | SourceMap:MOCSourceMap（只读引用）; ActiveProjectId:Guid; MappingDiagnostics:只读集合 |
| 核心方法（拟定） | Navigate(MOCDiagnostic diagnostic):bool; NavigateStep(string compiledStepId):bool; NavigateTrace(运行观察引用):bool |
| 不变量/异常/版本/线程 | 必须检验计划指纹/RunSession/ResourceId；只通过真实MOC映射定位；映射缺失返回未解析，不按字符串猜测对象；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP10/EP12；子流程节点→父流程→HMI，失败可定位 |
| 验收用例ID | `StudioDiagnostic-01`（Windows STA / Core 组合测试待执行） |

### 37.14 N14　`StudioRuntimeObservationCoordinator`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**拟新增跨MRK/MDS的只读投影协调**，不是已实施/已验收 |
| 属性/状态 | Session:MRKRuntimeSession?; DataContext:IMDSDataContext?; CurrentPlanFingerprint:string?; IsAttached:bool |
| 核心方法（拟定） | Attach(受控会话与绑定):void; ReadFrame():只读UI快照; PauseObservation():void; Detach():void; Dispose():void |
| 不变量/异常/版本/线程 | 不得创建MRK执行引擎或MDS Heap；采用有界UI更新/订阅，UI Dispatcher边界清楚；Detach后旧帧不再更新新项目；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP07/EP11/EP12；Watch、Flow Overlay、HMI状态联动和断连质量 |
| 验收用例ID | `StudioObserve-01`（Windows STA / Core 组合测试待执行） |

### 37.15 N15　`StudioViewSubscriptionScope`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Studio`；**拟新增internal聚合器（如现有Dispose覆盖足够可合并）**，不是已实施/已验收 |
| 属性/状态 | OwnerDocument:StudioDocumentKey; IsDisposed:bool; Subscriptions:私有IDisposable集合 |
| 核心方法（拟定） | Add(IDisposable subscription):void; Clear():void; Dispose():void |
| 不变量/异常/版本/线程 | 重复Dispose幂等；添加到已释放作用域须拒绝并即时释放；窗口/Tab关闭和工程切换调用；不持有设备真实资源租约；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP07/EP11/EP15；关闭文档后不再响应旧MRK/MDS事件 |
| 验收用例ID | `StudioObserve-02`（Windows STA / Core 组合测试待执行） |

### 37.16 N16　`MPWConfigCompositePanel`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Mechanism.Presentation.Wpf`；**拟新增可复用UserControl**，不是已实施/已验收 |
| 属性/状态 | SectionModels:只读组合; Columns:int; LayoutMode:Auto/Single/Double/Triple; IsDirty:bool |
| 核心方法（拟定） | SetSections(既有受控Config编辑模型集合):void; Reflow(double width):void; ApplyAsync(CancellationToken):Task<受控提交结果>; CancelDraft():void; Dispose():void |
| 不变量/异常/版本/线程 | 仅组合MPWConfigEditorControl<T>/MHS授权节；不得建立自己的配置字典或Undo；响应式1×N/2×N/3×N；不可吞掉事务失败；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP04/EP13；设备/检测方案多个配置区在中央同时可操作 |
| 验收用例ID | `WpfConfig-01`（Windows STA / Core 组合测试待执行） |

### 37.17 N17　`IMPWResourceEditorFactory`（interface）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Mechanism.Presentation.Wpf`；**拟新增可扩展WPF界面合同**，不是已实施/已验收 |
| 属性/状态 | ResourceTypeId:string（只读）; EditorKindId:string（避免依赖Studio枚举） |
| 核心方法（拟定） | CanCreate(MPWResourceEditorContext context):bool; Create(MPWResourceEditorContext context):FrameworkElement |
| 不变量/异常/版本/线程 | WPF工程可以引用纯Mechanism资源/Context，但不得引用CxVisual.Studio；专业Viewer通过应用组合根桥接，宿主负责Dispose/异常回滚；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13/EP14；专业增加Viewer无需修改六套Shell |
| 验收用例ID | `WpfEditor-01`（Windows STA / Core 组合测试待执行） |

### 37.18 N18　`MPWResourceEditorContext`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Mechanism.Presentation.Wpf`；**拟新增只读受控上下文**，不是已实施/已验收 |
| 属性/状态 | ProjectId:Guid; ResourceId:Guid?; ReferenceId:Guid?; Engineering:受控只读查询/事务入口; Mode:枚举/字符串适配 |
| 核心方法（拟定） | DemandValid():void; CreateSubscriptionScope():IDisposable（由宿主提供） |
| 不变量/异常/版本/线程 | 不能把MainWindow、原生设备会话或可任意修改MEM集合裸传给第三方；WPF专用上下文不进入Core序列化；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP01/EP13/EP14；从Vision/CT3D专业Editor查资源/请求受控事务 |
| 验收用例ID | `WpfEditor-02`（Windows STA / Core 组合测试待执行） |

### 37.19 N19　`MPWSchemaPropertyEditorHost`（class）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Mechanism.Presentation.Wpf`；**拟新增统一类型模板UserControl**，不是已实施/已验收 |
| 属性/状态 | SchemaSource:MPM或MHS适配器; Items:只读属性投影; SelectionState:只读 |
| 核心方法（拟定） | SetSchema(只读Schema适配器):void; SetSelection(对象ID集合):void; CommitAsync(CancellationToken):Task<受控结果>; RevertDraft():void |
| 不变量/异常/版本/线程 | MPM和MHS各保留不同提交/校验Owner；三态Mixed、资源选择、ReadOnly/错误可见；不将枚举/复杂属性降格字符串保存；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP04/EP13/EP14；统一右侧属性面板和配置组合区的编辑体验 |
| 验收用例ID | `WpfSchema-01`（Windows STA / Core 组合测试待执行） |

### 37.20 N20　`IMPWSchemaEditorFactory`（interface）

| 项 | 已裁决契约 |
|---|---|
| 所属与状态 | `CxVisual.Mechanism.Presentation.Wpf`；**拟新增专业属性控件注册**，不是已实施/已验收 |
| 属性/状态 | SupportsValueKind:string/类型标识; Priority:int |
| 核心方法（拟定） | CanEdit(只读Schema描述,当前模式):bool; CreateEditor(只读属性上下文):FrameworkElement |
| 不变量/异常/版本/线程 | 不能反向要求Foundation引用WPF；专业受控模板仅处理输入/展示，真实写入回到MPM/MHS；冲突显式诊断；UI交互只在WPF Dispatcher，来自MRK/MDS异步数据必须先复制为不可变观察快照后调度 |
| 对应需求/角色使用 | EP04/EP13/EP14；Color/Brush/Enum/ROI/CT参数复用受控类型化控件 |
| 验收用例ID | `WpfSchema-02`（Windows STA / Core 组合测试待执行） |

## 38. 同名重复、项目依赖与类合并裁决

| 裁决项 | 最终安排 | 不能发生的情况 |
|---|---|---|
| `MainWindow`与“StudioShell” | 复用现有`MainWindow` partial；分担于`StudioShell.cs`、`StudioShell.ResourceTree.cs`、`StudioShell.GraphicsTree.cs` | 不新增第二个主窗口或同名工程Shell |
| N08/N09 两个投影类 | **首选合并入现有partial扩展**；除非UI复用和单元测试证明确有独立类收益，暂不计强制新增类型数 | 不能重复绘制第二个资源树/图形结构面板 |
| N04/N05 | 可以合并为内部注册表/描述条目；保留Resource类型→受控WPF编辑器的单一映射 | 不同时保有StudioRegistry和ProfessionalRegistry两份注册真源 |
| N10/N11/N12 | 共享同一UI命令ID和路由，描述与可用性可为小值对象；S1菜单/Ribbon只负责可见 UI | 不把设备控制、MOC Build直接硬编码到两个Click路径 |
| N14/N15 | N15只负责关闭/取消订阅，可内嵌N14或现有ViewModel；保护真实MRK/MDS源 | 不创建第二DataHeap、执行线程池和RunContext仓 |
| N16/N19/N20 | 统一WPF输入模板/组合容器，分别把提交委托送到MEM/MPM或MHSConfig原Owner | 不复制Schema、设置第二Undo历史或把复合值当纯字符串 |
| `Mechanism.Presentation.Wpf` ↔ Studio | WPF项目**不引用Studio**，接口只采用FrameworkElement/Resource引用等低层契约；由Studio组合根注册 | Professional Core不得调用MainWindow或WPF具体UI |
| 6专业 Workbench | 各专业的逻辑与Provider仍驻留Professional；Viewer可由WPF适配、应用注册 | CT3D/AI设备对象不进入Foundation/Visual纯核心 |

### 38.1 依赖规则（不是新增工程引用指令）

```text
Foundation.Geometry、Foundation.Data（无Mechanism/WPF依赖）
   ↓
Mechanism.Core（MEM/MPM/MOL/MTC/MRC）  Mechanism.Data（MDS/MHS）
   ↓                                     ↓
Mechanism.Visual / Orchestration / Runtime（依当前项目允许的DAG）
   ↓
Mechanism.Presentation.Wpf（Scene / Flow / 配置/属性宿主）
   ↓
Applications.Studio（AvalonDock/MainWindow/注册/导航/诊断/观测组合根）
          ⇐ Professional 的业务能力经现有抽象/受控适配注册
```

此图是**职责方向示意**，不暗示列出的 Mechanism 项目之间全部可互相引用；具体 `.csproj ProjectReference` 和四大规格DAG需逐引用校验。Professional业务Core不反向依赖Studio；适配UI的技术位置在现有WPF或允许的应用组合根。

## 39. 从类型到完整使用体验：两个角色的可执行与拟定合同

### 39.1 软件开发者：真实已有 API 示例（根据当前Core接口编写，非本环境编译证据）

```csharp
using System;
using CxVisual.Mechanism.Core;

// 文件路径由调用者提供。既有 MEM API；源定义与两个实例只有一份工程真源。
using (MEMEngineeringSession engineering = MEMEngineering.Create("电机产线"))
{
    MEMPage page = engineering.AddPage("总览");
    MEMSymbol symbol = engineering.AddSymbol("电机定义");
    engineering.DefineProperty(symbol, new MPMPropertyDescriptor(
        "设备名", MPMValueKind.String, false, true,
        MPMBinding.Literal(MPMValue.String("未绑定"))));
    MRIResourceReference motorA = engineering.PlaceSymbol(
        page.RootContent, symbol, "电机A");
    MRIResourceReference motorB = engineering.PlaceSymbol(
        page.RootContent, symbol, "电机B");
    engineering.Edit("分别覆盖设备名", engineering.Revision, edit =>
    {
        edit.SetOverride(motorA, "设备名", MPMBinding.Literal(MPMValue.String("MotorA")));
        edit.SetOverride(motorB, "设备名", MPMBinding.Literal(MPMValue.String("MotorB")));
        return true;
    }).RequireValue();
    if (!engineering.Validate().IsValid)
        throw new InvalidOperationException("工程校验失败");
    engineering.SaveAs(filePath);
}
```

**此代码只调用真实已读取的 Core API**；编译/运行/图元画布/用户布局等功能不包含在这个示例，不以其代替 Windows 端到端验收。

### 39.2 专业开发者与Studio的目标适配（设计合同，未实施）

```text
// 拟议调用链，非当前SDK方法，严禁直接复制为能编译的实现示例。
EditorDescriptor(ResourceTypeId="ct.volume", EditorKind=ProfessionalViewer)
  -> Registry.Register(Descriptor, AuthorizedViewerFactory)
  -> StudioDocumentCoordinator.Open(Project/Resource/EditorKind)
  -> IMPWResourceEditorFactory.Create(ReadOnlyResourceEditorContext)
  -> UI通过受控原Owner命令处理，关闭调用SubscriptionScope.Dispose()
  -> MRK/MDS只读帧->Viewer投影，MEM Revision不变
```

### 39.3 工程编排用户的真实界面行为链（目标验收脚本）

1. **设备配置**：资源树选择“电机配置”→中央配置组合面板（N16）显示设备、通信、运行绑定的真实Schema；非法值不修改MEM/MHS保存状态。
2. **图元定义**：进入电机Symbol→`SceneView`图元画布；右侧Schema（N19）编辑定义，进入总览页放A/B两实例并分别配置公开属性；选择同步到`tool.graphics`图形结构和属性，不能错改其他实例。
3. **专业流程**：检测主流程→AI子流程→CV子流程各有资源Tab；MOLFlowCanvas的节点/Port由真正MOL定义，应用操作走原事务；从子流程回父流程及设备HMI能恢复焦点。
4. **构建/调试**：MOC真实构建结果、错误、SourceMap经N13定位；MRK真实会话/MDS数据经N14观察，N15在关闭Tab/工程切换时解除订阅；禁止预览发送PLC/曝光/运动指令。
5. **结束**：Studio保存工程、重开，三类编辑器显示稳定ID；布局另行存储；Undo/Redo和无授权反例在Core和WPF层都能复现。

## 40. 全类契约的生命周期、线程、安全和持久化

| 类型组 | 创建/销毁与所有权 | 线程/并发 | 持久化与版本 | 失败保护 |
|---|---|---|---|---|
| MEM/MPM/MTC/MRC | 工程会话拥有真源；事务控制修改；资源ID稳定 | 原事务锁/Revision；UI不得绕过 | 仅MEM工程保存/加载，Dirty/Undo由Core | 失败Edit不部分提交，不可越权清除实例覆盖 |
| MOL/MOC | MOL随工程定义；MOC构建产出版本化Plan/SourceMap | 后台构建用稳定工程Snapshot，不直接改WPF控件 | Plan指纹对应工程Revision/能力版本 | 错Port/错版本时不发布虚假计划 |
| MRK/MDS | MRK会话/资源租约及MDS数据上下文独立释放 | 运行线程与WPF Dispatcher隔离，UI帧有界/限频 | MDS快照/日志另行持有，绝不作为工程Undo | 取消、断连、旧Plan重放必须显式处理 |
| N01～N15（Studio UI协调） | Project/Tab/Window作用域；关闭Tab销毁观测Scope | UI状态在Dispatcher，后台回调调度回UI | Workspace布局持久化独立于MEM；选择和Navigation是会话态 | 旧ResourceId/Revision/Trace映射必须拒绝并诊断 |
| N16～N20（WPF适配） | 宿主创建控件、Dispose / Unregister；专业Viewer资源受Scope监管 | 只允许在WPF线程操作控件，耗时计算/数据在机制/专业线程 | 仅传Schema与受控提交，不建立第二XML/Json仓 | 类型/权限/质量无效时不写工程/设备，不静默强转类型 |

### 40.1 状态标识（阶段三裁决）

- **E0 源码已存在**：能定位真实声明/版本，未证明完整运行；第一阶段305项及本轮新增的MainWindow partial/SelfTests属此级。
- **E1 设计拟定**：N01～N20中的候选职责/属性/方法已经写入本文，尚未落码，不得标为E0或“完成”。
- **E2 测试源码存在**：已经编写用例、能定位文件与预期，仍与Windows运行成功分开。
- **E3 同SHA Windows通过**：引用运行ID、时间、平台、编译/STA日志；GitHub综合状态空不能替代。
- **E4 专业/实机验收**：工位、图像/CT实数据、互锁、授权、性能和运维可重复签收；不能由E3自动推出。

## 41. 类/接口→EP→角色→测试分层验收映射

| 用例 | 需求 | 类型/Owner | 角色/案例 | 正向与失败反证 | 证据等级 |
|---|---|---|---|---|---|
| T01 | EP01/14 | N01～N05；MainWindow/MEM | R1/R3；U1/U2 | 多Tab同ID只一个实例；旧S1打开/关闭不回退；项目切换不串资源；退出后重启恢复独立布局 | Windows STA+Core |
| T02 | EP01/02/03 | N06/N08/N09；SceneView/GraphicsTree | R3；U2 | 资源树无Group/Element；图形结构Tree与画布双向选中；Show不得夺焦点；跨资源选择拒绝 | Windows STA |
| T03 | EP03/04 | MPM/MTC；N19/N20 | R1/R3；U1/U2 | Boolean Mixed三态、合法数值批量事务一次Revision/Undo，越权/过期不修改；配置MHS单独提交 | Core+Windows STA |
| T04 | EP01/04/13 | N16/N17/N18；MHSConfig | R2/R3；U2/U3 | 1/2/3列组合、字段丢焦点/取消、校验、保存重开、权限不足拒绝、草稿不写工程 | Windows STA+Config |
| T05 | EP05/06/07 | MRC/MPWWidgetPresenter/N19/N14 | R2/R3/R4；U2 | 两个电机实例不同绑定、真实WPF Widget更新、时间/质量、断线，Preview不改MEM Revision | Windows STA+MRK/MDS |
| T06 | EP08/15 | N10/N11/N12；MRK/专业策略 | R3/R4/R6；U2/U5 | 九菜单/专业Ribbon同一Command；无权限/联锁不满足禁止设备动作且可见原因 | Windows STA+安全反证 |
| T07 | EP09/10 | MOLFlowCanvas/MOCBuildService/N07 | R3/R5；U3 | 主流程+AI/CV两个子流程，Port/类型/错循环诊断、资源Tab导航/单步返回 | Core+Windows STA |
| T08 | EP10/12 | MOCSourceMap/N13/N07 | R3/R5；U3/U5 | 故障AI compiledStep映射到子流、父流程和HMI；错计划/丢SourceMap不得猜测 | Core+Windows STA |
| T09 | EP11/12 | MRKRuntimeSession/MDS/N14/N15 | R4/R5；U3/U5 | 真实RunSession、Watch/Trace、订阅释放，旧帧不能更新新工程；停止/超时反馈 | MRK+WPF STA |
| T10 | EP13/14 | N04/N05/N17/N18；专业工作台 | R1/R2/R3；U4 | CT/ROI专业Viewer可注册，缺插件可诊断；巨大Volume不复制进MEM，接口隔离正常 | Windows+专业合同 |
| T11 | EP02/03/05 | FGE/MRC/Visual/SceneView | R1/R3/R6；A场景 | 九图元/Bezier控制柄、路径段修改、选中/撤销、DPI/资源错误占位和安全SVG | Geometry+WPF STA |
| T12 | EP01/15 | S1 Shell/N03/N15 | R3/R6；U1/U5 | AvalonDock5.0.1布局重启、反序列化拒绝DTD/超限、深浅VS主题、双屏DPI、循环开关无泄漏 | Windows真实STA+人工 |
| T13 | EP08/10/11/15 | MOC/MRK/MEMAccessPolicy/Professional.Automation | R4/R6；U3 | Design/Preview不发真实曝光/运动，Run/Debug需权限/联锁；现场操作日志可追溯 | 专业安全/实机，人工签收 |
| T14 | EP13/15 | Professional.Vision/CT3D/Inspection | R2/R5/R6；U3/U4 | 真实图像ROI、AI/CV检测、重建MPR、结果/高负载生命周期，不能用Demo冒充 | 专业实数据/压力 |
| T15 | EP01～15 | N01～N20+第一阶段305类型Owner | R1～R6；U1～U5 | API独立示例→Studio同一工程→Build→Runtime→Trace→定位→修改→重构建；全链路不建立第二真源 | Windows CI+端到端/多专业 |

**测试签收规则**：每项需关联具体Commit SHA、Windows Runner/VS版本、真实命令、日志路径、成功/失败与修复记录；设备测试记录另存设备型号、人员授权、联锁条件。已有测试源代码只算E2；若无本次真实日志状态维持待验。

## 42. 第三阶段成果与进入实现的门禁

| 项 | 交付内容 | 审核结论 |
|---|---|---|
| 阶段一 | 305现有工程呈现化相关类型、11项目归属、89个源码入口 | 完成当时的**源码声明快照**，不等于全仓所有类/验收 |
| 阶段二 | 38复用增强组、20拟新增职责、12禁建项、10专项待核；EP01～EP15映射 | 唯一Owner和去重裁决形成；近期S1变更已调整 |
| 阶段三 | 20候选的项目、状态、属性/方法、线程/生命周期/安全/失败边界；关键已有API真实入口；两角色使用案例；15条验收门禁 | **设计契约已整理**；真实代码尚未按新契约全部实现/编译/验收 |
| 类级别待实证问题 | 第一阶段其他类型的**完整公共方法签名与所有重载**、全部专业接入代码、Studio V1.2全文、跨项目调用DAG、质量/性能/实机安全 | 需要源码逐类型精确盘点及阶段实现时动态校验，不能把本轮签名概要冒称100%成员级权威合同 |
| 开发实施方式 | 原位增强优先；先A图元、B双实例、C主/AI/CV专业子流纵向跑通；分包按最少有意义提交 | 各包必须产生真实API + Studio UI + 工程保存/运行/调试 + Windows测试日志；否则只记部分实现 |

**阶段三的完成边界**：本轮完成目标**协同类型契约与需求/用例/Owner/测试全映射**；第一阶段305项的成员级全量自动盘点属于后续可追加核对工作，不将已抽样API表冒充全部305型的全部重载。确认后的具体代码实施由单一分轮台账继续驱动，不能因为本文件新增设计表就将R01～R04或S1标为Windows/现场验收通过。

---
## 43. 既有类型实际源码成员辅助索引（非编译器签名报告）

> 依据阶段一305型和实际源文件提取可见声明摘录；**静态提取可能遗漏重载、继承、partial和泛型成员**，以链接源码与真实编译为准；不将本表视作E3验收。

### 43.1 批次1（57型）

| 类型 | 形态 | 原Owner | 源码成员摘录（S1） | 来源 |
|---|---|---|---|---|
| `FGEPoint2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEPoint2D(double x, double y)`；`public double X`；`public double Y`；`public static FGEPoint2D operator +(FGEPoint2D p, FGEVector2D v)`；`public static FGEVector2D operator -(FGEPoint2D a, FGEPoint2D b)`；`public double DistanceTo(FGEPoint2D other)`；`public bool Equals(FGEPoint2D other)`；`public override bool Equals(object obj)`；`public override int GetHashCode()`；`public override string ToString()` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEVector2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEVector2D(double x, double y)`；`public double X`；`public double Y`；`public double Length`；`public double LengthSquared`；`public FGEVector2D Normalize()`；`public static double Dot(FGEVector2D a, FGEVector2D b)`；`public static double Cross(FGEVector2D a, FGEVector2D b)`；`public static FGEVector2D operator +(FGEVector2D a, FGEVector2D b)`；`public static FGEVector2D operator -(FGEVector2D a, FGEVector2D b)`；`public static FGEVector2D operator *(FGEVector2D a, double k)`；`public static FGEVector2D operator /(FGEVector2D a, double k)`；另有3项待展开 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGESize2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGESize2D(double width, double height)`；`public double Width` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGERect2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGERect2D(double x,double y,double width,double height)`；`public double X`；`public double Width`；`public double Left`；`public double Right`；`public bool IsDegenerate`；`public bool Contains(FGEPoint2D p)`；`public bool Intersects(FGERect2D other)`；`public FGERect2D Union(FGERect2D other)`；`public FGERect2D Intersection(FGERect2D other)`；`public FGERect2D Inflate(double thickness)`；`public bool Equals(FGERect2D o)`；另有3项待展开 | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGESegment2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGESegment2D(FGEPoint2D start,FGEPoint2D end)`；`public FGEPoint2D Start`；`public double Length`；`public FGERect2D Bounds`；`public FGEPoint2D PointAt(double t)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGECircle2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGECircle2D(FGEPoint2D center,double radius)`；`public FGEPoint2D Center`；`public FGERect2D Bounds`；`public bool Contains(FGEPoint2D p)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEEllipse2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEEllipse2D(FGEPoint2D center,double radiusX,double radiusY)`；`public FGEPoint2D Center`；`public FGERect2D Bounds`；`public bool Contains(FGEPoint2D p)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEPolyline2D` | 类 | `CxVisual.Foundation.Geometry` | `public FGEPolyline2D(IEnumerable<FGEPoint2D> points)`；`public IReadOnlyList<FGEPoint2D> Points`；`public double Length` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEPolygon2D` | 类 | `CxVisual.Foundation.Geometry` | `public FGEPolygon2D(IEnumerable<FGEPoint2D> points)`；`public IReadOnlyList<FGEPoint2D> Points`；`public double SignedArea`；`public bool Contains(FGEPoint2D p)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEArc2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEArc2D(FGEPoint2D center,double radiusX,double radiusY,double startRadians,double sweepRadians,double rotationRadians=0)`；`public FGEPoint2D Center`；`public double StartRadians`；`public FGEPoint2D PointAt(double t)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEBezier2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEBezier2D(FGEPoint2D start,FGEPoint2D control1,FGEPoint2D control2,FGEPoint2D end)`；`public FGEPoint2D Start`；`public FGEPoint2D PointAt(double t)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEValidation` | 类 | `CxVisual.Foundation.Geometry` | `public static double Finite(double value, string name)` | [源码](../../Foundation/Geometry/Primitives2D/FGEGeometry2D.cs) |
| `FGEPathVerb` | 枚举 | `CxVisual.Foundation.Geometry` | `Move`；`Line`；`Quadratic`；`Cubic`；`Arc`；`Close` | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs) |
| `FGEPathCommand` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGEPathVerb Verb`；`public FGEPoint2D End`；`public FGEPoint2D Control1`；`public FGEPoint2D Control2`；`public FGEArc2D Arc`；`public static FGEPathCommand Move(FGEPoint2D p)`；`public static FGEPathCommand Line(FGEPoint2D p)`；`public static FGEPathCommand Quadratic(FGEPoint2D c,FGEPoint2D end)`；`public static FGEPathCommand Cubic(FGEPoint2D c1,FGEPoint2D c2,FGEPoint2D end)`；`public static FGEPathCommand ArcTo(FGEArc2D arc)`；`public static FGEPathCommand Close()` | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs) |
| `FGEPath2D` | 类 | `CxVisual.Foundation.Geometry` | `public FGEPath2D(IEnumerable<FGEPathCommand> commands)`；`public IReadOnlyList<FGEPathCommand> Commands`；`public FGERect2D Bounds`；`public IReadOnlyList<IReadOnlyList<FGEPoint2D>> FlattenContours(int subdivisions=48)`；`public static FGEPath2D Line(FGEPoint2D a,FGEPoint2D b)`；`public static FGEPath2D Rectangle(double w,double h)`；`public static FGEPath2D Circle(FGEPoint2D center,double radius)`；`public static FGEPath2D Ellipse(FGEPoint2D center,double rx,double ry)`；`public static FGEPath2D Polyline(IEnumerable<FGEPoint2D> points,bool closed=false)`；`public static FGEPath2D Arc(FGEArc2D arc)`；`public static FGEPath2D Bezier(FGEBezier2D curve)`；`public IReadOnlyList<FGEPoint2D> Flatten(int subdivisions=32)` | [源码](../../Foundation/Geometry/Paths/FGEPath2D.cs) |
| `FGEPathHandleKind` | 枚举 | `CxVisual.Foundation.Geometry` | `Anchor`；`Control1`；`Control2` | [源码](../../Foundation/Geometry/Paths/FGEPathEditing.cs) |
| `FGEPathEditing` | 类 | `CxVisual.Foundation.Geometry` | `public static FGEPoint2D HandlePoint(FGEPath2D path,int commandIndex,FGEPathHandleKind kind)`；`public static FGEPath2D SplitSegment(FGEPath2D path,int commandIndex,double t)`；`public static FGEPath2D DeleteAnchor(FGEPath2D path,int commandIndex)`；`public static FGEPath2D ConvertSegment(FGEPath2D path,int index,FGEPathVerb to)`；`public static FGEPath2D SetContourClosed(FGEPath2D path,int commandIndex,bool closed)`；`public static FGEPath2D UpdateArc(FGEPath2D path,int arcCommandIndex,FGEArc2D arc)`；`public static FGEPath2D MoveHandle(FGEPath2D path,int commandIndex,FGEPathHandleKind kind,FGEPoint2D point)` | [源码](../../Foundation/Geometry/Paths/FGEPathEditing.cs) |
| `FGETransform2D` | 结构体 | `CxVisual.Foundation.Geometry` | `public FGETransform2D(double m11,double m12,double m21,double m22,double dx,double dy)`；`public double M11`；`public double M21`；`public double DX`；`public static FGETransform2D Identity`；`public static FGETransform2D Translation(double x,double y)`；`public static FGETransform2D Scale(double x,double y)`；`public static FGETransform2D Rotation(double radians)`；`public double Determinant`；`public FGEPoint2D TransformPoint(FGEPoint2D p)`；`public FGEPoint2D Transform(FGEPoint2D p)`；`public FGEVector2D TransformVector(FGEVector2D p)`；另有3项待展开 | [源码](../../Foundation/Geometry/Transforms/FGETransform2D.cs) |
| `FGEFillRule` | 枚举 | `CxVisual.Foundation.Geometry` | `EvenOdd`；`NonZero` | [源码](../../Foundation/Geometry/Operations/FGEGeometryAlgorithms.cs) |
| `FGEGeometry2D` | 类 | `CxVisual.Foundation.Geometry` | `public static FGEPoint2D ClosestPointOnSegment(FGEPoint2D p,FGESegment2D s)`；`public static double DistanceToSegment(FGEPoint2D p,FGESegment2D s)`；`public static bool PointOnSegment(FGEPoint2D p,FGESegment2D s,double tolerance=1e-9)`；`public static bool TryIntersectSegments(FGESegment2D a,FGESegment2D b,out FGEPoint2D intersection,double tolerance=1e-12)`；`public static bool PointInPolygon(FGEPoint2D p,IReadOnlyList<FGEPoint2D> points)`；`public static FGERect2D Bounds(IReadOnlyList<FGEPoint2D> points)`；`public static double DistanceToPath(FGEPoint2D p,FGEPath2D path,int subdivisions=48)`；`public static bool ContainsPath(FGEPoint2D p,FGEPath2D path,FGEFillRule fillRule=FGEFillRule.NonZero,int subdivisions=48)` | [源码](../../Foundation/Geometry/Operations/FGEGeometryAlgorithms.cs) |
| `MEMNodeKind` | 枚举 | `CxVisual.Mechanism.Core` | `Project`；`Page`；`Content`；`Symbol`；`Group`；`Element` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMResourceScope` | 枚举 | `CxVisual.Mechanism.Core` | `Standalone`；`Embedded` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `IMEMResource` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMNode` | 类 | `CxVisual.Mechanism.Core` | `internal const int MaximumDepth = 256`；`internal const int MaximumNodeCount = 2000000`；`internal static readonly IReadOnlyList<MEMNode> Empty = new MEMNode[0]`；`protected MEMNode(Guid id, string name, string typeId, MEMNodeKind kind)`；`public Guid Id`；`public string Name`；`public string TypeId`；`public MEMNodeKind Kind`；`public MEMNode Parent`；`public IReadOnlyList<MEMNode> Children`；`public MEMNode FirstChild`；`public MEMNode LastChild`；另有21项待展开 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMProject` | 类 | `CxVisual.Mechanism.Core` | `internal Dictionary<string,MPMTypeDescriptor> TypeItems`；`internal Dictionary<Guid,MEMAssetReference> AssetItems`；`internal MEMEngineeringSession Session`；`public MEMProject(Guid id, string name) : base(id, name, "core.project", MEMNodeKind.Project)`；`internal override IReadOnlyList<MEMNode> Owned`；`internal override bool Accepts(MEMNode child)`；`internal override void InsertCore(int index, MEMNode child)`；`internal override void RemoveCore(MEMNode child)`；`public IReadOnlyList<MEMPage> Pages`；`public IReadOnlyList<MEMContent> Contents`；`public IReadOnlyList<MEMSymbol> Symbols`；`public IReadOnlyList<IMEMResource> Resources`；另有4项待展开 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMPage` | 类 | `CxVisual.Mechanism.Core` | `public MEMPage(Guid id, string name) : this(id, name, true)`；`internal MEMPage(Guid id, string name, bool root) : base(id, name, "core.page", MEMNodeKind.Page)`；`public MEMContent RootContent`；`public Guid StorageRootId`；`public MEMResourceScope Scope`；`internal override IReadOnlyList<MEMNode> Owned`；`internal override bool Accepts(MEMNode child)`；`internal override void InsertCore(int index, MEMNode child)`；`internal override void RemoveCore(MEMNode child)` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMContent` | 类 | `CxVisual.Mechanism.Core` | `internal MPMDefinitionState DefinitionState`；`public IReadOnlyList<MPMProperty> Properties`；`public IReadOnlyList<MPCPort> Ports`；`public IReadOnlyList<MPCConnection> Connections`；`public IReadOnlyList<MRCPublicMapping> PublicMappings`；`public IReadOnlyList<MPMAspect> Aspects`；`internal List<MRIResourceReference> ReferenceItems`；`public MEMContent(Guid id, string name) : base(id, name, "core.content", MEMNodeKind.Content)`；`public Guid StorageRootId`；`public MEMResourceScope Scope`；`public IReadOnlyList<MRIResourceReference> References`；`public IReadOnlyList<MEMContent> Contents`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMSymbol` | 类 | `CxVisual.Mechanism.Core` | `internal List<MRIResourceReference> ReferenceItems`；`public IReadOnlyList<MRIResourceReference> References`；`internal MPMDefinitionState DefinitionState`；`public IReadOnlyList<MPMProperty> Properties`；`public IReadOnlyList<MPCPort> Ports`；`public IReadOnlyList<MPCConnection> Connections`；`public IReadOnlyList<MRCPublicMapping> PublicMappings`；`public IReadOnlyList<MPMAspect> Aspects`；`public MEMSymbol(Guid id, string name) : this(id, name, true)`；`internal MEMSymbol(Guid id, string name, bool root) : base(id, name, "core.symbol", MEMNodeKind.Symbol)`；`public MRCGroup RootGroup`；`public Guid StorageRootId`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MRIReferenceKind` | 枚举 | `CxVisual.Mechanism.Core` | `Content`；`Symbol` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMResourceEntry` | 类 | `CxVisual.Mechanism.Core` | `internal MEMResourceEntry(IMEMResource resource,string hash,int schemaVersion,IEnumerable<Guid> dependencies)`；`public Guid Id`；`public string Name`；`public string TypeId`；`public Guid StorageRootId`；`public MEMResourceScope Scope`；`public string ContentHash`；`public int SchemaVersion`；`public IReadOnlyList<Guid> Dependencies` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MRIResourceReference` | 类 | `CxVisual.Mechanism.Core` | `internal Dictionary<string,MPMBinding> OverrideItems`；`internal MRIPlacementState PlacementValue`；`internal MRIResourceReference(Guid id, string name, Guid targetId, MRIReferenceKind kind, MEMNode owner)`；`public Guid Id`；`public string Name`；`public Guid TargetId`；`public MRIReferenceKind Kind`；`public MEMNode Owner`；`public IReadOnlyDictionary<string,MPMBinding> Overrides`；`public MRCTransform Placement`；`internal void SetName(string name)`；`internal void SetTarget(Guid target)` | [源码](../../Mechanism/Core/Engineering/MEMNode.cs) |
| `MEMEngineering` | 类 | `CxVisual.Mechanism.Core` | `public static MEMEngineeringSession Create(string name,IMEMProjectStore store=null,IMEMAccessPolicy policy=null)`；`public static MEMEngineeringSession Open(string path,IMEMProjectStore store=null,IMEMAccessPolicy policy=null)`；`public static Task<MEMEngineeringSession> OpenAsync(string path, CancellationToken cancellation)` | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MEMEngineeringSession` | 类 | `CxVisual.Mechanism.Core` | `internal MEMEngineeringSession(MEMProject project, int revision)`；`public MEMProject Project`；`public int Revision`；`public string Location`；`public bool IsDirty`；`public bool IsClosed`；`public int Count`；`public bool CanUndo`；`public bool CanRedo`；`public Exception LastObserverError`；`internal void DemandOpen()`；`internal void DemandNode(MEMNode node)`；另有46项待展开 | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MTCEditResult` | 类 | `CxVisual.Mechanism.Core` | `internal MTCEditResult(bool success, T value, int revision, string error, string message)`；`public bool Success`；`public T Value`；`public int Revision`；`public string ErrorCode`；`public string Message`；`public static MTCEditResult<T> Rejected(int revision,string code,string message)`；`public T RequireValue()` | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MTCChangeSet` | 类 | `CxVisual.Mechanism.Core` | `internal MTCChangeSet(string intent, int before, int after, IEnumerable<MTCMutation> changes, bool undo = false)`；`public string Intent`；`public int BeforeRevision`；`public int AfterRevision`；`public IReadOnlyList<MTCChangeOperation> Operations` | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MVEValidationReport` | 类 | `CxVisual.Mechanism.Core` | `internal MVEValidationReport(IList<string> issues)`；`public IReadOnlyList<string> Issues`；`public bool IsValid` | [源码](../../Mechanism/Core/Engineering/MEMEngineeringSession.cs) |
| `MTCEditSession` | 类 | `CxVisual.Mechanism.Core` | `internal readonly List<MTCMutation> Changes = new List<MTCMutation>()`；`internal MTCEditSession(MEMEngineeringSession session)`；`internal void Complete()`；`public MEMNode Add(MEMNode parent, MEMNode node)`；`public MEMPage AddPage(string name)`；`public MEMContent AddContent(MEMNode parent, string name)`；`public MEMSymbol AddSymbol(string name)`；`public MRCGroup AddGroup(MEMNode parent, string name)`；`public MRCRectangle AddRectangle(MEMNode parent, string name, double x, double y, double width = 120, double height = 80)`；`public void Rename(MEMNode node, string name)`；`public void Reparent(MEMNode node, MEMNode parent, int index = -1)`；`public void Remove(MEMNode node)`；另有9项待展开 | [源码](../../Mechanism/Core/Engineering/MTCEditSession.cs) |
| `MTCChangeKind` | 枚举 | `CxVisual.Mechanism.Core` | `Add`；`Remove`；`Rename`；`Reparent`；`Geometry`；`Binding`；`Visibility`；`Reference`；`RemoveReference`；`Property`；`Port`；`Connection`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs) |
| `MTCChangeOperation` | 结构体 | `CxVisual.Mechanism.Core` | `internal MTCChangeOperation(MTCChangeKind kind,Guid target)`；`public MTCChangeKind Kind`；`public Guid TargetId`；`internal MTCChangeOperation Reversed()` | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs) |
| `MTCEditState` | 枚举 | `CxVisual.Mechanism.Core` | `Open`；`Committing`；`Committed`；`Cancelled`；`Rejected` | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs) |
| `MTCEditDraft` | 类 | `CxVisual.Mechanism.Core` | `internal MTCEditDraft(MEMEngineeringSession session,string intent,int revision)`；`public string Intent`；`public int BaseRevision`；`public MTCEditState State`；`public MEMNode Add(MEMNode parent,MEMNode node)`；`public MEMPage AddPage(string name)`；`public MEMContent AddContent(MEMNode parent,string name)`；`public MEMSymbol AddSymbol(string name)`；`public MRCGroup AddGroup(MEMNode parent,string name)`；`public MRCRectangle AddRectangle(MEMNode parent,string name,double x,double y,double width=120,double height=80)`；`public void Rename(MEMNode node,string name)`；`public void Rename(MRIResourceReference reference,string name)`；另有32项待展开 | [源码](../../Mechanism/Core/Engineering/MTCEditDraft.cs) |
| `IMEMProjectStore` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs) |
| `MEMSaveSnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MEMSaveSnapshot(Guid projectId,int revision,byte[] xml,System.Collections.Generic.IReadOnlyList<MEMAssetReference> assets)`；`public Guid ProjectId`；`public System.Collections.Generic.IReadOnlyList<MEMAssetReference> Assets`；`public Stream OpenRead()` | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs) |
| `MEMSaveResult` | 类 | `CxVisual.Mechanism.Core` | `public MEMSaveResult(Guid projectId,int savedRevision,string location,string storeToken)`；`public Guid ProjectId` | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs) |
| `MEMStoreConflictException` | 类 | `CxVisual.Mechanism.Core` | `public MEMStoreConflictException(string path):base("Engineering store changed or target already exists: "+path)` | [源码](../../Mechanism/Core/Engineering/MEMProjectStorage.cs) |
| `MEMFileProjectStore` | 类 | `CxVisual.Mechanism.Core` | `public static readonly MEMFileProjectStore Default=new MEMFileProjectStore()`；`public void WritePackage(Stream output,MEMSaveSnapshot snapshot,CancellationToken cancellation)`；`public MEMEngineeringSession ReadPackage(Stream input)`；`internal static MEMSaveSnapshot Capture(MEMEngineeringSession document,CancellationToken token)`；`internal static string ResourceHash(MEMNode root)`；`public MEMSaveResult Save(MEMSaveSnapshot snapshot, string path,string expectedToken,CancellationToken token)`；`public MEMEngineeringSession Load(string path)` | [源码](../../Mechanism/Core/Engineering/MEMFileProjectStore.cs) |
| `MEMProjectSnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MEMProjectSnapshot(MEMProject project,int revision,IEnumerable<MEMNode> selection=null)`；`public Guid ProjectId`；`public IReadOnlyList<MPMTypeDescriptor> RegisteredTypes`；`public IReadOnlyList<MEMAssetReference> Assets`；`public int Revision`；`public IReadOnlyList<MEMNodeSnapshot> Nodes`；`public MEMNodeSnapshot Find(Guid id)` | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs) |
| `MEMNodeSnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MEMNodeSnapshot(MEMNode node)`；`public Guid Id`；`public string Name`；`public string TypeId`；`public MEMNodeKind Kind`；`public Guid? ParentId`；`public Guid StorageRootId`；`public MEMResourceScope? Scope`；`public IReadOnlyList<Guid> ChildIds`；`public MRCVisualSnapshot Visual`；`public IReadOnlyList<MRIReferenceSnapshot> References`；`public IReadOnlyList<MPMPropertySnapshot> Properties`；另有4项待展开 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs) |
| `MPMPropertySnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MPMPropertySnapshot(MPMProperty property)`；`public MPMPropertyDescriptor Descriptor`；`public MPMBinding Binding`；`public bool HasOverride` | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs) |
| `MRCVisualSnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MRCVisualSnapshot(MRCVisualItem item)`；`public double X`；`public double Y`；`public double Width`；`public double Height`；`public double Rotation`；`public double ScaleX`；`public double ScaleY`；`public double Shear`；`public string LayerId`；`public bool IsLocked`；`public bool IsVisible`；另有2项待展开 | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs) |
| `MRIReferenceSnapshot` | 类 | `CxVisual.Mechanism.Core` | `internal MRIReferenceSnapshot(MRIResourceReference reference)`；`public Guid Id`；`public Guid TargetId`；`public Guid OwnerId`；`public string Name`；`public MRIReferenceKind Kind`；`public MRCTransform Placement`；`public IReadOnlyDictionary<string,MPMBinding> Overrides` | [源码](../../Mechanism/Core/Engineering/MEMProjectSnapshot.cs) |
| `MRIObjectAddress` | 类 | `CxVisual.Mechanism.Core` | `public MRIObjectAddress(Guid projectId,Guid resourceId,Guid objectId,IEnumerable<Guid> instancePath=null)`；`public Guid ProjectId`；`public Guid ResourceId`；`public Guid ObjectId`；`public IReadOnlyList<Guid> InstancePath`；`public override string ToString()`；`public static MRIObjectAddress Parse(string text)`；`public static bool TryParse(string text,out MRIObjectAddress address)`；`public bool Equals(MRIObjectAddress other)`；`public override bool Equals(object value)`；`public override int GetHashCode()` | [源码](../../Mechanism/Core/Engineering/MRIObjectAddress.cs) |
| `MRIObjectHandle` | 类 | `CxVisual.Mechanism.Core` | `internal MRIObjectHandle(Guid session,Guid project,Guid objectId)`；`internal Guid SessionId`；`public Guid ProjectId`；`public Guid ObjectId` | [源码](../../Mechanism/Core/Engineering/MRIObjectAddress.cs) |
| `MEMAccessOperation` | 枚举 | `CxVisual.Mechanism.Core` | `Open`；`Read`；`Edit`；`Save`；`Export`；`PrepareRun` | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs) |
| `IMEMAccessPolicy` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs) |
| `MEMAccessRequest` | 类 | `CxVisual.Mechanism.Core` | `internal MEMAccessRequest(MEMAccessOperation operation,Guid projectId,string location)`；`public MEMAccessOperation Operation` | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs) |
| `MEMCloseMode` | 枚举 | `CxVisual.Mechanism.Core` | `RequireSaved`；`Discard`；`Save` | [源码](../../Mechanism/Core/Engineering/MEMAccessPolicy.cs) |

### 43.2 批次2（75型）

| 类型 | 形态 | 原Owner | 源码成员摘录（S1） | 来源 |
|---|---|---|---|---|
| `MEMAssetReference` | 类 | `CxVisual.Mechanism.Core` | `internal const int MaximumBytes=64*1024*1024`；`internal MEMAssetReference(Guid id,string name,string mediaType,byte[] bytes)`；`internal static string Hash(byte[] bytes)`；`internal static byte[] ReadBytes(Stream stream)`；`internal string ArchivePath`；`internal Stream OpenRead()`；`internal MEMAssetReference WithId(Guid id)`；`public Guid Id`；`public int Length`；`public Guid StorageRootId` | [源码](../../Mechanism/Core/Engineering/MEMAssetReference.cs) |
| `MTIExternalPolicy` | 枚举 | `CxVisual.Mechanism.Core` | `Reject`；`Pin`；`IncludeClosure` | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs) |
| `IMTIAspectRemapper` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs) |
| `MTITransferResult` | 类 | `CxVisual.Mechanism.Core` | `internal MTITransferResult(MTITransferPlan plan,int revision)`；`public IReadOnlyDictionary<Guid,Guid> IdMap` | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs) |
| `MTITransferPlan` | 类 | `CxVisual.Mechanism.Core` | `internal MEMProjectSnapshot Source`；`internal MEMEngineeringSession SourceSession`；`internal Guid TargetSessionId,ParentId,TargetProjectId`；`internal IReadOnlyList<Guid> SelectedRoots,DependencyRoots`；`internal HashSet<Guid> Included=new HashSet<Guid>()`；`internal HashSet<Guid> CopiedAssets=new HashSet<Guid>()`；`internal Dictionary<Guid,Guid> NewResourceRoots=new Dictionary<Guid,Guid>()`；`internal readonly Dictionary<MPMAspect,MPMAspect> MappedAspects=new Dictionary<MPMAspect,MPMAspect>()`；`internal MTITransferPlan(MEMProjectSnapshot source,MEMEngineeringSession sourceSession,Dictionary<Guid,MEMResourceEntry> resources, MEMEngineeringSession target,MEMNode parent,IEnumerable<Gu`；`internal void Reject(IEnumerable<string> issues)`；`public int BaseRevision`；`public IReadOnlyDictionary<Guid,Guid> IdMap`；另有9项待展开 | [源码](../../Mechanism/Core/Engineering/MTITransferPlan.cs) |
| `MTIExtractionResult` | 类 | `CxVisual.Mechanism.Core` | `internal MTIExtractionResult(Guid definition,Guid reference,MTITransferResult transfer)`；`public Guid DefinitionId` | [源码](../../Mechanism/Core/Engineering/MTIPackages.cs) |
| `MTIEngineeringTemplates` | 类 | `CxVisual.Mechanism.Core` | `public static MEMEngineeringSession Create(string packagePath,string name,IMTIAspectRemapper remapper=null)` | [源码](../../Mechanism/Core/Engineering/MTIPackages.cs) |
| `MPMValueKind` | 枚举 | `CxVisual.Mechanism.Core` | `Null`；`String`；`Number`；`Integer`；`Boolean`；`Decimal`；`Resource` | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMValue` | 类 | `CxVisual.Mechanism.Core` | `public MPMValueKind Kind`；`public string Canonical`；`public static MPMValue Null`；`public static MPMValue String(string value)`；`public static MPMValue Number(double value)`；`public static MPMValue Integer(long value)`；`public static MPMValue Decimal(decimal value)`；`public static MPMValue Boolean(bool value)`；`public double AsNumber()`；`public long AsInteger()`；`public decimal AsDecimal()`；`public string AsString()`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMBindingKind` | 枚举 | `CxVisual.Mechanism.Core` | `Legacy`；`Literal`；`Configuration`；`Variable`；`Resource`；`Member`；`Expression` | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMBinding` | 类 | `CxVisual.Mechanism.Core` | `public MPMBinding(string sourceId,string memberId):this(MPMBindingKind.Legacy,Required(sourceId),Required(memberId))`；`public MPMBindingKind Kind`；`public string SourceId`；`public string MemberId`；`public MPMValue Value`；`public MRIObjectAddress Address`；`public Guid? ResourceId`；`public FCMConfigReference Configuration`；`public static MPMBinding Literal(MPMValue value)`；`public static MPMBinding Variable(string id,string member="value")`；`public static MPMBinding Resource(Guid id)`；`public static MPMBinding Member(MRIObjectAddress address,string member)`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMPropertyDescriptor` | 类 | `CxVisual.Mechanism.Core` | `public MPMPropertyDescriptor(string memberId,MPMValueKind valueKind,bool required=false,bool isPublic=true,MPMBinding defaultBinding=null,double? minimum=null,double? maximum=null,bool isRea`；`public string MemberId`；`public MPMValueKind ValueKind`；`public bool Required`；`public bool IsPublic`；`public bool IsReadOnly`；`public MPMBinding DefaultBinding`；`public double? Minimum`；`public double? Maximum`；`internal void Validate(MPMBinding binding)` | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `MPMProperty` | 类 | `CxVisual.Mechanism.Core` | `internal MPMProperty(Guid ownerId,MPMPropertyDescriptor descriptor)`；`public Guid OwnerId`；`public MPMPropertyDescriptor Descriptor`；`public string MemberId`；`public MPMBinding Binding`；`public bool HasOverride`；`internal MPMBinding ExplicitBinding` | [源码](../../Mechanism/Core/Engineering/MPMValue.cs) |
| `IMPMTypeProvider` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs) |
| `MPCPortDescriptor` | 类 | `CxVisual.Mechanism.Core` | `public MPCPortDescriptor(string memberId,MPCPortDirection direction,string typeId,MPCPortKind kind=MPCPortKind.Data, MPCPortMultiplicity multiplicity=MPCPortMultiplicity.Many,MTUContractRefe`；`public string MemberId`；`public MPCPortKind Kind`；`public MTUContractReference Contract` | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs) |
| `MPMTypeDescriptor` | 类 | `CxVisual.Mechanism.Core` | `public MPMTypeDescriptor(string typeId,int schemaVersion,MEMNodeKind kind,IEnumerable<MPMPropertyDescriptor> properties=null, IEnumerable<MPCPortDescriptor> ports=null,IEnumerable<MPMAspect>`；`public string TypeId`；`public IReadOnlyList<MPMPropertyDescriptor> Properties`；`internal static bool SameSchema(MPMTypeDescriptor a,MPMTypeDescriptor b)` | [源码](../../Mechanism/Core/Engineering/MPMTypeDescriptor.cs) |
| `IMPMDefinition` | 接口 | `CxVisual.Mechanism.Core` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs) |
| `MPMAspectKind` | 枚举 | `CxVisual.Mechanism.Core` | `Visual`；`Logic`；`Data`；`Collaboration` | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs) |
| `MPMAspect` | 类 | `CxVisual.Mechanism.Core` | `public MPMAspect(MPMAspectKind kind,string providerId,int schemaVersion,byte[] payload,bool required=true,bool requiresRemap=false)`；`public MPMAspectKind Kind`；`public int SchemaVersion`；`public bool RequiresRemap`；`public byte[] Payload`；`internal byte[] PayloadBytes` | [源码](../../Mechanism/Core/Engineering/MPMDefinitionState.cs) |
| `MPCPortDirection` | 枚举 | `CxVisual.Mechanism.Core` | `Input`；`Output`；`InOut` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MPCPortKind` | 枚举 | `CxVisual.Mechanism.Core` | `Data`；`Control`；`Event`；`Command`；`State`；`Stream`；`Resource`；`Visual` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MPCPortMultiplicity` | 枚举 | `CxVisual.Mechanism.Core` | `One`；`Many` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MTUContractReference` | 类 | `CxVisual.Mechanism.Core` | `public MTUContractReference(string unitId="",string coordinateFrameId="",string timeBaseId="",MRIObjectAddress calibration=null)`；`public string UnitId`；`public string CoordinateFrameId`；`public string TimeBaseId`；`public MRIObjectAddress Calibration`；`public bool Equals(MTUContractReference other)`；`public override bool Equals(object value)`；`public override int GetHashCode()` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MPCPort` | 类 | `CxVisual.Mechanism.Core` | `internal MPCPort(Guid id,Guid ownerId,string memberId,MPCPortDirection direction,string typeId,MPCPortKind kind,MPCPortMultiplicity multiplicity,MTUContractReference contract,bool isPublic,b`；`public Guid Id`；`public string MemberId`；`public MPCPortDirection Direction`；`public MPCPortMultiplicity Multiplicity`；`public bool IsPublic` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MPCEndpoint` | 类 | `CxVisual.Mechanism.Core` | `public MPCEndpoint(MRIObjectAddress address,Guid portId)`；`public MRIObjectAddress Address`；`public bool Equals(MPCEndpoint other)`；`public override bool Equals(object value)`；`public override int GetHashCode()` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MPCConnection` | 类 | `CxVisual.Mechanism.Core` | `internal MPCConnection(Guid id,Guid ownerId,MPCEndpoint source,MPCEndpoint target)`；`public Guid Id`；`public MPCEndpoint Source` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MRCPublicMappingKind` | 枚举 | `CxVisual.Mechanism.Core` | `Property`；`Port` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MRCPublicMapping` | 类 | `CxVisual.Mechanism.Core` | `internal MRCPublicMapping(Guid owner,string member,MRIObjectAddress target,string targetMember)`；`internal MRCPublicMapping(Guid owner,string member,Guid publicPort,MPCEndpoint target)`；`public Guid OwnerId`；`public MRCPublicMappingKind Kind`；`public MRIObjectAddress TargetAddress`；`public Guid? PublicPortId` | [源码](../../Mechanism/Core/Engineering/MPCPort.cs) |
| `MOLControlKind` | 枚举 | `CxVisual.Mechanism.Core` | `Action`；`Sequence`；`Branch`；`Loop`；`ForEach`；`Parallel`；`Invoke`；`Wait`；`Try`；`Exit` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLBranchMode` | 枚举 | `CxVisual.Mechanism.Core` | `If`；`Switch` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLLoopMode` | 枚举 | `CxVisual.Mechanism.Core` | `For`；`While`；`DoWhile` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLExitKind` | 枚举 | `CxVisual.Mechanism.Core` | `Break`；`Continue`；`Return` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLExpressionSource` | 枚举 | `CxVisual.Mechanism.Core` | `Constant`；`FlowInput`；`StepResult`；`Iteration` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLComparison` | 枚举 | `CxVisual.Mechanism.Core` | `Truthy`；`Equal`；`NotEqual`；`LessThan`；`LessOrEqual`；`GreaterThan`；`GreaterOrEqual` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLExpression` | 类 | `CxVisual.Mechanism.Core` | `public MOLExpression(MOLExpressionSource source, string name = null, MOLComparison comparison = MOLComparison.Truthy, string expected = null)`；`public MOLExpressionSource Source`；`public string Name`；`public MOLComparison Comparison`；`public string Expected`；`public static MOLExpression InputEquals(string input, string value)`；`public static MOLExpression InputTrue(string input)`；`public static MOLExpression Always(bool value)` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLBranchCase` | 类 | `CxVisual.Mechanism.Core` | `public MOLBranchCase(MOLExpression condition, MOLControlBlock body, string label = null)`；`public MOLExpression Condition`；`public MOLControlBlock Body`；`public string Label` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLLoopDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MOLLoopDefinition(MOLLoopMode mode, int maxIterations, MOLControlBlock body, MOLExpression condition = null, string collectionInput = null)`；`public MOLLoopMode Mode`；`public int MaxIterations`；`public MOLControlBlock Body`；`public MOLExpression Condition`；`public string CollectionInput` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLControlBlock` | 类 | `CxVisual.Mechanism.Core` | `public MOLControlBlock(Guid id, MOLControlKind kind, string capabilityId = null, IEnumerable<MOLControlBlock> children = null, IEnumerable<MOLBranchCase> cases = null, MOLLoopDefinition loop`；`public Guid Id`；`public MOLControlKind Kind`；`public string CapabilityId`；`public string TargetFlow`；`public MOLExpression Condition`；`public MOLLoopDefinition Loop`；`public MOLExitKind ExitKind`；`public IReadOnlyList<MOLControlBlock> Children`；`public IReadOnlyList<MOLBranchCase> Cases`；`public static MOLControlBlock Action(string capability)`；`public static MOLControlBlock Sequence(params MOLControlBlock[] children)`；另有5项待展开 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLLogicDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MOLLogicDefinition(MOLControlBlock root)`；`public MOLControlBlock Root`；`public void Validate()` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLLogicCodec` | 类 | `CxVisual.Mechanism.Core` | `public const string ProviderId = "cx.logic.structured"`；`public const int SchemaVersion = 1`；`public static void Set(MEMEngineeringSession session, MEMContent content, MOLLogicDefinition definition)`；`public static MOLLogicDefinition Get(MEMNodeSnapshot content)`；`public static byte[] Serialize(MOLLogicDefinition definition)`；`public static MOLLogicDefinition Deserialize(byte[] payload)` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLBlockEndpoint` | 类 | `CxVisual.Mechanism.Core` | `public MOLBlockEndpoint(Guid blockId, string portMemberId)`；`public Guid BlockId`；`public string PortMemberId`；`public bool Equals(MOLBlockEndpoint other)`；`public override bool Equals(object obj)`；`public override int GetHashCode()` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLLink` | 类 | `CxVisual.Mechanism.Core` | `public MOLLink(Guid id,MOLBlockEndpoint source,MOLBlockEndpoint target, MPCPortKind kind,string conditionId=null,int? order=null)`；`public Guid Id`；`public MOLBlockEndpoint Source`；`public MOLBlockEndpoint Target`；`public MPCPortKind Kind`；`public string ConditionId`；`public int? Order` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLBlockPosition` | 类 | `CxVisual.Mechanism.Core` | `public MOLBlockPosition(double x,double y)`；`public double X`；`public double Y` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLSelectionGroup` | 类 | `CxVisual.Mechanism.Core` | `public MOLSelectionGroup(Guid id,string name,IEnumerable<Guid> members)`；`public Guid Id`；`public string Name`；`public IReadOnlyList<Guid> BlockIds` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLStateDescriptor` | 类 | `CxVisual.Mechanism.Core` | `public MOLStateDescriptor(string id,string region,string parent=null,bool initial=false)`；`internal static string Check(string input)`；`public string Id`；`public string Region`；`public string ParentId`；`public bool Initial` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLStateTransition` | 类 | `CxVisual.Mechanism.Core` | `public MOLStateTransition(string source,string eventId,string target)`；`public string Source`；`public string EventId`；`public string Target` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLStateMachineDefinition` | 类 | `CxVisual.Mechanism.Core` | `public IReadOnlyList<MOLStateDescriptor> States`；`public IReadOnlyList<MOLStateTransition> Transitions`；`public MOLStateMachineDefinition(IEnumerable<MOLStateDescriptor> states, IEnumerable<MOLStateTransition> transitions)`；`public IReadOnlyDictionary<string,string> Run(IEnumerable<string> events, System.Threading.CancellationToken cancellation= default(System.Threading.CancellationToken))` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLStepPolicy` | 类 | `CxVisual.Mechanism.Core` | `public MOLStepPolicy(int maxAttempts=1,int retryDelayMs=0,int timeoutMs=0, bool idempotent=false)`；`public int MaxAttempts`；`public int RetryDelayMs`；`public int TimeoutMs`；`public bool Idempotent` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLWiringDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MOLWiringDefinition(Guid entryBlockId,IEnumerable<MOLControlBlock> blocks, IDictionary<Guid,IEnumerable<MPCPortDescriptor>> ports,IEnumerable<MOLLink> links, IDictionary<Guid,MOLBlock`；`public Guid EntryBlockId`；`public IReadOnlyList<MOLControlBlock> Blocks`；`public IReadOnlyList<MOLLink> Links`；`public IReadOnlyDictionary<Guid,IReadOnlyList<MPCPortDescriptor>> Ports`；`public IReadOnlyDictionary<Guid,MOLBlockPosition> Positions`；`public IReadOnlyList<MOLSelectionGroup> Groups`；`public IReadOnlyDictionary<Guid,IReadOnlyDictionary<string,string>> Parameters`；`public IReadOnlyDictionary<Guid,MOLStepPolicy> Policies`；`public IReadOnlyDictionary<Guid,MOLStateMachineDefinition> StateMachines`；`public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowInputs`；`public IReadOnlyDictionary<string,MOLBlockEndpoint> FlowOutputs`；另有2项待展开 | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MOLWiringCodec` | 类 | `CxVisual.Mechanism.Core` | `public const int SchemaVersion=2`；`public static MPMAspect CreateAspect(MOLWiringDefinition definition)`；`public static void Set(MEMEngineeringSession session,MEMContent content,MOLWiringDefinition definition)`；`public static MOLWiringDefinition Get(MEMNodeSnapshot node)`；`public static byte[] Serialize(MOLWiringDefinition def)`；`public static MOLWiringDefinition Deserialize(byte[] bytes)` | [源码](../../Mechanism/Core/Engineering/MOLLogicDefinition.cs) |
| `MRCVisualItem` | 类 | `CxVisual.Mechanism.Core` | `internal MPMDefinitionState DefinitionState`；`public IReadOnlyList<MPMProperty> Properties`；`public IReadOnlyList<MPCPort> Ports`；`public IReadOnlyList<MPCConnection> Connections`；`public IReadOnlyList<MRCPublicMapping> PublicMappings`；`public IReadOnlyList<MPMAspect> Aspects`；`internal Dictionary<string, MPMBinding> BindingItems`；`protected MRCVisualItem(Guid id, string name, string typeId, MEMNodeKind kind, double x, double y, double width, double height) : base(id, name, typeId, kind)`；`public double X`；`public double Y`；`public double Width`；`public double Height`；另有16项待展开 | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs) |
| `MRCElement` | 类 | `CxVisual.Mechanism.Core` | `protected MRCElement(Guid id, string name, string typeId, double x, double y, double width, double height) : base(id, name, typeId, MEMNodeKind.Element, x, y, width, height)` | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs) |
| `MRCRectangle` | 类 | `CxVisual.Mechanism.Core` | `public MRCRectangle(Guid id, string name, double x, double y, double width = 120, double height = 80) : base(id, name, "core.rectangle", x, y, width, height)` | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs) |
| `MRCGroup` | 类 | `CxVisual.Mechanism.Core` | `public MRCGroup(Guid id, string name) : base(id, name, "core.group", MEMNodeKind.Group, 0, 0, 120, 80)`；`public new IReadOnlyList<MRCVisualItem> Children`；`internal override IReadOnlyList<MEMNode> Owned`；`internal override bool Accepts(MEMNode child)`；`internal override void InsertCore(int index, MEMNode child)`；`internal override void RemoveCore(MEMNode child)` | [源码](../../Mechanism/Core/Engineering/MRCVisualItem.cs) |
| `MRCShapeKind` | 枚举 | `CxVisual.Mechanism.Core` | `Line`；`Rect`；`Circle`；`Ellipse`；`Polyline`；`Polygon`；`Arc`；`Bezier`；`Path` | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs) |
| `MRCShapeDefinition` | 类 | `CxVisual.Mechanism.Core` | `public const string ProviderId = "core.visual.shape"`；`public MRCShapeDefinition(MRCShapeKind kind,FGEPath2D geometry)`；`public MRCShapeKind Kind`；`public FGEPath2D Geometry`；`public FGERect2D Bounds`；`public static MRCShapeDefinition Line(FGEPoint2D a,FGEPoint2D b)`；`public static MRCShapeDefinition Rect(double w,double h)`；`public static MRCShapeDefinition Circle(FGEPoint2D center,double r)`；`public static MRCShapeDefinition Ellipse(FGEPoint2D center,double rx,double ry)`；`public static MRCShapeDefinition Polyline(IEnumerable<FGEPoint2D> points)`；`public static MRCShapeDefinition Polygon(IEnumerable<FGEPoint2D> points)`；`public static MRCShapeDefinition Arc(FGEArc2D arc)`；另有6项待展开 | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs) |
| `MRCShapeElement` | 类 | `CxVisual.Mechanism.Core` | `public MRCShapeElement(Guid id,string name,double x,double y,double width,double height) :base(id,name,"core.shape",x,y,width,height)`；`public MRCShapeDefinition Shape`；`public FGETransform2D GeometryToLocalTransform`；`public override bool HitTest(double worldX,double worldY)`；`internal static MRCShapeElement Create(Guid id,string name,MRCShapeDefinition shape,double x,double y)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCShapes.cs) |
| `MRCTextElement` | 类 | `CxVisual.Mechanism.Core` | `internal const string TextProvider="core.visual.text"`；`public MRCTextElement(Guid id,string name,double x,double y,double width=150,double height=30) :base(id,name,"core.text",x,y,width,height)`；`public string Text`；`public static string TextFromAspects(System.Collections.Generic.IEnumerable<MPMAspect> aspects)`；`internal static MPMAspect ToAspect(string text)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs) |
| `MRCImageElement` | 类 | `CxVisual.Mechanism.Core` | `public MRCImageElement(Guid id,string name,double x,double y,double width=120,double height=80) :base(id,name,"core.image",x,y,width,height)`；`public MPMBinding Source` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs) |
| `MRCSvgElement` | 类 | `CxVisual.Mechanism.Core` | `public MRCSvgElement(Guid id,string name,double x,double y,double width=120,double height=80) :base(id,name,"core.svg",x,y,width,height)`；`public MPMBinding Source` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs) |
| `MRCWidgetTypes` | 类 | `CxVisual.Mechanism.Core` | `public static bool IsBuiltIn(string id)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs) |
| `MRCWidgetElement` | 类 | `CxVisual.Mechanism.Core` | `public MRCWidgetElement(Guid id,string name,string typeId,double x,double y,double width=120,double height=36) :base(id,name,"core.widget",x,y,width,height)`；`public string WidgetTypeId` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualElements.cs) |
| `MRCColor` | 结构体 | `CxVisual.Mechanism.Core` | `public MRCColor(byte a,byte r,byte g,byte b)`；`public byte A`；`public static MRCColor FromRgb(byte r,byte g,byte b)`；`public static MRCColor Parse(string hex)`；`public override string ToString()`；`public bool Equals(MRCColor o)`；`public override bool Equals(object o)`；`public override int GetHashCode()` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCBrushKind` | 枚举 | `CxVisual.Mechanism.Core` | `None`；`Solid`；`LinearGradient`；`Resource` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCGradientStop` | 类 | `CxVisual.Mechanism.Core` | `public MRCGradientStop(double offset,MRCColor color)`；`public double Offset` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCBrushDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MRCBrushKind Kind`；`public IReadOnlyList<MRCGradientStop> Stops`；`public static MRCBrushDefinition None()`；`public static MRCBrushDefinition Solid(MRCColor c)`；`public static MRCBrushDefinition Resource(string id)`；`public static MRCBrushDefinition LinearGradient(IEnumerable<MRCGradientStop> stops)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCVisualStyle` | 类 | `CxVisual.Mechanism.Core` | `public MRCVisualStyle(MRCBrushDefinition fill,MRCBrushDefinition stroke,double strokeWidth=1,double opacity=1, string fontFamily="Segoe UI",double fontSize=12,string themeKey="")`；`public MRCBrushDefinition Fill`；`public double StrokeWidth`；`public string FontFamily`；`public static MRCVisualStyle Default`；`public static MRCVisualStyle Solid(MRCColor color)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCVisualStateDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MRCVisualStateDefinition(string name,MRCVisualStyle style)`；`public string Name`；`internal static string Required(string s)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCAnimationEasing` | 枚举 | `CxVisual.Mechanism.Core` | `Linear`；`EaseIn`；`EaseOut`；`EaseInOut` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCAnimationDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MRCAnimationDefinition(string property,double from,double to,int durationMilliseconds,string triggerId="", MRCAnimationEasing easing=MRCAnimationEasing.Linear,bool repeat=false)`；`public string Property`；`public int DurationMilliseconds`；`public MRCAnimationEasing Easing` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCVisualEventAction` | 类 | `CxVisual.Mechanism.Core` | `public MRCVisualEventAction(string eventId,string actionId,string parameterBindingId="")`；`public string EventId`；`public string ParameterBindingId` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCVisualBehaviorDefinition` | 类 | `CxVisual.Mechanism.Core` | `public MRCVisualBehaviorDefinition(IEnumerable<MRCVisualStateDefinition> states=null, IEnumerable<MRCAnimationDefinition> animations=null,IEnumerable<MRCVisualEventAction> eventActions=null)`；`public IReadOnlyList<MRCVisualStateDefinition> States`；`public IReadOnlyList<MRCAnimationDefinition> Animations`；`public IReadOnlyList<MRCVisualEventAction> EventActions` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCVisualAppearance` | 类 | `CxVisual.Mechanism.Core` | `public const string StyleProvider="core.visual.style"`；`public const string BehaviorProvider="core.visual.behavior"`；`public static MRCVisualStyle StyleFromAspects(IEnumerable<MPMAspect> aspects)`；`public static MRCVisualStyle StyleOf(MRCVisualItem item)`；`public static MRCVisualBehaviorDefinition BehaviorFromAspects(IEnumerable<MPMAspect> aspects)`；`public static MRCVisualBehaviorDefinition BehaviorOf(MRCVisualItem item)`；`public static MPMAspect StyleAspect(MRCVisualStyle style)`；`public static MPMAspect BehaviorAspect(MRCVisualBehaviorDefinition behavior)` | [源码](../../Mechanism/Core/VisualDefinitions/MRCVisualAppearance.cs) |
| `MRCTransform` | 结构体 | `CxVisual.Mechanism.Core` | `public MRCTransform(double m11,double m12,double m21,double m22,double x,double y)`；`public double M11`；`public double M21`；`public double X`；`public static MRCTransform Identity`；`public FGETransform2D ToGeometry()`；`public static MRCTransform FromGeometry(FGETransform2D value)`；`public void TransformPoint(double x,double y,out double tx,out double ty)`；`public MRCTransform Compose(MRCTransform child)`；`public MRCTransform Inverse()`；`internal void Decompose(out double angle,out double sx,out double sy,out double shear)` | [源码](../../Mechanism/Core/Engineering/MRCTransform.cs) |
| `CxSceneItem` | 类 | `CxVisual.Mechanism.Visual` | `public CxSceneItem(MRCElement source)`；`internal CxSceneItem(Guid selectionId,string name,MRIObjectAddress address, MRCTransform world,double width,double height,bool selectable, MRCTransform moveParent,double moveX,double moveY, `；`internal static MRCTransform Inverse(MRCTransform transform)`；`public Guid Id`；`public string Name`；`public MRIObjectAddress Address`；`public bool IsSelectable`；`public double LocalWidth`；`public double LocalHeight`；`public string ElementTypeId`；`public MRCShapeDefinition Shape`；`public MRCVisualStyle Style`；另有14项待展开 | [源码](../../Mechanism/Visual/CxSceneItem.cs) |

### 43.3 批次3（27型）

| 类型 | 形态 | 原Owner | 源码成员摘录（S1） | 来源 |
|---|---|---|---|---|
| `CxSceneProjection` | 类 | `CxVisual.Mechanism.Visual` | `public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session)`；`public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session,Guid? pageId)`；`public static ReadOnlyCollection<CxSceneItem> Project(MEMEngineeringSession session,Guid? pageId, IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)`；`public static Guid? HitTest(IList<CxSceneItem> items, FGEPoint2D point)`；`public static ReadOnlyCollection<CxSceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId)`；`public static ReadOnlyCollection<CxSceneItem> ProjectResource(MEMEngineeringSession session,Guid resourceId, IMDSExternalBindingResolver runtimeSource,double elapsedMilliseconds=0)` | [源码](../../Mechanism/Visual/CxSceneProjection.cs) |
| `CxVisualEditor` | 类 | `CxVisual.Mechanism.Visual` | `public void SetRuntimeFrame(IMDSExternalBindingResolver source,double elapsedMilliseconds)`；`public void ClearRuntimeFrame()`；`public CxVisualEditor(MEMEngineeringSession session)`；`public event EventHandler Changed`；`public event EventHandler<MVIInteractionEventArgs> Interaction`；`public Guid? CurrentPageId`；`public Guid? CurrentResourceId`；`public Guid? SelectedId`；`public void SetPage(Guid id)`；`public void OpenDefinition(Guid id)`；`public bool ReturnToPrevious()`；`public MEMEngineeringSession Document`；另有21项待展开 | [源码](../../Mechanism/Visual/CxVisualEditor.cs) |
| `MVIShapeCatalog` | 类 | `CxVisual.Mechanism.Visual` | `public static MRCShapeDefinition CreateDefault(MRCShapeKind kind)` | [源码](../../Mechanism/Visual/Catalog/MVIShapeCatalog.cs) |
| `MVIVisualHandleKind` | 枚举 | `CxVisual.Mechanism.Visual` | `Anchor`；`Control1`；`Control2`；`Resize`；`Rotate`；`ArcCenter`；`ArcRadiusX`；`ArcRadiusY`；`ArcStart`；`ArcEnd` | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs) |
| `MVIVisualHandle` | 类 | `CxVisual.Mechanism.Visual` | `public MVIVisualHandle(Guid elementId,MVIVisualHandleKind kind,int commandIndex,FGEPoint2D worldPoint)`；`public Guid ElementId`；`public MVIVisualHandleKind Kind`；`public int CommandIndex`；`public FGEPoint2D WorldPoint` | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs) |
| `MVIVisualEditing` | 类 | `CxVisual.Mechanism.Visual` | `public static IReadOnlyList<MVIVisualHandle> Handles(CxSceneItem item)`；`public static MVIVisualHandle HitHandle(CxSceneItem item,double x,double y,double radius=8)`；`public static MTCEditResult<bool> Edit(CxVisualEditor editor,MVIVisualHandle handle, double worldX,double worldY)`；`public static MTCEditResult<bool> CommitGeometry(CxVisualEditor editor, MRCShapeElement item,FGEPath2D newPath)` | [源码](../../Mechanism/Visual/Editing/MVIVisualEditing.cs) |
| `MVIVisualRuntimeEvaluator` | 类 | `CxVisual.Mechanism.Visual` | `public static MPMValue Resolve(MPMBinding binding,MPMValueKind target, IMDSExternalBindingResolver source)`；`public static bool StateActive(string trigger,IMDSExternalBindingResolver source)`；`public static double EvaluateNumber(string property,double declared, MRCVisualBehaviorDefinition behavior,double elapsedMilliseconds, IMDSExternalBindingResolver source)`；`public static MRCVisualStyle EvaluateStyle(MRCVisualStyle declared, MRCVisualBehaviorDefinition behavior,double elapsedMilliseconds, IMDSExternalBindingResolver source)` | [源码](../../Mechanism/Visual/Runtime/MVIVisualRuntimeEvaluator.cs) |
| `MVIVisualActionInvocation` | 类 | `CxVisual.Mechanism.Visual` | `internal MVIVisualActionInvocation(Guid itemId,string eventId,string actionId, string bindingId,string value) :this(itemId,null,eventId,actionId,bindingId,value)`；`internal MVIVisualActionInvocation(Guid itemId,MRIObjectAddress address, string eventId,string actionId,string bindingId,string value)`；`public MRIObjectAddress Address`；`public Guid ItemId`；`public string EventId`；`public string ActionId`；`public string ParameterBindingId`；`public string EventValue` | [源码](../../Mechanism/Visual/Runtime/MVIVisualEventRouter.cs) |
| `MVIVisualEventRouter` | 类 | `CxVisual.Mechanism.Visual` | `public static IReadOnlyList<MVIVisualActionInvocation> Resolve( MEMEngineeringSession document,Guid elementId,string eventId,string value)`；`public static IReadOnlyList<MVIVisualActionInvocation> Resolve( MEMEngineeringSession document,MRIObjectAddress address,string eventId,string value)`；`public static int Dispatch(MEMEngineeringSession document,MRIObjectAddress address, string eventId,string value,Func<MVIVisualActionInvocation,bool> authorizedHost)`；`public static int Dispatch(MEMEngineeringSession document,Guid elementId, string eventId,string value,Func<MVIVisualActionInvocation,bool> authorizedHost)` | [源码](../../Mechanism/Visual/Runtime/MVIVisualEventRouter.cs) |
| `MVIEventPhase` | 枚举 | `CxVisual.Mechanism.Visual` | `Capture`；`Target`；`Bubble` | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs) |
| `MVIInteractionKind` | 枚举 | `CxVisual.Mechanism.Visual` | `Select`；`Move`；`Delete`；`Place` | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs) |
| `MVIInteractionEventArgs` | 类 | `CxVisual.Mechanism.Visual` | `internal MVIInteractionEventArgs(MVIInteractionKind kind,Guid target,IReadOnlyList<Guid> route)`；`public MVIInteractionKind Kind`；`public MVIEventPhase Phase`；`public bool DefaultPrevented`；`public void PreventDefault()` | [源码](../../Mechanism/Visual/MVIInteractionEventArgs.cs) |
| `MOLBlockLayout` | 类 | `CxVisual.Mechanism.Visual` | `internal MOLBlockLayout(Guid id,MOLControlKind kind,string label,int depth,double x,double y,double height=40)`；`public Guid BlockId`；`public MOLControlKind Kind`；`public string Label`；`public int Depth`；`public double X`；`public double Y`；`public double Width`；`public double Height` | [源码](../../Mechanism/Visual/MOLFlowLayout.cs) |
| `MOLFlowLayout` | 类 | `CxVisual.Mechanism.Visual` | `public static IReadOnlyList<MOLBlockLayout> Build(MOLWiringDefinition wiring)`；`public static IReadOnlyList<MOLBlockLayout> Build(MOLLogicDefinition definition)` | [源码](../../Mechanism/Visual/MOLFlowLayout.cs) |
| `SceneView` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public IReadOnlyList<Guid> SelectedVisualIds`；`public void SetSelectedVisualIds(IEnumerable<Guid> ids)`；`public event Action<IReadOnlyList<Guid>> SelectionSetRequested`；`public event EventHandler<MPWVisualHandleEventArgs> HandleEditRequested`；`public IReadOnlyDictionary<Guid,string> AssetErrors`；`public void SetAssetResolver(Func<Guid,Stream> resolver)`；`public double Zoom`；`public bool SnapToGrid`；`public Func<string,Brush> ResourceBrushResolver`；`public void SetViewport(double zoom,double panX,double panY)`；`protected override void OnMouseWheel(MouseWheelEventArgs e)`；`protected override void OnMouseDown(MouseButtonEventArgs e)`；另有18项待展开 | [源码](../../Mechanism/Presentation.Wpf/SceneView.cs) |
| `MOLFlowCanvas` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static readonly DependencyProperty DefinitionProperty = DependencyProperty.Register("Definition",typeof(MOLLogicDefinition), typeof(MOLFlowCanvas),new FrameworkPropertyMetadata(null, `；`public MOLLogicDefinition Definition`；`public static readonly DependencyProperty WiringDefinitionProperty= DependencyProperty.Register("WiringDefinition",typeof(MOLWiringDefinition), typeof(MOLFlowCanvas),new FrameworkPropertyMet`；`public MOLWiringDefinition WiringDefinition`；`public event EventHandler<MOLBlockMoveRequestedEventArgs> BlockMoveRequested`；`public event EventHandler<MOLBlocksMoveRequestedEventArgs> BlocksMoveRequested`；`public event EventHandler<MOLSelectionChangedEventArgs> SelectionChanged`；`public double ZoomFactor`；`public IReadOnlyList<Guid> SelectedBlockIds`；`public void SetSelection(IEnumerable<Guid> ids)`；`public void RetainValidSelection()`；`public void SelectAll()`；另有26项待展开 | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MOLPortVisual` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `internal MOLPortVisual(Guid blockId,MPCPortDescriptor descriptor, MPCPortDirection direction,Point center)`；`internal MPCPortDescriptor Descriptor`；`public Guid BlockId`；`public string MemberId`；`public MPCPortKind Kind`；`public MPCPortDirection Direction`；`public Point Center` | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MOLBlockTemplateSelector` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static bool IsDecision(MOLControlKind kind)`；`public static bool IsContainer(MOLControlKind kind)` | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MOLSelectionChangedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `internal MOLSelectionChangedEventArgs(IEnumerable<Guid> ids)`；`public IReadOnlyList<Guid> BlockIds` | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MOLPortLinkRequestedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `internal MOLPortLinkRequestedEventArgs(MOLPortVisual from,MOLPortVisual to)`；`public Guid SourceBlockId`；`public string SourcePort`；`public Guid TargetBlockId`；`public string TargetPort`；`public MPCPortKind Kind` | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MOLPortLinkRejectedEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `internal MOLPortLinkRejectedEventArgs(string reason)`；`public string Reason` | [源码](../../Mechanism/Presentation.Wpf/MOLFlowCanvas.cs) |
| `MPWShapeRenderer` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static Geometry BuildGeometry(CxSceneItem item)`；`public static Geometry BuildGeometry(FGEPath2D geometry,FGETransform2D transform)`；`public static Brush BrushOf(MRCBrushDefinition definition,Brush fallback=null, Func<string,Brush> resourceResolver=null)`；`public static void Draw(DrawingContext dc,CxSceneItem item,double dx=0,double dy=0, Func<string,Brush> resourceResolver=null)` | [源码](../../Mechanism/Presentation.Wpf/Renderers/MPWShapeRenderer.cs) |
| `MPWVisualAssetRenderer` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static ImageSource DecodeBitmap(Stream input,string mediaType)`；`public static DrawingImage DecodeSvg(Stream input)` | [源码](../../Mechanism/Presentation.Wpf/Renderers/MPWVisualAssetRenderer.cs) |
| `MPWWidgetEventArgs` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public MPWWidgetEventArgs(Guid id,string eventId,string value) :this(id,null,eventId,value)`；`public MPWWidgetEventArgs(Guid id,MRIObjectAddress address,string eventId,string value)`；`public MRIObjectAddress Address`；`public Guid ElementId`；`public string EventId`；`public string Value` | [源码](../../Mechanism/Presentation.Wpf/Widgets/MPWWidgetPresenter.cs) |
| `MPWWidgetPresenter` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static bool Supports(string typeId)`；`public static FrameworkElement Create(CxSceneItem item,Action<MPWWidgetEventArgs> callback)`；`public static void UpdateAppearance(FrameworkElement widget,CxSceneItem item)` | [源码](../../Mechanism/Presentation.Wpf/Widgets/MPWWidgetPresenter.cs) |
| `MPWConfigEditorControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public bool WasSaved`；`public T Draft`；`public int VisibleEditorCount`；`public MPWConfigEditorControl(MHSConfigSection<T> section)`；`public MPWConfigEditorControl(MHSConfigAuthorizedSection<T> authorized)`；`public void RegisterEditor(string path, Func<string, Action<string>, FrameworkElement> factory)`；`internal static string ToConfigJson(string value, Type type)`；`public async Task<MHSConfigCommitResult> SaveDraftAsync(CancellationToken token)`；`public MHSConfigApplyResult ApplySaved(FCMConfigApplyPolicy boundary)`；`public async Task<MHSConfigCommitResult> RestoreAsNewSavedAsync(string version, CancellationToken token)`；`public void Dispose()` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs) |
| `MPWConfigEditorDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static bool Edit<T>(Window owner, MHSConfigAuthorizedSection<T> config) where T : new()`；`public static bool Edit<T>(Window owner, MHSConfigSection<T> config) where T : new()` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs) |

### 43.4 批次4（50型）

| 类型 | 形态 | 原Owner | 源码成员摘录（S1） | 来源 |
|---|---|---|---|---|
| `MPWConfigLayerOverview` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public string ProfileId`；`public int DisplayedSourceCount`；`public MPWConfigLayerOverview(MVBConfigAtomicLayerProfile<T> profile) : this(profile.ProfileId, profile.Layers, profile.ResolveEffective, profile.ResolveSaved, (layer, path, json, token) => `；`public MPWConfigLayerOverview(MVBConfigLayeredProfile<T> profile) : this(profile.ProfileId, profile.Layers, profile.ResolveEffective, null, (layer, path, json, token) => profile.SetOverrideP`；`public void RefreshOverview()` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigLayerOverview.cs) |
| `MPWConfigLayerDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static void Show<T>(Window owner, MVBConfigAtomicLayerProfile<T> profile) where T : new()`；`public static void Show<T>(Window owner, MVBConfigLayeredProfile<T> profile) where T : new()` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigLayerOverview.cs) |
| `MPWConfigAtomicGroupControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public int MemberCount`；`public int PendingFields`；`public MPWConfigAtomicGroupControl(MHSConfigAtomicGroup group)`；`public void StageField(string memberKey, string path, string raw)`；`public async Task<MHSConfigCommitResult> SaveGroupDraftAsync(CancellationToken token)` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigAtomicGroupControl.cs) |
| `MPWConfigAtomicGroupDialog` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public static void Show(Window owner, MHSConfigAtomicGroup group)` | [源码](../../Mechanism/Presentation.Wpf/MPWConfigAtomicGroupControl.cs) |
| `LogViewerControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public LogViewerControl()`；`public void Attach(FLDLogService hub)`；`public void Detach()` | [源码](../../Mechanism/Presentation.Wpf/LogViewerControl.xaml.cs) |
| `SystemMonitorControl` | 类 | `CxVisual.Mechanism.Presentation.Wpf` | `public SystemMonitorControl()`；`public void Attach(FCRSystemMonitor monitor)`；`public void Detach()` | [源码](../../Mechanism/Presentation.Wpf/SystemMonitorControl.xaml.cs) |
| `MOCDiagnosticSeverity` | 枚举 | `CxVisual.Mechanism.Orchestration` | `Info`；`Warning`；`Error` | [源码](../../Mechanism/Orchestration/MOCBuildService.cs) |
| `MOCDiagnostic` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCDiagnostic(MOCDiagnosticSeverity severity,string code,string message, Guid contentId,Guid? blockId=null,Guid? linkId=null)`；`public MOCDiagnosticSeverity Severity`；`public string Code`；`public string Message`；`public Guid ContentId`；`public Guid? BlockId`；`public Guid? LinkId` | [源码](../../Mechanism/Orchestration/MOCBuildService.cs) |
| `MOCBuildResult` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCBuildResult(MOCCompiledPlan plan,IEnumerable<MOCDiagnostic> diagnostics)`；`public bool Succeeded`；`public MOCCompiledPlan Plan`；`public IReadOnlyList<MOCDiagnostic> Diagnostics` | [源码](../../Mechanism/Orchestration/MOCBuildService.cs) |
| `MOCBuildService` | 类 | `CxVisual.Mechanism.Orchestration` | `public MOCBuildResult Build(MEMProjectSnapshot snapshot,Guid contentId, IMRKCapabilityResolver resolver)` | [源码](../../Mechanism/Orchestration/MOCBuildService.cs) |
| `MOCStepOrigin` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCStepOrigin(string step, MRIObjectAddress address, Guid block, IEnumerable<Guid> callPath, int? iteration, string role)`；`public string StepId`；`public MRIObjectAddress ContentAddress`；`public Guid BlockId`；`public IReadOnlyList<Guid> CallPath`；`public int? Iteration`；`public string Role` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCSourceMap` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCSourceMap(IEnumerable<MOCStepOrigin> origins)`；`public IReadOnlyDictionary<string, MOCStepOrigin> Steps`；`public bool TryResolve(string compiledStepId, out MOCStepOrigin origin)`；`public IList<MOCStepOrigin> Find(Guid blockId)` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCObservedStep` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCObservedStep(MRKExecutionTransition transition, MOCStepOrigin origin)`；`public MRKExecutionTransition Transition`；`public MOCStepOrigin Origin` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCTraceView` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCTraceView(IList<MOCObservedStep> events, long cursor, long lost)`；`public IList<MOCObservedStep> Events`；`public long NextCursor`；`public long LostCount` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCCompiledPlan` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCCompiledPlan(MRKExecutionPlan plan, MOCSourceMap source, Guid projectId, int revision, IDictionary<string,object> inputDefaults=null)`；`public IReadOnlyDictionary<string,object> InputDefaults`；`public MRKExecutionPlan Plan`；`public MOCSourceMap SourceMap`；`public Guid ProjectId`；`public int EngineeringRevision`；`public MOCTraceView ReadTrace(MRKFlowHandle handle, long afterSequence)` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCFlowBuilder` | 类 | `CxVisual.Mechanism.Orchestration` | `public void ExposeInput(string publicName,Guid blockId,string inputPort)`；`public void ExposeOutput(string publicName,Guid blockId,string outputPort)`；`public void RemoveExposedInput(string publicName)`；`public void RemoveExposedOutput(string publicName)`；`public Guid LastBlockId`；`public Guid InvokeContent(MEMContent content)`；`public Guid Step(MOCCapabilityDescriptor descriptor)`；`public Guid Step(string capabilityId,IEnumerable<MPCPortDescriptor> extraPorts=null)`；`public Guid Connect(Guid source,string outPort,Guid target,string inPort, MPCPortKind kind=MPCPortKind.Control)`；`public Guid ConnectConditional(Guid source,string outPort,Guid target,string inPort, string guard)`；`public void Disconnect(Guid linkId)`；`public void Remove(Guid blockId)`；另有27项待展开 | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCEngineeringCompiler` | 类 | `CxVisual.Mechanism.Orchestration` | `public MOCCompiledPlan Compile(MEMProjectSnapshot snapshot, Guid entryContentId, IMRKCapabilityResolver resolver, IEnumerable<Guid> instancePath = null)` | [源码](../../Mechanism/Orchestration/MOCEngineeringCompiler.cs) |
| `MOCCapabilityDescriptor` | 类 | `CxVisual.Mechanism.Orchestration` | `public MOCCapabilityDescriptor(string id,string name,string category, IEnumerable<MPCPortDescriptor> ports=null,int version=1)`；`public string Id`；`public string Name`；`public string Category`；`public int Version`；`public IReadOnlyList<MPCPortDescriptor> Ports` | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs) |
| `IMOCCapabilityProvider` | 接口 | `CxVisual.Mechanism.Orchestration` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs) |
| `MOCCapabilityCatalogItem` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCCapabilityCatalogItem(MOCCapabilityDescriptor item,bool ready)`；`public MOCCapabilityDescriptor Definition`；`public bool RuntimeAvailable` | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs) |
| `MOCCapabilityCatalog` | 类 | `CxVisual.Mechanism.Orchestration` | `public MOCCapabilityCatalog(IEnumerable<IMOCCapabilityProvider> providers, IMRKCapabilityResolver runtime)`；`public IReadOnlyList<MOCCapabilityCatalogItem> All`；`public MOCCapabilityCatalogItem Find(string id)`；`public IReadOnlyList<MOCCapabilityCatalogItem> Search(string query,int limit=50)` | [源码](../../Mechanism/Orchestration/MOCCapabilityCatalog.cs) |
| `MOCProjectReleaseCandidate` | 类 | `CxVisual.Mechanism.Orchestration` | `internal MOCProjectReleaseCandidate(MOCCompiledPlan plan,Guid entry, string fingerprint,IDictionary<string,string> dependencies)`；`public MOCCompiledPlan Plan`；`public Guid ProjectId`；`public int EngineeringRevision`；`public Guid EntryContentId`；`public string Fingerprint`；`public IReadOnlyDictionary<string,string> CapabilityVersions` | [源码](../../Mechanism/Orchestration/MOCProjectRelease.cs) |
| `MOCProjectRelease` | 类 | `CxVisual.Mechanism.Orchestration` | `public static MOCProjectReleaseCandidate Prepare(MEMProjectSnapshot snapshot, Guid entryContentId,IMRKCapabilityResolver resolver, IReadOnlyDictionary<string,string> declaredVersions)`；`public static MRKPlanPublicationReceipt Stage(MOCProjectReleaseCandidate candidate, MEMProjectSnapshot current,IMRKCapabilityResolver resolver, IReadOnlyDictionary<string,string> declaredVer` | [源码](../../Mechanism/Orchestration/MOCProjectRelease.cs) |
| `MRKFlowState` | 枚举 | `CxVisual.Mechanism.Runtime` | `Running`；`Completed`；`Failed`；`Cancelled` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKStepState` | 枚举 | `CxVisual.Mechanism.Runtime` | `WaitingDependencies`；`Scheduled`；`Completed`；`Failed`；`Cancelled`；`Queued`；`Running`；`Waiting`；`Paused`；`WaitingResources`；`WaitingGroup`；`Skipped`；另有1项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKExecutionContext` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKExecutionContext(string planId, IDictionary<string, object> inputs, MRKFlowMetadata metadata = null, IFDEExecutionDataScope dataSpace = null, IMRKExecutionJournal journal = null,`；`internal MRKExecutionContext ForSubflow(string prefix)`；`internal MRKExecutionContext ForLoopIteration(string prefix,int iteration, bool parallel=false)`；`public int IterationIndex`；`internal string QualifiedStepId(string id)`；`internal int LoopExit`；`internal bool FlowReturnRequested`；`public void SignalControlExit(string kind)`；`public bool TryGetScopedResult(string stepId,out object value)`；`internal MRKExecutionContext WithInvocation(MRKInvocationContext invocation)`；`public MRKInvocationContext Invocation`；`internal MRKExecutionContext WithBoundInputs(IDictionary<string, object> ports)`；另有25项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKFlowResult` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKFlowResult(MRKFlowState state, MRKExecutionContext context, Exception error, IDictionary<string, object> committedOutputs = null)`；`public MRKFlowState State`；`public Guid FlowId`；`public MRKFlowMetadata Metadata`；`public DateTime CompletedUtc`；`public Exception Error`；`public IDictionary<string, object> Results`；`public IDictionary<string, object> CommittedOutputs`；`public T GetCommittedOutput<T>(string name)`；`public IDictionary<string, MRKStepState> StepStates` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKFlowHandle` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKFlowHandle(MRKExecutionContext context, CancellationTokenSource cancellation, MRKExecutionPlan originalPlan, MRKExecutionEngine ownerEngine)`；`internal bool IsOriginalPlan(MRKExecutionPlan candidate)`；`internal bool IsOwnedBy(MRKExecutionEngine engine)`；`public MRKExecutionContext Context`；`public Guid FlowId`；`public MRKFlowState State`；`public Task<MRKFlowResult> Completion`；`public MRKFlowSnapshot GetSnapshot()`；`internal CancellationToken Token`；`internal void SetCompletion(Task<MRKFlowResult> completion)`；`public bool RequestCancel()`；`internal bool TryStartCompensation()`；另有1项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `IMRKExecutionEngine` | 接口 | `CxVisual.Mechanism.Runtime` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKExecutionEngine` | 类 | `CxVisual.Mechanism.Runtime` | `public int ActiveFlowCount`；`public MRKExecutionEngine(FCRWorkerOptions options, MRKResourceLeaseCoordinator coordinator = null, IEnumerable<MRKExecutionGroupDefinition> groups = null, IMRKExecutionJournal journal = nul`；`public IList<MRKExecutionGroupSnapshot> GroupSnapshot()`；`public FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action, string serialKey = null, bool droppable = false, CancellationToken cancellation = default(CancellationToken))`；`public async Task<MRKCompensationReport> CompensateAsync( MRKExecutionPlan plan, MRKFlowHandle flow, CancellationToken cancellation = default(CancellationToken))`；`public FCRWorkerSnapshot GetSnapshot()`；`public Task StopAsync(bool cancelPending = false)`；`public MRKFlowHandle StartFromSlot(MRKExecutionPlanSlot slot, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(CancellationToken))`；`public MRKFlowHandle StartFromSlotWithMetadata(MRKExecutionPlanSlot slot, MRKFlowMetadata metadata, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(Cancel`；`public MRKFlowHandle StartFromSlotWithControl(MRKExecutionPlanSlot slot, MRKExecutionDebugController control, MRKFlowMetadata metadata = null, IDictionary<string, object> inputs = null, Canc`；`public MRKFlowHandle StartFlow(MRKExecutionPlan plan, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(CancellationToken))`；`public MRKFlowHandle StartFlowWithMetadata(MRKExecutionPlan plan, MRKFlowMetadata metadata, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(CancellationTo`；另有2项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionEngine.cs) |
| `MRKExecutionStep` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionStep(string id, IEnumerable<string> dependencies, Func<MRKExecutionContext, CancellationToken, Task<object>> executeAsync, string serialKey = null, MRKExecutionPolicy poli`；`public string Id`；`public string SerialKey`；`public int Priority`；`public long EstimatedPendingBytes`；`public string ExecutionGroup`；`public IList<MRKExecutionInput> Inputs`；`public MRKExecutionOutput Output`；`public IDictionary<string, Type> DataOutputs`；`public bool EnableDataContext`；`public string SubflowAccessGroup`；`public Func<MRKExecutionContext, bool> Condition`；另有9项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs) |
| `MRKLoopRegion` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKLoopRegion(MRKExecutionPlan body,int maxIterations, Func<MRKExecutionContext,int,bool> condition=null, string collectionInput=null,bool executeFirst=false, int maxParallelIteration`；`public MRKExecutionPlan Body`；`public int MaxIterations`；`public int MaxParallelIterations`；`public Func<MRKExecutionContext,int,bool> Condition`；`public string CollectionInput`；`public bool ExecuteFirst` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs) |
| `MRKControlRegion` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKControlRegion(string id,string entryStepId,string exitStepId, IEnumerable<string> members)`；`public string Id`；`public string EntryStepId`；`public string ExitStepId`；`public IList<string> Members` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs) |
| `MRKExecutionPlan` | 类 | `CxVisual.Mechanism.Runtime` | `public IDictionary<string, string> DataExports`；`public IDictionary<string, Type> DataExportTypes`；`public MRKExecutionPlan(string id, IEnumerable<MRKExecutionStep> steps, IDictionary<string, string> dataExports = null, IEnumerable<MRKControlRegion> regions = null)`；`public static IList<MRKExecutionStep> InlineSubflow( string prefix, MRKExecutionPlan child, IEnumerable<string> entryDependencies, IEnumerable<MRKExecutionInput> mappedInputs = null)`；`public string Id`；`public IList<MRKExecutionStep> Steps`；`public IList<MRKControlRegion> Regions`；`public MRKExecutionStep GetStep(string id)` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlan.cs) |
| `MRKRuntimeSessionState` | 枚举 | `CxVisual.Mechanism.Runtime` | `Created`；`Ready`；`Stopping`；`Stopped` | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs) |
| `MRKRuntimeSessionSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKRuntimeSessionSnapshot(string id, MRKRuntimeSessionState state, int active, long rejected, long revision)`；`public string Id`；`public MRKRuntimeSessionState State`；`public int ActiveFlows`；`public long Rejected`；`public long PlanRevision` | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs) |
| `MRKRuntimeSession` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKRuntimeSession(string id,MRKExecutionEngine engine,MRKExecutionPlanSlot slot,int maxInFlight=1)`；`public string Id`；`public MRKRuntimeSessionSnapshot Snapshot()`；`public void Start()`；`public MRKFlowHandle TryStartFlow(IDictionary<string,object> inputs=null, string businessKey=null,string correlationId=null, CancellationToken cancellation=default(CancellationToken))`；`public MRKFlowHandle TryStartFlowWithControl(MRKExecutionDebugController control, IDictionary<string,object> inputs=null, string businessKey=null,string correlationId=null, CancellationToken`；`public bool TryActivatePendingPlan()`；`public Task StopAsync(bool cancel=false)` | [源码](../../Mechanism/Runtime/Execution/MRKRuntimeSession.cs) |
| `MRKExecutionTransition` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKExecutionTransition(string stepId, MRKStepState state, long sequence)`；`public string StepId`；`public MRKStepState State`；`public long Sequence`；`public DateTime TimestampUtc` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs) |
| `MRKTraceBatch` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKTraceBatch(IList<MRKExecutionTransition> changes, long cursor, long lost)`；`public IList<MRKExecutionTransition> Changes`；`public long NextCursor`；`public long LostCount`；`public bool HasGap` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs) |
| `MRKExecutionTrace` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionTrace(int capacity = 2048)`；`public long Dropped`；`internal void Record(string stepId, MRKStepState state)`；`public IList<MRKExecutionTransition> Snapshot()`；`public MRKTraceBatch ReadSince(long lastSequence)` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs) |
| `MRKExecutionDebugController` | 类 | `CxVisual.Mechanism.Runtime` | `public bool IsPaused`；`public int WaitingCount`；`public void AddBreakpoint(string id)`；`public void RemoveBreakpoint(string id)`；`public void Pause()`；`public void Resume()`；`public bool StepOnce()`；`internal Task WaitBeforeAsync(string stepId, CancellationToken cancellation)`；`internal Task WaitBeforeAsync(string stepId,string qualifiedStepId, CancellationToken cancellation)` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs) |
| `MRKExecutionOperations` | 类 | `CxVisual.Mechanism.Runtime` | `public static Func<MRKExecutionContext, CancellationToken, Task<object>> Condition( Func<MRKExecutionContext, bool> predicate, Func<MRKExecutionContext, CancellationToken, Task<object>> when`；`public static Func<MRKExecutionContext, CancellationToken, Task<object>> Loop( int maxIterations, Func<MRKExecutionContext, int, bool> shouldContinue, Func<MRKExecutionContext, int, Cancella` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionControls.cs) |
| `MRKFlowMetadata` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKFlowMetadata(long planRevision = 0, string businessKey = null, string correlationId = null, string sessionId = null, Guid? parentFlowId = null)`；`public long PlanRevision`；`public string BusinessKey`；`public string CorrelationId`；`public string SessionId`；`public Guid? ParentFlowId` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs) |
| `MRKInvocationContext` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKInvocationContext(Guid flowId,string stepId, int activationIndex=0,string parentInvocationId=null)`；`public Guid FlowId`；`public string StepId`；`public int ActivationIndex`；`public string ParentInvocationId`；`public string InvocationId`；`public string FrameKey` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs) |
| `MRKStepObservation` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKStepObservation(string id, MRKStepState state, Guid? workId, string reason, DateTime changedUtc,string invocationId=null)`；`public string StepId`；`public string InvocationId`；`public MRKStepState State`；`public Guid? WorkId`；`public string Reason`；`public DateTime ChangedUtc` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs) |
| `MRKFlowSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKFlowSnapshot(Guid id, string planId, MRKFlowMetadata metadata, MRKFlowState state, bool cancelRequested, DateTime createdUtc, DateTime? endedUtc, int total, int completed, int wa`；`public Guid FlowId`；`public string PlanId`；`public MRKFlowMetadata Metadata`；`public MRKFlowState State`；`public bool CancelRequested`；`public DateTime CreatedUtc`；`public DateTime? EndedUtc`；`public int TotalSteps`；`public int CompletedSteps`；`public int WaitingSteps`；`public int FailedSteps`；另有2项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionIdentity.cs) |
| `MRKPlanPublishAction` | 枚举 | `CxVisual.Mechanism.Runtime` | `Stage`；`Activate`；`Rollback` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs) |
| `IMRKPlanPublicationPolicy` | 接口 | `CxVisual.Mechanism.Runtime` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs) |
| `MRKPlanPublicationReceipt` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKPlanPublicationReceipt(long revision, bool accepted, IList<MRKCompileDiagnostic> diagnostics)`；`public long Revision`；`public bool Accepted`；`public IList<MRKCompileDiagnostic> Diagnostics` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs) |
| `MRKExecutionPlanPublisher` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionPlanPublisher(MRKExecutionPlanSlot slot, MRKExecutionPlan initial, long initialRevision, IMRKPlanPublicationPolicy policy)`；`public long ActiveRevision`；`public long? PendingRevision`；`public MRKPlanPublicationReceipt CompileAndStage(string principal, string planId, IEnumerable<MRKStepCompileRequest> requests, IMRKCapabilityResolver resolver, long revision)`；`public MRKPlanPublicationReceipt CompileAndStageOrchestration(string principal, string rootId, IMRKFlowCompileCatalog catalog, IMRKCapabilityResolver resolver, long revision)`；`public MRKPlanPublicationReceipt CompileAndStageBlueprint(string principal, string rootId, MRKCodeRuntimeBlueprint blueprint, long revision)`；`public MRKPlanPublicationReceipt StagePrecompiled(string principal, MRKExecutionPlan compiledPlan,long revision)`；`public void StageRollback(string principal, long newRevision)`；`public bool TryActivate(string principal)` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPublishing.cs) |

### 43.5 批次5（43型）

| 类型 | 形态 | 原Owner | 源码成员摘录（S1） | 来源 |
|---|---|---|---|---|
| `MRKExecutionPlanLease` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKExecutionPlanLease(MRKExecutionPlan plan, long revision, Action release)`；`public MRKExecutionPlan Plan`；`public long Revision`；`public void Dispose()` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs) |
| `MRKExecutionPlanSlot` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionPlanSlot(MRKExecutionPlan initial, long revision, bool requireAuthorizedPublication = false)`；`public long ActiveRevision`；`public bool RequiresAuthorization`；`public long? PendingRevision`；`public int RunningFlows`；`public MRKExecutionPlanLease Capture()`；`public void StageValidated(MRKExecutionPlan candidate, long revision)`；`internal void StageAuthorized(MRKExecutionPlan candidate, long revision)`；`public bool TryActivatePending()`；`internal bool TryActivateAuthorized()` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs) |
| `MRKJournalKind` | 枚举 | `CxVisual.Mechanism.Runtime` | `FlowStarted`；`StepTransition`；`FlowFinished`；`ExternalUnknown`；`ExternalConfirmed` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs) |
| `MRKJournalRecord` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKJournalRecord(MRKJournalKind kind, Guid flowId, string planId, long revision, string stepId, string state, string correlationId = null, Guid operationId = default(Guid))`；`public long Sequence`；`public MRKJournalKind Kind`；`public Guid FlowId`；`public string PlanId`；`public long Revision`；`public string StepId`；`public string State`；`public string CorrelationId`；`public Guid OperationId`；`public DateTime TimestampUtc` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs) |
| `IMRKExecutionJournal` | 接口 | `CxVisual.Mechanism.Runtime` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs) |
| `MRKRecoveryDecision` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKRecoveryDecision(Guid flow, MRKRecoveryDisposition disposition, string reason)`；`public Guid FlowId`；`public MRKRecoveryDisposition Disposition`；`public string Reason` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs) |
| `MRKFileExecutionJournal` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKFileExecutionJournal(string path)`；`public void Append(MRKJournalRecord record)`；`public IList<MRKJournalRecord> ReadAll()`；`public void Dispose()` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionJournal.cs) |
| `MRKInputSource` | 枚举 | `CxVisual.Mechanism.Runtime` | `FlowInput`；`StepResult`；`StepOutput` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs) |
| `MRKExecutionInput` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionInput(string name, MRKInputSource source, string sourceName, Type valueType, bool required = true)`；`public string Name`；`public MRKInputSource Source`；`public string SourceName`；`public Type ValueType`；`public bool Required` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs) |
| `MRKExecutionOutput` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionOutput(Type valueType, bool allowNull = false)`；`public Type ValueType`；`public bool AllowNull` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs) |
| `MRKExecutionPortException` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionPortException(string stepId, string portName, string reason) : base("节点 " + stepId + " 端口 " + portName + "：" + reason)`；`public string StepId`；`public string PortName` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionPorts.cs) |
| `MRKExecutionGroupDefinition` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionGroupDefinition(string id, int maxConcurrency, int maxWaiting)`；`public string Id`；`public int MaxConcurrency`；`public int MaxWaiting` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs) |
| `MRKExecutionGroupSnapshot` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKExecutionGroupSnapshot(string id, int active, int waiting, long rejected)`；`public string Id`；`public int Active`；`public int Waiting`；`public long Rejected` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs) |
| `MRKExecutionGroups` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKExecutionGroups(IEnumerable<MRKExecutionGroupDefinition> definitions)`；`public bool Contains(string id)`；`public async Task<MRKExecutionGroupLease> AcquireAsync(string id, CancellationToken cancellation = default(CancellationToken))`；`public IList<MRKExecutionGroupSnapshot> Snapshot()` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionGroups.cs) |
| `IMRKCapabilityResolver` | 接口 | `CxVisual.Mechanism.Runtime` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs) |
| `MRKStepCompileRequest` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKStepCompileRequest(string id, string capabilityId, IEnumerable<string> dependencies = null, IEnumerable<MRKExecutionInput> inputs = null, string serialKey = null, string executionG`；`public string Id`；`public string CapabilityId`；`public string SubflowId`；`public MRKLoopRegion LoopRegion`；`public IList<string> Dependencies`；`public IList<MRKExecutionInput> Inputs`；`public string SerialKey`；`public string ExecutionGroup`；`public MRKExecutionPolicy Policy`；`public IList<string> RequiredResources`；`public Func<MRKExecutionContext, bool> Condition`；另有4项待展开 | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs) |
| `MRKCompileDiagnostic` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKCompileDiagnostic(string stepId, string code, string message)`；`public string StepId`；`public string Code`；`public string Message` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs) |
| `MRKCompileResult` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKCompileResult(MRKExecutionPlan plan, IList<MRKCompileDiagnostic> diagnostics)`；`public MRKExecutionPlan Plan`；`public IList<MRKCompileDiagnostic> Diagnostics`；`public bool Succeeded` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs) |
| `MRKExecutionPlanCompiler` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKCompileResult Compile(string planId, IEnumerable<MRKStepCompileRequest> source, IMRKCapabilityResolver resolver, IDictionary<string, string> dataExports = null, IEnumerable<MRKCont` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionCompiler.cs) |
| `MRKFlowCompileDefinition` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKFlowCompileDefinition(string id, IEnumerable<MRKStepCompileRequest> steps, IDictionary<string, string> dataExports = null)`；`public string Id`；`public IList<MRKStepCompileRequest> Steps`；`public IDictionary<string, string> DataExports` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs) |
| `IMRKFlowCompileCatalog` | 接口 | `CxVisual.Mechanism.Runtime` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs) |
| `MRKExecutionOrchestrationCompiler` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKCompileResult Compile(string rootId, IMRKFlowCompileCatalog catalog, IMRKCapabilityResolver resolver)` | [源码](../../Mechanism/Runtime/Execution/MRKExecutionComposition.cs) |
| `MRKCodeFlowBuilder` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKCodeFlowBuilder(string id)`；`public MRKCodeFlowBuilder Step(string id, string capabilityId, params string[] after)`；`public MRKCodeFlowBuilder Subflow(string id, string flowId, params string[] after)`；`public MRKCodeFlowBuilder StepData(string id, string capabilityId, IEnumerable<MRKExecutionInput> inputs, params string[] after)`；`public MRKCodeFlowBuilder Subflow(string id, string flowId, IEnumerable<MRKExecutionInput> inputs, params string[] after)`；`public MRKCodeFlowBuilder Export(string name, string stepOutput)`；`public MRKCodeFlowBuilder Add(MRKStepCompileRequest definition)`；`internal MRKFlowCompileDefinition Freeze()` | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs) |
| `MRKCodeRuntimeBuilder` | 类 | `CxVisual.Mechanism.Runtime` | `public MRKCodeRuntimeBuilder Capability(string id, Func<MRKExecutionContext, CancellationToken, Task<object>> action, Type outputType = null)`；`public MRKCodeRuntimeBuilder DataCapability(string id, Func<MRKExecutionContext, CancellationToken, Task<object>> action, IDictionary<string, Type> dataOutputs, IEnumerable<MRKCapabilityInpu`；`public MRKCodeRuntimeBuilder Capability<TResult>(string id, Func<MRKExecutionContext, CancellationToken, Task<TResult>> action)`；`public MRKCodeRuntimeBuilder CapabilityForRequest<TRequest, TResult>(string id, Func<TRequest, CancellationToken, Task<TResult>> action)`；`public MRKCodeRuntimeBuilder Flow(string id, Action<MRKCodeFlowBuilder> configure)`；`public MRKCodeRuntimeBlueprint Freeze()`；`public MRKDevelopmentRuntime Build(FCRWorkerOptions options = null)` | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs) |
| `MRKCodeRuntimeBlueprint` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKCodeRuntimeBlueprint( IDictionary<string, MRKFlowCompileDefinition> definitions, IDictionary<string, MRKResolvedCapability> capabilities)`；`public IList<string> FlowIds`；`public bool TryGet(string flowId, out MRKFlowCompileDefinition definition)`；`public bool TryResolve(string capabilityId, out MRKResolvedCapability capability)`；`public MRKDevelopmentRuntime BuildForDevelopment(FCRWorkerOptions options = null)` | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs) |
| `MRKDevelopmentRuntime` | 类 | `CxVisual.Mechanism.Runtime` | `internal MRKDevelopmentRuntime(MRKExecutionEngine engine, IDictionary<string, MRKExecutionPlan> plans)`；`public IList<string> FlowIds`；`public MRKFlowHandle StartFlow(string flowId, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(CancellationToken))`；`public Task<MRKFlowResult> RunAsync(string flowId, IDictionary<string, object> inputs = null, CancellationToken cancellation = default(CancellationToken))`；`public Task<MRKFlowResult> RunAsync(string flowId, object request, CancellationToken cancellation = default(CancellationToken))`；`public Task<MRKFlowResult> RunRequestAsync<TRequest>(string flowId, TRequest request, CancellationToken cancellation = default(CancellationToken))`；`public async Task<TResult> RunValueAsync<TResult>(string flowId, object request, string outputName, CancellationToken cancellation = default(CancellationToken))`；`public async Task<TResult> RunValueAsync<TResult>(string flowId, IDictionary<string, object> inputs, string outputName, CancellationToken cancellation = default(CancellationToken))`；`public FCRWorkerSnapshot Snapshot()`；`public Task StopAsync(bool cancelPending = false)` | [源码](../../Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs) |
| `MDSDataHeap` | 类 | `CxVisual.Mechanism.Data` | `public FDTDataAdapterRegistry Adapters`；`public MDSDataBudget Budget`；`public string RunId`；`public MDSDataHeap(FDTDataAdapterRegistry adapters = null, MDSDataBudget budget = null)`；`internal object SyncRoot`；`public int Count`；`public void Set<T>(string name, T value)`；`public T Get<T>(string name)`；`public IList<string> Search(string prefix = "")`；`public void Remove(string name)`；`public void Replace<T>(string name, T value)`；`internal object TransferToken(string name)`；另有6项待展开 | [源码](../../Mechanism/Data/RuntimeData/MDSDataHeap.cs) |
| `MDSDataStack` | 类 | `CxVisual.Mechanism.Data` | `public MDSDataHeap FlowHeap`；`public MDSDataBudget Budget`；`public MDSDataStack(IFDEExecutionDataScope root = null) : this(root, null)`；`public MDSDataStack(IFDEExecutionDataScope root, FDTDataAdapterRegistry adapters, string runId = null, MDSDataBudget budget = null)`；`public IFDEExecutionDataScope RootScope`；`public int ActiveFrames`；`public MDSDataFrame OpenFrame(string invocationId, IFDEExecutionDataScope scope, IDictionary<string, object> inputs, IDictionary<string, Type> declaredOutputs)`；`internal void Publish(string invocationId, IDictionary<string, object> outputs)`；`public bool TryReadOutput<T>(string invocationId, string outputName, out T value)`；`public bool TryReadOutput(string invocationId, string outputName, out object value)`；`internal void FrameClosed(string invocationId)`；`public void Dispose()` | [源码](../../Mechanism/Data/RuntimeData/MDSDataStack.cs) |
| `MDSDataFrame` | 类 | `CxVisual.Mechanism.Data` | `internal MDSDataFrame(MDSDataStack owner, string id, IFDEExecutionDataScope scope, IDictionary<string, object> inputs, IDictionary<string, Type> outputs)`；`public string InvocationId`；`public IMDSDataContext Data`；`public IDictionary<string, object> CommitOutputs()`；`public void AbortOutputs()`；`public void Dispose()` | [源码](../../Mechanism/Data/RuntimeData/MDSDataStack.cs) |
| `IMDSDataContext` | 接口 | `CxVisual.Mechanism.Data` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Data/RuntimeData/MDSDataContext.cs) |
| `MDSDataContext` | 类 | `CxVisual.Mechanism.Data` | `public FDTDataAdapterRegistry Adapters`；`public MDSDataContext(IFDEExecutionDataScope scope) : this(scope, null, null, null, null)`；`internal MDSDataContext(IFDEExecutionDataScope scope, IDictionary<string, object> inputs, IDictionary<string, Type> outputs, Action<IDictionary<string, object>> publish, MDSDataHeap heap)`；`public T Get<T>(FDTDataKey<T> key)`；`public void Set<T>(FDTDataKey<T> key, T value)`；`public void Return<T>(FDTDataKey<T> key, T value)`；`public T Get<T>(string name)`；`public bool TryGet<T>(string name, out T value)`；`public void Set<T>(string name, T value)`；`public void Return<T>(string name, T value)`；`public IDictionary<string, object> InspectPendingOutputs()`；`public IDictionary<string, object> CommitOutputs()`；另有14项待展开 | [源码](../../Mechanism/Data/RuntimeData/MDSDataContext.cs) |
| `MDSBindingSource` | 枚举 | `CxVisual.Mechanism.Data` | `Constant`；`CurrentSlot`；`NodeOutput`；`FlowHeap`；`Variable`；`EngineeringResource`；`Expression` | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs) |
| `MDSDataBinding` | 类 | `CxVisual.Mechanism.Data` | `public MDSDataBinding(MDSBindingSource source, Type valueType, string sourceName = null, object constant = null)`；`public MDSBindingSource Source`；`public Type ValueType`；`public string SourceName`；`internal object CaptureConstant(FDTDataAdapterRegistry adapters)` | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs) |
| `IMDSExternalBindingResolver` | 接口 | `CxVisual.Mechanism.Data` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs) |
| `MDSDataBindingResolver` | 类 | `CxVisual.Mechanism.Data` | `public static T Resolve<T>(IMDSDataContext data, MDSDataBinding binding, IMDSExternalBindingResolver external = null)`；`public static IList<MDSDataSnapshot> CompatibleCandidates(IMDSDataContext data, Type targetType, bool includeUnloaded = false)` | [源码](../../Mechanism/Data/RuntimeData/MDSDataBindings.cs) |
| `MDSDataLocation` | 枚举 | `CxVisual.Mechanism.Data` | `Any`；`FlowHeap`；`Input`；`Local`；`PendingOutput`；`CommittedOutput` | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs) |
| `MDSDataQuery` | 类 | `CxVisual.Mechanism.Data` | `public string NamePrefix`；`public string ExactName`；`public Type DataType`；`public MDSDataLocation Location`；`public MDSDataLoadState? LoadState`；`public string RunId`；`public string ScopeId`；`public int Skip`；`public int Take` | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs) |
| `MDSDataSnapshot` | 类 | `CxVisual.Mechanism.Data` | `public MDSDataSnapshot(string name, Type dataType, MDSDataLocation location, MDSDataLoadState loadState, string source, long? sizeBytes, long version, string runId = null, string scopeId = n`；`public string Name`；`public Type DataType`；`public MDSDataLocation Location`；`public MDSDataLoadState LoadState`；`public string Source`；`public long? SizeBytes`；`public long Version`；`public string RunId`；`public string ScopeId` | [源码](../../Mechanism/Data/RuntimeData/MDSDataQuery.cs) |
| `MDSDataBudget` | 类 | `CxVisual.Mechanism.Data` | `public long LimitBytes`；`public long RetainedBytes`；`public MDSDataBudget(long limitBytes)`；`internal long Measure(object value, FDTDataAdapterRegistry adapters)`；`internal void Reserve(long bytes)`；`internal void Release(long bytes)` | [源码](../../Mechanism/Data/RuntimeData/MDSDataBudget.cs) |
| `MDSDataLoadState` | 枚举 | `CxVisual.Mechanism.Data` | `NotFile`；`Unloaded`；`Loading`；`Loaded`；`Failed` | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs) |
| `IMDSDataFileLoader` | 接口 | `CxVisual.Mechanism.Data` | 当前扫描未识别顶层公开成员（请看源码） | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs) |
| `MDSDataFileLoader` | 类 | `CxVisual.Mechanism.Data` | `public MDSDataFileLoader(Func<string, CancellationToken, Task<T>> load)`；`public Type ValueType`；`public async Task<object> LoadAsync(string absolutePath, CancellationToken cancellation)` | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs) |
| `MDSLimitedBinaryFileLoader` | 类 | `CxVisual.Mechanism.Data` | `public MDSLimitedBinaryFileLoader(long maxBytes = 64L * 1024 * 1024)`；`public Type ValueType`；`public async Task<object> LoadAsync(string absolutePath, CancellationToken cancellation)` | [源码](../../Mechanism/Data/RuntimeData/MDSDataFiles.cs) |

## 附录 A｜现有源码和记录入口

- 统一实施/验收进度：`DOCS/Implementation/CxVisual_工程呈现化体系_30分钟分轮实施与验收_V1.0.md`。
- 真实 A/B 工作包：`DOCS/Implementation/CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md`。
- A/B/C 审计：`DOCS/Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md`。
- 平台架构原则：`DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`。
- Studio 菜单与 Ribbon 依据：已核读《CxVisual_Studio_一级菜单与上下文Ribbon设计规范_V1.0.docx》；《CxVisual Studio 统一界面架构与完整设计规范 V1.1》当前只恢复到已讨论的要点，尚待完整原稿核对。
- 真实开发者范例：`Examples/VisualAuthoring/EngineeringVisualQuickStart.cs`、`Examples/EngineeringOrganization/H03ContractsQuickStart.cs`。
- 工程/属性与事务：`Mechanism/Core/Engineering/`；场景投影：`Mechanism/Visual/`；WPF：`Mechanism/Presentation.Wpf/`；Studio：`Applications/Studio/`。
- 自动化测试：`Tests/ArchitectureSmoke/OrganizationStudioTests.cs`、`Tests/ArchitectureSmoke/OrganizationH03ContractTests.cs`。
- Windows 门禁：`.github/workflows/engineering-organization-windows.yml`。

## 附录 B｜规格追踪使用规则

设计讨论以 `EPxx-yy` 引用需求；工程用户反馈以 `U1～U5` 指向场景；Studio 与工程呈现化协作引用 `§12.1～12.10`；后续全类架构每个类型标记 Owner + 需求 ID + 用例 + 证据状态，区分已定案与待核 V1.1；实现记录仍在原 30 分钟分轮文档统一维护，不复制成多套进度台账。

**本文件统一保存需求、角色/用例、总体设计及三阶段架构成果：阶段一305现有类型归属、阶段二38复用增强/20候选/12禁建及唯一Owner、阶段三20候选详细契约及15组测试矩阵。现有305类型的逐一完整重载与真实Windows/设备验收尚需源码及执行证据，不能虚报。**

