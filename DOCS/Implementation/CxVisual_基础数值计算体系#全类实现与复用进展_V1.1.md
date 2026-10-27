# CxVisual Foundation.Numerics —— 全类增量实现与复用进展 V1.1

> 2026-10-27，模块Owner：F03数学计算、F04数值求解、F10采样信号、F13张量计算。
> 此文档记录**真实实现与验证证据**，与原规划“52个候选职责位”区别；目前不宣称完整52项已实现。

## 一、按开发者使用方式已交付的真实功能

| 类别 | 本轮真实API/类型 | 典型使用 | 状态 |
|---|---|---|---|
| F03 数学 | `FNUPolynomial`、`FNUInterpolation`、`FNUPrecision` | Horner/导数/线性双三线性/精度比较 | 源码+专项测试 |
| F03 统计 | `FNUStatistics`、`FNURunningStatistics`、`FNUStatisticsSnapshot`、`FNUHistogram`、`FNUDistribution` | 统计增量Merge、样本/总体、分箱、Quantile | 源码+专项测试 |
| F04 解算 | `FNULinearAlgebra`、`FNULeastSquaresResult`、`FNUWeightedLeastSquares`、`FNUCurveFitting` | Math.NET SVD最小二乘、加权与多项式拟合 | 源码+专项测试 |
| F04 求根优化 | `FNURootFinding`、`FNURootResult`、`FNUOptimization`、`FNUOptimizationResult`、`FNUFiniteDifferences` | 二分、黄金分割、中心差分、收敛结果 | 源码+专项测试 |
| F04 积分/ODE | `FNUNumericalIntegration`、`FNUOdeSolver`、`FNUOdeTrajectory` | 复合Simpson、标量定步长RK4 | 源码+专项测试 |
| F10 时域 | `FNUMovingAverage`、`FNUFirFilter`、`FNUIirFilter`、`FNUResampling`、`FNUConvolution`、`FNUCorrelation`、`FNUWindowFunction` | 状态滤波/重采样/卷积相关/Hann窗 | 源码+专项测试 |
| F10 频域 | `FNUFourier`、`FNUFrequencyAnalysis`、`FNUFrequencySpectrum` | FFT/逆变换、单边振幅频谱/Hz | 源码+专项测试 |
| F13 张量 | `FNUTensorShape`、`FNUTensorBuffer`、`FNUTensorMath` | RowMajor/Stride/Offset、广播、求和、MatMul、切片复制 | 源码+专项测试 |

合计 **33个公开类型**，包含代表性算法类/结果类，**不是“52个全部完成”**。代码位于 `Foundation/Numerics`，传统.NET Framework 4.8 C#7.3 csproj，Math.NET 5.0.0 MIT NuGet已存在；本轮没有新建csproj与第二套平台运行数据存储。

## 二、开发者自由组合示例（以下API均有对应真实源码）

```csharp
var filtered=FNUFirFilter.Apply(new double[]{1,2,3,4},new double[]{.5,.5});
var spectrum=FNUFrequencyAnalysis.OneSidedAmplitude(filtered,1000);
var fit=FNUCurveFitting.Polynomial(new double[]{0,1,2,3},new double[]{1,4,9,16},2);
var tensor=new FNUTensorBuffer(new FNUTensorShape(2,2),new double[]{1,2,3,4});
var product=FNUTensorMath.MatMul(tensor,tensor);
var root=FNURootFinding.Bisection(x=>x*x-2,0,2);
```

## 三、专业复用和验证等级

- FIM: `FIMImageAnalyzer` 原始/掩膜 ROI Welford 统计复用 `FNURunningStatistics`，原FIM API/像素所有权/直方图/取消不变；[Imaging PASS #40503133853](https://github.com/oudream/cx-visual-x3/actions/runs/40503133853) 对应旧基线 `51ba6ef`。
- FVO: `FVOObliqueSliceBuilder` 的八角点插值在 `22bbee9` 改用 `FNUInterpolation.Trilinear`；Volume 保留原体素采样、深度、越界及浮点输出语义；**Imaging/Numerics同SHA验收尚需以对应记录为准**。
- 数值基础F03/F04增量 `b91bb45`：[Numerics #40515727903](https://github.com/oudream/cx-visual-x3/actions/runs/40515727903) 实际成功。
- F10增量 `4656dd2`：[Numerics #40515935323](https://github.com/oudream/cx-visual-x3/actions/runs/40515935323) 实际成功。
- F13之后 `e65452e`、`760c71c`：`--numerics` 专项PASS，完整构建与DAG PASS，但默认烟测被不相关图元整圆Sweep回归阻断；不可宣称整体通过。
- `2629e73` 及 `22bbee9` 结果等待各自CI结束才可填写，避免预先标记通过。

## 四、未完成和不得夸大的部分

1. 多维/约束优化、病态矩阵稳定性/高级分解、鲁棒拟合、常微分方程自适应和高阶专用求解未全覆盖。
2. 信号小波、谱密度估计、完整FIR/IIR设计器、流式环形缓冲、专业重采样的抗混叠滤波和大数据FFT基准未完成。
3. F13当前仅double CPU稠密张量，未做多dtype、无拷贝非连续 View、底层共享内存/原生/并行/GPU、自动微分/计算图；不能冒充 AI推理或既有FDT Owner。
4. 数值量化容差、条件数、大规模吞吐/GC/p95及跨平台测试与设备HIL未完成；本次不伪造。
5. 原规划52个候选类型不必逐项机械新建，优先以高质量实际算法/API支持开发者目标，按场景证据继续增构。

## 五、验收操作

```powershell
msbuild .\CxVisual.Unified.sln /t:Restore,Build /p:Configuration=Release /p:Platform=x64 /m:1
.\Build\Verify-Structure.ps1
.\Build\Verify-OwnerDependencies.ps1
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --numerics
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --imaging
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --volume
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
```

所有命令仍以真实 Windows CI 的同SHA日志为验收依据。单个专项通过和默认全量通过分别记录，互不替代。
