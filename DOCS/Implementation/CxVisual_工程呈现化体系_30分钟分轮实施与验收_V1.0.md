# CxVisual 工程呈现化体系 · 完整功能与30分钟分轮实施验收 V1.0

> 定案：2026-10-19。仓库：oudream/cx-visual-x3，直接master/中文少量提交，.NET Framework 4.8/WPF，既有28项目优先。分轮立项核查代码基线：cafd52a0cec1d936e79f540ed28f36cc34a14ad4；并行更新可能改变HEAD，任何结果必须使用实际提交SHA。
> 本文一处维护总功能/实施轮次/验收台账，保留用户原始EP01～EP15编号及全部覆盖范围；本表是原始V1.0需求的便于追踪的归纳，不把缩写当成删除需求。
> **2026-10-21 已确定架构基线**：`DOCS/Architecture/CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md`，其中EP01～EP15、六角色/五案例、阶段一305型归属、阶段二复用增强与必要增构、阶段三协同契约与测试矩阵均为后续实现的目标依据；以当前master源码与真实CI判定实际状态。

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
| R04 | EP04 | 定义/实例覆盖、多选混合值/只读约束 | 已提交待CI，范围部分闭环 | 45ff4d1eecc（同定义实例批量、MPM原生ReadOnly；待Windows验证） |
| R05 | EP01 | 工作区/资源树/实例与定义往返导航 | 部分通过：导航切片同SHA Windows/STA/TestCenter已过 | 56a7a467；[Studio 13 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/39527839595)；[工程WPF回归](https://github.com/oudream/cx-visual-x3/actions/runs/39527839579)；[TestCenter通过](https://github.com/oudream/cx-visual-x3/actions/runs/39527839575) |
| R06 | EP01 | 多页/多标签/保存重开/未保存提醒/布局恢复 | **主要纵向功能已验证：Page/Content/Symbol/Flow真实Tab、独立画布、单MEM历史、资源Dirty/引用影响的Undo/Redo反证** | [R06修复a4e94349](https://github.com/oudream/cx-visual-x3/commit/a4e943493e0748b5e8b357dcd1d08a5371f3bfe3)，[Studio 17/17 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491)，[工程Windows PASS](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541)。编辑器完整命令/快捷键、人工键鼠、DPI与专业Viewer尚待签收 |
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
| R04 | 已提交待CI，范围部分闭环 | 45ff4d1eecc | 同一Content/Symbol定义的多个引用实例通过MPM公共Schema投影共同属性；混合值与Boolean三态、一个MEM事务更新/撤销重做、批量清除覆盖、过期选区拒绝；跨定义/图元混选不降级成最后实例编辑。新增原生MPMPropertyDescriptor.IsReadOnly（旧构造兼容），MTC拒绝设置定义/实例覆盖；两条工程XML持久化、类型Schema比较、工程资源转移同步该标记。Studio按只读禁用。ArchitectureSmoke新增纯Core和WPF STA反证（未经执行）。 | 仍未获得R01～R04同SHA Windows构建/STA/CI验证。跨定义兼容Schema的批量交集、多选性能和真实外部鼠标验证待追加轮次；若有CI失败先修复。 |
| R05 | 部分通过：局部导航Windows+TestCenter均通过 | 56a7a4674be0bb78a064136e4073d1bd319ed159 | `CxVisualEditor` 的NavFrame保存真实资源及被选实例身份、剔除已删除对象；Studio ViewModel同步恢复；左资源树提供返回入口；Studio Shell测试新增13号“R05 定义与实例往返恢复及失效选择保护”。[S1 Windows CI 39527839595](https://github.com/oudream/cx-visual-x3/actions/runs/39527839595) **success**：2026-10-21日志13 PASS、0 FAIL；[工程组织CI 39527839579](https://github.com/oudream/cx-visual-x3/actions/runs/39527839579) **success**：Windows x64 MSBuild、ArchitectureSmoke --organization-wpf、benchmark和开发者示例均通过；配置两工作流同SHA success；[TestCenter Windows 39527839575](https://github.com/oudream/cx-visual-x3/actions/runs/39527839575) **success**，Windows NUnit/Worker/TestCenter验证成功。 | 全资源任意Tab及布局恢复/未保存提示属R06；跨MOL/SourceMap定位属R32/R43；实机UI手工及性能未验；不能因本轮五条Windows工作流通过就冒称全EP01或A/B/C已签收 |

**2026-10-21 CI事实复核（更新旧结论）**：从 GitHub Actions `push` 运行记录取到了实际证据：集成提交 `0eb5cf69a250cd3c74aa3268de68deef066948ea` 的 [工程组织Windows工作流](https://github.com/oudream/cx-visual-x3/actions/runs/39525077202) **success**，日志显示 Windows 2022 / VS2022 x64 MSBuild、ArchitectureSmoke、`--organization-wpf`、`--organization-benchmark`、开发者示例编译运行成功，且 `PASS H06 Studio real WPF automation` 包含页面、双实例、定义导航、Undo/Redo、保存重开；同SHA的 [Studio S1工作流](https://github.com/oudream/cx-visual-x3/actions/runs/39525077173) **success**，12个Studio Shell STA自测通过。该SHA是在R01～R04源码提交**之后**的集成回归版本，证明其合入代码通过该集成检查；**不是四个原提交各自独立的同SHA CI验收，也不能替代R04跨定义批量、真实设备安全/性能、A/B/C端到端签收**。R01～R04继续记“部分闭环/集成回归已过、具体功能剩余待测”，不得再说完全没有Windows证据。R05提交 `56a7a4674be0bb78a064136e4073d1bd319ed159` 的工程组织/Studio Shell及两项配置Windows CI已经有明确**success**；第13项实际WPF STA自测PASS，错误原实例选择/删除返回两条路径均有反证。[TestCenter Windows 39527839575](https://github.com/oudream/cx-visual-x3/actions/runs/39527839575) 现已 **success**（同SHA `56a7a467`），与工程组织、Studio Shell及两项配置Windows工作流均有真实通过记录。R05最小导航切片Windows门禁已绿；跨专业多资源Tab、独立人工鼠标、DPI/长时间性能及A/B/C最终验收不在本轮签收范围。

### P0-1与P0-2两个工作包的源码和CI汇总

- P0-1：`Mechanism.Core.AnalyzeResourceImpact`、`PreviewRetarget`，复用现有MEM引用索引、资源依赖及MTC可逆预演，不创建独立资源图；两个提交`944a0359`、`9f9d464e`；[工程Windows 39549728186](https://github.com/oudream/cx-visual-x3/actions/runs/39549728186) **success**，包含P0-1影响链和H03契约回归。
- P0-2：`StudioShell.ResourceDocuments.cs`在原Studio Shell中复用ViewModel/MEM及Visual；Page/Content/Symbol各有独立SceneView，主/AI/CV子流程各有独立MOLFlowCanvas；资源树打开、图形结构选择、布局白名单及工程切换释放一并接通。提交`cdb6d382`、`6d722021`和`e06c2f26`；[Studio Windows 39549457531](https://github.com/oudream/cx-visual-x3/actions/runs/39549457531) **16 PASS / 0 FAIL**、[工程组织 39549457123](https://github.com/oudream/cx-visual-x3/actions/runs/39549457123) **success**。
- 边界：以上为 **Windows自动测试可复现的功能完成**，不是人工高DPI/实机或完整专业跨场景的验收证明。计划版本变化后的MOC重建、复杂资源/子流程完整快捷操作与每资源Dirty提示，继续按Owner补强；不能在Studio另建MEM/MOC机制。

### R06 页面型多文档的真实证据与边界（2026-10-21）

- **对应代码**：Applications/Studio/StudioShell.ResourceDocuments.cs，以及 StudioShell、资源树、图形结构树衔接。每个MEMPage一个真实CxVisualEditor与SceneView，画布选择、缩放独立，编辑事务、Revision及Undo/Redo全部共用同一个MEMEngineeringSession。
- **正反用例**：页面A/B不同Tab及场景实例；真正新增MRCRectangle只改变A；布局可保存/恢复，跨工程或已删除页面ID拒绝；关闭重新打开资源文档无重复；保存工程重开后关闭旧页面Tab，同时原doc.flow仍然激活。
- **同SHA证据**：[773450c7](https://github.com/oudream/cx-visual-x3/commit/773450c7bd76c9c80f6cadfa8876c68c14e6fe09)；[Studio S1 Windows 39533807317](https://github.com/oudream/cx-visual-x3/actions/runs/39533807317) **success，14/14 STA**；[Engineering Windows 39533807345](https://github.com/oudream/cx-visual-x3/actions/runs/39533807345) **success**，Windows x64编译、工程WPF与开发者示例成功。
- **已追加验收**：Content、Symbol、MOL流程资源独立多Tab及变更/引用影响Dirty在后续P0-2回归中已有Windows证据（详见下文）；九Shape在资源Tab的完整精细编辑体验、长期键鼠/多屏DPI、完整未保存提醒键鼠验收与TestCenter同SHA仍未最终完成。R06为阶段性闭环，不能宣称EP01整体完成。
- **R06追加修复实测**：动态Ribbon调用现有Studio ViewModel编辑能力时先同步当前活动页面Tab对应的MEMPage，并在资源失效时拒绝写入；关闭Tab保证AvalonDock中央Document与Ribbon上下文同步；切换工程先保存关闭前活动Document身份，避免关闭事件抢焦点。本次新增“活动A/检视B/点击圆”跨页原子编辑反证、关闭Tab上下文断言以及从活动动态Tab保存重载工程反证。原[24d3c37](https://github.com/oudream/cx-visual-x3/commit/24d3c37f5f40a234fee2fa2cf5eb9b62013f4a6a)的S1 CI确因新发现的活动文档焦点竞态失败，随后修复在[4770a698](https://github.com/oudream/cx-visual-x3/commit/4770a698b1f89324769914ca8e09bec3af7eb757)同SHA的[Studio S1 39548325060](https://github.com/oudream/cx-visual-x3/actions/runs/39548325060)与[工程组织 39548325067](https://github.com/oudream/cx-visual-x3/actions/runs/39548325067)均真实success。新增命令Guard适用于动态Ribbon；既有画布旧操作与人工键鼠链仍需分别核实，不声称全量HMI编辑器合并完成。

### S1～S4规划与P0-Dirty-Undo最新闭环（2026-10-21）

- 规划入口：[Studio S1～S4阶段开发与真实验收总规划](CxVisual_Studio_S1至S4_阶段开发与真实验收总规划_V1.0.md)。继续按S1 Shell、S2配置命令、S3图元和MOL流程、S4运行调试与专业集成推进，同时保留本表EP01～EP15、R01～R48、G1～G6，不为每阶段另建一套R编号。
- 最新实际源码SHA [a4e94349](https://github.com/oudream/cx-visual-x3/commit/a4e943493e0748b5e8b357dcd1d08a5371f3bfe3) 通过 [Studio S1 Windows 39553816491](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491) **17/17 WPF STA success** 与 [Engineering Organization Windows 39553816541](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541) **success**。保存后Undo删除新增图元及Redo对应Tab直接Dirty、引用影响与无关Tab隔离均由同SHA自动测试证明；人工场景仍待。


### P0-1～P0-4 实际进度补充（2026-10-21，不能替代EP全域验收）

| 优先级 | 当前实现与Owner | 实测证据 | 下一步缺口 |
|---|---|---|---|
| P0-1 工程组织 | **主要代码闭环**：MEM现有ResourceEntry推导直接/传递依赖影响、Path和Revision；复用MTC事务预演共享定义兼容性、错误回滚。无需第二依赖图 | [工程组织Windows 39549728186](https://github.com/oudream/cx-visual-x3/actions/runs/39549728186) success；源码`944a0359`、`9f9d464e` | MOC已构建计划/SourceMap真实版本失效联动、专业Provider版本与真实设备 |
| P0-2 统一资源编辑 | **主要代码闭环**：Page/Content/Symbol各有CxVisualEditor+SceneView，Content独立MOLFlowCanvas；资源导航、Dirty/间接受影响提示、关闭重开及布局恢复，共用MEM事务和历史 | [Studio Windows 39553816491](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491) 17/17 STA、[工程Windows 39553816541](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541) success；源码`a4e94349` | 九Shape完整精细编辑、专业Viewer、复杂快捷键、高DPI、多小时/手工验证 |
| P0-3 统一Schema | **持续实现，未全签收**：资源树与真实资源Tab不再局限选中单图元；Content/Symbol和Page内嵌Content直接投影原MPM Schema，编辑复用ApplyPropertyCommand/MTC校验、Undo/Redo；不同资源旧属性行拒绝 | [Studio Windows 39556251854](https://github.com/oudream/cx-visual-x3/actions/runs/39556251854) 18/18 STA（`cf2b0209`）；[Studio Windows 39556665984](https://github.com/oudream/cx-visual-x3/actions/runs/39556665984) 19/19 STA（`50e07137`） | 实际Vision/CT专业Provider Schema与配置集合适配、统一CommandId多入口/权限/异常、复杂属性类型 |
| P0-4 X-Ray跨流程 | **首个定位切片通过，整体未验收**：保留父流程调用节点定位，新增“跳转子流程源”，由MRK轨迹ContentId/BlockId仅在MEM实际存在、MOL V2实际包含Block时打开源资源Tab并选中Block；错误/删除目标拒绝，导航不增加MEM Revision | [Studio Windows 39557455651](https://github.com/oudream/cx-visual-x3/actions/runs/39557455651) **20/20 STA**（`2a6a31c3`），[源码`1e246bca`](https://github.com/oudream/cx-visual-x3/commit/1e246bca0184ad46a82a112871bce3f3ddcc12b1) | 使用真实MOC SourceMap+MRK完整执行轨迹做主/AI/CV/诊断→源节点→HMI→修改重构建端到端闭环；设备与权限实测 |
| P1 后续深化 | 专业底层已有能力，尚无本工作包最终实机/负载验收 | 不以类存在替代通过 | CT3D Viewer、跨工程模板、长期资源/内存与数据观察 |

**同SHA门禁必须区分**：`2a6a31c3` 的 [Studio Windows 39557455651](https://github.com/oudream/cx-visual-x3/actions/runs/39557455651) 20项STA和Windows x64编译成功；同SHA [工程组织Windows 39557455627](https://github.com/oudream/cx-visual-x3/actions/runs/39557455627) 因Foundation同步日志跨进程占用(`System.IO.IOException`)失败，**仓库全CI不能声称绿**。该日志并发故障由Foundation原Owner独立修复/复验，本表保留失败事实，不冒充P0-4功能失败或全平台通过。之后的集成源码继续以各自最新同SHA记录为准。

下一步顺序：先按最新master核实上述失败是否已修复；继续P0-3真实专业Schema+统一命令的跨资源入口；贯通P0-4 X-Ray完整构建→模拟运行→Trace/SourceMap定位→修改重构建；最后联合复核P0-1影响闭包与MOC计划版本。无真实生产设备权限及联锁验证时，只能签仿真/工程测试，不签设备现场验收。

### P0-4 运行轨迹与工程版本一致性增量（2026-10-21，待本轮同SHA复核）

- Studio消费现有MOC已编译计划的 `ProjectId`、`EngineeringRevision`，并随现有MRK Trace行携带，不另建Plan/SourceMap/版本数据仓库。
- “跳转子流程源”在定位前校验当前MEM `Project.Id` 和 `Revision`。跨工程、过期Revision拒绝导航并提示重新构建运行；有效版本仍按原MEM Content和MOL V2 Block定位。旧 `LocateStudioFlowSource(Guid,Guid)` 保留兼容。
- WPF STA新增独立正/反测试：当前版本、外来工程、未来版本、修改工程后旧Plan版本，检查非法导航不切换Tab；Studio CI断言从20提高到21。这里只记录**源码实施目标**；编译与STA结果以该轮真实commit和Windows Actions为准，未执行之前不宣称通过。

### P0-4 构建诊断从主流程进入原始子流程（2026-10-21，待该轮Windows验收）

- 直接消费既有 `MOCBuildResult.Diagnostics` 的 `ContentId/BlockId`，连同本次MOC构建所用不可变Snapshot的ProjectId/Revision投影到Studio诊断行；不创建诊断数据真源。
- “定位诊断”保留主流程原有MOL选中路径；当诊断指向其他Content且其MOL V2定义实际包含原Block时，使用同一资源Tab下钻；跨工程或过期构建诊断拒绝错跳并要求重新构建。
- WPF STA新增跨Content诊断导航、Revision不变和过期诊断拒绝测试（总22项）。这仅是有具体BlockId诊断的UI定位增量；当前MOC某些深层编译异常只返回根Content级错误，无原子块来源时不能虚称能够定位子流程节点。CI证据须按实际提交对应run记录。

### P0-4 当日Windows证据补记（2026-10-21）

- [19b9cf53](https://github.com/oudream/cx-visual-x3/commit/19b9cf53e48e200f0fb9c00fe588abdfad852cbe)：MRK来源行携带MOC Plan工程身份与Revision，拒绝过期源定位。[Studio Windows 39562875531](https://github.com/oudream/cx-visual-x3/actions/runs/39562875531) **success，21/21 STA，Windows x64编译及Owner依赖检查通过**；同SHA工程组织[39562875409](https://github.com/oudream/cx-visual-x3/actions/runs/39562875409)、配置L1[39562875413](https://github.com/oudream/cx-visual-x3/actions/runs/39562875413)、配置公共底座[39562875500](https://github.com/oudream/cx-visual-x3/actions/runs/39562875500)均为**success**。
- [e56b91b9](https://github.com/oudream/cx-visual-x3/commit/e56b91b94fbe10060ad155bfd1921f6fc9f3c027)：MOC构建诊断带原ContentId/BlockId和Snapshot版本，主流程保留原定位，子流程从已有Tab下钻。初次Windows[Studio 39563392658](https://github.com/oudream/cx-visual-x3/actions/runs/39563392658)为**failure**，原因是新增STA测试同名变量`CS0136`，不宣称当时通过。
- [b6b43779](https://github.com/oudream/cx-visual-x3/commit/b6b43779173f3d8ec227c798bdab8d82c0c2beb5)：修复`CS0136`且保留全部测试。[Studio Windows 39563632288](https://github.com/oudream/cx-visual-x3/actions/runs/39563632288) **success，22/22 WPF STA，0 FAIL，Windows x64统一解决方案编译及Owner依赖检查通过**。同SHA其他工作流以各自最终状态为准。
- 这些是P0-4的Studio自动验证切片，不等于完整X-Ray主/AI/CV流程执行、Trace+SourceMap所有路径、发布授权、实体设备或人工DPI验收。并行新master仍需各自同SHA验证。

### P0-S1/S2 统一资源Tab真实图元命令验收（2026-10-21）

- 当前已提交且经Windows验收：[de3a024b](https://github.com/oudream/cx-visual-x3/commit/de3a024b4198e5be2dada7de899f7e5cf566afcc)；[Studio S1 #39564306075](https://github.com/oudream/cx-visual-x3/actions/runs/39564306075) **23/23 WPF STA success**，[工程组织 #39564306081](https://github.com/oudream/cx-visual-x3/actions/runs/39564306081) **success**。补真实Page/Content/Symbol绘图Ribbon与旧工具箱到各独立`CxVisualEditor`，九Shape/Text/Widget只通过MEM事务，保留旧R06活动页面上下文；新增跨资源误写、撤销重做、流程Tab拒绝和Tab复用反证。不是完整EP02/03签收；专业Editor注册、对齐/路径工具、人工鼠键及性能继续开发。

### P0-4 X-Ray真实MOC/MRK来源到Studio联动开发（2026-10-21，待CI）

- 使用Studio现有“创建完整X-Ray双子流程”生成真实MEM主/子Content和HMI，不重造演示工程真源，取原MOCBuildService不可变Plan及CPU仿真Resolver，MRKExecutionEngine执行并用MOCCompiledPlan.ReadTrace读取真实AI/CV子流程执行记录。
- Studio运行Trace行投影统一复用MainWindow.ProjectFlowTrace(MOCCompiledPlan,MOCObservedStep)，同一编译版ProjectId/Revision/ContentId/BlockId/CallPath用于父流程、子流程及HMI定位；父流程与关联画面导航也拒绝旧工程/过期Revision，防止越过先前只对“跳转子流程源”设置的版本保护。
- WPF STA新增真实MOC/MRK成功轨迹→主流程调用→AI子流程源→HMI→MEM版本变更拒绝→重新构建的贯通测试，23项扩展为24项。没有实机I/O，也不宣称设备安全和所有故障路径已验收；同SHA CI通过前均记为源码待验收。

### P0-4 AI真实MRK失败反证及工程保存重构建（2026-10-21，待CI）

- 基于上轮同一个真实X-Ray主/AI/CV工程，MOC构建时把AI Provider替换为确定抛出异常的CPU能力实现（只在STA测试中），不连接真实设备、不修改工程逻辑或另建引擎。
- 通过MRKExecutionEngine真实运行失败Plan、MOCCompiledPlan.ReadTrace定位AI Content/Block的Failed转移，反证失败状态、错误传播、无成功输出泄漏和MEM Revision不变；Studio现有“跳转子流程源”从失败观测来源直接打开AI流程Tab选中原Block。
- 保存重开后确认AI定义、Block身份和MOC SourceMap可重新构建。本轮新增第25项WPF STA反证；CI的相同SHA验收证据和全量设备验证必须另行记录。

### P0-4 真实X-Ray正常/失败执行Windows同SHA证据（2026-10-21）

- [98604b44](https://github.com/oudream/cx-visual-x3/commit/98604b4488bce19028b5045739b2ef121a0b69df)把现有主/AI/CV真实资源示例走通MOC→MRK，再用真实ReadTrace投影到Studio父流程调用点/源子流程MOL Tab/HMI，并对父流程与画面导航同样执行Plan ProjectId/Revision版本校验。首次构建发现不可变MRIReferenceSnapshot误写为可编辑MRIResourceReference，引起CS0029，并非运行语义故障。
- [2b4ab62b](https://github.com/oudream/cx-visual-x3/commit/2b4ab62b17ce4332d21756420ba945cb5aef7814) 修复上述测试类型。真实[Studio Windows 39565169101](https://github.com/oudream/cx-visual-x3/actions/runs/39565169101) **success，24/24 WPF STA，0 FAIL**，真实CPU执行AI/CV和SourceMap/父子流程/画面定位、旧版本拒绝及MOC再构建均PASS；同SHA[工程组织39565169043](https://github.com/oudream/cx-visual-x3/actions/runs/39565169043)及[配置公共底座39565169105](https://github.com/oudream/cx-visual-x3/actions/runs/39565169105)均success。
- [f0d2ce56](https://github.com/oudream/cx-visual-x3/commit/f0d2ce56076f78ef93b4559aa97f5d4e525901ed)：对相同真实X-Ray工程提供只在测试中的AI CPU能力失败注入，MRK真实失败、Trace定位原AI Content/Block、无成功输出泄漏、Revision不变、保存重开后编译/SourceMap身份恢复。[Studio Windows 39565607220](https://github.com/oudream/cx-visual-x3/actions/runs/39565607220) **success，25/25 STA，0 FAIL**；[工程组织39565607298](https://github.com/oudream/cx-visual-x3/actions/runs/39565607298)与[配置公共底座39565607271](https://github.com/oudream/cx-visual-x3/actions/runs/39565607271)亦success，配置L1[39565607209](https://github.com/oudream/cx-visual-x3/actions/runs/39565607209)在落文时仍在执行，不先写通过。
- 保留失败事实及边界：上述为CPU仿真+WPF STA与工程保存重建，非多实例设备调度、全部异常恢复、生产硬件联锁、高DPI人工交互、长时间运行或整体EP01～EP15签收。并行开发如提交新Studio功能，需重新核验其自身HEAD的Windows CI。

### Studio S1/S2 P0第二轮：多选排列/图层Command（2026-10-21）

- 实际源码：[a8022b66](https://github.com/oudream/cx-visual-x3/commit/a8022b66975821da39b472baa1e4582e6dc07706)。Page/Content/Symbol活动Tab的真实Scene多选调用原`CxVisualEditor.Align`一次MEM事务；Z序调用`SetZOrder`；原固定图元Editor路径不变，流程/失效文档拒绝图元命令。并行P0-4 X-Ray真实执行及失败追踪测试完整保留。
- **同SHA测试**：[Studio Windows 39565771432](https://github.com/oudream/cx-visual-x3/actions/runs/39565771432) **26/26 WPF STA成功**；[Engineering Windows 39565771340](https://github.com/oudream/cx-visual-x3/actions/runs/39565771340) **success**；反证涵盖仅单选拒绝、跨资源误写、Undo/Redo、Z序及流程Tab拒绝。完整几何/交互/人工DPI仍待下一轮。

### Studio P0统一Resource Editor/Command收敛（2026-10-21）

- 最新代码[c696905c](https://github.com/oudream/cx-visual-x3/commit/c696905cc0418b13030a32ebeedafb1defcbdd7a) 对应[Studio Windows 39568829529](https://github.com/oudream/cx-visual-x3/actions/runs/39568829529) **31/31 WPF STA success**、[工程组织39568829447](https://github.com/oudream/cx-visual-x3/actions/runs/39568829447)success。实装四个资源Tab的几何/路径/多选/子流程主命令、属性防串写及Tab浮动/关闭释放的自动化切片。真实操作尚需另行Windows桌面人工记录；详细交叉矩阵统一维护在[Studio S1～S4主规划§15](CxVisual_Studio_S1至S4_阶段开发与真实验收总规划_V1.0.md)。

### P0-4 跨页面主流程恢复与MRK双Flow实例轨迹身份（2026-10-21，待CI）

- Studio的MRK轨迹UI投影补充实际执行入口RootContentId与FlowId（真实MRKFlowHandle），无需新增工程/计划真源。运行时由当前MOC Plan + MRK流实例投影；既有内部旧调用保持兼容。
- 从Trace定位父Invoke或HMI时，若携带根Content身份，先验证MEM工程中真实Page，基于该页面的MOL定义定位，避免当前活动页面变更造成错跳；非法根身份拒绝。SourceMap原Block/CallPath及项目Revision校验仍保留。
- Windows WPF STA增加两条真实MRKFlowHandle并发执行的来源隔离、换页后恢复主调用与HMI、无效根拒绝及导航不变更MEM Revision反证。原S1用例数量由31提高为32；同SHA CI通过前仅标记源码已提交，人工高DPI/设备仍未验。

### P0-4 跨页面双Flow真实Windows结果补录（2026-10-21）

- [102579e1](https://github.com/oudream/cx-visual-x3/commit/102579e183d2f4a4033c43272994708a8b9898ca)：Studio Trace投影新增来自MRKFlowHandle的FlowId与本次主MOC运行RootContentId；父Invoke及关联HMI导航先恢复有效根MEM Page并按原MOL定义定位，不因用户切到另一页面而误跳。首次同SHA Windows发现新测试的局部`revision`命名与外层冲突：`CS0136`，记录失败、不冒充通过。
- [17324d18](https://github.com/oudream/cx-visual-x3/commit/17324d184a0fa765ea9497a741092ef26a9c7fc1)修复测试变量名；但并行R08的F19提交暴露`FCRWorker.Job`访问泛型`Handle`导致`CS1061`，统一构建再次失败。工程组织与Studio这时不能签收。
- [c1410705](https://github.com/oudream/cx-visual-x3/commit/c1410705511713c12594fa15871322adec10260c)：遵守F19原Owner，在非泛型Job抽象添加MarkAccepted并由泛型Job<T>实现，修复异步准入编译，不重建队列。该SHA的[Studio Windows #39576040381](https://github.com/oudream/cx-visual-x3/actions/runs/39576040381) **success，32/32 WPF STA，0 FAIL，Windows x64统一解决方案编译及Owner依赖检查均通过**。新增真实双MRK Flow同Plan运行、不同FlowId、跨页面父Invoke/HMI恢复、无效根身份拒绝、工程Revision不变均PASS；同SHA[Unified Runtime TDD #39576040354](https://github.com/oudream/cx-visual-x3/actions/runs/39576040354) **success**，配置公共底座[39576040765](https://github.com/oudream/cx-visual-x3/actions/runs/39576040765) **success**。Config L1和TestCenter同SHA在本条写入时仍有运行，需各自核验。
- 此闭环只证明CPU仿真+WPF STA的身份导航与隔离，不证明专业Provider发布版本门禁、真实硬件联锁、多监视端或人工长期DPI验收。并行新master若扩展Studio功能，其新SHA需重新验收。

### P0-4 最新并行33项STA集成复核（2026-10-21）

- 在[修复提交c1410705](https://github.com/oudream/cx-visual-x3/commit/c1410705511713c12594fa15871322adec10260c)同SHA下，Config L1 [39576040321](https://github.com/oudream/cx-visual-x3/actions/runs/39576040321)最终 **success**，与Studio 32/32、Unified Runtime TDD、配置公共底座同SHA全部成功；TestCenter[39576040522](https://github.com/oudream/cx-visual-x3/actions/runs/39576040522)在此更新时仍跑并行重复测试，不提前签收。
- 并行P0主编辑器提交[a78a89f1](https://github.com/oudream/cx-visual-x3/commit/a78a89f1a28b1ea046bbab92ca5d96a02c71ebe1)进一步增加活动Content动态绑定/校验命令反证；[Studio S1 Windows 39576219329](https://github.com/oudream/cx-visual-x3/actions/runs/39576219329) **33/33 WPF STA success、0 FAIL**，保留并再次实测本轮P0-4双Flow跨页面来源验证；同SHA[Engineering 39576219487](https://github.com/oudream/cx-visual-x3/actions/runs/39576219487)、[配置公共底座39576219204](https://github.com/oudream/cx-visual-x3/actions/runs/39576219204)亦success，其余工作流以最终结果为准。
- 下一步仍是专业Provider版本/构建发布交接、复杂多实例故障可观察性、真实设备安全联锁，以及Windows桌面高DPI和长期负载的非自动验收。不得将WPF STA变绿误写成P0-4全生产签收。

### 连续收敛：P0-4 Vision CPU专业能力版本→MOC/授权MRK软件发布（2026-10-21，待本轮同SHA验收）

- **专业Owner**：`CxVisionThresholdProvider`公布只读`ConfigurationRevision`、`ReleaseVersion`（算法合同版本+配置Revision，非硬件/AI模型版本），Host必须从该Provider取得标识并声明给原`MOCProjectRelease`。G06真实像素链路贯通Prepare、Version锁定、MRK授权Stage/Activate、Slot Plan执行、错误主体/安全点拒绝与配置升级失效反证，不增新Publisher。
- **Studio CPU仿真**：在原MOL工具栏新增“准备CPU软件发布”，使用既有`MOCProjectRelease.Prepare`锁定主/AI/CV可达子Content及模拟能力版本；运行此候选时经`MOCProjectRelease.Stage`和受保护的`MRKExecutionPlanPublisher/PlanSlot`授权激活才执行，过期Revision/能力版本拒绝且不得悄悄降级为普通运行。Studio本地模拟策略只承认`studio-cpu-simulation`，**绝不用于生产设备授权/联锁**。轨迹补软件候选指纹与发布修订号，原Flow/根Content身份不变。
- **验收**：原Studio STA总数34→35，新增真实X-Ray Prepare、未授权、Host否决、模拟AI版本变化、工程Revision变更、重新构建发布反证。原G06集成测试追加真实Vision Provider版本锁定→执行。仅在Windows CI证实后计为自动化通过；真正设备版本/发布ACK、高DPI/长时/现场安全未签收。

### P0-4 CPU软件发布与Studio真实仿真执行闭环（2026-10-21，待同SHA CI）

- 继续复用上轮`MOCProjectRelease.Prepare/Stage`与MRK授权`PlanSlot`；Studio执行前补`EntryContentId`严格匹配活动主流程页面，防止同工程内切页后用原候选执行另一个Root。
- 真实WPF STA测试直接触发Studio“准备CPU软件发布”事件和`OnRunLogicDemo`，等待MRK CPU异步运行完成，核对FlowTrace实际`SoftwareReleaseFingerprint/PublicationRevision/FlowId/RootContentId`、成功候选清理；同时测试跨Root错用拒绝和工程Revision变化后不隐式降级运行。原测试数量36→37，提交前不冒充CI通过。
- 本轮仍是无设备CPU仿真软件发布，不代表生产PLC/AI硬件、实际设备Release ACK或硬件安全点签收。

### P0-4 Provider真实版本自证与Host发布声明一致性（2026-10-21，待同SHA CI）

- Mechanism.Runtime在原`IMRKCapabilityResolver`旁增加**可选**`IMRKVersionedCapabilityResolver.TryGetCapabilityVersion`，只提供专业Provider版本查询，不改变MRK执行器。专业Vision阈值Provider返回基于配置Revision的`ReleaseVersion`，Studio本地CPU模拟Resolver返回其真实模拟能力版本。
- MOC原`Prepare/Stage`递归可达Content的发布指纹计算时，对支持自证的Provider核对Host声明，发现版本不相等拒绝`moc.release.provider_version_mismatch`，不进入标准MRK PlanSlot；旧未实现接口的外部Provider仍可沿用Host声明，但**不等于已自证**。
- G06在真正CPU图像检测+MRK执行+发布授权测试中增加Host伪版本声明拒绝、Provider配置Rev12→Rev13但Host漏更新拒绝，保证未污染已激活计划。无真实设备配置ACK/跨进程插件签名和机台安全联锁验收，不将此可选软件契约误报为生产级完整自证。

### P0 Flow Port收敛与P1双专业MHS中央编辑（2026-10-21）

- **P0自动代码收口**：提交[b2791c5](https://github.com/oudream/cx-visual-x3/commit/b2791c5ef64014d5b66be18f9841cb45a486d016)将独立主/AI/CV Flow的Port属性、选中节点与`MOCFlowBuilder.Edit`统一由当前Document ContentId决定，不再由旧页面`ActivePage`决定，跨Tab错误编辑拒绝且支持MEM Undo/Redo。[Studio 39579075529](https://github.com/oudream/cx-visual-x3/actions/runs/39579075529) **34/34 STA成功**，对应工程、配置两工作流同SHA成功；人工键鼠/多屏/真实机台仍待。
- **P1开始真正交付**：Studio `doc.config`不再是四块占位说明，已为真实MEM Project Scope组合两种`MPWConfigEditorControl`：Vision.Inspection与CT3D.Reconstruction。原`MHSConfigService`集中Saved/Effective、申请生效、Schema校验、审计历史，未增加第二套数据真源或新项目。工程切换移除旧配置视图，关闭重开构造新Editor；保存专业参数不修改MEM Revision。
- **同SHA源码与验收**：[48f190ab](https://github.com/oudream/cx-visual-x3/commit/48f190ab96469c61fd190b9531d47b5d7236c25c)、[Studio 39580735263](https://github.com/oudream/cx-visual-x3/actions/runs/39580735263) **36/36 STA成功**、[工程39580735294](https://github.com/oudream/cx-visual-x3/actions/runs/39580735294) success、[Config L1 39580735210](https://github.com/oudream/cx-visual-x3/actions/runs/39580735210) success、[配置底座39580735249](https://github.com/oudream/cx-visual-x3/actions/runs/39580735249) success。新增Saved与Effective分离、非法8-bit阈值不污染Saved和MEM Revision、Project作用域和关闭重开的实测反证。后续提交[449ebf88](https://github.com/oudream/cx-visual-x3/commit/449ebf8826016b520739586be7a292d33c4bf253)对跨Studio Tab切换后草稿保留再验，[Studio 39581208046](https://github.com/oudream/cx-visual-x3/actions/runs/39581208046) **37/37 STA成功**，同SHA[Engineering 39581208067](https://github.com/oudream/cx-visual-x3/actions/runs/39581208067) success；Config L1该SHA以最终运行结果为准。
- **保留的Owner/阶段门**：P0-3专业资源声明/MPM-MHS统一入口/命令Descriptor和人工E级尚未签全；P1现仅落实两个真实专业、Project Scope双栏配置，并非任意资源实例Schema自动发现、授权全链或现场设备生效。下一轮沿Studio唯一主规划第17节，优先真实资源类型注册与多Scope/配置来源/权限校验。

### P0-4软件发布连续收敛最终CI核验（2026-10-21）

本节仅对**已有P0-1～P0-4/R01～R48、Studio S1～S4原台账**补充同SHA测试证据，不新建平行实施序号。

| 阶段/Commit | 实际新增或修复 | Windows CI 事实 | 签收边界 |
|---|---|---|---|
| [99c96538](https://github.com/oudream/cx-visual-x3/commit/99c9653877ba12401ce5b2b2a24852a940eb8aef) + [ab750dac](https://github.com/oudream/cx-visual-x3/commit/ab750dac72579a99b83758fb78e93d4c24117ea6) | Vision CPU Provider只读配置ReleaseVersion；Studio MOC候选→MRK授权软件发布、指纹轨迹；修复STA只读字典编译错误 | [Studio 39579860896](https://github.com/oudream/cx-visual-x3/actions/runs/39579860896) **35/35 PASS**；[Engineering 39579860873](https://github.com/oudream/cx-visual-x3/actions/runs/39579860873) **success**，含G06真实CPU像素检测、Host授权拒绝、设备安全点模拟拒绝、版本变动不污染活跃Plan | 本地无设备软件发布；不是现场生产发布 |
| [45586d62](https://github.com/oudream/cx-visual-x3/commit/45586d624101d403dc586e3e5302df02a992a13b) | 真正触发Studio准备CPU软件发布→MRK运行→发布指纹与FlowId轨迹；跨Root页面候选误用、过期Revision拒绝且不回退普通执行 | [Studio 39580614913](https://github.com/oudream/cx-visual-x3/actions/runs/39580614913) **37/37 PASS**；同SHA [Engineering 39580614903](https://github.com/oudream/cx-visual-x3/actions/runs/39580614903)、[Config L1 39580614890](https://github.com/oudream/cx-visual-x3/actions/runs/39580614890)、[配置底座 39580614885](https://github.com/oudream/cx-visual-x3/actions/runs/39580614885) 均 **success** | 真实Studio WPF CPU自动验收，非硬件 |
| [99f1ffcb](https://github.com/oudream/cx-visual-x3/commit/99f1ffcbd8eccda4170b3c3f0e966cb4107544dd) | Runtime可选Provider自证`IMRKVersionedCapabilityResolver`；MOC发布指纹验证专业Resolver自身版本与Host声明一致；Rev12→Rev13漏报与虚假版本拒绝，不污染活跃MRK计划；旧未实现自证接口的Resolver保持兼容 | [Studio 39581474675](https://github.com/oudream/cx-visual-x3/actions/runs/39581474675) **37/37 PASS**；[Engineering 39581474603](https://github.com/oudream/cx-visual-x3/actions/runs/39581474603)、[Unified Runtime 39581474385](https://github.com/oudream/cx-visual-x3/actions/runs/39581474385)、[Config L1 39581474415](https://github.com/oudream/cx-visual-x3/actions/runs/39581474415)、[配置底座 39581474429](https://github.com/oudream/cx-visual-x3/actions/runs/39581474429) 均 **success**。TestCenter [39581474410](https://github.com/oudream/cx-visual-x3/actions/runs/39581474410) 在本条记录时仍执行中，不提前签收 | 软件来源和版本自证仅覆盖实现接口的Provider；没有完整跨机/签名/设备联锁 |

**当前已按真实Windows自动验收收敛的纵向切片**：P0-1 工程依赖/引用及版本预演；P0-2 Page/Content/Symbol/Flow多Tab与MEM历史；P0-3 通用MPM Schema及Vision/CT3D MHS Project Scope两个真实专业配置编辑视图；P0-4 真实X-Ray主+AI/CV子流程、MOC构建、MRK成功/失败Trace、主/子流程/HMI定位、双Flow隔离、保存重开、版本失效、CPU软件发布授权与专业版本自证。以上不同功能有不同提交/CI证据，**不等于四个工作包100%完成**。

**继续受阻或尚未实测**：所有专业资源实例的可发现Schema/统一Command Descriptor，任意多页长期负载与高DPI/多屏键鼠，CT3D真三维Viewer及真实GPU/体数据压力，Provider跨机部署/模型签名/远程审批，机台PLC联锁/运动控制/实机权限与安全点、生产发布ACK、工业性能和多小时运行。这些不能仅靠35/37项Studio STA证明，后续须按既有Studio S2～S4和EP/R条目补生产环境及人工证据，不能强行标绿。

### P0-4 从MHS真实Effective配置到Vision Provider版本闭环（2026-10-21，待同SHA CI）

- 专业Vision Owner新增`CxVisionThresholdProvider.FromEffectiveConfiguration(MHSConfigSection<CxVisionInspectionOptions>, fallback)`；使用现有`CaptureForTask`读取Effective不可变快照，从MHS`vN`得到真实配置Revision，复制并释放快照；不自动Apply、不以Saved为Effective，不引入第二配置仓库。
- G06真实工程集成测试新增MHS Project Scope编辑、NextTask Saved/Effective分离、Effective CPU像素MRK执行、任务边界升级、旧候选`MOCProjectRelease.Stage`因版本自证拒绝、PlanSlot当前软件Revision不受污染、新候选指纹改变。此为软件版本/配置闭环；物理设备配置ACK与机台联锁继续未验。
