# CxVisual Foundation.Numerics —— 全类增量实现与复用进展 V1.1

> 2026-10-27，模块Owner：F03数学计算、F04数值求解、F10采样信号、F13张量计算。
> 此文档记录**真实实现与验证证据**，与原规划“52个候选职责位”区别；目前不宣称完整52项已实现。

## 一、按开发者使用方式已交付的真实功能

| 类别 | 本轮真实API/类型 | 典型使用 | 状态 |
|---|---|---|---|
| F03 数学 | `FNUPolynomial`、`FNUInterpolation`、`FNUPrecision` | Horner/导数/线性双三线性/精度比较 | 源码+专项测试 |
| F03 统计 | `FNUStatistics`、`FNURunningStatistics`、`FNUStatisticsSnapshot`、`FNUHistogram`、`FNUDistribution` | 统计增量Merge、样本/总体、分箱、Quantile | 源码+专项测试 |
| F04 解算 | `FNULinearAlgebra`、`FNULeastSquaresResult`、`FNUWeightedLeastSquares`、`FNUCurveFitting` | Math.NET SVD最小二乘及条件数诊断、加权与多项式拟合 | 源码+专项测试 |
| F04 求根优化 | `FNURootFinding`、`FNURootResult`、`FNUOptimization`、`FNUOptimizationResult`、`FNUFiniteDifferences` | 二分、黄金分割、中心差分、收敛结果 | 源码+专项测试 |
| F04 积分/ODE | `FNUNumericalIntegration`、`FNUOdeSolver`、`FNUOdeTrajectory` | 复合Simpson、标量定步长RK4 | 源码+专项测试 |
| F10 时域 | `FNUMovingAverage`、`FNUFirFilter`、`FNUFirStream`、`FNUIirFilter`、`FNUResampling`、`FNUConvolution`、`FNUCorrelation`、`FNUWindowFunction` | 状态滤波/流式FIR/重采样/卷积相关/Hann窗 | 源码+专项测试 |
| F10 频域 | `FNUFourier`、`FNUFrequencyAnalysis`、`FNUFrequencySpectrum` | FFT/逆变换、单边振幅频谱/Hz | 源码+专项测试 |
| F13 张量 | `FNUTensorShape`、`FNUTensorBuffer`、`FNUTensorMath` | RowMajor/Stride/Offset、广播、求和、MatMul、切片复制 | 源码+专项测试 |

合计 **34个公开类型**，包含代表性算法类/结果类，**不是“52个全部完成”**。代码位于 `Foundation/Numerics`，传统.NET Framework 4.8 C#7.3 csproj，Math.NET 5.0.0 MIT NuGet已存在；本轮没有新建csproj与第二套平台运行数据存储。

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
- FVO: `FVOObliqueSliceBuilder` 的八角点插值在 `22bbee9` 改用 `FNUInterpolation.Trilinear`；Volume 保留原体素采样、深度、越界及浮点输出语义；**Imaging/Numerics同SHA双CI已通过 #40516958563 / #40516958832**。
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

## 六、最终已核实的专业抽取/替代证据

- FIM原始灰度与掩膜ROI：继续使用原FIM API，公共Welford统计内核由FNU提供，旧Windows Imaging已通过。
- FVO任意切面：体素边界/读取、原始16/32位浮点语义保留，原八角点数值插值转交FNU；固定SHA `22bbee9` 对应的 Numerics CI 和 Imaging CI **均成功**，包含 Volume、Imaging、默认回归及WPF图窗/切片烟测。
- 早先 `e65452e`、`760c71c` 的默认架构烟测曾遭遇图元整圆Sweep失败，而更新的 `2629e73` 与 `22bbee9` 默认测试已经成功；须持续关注图元回归的跨次稳定性，勿把它归因于数值代码。

## 七、2026-10-27 数值质量优先增量（固定SHA）

**源码提交：** `eac5970494ddf91e4f880e6064066c214d0da087`。
**Windows 验证：** [Numerics #40525673674](https://github.com/oudream/cx-visual-x3/actions/runs/40525673674) 成功；.NET Framework 4.8 x64全解决方案构建、Owner/DAG、独立 `--numerics`、默认 ArchitectureSmoke 全部通过。

1. **F13计算一致性：** `FNUTensorMath.ReduceSum` 与 `MatMul` 使用逐输出补偿求和，覆盖 `1e16, 1, -1e16` 大数抵消、普通数值和零长度维度；未声称任意溢出/任意病态问题都已解决。
2. **F10面向开发者：** `FNUFirStream` 复用既有 FIR 算法契约，提供 `Process(sample)`、`Reset()` 和 `TapCount`；采用持有状态的循环缓冲，不在每个 `Process` 内分配数组；非有限输入或结果溢出时不更改历史。16组固定种子信号逐值对比 `FNUFirFilter.Apply`，并验证系数防御性复制、Reset 和失败原子性。单实例非线程安全。
3. **F04可诊断：** `FNULeastSquaresResult.ConditionNumber` 报告最大/最小奇异值的比值。该值是数值诊断，不替代误差界/条件数阈值策略，病态问题仍须增补分尺度对照。
4. **验证口径：** CI中的“无逐样本分配”是由 `Process` 实现结构和测试覆盖的 API 行为说明，尚未建立跨规模运行时GC/吞吐量测；p50/p95、病态矩阵性能和多核/原生后端仍待独立验证。

开发者使用示例：

```csharp
var filter = new FNUFirStream(new double[] { 0.25, 0.5, 0.25 });
double y0 = filter.Process(10);
double y1 = filter.Process(20);
filter.Reset(); // 新采样序列从零历史重新开始

var solved = FNULinearAlgebra.LeastSquares(design, observations);
double conditionNumber = solved.ConditionNumber;
```

## 八、F13 数值边界与输出缓冲复用（2026-10-28）

- `FNUTensorShape` 先验证所有维度，再计算总元素数。任意一个轴长度为0的空张量不会因其他大维度的中间乘法提前溢出；无可访问元素而理论Stride超出Int32范围时，该Stride值设为0（无有效索引）。非空张量仍严格检查 `int.MaxValue` 上限。
- `FNUTensorMath.MatMulInto(a,b,output)` 由开发者持有输出缓冲区，复用现有补偿求和，不需要每次调用都创建新结果；`MatMul(a,b)` 保留原简便API并复用同一计算内核。
- 前置合同：必须为二维 (m,k) 与 (k,n)；output为 (m,n)；拒绝与输入同实例；维度错误不会写输出。数值溢出可能留下部分输出，调用者应丢弃失败的输出内容。
- 固定种子矩阵逐值对照、1e16抵消、复用结果、同实例禁止、空张量巨大维度及非空超大维度均纳入 `FNUNumericsQualityTests`。
- **状态：源码与测试已加入；同SHA Windows .NET48/x64结果须以对应Actions实际执行结果认定。** 尚未完成大规模矩阵的独立吞吐/GC基线以及原生计算后端对比。
