# VS 全部跳过：x64 配置修复与实际生成验证

日期：2026-10-21。修改基线：master `d43fff4685208bb37cbd4816a536e689f6ddd360`。

## 问题与交付纠正

用户的 VS2022 Debug/x64 全部重新生成为 0 成功、0 失败、28 已跳过。
本次重新克隆的仓库起点为 `f86e572`；先前聊天所述本地提交 `c71f06e` 未推送，远端及新克隆都没有该修复，不能让用户重复拉取同一未修复版本。

28 个传统 C# 项目仅有共享 Configuration/Platform 默认属性，没有项目文件内显式的 Debug|x64、Release|x64 条件属性组。解决方案本身已有正确的 ActiveCfg/Build.0 映射。

本次为全部正式 csproj 补齐两个显式配置组，并在 Verify-Structure 中同时检查项目配置与两份解决方案的生成映射。保持 NET48、C# 7.3、项目 GUID、依赖及包版本。Debug 使用 full 符号，Release 使用 pdbonly。

## 本次实际验证

| 验证 | 真实结果 |
|---|---|
| Verify-Structure | 通过，28 个正式项目及配置/映射完整 |
| Verify-OwnerDependencies | 通过，无非法向上依赖或环 |
| MSBuild Debug/x64 Restore + Rebuild | 退出码 0，有既有警告 |
| MSBuild Release/x64 Rebuild | 退出码 0，有既有警告 |
| VS2022 devenv.com /Rebuild Debug\|x64 | 退出码 0；28 成功、0 失败、0 已跳过 |
| TestCenter --ui-smoke | 退出码 0 |
| git diff --check | 通过 |

Visual Studio 自身的实际日志（不是用 MSBuild 成功替代 IDE 生成）：

```text
========== 全部重新生成: 28 成功，0 失败，0 已跳过 ==========
========== 重新生成 于 22:56 完成，耗时 05.471 秒 ==========
```

日志为本机 Output/Debug/x64/VSConfigFix.IDEBuild.log（系统中文编码）、Output/Debug/x64/VSConfigFix.Build.log、Output/Release/x64/VSConfigFix.Build.log。

## 状态边界

已验证 VS 自身生成入口与本地 WPF Smoke。用户原有管理员 VS 窗口的截图/输入自动化存在激活问题，未宣称鼠标交互或人工验收通过。
本提交需实际推送并核对远端 SHA；远程 CI 结果以对应 SHA 的运行结果为准，不借用旧提交的成功记录。
已打开的旧 VS 会话仍需接受项目重新加载，或关闭并重新打开解决方案，再执行重新生成。
