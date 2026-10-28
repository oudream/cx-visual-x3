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
| F13 张量 | `FNUTensorShape`、`FNUTensorBuffer`、`FNUTensorView`、`FNUTensorMath` | RowMajor/Stride/Offset、实时无拷贝只读View、广播、归约、MatMul与切片复制 | 源码+专项测试 |

合计 **35个公开类型**，包含代表性算法类/结果类，**不是“52个全部完成”**。代码位于 `Foundation/Numerics`，传统.NET Framework 4.8 C#7.3 csproj，Math.NET 5.0.0 MIT NuGet已存在；本轮没有新建csproj与第二套平台运行数据存储。

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

## 九、F13 逐元素零额外缓冲分配组合API（2026-10-28）

- 增加 `FNUTensorMath.AddInto(a,b,output)`、`MultiplyInto(a,b,output)`，保留 `Add(a,b)`、`Multiply(a,b)` 的简便分配调用。支持尾轴广播，提前校验输出Rank/Shape及输入别名。
- `Into` 版本不按元素新建数组，也不为广播形状另外分配数据数组；已有输出空间归调用者持有，可在循环计算中复用。
- 输入/输出对象不允许为同一实例；参数形状不合法会在计算前报错且不修改输出。数值溢出后输出可能部分更新，需视为无效。
- 新增正反例：行向量广播加/乘、与已有分配API逐值比较、错误形状/别名、零维张量、零内维矩阵乘积。
- Windows CI 成功及完整回归以代码 SHA 对应的实际运行记录为准。性能指标尚需独立微基准/GC计数实证。

## 十、F13 同代码SHA Windows验证记录（2026-10-28）

| 代码SHA | Windows Actions | 实际验证 |
|---|---|---|
| `f73b11d` | [Numerics #40612654481](https://github.com/oudream/cx-visual-x3/actions/runs/40612654481) | **成功**：NuGet restore、.NET48/x64完整构建、Owner DAG、--numerics及默认ArchitectureSmoke |
| `9c0fb6d` | [Numerics #40612824905](https://github.com/oudream/cx-visual-x3/actions/runs/40612824905) | 截至补录时：全方案构建、Owner DAG、--numerics及默认ArchitectureSmoke **每步成功**；最终Workflow状态须点击Actions核对 |

不能把现有CPU密集张量API等同于GPU/多类型Tensor能力，也不能把编译和数值回归当作性能吞吐测试。F13下一质量门槛仍为低分配基准、跨规模矩阵对照、极端溢出处理与更多数据类型/视图合同。 

## 十一、F13 无拷贝只读视图（2026-10-28）

- 新增 `FNUTensorBuffer.AsView()` 与 `FNUTensorView`：共享现有Buffer数值数据，仅复制少量Shape/Stride元数据，支持只读 `Get`、无索引数组的 `Get2D`、`GetFlat`、`Slice`、`Transpose`；`CopyToBuffer` 才产生独立数值副本。
- View是**实时引用**，Owner.Set会立即影响View；外部并发修改必须自行同步。原Buffer.Slice和Reshape仍表示复制，保持已交付API的所有权语义。
- `FNUTensorMath.SumView` 与 `MatMulViews/MatMulViewsInto` 可直接处理非连续步长输入；Into会检查输出不能引用输入任一底层Owner，结果溢出可能留下部分输出。
- 测试覆盖转置-切片组合、步长/行序、活视图与快照、独立MatMul参考、别名、错误维度与空视图。
- 本次源码/自动化需按实际代码提交SHA的Windows CI单独验证；视图不是GPU/多dtype Tensor，也不保证多线程写安全。

## 十二、F13视图专项固定SHA验收（2026-10-28）

**代码 SHA**：`1aa095afaa3ef264ef8e0f6b22873101ea54c38e`。
**Windows CI**：[Numerics #40613787782](https://github.com/oudream/cx-visual-x3/actions/runs/40613787782) **Success**。

- `CxVisual.Unified.sln` .NET Framework 4.8 / x64 完整构建成功。
- `Verify-Structure.ps1` 与 `Verify-OwnerDependencies.ps1` 成功。
- `--numerics` 独立质量测试成功（含无拷贝Slice/Transpose、Get2D/GetFlat、数据实时性、CopyToBuffer隔离、非连续View MatMul、别名和越界）。
- 默认 `ArchitectureSmoke` 通过。此证据为代码SHA对应结果；此后文档补录不改变已验证代码。
- 尚未进行独立矩阵性能基准、GC分配/吞吐曲线、多数据类型、非CPU后端或实际工业设备验证。

## 十三、F04 条件数阈值与 F13 视图顺序遍历（2026-10-28）

- F04 兼容原 `LeastSquares(A,b)`；新增 `LeastSquares(A,b,maxConditionNumber)` 允许在 SVD 满秩情况下按条件数拒绝病态求解。有效阈值必须>=1，`PositiveInfinity` 表示不设限制；不改变旧调用默认行为。
- F13 `SumView` 与 `CopyToBuffer` 采用一次性分配的多轴坐标游标，顺序前进避免每样本重复逐轴整数除法；随机定位 `GetFlat` 继续保留。
- 正反测试：病态满秩 diag(1,1e-8) 的旧方法可返回数值诊断、可接受阈值、过严阈值拒绝、NaN阈值、真正秩亏仍拒绝；固定随机非连续View的Copy/Sum与独立GetFlat遍历结果对照。
- 新增 `--numerics-benchmark` 独立性能诊断，报告 `SumView` vs `GetFlat` 逐点参考路径与 `MatMulViewsInto` vs 新分配的 ms/op、bytes/op。仅记录数据，不以易抖动时间阈值判CI。是否真正加速由同机数据决定。
- 此处属于源码与测试合同；**实际Windows结果须以本次提交SHA的CI日志为准**。

## 十四、F04/F13自有数值能力验收与基准实证（2026-10-28）

**实现基线：** `f69760e`，代码主要为条件数上限API、F13张量View顺序游标以及`--numerics-benchmark`独立入口。

- 开发者使用：`FNULinearAlgebra.LeastSquares(A,b, maxConditionNumber:1e6)` 可拒绝数值病态但仍满秩的矩阵；现有 `LeastSquares(A,b)` 兼容默认无限阈值。
- F13 `SumView` 和 `CopyToBuffer` 采用一次性坐标数组按RowMajor逻辑遍历，不再为每个元素执行多轴除法；`GetFlat` 仍支持独立随机访问。
- 固定SHA [Numerics CI #40639945222](https://github.com/oudream/cx-visual-x3/actions/runs/40639945222) 尝试1/2：Windows 28工程编译、Owner/DAG、Numerics专项和独立性能基准均成功。默认ArchitectureSmoke两次在无关的 F17.PIPE_BULK 命名管道子进程退出问题失败，故工作流整体为 **failure**，不能宣称全量成功。
- 同机可复现的两次基准数据见 `DOCS/Testing/CxVisual_基础数值计算体系#测试验收矩阵_V1.0.md` N35～N38。游标求和速度有所改善；复用输出减少矩阵内存分配，但当前样本未体现确定性的计算时间改善。
- 继续关注：大型真实矩阵的条件数策略/正规化、原生 BLAS、多维张量多数据类型、GC与吞吐多轮统计；不因本轮性能诊断而声称全部已完成。

## 十五、F04 SVD岭正则化（2026-10-28）

- 公开 `FNULinearAlgebra.RidgeLeastSquares(A,b,lambda)`：最小化 `||Ax-b||²+lambda||x||²`，要求有限且严格正的lambda，支持超定、方阵、秩亏及欠定问题。与原 `LeastSquares(A,b)` 严格满列秩并列，原签名/失败语义不变。
- 使用 Math.NET 的SVD因子 `U,S,VT` 构造滤波 `s/(s²+lambda)`，避免正规方程放大病态与平方中间溢出；秩亏/零矩阵允许求解。原设计矩阵秩与条件数按真实SVD返回，秩亏报告正无穷。
- 正反验收：对角矩阵独立解析解、秩亏列重复的解析解、零设计矩阵、参数非法、欠定宽矩阵正确求解及观测维度错误拒绝、输入防修改。
- 单一lambda有物理单位（设计矩阵奇异值平方），此API不自动标准化特征或自动挑选正则化；大型/极端尺度与广泛数值稳定性测试仍待验证。
- 同SHA Windows验收须在真实 Actions 日志核实，不能因已提交代码便标记通过。

## 十六、F13非连续MatMulViews物理游标路径（2026-10-28）

- `MatMulViewsInto` 现在对已验证维度的只读View使用物理存储偏移和Stride在数值内层遍历，不再对每个元素调用会重复计算和校验行列的公开`Get2D`；底层数组访问仍由View统一约束。
- 不改变 `MatMulViews` 与 `MatMulViewsInto` 的输出所有权、别名限制、零长度行为和Neumaier补偿求和。
- 添加非连续双切片/转置的独立参考算法测试和尺寸不兼容反例。性能优劣依真实Windows基准，不能预先假定更快。

### 欠定矩阵能力补充

- 增补宽矩阵（1×2）的唯一带正则化解解析反例：`A=[1,1], b=[2], lambda=2`，系数应为[0.5,0.5]；无正则化的普通LeastSquares仍明确拒绝欠定系统。

## 十七、F04岭正则化与F13视图物理游标同SHA验收（2026-10-28）

| 代码SHA | Numerics实测状态 | 全量回归说明 |
|---|---|---|
| `51136d0` | [CI #40641679779](https://github.com/oudream/cx-visual-x3/actions/runs/40641679779)：全方案Build、Owner/DAG、--numerics、独立基准均通过；F04方阵、秩亏、零矩阵及正反合同均通过 | F17.PIPE_BULK失败，整体Workflow为failure |
| `11af443` | [CI #40641789076](https://github.com/oudream/cx-visual-x3/actions/runs/40641789076)：上述专项全通过；F13切片/转置View MatMul物理游标与独立逐元素参考一致 | 同一F17.PIPE_BULK失败，整体Workflow为failure |
| `418e766` | [CI #40641947228](https://github.com/oudream/cx-visual-x3/actions/runs/40641947228)：上述专项全通过；进一步增加1×2欠定宽矩阵岭正则化解析解 | 同一F17.PIPE_BULK失败，整体Workflow为failure |

第三批Windows Runner单次观测：64×64的MatMulViewsInto(复用)约 **0.8617 ms/op、32 B/op**，MatMulViews(分配)约 **0.7984 ms/op、32992 B/op**；256×256 SumView游标约 **0.1901 ms/op**，逐点GetFlat参考约 **0.3716 ms/op**。仅为CI机观测，不能把跨运行差异直接当严格加速比，也未证明分配式矩阵乘法更慢。未触及其他Owner的F17源码，全平台回归仍需由Transport专项定位。
