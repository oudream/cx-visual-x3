# 02 · device_communicator：IoT设备模型、通信运行、边缘管理技术资产档案（V1.0）

> **唯一主文件**：`DOCS/Codebases/02_device_communicator.md`。本档案与 [01_ccxx.md](01_ccxx.md)、[03_gcl3.md](03_gcl3.md) 并列，属于CxVisual“七仓代码资产”分析。  
> **旧仓库**：[oudream/device_communicator](https://github.com/oudream/device_communicator)；**核查基线**：master，[18b5313d8c0c350f3a8cc723f57120cf9e551156](https://github.com/oudream/device_communicator/commit/18b5313d8c0c350f3a8cc723f57120cf9e551156)（2024-04-11 UTC）；**核查日期**：2026-10-14。旧仓可能为私有仓，访问源码链接需要授权。  
> **目标主仓**：[oudream/cx-visual-x3](https://github.com/oudream/cx-visual-x3)；本次撰写参照主仓 master 的 `3a0790d891757f5e3dad43aebdfc67b30b2280da` 版本、`DOCS/ProjectCatalog.csv`、`DOCS/DirectoryLayout_V1.1.md` 和 `DOCS/04_CxVisual Foundation与Mechanism 统一架构与对象API设计规格 V6.4.xlsx`。后续阅读须重新核查主仓HEAD与最新规格。  
> **分析范围（已约定方案A）**：深入分析 `hy/` 主体系及 `common/` 基础实现；`tk/`、`lm/` 和历史/示例实现只作互补与差异分析；不将vendored依赖、示例与历史分支等同于当前核心实现。  
> **档案性质**：旧系统源码事实与工程经验、复用分析、讨论提案。**本文不是架构定案、没有批准新增类、没有修改28项目结构，也不是迁入CxVisual后的编译、测试或实机验收报告。** 任何设计均可在后续逐项目深入讨论中修正。  
> **历史经验口径**：项目负责人确认 device_communicator 曾投入一年多开发并用于多个实际项目；该工程经历应得到尊重，但不等同于本轮在新平台完成集成验证。  
> **保密声明**：旧仓部分说明和部署样例含明文测试账号/密码。本档案**不转载凭据、内网地址、密钥或敏感配置值**；迁移/公开文档前须清理并评估凭据轮换。

## 00. 快速阅读：核心结论与证据标记

### 00.1 一分钟认识

**device_communicator 是一套以Go实现的IoT/边缘通信与管理系统，价值不仅在Modbus等协议，更在“型号/设备/测点 → 点表/通道/协议 → 通信实例 → 实时数据/事件/控制 → 监控/守护”的贯通链路。**

相对 [01_ccxx.md](01_ccxx.md) 的通用底座与 [03_gcl3.md](03_gcl3.md) 的分布式SCADA，本档案重点保留IoT特有的**设备产品模型、属性/测点、点表模板、设备与通道关系、事件关联、控制入口、平台侧接口与运行监控**，避免三份档案相互重复。

**本次最有价值的三项资产**：

1. **模型到点表的可配置关系**：型号测点与设备测点、协议寄存器/地址可以分离；设备与通道存在独立关联。
2. **Communicator运行单元**：一个驱动单元明确组合一种协议、一份通道配置和一份点表；Manager统一处理创建、停止、配置更新与控制消息。
3. **运行状态和故障管理**：通信进程、Monitor、Daemon原先分工明确；其监控、心跳、恢复、日志思想可转化为CxVisual模块化机制，**不必照搬原进程拓扑**。

### 00.2 证据级别与阅读规则

| 标记 | 精确定义 |
|---|---|
| **S：源码事实** | 在固定commit中已查看对应源码声明或函数流程；证明“代码这样写”，不自动证明运行成功 |
| **H：历史事实** | 项目负责人说明的开发/应用经历，不能取代本轮技术验收 |
| **I：分析推断** | 从多段源码推得的关系、问题或抽象，尚需运行和反例检验 |
| **D：设计提案** | 对CxVisual未来公共机制/专业实现的候选建议，**不是定案** |
| **V：已验证** | 有可复现构建命令、测试结果或现场证据；**本档案本轮未赋予V状态** |

所有下文中的“建议Owner”“设计关系”“三进程方案”都是 **D**，不得与V6.4已经列出的候选名称混同为新平台已实现或已批准新增。

### 00.3 阅读路线

| 关心什么 | 章节 |
|---|---|
| 主要目录、应用和源代码入口 | 01～02 |
| 设备模型、测点、产品属性和实体关系 | 03 |
| Excel点表模板与装载/运行计划 | 04 |
| Channel、Protocol、Communicator | 05 |
| 实时值、控制、命令、事件 | 06～07 |
| Monitor、Daemon、Web管理和部署 | 08～10 |
| 旧实现风险、改进机会 | 11 |
| **CxVisual F/M/P归属与设计候选** | **12～14** |
| 小闭环与迁移验收、后续深化问题 | 15～16 |
| 源码索引 | 17 |

## 01. 系统总体架构与代码范围

### 01.1 仓库实际目录

| 源码区域 | 本次看到的主要功能 | 本档案权重 |
|---|---|---|
| **`common/channels/`** | TCP、UDP、Serial、HTTP、MQTT客户端/服务端等传输 | 深入：与hy运行链直接相关 |
| **`common/modbus/`、`common/hj212/`、`common/iecp5c/`** | 规约相关基础实现和协议包 | 深入到机制与入口；不宣称逐功能码验收 |
| **`hy/common/model/`** | DeviceBase、DeviceAttrBase、Channel、协议反射、IoT/Comm设备、实时数据模型 | **重点** |
| **`hy/common/sheet/`** | ProductAttr/Event/Link、DeviceAttr/Event/Link、Excel字段规则 | **重点** |
| **`hy/common/communicatorc/`** | Channel/Device服务、Sheet装载、CommunicatorManager、RT缓存、MQTT控制入口 | **重点** |
| **`hy/common/monitorc/`、`hy/common/program/`、`hy/common/daemonc/`** | 监控消息契约、程序定义、守护执行 | **重点** |
| **`hy/communicator/`** | Modbus、Modbus TCP、IEC104、DL/T645-2007、HJ212、MQTTO等驱动入口 | 具体专业实例核查 |
| **`hy/monitor/`、`hy/daemon/`** | 运行监控、状态缓存、进程守护、资源统计 | **重点** |
| **`hy/http_server/iot/`** | IoT设备、属性、通道、服务管理Web API | 深入：业务管理入口 |
| **`tk/`** | 轻量网关、Web及ARM交叉编译/部署示例 | 补充 |
| **`lm/`** | Gin + Vue后台基础、权限、日志、任务、监控等 | 补充，不等于全部功能已验收 |
| **`vendor/`、`backup/`、`hello/`** | 第三方依赖、历史版本、示例 | 仅必要时溯源 |

仓库 [go.mod](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/go.mod) 声明 module `gogo`、Go 1.19，以及Excelize、Gin、GORM、MQTT、Redis、InfluxDB、Modbus、gopsutil等依赖。这证明**存在依赖与工程组织**，不代表所列组件的全部功能都已用于生产闭环。

### 01.2 旧系统的逻辑业务链

~~~text
型号/产品测点/通用属性
        │                         物理设备/资产信息
        ├───────> 设备测点 <─────────────┤
        │               │
        ▼               ▼
型号点表模板       通道设备关系 / 通道测点关系
        └──────────┬───────────┘
                   ▼
              通道点表Excel
            (attr/event/link)
                   │
               协议解析/校验
                   ▼
          Communicator运行单元
       (Protocol + Channel + Sheet)
                   │
          采集、转换、控制、事件
                   ▼
        RT缓存 / MQTT / Redis / 下游
                   │
       HTTP管理、Monitor、Daemon
~~~

这是**职责和数据依赖的合成示意**，并非某一函数的完整真实调用图。部分产品模型与设备资产的持久化管理位于不同服务模块，不可直接断言是同一进程内事务。

### 01.3 原进程分布与当前CxVisual目标不同

旧Go体系有通信器进程、Monitor、Daemon、HTTP/数据转发/存储等独立运行部件，详见 [hy/monitor/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/main.go) 和 [hy/daemon/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/daemon/main.go)。

**CxVisual当前讨论约束则是三个主进程**：编排进程、运行进程、AI服务进程；总线和终端服务是可在编排/运行两端分别实例化的模块，监控展示放在编排端，运行端仍独立产生健康与故障事实。此为用户明确给出的**目标方案背景**，不是对旧Go进程布局的描述，也尚非实现验收。

## 02. 设备模型相关概念：先按旧系统原语义理解

旧仓 [hy/hy.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/hy.md) 已经显式解释了**模型、模板、测点、设备模型、设备模型测点、点表模板、物理设备**，值得作为历史需求来源。

- **模型（Model）**：现实对象的数字化描述，可表达为对象、参与程序计算或存储于数据库。
- **模板（Template）**：人工维护/程序读写的Excel表；重点是交换与批量编辑形式。
- **测点（Point）**：设备属性中需要采集/监测/控制的部分，区分遥测、遥信、遥控、遥调。
- **设备模型（Product/Device Model）**：同类设备的型号定义，关联测点与资产属性。
- **设备模型测点**：同类设备共用的测点编码、名称等。
- **点表模板**：从设备模型测点导出，再补全规约地址/寄存器等字段；生成实际设备点表。
- **物理设备**：现场管理对象；可因计量、保护等不同用途拆成多个逻辑管理设备。

**需要保留的思想**：型号/模板/物理实例/测点/协议绑定分离，并通过稳定关联形成完整系统。**不应照搬的假设**：每个行业模型一定代表“物理设备”；Vision算法和CT扫描方案不能被迫具有通道、遥测点或资产序列号。

## 03. 设备、测点、型号与关联关系——核心资产

### 03.1 已核查主要数据类型

| 类型 | 关键字段或结构（S） | 表达的边界 |
|---|---|---|
| **DeviceBase** | ID、ProductCode/ProductId、ParentDeviceCode、Code/Name、DeviceType、SheetCode、CabinetCode、SwitchCode | 设备实例、型号引用、父子/柜体归属等业务身份 |
| **IotDevice** | 嵌入DeviceBase，含TenantId | IoT侧多租户设备实例 |
| **CommDevice** | 嵌入DeviceBase，含SyncIotTime | 通信侧设备记录及与IoT侧同步痕迹 |
| **GeneralAttr** | Code、Name、DataType、Min/Max、UnitName、GeneralTags | 与具体设备无关的通用属性词汇 |
| **DeviceAttrBase** | DeviceId、ProductAttrId、GeneralAttrId、Code/Name、类型、单位、范围、换算、零死区、偏移 | 设备属性/测点与型号或通用属性关系 |
| **IotDeviceAttr / CommDeviceAttr** | 嵌入DeviceAttrBase；通信侧有SyncIotTime | IoT/通信侧属性记录 |
| **Channel** | ProgramId、ConnectMode、TCP/串口/MQTT配置、ReadInterval等 | 通信连接**定义**，不是已经打开的Socket |
| **ChannelDevice** | ChannelId、DeviceId | 一个通道和设备的关联 |
| **ChannelDeviceAttr** | ChannelId、DeviceId、DeviceAttrId | 通道与某设备测点的关联 |

代码证据：[device_base.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_base.go)、[device_attr_base.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_attr_base.go)、[general_attr.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/general_attr.go)、[iot_device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/iot_device.go)、[comm_device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/comm_device.go)、[channel_device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel_device.go)、[channel_device_attr.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel_device_attr.go)。

### 03.2 关系模型：设备与测点不是协议地址

~~~text
Product / Device Model（同类型设备的定义）
       │
       ├─ ProductAttr（型号测点）
       ├─ ProductEvent（型号事件规则）
       └─ ProductLink（型号关联规则）
       │
       ▼
DeviceBase（具体设备，可关联Product）
       │
       ├─ DeviceAttrBase（本设备测点）
       │      ├─ ProductAttrId（型号来源）
       │      └─ GeneralAttrId（通用属性来源）
       ├─ DeviceEvent（设备事件配置）
       └─ DeviceLink（设备关联配置）
       │
       ├─ ChannelDevice → Channel
       └─ ChannelDeviceAttr → Channel + DeviceAttr
                                  │
                                  ▼
                        协议属性/地址/读写区域
~~~

表中ProductAttr、ProductEvent、ProductLink以Excel模板类型为证据；**不能据此推断仓内一定存在一个统一、完整的ProductModel对象聚合API**。型号实体持久化及生成流程需继续追踪相应HTTP服务和数据库实现。

### 03.3 值得学习的设计与需要规避的耦合

**可以保留**：通用属性字典、型号属性、设备属性分层；设备实例可继承型号默认配置并具独立标识；通道与设备、通道与点位分别建关系；控制功能不应直接等同于寄存器写入。

**需进一步审查**：设备Code/AssetId/数据库ID如何稳定映射；型号修改后是否同步覆盖既有设备属性；变更是否可回滚；多通道同设备的权限/写入所有权；删除设备时的点表与历史数据处理；租户边界。

**现场语义的重要反例**：同一柜体有“仪表测量”和“保护装置”两个独立设备，二者可能具有相似属性但不能合并成同一个通信Session。反之，一个设备也可能通过多个通道采集不同测点；不能强行假设“一设备一通道”。

### 03.4 固定属性机制

[device_fix_attr.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_fix_attr.go) 定义了数据采集心跳等系统维护属性，说明业务测点之外，还有运行/系统属性。迁入CxVisual时应分清：**专业设备属性**、**服务健康状态**、**通信链路状态**，避免把进程健康写成业务设备测点而难以区分来源。

## 04. Excel点表、事件表、关联表及协议解析

### 04.1 模板与通道实际点表

根据 [sheet.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/sheet.go)：

| 对象 | 旧格式 | 用途 |
|---|---|---|
| 通道实际点表 | `sheet/channel/{ChannelId}.xlsx` | 某Channel对应的运行点表 |
| 型号/规约模板 | `sheet/template/{ProtocolType}-{TemplateId}.xlsx` | 同类设备/协议模型可复用模板 |
| 样例 | `sheet/sample/` | 按协议类型生成/示范字段 |
| 工作表 | `attr` / `event` / `link` | 测点、事件、关联规则 |

这里的`event`是**点表中的事件规则/关联声明**，不是说Excel存储了所有实际发生过的事件历史；`link`用于关联配置，不等同于CxVisual编排中的Connection/Port。

### 04.2 两级点表的数据内容

| 表类型 | 主要内容 | 几个代表字段 |
|---|---|---|
| ProductAttr | 型号级测点模板 | SheetCode、ProductCode、AttrCode、AttrName |
| ProductEvent | 型号级事件配置 | AttrCode、RelatedAttrCodes、RelatedExpression、EventCode、MessageType |
| ProductLink | 型号级联动配置 | AttrCode、DataType、CollectionFactor、BadZone、LinkType |
| DeviceAttr | 具体设备协议绑定、数据类型和标定换算 | DeviceCode、ProductCode、AttrCode、DataType、UnitName、ConversionFactor |
| DeviceEvent | 具体设备事件关联/表达式 | DeviceCode、AttrCode、EventCode、RelatedAttrCodes、RelatedExpression |
| DeviceLink | 具体设备点位连接配置 | DeviceCode、AttrCode、LinkType、DataType、CollectionFactor |

源码：[hy/common/sheet/product.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/sheet/product.go)、[hy/common/sheet/device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/sheet/device.go)。不同协议再扩展字段，例如Modbus的Slave/数据区/寄存器/长度/编码/位范围，见 [protocol_modbus.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol_modbus.go)。

### 04.3 实际加载调用链（S）

[DeviceSheetService.LoadSheet4InfoByData](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/device_sheet.go) 的主要步骤：

1. 由Channel相关信息确定ProtocolType，查找ProtocolSheet及反射描述；
2. 解析`attr`，按配置过滤不使用的测点；
3. 根据设备Code查询CommDeviceAttr组，补足设备、型号、属性ID；
4. 通过协议特定`ParseInfos`生成读取/写入所需信息；
5. 可选加载`event`与`link`，关联属性并让协议解析器更新运行信息；
6. 再次补足属性映射，输出通信驱动可使用的协议数据结构。

**关键价值**：Excel仅提供可编辑数据；后续有真实的**协议解析、依赖查询、转换为运行结构**过程，给未来“定义/校验/编译/运行”分层提供现成经验。

**迁入CxVisual的候选**：将Excel视为导入/导出，统一内部BindingDefinition/BindingPlan；先校验全部点位、ID、地址及协议，再生成不可变Plan；新计划校验通过后由Runtime在安全点切换。**这属于D，旧Go代码不能被描述成已有原子热切换与版本回滚。**

### 04.4 点表变更

[communicator.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/communicator.go) 有通道增加/删除/更新及点表变更通知入口：通道更新时可以先停止旧Communicator再创建新的；点表变化发送ChannelSheetChange事件给已有实例。它已经体现动态更新思路，但**不保证当前实现具有原子替换、端到端不丢采集点或跨进程版本一致性**，这些都需要专门测试。

## 05. Channel、Protocol与Communicator——运行组合的核心

### 05.1 通道与协议确实分开建模

[common/channels](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/common/channels/client.go) 与同目录TCP/UDP/串口/MQTT实现提供物理/网络IO；[model/protocol.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol.go) 则提供协议解析、点表类型及设备信息转换入口。

[protocol_reflect.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol_reflect.go) 的固定注册映射包含LWM2M、Modbus、DL/T645、KWZD、LINNER、HJ212、MQTTO、Acrel、IEC104等枚举的描述对象。这里证实**注册表存在**；某个具体规约是否覆盖全部采集/遥控/异常能力，必须再按驱动与真实协议测试。

[hy/communicator/](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/communicator/modbustcp/main.go) 中存在具体协议应用入口（Modbus TCP、IEC104、DL/T645、HJ212等）；与基础Protocol/Channel共同形成通信进程，不能把具体协议功能直接算成common通用能力。

### 05.2 Communicator真实契约

[hy/common/communicatorc/communicator.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/communicator.go) 注释明确：

- 一个Communicator对应**一个Protocol**；
- 一个Communicator对应**一个Channel配置**；
- 一个Communicator对应**一份Sheet**。

主要接口/对象：

| 代码 | 已读方法 | 职责 |
|---|---|---|
| CommunicatorBase | SetChannel、LoadSheet、UpgradeData、RunCommunicator、SetCExit、SetCEvent | 规约驱动统一行为接口 |
| CommunicatorCustom | ProtocolType、ProtocolSheet、ChannelConfig、SheetFilePath、事件通道 | 将定义信息装配为具体驱动运行单元 |
| CommunicatorManageBase | CheckChannel、NewCommunicator | 每类驱动提供工厂和检查 |
| CommunicatorManagerCustom | InitCommunicators、CreateCommunicator、StopCommunicators、Find、StopAndRemoveByChannel、DealMqttControl | 多个运行单元的管理、变化响应、控制路由 |

典型运行链：

~~~text
Manager.InitCommunicators(ProgramId)
  └─ 查询通道列表
      └─ CreateCommunicator(channel)
          ├─ CheckChannel
          ├─ 取ChannelId对应xlsx
          ├─ NewCommunicator
          ├─ SetChannel → ProtocolType/ProtocolSheet
          ├─ LoadSheet → 协议数据结构
          ├─ UpgradeData → 各驱动个性化参数
          ├─ RunCommunicator
          └─ SubscribeControl(channelId)
~~~

旧实现通过`Channel.ProgramPath`推导ProtocolType，属于明确的历史耦合。目标设计宜显式指定ProtocolId/版本、ProviderId、通道ID和点表版本；不能依赖程序可执行文件路径推理协议。

### 05.3 动态运行与职责分离的启示（D）

- **Definition**：Channel/Protocol/PointBinding/Capability的声明；
- **Provider/Adapter**：具体Modbus、IEC104、IoT网关、设备SDK；
- **CompiledPlan**：解析与校验后的可执行配置；
- **Session**：连接、读写队列、设备状态和重连上下文；
- **Manager**：创建/查找/停止/资源诊断的入口。

五个是**设计职责**，**不是要求增加五个同名CLR类**。原Go中的`CommunicatorManagerSingle`为进程级全局指针；迁入多工作站/多运行实例时应改为有作用域的Manager/注册表或依赖注入实例，保留便利入口但不形成隐式共享可变单例。

### 05.4 有点表才启动，还是允许逻辑设备？

旧`InitCommunicators`注释明确允许**没有任何通道时运行逻辑点计算和上报**，而`CreateCommunicator`对单通道要求点表文件存在。这个区分值得保留：系统允许“无实体设备的逻辑属性和计算Provider”，但真实通信Session必须有足够的协议绑定信息才能启动。

## 06. 实时值、变化发布、命令入口与事件

### 06.1 实时数据实际结构（S）

[communicatorc/rt.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/rt.go)：

- RtDataFloatC包含RtDataFloat、Channel ID、变化时间、旧值；
- RtDataStringC提供字符串路径；
- 缓存按属性ID保存当前值，附设备心跳相关固定属性；
- UpdateRtDataFloat维护当前值、旧值及变化时间，再向更新通道发送通知；
- RTTWrite*系列写入时间戳和值，再发布MQTT/其他下游数据；
- RTWriteEvent携带事件类型/状态、时间、设备、消息以及相关测点快照。

原系统的**测点快照与事件结果**存在具体实现，说明“值、来源、时标、变化通知及事件”不是抽象提案。

### 06.2 一次典型采集

~~~text
通信驱动收到设备报文
  → Protocol/点表转换得到设备测点ID和值
  → RT(T)Write*
  → UpdateRtDataFloat 缓存当前值/旧值/变化时间
  → MQTT发布/Redis等下游
  → 订阅方更新监控画面、趋势或业务处理
~~~

此链是多段代码合成；实际下游的QoS、重发、入库完整性及调用顺序需要逐实现验证。

### 06.3 控制路径与“命令模型”的边界

[communicatorc/control.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/control.go) 从MQTT控制Topic解析ChannelId，再调用CommunicatorManager.DealMqttControl，向对应Communicator的事件通道发送控制消息。

这证明**存在可运行的控制消息接入路径**，但目前所读源码不支持声称已经有统一的CommandDefinition/Invocation/Accepted/Completed/Readback等完整跨协议命令契约。目标Mechanism的命令生命周期需由M12/M28/M30及专业Provider协作深化。

### 06.4 事件和关系规则

[hy/common/sheet/device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/sheet/device.go) 中的DeviceEvent含EventCode、MessageType、RelatedAttrCodes、RelatedExpression；DeviceLink含LinkType等。说明事件发生可与测点值、多个依赖属性及表达式关联。

应严格区别：

- **Event Definition/Rule**：事件条件或负载定义；
- **Runtime Event**：已经发生的一次事实；
- **Delivery/Subscription**：消息如何通知消费方；
- **UI Event**：编辑器中的鼠标/对象冒泡等交互事件，语义并不相同。

本分类是CxVisual建议用于防止重名冲突的分析框架，不把这些词当成旧Go中已完整实现的同名API。

## 07. 通信进程启动、控制流与动态配置

[communicatorc/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/main.go) 的RunMain将每类规约驱动的公共启动/退出、HTTP、Monitor、MQTT、定时事件、通道初始化统一起来。

- 进程维度：加载配置、启动HTTP和Monitor通信、初始化Communicator集合、周期心跳与资源清理。
- 实例维度：各规约Driver自行实现RunCommunicator、采集和控制逻辑。
- 变更维度：通道与点表变更通过消息进入管理器，不要求所有驱动器重启整个应用。
- 数据维度：进程内实时缓存及MQTT等服务联动。

**待复核项**：Go goroutine何时建立、Stop请求是否会被阻塞、是否等待结束、刷新Sheet是否有并发写入竞态、退出时订阅是否完全撤销。这些必须读具体`hy/communicator/<protocol>/`驱动实现，不能仅凭Manager接口推定全部安全。

## 08. Monitor与Daemon——历史能力，不直接复制目标进程拓扑

### 08.1 旧系统职责

| 旧部件 | 证据对象 | 职责 |
|---|---|---|
| Monitor | ProgramStatus、HeartbeatStatus、ProcessStatus | 接收心跳、守护事件、CPU/内存、提供状态接口 |
| Daemon | program.Program、daemonc.RunProgram | 按配置启动子进程、等待退出、计数、限制/延迟重启 |
| monitorc | MonFrame、命令码、UDP消息 | 心跳、状态请求、对象变化、重启请求与转发 |
| 业务通信进程 | communicatorc.RunMain | 驱动采集、事件监听、心跳和主动退出 |

证据：[monitor/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/main.go)、[monitor/app_status.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/app_status.go)、[monitor/process_monitor.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/process_monitor.go)、[daemon/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/daemon/main.go)、[daemonc/daemon.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/daemonc/daemon.go)。

### 08.2 状态和健康模型（S）

ProgramStatus包含DaemonStatus、HeartbeatStatus和ProcessStatus；其中包含ProgramId/PID、是否运行、最近心跳、启动/失败次数、CPU、RSS/VMS等。Monitor周期检查通道和拉取Daemon状态，通过HTTP接口输出状态列表。

这证明旧系统认识到**进程是否存活**与**服务是否健康**不是同一指标；工业平台还应单独观察**通道健康、规约健康、设备健康、测点数据质量**，这些后四项属CxVisual深化提案而非旧Monitor统一完成。

### 08.3 守护策略（S）

[program/program.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/program/program.go) 定义Path、Dir、ArgsString、EnvsString、Enabled、StdOutPath、FirstWaitTime、StartWaitTime、MinExitTime、MaxError、MaxCount等。RunProgram启动并Wait子进程，根据退出和运行时长决定失败计数及重启。

[hy/daemon/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/daemon/main.go) 还处理程序定义变更、停止/重启消息和数据库可用性检查。在某些故障恢复路径会等待服务恢复后重新启动受管应用。

**复用建议**：保留配置、健康事实、故障分类、恢复策略及可观察性；不要将自定义UDP控制报文原样当成新系统的安全运维协议。CRC16不等于认证；远程重启/配置变更需要明确权限、身份、审计及幂等。

### 08.4 CxVisual当前阶段的收敛（D，非最终定案）

当前讨论要求**只保留编排/运行/AI三种主进程**。因此不增加独立Monitor、Daemon、Bus、终端进程；总线和专业终端作为可在编排端与运行端分别实例化的模块：

| 功能 | 编排进程Studio | 运行进程Runtime.Host | AI服务进程 |
|---|---|---|---|
| 工程与模型 | 草案、编辑、编译请求 | 当前已发布Plan | 只读取适用能力与请求 |
| 内部消息/事件Bus | 自有调试/编辑Bus实例 | 自有正式运行Bus实例 | AI任务/结果边界 |
| 终端/Provider | 编辑、模拟、在线观察或受控调试 | 真实通信Session与硬件资源所有权 | 模型、GPU、推理任务 |
| 运维监控 | 统一展示、查看健康、日志和调试 | 生成真实健康/故障事实并上报 | 上报模型/队列/GPU健康 |
| 进程故障 | 显示不可达及恢复后的重连 | 独立于Studio继续运行 | 独立反馈失败/恢复 |

当前目标允许未来拆出独立Bus/运维/终端服务，但**本档案不把未来选项当作本期必须新增的项目或进程**。

关键约束：编辑器关闭或崩溃，正式运行仍应独立；Studio不直接持有生产设备的写控制权。资源租约由运行端仲裁，现场安全联锁需设备侧保障，不能只靠软件锁。

## 09. IoT HTTP管理与模型服务

[hy/http_server/iot/device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/http_server/iot/device.go) 提供设备创建/批量注册/查询/更新/删除、设备清单等API；[device_attr.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/http_server/iot/device_attr.go) 提供设备测点创建、批量更新、查找等接口。其他文件处理GeneralAttr、Machine、Device/Channel相关映射。

源码证明**存在管理API与模型持久化管理逻辑**，但不能默认所有业务操作都具有跨表事务、幂等令牌、多租户安全审计和灰度部署。提交CxVisual时，应先明确这些API是导入/配置管理、正式操作还是仅管理后台功能。

**同名概念不可混用**：旧Go的HTTP `Service`或后端模块，华为云物模型的`Service`功能分组，以及CxVisual运行进程的Service实例，三者不是同一概念。

## 10. tk、lm与历史实现作为补充

| 区域 | 可核实线索 | 对本档案的补充价值 |
|---|---|---|
| **tk** | [tk/readme.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/tk/readme.md) 含ARM/ARM64、OpenWrt交叉构建、Web+MQTT/Modbus及轻量部署示例 | 边缘网关体量/移植性、少依赖可部署形式 |
| **lm** | [lm/README.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/lm/README.md) 介绍Gin/Vue管理后台、角色/API权限、日志、任务与监控 | 平台运维UI、管理API和访问控制设计经验 |
| **hy之外的示例/旧代码** | backup、hello、vendor及部分压缩归档 | 判断历史来源/不同实现差异，不计为统一生产能力 |

不能仅凭README列出的“内置功能”就判所有功能都已在当前Go源码中形成闭环；需要具体模块、编译和运行证据。对CxVisual本期，**不需要因此增加独立Web管理进程或IoT工程项目**。

## 11. 代码风险与需要继承的工程经验

下表列出**已观察到的代码事实/静态风险**，不是宣称真实现场发生了对应事故。

| 优先 | 证据 | 风险或事实 | 未来处理/验收 |
|---|---|---|---|
| P0 | [device_attr_base.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_attr_base.go) 的IsSame | 部分数值/指针比较前有取反，字段语义显著可疑 | 用字段同值、仅一字段改变、空值组合等测试验证；正确实现差异比较 |
| P0 | [rt.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/rt.go) 的RTTWriteInt64 | int64经float64通道转发，大整数会损失精度 | 保留原始整数类型、值类型/Schema与序列化往返 |
| P0 | [rt.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/rt.go) 的UpdateRtDataFloat | 在互斥锁内向CCM通道发送，若下游堵塞则阻塞持锁 | bounded队列、背压策略、锁外通知、取消与压测 |
| P0 | [channel.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel.go) | 通道结构体包含登录密码字段，格式化/日志路径需防泄漏 | 安全存储、日志脱敏、禁用凭据明文回显 |
| P0 | [hy/hy.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/hy.md)、[tk/readme.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/tk/readme.md) | 仓库说明/部署命令含示例明文账号或密码 | 不引用其值，全面扫描历史、清理并评估轮换 |
| P1 | [communicator.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/communicator.go) 的SetChannel | 依赖ProgramPath推断ProtocolType | 显式ProtocolId/版本/Provider绑定，禁止路径猜测 |
| P1 | [communicator.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/communicator.go) 动态更新 | 停旧建新，未证明无损原子热切换 | 版本化Plan、新旧Session切换失败回退、连接清理 |
| P1 | [monitor/app_event.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/app_event.go) 的logProgramHeartbeat | 函数内构造的事件类型是EventLogin | 事件码一致性、监控记录与时序测试 |
| P1 | [daemon/main.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/daemon/main.go) / [monitor/app_event.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/app_event.go) | 部分时间字段存在秒/毫秒混用风险 | 使用明确时间类型、UTC/单调时间分离 |
| P1 | [monitorc/mon_frame.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/monitorc/mon_frame.go) | 自定义UDP+CRC协议负责监控/操作消息 | 身份认证、重放保护、命令授权与审计 |
| P1 | [go.mod](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/go.mod) | 使用多套历史第三方依赖，需要重新审查维护/漏洞状态 | SBOM/许可证/依赖安全审查；不能仅凭版本号断言有漏洞 |

**保留的核心工程经验**：模型与协议解耦、模型到点表的批量化生成、Manager统一入口、通信运行实例化、实时值与变化通知、Monitor/Daemon监控事件，以及轻量边缘部署思路。

## 12. CxVisual融合：从IoT专有能力到公共机制（全部为D）

**基本判断**：让IoT场景成为Mechanism的压力测试，优先增强已有类型和关系；不新增IoT专用持久工程树，不改变既定28项目，不盲目把Go类逐一译成C#。必要时可以继续保留成熟Go服务，通过明确IPC/Adapter集成，但当前三进程目标不因此自动增加第四进程。

### 12.1 建议Owner矩阵

| IoT能力 | 当前Mechanism/Professional候选Owner | 增强要点/明确排除 |
|---|---|---|
| Type / Schema | **M04**（Core） | 可扩展类型、属性Schema、参数类型、版本与约束；不包括真实设备驱动 |
| Model / 可复用组合 | **M01/M02/M03/M38**（Core） | Content、Ref、模板、Model组织与实例覆盖；不新建IoT专属CxNode |
| Instance / 身份 | **M01/M03/M30**；设备资产**P01** | 工程实例、运行实例、外部设备身份分离 |
| Property | **M04 + M09** | Definition/Value/Quality/Time/Access/Binding；不复制DeviceVariable第二套模型 |
| Command | **M12 + M28 + M30** | 命令声明、Invocation、结果未确认、审计、互斥；专业动作仍在P层 |
| Event | **M12 + M23/M31/M36** | 定义、发生、订阅/触发、交付策略；UI冒泡事件不可冒充设备事件 |
| Capability | **M15 + M40** | Contract/Requirement/Provider/Binding/Placement分离；按需服务组合 |
| Relationship / Port | **M03/M05/M18** | 稳定Ref、端口连接、行为/数据流引用；不新建权威Graph树 |
| Point Table | **M09/M13–M15/M38** + **P13/P14** | 逻辑测点与协议地址绑定；Excel仅交换格式 |
| Channel / Protocol / Provider | **M13/M14/M15** + **P13/P14** | 传输与通用协议机制公共化；具体规约/专业地址/设备语义在专业层 |
| 运行Session和资源租约 | **M26/M27/M29/M30/M31** | 编译、资源仲裁、超时/重连、状态；运行进程权威Owner |
| 运行监控/诊断 | **M32/M33** + Studio界面 | Runtime提供事实，Studio只读监测与授权调试 |
| IoT设备专业语义 | **P01/P13/P14** | 产品/设备、设备管理、专业协议映射与网关能力 |

这里的M编号/项目边界以主仓最新目标规格及实际依赖为准；**该表是候选映射，不等于上述项目都有完整实现**。V6.4是结构与证据审核版，签名与可执行性需要再审。

### 12.2 Model依托现有CxNode、Content、资源与Schema

CxVisual的权威工程骨架仍是：**Project / Page / Content / Symbol / Group / Element**。四个Aspect为可视化、逻辑、数据、协同。对于需要复用和组合的专业模型，候选采用：

~~~text
Project（唯一工程根）
  ├─ Content：型号/功能组合Model（可复用定义）
  │     ├─ TypeRef + SchemaVersion
  │     ├─ Service Group A → Property/Command/Event/Capability Ref
  │     └─ Service Group B → Property/Command/Event/Capability Ref
  │
  ├─ ContentReference：设备工程实例A
  │     ├─ TargetContentId、InstanceId、参数覆盖
  │     ├─ 物理设备身份映射（P01）与资源Ref
  │     └─ PointBinding资源Ref（设备点表）
  │
  └─ ContentReference：设备工程实例B
        └─ 引用同一Model但配置不同的Provider/协议/通道
                    ↓  M29校验/编译
               Runtime Plan
                    ↓  M30实例化
              Session / Variables / Events
~~~

**重要边界**：

1. Model是Content的一种**组合用途**，不等于要求所有Schema/设备参数都创建Content节点；
2. Service Group是**功能分组语义**，不强制新增顶层工程节点或第七种Node；
3. Property/Command/Event/Capability可以是Schema或附属Descriptor，不强迫全都成为CxNode子元素；
4. 设备本体的Device/Asset和IoT租户/网关等是专业实体，由P01/P14负责；
5. Content/Ref是Model/实例关联的一个优选形态，须结合实际V7.8/new x3类型签名验证能否自然表达，不得为了“套用Content”而引入重复持久权威；
6. Symbol派生于Group的最新目标定案应优先于V7.8历史SymbolDefinition，后者只作迁移证据。

**关系模型应先审不变量**：Ref只引用且不能改变Owner；同一Model生成多个实例时实例覆盖互不污染；型号升级应经版本迁移，不能静默改变运行态；删除引用目标留可诊断悬挂引用；无设备的算法Model不被强迫拥有PointTable/Channel。

### 12.3 Service / Capability Group与华为云IoT模型（候选）

历史开发曾参考华为IoT平台；华为云IoTDA官方API入口：[API参考](https://support.huaweicloud.com/api-iothub/iot_06_v5_0200.html)。从IoT行业产品模型中可借鉴“产品型号→可复用功能Service→属性/命令等契约→设备实例”的分组思想。

**本档案的D级建议**：ServiceId在模型内唯一，ServiceType可在一个模型中出现多次；一个Service Group组织Property/Command/Event/Capability的引用；Service**不是**操作系统进程，也不是必须独立部署的微服务。可复用模型可以声明Capability Requirement，而不预先硬编码具体Provider。

这一方案要重点验证 **Core→Logic/Connectivity的依赖方向**：Core只持有轻量稳定契约引用，不直接引用M12命令执行器/M15 Provider注册表；在应用组合层由M29/M30解析。

### 12.4 模型与运行值/期望值的可选分离

借鉴设备影子思想，但不照搬IoT云的离线执行策略：

| 值/状态 | 含义 | 候选Owner |
|---|---|---|
| Default | Model/Schema定义的默认值 | M04 |
| Configured | 已保存/已发布工程参数 | M01/M07/M29 |
| Desired | 希望Provider达到的目标状态 | M09/M12协调 |
| Reported | Provider报告/观测的状态 | M09 |
| Confirmed / Effective | 经应答/读回/专业规则确认生效 | M12与专业Provider |

它是**可选的值协调语义**，不是要求每个图元/AI结果都有五份属性。危险控制（轴、曝光、PLC输出）不能离线自动补发未确认命令；超时也不应自动判为未执行。命令需可追踪Id、超时、幂等、互斥与人工恢复策略。

### 12.5 三进程与总线/终端模块化（候选）

- **编排端**：草稿模型、编译请求、模拟Provider、只读观察、调试请求；拥有独立内部Bus实例；
- **运行端**：当前正式Plan、权威变量、实际通道Session、设备资源租约和受控命令；拥有独立内部Bus实例；
- **AI端**：模型/任务/推理服务、GPU资源状态；通过能力及消息契约与另外两端协作。

**内部Message/Event Bus≠工业现场总线**。Modbus/IEC104等依然由Channel/Protocol/Driver机制实现。

三种调试形态可作为验收候选：模拟（不接触实机）/在线观察（只读快照，不再连接同一实机）/独占调试（申请运行端租约，经安全授权）。在线编辑→新Plan→安全点切换，不直接在正在运行的配置上写字段。即使Studio退出，Runtime也不得失去维持安全运行和故障记录的能力。

### 12.6 三个跨专业反例检验

| 场景 | 共用公共能力 | 必须由专业实现 |
|---|---|---|
| IoT电表（Modbus/MQTT） | Model、Property、PointBinding、Command/Event、Capability、Session、质量时间 | 型号/仪表语义、寄存器和设备控制策略 |
| X-Ray射线源 | Model、参数、命令、状态/事件、租约/安全Gate | 射线源时序、硬件联锁、曝光安全、SDK |
| AI推理服务 | Type/Model、输入输出Schema、Command/Job、Event、Capability、健康 | 模型装载、图像/张量、GPU、推理语义 |

若AI需要强制补DeviceId、Channel或测点表才可以运行，说明通用Model抽象已被IoT污染；若IoT要新增一套变量/事件/命令基础设施才能接入，则说明Mechanism关系没有闭合。

## 13. 与V6.4/V7.8对象的查重复用优先级

现有规格与旧C#源码已出现 `CxNode`、`ContentDefinition`、`ContentReference`、`CxPropertyDescriptor`、`CxVariableDefinition`、`CxBindingRegistry`、`CxCommandBus`、`CxEventContract`、`CxRuntimeEvent`、`CxCapabilityDescriptor`、`CxCapabilityRegistry`、`CxChannelDescriptor`、`CxLeaseHandle`等名称或源码线索。

**严禁因本档案再创建一套IoTProperty/IoTCommand/IoTEvent/IoTDeviceVariable权威模型**。进一步处理每个对象前按顺序审：

1. 现目标主仓是否已有完全同语义、可编译可测试的对象？
2. 旧CxVisual V7.8是否已有近义定义、引用和调用行为？
3. V6.4是否为候选而非已实现？在哪个项目/子方面、依赖是否允许？
4. 可否复用.NET标准类型或已成熟Provider而非新增Helper/Manager？
5. 如果需要新对象，是补薄契约、适配器还是领域专有实现？唯一Owner是谁？

需要单独警惕：
- `CxEvent` 的节点冒泡/交互事件与IoT运行事件不能直接合并；
- M04元数据与M09运行变量不能相互复制职责；
- M15的Capability **声明**不应天然绑定某个具体Provider；
- Content/Ref的实例化不等于实际设备Session；
- CxResourceDefinition（定义）≠CxLeaseHandle（占用凭证）≠Runtime Instance（执行状态）；
- 旧系统已实现某方法≠目标主仓已经具备该行为。

## 14. 可复用资产与迁移方式

| 资产 | 优先迁移方式（D） | 验收重点 |
|---|---|---|
| 产品/设备/测点关系 | 抽取模式、校验与测试用例，映射到Mechanism公共契约+Automation | 稳定ID、型号版本、设备实例覆盖、不重建第二树 |
| Product/Device Excel点表 | 保留样例和解析规则，先设计内部Binding规范；必要时做Importer | 遗留点表无损导入、重复/缺失地址诊断 |
| Go协议解析/通信器 | 评估继续使用Go服务或迁移C# Provider，**按单个协议性能/现场要求选择** | 报文兼容、错误码、连接状态、重连/资源释放 |
| RT值/事件 | 优先融入已有M09/M12/M31/M35，不自建独立IoT缓存与总线 | int64精度、质量、时间、顺序、订阅背压 |
| Monitor/Daemon策略 | 保留健康/故障指标和策略，暂不复制多进程布局 | Host失联、Studio关闭、运行保活、恢复责任 |
| 管理API与Web后台 | 保留API/权限/审计与操作闭环经验，本期由Studio服务/专业扩展实现 | 未授权控制拒绝、远程更改审计 |
| tk/lm | 选择性参考边缘轻部署、管理后台、权限和监控 | 不引入重复平台或无依据的额外服务 |

**是否保留Go运行时**不是当前定案问题。三个主进程是本期目标；当某成熟Go组件需要作为外部进程复用时，应先提出进程约束冲突与性能/维护成本评估，不能自行增设长期运行的第四个系统进程。

## 15. 最小真实闭环验证（未来执行，当前未实施）

### 15.1 核心功能链

**案例A：一个型号、两个设备、多种通道**：同一型号定义Voltage/Current等属性；实例A与B有不同DeviceId、Channel与Binding；两个运行会话互不污染，HMI引用稳定逻辑属性。

**案例B：点表编辑与在线运行隔离**：Studio导入Excel并修改地址，得到Draft→Validate→Compile；正式运行在批准安全点前保持旧Plan；失败不部分生效，回退/重连/状态可诊断。

**案例C：命令、事件与结果未确认**：写某设备设定值，分别模拟成功、应答超时但设备已执行、拒绝、取消和断线；不能把Desired当Reported，也不能重复下发不可幂等命令。

**案例D：监控、进程和终端隔离**：关闭Studio后Runtime继续采集；AI服务不可达时记录健康变化；Runtime独占资源；模拟Provider只存在编排上下文；重新连接监控不会改变工程Revision。

**案例E：X-Ray/AI反向压力测试**：同一公共契约也能表达有硬件互锁的光源与无设备/无点表的AI推理，未出现Mechanism依赖反转和IoT专有字段污染。

### 15.2 必须留下的证据

| 测试层级 | 结果要求 |
|---|---|
| 类型/Schema | 字段、可空、范围、单位、事件负载、版本兼容校验 |
| 数据/事件 | 值类型不丢精度、乱序/重复/背压可观测、质量不虚报 |
| 通道/点表 | 空表、重复DeviceId/寄存器、非法协议字段、导入回滚 |
| 运行/隔离 | 双实例、租约互斥、运行中配置切换、Studio断开恢复 |
| 故障与安全 | 异常退出、超时结果不确定、联锁拒绝、恢复显式化 |
| 工程验证 | 真实构建日志、自动化用例、环境、依赖版本；实机单独记录 |

不能通过仅打印“PASS”、静态扫描或只有模拟Provider，就宣称工业设备闭环已全部验收。

## 16. 建议下一轮讨论但**不在本档案定案**的事项

以下是后续逐项目审核的候选问题，按当前重要性排序：

1. **M02/M03**：Model与ContentDefinition、ContentReference如何自然组合？Service Group做轻量描述还是组合Content？模型版本如何影响多个实例？
2. **M04/M09**：PropertyDescriptor与VariableDefinition的唯一映射；通用属性与设备测点的界线；Configured/Desired/Reported/Confirmed是否按能力可选启用？
3. **M12/M31/M36**：Command、Operation、Runtime Event、UI Event、持久/非持久消息如何区分？交付保证如何如实声明？
4. **M15/M40**：Capability Contract/Requirement与Provider绑定、ServiceType/ServiceId、模块注册和能力组合是否需要少量扩展？
5. **M13/M14/Automation P13/P14**：哪些通用协议适配器应留公共机制，哪些规约/设备差异归专业实现？
6. **M26/M27/M28/M29/M30**：编排端与运行端双实例、设备资源仲裁、独占调试、安全点热更新。
7. **M32/M33**：三进程无独立Daemon情况下监控事实、恢复责任和诊断UI；未来外部Supervisor怎样兼容。
8. **M38/M39**：从Product/Device Excel点表导入到CxVisual版本化Binding，未知字段/旧工程迁移如何保证无损。

这些问题**可以修正、合并或放弃**，无需维护一套脱离四大文档的新“定案”；未来一旦讨论批准，应在相应项目/子方面规格及`DOCS/Decisions/`记录正式决议，而不是反向将本档案“D”行提升成事实。

## 17. 固定源码索引与后续审核命令入口

以下链接均固定在上述旧仓commit，不会因为master后续变化而静默改变本文事实基础：

| 对象/问题 | 文件 |
|---|---|
| 历史IoT概念及术语 | [hy/hy.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/hy.md)（**含历史敏感示例，不复制**） |
| 设备及属性 | [DeviceBase](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_base.go)、[DeviceAttrBase](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/device_attr_base.go) |
| IoT与通信侧设备 | [IotDevice](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/iot_device.go)、[CommDevice](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/comm_device.go) |
| 通道/设备绑定 | [Channel](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel.go)、[ChannelDevice](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel_device.go)、[ChannelDeviceAttr](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/channel_device_attr.go) |
| 点表声明 | [产品模板](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/sheet/product.go)、[设备点表](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/sheet/device.go)、[文件路径/工作表名](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/sheet.go) |
| 点表加载/导入处理 | [DeviceSheetService](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/device_sheet.go) |
| 协议接口及注册 | [ProtocolService](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol.go)、[ProtocolReflect](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol_reflect.go)、[ModbusAttr](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/model/protocol_modbus.go) |
| Channel底层 | [common/channels](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/common/channels/client.go)、[Modbus](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/common/modbus/modbus.go) |
| 通信实例/Manager | [CommunicatorCustom/Manager](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/communicator.go) |
| 实时数据/控制 | [rt.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/rt.go)、[control.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/communicatorc/control.go) |
| 守护/监控 | [program.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/program/program.go)、[daemonc/daemon.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/daemonc/daemon.go)、[monitor/app_status.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/monitor/app_status.go) |
| 控制报文 | [monitorc/code.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/monitorc/code.go)、[monitorc/mon_frame.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/common/monitorc/mon_frame.go) |
| IoT管理API | [device.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/http_server/iot/device.go)、[device_attr.go](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/hy/http_server/iot/device_attr.go) |
| 补充资料 | [tk/readme.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/tk/readme.md)、[lm/README.md](https://github.com/oudream/device_communicator/blob/18b5313d8c0c350f3a8cc723f57120cf9e551156/lm/README.md) |

### 给下一轮ChatGPT/Codex的执行口径

> 先读 `DOCS/Codebases/02_device_communicator.md` 的00、03～08和12～16章，按需要再打开固定commit的Go源码。讨论新的CxVisual项目/子方面时，先查主仓实际C#类型和当前V6.4及后续批准规格，按**现状→旧仓复用行为→唯一Owner→设计候选→测试**逐项审核。本文的D提案不是定案；不得因为它新增项目/进程、平行IoT对象树或重复DeviceVariable，不得把已读源码直接当成已运行验收，也不得复制旧仓凭据到公共文档。

---

**维护规则**：此文件为device_communicator的唯一专项档案。后续可以迭代同一路径；大幅深入某个机制时应在本档案附源码证据与目标Owner，不额外维护互相矛盾的“正式第二本”。旧Go原仓由原团队/负责人维护，未经明确指示不修改；CxVisual的正式设计决议另记`DOCS/Decisions/`，本档案保留演进脉络与可复核事实。
