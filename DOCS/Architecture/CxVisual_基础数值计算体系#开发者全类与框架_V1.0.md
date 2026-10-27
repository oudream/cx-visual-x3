# CxVisual Foundation.Numerics——开发者需求、全类与框架 V1.0

> 2026-10-27；唯一 Owner：Foundation/Numerics；F03/F04/F10/F13。
> 依据：AGENTS、DOCS/00、01架构宪法、工程规范、ProjectCatalog、FGE/FIM/FVO真实源码、ccxx/gcl3参考档案与开发者需求讨论。
> **本表是开发者驱动的全类候选清单，不是当前已实现类统计。尚未逐个Excel Sheet对齐DOCS/02～04类型身份，未对照完成前类名为候选。不得为数量创建空类。**

## 1. 开发者视角的八项需求

- N-D01 直接调用：数学/插值/统计一行完成，不必创建全局Manager。
- N-D02 组合调用：数组、矩阵、求解、滤波、频域等自由组合；与业务对象解耦。
- N-D03 高级参数：容差、算法选择、终止准则、失败原因、收敛/秩/残差。
- N-D04 数据：支持标量、数组、矩阵/张量，读写所有权明确；大型数组减少复制与GC。
- N-D05 性能：优化低分配路径、增量状态、批量/并行；成熟计算后端可替换。
- N-D06 可靠：空集、NaN/Infinity、秩亏、溢出、退化、端点/取样界，行为有明确测试。
- N-D07 扩展：小算法静态、持续滤波等用实例，GoF Strategy仅用于实际多实现求解器。
- N-D08 验证：解析解、反例、同样本旧版替换、误差/性能基线、Windows CI和部署。

## 2. 候选全类 / 归属与实现状态

状态：I=首轮有真实源码，P=规划，不可把P创建为空壳或声称可用。

| 子方面 | 细分 | 主体类型与职能 | 状态 |
|---|---|---|---|
| F03 数学 | 标量与精度 | FNUScalarMath、FNUPrecision、FNUInterpolation、FNUPolynomial、FNUComplexMath | FNUInterpolation I；其他 P |
| F03 数学 | 统计 | FNUStatistics、FNURunningStatistics、FNUStatisticsSnapshot、FNUHistogram、FNUDistribution、FNURegression | 前三项 I；其他 P |
| F04 求解 | 线性代数 | FNULinearAlgebra、FNULeastSquaresResult、FNUMatrixView、FNUFactorization、FNUDecompositionResult | 前两项 I；其他 P |
| F04 求解 | 数值方程 | FNUEquationSolver、FNURootFinding、FNUOptimization、FNUOptimizerOptions、FNUConvergenceResult、FNUNumericalIntegration、FNUOdeSolver | P |
| F04 求解 | 拟合 | FNUCurveFitting、FNUWeightedLeastSquares、FNURobustFitting、FNUFitResult | P |
| F10 信号 | 采样变换 | FNUSampleSeries、FNUResampling、FNUWindowFunction、FNUConvolution、FNUCorrelation | P |
| F10 信号 | 时域滤波 | FNUMovingAverage、FNUFirFilter、FNUIirFilter、FNUFilterState、FNUFilterCoefficients | FNUMovingAverage I；其余 P |
| F10 信号 | 频域 | FNUFourier、FNUSpectrum、FNUFrequencyAnalysis、FNUSpectralDensity、FNUWavelet | FNUFourier I；其余 P |
| F13 张量 | 形状与数据 | FNUTensorShape、FNUTensorView、FNUTensorBuffer、FNUTensorSlice、FNUTensorLayout | P |
| F13 张量 | 运算 | FNUTensorMath、FNUTensorReduction、FNUTensorBroadcast、FNUTensorContract、FNUTensorBackend | P |

该清单共 **52个候选类型槽位**，部分会合并或直接复用 System/Math.NET 类型；**并非承诺52个新类**。避免复制System.Math、Math.NET、System.Numerics.Tensors或已有Foundation.Data Shape，也不建立独立Tensor管理平台。

## 3. 框架与依赖决策

1. 保持现行.NET Framework 4.8、C#7.3、x64、28项目和现有 csproj/程序集。
2. 纯计算本项目唯一Owner，不依赖Imaging/Volume/Geometry/Mechanism/Professional/WPF。
3. 轻量、性能路径可以自有实现；SVD和FFT采用MathNet.Numerics **5.0.0**（NuGet, MIT），不手工复制 DLL，也不先引入 MKL。
4. Tensor/MKL/ALGLIB/NLopt等仅后续候选；先对照已有数据形状、量纲、张量与许可证/部署。
5. 计算策略、可取消迭代、原生Provider等在出现真实需求时引入；普通纯数学不建Interface/Manager/运行图。
6. 开发者直接使用C#组合；需要图形编排则由现有MOL/MRK进行调度，Numerics不创建第二引擎。
7. FGE负责空间几何、FIM负责像素/ROI、FVO负责体素/采样；仅把真正通用的数值计算抽到Numerics，专业公开API保持兼容。

## 4. 首批真实开发者API与语义

\`\`\`csharp
double a = FNUInterpolation.Linear(10,20,.25);
double b = FNUInterpolation.Trilinear(v000,v100,v010,v110,v001,v101,v011,v111,x,y,z);
double mean = FNUStatistics.Mean(samples);
var streaming = new FNURunningStatistics(); streaming.Add(3); streaming.Add(4);
var summary = streaming.Snapshot();
double[] filtered = FNUMovingAverage.Calculate(samples,5);
FNUMovingAverage.Write(samples,5,externalOutput);
var solved = FNULinearAlgebra.LeastSquares(design,observations);
var fft = FNUFourier.ForwardReal(samples);
var inverted = FNUFourier.Inverse(fft);
\`\`\`

- 插值允许有限范围外推，不处理空间边界、mask/体素访问；输入非有限拒绝。
- 统计Welford，区分总体和样本方差，单样本样本方差为NaN；一个实例不允许并发写入。
- 移动平均为因果窗口；前期窗口不足以已采样点为分母；输出可由调用者提供，拒绝输出输入别名。
- SVD最小二乘只接受行数≥列数的满列秩问题，返回秩和残差；退化、输入非有限则明确失败，不修改输入。
- FFT使用Matlab约定（正变换不缩放，逆变换除以N）；拷贝数组，不改调用者数据。

## 5. 实施与抽取替代顺序

- G1 本次提交：F03插值/在线统计、F04 SVD最小二乘、F10移动平均/FFT、源码类测试+独立Windows CI。**代码写入不代表CI已成功。**
- G2 先定 FIM/FVO 数值与性能黄金样本，再从旧统计或三线性内核替代调用，要求原API、端点、16bit数值、异常/性能回归无损。
- G3 数值求根、QR/迭代求解、信号滤波/窗函数、拟合、统计分布。
- G4 F13张量、Shape/Stride/广播/归约，与Foundation.Data及专业数据内核查重后实施。
- G5 大规模内存/性能、MKL原生后端评估、稳定度、交叉专业真实验收。
- 任何新类别从**需求→Owner→API→正反用例→同SHA Windows证据**闭环，不填空类凑数。

测试见：DOCS/Testing/CxVisual_基础数值计算体系#测试验收矩阵_V1.0.md。
