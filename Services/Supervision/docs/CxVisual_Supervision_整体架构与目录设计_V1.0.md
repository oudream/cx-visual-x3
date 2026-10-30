# CxVisual Supervision——整体架构、目录与查询合同 V1.0

> 日期：2026-10-29。文档与 Go 规范、Web 规范共同放在 `Services/Supervision/docs/`；本设计区分已经实装的 V1 查询与未来管理能力。

## 1. 总体裁决

- **Monitor + Daemon，两个 Go 进程**，不新增专门的 H5 后端进程。Monitor 承接 Gin HTTP/H5、状态查询与聚合；Daemon 执行被监管程序的启动、停止、恢复。
- Monitor 当前的运行态只保留原有一份；新增的 `internal/supervision` 只负责快照转换和聚合，不建立第二套持久化状态。
- UDP 报文、SQLite 表、Program ID、原始配置键及旧 `GET /programs_status` 必须保留兼容。
- H5 和 CxVisual Studio 作为 API 客户端，不直接执行 shell，也不直接传入原 UDP 控制报文。
- **现阶段只实现只读查询**。任何启动、停止、重启、系统设置必须经鉴权、授权、审计、结果回执和隔离测试后才能开放。

## 2. 依赖与对象关系

~~~text
浏览器 Vue 3 / TS / Element Plus / ECharts
                   |
                GET API
                   v
          cmd/monitor (Gin)
                   |
         internal/httpapi
        (路由 / 参数 / DTO)
                   |
       internal/supervision
       (只读快照 / 总览)
                   |
  原 Monitor 运行态 map（唯一状态源）
                   ^
                   |
       monitorc + channels（UDP）
                   ^
                   |
          Daemon + program
         (真实进程守护 / DB)
~~~

`security` 目前只有只读 HTTP 的缓存/框架限制响应头，**不是登录、用户授权或操作审计的完成实现**。

## 3. 推荐目录与当前实现

~~~text
Services/Supervision/
├── cmd/
│   ├── monitor/                # 已有 Monitor 装配与旧接口
│   └── daemon/                 # 已有 Daemon
├── internal/
│   ├── monitorc/               # 已有 UDP 消息协议
│   ├── daemonc/                # 已有守护逻辑
│   ├── program/                # 已有模型 / SQLite
│   ├── supervision/            # V1 只读状态、统计
│   ├── httpapi/                # V1 查询路由及 httptest
│   └── security/               # V1 只读 HTTP 安全头
├── common/
│   ├── channels/
│   ├── models/
│   └── utilities/
├── web/
│   ├── src/
│   │   ├── api/                # 唯一查询客户端
│   │   ├── components/         # CPU 图表等
│   │   ├── layouts/
│   │   ├── views/              # 总览 / 程序监控
│   │   ├── stores/             # Pinia
│   │   ├── router/
│   │   ├── types/
│   │   ├── utils/
│   │   └── styles/
│   ├── package.json
│   ├── vite.config.ts
│   └── index.html
├── deploy/
├── docs/
├── go.mod
└── go.sum
~~~

不建立没有真实代码的 `gateway`、`logs`、`settings`、`composables` 空目录。`internal` 的新增包都属于 Monitor 进程内部实现，不代表增加 Go 进程。

## 4. 实际查询接口合同

| 方法 | 路径 | 数据和含义 |
| --- | --- | --- |
| GET | `/programs_status` | 兼容旧结构 `{status:200,params:[...]}` |
| GET | `/api/v1/health` | HTTP 进程响应、UTC 时间；不是全部程序健康 |
| GET | `/api/v1/overview` | `data:{total,running,stopped,heartbeatReporting,cpuPercent,memoryRssBytes}`、`generatedAt` |
| GET | `/api/v1/programs` | 以 ID 升序的 `Program` 快照数组和生成时间；`id` 使用十进制字符串以免 JavaScript 丢失 int64 精度 |
| GET | `/api/v1/programs/:id` | 单程序查询，非法 ID=400，不存在=404 |

字段 `*AtMs` 是 Unix 毫秒；`memoryRssBytes` 是字节。`heartbeatReporting` 只表示有心跳记录；CPU 为各程序采样值，合计不等于整机 CPU。查询失败保留原快照但显示“已失联/旧数据”。不提供任何写操作。

## 5. 前端与发布

- V1：Vue 3、TypeScript、Vite、Vue Router Hash、Pinia、Element Plus、ECharts。
- 前端只提供两个有真实 API 的页面：总览、程序监控（搜索/状态/CPU/RSS/心跳）。
- 开发时 Vite 代理 `/api` 到本地 Monitor，生产构建产生 `web/dist/`，复制到 Monitor 的 `<WorkDir>/assets/comm/`。
- 构建静态页由原 Gin 服务提供 `/assets/comm/index.html`；生产不必运行 Node.js。
- 前端与 Go 各有测试：Go `go test`、Windows/macOS/Linux 编译；Web `typecheck`、Vitest、Vite build。真实现场运行需要另行验收。

## 6. 明确未完成项

- 用户登录、角色授权、操作审计，控制命令闭环与安全启停；
- SSE/WebSocket 真实推送，事件和历史日志持久化；
- 多设备集中管理、多租户 Gateway，前端页面扩展；
- 实际工厂环境的运行/故障/设备验收。

以上均不得以“目录存在”或“代码编译通过”冒称功能完成。

## 7. 12 大功能域与 P0 查询扩展

完整功能矩阵及分阶段验收见 [12 大功能域与实施验收矩阵 V1.0](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)。本次新增事件只读历史接口、原 Monitor 事件文件限量读取、程序详情和双主题前端。事件查询仅本机可用，未来远程访问必须先完成身份认证、授权和敏感信息脱敏；未新增独立事件数据库、Gateway 或第三个常驻进程。

## 8. 原事件异常提取与安全边界（2026-10-30）

为 S08 增加只读 `/api/v1/alerts/history`，底层 `internal/alerts` 直接复用 Monitor NDJSON 日志，不建立第二套状态或数据库。识别规则只匹配原 `EventMaxError`、`EventMaxCount` 和明确写有“启动失败”的 `EventStart`，普通退出、登录和心跳不视为告警。

仅允许本机查询，拒绝远端地址、外部 Host、转发头与跨域 Origin。返回的 `kind=historical-failures` 明示“历史故障观察”而非“当前活动告警”。未来确认、恢复、角色与审计必须有真正的数据模型与安全闭环，再开放对应接口。

## 9. P0 新 API 最小访问控制（2026-10-30）

**已提交代码，不等于完整远程认证**：`internal/httpapi.Register` 已为 `GET /api/v1/overview`、`/programs`、`/programs/:id` 增加原有 `security.LocalOnly()`；原先 `/api/v1/events`、`/api/v1/alerts/history` 已受该限制；`/api/v1/health` 仍仅返回 Monitor HTTP 存活而不调用进程快照。禁止访问请求在进入实际 SnapshotProvider 前被拒绝，响应 403。无新增接口、进程和认证服务。

`LocalOnly` 目前要求直接来源为 loopback、Host 为 loopback/localhost、没有 Forwarded/X-Forwarded-For/X-Real-IP 和 Origin；这是一项**保守的临时本机访问门禁，不是 RBAC 或 TLS 客户端认证**。用户在另一台电脑直接访问 H5 时，静态页面可能可加载，但这些敏感 API 将显示 403；**这是安全策略变化，不能在前端绕过**。开发期 Vite 代理默认回环调用可继续使用，正式远程访问须新建经审查的认证授权方案并在 Monitor 和最终 Owner 处防护，切勿依赖反向代理伪造 localhost 或删除转发头来绕过门禁。

**历史兼容留存的风险**：`GET /programs_status` 继续保持旧调用方依赖的返回与访问合同，若 Monitor 监听到外网卡，它仍可能被未认证访问；启动时 `httpListenMayBeRemote` 对非回环/未知 HTTP Host 记录显式安全告警。运维须通过 `sys.config` HTTP host 绑定本机，或网络/主机防火墙 ACL 保护所有 Monitor 端口（包括旧 UDP 控制面）。当前并不具备远程写操作、登录、RBAC、审计、设备安全控制；只能把受控本机只读范围视为现有实现。

**回归位置**：`internal/httpapi/routes_test.go` 检查全部三个敏感路径及拒绝时零 Provider 调用；`cmd/monitor/web_security_test.go` 验证监听暴露风险；`web/src/api/client.test.ts` 验证 403 与真实 Monitor 连接/服务错误的显示区别。自动化 Go/TS 测试实际通过情况须到 GitHub Actions 针对固定 SHA 查询。
