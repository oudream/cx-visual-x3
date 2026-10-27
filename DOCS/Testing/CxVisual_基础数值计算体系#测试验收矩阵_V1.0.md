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

