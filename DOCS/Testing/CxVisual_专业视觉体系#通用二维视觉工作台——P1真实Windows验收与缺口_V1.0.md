# CxVisual_专业视觉体系#通用二维视觉工作台——P1真实Windows验收与缺口 V1.0

> 测试依据：用户《通用二维视觉工作台完整开发要求》（2026-10-25）；源码使用当前 `master`。P1首个横跨FIM/MVI/MPW/TCT的功能闭环；**不代表60项需求全部实现**。
> 完整功能/Owner/复用路径见 [主规格](../Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——完整功能架构与实施验收_V1.0.md) 与 [S0旧源文件映射](../Migration/CxVisual_基础图像与体数据体系%23成熟源码迁移——S0主体归属与逐文件台账_V1.0.md)。

## 1. 最后一次已验证的源代码提交

- `98b664fc44af2ee88b8726875da594687bf6186b`：修复TestCenter RAW参数无效时`async void`异常及操作失败状态显示；前序同功能提交包括：
  - `fc9ee57`：MVI ImageViewport纯数学Fit/Actual/Pan/ZoomAt；
  - `62be7a2`：MPW原生WPF ImageViewer；
  - `837fd76`：TCT人工2D视觉工作区注册；
  - `c980801`：FIM RAW及16位大小端；
  - `c658ff5`：RAW输入UI/真实WPF应用Smoke；
  - `956ebec`：旋转/镜像/阈值/16位绝对差分；
  - `f058499`：WinExe退出码同步修复；
  - `4677ae8`：FIM灰度去噪、最大/最小形态学、高斯和UnsharpMask；
  - `463d7c1`：TCT工具栏/算法参数与耗时；
  - `98b664f`：RAW无效参数防御及错误可见性。

## 2. Windows CI实际运行证据（必须区分专项与全局）

| 检查项 | 实际结论 | 日志证据 |
|---|---|---|
| Windows Server 2022, VS2022/MSBuild, NET48 x64全解决方案构建 | **通过** | [Imaging 专项 run 40206188476](https://github.com/oudream/cx-visual-x3/actions/runs/40206188476)，Build all existing projects on Windows x64 |
| 28项目传统工程结构/真实源码Include | **通过** | 同工作流 Verify structure and dependency DAG |
| 项目依赖无非法上行边/无环 | **通过** | 同工作流，29个被扫描项目（包含解决方案之外工程），97条引用边 |
| FIM--imaging像素/RAW/滤波/计算及异常反证 | **通过** | 同工作流，`PASS FIM Imaging` |
| MVI图像会话SourceVersion与Observer | **通过** | 同工作流，`PASS MVI ImageSession` |
| MVI Fit/Pan/ZoomAt与鼠标锚点逆映射 | **通过** | 同工作流，`PASS MVI ImageViewport` |
| 全ArchitectureSmoke无参数回归 | **通过** | 同工作流，`PASS architecture tests` |
| WPF TestCenter Native WinExe进程真正启动/退出 | **通过** | 同工作流`Execute real WPF ImageViewer headless window and PNG roundtrip`阶段`success`，`Start-Process -Wait -PassThru`验证ExitCode=0 |
| TestCenter完整版UIAutomation（全菜单/报告/其它工作区） | **本专项未验收** | 独立 [TestCenter Windows工作流](https://github.com/oudream/cx-visual-x3/actions/workflows/testcenter-windows.yml)；历史UIA超过105秒，需单独解决 |
| HALCON/OpenCV真实库、驱动/相机/共享内存、X-Ray实机 | **未运行** | 本轮没有工业许可证/DLL/实体设备的成功测试，不得记录Passed |
| 4300²图、大图/长期运转性能及内存释放 | **未运行** | 不允许把4×4/3×3小样本测试当吞吐/资源测试 |

成功工作流具体运行链接：<https://github.com/oudream/cx-visual-x3/actions/runs/40206188476>。

> 注意：`864f0e1` 为后续“主规格文档”提交；它不等于新的源代码验收提交。以上可验证代码阶段以 `98b664f` 为准，后续源码变更必须有自己的实测证据。

## 3. 工具与手工验收

启动 `Output/Release/x64/CxVisual.TestCenter.exe`，打开“专有测试（人工操作）”→“二维工业视觉工作台”，可实际执行：
- 图片/BMP/PNG/JPG/TIFF导入和16位显示；
- 按长宽、位深和字节序的RAW输入（错误参数应见提示）；
- Fit、1:1、缩放、平移、光标灰度、矩形ROI选择；
- 原图/结果图切换、裁剪、归一化、旋转、镜像、Gray8/16差分、阈值；
- 均值/中值/腐蚀/膨胀/高斯降噪/UnsharpMask锐化，耗时观察；
- ROI原始灰度统计/32档直方图、当前图像PNG和复制。
这些入口基于FIM/MVI/MPW真实数据；**本轮没有逐按钮人工视觉验收、截图对照或真实工厂性能数据，不能标记为已完成**。

## 4. 后续功能门禁与优先级

1. P2：完善通用ROI（点/线/旋转矩形/圆/椭圆/多边形/曲线、编辑/Undo、Layer）及图像显示状态（旋转视口/伪彩/多视图同步/亮度/对比度）。
2. P3：分析/测量（边缘、连通域、形状/纹理/频域/配准；画线量测/圆弧等），算法入参/结果/中间图/结果复现。
3. P4：流/批量/相机/共享内存、背压/取消、资源所有权，实测精度与延迟。
4. P5：MEM图元/持久化、正式Vision Studio工作台、HALCON/OpenCV专业运算和设备驱动。
5. P6：行业真场景/算法准确性、图形界面逐操作、资源释放和X-Ray/AOI/CT工程质量验收。

所有未完成项均不得通过“有按钮”“有类”“有CI绿色”冒充60项完整能力。
