# CxVisual 工程呈现化体系 · 完整功能与30分钟分轮实施验收 V1.0

> 定案：2026-10-19。仓库：oudream/cx-visual-x3，直接master/中文少量提交，.NET Framework 4.8/WPF，既有28项目优先。分轮立项核查代码基线：cafd52a0cec1d936e79f540ed28f36cc34a14ad4；并行更新可能改变HEAD，任何结果必须使用实际提交SHA。
> 本文一处维护总功能/实施轮次/验收台账，保留用户原始EP01～EP15编号及全部覆盖范围；本表是原始V1.0需求的便于追踪的归纳，不把缩写当成删除需求。

## 1. 目标与不可突破边界

工程设计 → 可视编辑 → 配置编排 → 验证构建 → 仿真运行 → 实时观察 → 断点调试 → 故障定位 → 修改重建。

**平台原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。** 软件开发者使用简洁统一API；工程用户只通过Studio实现完整流程、无需编写底层框架代码。工程真源MEM Project/Page/Content/Symbol/Group/Element、MPM属性与Schema、原MTC事务/Undo、MOL编排、MOC构建/SourceMap、MRK执行调试、MDS运行数据唯一；跨画布仍保留同一对象身份/编排定义/运行追踪。Foundation(纯基础)、Mechanism(通用机制)、Professional(专用语义/Viewer)按既有Owner落地，不加第二套工程树/流程模型/Undo历史/运行仓库。Core/Geometry/Visual无WPF依赖；WPF/Studio仅映射和输入，CT3D三维由独立专业Viewer承担。

四工作模式：**设计**(MEM修改，禁设备现场动作)、**预览**(模拟或授权帧，不进工程历史)、**运行**(已构建计划，现场命令需授权)、**调试**(MRK真实调试接口+安全策略)。构建是工程流程，不是第五状态模型。任何运行值、状态和动画不得改变MEM Revision；无授权的设备事件必须拒绝。

## 2. EP01～EP15完整功能追踪索引

| ID | 功能域 | 范围（原始V1.0必须继续逐条验收） |
|---|---|---|
| EP01 | 工程工作区 | 工程创建/打开/关闭/保存/另存/导入导出、最近项目/未保存提醒；Project/Resource/Object树、定义/实例跳转、多页多资源多编辑器、布局保存、全局搜索、双向定位、Workbench切换、Revision/运行/构建版本。 |
| EP02 | 图形与场景 | 九Shape（Line/Rect/Circle/Ellipse/Polyline/Polygon/Arc/Bezier/Path）、Text/Image/受限安全SVG/Group/Symbol/Widget，网格/参考线/手柄；真实曲线、多轮廓、绘制顺序、裁剪、嵌套变换、高DPI/缺资源占位。 |
| EP03 | 图元精细编辑 | 创建、单/多选/框选、拖动/缩放/旋转、Arc/Bezier/Path节点及段转换、对齐分布吸附/标尺、图层锁显、Group/Symbol、剪贴板/快捷键/视口，统一MEM事务与Undo/Redo。 |
| EP04 | 统一Schema属性 | 复用MPM Descriptor/Schema自动生成类型化属性编辑器；Boolean/Number/String/Enum/Color/Brush/List/Map/对象资源引用、必填范围只读默认、混合值/多选、搜索与帮助、常量变量资源表达式、定义和实例覆盖、验证和保存重开。 |
| EP05 | 样式主题资源 | 填充/描边/渐变/透明/字体线型/渐变控制点；Theme及默认→定义→实例→状态→动画优先级；素材导入/替换/打包/依赖/缓存/失联提示及SVG外链脚本DTD禁用。 |
| EP06 | 真实Widget | 六个基础WPF控件Button/TextBox/CheckBox/Slider/ProgressBar/ComboBox，布局属性/默认值/范围/焦点/Tab/Enabled/ReadOnly/双向绑定/用户与程序事件区分；扩展Panel/Tab/List/Tree/Table/Chart/Trend/Gauge/Alarm/Dashboard及专业Viewer。 |
| EP07 | 数据绑定状态动画 | MDS及专业源的变量/表达式/资源/端口/能力输出、类型/方向/转换/默认值/质量失联/时间戳；多实例上下文，图元/Widget运行更新、状态组合优先级、动画循环反向缓动启停，不修改MEM Revision。 |
| EP08 | 事件与交互 | 鼠键/Widget/状态/生命周期、已有命令/编排/专业宿主的参数映射、实例身份/来源、执行轨迹/失败定位；设计态禁设备动作、未经授权拒绝、真实权限/联锁归专业。 |
| EP09 | 逻辑流程画布 | 同源MOL多级主/子/子子及跨专业Content；节点与能力工具箱/连接端口/类型单位/参数映射、条件循环分支并行等待重试异常、折叠导航/布局、HMI关联；画布位置不决定语义。 |
| EP10 | 构建验证 | MOC结构引用类型端口能力校验，构建入口/进度/诊断定位/SourceMap/计划摘要、版本依赖/缓存/增量/过期提示、仿真与生产区分，不冒充设备验收。 |
| EP11 | 运行仿真 | MRK运行/停止/暂停/继续/取消、实例/版本/节点状态、MDS局部堆栈端口/HMI与Widget实时投影、虚拟设备/故障注入、专业图像CT结果、并发性能异常恢复订阅释放。 |
| EP12 | 统一调试 | MRK断点/条件命中/单步进入越过跳出按真语义、调用栈/Watch/输入输出/Trace时间线、SourceMap子→父→HMI双向定位、故障后修改重建、安全权限。 |
| EP13 | 专业扩展 | HMI、Automation(PLC/轴/IO/机器人)、Vision(ROI/AI/CV)、CT3D(MPR/体绘)、Inspection(复测判定追溯)、Simulation(虚拟设备/时钟/故障重放)均复用工作区、Schema、Viewer适配而无平行工程。 |
| EP14 | 开发者SDK | 统一工程/图元/Widget/几何命中编辑、Group/Symbol/属性/样式/资源/绑定/动画/事件、事务/保存/运行只读观察/调试结果/扩展注册；Core/Geometry/Visual不依赖WPF。 |
| EP15 | 质量与安全 | 真实大场景/频繁刷新/长时运行/缓存/线程/内存句柄/多显示器DPI、恶意SVG/XML媒体/错文件/失联/表达式、可定位诊断、唯一Owner、Windows STA+TestCenter+CI+性能证据。 |

统一Studio建议：上方工程/保存/Undo/构建/运行/调试/模式/版本；左侧资源树、对象树、工具箱；中央HMI Scene/MOL Flow/专业Viewer多标签；右侧Schema属性、绑定、主题、状态、动画、事件；底部诊断/数据堆栈/断点Watch/轨迹日志/TestCenter。跨编辑器双向定位不丢对象身份。

## 3. 30分钟单轮机制与证据

每轮目标时间盒：5分钟核对最新master/源码/Owner与真实差距，18分钟完成最小纵向实现，5分钟正/反测试与回归，2分钟记录SHA、CI和接力。**时间盒不是硬性工期承诺或牺牲功能闭环理由**；Windows CI可能超出30分钟，未确认时明确写“待CI”，不得计入验收通过。若超时，拆Rxx-a/b，先保留未验收状态。

每轮固定交付七列证据：①可编译可用的开发者API，②Studio实际操作，③原MEM事务/Undo/保存重开，④MDS/MRK真帧（若相关），⑤非法输入/未授权/实例隔离反证，⑥相同SHA Windows WPF STA+TestCenter+CI，⑦真实性能/长期资源数据（若相关）；“不适用”要说明理由。

状态固定为：未开始、进行中、已提交待CI、验证失败、部分通过、验收通过、阻塞。**类已建、按钮已加、mock输出、旧SHA Success均不得写完成。**

## 4. 六道阶段门与48轮候选计划

| 阶段门 | 轮次 | 核心验收 |
|---|---|---|
| G1 | R01–R08 | 统一Schema属性与工作区/真实场景绘制，稳定工程对象身份 |
| G2 | R09–R16 | A：九Shape/Bezier/Path/多选/变换/层级/事务/保存重开/鼠键输入 |
| G3 | R17–R28 | B：Theme/Widget/MDS多实例/状态动画/授权事件/电机Symbol完整验收 |
| G4 | R29–R36 | MOL多级编排、MOC构建/版本/SourceMap、X-Ray主/AI/CV双子流 |
| G5 | R37–R44 | MRK运行/仿真/断点/Watch/Trace、错误回溯/修改重建、C场景验收 |
| G6 | R45–R48 | 专业扩展SDK、性能/安全、跨A+B+C完整联合验收 |

| 轮次 | EP | 最小闭环切片与验证重点 | 状态 | 对应SHA/CI |
|---|---|---|---|---|
| R01 | EP04 | Studio属性显示MPM类型/必填/数值范围；合法应用、越界拒绝且Revision不变 | 已提交待CI | f6c0dceb7afa（须同SHA Windows复核） |
| R02 | EP04 | 根据Schema形成Boolean与Numeric输入控件；拒绝错误类型 | 已提交待CI | f49760aa9d5c（静态结构已核对，Windows待检） |
| R03 | EP04 | 属性默认、资源来源与无效引用诊断 | 已提交待CI | c2799d713c27（等待Windows验收） |
| R04 | EP04 | 定义/实例覆盖、多选混合值/只读约束 | 未开始 | 待对应源码及Windows证据 |
| R05 | EP01 | 工作区/资源树/实例与定义往返导航 | 未开始 | 待对应源码及Windows证据 |
| R06 | EP01 | 多页/多标签/保存重开/未保存提醒/布局恢复 | 未开始 | 待对应源码及Windows证据 |
| R07 | EP02 | 九Shape精确WPF绘制和文本图片受限SVG回归 | 未开始 | 待对应源码及Windows证据 |
| R08 | EP02 | 缩放/高DPI/嵌套Group的命中、裁剪、遮挡 | 未开始 | 待对应源码及Windows证据 |
| R09 | EP03 | 真实鼠标键盘多选、框选、批量拖动原子Undo | 未开始 | 待对应源码及Windows证据 |
| R10 | EP03 | 缩放旋转与Arc/Circle/Ellipse参数控柄 | 未开始 | 待对应源码及Windows证据 |
| R11 | EP03 | Bezier节点/切线/精确段拆分转换 | 未开始 | 待对应源码及Windows证据 |
| R12 | EP03 | Path节点增删、分割、多轮廓开闭及退化路径 | 未开始 | 待对应源码及Windows证据 |
| R13 | EP03 | 对齐分布、吸附/参考线、视口与键盘操作 | 未开始 | 待对应源码及Windows证据 |
| R14 | EP03 | 锁定隐藏、层级调整、Group/Ungroup | 未开始 | 待对应源码及Windows证据 |
| R15 | EP03 | Symbol定义/实例与剪贴板/跨引用编辑 | 未开始 | 待对应源码及Windows证据 |
| R16 | EP03 | A图元场景Studio端到端/保存重开/故障反证 | 未开始 | 待对应源码及Windows证据 |
| R17 | EP05 | 样式继承与定义/实例覆盖优先级 | 未开始 | 待对应源码及Windows证据 |
| R18 | EP05 | Theme、渐变/字体与Widget外观一致 | 未开始 | 待对应源码及Windows证据 |
| R19 | EP05 | PNG/JPEG/BMP/SVG素材安全导入、恶意输入拒绝 | 未开始 | 待对应源码及Windows证据 |
| R20 | EP05 | 资源打包替换/依赖断链诊断/缓存 | 未开始 | 待对应源码及Windows证据 |
| R21 | EP06 | 六种Widget共用Schema控件与属性类型校验 | 未开始 | 待对应源码及Windows证据 |
| R22 | EP06 | Widget焦点/Tab/读写限制与程序刷新无伪事件 | 未开始 | 待对应源码及Windows证据 |
| R23 | EP07 | MDS变量/资源/表达式/端口数据源及失联诊断 | 未开始 | 待对应源码及Windows证据 |
| R24 | EP07 | Group/Content/Symbol双实例独立绑定与运行值 | 未开始 | 待对应源码及Windows证据 |
| R25 | EP07 | 状态组合与正常运行报警故障样式优先级 | 未开始 | 待对应源码及Windows证据 |
| R26 | EP07 | 动画生命周期/缓动/停止/模式切换清理 | 未开始 | 待对应源码及Windows证据 |
| R27 | EP08 | 图形/Widget事件参数、来源实例与Trace | 未开始 | 待对应源码及Windows证据 |
| R28 | EP08 | 专业授权/联锁反证与B电机Symbol完整场景 | 未开始 | 待对应源码及Windows证据 |
| R29 | EP09 | MOL节点创建移动/选区/导航与布局分离 | 未开始 | 待对应源码及Windows证据 |
| R30 | EP09 | MOL多类端口连线/断线/类型与单位约束 | 未开始 | 待对应源码及Windows证据 |
| R31 | EP09 | 条件循环分支并行等待重试/异常可视呈现 | 未开始 | 待对应源码及Windows证据 |
| R32 | EP09 | 主/子/子子、跨专业Content与多标签 | 未开始 | 待对应源码及Windows证据 |
| R33 | EP10 | 结构化构建诊断/进度/点击定位 | 未开始 | 待对应源码及Windows证据 |
| R34 | EP10 | SourceMap、工程Revision、过期及增量构建 | 未开始 | 待对应源码及Windows证据 |
| R35 | EP10 | 依赖版本/仿真生产构建、安全提示 | 未开始 | 待对应源码及Windows证据 |
| R36 | EP09,EP10 | X-Ray主流程+AI/CV双子流连线构建反证 | 未开始 | 待对应源码及Windows证据 |
| R37 | EP11 | MRK运行状态真实投影到HMI、实例版本 | 未开始 | 待对应源码及Windows证据 |
| R38 | EP11 | 堆栈/全局堆/端口/输入输出实时观察 | 未开始 | 待对应源码及Windows证据 |
| R39 | EP11 | 仿真设备、故障注入、多实例隔离 | 未开始 | 待对应源码及Windows证据 |
| R40 | EP11 | 取消超时恢复/订阅释放/性能数据 | 未开始 | 待对应源码及Windows证据 |
| R41 | EP12 | 断点条件命中/暂停继续/单步语义 | 未开始 | 待对应源码及Windows证据 |
| R42 | EP12 | Watch/调用栈/局部数据/异常定位 | 未开始 | 待对应源码及Windows证据 |
| R43 | EP12 | Trace/SourceMap子到父及HMI双向跳转 | 未开始 | 待对应源码及Windows证据 |
| R44 | EP11,EP12 | C场景出错定位→修改→构建→复运行 | 未开始 | 待对应源码及Windows证据 |
| R45 | EP13 | 专业Descriptor、Schema、Viewer共享注册 | 未开始 | 待对应源码及Windows证据 |
| R46 | EP14 | 简洁开发者API/无WPF核心真实示例 | 未开始 | 待对应源码及Windows证据 |
| R47 | EP15 | 大画布/DPI/多小时刷新/内存与非法输入 | 未开始 | 待对应源码及Windows证据 |
| R48 | EP01–EP15 | A+B+C跨场景签收，同一SHA Windows测试和总证据 | 未开始 | 待对应源码及Windows证据 |

**48轮是初始工作池，不等于48轮能保证全部完成。** 已完成能力以当前源码和CI核对，略过重复建设；未完成能力继续拆分追加轮次。每轮不能同时引入不相关Owner的大规模重构。

## 5. 真实基线与三个最终场景

已有真实实现及历史审计：`DOCS/Implementation/CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md`、`DOCS/Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md`。存在九Shape与Path操作、实际WPF Widget、部分电机Symbol双实例和X-Ray两个子流程集成及测试；**A、B、C仍全部未最终签收**。历史工程组织CI存在`moc.wiring.parameter_port_missing`，不得跳过/掩盖，必须以最新同SHA证据复核。当前工具环境不具Windows WPF编译运行能力，GitHub Actions结果须实际查询后才可写通过。

**A图元场景**：Studio创建九Shape、复杂Bezier/多轮廓Path，选中/变换/吸附/图层/分组/Undo/Redo，保存重开一致；加真实鼠键与异常退化反证。

**B工业电机Symbol场景**：Studio组合Shape/Text/Image/SVG/真实Widget，配置Theme/状态/动画/事件；同一Symbol两个实例绑定不同MDS源，运行隔离、Revision不变；设计态及未授权事件绝不操作设备，保存重开后一致。

**C X-Ray检测线**：同一工程HMI、主流程及AI/CV双子流程；Studio接端口/参数、MOC构建、MRK仿真与真实状态投影、断点单步Watch/Trace/SourceMap由故障子节点定位父流程与HMI，再编辑/重构建/重跑、保存重开。

必须A/B/C分别成功，最后跨A+B+C联合成功。生产现场设备权限/联锁验证不允许用模拟成功替代。

## 6. R01执行卡：EP04现有Schema在Studio可见、可校验

核查范围：`Mechanism/Core/Engineering/MPMValue.cs`中的`MPMPropertyDescriptor`已有`ValueKind`、`Required`、`Minimum/Maximum`及`Validate`；`Applications/Studio/StudioViewModel.Engineering.cs`的`StudioPropertyItem`和`Row`尚未完整暴露Schema提示，`Applications/Studio/MainWindow.xaml`当前用通用TextBox。已有`Tests/ArchitectureSmoke/OrganizationStudioTests.cs`中真实Symbol两个实例属性操作回归。

本轮仅做小纵向切片：**将既有Schema类型/必填/范围显示在属性行，不增第二Schema**；现有MEM提交原样负责验证；新增WPF STA实用例验证合法值生效、越界值被拒且Revision及实例值不变，另一个Symbol实例不受影响。对象属性元信息只在UI投影，不持久化第二份数据。

R01源码已提交（截止 f6c0dceb7afae75d320f6b442f3a152b11566162）：修改StudioViewModel.Engineering.cs，仅把现有MPM Descriptor投影为属性行Schema提示；MainWindow.xaml以实际WPF TextBlock显示元信息；OrganizationStudioTests.cs在原双Symbol实例测试上增加类型/范围UI展示、越界拒绝不改Revision及实例值、错误反馈的STA断言。未创建平行Schema/工程数据仓库，未改生产MPM验证机制。**验收状态：已提交待CI**。当前工具仅验证源码文件存在及关键绑定字符串，未能执行Windows MSBuild/WPF STA；该SHA的GitHub合并状态与PR触发运行记录查询均为空，不能当作通过。

## 7. 滚动轮次记录（不要另起几十份阶段日记）

| 轮次 | 当前状态 | 实际SHA | 真实变更与证据 | 未通过或待办 |
|---|---|---|---|---|
| R01 | 已提交待CI | f6c0dceb7afa | Studio属性行/真实WPF提示/双实例越界回归代码已提交；仅静态核查源文件已落仓 | Windows MSBuild、OrganizationStudioTests STA、对应SHA CI均待实际执行 |
| R02 | 已提交待CI | f49760aa9d5c | StudioPropertyItem的INotifyPropertyChanged、Boolean/Number/Integer/Decimal类型投影及格式/范围提示；WPF CheckBox与Numeric TextBox随来源动态切换；OrganizationStudioTests新增非法格式拒绝、Boolean双实例、Undo/Redo、保存重开与变量/常量切换用例。当前已对比GitHub实际3处源码并做XAML标记栈静态核查 | 未有实际Windows构建、WPF STA或同SHA完整CI通过报告；公共XAML结构检查不等于WPF编译，现场设备与外部鼠标注入不适用本轮 |
| R03 | 已提交待CI | c2799d713c27 | 增加属性独立资源选择、Schema默认恢复、实例覆盖清除及输入提示；WPF回归代码覆盖有效资产、跨工程无效资产、两实例隔离、Undo/Redo、保存重开；源码和XAML结构已核查 | Windows编译、WPF STA、TestCenter及最新同SHA CI均待核实；本轮不能标记验收通过 |

R02状态说明：代码和测试均已提交，不能称“验收通过”。适配当前GitHub连接器的CI查询仅能读综合Commit Status与PR触发运行，两者在`f49760aa9d5c`返回空；这**不能证明**没有push-triggered的Actions，更不能冒称成功。需要在GitHub Actions中以HEAD/完整SHA核对工程组织Windows Acceptance、TestCenter、统一Runtime和Config，再补实测结论。若旧R01测试在新版HEAD失败，先修正R01/R02再开始R03，不禁用任何测试。下一任务：R03（默认值、资源来源选择、无效引用反证）。
