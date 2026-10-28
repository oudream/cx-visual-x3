# CxVisual 通用三维视觉工作台——P0原值安全与MVI统一相机首轮实现 V1.0

> 日期：2026-10-28；架构依据：`DOCS/Architecture/CxVisual_专业视觉体系#通用三维视觉工作台——整体架构与UML_V2.0.md` R4；本轮提交SHA以此文件所属Git提交为准。未改变现有28项目/F/M/P Owner，未引入第三方SDK。
>
> **实施范围：** FVO C01原值安全 + MVI/MPW C04统一3D相机。GPU Raymarch算法、GLSL和Shader未重写；CAD/Surface/计量产业闭环仍未实现。

## 实现内容
1. FVO `SetRawBuffer` 接管前验证来源标量类型、ByteLength和已关闭状态，原子置换，重复同一句柄幂等，拒绝不匹配长度且不破坏旧数据。保留原来的预览读数API以兼容浏览。
2. `FVOUnmanagedRawVolumeBuffer.ReadValue` 限界 + `DangerousAddRef/DangerousRelease` 生命周期保护，避免并发Dispose期间取得悬空指针。原始资源的多使用者Lease尚未实现，关闭旧Volume前仍应取消在途任务。
3. 新增严格计量读取API：`FVOVolumeData.TryGetOriginalScalar` / `GetOriginalScalarRequired`。返回失败/异常时禁止利用R8显示预览补数；兼容旧`GetScalarByOriginalIndex`仍允许已有显示用途的回退。
4. MVI已有`MVIVolumeViewState`直接拥有`MVI3DCameraState`，统一弧度yaw/pitch、target、distance及Revision；Changed事件每个真实修改才发送。新增文件由现有MVI.csproj显式包含。
5. MPW的WPF切面和OpenGL HwndHost均增加`BindCamera`，绑定后轨道旋转/缩放/平移只写MVI，渲染器保留私有矩阵缓存。解绑删除订阅，GL窗口销毁也解除引用。TCT只进行正式控件绑定/测试调用，不新建相机数学。
6. ArchitectureSmoke的FVO/MVI回归加入相同RAW接管、ByteLength失败、Byte/UInt16/Float32原值、R8-only拒绝、Camera Revision、事件幂等、非法姿态等正反例。

## 线程/精度/版本的限制
- MVI`MVI3DCameraState`是一会话UI线程内的变化真源，不宣称跨线程可随意写；依然由实际宿主Dispatcher保持顺序。
- MVI Camera坐标是标准化显示场景，不等于FVO physical mm或工业计量CAD Frame；C02空间Frame/Direction/单位合同仍待对应Owner核实。
- `TryGetOriginalScalar`严格精度依赖仍可访问的原始RAW（Byte格式可用原始字节缓存）；只读安全锁只能保护单次Read，完整分析任务仍需要SourceLease。
- 原WPF无绑定时保留其旧默认相机行为；现有TCT使用会话Camera绑定后WPF和GL共用一个姿态，gpu失败回退不丢失相机真源。

## 验收（必须根据最终Actions记录更新）
- 本包在提交前进行了源代码标记与依赖核查，**不应宣称已编译或自动测试已通过**。
- 主要Windows专项：`.github/workflows/imaging-foundation-windows.yml` 应执行Release x64、ArchitectureSmoke --volume、真实WPF空间切片；另查TestCenter Windows .NET48。
- 实际NVIDIA/AMD/Intel真实GPU、显存、工业CT标定/计量、GPU画质/性能尚待现场验证。

## C01 SourceLease补充（2026-10-28）

- 正式新增`Foundation/Volume/Memory/FVOOriginalScalarLease.cs`；`FVOVolumeData.AcquireOriginalScalarLease()`提供针对已加载原始RAW（Byte、UInt16、Float32）的只读租约。
- 租约取得时`DangerousAddRef`固定原始SafeHandle，在`FVOVolumeData.Dispose()`或体数据切换后仍可读取，不重新打开源文件、也不复制大体积缓冲；`Dispose`归还引用并带最终回收保护。
- `ReadScalar(x,y,z)`严格检查索引与租约状态，并持有获取时的空间Frame快照；任务应通过`using`归还租约，不能与`ReadScalar`并发调用`Dispose`。
- 只有预览或未取得真实RAW时拒绝创建租约，不能静默恢复8位预览为专业原值；完整Byte托管缓存仍可使用已有严格标量读取API，但本RAW租约仅针对SafeHandle来源。
- `FVOVolumeTests`新增释放Volume后继续读取UInt16末像素1511、索引错误、两次Dispose、已释放后拒绝访问、无原值拒绝租约的反例。
- 完整Windows Release x64与工业大RAW长时间并行计算验收仍待同SHA CI及实际场景验证；不冒称跨任务并发Dispose安全或超大数据性能已经通过。
