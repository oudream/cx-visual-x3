# CxVisual 通用三维视觉工作台——P1正式GPU生命周期归属实现 V1.0

> R4整体架构下的正式代码包。所有新增类型留在既有MPW工程，TestCenter仅作为正式Host的消费者。提交/Windows/GPU证据请按本文件所在Commit核实，不冒称全部验收。

## 已实现
- MPWVolumeRenderHost管理OpenGL HwndHost创建、驱动检测、GpuEpoch、状态通知、失败恢复为WPF切面、关闭/重启、幂等释放；不引用CT3D专业程序集。
- MPWVolumeOpenGlControl仍拥有WGL/Shader/Texture，保留已验证GLSL Raymarch路径；资源释放不在TCT手工直接调用。
- TCT原直接new/add/remove/Dispose路径改为RenderHost.Enable/Disable/Fail及StateChanged；真实GPU截图仍验证IsAvailable。
- GPU切换保持MVI唯一Camera（前一批已落地），不通过WPF fallback冒充GPU成功。

## 后续
- GPU Context/ResourceSet继续从910行旧控件中提取，LUT/Uniform独立调度及Upload/PresentedRevision仍待实装。
- 完整Windows/TestCenter及NVIDIA/AMD/Intel GPU硬件实测未在代码提交时自动成立，按CI Run日志和现场配置单独标记。
