# CxVisual 通用三维视觉工作台——整体迁入与 FVO/MVI 首轮实证 V1.0

> 2026-10-26。主仓 `oudream/cx-visual-x3`；`ReferTo` 保持只读。**本台账按固定提交和实际 GitHub Actions 分级，不代表旧三维 Viewer 已在 WPF 复原。**

## 一、原样迁入（可核验）

- [`fcf00bef`](https://github.com/oudream/cx-visual-x3/commit/fcf00bef02cdfe247853ff3f7085a752061927b9)：从三个旧三维工程将 **25 个非生成 C# 文件**按 FVO/MVI/MPW/CT3D/TestCenter 目标 Owner 迁入。原始 Blob SHA 一致，`ReferTo` 不变。
- 迁入时未加入传统 `.csproj` 的旧 WinForms/OpenTK/HALCON/Studio 主界面代码，避免误判为正式 WPF Viewer 已可运行。
- 原三维 S0 完整基线共 44 文件（源码、配置、程序集信息、文档）；额外四项 Halcon/CommonInterfaces 依赖另按本轮 48 行 CSV 跟踪。

## 二、正式编译和回归的真实增量

| 主题 | 固定 SHA | Windows Imaging CI | 已覆盖 |
|---|---|---|---|
| FVO RAW、8/16 位原始标量、非托管内存、Oblique 几何 | `13c2cb2` | [PASS #40264752336](https://github.com/oudream/cx-visual-x3/actions/runs/40264752336) | 32位/64位规则以目标运行时验收，构建 Release/x64、`--volume`、默认回归 |
| FVO 正交切片/FIM 缓冲、高精度 XY 与物理坐标映射 | `f62f755` | [PASS #40265179437](https://github.com/oudream/cx-visual-x3/actions/runs/40265179437) | XY/XZ/YZ、斜切几何基础、非等距 Spacing、Bitmap 输出替换 |
| MVI 四视图单状态（无 WPF 依赖） | `8039a95` | [PASS #40265366125](https://github.com/oudream/cx-visual-x3/actions/runs/40265366125) | SliceX/Y/Z、一致性通知、视口、斜切状态快照 |
| FVO 灰阶统计/梯度直方图/自动范围 | `621d576` | [PASS #40265545896](https://github.com/oudream/cx-visual-x3/actions/runs/40265545896) | 实际预览体统计、默认区间、无设备依赖 |

测试入口：`CxVisual.Tests.ArchitectureSmoke.exe --volume`；亦纳入默认回归。工作流：`.github/workflows/imaging-foundation-windows.yml`。不以其它工作流的 PASS 代替对应工作包验收。

**高位深保真修复：** 当 `FVOVolumeData` 持有真实原始 RAW 非托管缓存，`GetScalarByOriginalIndex` 直接读原始 UInt16/Float32；不再由预览 Gray8 恢复造成精度损失。

## 三、未完成（不能宣称已交付）

1. FVO 中原迁入的 TransferFunctions 原文件尚未完成 Foundation/Professional 的依赖边界收敛，未加入编译；渲染预设（Slicer风格）仍需迁入 CT3D 正式 Owner。
2. MPW OpenGL/GLSL/Camera/RenderingVolumeControl 7 个渲染源码为**原样迁入、未编译**，未完成 WPF GPU 宿主/驱动与资源释放实测；不得把旧 WinForms GLControl 冒称原生 WPF。
3. MPW 2D Slice、四视图布局、TransferFunction 编辑器、方向指示等为旧源码参考拷贝，尚未成为实际 WPF 控件；TestCenter 主界面亦为旧源码拷贝，未注册工作区。
4. 仍须旧新相同 RAW/BMP 对照、完整灰阶/色彩/透明度曲线/光照/任意切/四视图交互/截图/显存/资源寿命、HIL及大体积性能测试。
5. `ReferTo/Vision/Halcon` 两个图窗依赖未迁入正式 Professional；不得引入 FVO/MPW 公共 API。

## 四、下一实施顺序

按现行 [48 行 CSV](../Migration/CxVisual_通用三维视觉工作台%23成熟源码整体迁移——逐文件归属与保真验收清单_V1.0.csv) 继续：
`FVO Transfer/LUT → CT3D预设与渲染参数 → MPW WPF GPU宿主/GLSL真实验收 → MVI四视图联动 → TCT旧界面完整回归`。

仍遵守：不新增 `.csproj`、不建 Adapter/Wrapper/Facade 中间层，不复制第二套 FIM、工程树、事件、Undo、命令、执行或数据仓库。成熟着色器、渲染性能和交互行为必须保留，再逐项增构。
