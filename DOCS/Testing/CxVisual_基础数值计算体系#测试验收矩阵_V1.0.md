# CxVisual Foundation.Numerics——TDD测试验收矩阵 V1.0

> 本文件日期2026-10-27。I=源码与测试入口已写入；W=对应SHA的Windows CI完成且成功；P=待实现。**现阶段提交之前所有W均不可先填写。**
> 入口：Tests/ArchitectureSmoke/Numerics/FNUNumericsTests.cs，--numerics 与默认回归；Windows独立workflow: .github/workflows/numerics-foundation-windows.yml。

| Case | 正向行为 | 失败/边界 | 当前阶段 |
|---|---|---|---|
| N01 | 线性插值 10→20 @.25=12.5 | NaN / Infinity拒绝 | I+W |
| N02 | 线性外推 t=-1=0 | 输入所有权 | I+W |
| N03 | 双线性中心值3 | 非有限顶点 | I+W |
| N04 | 三线性8顶点中心3.5 | 轴顺序/越界由专业控制 | I+W |
| N05 | 四样本 Welford：均值2.5、总体方差1.25 | 空集拒绝 | I+W |
| N06 | 样本方差5/3、最小1最大4 | 单样本未定义 | I+W |
| N07 | 流统计Merge与一次性等价 | NaN、Infinity，不污染状态 | I+W |
| N08 | 移动平均 [1,1.5,2.5,3.5] | window<=0拒绝 | I+W |
| N09 | 自有buffer填充，源数组不变 | 输入/输出别名拒绝 | I+W |
| N10 | SVD 解 y=1+2x、rank2残差≈0 | 秩亏显式失败 | I+W |
| N11 | SVD 系数快照隔离 | NaN数据拒绝、输入不改写 | I+W |
| N12 | FFT冲激输入→常数频谱1 | 空输入/非有限输入 | I+W |
| N13 | FFT逆变换恢复冲激 | 不篡改原Spectrum | I+W |
| N14 | 非线性一维求根、极小化、数值微分 | 收敛上限、参数/非有限及取消；病态矩阵专项仍待做 | I，Windows新SHA待核 |
| N15 | FIR/IIR、窗函数、重采样、卷积和相关 | 非法系数、滤波状态Reset、边界与别名 | I，Windows新SHA待核 |
| N16 | FFT、单边谱幅值/Hz频率轴 | 采样率非法、非法输入；跨频谱规模专项待做 | I，Windows新SHA待核 |
| N17 | CPU double张量Shape/Stride/广播/归约/转置/MatMul/复制切片 | 大Shape溢出、错误索引、0维长度、形状冲突、所有权 | I，Windows新SHA待核 |
| N18 | FIM Welford与FVO三线性内核实际迁移 | ROI与掩膜、16位RAW、FVO任意切面/体素边界/取消 | FIM已有W；FVO最新SHA待核 |
| N19 | 长数组/大矩阵性能与GC/p95 | 热路径分配、不同CPU | P |
| N20 | NuGet restore、28项目build、DAG、default smoke | 原生缺库/重复API/依赖反向 | W：94ba3cd |

Windows CI必须同SHA满足Restore、完整.NET48 x64 Build、Verify-Structure、Verify-OwnerDependencies、--numerics和默认ArchitectureSmoke。成功并不代表工业现场HIL、跨平台、MKL/GPU、所有算法完成；复用专业真实计算后才标记替代R。

## 2026-10-27 Numerics / FIM 统计复用闭环

- [首批Numerics Windows PASS #40502429325](https://github.com/oudream/cx-visual-x3/actions/runs/40502429325)，代码固定 SHA `76105e9`：NuGet Restore、全28工程 Windows .NET48/x64 Build、Owner DAG、`--numerics` 与默认 ArchitectureSmoke 均成功。
- 本轮真实迁移：`Foundation/Imaging/Processing/FIMImageAnalyzer.cs` 的完整ROI和掩膜ROI两处Welford统计循环改为调用 `FNURunningStatistics`；旧FIM公开签名、像素中心筛选、取消、无效像素拒绝、Histogram及五区域业务不变。
- `Foundation/Imaging/CxVisual.Foundation.Imaging.csproj` 增加向下的 Numerics ProjectReference，无新项目，无第三套统计Store。
- 保真门槛：同SHA的 `--numerics`、`--imaging`、`--volume`、默认回归与Imaging Windows真WPF测试；**替换成功状态需待该SHA的对应Windows日志核实，不能继承前一SHA的PASS**。
- 尚未完成：Volume三线性替代与性能比较，FIM性能/大图/ROI生成器副作用基线，实际现场数据回归。

## 同SHA Windows 实际证据（2026-10-27）

| 固定代码基线 | Workflow | 已核实结果与边界 |
|---|---|---|
| `76105e9` | [Numerics #40502429325](https://github.com/oudream/cx-visual-x3/actions/runs/40502429325) | 成功：完整.NET48/x64构建、Owner DAG、`--numerics`、默认ArchitectureSmoke |
| `51ba6ef` | [Numerics #40503133766](https://github.com/oudream/cx-visual-x3/actions/runs/40503133766) | 成功：FIM统计复用后，Numerics以及默认FIM/FVO/专业回归仍通过 |
| `51ba6ef` | [Imaging #40503133853](https://github.com/oudream/cx-visual-x3/actions/runs/40503133853) | 成功：全方案编译、FIM/MVI/FVO测试、真实WPF 2D图窗及3D切片工作台（含截图步骤）；原生产设备HIL不在范围 |
| `94ba3cd` | [Numerics #40503524588](https://github.com/oudream/cx-visual-x3/actions/runs/40503524588) | 成功：在上述基础上增加8条异常反证后完整编译、Owner DAG、`--numerics`与默认ArchitectureSmoke再次通过 |

**结果说明：** N01～N13的当前测试入口整体已通过，但这并不代表所有扩展精度/性能边界充分穷举。N14～N19多数仍是规划，尤其F13全类、矩阵病态诊断、FVO三线性替换、大图性能和真实工业设备尚未完成。此文档-only提交不伪装成新的代码验收。

## V1.1 扩充覆盖的正反例（提交后以 Actions 为准）

- N21 多项式 Horner/导数、分布 Quantile/Median、Histogram最大端点、FNUPrecision双容差和不可变返回。
- N22 二分求根解析√2、括号失败、非有限函数、取消；Simpson积分 x² 反向区间及奇数分段拒绝。
- N23 SVD二次多项式拟合、加权最小二乘隔离离群点、权重负数拒绝；GoldenSection优化及不收敛。
- N24 RK4解 y'=y / Math.E、起止采样、轨迹所有权及数值异常。
- N25 FIR卷积边界、IIR历史状态/Reset、线性重采样端点/单样本、Hann窗、离散卷积/互相关滞后。
- N26 单边振幅频谱、采样Hz、DC/Nyquist和频谱防篡改；仍需任意长度频谱幅度黄金基线。
- N27 F13维度/Stride/索引、RowMajor布局、复制切片及重塑、广播逐元素运算、归约、二维转置/MatMul。
- N28 同SHA Windows完整解决方案构建、Owner DAG 与 --numerics + 默认ArchitectureSmoke，两者独立记录，不能把专项 PASS 等同于全量 PASS。

**已观察到与 Numerics 无直接关系的全量回归失败**：在 `e65452e` 和 `760c71c`，`--numerics` 输出 PASS，但默认 ArchitectureSmoke 在 `VisualShapeEditingTests` 的整圆 sweep 编辑反例失败。必须由图元工程专项修复并重新核验；不借此降低 Numerics 正反例要求，也不冒称默认测试通过。

## 最新固定SHA验收补录

| SHA | Numerics专项 | Imaging/Volume/WPF | 默认架构回归 |
|---|---|---|---|
| `2629e73` | [PASS #40516716245](https://github.com/oudream/cx-visual-x3/actions/runs/40516716245) | 当时未变更FVO | PASS |
| `22bbee9` | [PASS #40516958832](https://github.com/oudream/cx-visual-x3/actions/runs/40516958832) | [PASS #40516958563](https://github.com/oudream/cx-visual-x3/actions/runs/40516958563) | PASS |

专项成功基于已记录测试，不代表未实现的多维优化、GPU Tensor、多种数值 dtype、完整性能稳定性测试已完成。上述记录为既有代码 SHA 的CI；文档补录提交不等于新的代码验收。

## Numerics 自身质量：极值与独立参考测试（2026-10-27）

本轮**只改 Foundation.Numerics 自身和独立测试/工作流**，不新增跨领域调用。

- 修正有限端点大幅差值的线性插值中间溢出；保留常规算式，有限区间的真实结果必须是有限值；区间外真溢出显式报告。
- 张量 Sum 改用 Neumaier 补偿累加，覆盖 `1e16+1-1e16` 消去场景；轴向 ReduceSum 仍需后续补偿及基准。
- 专用 `FNUNumericsQualityTests`：确定性随机普通插值、FFT与独立朴素DFT（含非2幂）、FIR与卷积、张量3D偏移/广播/非末轴切片/归约。
- Windows Numerics工作流将独立 --numerics 与默认ArchitectureSmoke 分为两个执行步骤，**总体门禁仍要求两者通过**；单步 Numerics 通过不等于全平台无回归。
- 新增代码状态在提交后以同SHA Windows CI为准，不能因源码存在就称已通过。


### 同SHA验收说明（额外构建重试）

- [8b8c160 CI #40523131245](https://github.com/oudream/cx-visual-x3/actions/runs/40523131245) 的 Windows 项目编译失败，错误来自并行图元编辑测试 `VisualShapeEditingTests.cs` 缺少 `using System.Collections.Generic;`（CS0246/CS1503），不是 Numerics 底座编译错误；专项数值测试因此未执行，不能标记通过。
- 该图元测试引用已在 `17aee42` 中由负责方修复；本次补录用于在含该修复的新提交上重新触发 Numerics 专项与全平台回归，最终结果以新CI为准。

## 2026-10-28 F13 张量无额外结果分配调用与空形状验证

- N29：多轴巨大尺寸但存在零维时，创建Shape不应虚假溢出；非空巨大尺寸必须拒绝，空Shape没有可访问坐标。
- N30：`MatMulInto` 输出复用、尺寸和别名检查、补偿求和、固定随机矩阵逐值与朴素乘法对照、零内维矩阵乘积。
- N31：`AddInto`、`MultiplyInto` 的尾轴广播、输出形状合同、输入输出别名防护、复制API一致性与零长维。
- 第一批固定代码SHA `f73b11d`：[Numerics Windows CI #40612654481](https://github.com/oudream/cx-visual-x3/actions/runs/40612654481)，完整编译、Owner检查、Numerics专项和默认回归通过。
- 第二批固定代码SHA `9c0fb6d`：[Numerics Windows CI #40612824905](https://github.com/oudream/cx-visual-x3/actions/runs/40612824905)，截至本次记录，完整构建、Owner检查、Numerics专项和默认回归各执行步骤成功，工作流最终结论须核对。

## 2026-10-28 F13 无拷贝 View 质量测试

| 编号 | 需求与测试 | 证据 |
|---|---|---|
| N32 | 从已有Buffer创建只读活View，源缓冲修改可见；元数据独立，显式CopyToBuffer隔离 | `FNUNumericsQualityTests.TensorViewQuality` |
| N33 | 非连续Transpose与半开区间Slice组合、物理Stride与逻辑GetFlat一致、空视图和越界拒绝 | 同上 |
| N34 | SumView/MatMulViews/MatMulViewsInto直接消费View；参考矩阵乘法对照、输入Owner别名禁止 | 同上 |

固定代码提交 `1aa095a`：[Numerics Windows CI #40613787782](https://github.com/oudream/cx-visual-x3/actions/runs/40613787782) **完成且成功**（.NET48/x64 全方案Build、Owner/DAG、--numerics与默认ArchitectureSmoke）。

验收仅覆盖当前double CPU View及已写正反例，不意味着非连续视图所有可能排列、多dtype/GPU或性能指标均已验收。

## 2026-10-28 F04条件数与F13游标性能（本次固定 SHA）

| ID | 实际覆盖 | 结果 |
|---|---|---|
| N35 | SVD 病态满秩例 `diag(1,1e-8)` 条件数约1e8，兼容旧无阈值求解，阈值1e9接受/1e6拒绝，非法阈值和真秩亏反例 | --numerics PASS |
| N36 | 50组固定随机多维/转置/切片 View：游标 Copy 与独立 GetFlat 参考逐值一致，Neumaier Sum一致，极值相消保持精度 | --numerics PASS |
| N37 | 独立 `--numerics-benchmark`：游标 SumView 与逐元素 GetFlat 参考比较；MatMulViewsInto 与分配式 MatMulViews 的时间和线程分配比较 | Benchmark PASS，不设速度门限 |
| N38 | 全量 ArchitectureSmoke，确保数值 API 不破坏其他 Owner | **FAIL**：F17.PIPE_BULK 子进程退出失败，重试两次均失败，Numerics 测试在此之前全部 PASS |

- 对应修复后代码 SHA `f69760ee39675ac7bbc6f9188dc0152fa75ae52e`；[Windows Numerics run #40639945222](https://github.com/oudream/cx-visual-x3/actions/runs/40639945222) 第1、2次执行：NuGet恢复、.NET48/x64完整构建、Owner/DAG、`--numerics` 和性能诊断步骤成功。**整体工作流结论为 failure**，原因是完整 ArchitectureSmoke 的 F17 命名管道跨进程子测试 `F17.PIPE_BULK child exit failure`，并非Numerics断言失败。故不得登记为全平台验收成功。
- 首次同机基准（Windows Runner，256×256转置视图求和，64×64矩阵乘法）：
  - `SumView(cursor)`：0.3551 ms/op、32 B/op；`SumView(GetFlat reference)`：0.5474 ms/op、0 B/op，校验和相同。
  - `MatMulViewsInto(reused)`：2.6119 ms/op、32 B/op；`MatMulViews(allocating)`：2.4522 ms/op、32992 B/op，校验和相同。
- 同 SHA 第二次运行观测：SumView(cursor) 0.3597 ms/op、GetFlat 0.6459 ms/op；MatMulViewsInto 2.7125 ms/op、allocating 2.6998 ms/op。**当前结论是游标求和在该环境更快，矩阵复用显著降低分配，但不能声称矩阵耗时也更快。**
- 基准中数字是单 CI Runner 观测，不是可移植吞吐保证；跨CPU、p95、长稳、大数据物理内存仍未验证。F17故障需要由Transport唯一Owner专项处理，不在Numerics内绕过全量门禁。

## 2026-10-28 F04正则化及F13非连续乘法复验

| ID | 测试内容 | 固定SHA证据 |
|---|---|---|
| N39 | SVD岭正则化：对角2×2解析解、秩亏重复列、零设计矩阵；lambda=0/NaN/Infinity、输入长度错误和防修改 | `51136d0` [#40641679779](https://github.com/oudream/cx-visual-x3/actions/runs/40641679779)，Numerics专项 PASS |
| N40 | 非连续行/列切片和转置View MatMul，独立Get2D点积参考，错误内维拒绝 | `11af443` [#40641789076](https://github.com/oudream/cx-visual-x3/actions/runs/40641789076)，Numerics专项 PASS |
| N41 | 1×2欠定宽矩阵A=[1,1]、b=[2]、lambda=2，应返回[0.5,0.5]；原非正则LeastSquares仍拒绝欠定 | `418e766` [#40641947228](https://github.com/oudream/cx-visual-x3/actions/runs/40641947228)，Numerics专项 PASS |
| N42 | 全方案构建及独立数值/性能 vs 整体ArchitectureSmoke | 上述三SHA的编译、Owner、Numerics专项和基准均PASS，但完整回归均在F17.PIPE_BULK退出失败，Workflow最终为failure |

性能：`418e766`的Runner读数为MatMulViewsInto 0.8617 ms/op、32 B/op，MatMulViews分配0.7984 ms/op、32992 B/op；SumView游标0.1901ms/op、逐点索引参考0.3716ms/op。性能数据为同Runner测量，不是p95、跨机/现场结论。

**注意：本次文档提交仅归档已验证代码SHA的证据，不可冒充文档提交自身的CI通过。F17问题由Transport所有者闭环，Numerics不绕过它。**

## 2026-10-28 F13视图运算专项扩展（N43～N47）

| ID | 用例 | 合同和测试层级 |
|---|---|---|
| N43 | Transpose/Slice后的2D、3D View跨Rank尾轴广播加乘 | 独立按坐标参考对照，Into与分配API等价 |
| N44 | 输出Owner别名、错误Shape、不兼容广播维、数值溢出及零长轴 | 异常与状态反例，拒绝写入前先验参 |
| N45 | View任意轴ReduceSum、Rank1/空轴、溢出原子性 | Neumaier求和、结果形状和归属 |
| N46 | 30组固定随机形状/数据的View广播加乘与三个轴归约 | 独立逐坐标Oracle，避免只以同内核互相比对 |
| N47 | 独立`--numerics-benchmark`对比AddViewsInto/分配式、ReduceSumViewInto/分配式 | 同机输出ms/op和bytes/op；不以波动速度阈值判CI |

验收必须以对应代码SHA的GitHub Actions实际Windows步骤为准；数值专项通过与完整ArchitectureSmoke通过分别记录。原F17.PIPE_BULK阻断不在Numerics改动范围。

## N43～N47固定代码SHA验收与运行基准（2026-10-28）

| 代码SHA | 固定Windows运行 | Numerics专项 / Perf | 全量ArchitectureSmoke |
|---|---|---|---|
| `e320018` | [#40643223710](https://github.com/oudream/cx-visual-x3/actions/runs/40643223710) | PASS：N43、N44；编译与DAG PASS | FAIL：F17.PIPE_BULK |
| `47381cd` | [#40643343028](https://github.com/oudream/cx-visual-x3/actions/runs/40643343028) | PASS：N43～N45；编译与DAG PASS | FAIL：F17.PIPE_BULK |
| `2c11eaf` | [#40643586096](https://github.com/oudream/cx-visual-x3/actions/runs/40643586096) 第一轮 | PASS：N43～N47，30组固定随机Oracle、独立基准；编译与DAG PASS | FAIL：运行时数据M1/M2预算验收；同SHA复跑已请求，结论未核实 |

单次Windows Runner数值：`AddViewsInto(reused)`0.8605ms/176B，`AddViews(allocating)`0.9735ms/525468B；`ReduceSumViewInto(reused)`0.4860ms/4272B，`ReduceSumView(allocating)`0.4927ms/6512B。数据是诊断性结果，不构成p95或不同CPU上的性能验收。不可将其他Owner的F17或M1/M2失败归为Numerics代码失败，也不可反过来把Numerics专项PASS说成整个工作流PASS。

**复跑结论补录（同SHA #40643586096，Attempt 2）**：Windows全方案Build、Owner/DAG、Numerics数值质量、固定随机Oracle和独立Benchmark再次PASS；全量ArchitectureSmoke再次在“运行时数据M1/M2验收：显式opt-in后可在100字节预算内完成双消费者与结果交接”失败。两次同SHA失败复现，属于运行时数据/机制Owner，Numerics本轮不擅自修复其他Owner代码，整体workflow仍为failure。
