# CxVisual Supervision——Web 前端架构与编码规范 V1.0

> 日期：2026-10-29。范围：`Services/Supervision/web/`。与 [整体架构 V1.0](CxVisual_Supervision_整体架构与目录设计_V1.0.md)、[Go 规范 V1.0](CxVisual_Supervision_Go编码与工程规范_V1.0.md) 配套。**本版只读查询已实装，权限与操作控制尚未实装。**

## 一、技术基线

| 领域 | 选型 | 硬约束 |
| --- | --- | --- |
| 页面框架 | Vue 3 Composition API + `<script setup>` | 组件按职责组合，禁止伪类继承 |
| 语言 | TypeScript `strict` | API DTO 有明确类型，不添加隐式 `any` |
| 工程 | Vite + npm | 前后端独立编译，生产无 Node 长驻 |
| 控件 | Element Plus | 表格、标签、筛选、反馈优先复用成熟组件 |
| 状态 | Pinia | 一个当前快照 Store，禁止创建第二套监控运行真源 |
| 路由 | Vue Router Hash | 配合现有 Gin 静态路径，不依赖 SPA 回退 |
| 图表 | ECharts | 显示真实 CPU/RSS，不造假历史趋势 |
| 测试 | Vitest + vue-tsc | 类型检查/测试/构建为 CI 门禁 |

## 二、目录与依赖

- `src/api`：唯一请求层，统一 HTTP 错误、超时及请求类型。
- `src/types`：JSON API DTO；不把 Go 的数据库结构直接当成前端模型。
- `src/stores`：Pinia 持有当前程序快照、统计、刷新时间和错误信息。
- `src/views`：实际业务页面，只依赖 Store/组件，不在各页面直接 `fetch`。
- `src/components`：可复用图表和展示组件，通过 props 接收数据。
- `src/layouts`：导航、标题、连接状态、刷新、响应式布局。
- `src/router`：统一导航；V1 只有真实可用的总览/程序监控页面。
- `src/utils`：纯格式化功能，配套 Vitest。
- `src/styles`：暗色工业主题及自适应布局。
- `public`、`composables`、其它专题目录只在具备真实代码时新建，不创建空工程。

Vue SFC 用 PascalCase，TS 变量与方法用 camelCase；常量与 API path 按语义命名，避免硬编码在多处。浏览器不能持有生产密钥。

## 三、查询契约与数据语义

1. 新页面用 `/api/v1/`；原 `/programs_status` 保留给旧客户端。
2. 刷新默认每 5 秒；后台标签页停止主动刷新；禁止请求无限堆积，每次网络请求设置超时。
3. 失败时**显示错误**、保留上一份快照并标记可能过期；不将“请求失败”渲染为“无程序”。
4. `heartbeatAtMs`=0 显示未上报，不能自动解释为故障；`running` 只表示当前 Daemon 状态，不代表业务就绪。
5. CPU 合计不是整机 CPU；RSS 为字节。单位/采样时刻/来源要让操作员读懂。
6. 只允许展示真实数据；单测 Fixture 仅用于测试，不得编入生产 UI。
7. 未来 SSE/WS 必须有服务端、连接重试/心跳/背压及错误状态合同才可替代轮询。

## 四、操作、安全与可用性

- 默认深色工业界面，以查状态、定位程序、看资源为主；兼容 PC 与平板/窄屏。
- 每个页面必须有加载、空态、出错及恢复路径；可交互按钮必须调用真实功能。
- 在 Go 实际实现登录、角色授权、审计、命令回执和危险操作负面测试**之前，不增加“启动/停止/重启”按钮**。
- 前端隐藏按钮不构成权限控制；鉴权必须后端实施，跨域和生产监听要限制可信来源。
- 设备消息和日志默认文本转义，不使用不可信 `v-html`。
- 不使用 `VITE_*` 存储生产密钥；任何真实 Token 不能进入静态 JS 包或源码。
- 对 API 异常，页面明确区分“Monitor HTTP 已连接”“程序运行中”“业务健康”。

## 五、开发与发布

~~~bash
cd Services/Supervision/web
npm install
npm run typecheck
npm run test
npm run build
~~~

- 开发 `/api` 默认转发 `http://127.0.0.1:7811`，可配置 `VITE_MONITOR_PROXY`。
- 发布构建目录 `web/dist/` 的**内容**放入 Monitor 工作目录 `assets/comm/`；Vite 基路径固定为 `/assets/comm/`。
- H5 由原 Gin 托管；打开 `/index.html` 时沿用现有转发到 `/assets/comm/index.html`；Vue Hash 导航避免路由刷新失败。
- 本期前端测试覆盖查询格式化与实际构建；浏览器端功能、网络超时、窄屏视觉和真实设备体验需单独验收。

## 六、变更评审

新增页面必须有真实 API/状态/错误/单测支撑；新增写操作先经架构和安全裁决。API 不兼容变更使用新版本路径；依赖升级应锁定版本并完成前端构建。任何只构建未真实运行的结果，只能记作构建验收。

**本规范自提交起对新增及实质修改的 Web 源码生效，历史代码按兼容性和测试渐进治理。**
