# Decision 工程物理合并与 Runtime 解耦余项（2026-10-29）

## 一、执行范围与实际变更
- 以原 master `91a12829a264f7707ec26957cc39e6f29fe75f80` 为基线，移除空的 `Mechanism/Logic`（仅项目文件、AssemblyInfo），将 `Mechanism/Orchestration` 的真实 MOC/MDC 源码保留完整移入 `Mechanism/Decision`。
- 新工程 `Mechanism/Decision/CxVisual.Mechanism.Decision.csproj`，复用原 Orchestration GUID `{6E9A7006-3AD6-5F5B-A452-1C89847495A0}`；AssemblyName 与 RootNamespace 更新为 `CxVisual.Mechanism.Decision`。
- 更新 Unified.sln、Studio、Vision、ArchitectureSmoke、TestCenter 工程引用及相关 Windows 工作流；旧 Logic 独立引用删除。
- 统一项目清单与目录：先前 Lifecycle 独立工程已不存在，历史 28 项目当前实际为 27；本轮合并后为 **26（F8/M7/P6/A2/I1/T2）**。

## 二、兼容性与尚未完成
- MOC/MDC 现有公开 C# 命名空间与方法签名原样保留，避免工程物理迁移同时破坏源码调用；这不代表永远保留过渡别名。
- 为保全原编译器、运行 Binder、SourceMap、Loop/Foreach 和工程加载行为，`Decision.csproj` **仍临时 ProjectReference Runtime**。当前未建立 Runtime→Decision 反向项目引用，因此没有因本轮改名引入一个新的工程依赖环，但尚未满足最终 **Decision 纯静态 → Runtime 运行绑定** 的目标边界。
- 尚未执行真实 Windows 构建和设备验收；自动验证状态以本次提交后相应 Actions Run 为准。不要把仅目录合并写为 P0～P3 解耦完成。
- 后续必需：拆 MOC 对 MRK 执行委托的静态耦合，改为版本化纯 Plan 与运行 Loader 单向消费；维持历史运行语义、SourceMap、保存/重开及专业测试结果一致。

## 三、验收门禁
1. GitHub 目录不存在旧 Logic / Orchestration 正式工程，解决方案、项目清单中只剩一个 Decision，且 ProjectReference 文件全部可解析。
2. Windows 完整 x64 Release 构建、ArchitectureSmoke 默认与 `--decision`、Studio/TestCenter/ModuleCenter 相关工作流成功；具体结论按 Run 记录填写。
3. 完成运行依赖迁移后再宣告架构完全闭环，拒绝以目录改名代替真实无环职责拆分。
