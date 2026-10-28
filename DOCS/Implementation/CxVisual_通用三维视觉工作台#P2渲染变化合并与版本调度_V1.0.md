# CxVisual 通用三维视觉工作台——P2 渲染变化合并、Revision及ContextEpoch实现 V1.0

> 实施日期：2026-10-28。基线：总体架构与UML V2.0 R4。本记录为实装追踪，不另立第二架构总纲。

## 一、正式代码

| Owner | 代码入口 | 实际内容 |
|---|---|---|
| MPW | `Volume/Rendering/OpenGl/MPWVolumeRenderScheduler.cs` | `MPWVolumeRenderDirty`六类失效；Dispatcher Render优先级合并；Revision、AppliedRevision、ContextEpoch；显式Flush与Dispose |
| MPW | `Volume/Rendering/OpenGl/MPWVolumeRenderHost.cs` | GPU Host持有调度器；上下文销毁/启停后丢弃旧代次回调；提供QueueUpdate/FlushUpdates；异常仍由正式Host进入Fallback |
| TestCenter（消费者） | `Tests/TestCenter/Manual/Volume/TCTManualVolumeGpu.cs` | CT3DRenderingChangeSet转换为MPW通用Dirty，不增加MPW→CT3D依赖；Uniform/Scene合帧、LUT显式立即上传 |
| ArchitectureSmoke | `Integration/MPWVolumeSourceTests.cs` | 检查合帧、修订和代次，非法Dirty拒绝；保留旧GLSL/WGL源码检查 |

## 二、约束及兼容

- CT3D仍唯一负责专业渲染参数、LUT重算判断；MPW仅管理设备资源失效和渲染时机；TestCenter不持有调度器的权威状态。
- `ApplyParametersWithChangeSet`同参数不产生新的失效请求；RenderHost按当前GPU资源、ContextEpoch阻止旧回调操作失效纹理。
- 普通GPU材质/可见性变化被合并；颜色/阈值LUT操作保持即时刷新，兼容已有实机验收对上传计数的断言。
- 未创建新项目、第二套GPU backend或新命令中心，未触碰Raymarch GLSL。

## 三、状态与待测

> 代码已提交，完整Windows CI/真实GPU验收以该代码对应SHA的实际工作流输出为准，不能提前宣称通过。

- 需核对.NET Framework 4.8 Release x64、ArchitectureSmoke --volume、WPF真实场景、TestCenter全量。
- `AppliedRevision`标识在当前调用链中成功应用的变更；上传完成与真实GPU画面已呈现、Framebuffer截图、显卡设备可用是不同证据层。
- 后续仍有Context/ResourceSet进一步拆分、SourceLease与显存预算、大数据LOD、GPU设备失效主动恢复、工业数据/真实硬件验收。
