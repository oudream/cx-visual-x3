# CxVisual 通用三维视觉工作台——C02 原始体素空间Frame首批实装 V1.0

> 2026-10-28，参照唯一总体架构与UML V2.0 R4之C02。本文只记录真实代码与验证合同，不改变DOCS/02～04待裁决的通用3D Geometry类型Owner。

## 已提交源码

- `Foundation/Volume/Spatial/FVOVolumeSpatialFrame.cs`：双精度原始VoxelCenter→Physical(mm)映射；行优先3×3 Direction矩阵、Origin、Spacing、正交性/有限性/行列式校验、右/左手性、逆变换，体素坐标与毫米物理点使用不同的强类型。
- `Foundation/Volume/Data/FVOVolumeData.cs`：真实Volume拥有唯一空间Frame；构造时`UncalibratedAxisAligned`用于显示，不代表已标定；可信来源通过`SetSpatialFrame`提交方向及原点，拒绝与原Volume不一致的Spacing。
- `Tests/ArchitectureSmoke/Integration/FVOVolumeTests.cs`：包含旋转、非等距Spacing、三轴往返、左手反转、方向正交错误、非有限数、数据与Frame间距冲突、数组防御副本等反证。

## 关键数学合同

列向量：`Physical(mm) = Origin(mm) + Direction(row-major) × diag(Spacing(mm)) × VoxelIndex`。
本实现将整数`(i,j,k)`定义为**采样点/体素中心**，即第一个体素中心`(0,0,0)`与Origin重合。像素边界、CAD B-Rep单位、CT机架坐标、外部规范若采用`i+0.5`必须在导入环节显式适配，禁止静默混用。
方向允许det=+1或-1的正交轴向（包含镜像源），会保留是否右手的信息，不会把负Spacing冒充镜像方向。

**未标定（IsCalibrated=false）是明确状态，不可作为已知绝对工件坐标或工业CAD装配位置**。当前RAW文件名只有Spacing，没有可靠Origin/Direction；基础显示比例可用，专业绝对空间对齐前必须显式提供可信源标定。

## 仍待完成

- SourceLease、原始数据版本/来源签名、CT设备标定元数据/方向读取、Scene和CAD Frame链、标准件误差/不确定度、FGE通用3D刚体变换以及DOCS/02～04正式Owner逐项核验。
- 本轮只为算法提供准确双精度坐标合同，未宣称Surface、拟合、配准、GD&T或CAD对比已经实现。
- Windows Release x64、ArchitectureSmoke --volume、实际显卡/工业计量验收须以对应SHA和设备日志为准；本文不把设计与编译/硬件实测混写。
