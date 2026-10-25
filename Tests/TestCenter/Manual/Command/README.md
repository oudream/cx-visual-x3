# CxVisual TestCenter — 命令中心人工操作页（首阶段实现与验收）

2026-10-25。基于 **TestCenter 统一主界面与工作区 Frame V1.1**，对应左侧「专有测试 → 命令中心测试」，必须在资源树上通过右键**创建实例**后进入中央 Tab。状态与右侧操作来自现有 `ITCTWorkspaceFrame.Commands`，不建第二套窗口、CommandCenter、MRK执行/分发、MDS数据或NUnit/TCT结果仓。

## 已编码的操作入口

| 维度 | 真实接入及当前边界 |
|---|---|
| 命令浏览 | 当前工作区实际 `MRKCommandCenter.Router.Discover` Target/Code/ID/版本、`FDCCommandDefinition` Inputs/Outputs和 Policy；远端尚无 Schema 发现协议 |
| 参数编辑 | FDC Schema 自动产生控件；基础数据、`double[]` JSON、嵌套命名 JSON；统一 `BindInputs` 范围/类型/必填/子字段预检；不接受任意CLR反序列化/脚本 |
| MDS绑定 | 真实 `MDSVariableTable` + `MRKCommandMdsBinding`；演示变量 `InputSample` → Input，结果 `Output` → `LastOutput`；更大工程的端口/Expression自动资源发现待接 |
| 命令操作 | 当前人工会话注册/注销两个明确标注的虚拟算法 Handler；实际 `Registry.SetEnabled`、`InvokeBoundResultAsync`、`Query`/协作暂停恢复取消 |
| IPC | 可配置 Server/Pipe，通过 `MRKCommandPipeClient` 的真实Windows OS管道传输；目标Host必须自己运行/授权，不能把远程失败替换成本地成功；不可宣称跨PC设备/域鉴权已经验收 |
| 观察与证据 | 记录唯一RequestId/OperationId、Stage/State、业务值和MappingResult、错误与本次人工日志（最多250条）；可手动导出TSV；**不是正式 TCT Passed/历史仓库** |
| 安全 | 演示Handler只计算数值；工作区运行中不允许直接关闭Tab或删除实例；命令取消不代表工业联锁、急停或硬件安全停止 |

## 运行与验收

1. Windows x64：构建 `CxVisual.Unified.sln` 的Debug；启动 `Output/Debug/x64/CxVisual.TestCenter.exe`，左侧「专有测试 → 命令中心测试」，创建工作区实例。
2. 右侧点击 **调用选中命令**；默认 `Input=3, Factor=2`，应显示 Completed 和 Output=6，并更新当前工作区的 MDS 变量。
3. 修改 `Input=3000` 必须在 Handler 执行前拒绝；`Options={"Unknown":true}` 也应被 FDC Schema 拒绝；不产生 Passed。
4. 切换至 Windows IPC 时必须连接真实Host；可以观察 `RemoteUnknown`，不得自动重发非幂等动作；此阶段远端Contract只使用本地已发布定义，不提供虚构的远端发现。
5. 运行独立可重复冒烟 `CxVisual.TestCenter.exe --command-ui-smoke`，覆盖已注册树/操作入口、真实WPF控件、MDS变量映射、两组参数反例、实际OS命名管道请求/Query、注销重注册与释放。
6. `CxVisual.Tests.ArchitectureSmoke.exe --command-center` 保持通过；GitHub Action `testcenter-command-windows.yml` 在对应 SHA 下输出真实Windows验证结果。
7. 另有原 `testcenter-windows.yml` 的整套 UI Automation 105s 超时既存问题，本专项 CI 并不代替它，验收时单独列出。

## 第二阶段保留

- 把远程 `Discover/Schema` 与专业 Target 发布服务接入本页；支持完整复杂集合、定制适配器与工程Port/Expression动态映射。
- 加入独立Host进程启动与停止控制（基于已有正式Host而非在TestCenter复制Server机制），断线/拒绝/跨主机的正反例矩阵。
- 进度、中间结果实时刷新、Operation多行状态列表与完整操作回放；此阶段保留最新值和单次人工会话，不伪装持久审计。
- 将新增页纳入TestCenter统一视觉与全界面UIA；原UIA死锁/超时须以对应Windows日志修复，而不是延长等待冒充成功。

## 增量：现有ArchitectureSmoke独立IPC Host可视验证

右侧操作中心新增「启动独立Windows IPC测试Host」「停止独立测试Host」；调用既有 `CxVisual.Tests.ArchitectureSmoke.exe --command-pipe-child <PipeName>`，绝不在TestCenter增加第二套命令Server。

- 测试Host的正式发布身份是 `Target=child/algorithm`、`CommandId=5183`、`Code=Calculate`、`SchemaVersion=1`、`Input/Factor` 与 `Output`。UI通过 `FDCCommandDefinition` 显示已知发布的测试合同，**不称为远端自动发现**。
- 点击「启动」后建立与本工作区绑定的独立子进程，并等待真实 `IPC_CHILD_READY`，自动切换到 Windows Pipe 目标。之后通过原 `MRKCommandPipeClient` + `MRKCommandCenter` 调用、Query及MDS写回；并非进程内假执行。
- 点击「停止」向测试子进程控制stdin请求退出；异常时回收子进程。操作记录仅说明测试Host关停，不代表PLC/设备安全停止。
- `--command-ui-smoke` 增加独立进程Host → 真实OS管道 → 命令结果/Operation Query反证。测试Host必须是编译输出目录下的正式ArchitectureSmoke程序；找不到程序就报错，绝不返回演示成功。
- 该流程是**本机双进程IPC**；跨PC网络ACL/凭据/防火墙、真实硬件Provider和Host重启后Operation持久恢复仍未验收。
