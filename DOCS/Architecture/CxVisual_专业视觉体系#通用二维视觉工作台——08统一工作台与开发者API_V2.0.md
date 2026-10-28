# CxVisual 通用二维视觉工作台——08 统一产品工作台、交互体验与开发者API设计 V2.0

> 覆盖V2D-05—09、18/19、21/22、24/25，贯穿26。真实基础MPWImageViewerControl、MPWImagePlotControl/RulerControl、TCTManualImageViewerFrame + Layout、CxVisual Studio Shell/AvalonDock、共享Presentation.Wpf；新正式Vision Workspace装配、算法/结果综合面板及可发布独立应用均为目标。参考02 U01/U02/P02/DP01。

## 1. 产品形态复用同一套核心能力

1. **Studio内专业Vision Workspace**：正式编辑工程、算法资源、标定、ROI与参数，运行实验/结果复核；走MEM编辑权限与发布流程。
2. **TestCenter验证宿主**：测试已有模块、WPF控件操作、真实数值/反证、算法/参数、CI与手工场景；不能承载唯一业务逻辑。
3. **独立桌面程序**：需要交付时复用专业控件和工作台组合，不再复制一个专属图像算法实现工程；是否新宿主须工程评审。
4. **嵌入式控件/API**：在Studio、HMI或客户WPF程序中使用MPW呈现以及FIM/MVI/PVC正式公共API；不允许客户必须实例化TestCenter Frame才能做分析。
5. **无界面运行/API**：专业Pipeline通过已批准的MRK/MDS运行入口调用，不依赖Dispatcher、WPF Application或桌面Session。

## 2. 工作台窗口布局与信息架构

~~~mermaid
flowchart TB
  R["上下文Ribbon / 搜索 / 快捷命令"] --> L
  R --> M
  R --> P
  R --> B
  subgraph C["统一Dock工作区"]
    L["左：工程资源/序列/ROI/对象树"]
    M["中：图窗/对比/流程图/多文档"]
    P["右：属性/算法参数/测量设置"]
    B["下：结果表/曲线/中间数据/日志/任务"]
  end
  M --> S["选中SourceRef/RunId/ItemId"]
  P --> S
  B --> S
~~~

布局要求：应用级Ribbon只显示当前上下文可用能力；对象树与工程资源树分离；Image/ROI/AlgorithmNode/ResultItem不同选择对象驱动不同Property；图窗与统计曲线、缺陷表、算法步骤可互相定位。显示图层不改变结果数据，Dock面板关闭不会释放仍被Run持有的Artifact。

主工作台至少支持多文档、多视口、原图/结果/差异/Mask、算法流程画布、局部分析图、ROI对象层、日志诊断视图。允许简易模式把高级工具收起，工程师通过“高级工具”发现完整能力，不以隐藏菜单阻断API。输入有明显状态、校验/单位提示、取消和恢复；重要动作需可撤回/确认。

## 3. 用户任务与命令体验

| 当前选择 | Ribbon和快捷操作 | 结果联动 |
|---|---|---|
| 图像/Frame | 缩放、窗位、预处理、分析与导出 | 显示图像身份/格式/灰度 |
| ROI/Mask | 移动、几何编辑、属性、区域统计、按ROI处理 | 图元与统计/算法结果来源一致 |
| 测量图元 | 调整端点/标定、测量、批量测量、导出 | 像素与物理值、质量/单位显式 |
| 算法节点 | 参数、端口、验证、预览、单步、运行 | 当前输入/输出和Trace、参数版本 |
| VisionRunResult | 结果定位、比较、复核、报告 | 图窗与ResultItem稳定关联 |
| 工程Resource | 属性、版本、Undo/Redo、校验、保存、发布 | 正式MEM修订/依赖状态 |
| 连续帧Stream | 预览、暂停、性能、Drop/NoDrop状态 | 队列/设备/算法Run/错误指标 |

“操作自然”包括：选择新工具有明显高亮、鼠标样式、状态栏告知阶段、撤销和Esc行为一致；超长运算不冻结UI，异常不弹无人CI无法关闭的模态框；侧栏宽度及布局可恢复；键盘、DPI/多屏、触控设备（必要时）和UIA可用。

## 4. UI与运行职责拆分

- MPW：WPF原生输入、Canvas/BitmapSource/GPU Presenter、Accessibility与交互反馈，公共的实际控件级API。
- MVI：坐标系、Viewport、ImageSession、比较模式、图元临时Store和无WPF交互状态；不能直接引入Application Dispatcher。
- Vision专业模型：OperationDescriptor/参数/ResultItem/专业工作台状态，供独立宿主或SDK消费；**仅为待裁决职责**。
- Studio：正式Workspace、Dock生命周期、工程编辑/保存/权限、发布命令；不复制FIM算法。
- TestCenter：场景驱动、真实UserControl按钮、结果断言、截图和诊断；测试宿主不拥有独立业务数据层。
- Runtime/API：独立WPF之外的执行和数据流；SDK必须有可取消Async、可诊断错误、输入/输出可读的真正入口，禁止要求调用UI Click或依赖窗口显示。

当前MPWImageViewerControl.xaml.cs超过2300行，继续增加算法与面板逻辑容易形成巨型控件。通过迁移与测试，先抽出实际重复的非WPF操作意图、图像显示状态和工具状态；禁止仅为减行数而创建多层Facade/Wrapper。

## 5. 共用公共API的最低易用性

| 使用方式 | 要求 |
|---|---|
| 单图浏览 | FIMImageBuffer创建/读入→MPW.ShowOriginal/Fit→获源坐标/选ROI |
| 单次算法 | 专业Operation输入、参数校验、CancellationToken→类型化Result |
| 结构化结果 | 通过RunId/ItemId访问Image/Mask/Geometry/Measurement/Trace |
| 批量任务 | 受控输入集合/计划→进度、取消、错误、批次结果 |
| 嵌入显示 | 公共WPF UserControl→样式/选择/截图/多视口，不依赖TCT |
| 工程操作 | 正式Resource定义→Preview/Commit→Undo/Save，不能隐式写盘 |
| 专业扩展 | Capability/Provider声明→类型检查→加入工具箱/运行服务 |

上述“算法调用/结果SDK”等为目标合同，实际可用签名须依后续PVC/平台代码核实并提供可编译示例，不允许本文件直接被当作已发布SDK参考手册。

## 6. 模块接入与上下文可发现性

专业Operation通过已有能力描述/类型注册进入工具箱：显示名称、用途、输入格式、参数模板、预览/性能信息、实际Provider状态、是否需要许可证。无能力的操作提示原因并可浏览说明，不能显示按钮并静默失败。通用UI使用Command绑定与能否执行状态（CanExecute），而非在每个子页硬编码专业SDK分支。权限从UI到目标Runtime全链路检查，隐藏按钮不能替代授权。

## 7. 错误反馈、日志与用户保障

可用的用户反馈包括：加载无效格式、位深不支持、ROI格式不合法、失去数据源、算法取消、结果过期、配准无解、设备失联、GPU不可用、工程版本冲突。错误应给简短可行动中文说明与详细诊断ID；不误清用户上一个有效图像/结果；保留可复制的失败信息和非模态反馈。任务操作可见进度、取消和硬资源预算；不能通过UI按钮反复提交重任务而不限制并发。

## 8. 一致视觉风格和操作可访问性

沿用Presentation.Wpf统一资源色板、字号、图标、Dock/Header、按钮/表格样式及Studio/TestCenter共用控制。图像窗口以画面为主，左树/右属性/下结果可折叠或自动隐藏；测量和图元编辑显示必要的操作阶段提示。上下文Ribbon、Ctrl+Z/Redo、Esc、Space/中键平移及手势具体键位要由统一Shortcut Registry和应用冲突表最终定案，不能在各控件分别偷偷定义。支持键盘导航、AutomationProperties、清晰焦点、100/125/150% DPI和低分辨率窗口布局。

## 9. 一条完整用户工作链

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant S as 工作台Shell
  participant W as MPW+MVI
  participant V as Vision专业服务
  participant R as Result/Artifact
  participant E as MEM
  U->>S: 打开图像/工程
  S->>W: 加载并显示原图
  U->>W: 画ROI并选择算法
  S->>V: 校验参数和输入/运行
  V-->>R: 结构化输出/Trace
  R-->>W: 临时Mask/几何叠加投影
  W-->>U: 图窗+结果表+分析图联动
  opt 用户确认保存专业定义
    U->>E: 显式编辑/Commit/Save
  end
~~~

## 10. D08验收案例与交付

| ID | 用户可见验收 |
|---|---|
| D08-01 | TestCenter和Studio复用公共MPW且数据/交互行为一致 |
| D08-02 | 打开图→画ROI→操作算法→结果表定位→报告导出完整点击流程 |
| D08-03 | 多文档/多视口/布局保存重开与关闭任务取消 |
| D08-04 | 参数非法/取消/异常不弹阻塞窗口，不污染前次结果 |
| D08-05 | 算法能力注册、搜索、参数Schema驱动菜单/属性/CanExecute |
| D08-06 | FIM/PVC/MRK无UI API可由独立进程调用，Trace可追踪 |
| D08-07 | 对象和工程修订通过统一MEM/MRC编辑/Undo，未用私有栈 |
| D08-08 | 键盘/UIA/多DPI/多屏/真实物理鼠标及性能实测 |
| D08-09 | 嵌入客户WPF的ImageViewer与独立Vision宿主接口一致 |

**当前状态**：已有真实TCT/MPW控件及部分Windows自动Smoke，但正式Studio Vision全面装配、可复用算法工作台和独立程序发布均待编码及严格验收。