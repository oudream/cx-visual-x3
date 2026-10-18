# CxVisual 旧工程对象清理与验收记录

2026-10-18，提交 `11d76a3`、`0e6dcb0`、`c51f39d`。

## 已实施

从 `Mechanism/Core` 删除 14 个旧 Cx 工程文件及 `NodeKinds.cs`，移除对应 csproj 编译项。MEMNode 不再继承 CxNode；MEMEngineeringSession 直接持有 MEMProject。Studio、Runtime.Host、Visual、ArchitectureSmoke、TestCenter 转用 MEM/MRC/MTC 工程 API。Visual 编辑器保留默认组内添加，WPF 测试改用新的 Session。

正式工程持久化由 MEMFileProjectStore 实施；仍有 schema1 XML 读入及旧 Content 内嵌 Symbol 转换逻辑。旧 Cx 工厂与兼容事务不再是正式入口。

## 验证状态

- GitHub 文件删除及提交：已核实。
- 修改文件的目标旧类引用与基本语法结构：静态检查。
- Windows .NET Framework 4.8 全解决方案构建：**尚未确认**。
- ArchitectureSmoke、TestCenter、真实 Studio/WPF、旧文件数据迁移：**尚未完成本次提交的远端验证**。
- 本次不宣称 H03～H07 全部完成。

下一步必须以最终提交 SHA 的 Windows CI 和真实测试为准，有失败则修复，不删除有效行为断言。
