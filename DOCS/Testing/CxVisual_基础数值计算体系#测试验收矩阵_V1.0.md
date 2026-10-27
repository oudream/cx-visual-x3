# CxVisual Foundation.Numerics——TDD测试验收矩阵 V1.0

> 本文件日期2026-10-27。I=源码与测试入口已写入；W=对应SHA的Windows CI完成且成功；P=待实现。**现阶段提交之前所有W均不可先填写。**
> 入口：Tests/ArchitectureSmoke/Numerics/FNUNumericsTests.cs，--numerics 与默认回归；Windows独立workflow: .github/workflows/numerics-foundation-windows.yml。

| Case | 正向行为 | 失败/边界 | 当前阶段 |
|---|---|---|---|
| N01 | 线性插值 10→20 @.25=12.5 | NaN / Infinity拒绝 | I |
| N02 | 线性外推 t=-1=0 | 输入所有权 | I |
| N03 | 双线性中心值3 | 非有限顶点 | I |
| N04 | 三线性8顶点中心3.5 | 轴顺序/越界由专业控制 | I |
| N05 | 四样本 Welford：均值2.5、总体方差1.25 | 空集拒绝 | I |
| N06 | 样本方差5/3、最小1最大4 | 单样本未定义 | I |
| N07 | 流统计Merge与一次性等价 | NaN、Infinity，不污染状态 | I |
| N08 | 移动平均 [1,1.5,2.5,3.5] | window<=0拒绝 | I |
| N09 | 自有buffer填充，源数组不变 | 输入/输出别名拒绝 | I |
| N10 | SVD 解 y=1+2x、rank2残差≈0 | 秩亏显式失败 | I |
| N11 | SVD 系数快照隔离 | NaN数据拒绝、输入不改写 | I |
| N12 | FFT冲激输入→常数频谱1 | 空输入/非有限输入 | I |
| N13 | FFT逆变换恢复冲激 | 不篡改原Spectrum | I |
| N14 | 复线性求解/病态矩阵与容差 | 强尺度/条件数、溢出 | P |
| N15 | FIR/IIR、窗函数、重采样 | 非法系数、边界、持续状态 | P |
| N16 | FFT任意长度、频谱幅度/单位 | 采样率非法、频域NaN | P |
| N17 | 高维张量Shape/Stride/广播/归约 | 大Shape溢出、别名/共享数据 | P |
| N18 | FIM/FVO迁移同样本数值完全对照 | 16位RAW、ROI、边界/取消 | P |
| N19 | 长数组/大矩阵性能与GC/p95 | 热路径分配、不同CPU | P |
| N20 | NuGet restore、28项目build、DAG、default smoke | 原生缺库/重复API/依赖反向 | CI入口I，W待核 |

Windows CI必须同SHA满足Restore、完整.NET48 x64 Build、Verify-Structure、Verify-OwnerDependencies、--numerics和默认ArchitectureSmoke。成功并不代表工业现场HIL、跨平台、MKL/GPU、所有算法完成；复用专业真实计算后才标记替代R。
