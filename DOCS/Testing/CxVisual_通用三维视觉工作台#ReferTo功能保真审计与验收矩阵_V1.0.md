# CxVisual 通用三维视觉工作台——ReferTo 功能保真审计与验收矩阵 V1.0

> 核查日期：2026-10-26。旧代码仅作只读基准：`ReferTo/Vision/CxECT3DViewer`、`CxECTVolumeCore`、`CxECTVolumeWinForms`（必要时 `Halcon`）。  
> 新版 Owner：`Foundation/Volume`（FVO）、`Mechanism/Visual/Volume`（MVI）、`Mechanism/Presentation.Wpf/Volume`（MPW）、`Professional/CT3D`、`Tests/TestCenter/Manual/Volume`。  
> 基线审查开始于 `7be0dbf278cea7bdcea128df9f840e65e649754f`；实际代码修改和 CI 应以本次验收提交为准。  
> **证据分级**：**W**＝实际 Windows WPF/基础测试已有正例；**S**＝新源码已连通但旧版 UI 完整行为未验收；**H**＝需要真实 GPU/工业数据实机验收；**R**＝缺口/已列整改。**W 不代表 H**。

## 一、旧版可用功能—新版直接归属与检查

| ID | ReferTo 成熟功能/源码 | 新 Owner/工作区操作 | 证据 | 保真验收、缺口与注意事项 |
|---|---|---|---|---|
| R01 | MainForm RAW 文件加载/选择 | `FVORawVolumeLoader` + `TCTManualVolumeLegacyLoad.LoadRawFileAsync` + RAW 按钮/命令 | W/H | 有真实 RAW 正例；对多 GB 数据、RAW 格式/内存压力还需现场数据检查 |
| R02 | 预览先行/完整 RAW 原值回读 | `FVOUnmanagedRawVolumeBuffer` + 原始数据文件映射 | W/H | 预览交互、原始 16-bit/Float32、取消和文件映射已有测试；大体积压力待验 |
| R03 | MainForm 原始/显示尺寸、spacing、标量状态 | `FVOVolumeData`、`_status`、`_sliceStatus` | S | UI 必须同时可观察原始/预览尺寸、原始标量和物理坐标，不能仅显示图像尺寸 |
| R04 | FourViewLayoutControl 2×2 四视图 | `TCTManualVolumeLegacyUi.BuildLegacyUi` | W | 真实 WPF Window 已测；需继续核对不同 DPI/缩放下可操作性 |
| R05 | 3×二维切片/高分辨率替换 | `FVOSliceImageBuilder` + `MPWImageViewerControl.UpdateOriginalFrame` | W/H | 换层保留视口；高位深、各向异性和工业样本细节仍需对照 |
| R06 | HALCON 切片十字线/拖拽三刀 | `TCTManualVolumeCrosshairs` + 唯一 `MVIVolumeViewState` | W/H | 鼠标操作有状态验收；HALCON 全部快捷键/边界交互未完成逐项实测 |
| R07 | HALCON 图窗缩放、平移、滚轮、适合、1:1 | `MPWImageViewerControl` + 切片页标题工具栏 | W/H | 换层保持缩放；交互敏感度与坐标原点旧版对照待实机 |
| R08 | XY/YZ/XZ 分别跳转切层 | `_headerSliceSliders`、`JumpToViewSlice` | W | 实际 Slider.ValueChanged 到 MVI 的测试 |
| R09 | 最大化单视图/恢复四视图 | `ToggleSlot`/`RestoreAllViews` | W | WPF 四视图窗口测试已检查 |
| R10 | 切片灰阶和伪彩色、2D 窗口值 | `FVOSliceImageBuilder` + `_pseudo/_windowMin/_windowMax` | W/H | 图像非空与 FVO 转换有测试；需采用同一 RAW 像素对照旧版 |
| R11 | 窗位/窗宽快捷输入 | `ApplyWindowCenterWidth`，按钮与工作区命令 | W | 无效数值须经统一 UI 错误反馈而不是未捕获异常 |
| R12 | 三刀原始体素坐标、物理坐标/鼠标回显 | `FVOSliceImageBuilder.VoxelToImage`、`UpdateCrosshairs` | W/H | 非等距映射已有修复；不同窗口/缩放下仍需旧版比对 |
| R13 | VolumeCore 各向异性/三维物理尺寸 | FVO/MPW 世界坐标 | W/H | 已按 spacing 映射；CT 非等距工业样本必测 |
| R14 | OpenTK 渲染器 GPU Raymarch 与纹理 | `MPWVolumeOpenGlControl`（WGL/OpenGL/GLSL） | S/H | 非 OpenTK 直接复用旧 GLSL 语义；普通 Windows CI 不提供 NVIDIA 画质验收 |
| R15 | RenderingCamera 旋转/滚轮复位 | MPW GPU 原生窗口 + `ResetCamera` | W/H | WPF 状态有测试；GPU `--volume-gpu-smoke` 必须真实显卡执行 |
| R16 | 3D 平移/缩放、相机注视点 | MPW GPU/WPF 相机和标题栏按钮 | W/H | 新增能力，必须验收三键/拖动/滚轮鼠标消息，而非只调 API |
| R17 | 彩色三正交切面、切面独立纹理 | `MPWVolumeOpenGlControl.SetSceneOptions` | W/H | 体绘、定位面、切面纹理各自显示开关需截图验收 |
| R18 | 体积外接框 | GPU `ShowVolumeBounds` | S/H | GPU 线框材质/坐标来源已存在，显卡实测 |
| R19 | 红绿蓝方向指示器/双击复位 | MPW WPF / GPU `SetOrientationVisible` | S/H | 切换已覆盖两渲染模式，实际 GPU 标记清晰度和双击验收待做 |
| R20 | 灰阶预设：Slicer/Linear、分位、背景阈值 | `CT3DRenderingSettings.BuildScalarTransferTexture` + 灰阶设置页 | S/H | 动态修改必须置位 LUT dirty 并更新 GPU 纹理，不可只变 UI 数字 |
| R21 | 光照/透明度/采样/jitter | CT3D settings → MPW Uniform | W/H | 参数切换事件有测试；数值改动应真实影响 GPU 画面 |
| R22 | 阈值双端控制 | `TCTVolumeThresholdEditor` → FVO Transfer → GPU LUT | W/H | 下限/上限变化已测；需要异常/边界实机验证 |
| R23 | 颜色曲线控制点/RGB | `TCTVolumeCurveEditor` + `TCTManualVolumeTransferPanels` | W/H | 添加节点/启用/节点颜色修改均应影响唯一 FVO 和 GPU |
| R24 | 标量透明度曲线/梯度透明度曲线 | FVO Transfer Functions + 纹理上传 | W/H | 生成 R8/RGBA8，GPU 透明度视觉效果须显卡核实 |
| R25 | 直方图（标量与梯度） | `FVOVolumeHistogram` + 四类编辑面板 | S | 后台异步显示，切换体数据时不可让旧直方图覆盖新数据 |
| R26 | 任意切：中心/角度/步长/插值 | `FVOObliqueSliceDefinition` + MVI + 设置页 | W | 已有三轴独立/跟随、三线性/最近邻、中心状态测试 |
| R27 | 任意切预览/3D 平面叠加 | `FVOObliqueSliceBuilder`、MPW 斜切纹理 | W/H | **本轮修复连续帧更新时预览缩放重置**；GPU 斜切颜色和空间位置实测 |
| R28 | 任意切高清 Float32 RAW + 元数据 | `SaveHighResolutionObliqueRaw` | W/H | 已检查像素字节数及 txt 元数据；工业体数据数值比对待做 |
| R29 | 完整四视图截图、单视图 PNG、GPU PNG | `SaveFourViewScreenshot` / `SaveSelectedViewPng` / `SaveGpuFrame` | W/H | **本轮阻止空数据导出空白四视图**；HWND GPU 内容须显卡实测 |
| R30 | 状态/异常和耗时 | `_status` / `LastUiError` / FVO 定时 | S | 工作区错误路径已可反馈；加载阶段性能日志、异步异常需要继续实测 |
| R31 | MainForm 现有操作入口 | TestCenter `Commands` + WPF Button | W | 当前对外至少 20 项命令并有 ID/CanExecute 检查，不再按旧三项口径审计 |
| R32 | 旧 WinForms/GLControl Dispose/数据关闭 | FVO `Dispose`、MPW GPU `Dispose`、`OnWorkspaceClosed` | W/H | 关闭、反复装载已有自动检查；显卡资源与多 GB 文件映射压力测试待做 |

## 二、验收口径和保留规则

1. `ReferTo/Vision` **始终只读**。不能把历史项目的多月成熟功能当成新系统中已验收；缺失行为应直接迁入对应 FVO/MVI/MPW/CT3D Owner，不增加 Adapter 项目或第二套状态。
2. 每一项必须经过 **入口→控件事件/命令→Owner 对象/算法→结果变化→反例测试**；光有按钮和 CanExecute 不够。
3. Windows `--volume-viewer-smoke` 证明 WPF 与数据功能；**不能代替** NVIDIA/Intel/AMD GPU 实机 `--volume-gpu-smoke`，也不能宣称工业 CT 数据画质、性能全部保真。
4. 每轮先解决 P0 级“控件不生效/状态未同步/丢原始数值”，再推进 P1 性能、美观和 P2 新功能；不因为“目标从 2D 扩为 3D”删除成熟旧能力。
5. 不能把本文件标成“100%已完成”；**H 项持续未验收**，直到有真实显卡截图/RAW数值/性能及鼠标操作记录。

## 三、本轮发现并实施的具体修复

- 任意切每次变化原来执行 `_obliqueViewer.ShowOriginal(image)`，该调用会重新初始化视口；改为首次 `ShowOriginal`，后续 `UpdateOriginalFrame`。WPF 回归检验缩放后继续调整斜切角度不重置视口。
- `SaveFourViewScreenshot` 原来允许未装载体数据时将空视图保存为正常 PNG；现强制检查 FVO/MVI 是否就绪，增加拒绝空截图反证。
- 深色右侧设置区的 `_sliceStatus` 仍使用深蓝色前景，改成浅色提高信息可读性。
- 审核时 `master` 已存在约 20 项 `TCTWorkspaceAction`、`LastUiError` 及 GPU 方向标记联动，本轮**不重复添加**；以最新代码为准。

## 四、下一阶段未通过实机验收的重点

- GPU 真实显卡：Raymarch 实际像素与旧 WinForms 同体数据对比；GLSL/驱动失败路径；多次开关、拖动、缩放、焦点和 WGL 资源释放。
- 全控件 UIA：按钮/滑杆/曲线/勾选、键盘输入与空数据、错误 RAW、取消加载、重入等对照旧版逐项反证。
- 大 RAW：预览交互时间、原始高精度切片、切换与并发取消、内存占用和文件映射关闭资源。
- 2D 旧 HALCON：十字线位置、物理坐标、平移缩放和 3D 空间切片范围的真实截图比对。

## 五、V04专业结果持久化增量（2026-10-29）

此项是R5目标的新增专业闭环，不将其伪称ReferTo旧版已有功能。详细源码/本地证据/未验证项统一见[V02记录第十节](../Implementation/CxVisual_通用三维视觉工作台%23V02原值三角Mesh与拓扑质量闭环_V1.0.md)。

| 验收ID | 正常路径 | 边界/异常 | 实际入口 |
|---|---|---|---|
| V04-01 | UInt16原值→稳定资产/Frame签名→Mesh→9点端面 | R8/未标定、尺寸长度不匹配、超ROI、非法路径、取消拒绝 | `CT3DAnalysisPersistenceTests`，`--volume`及默认 |
| V04-02 | MEM提交→Undo/Redo→保存关闭→重开保留ResultId/ROI/mm/残差→新ID复算 | expectedRevision冲突、源变化/缺失、制品Hash损坏/缺失、临时文件清理 | 同上 |
| V04-03 | 真WPF保存/再次保存→重开原值/Mesh/Measurement→继续使用工具 | Volume切换使晚到Open丢弃；Frame变化和外部StoreToken冲突拒绝 | `--volume-viewer-smoke` |
| V04-04 | 校验读锁与RAW映射共存、读原值1700 | 映射有效期内并发改写必须拒绝，释放后可回收 | `--volume` |
| V04-05 | 现有GL真实Raymarch/原生鼠标/3次装载 | 回退不得算GPU通过 | 本机`--volume-gpu-smoke` PASS，NVIDIA OpenGL 3.3/576.02；不外推新Mesh深度或工业精度 |
| V04-06 | 保存快照完成且保留当前较新分析，再次保存沿用最新Token | 保存期间Frame/局部ROI拟合/Scene Revision改变不得回填旧Mesh/Plane或重置显隐，磁盘成功不能伪报回滚 | `TCTVolumeViewerSmoke.VerifyProfessionalPersistence`新增异步正反例；修复提交另核Windows证据 |

Windows本地Build/Headless/WPF/GPU与GitHub CI分开登记；推送后只依据对应SHA Run升级CI。本包未完成完整三维ROI编辑、Studio专业工作区、通用Artifact注册、CAD/GD&T或工业标准件精度验收。

**固定CI更新**：`b71827db`的Windows Imaging `40837507856`、TestCenter `40837508115`、Graphics `40837507948`已SUCCESS；其Imaging日志含V04 Headless与三维WPF成功步骤。Studio S1 `40837507882`构建成功，但配置组合响应式容器断言失败。V04-06后续新增修复不借用这些旧SHA结果。详细日志结论统一维护于实施记录第10.5节。

### 收敛补充（2026-10-29，本机）

| 验收项 | 本轮证据 | 限制 |
|---|---|---|
| 历史Schema1读取/另存 | 真实旧DLL固定样本、SHA256及V1头断言；严格环境变量下`--volume`与默认回归PASS | 本次固定合成案例；旧CI的SKIP不升级为成功，后续按本提交CI核对 |
| 全工程与公共回归 | Debug/Release x64 0错误；结构/DAG、默认、Imaging、Concurrency均PASS | 当前26正式项目，审计含一个独立验证项目；存在既有警告 |
| WPF全窗口/四视图PNG | **FAIL**：真实PNG全黑；同入口不设PNG的行为检查PASS | 不能以行为PASS替代完整G3；环境与导出故障归因仍待查 |
| 实卡GL多对象 | NVIDIA OpenGL3.3/576.02，真实像素、VBO隔离/MVI绑定/显隐删除/样式零重传和3次重装载PASS | 不代表工业精度、全部深度/透明混合/拾取通过 |

完整交接与下一包入口见[新聊天续接](../Implementation/CxVisual_通用三维视觉工作台%23新聊天续接与大工作包交接_20261029.md)，详细结果见实施记录10.16；不新增竞争性状态总纲。

**后续收敛更新**：实施记录10.17核实`5a8a9eec` CT3D B G1～G3与TestCenter固定CI成功。本机延后启动的一次PNG成功经实际查看，但重复失败，尚无稳定修复；临时启动/首帧等待代码均撤回，未降低原截图断言。Studio旧三卡断言补齐已注册MDS后，本机完整STA 61 PASS/0 FAIL；此修正的新SHA CI单独核实，不能借旧成功升级。

### B包切片ROI专业闭环

| ID | 正常路径 | 失败与失效反证 | 实证 |
|---|---|---|---|
| B-ROI-01 | MVI唯一矩形→MPW Shift拖动轮廓/切层隐藏恢复→CT3D物理棱柱选Mesh顶点→毫米平面 | 错Frame、空/退化区、非法毫米范围、取消、30000顶点预算 | `--volume`；旋转/镜像/各向异性三平面9点真值；固定`6f7a9b5c` G1～G3成功 |
| B-ROI-02 | 真实毫米输入/按钮→新Feature/Measurement→MEM保存关闭重开→继续工具 | 编辑ROI清旧结果；保存期间新框选不被旧快照覆盖；多Case隔离 | 真WPF行为本机退出0；输入按钮补强的新SHA CI另验 |
| B-ROI-03 | 真PNG包含选区金色像素 | 不能只登记HasRectangle标志或用SDK数值替代可见性 | 固定CI `40892609609` WPF/PNG成功；本机早期黑帧仍未关闭 |

完整证据及边界见实施记录10.18。矩形/棱柱交互配方重开、通用3D工具、正式Studio和工业精度仍未验收；有效顶点ROI保存不等于这些项已交付。二维WPF导航修正与TestCenter通用UIA超时分别追踪，不能互相抵消。

**固定升级**：`7ddb3c16`六个Windows门禁全部成功，CT3D B `40925092419`、Imaging `40925092368`及TestCenter `40925092677`覆盖输入按钮、ROI PNG和二维导航。后续B-ROI-04增加“三张FIM对象/缩放保持、直接MVI修改及后台清选区统一失效”的真实WPF反例，本机通过，新SHA CI另核；详见实施记录10.19。
