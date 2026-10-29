package program

// 当前程序所需的数据库脚本版本级别
const CurrentDBVersion = 3

const (
	TableNameProgramName = "comm_program"
	TableNameProgramRun  = "comm_program_run"
	TableNameSettings    = "comm_settings"
	MonitorDatabaseName  = "monitor.db"
)

const (
	SqlCreateCommDbVersion = `CREATE TABLE IF NOT EXISTS "comm_db_version" (
  "version" integer PRIMARY KEY, -- 版本号
  "date" integer, -- 日期
  "sql" text -- SQL代码
);`

	SqlCreateChannel = `CREATE TABLE IF NOT EXISTS "comm_channel" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建时间
  "update_time" integer(20), -- 更新时间
  "create_by" text(64), -- 创建人
  "update_by" text(64), -- 更新人
  "program_id" integer(20) NOT NULL, -- 程序ID
  "code" text(64) NOT NULL, -- 编号
  "name" text(64) NOT NULL, -- 名称
  "connect_mode" integer(11) NOT NULL, -- 连接方式
  "ip_address" text(64), -- IP地址
  "port" integer(11), -- 端口号
  "remote_ip_address" text(64), -- 远程IP
  "remote_port" integer(11), -- 远程端口
  "ssl_tls" text(64), -- SSL
  "client_id" text(64), -- 客户端ID
  "login_user" text(64), -- 登录用户名
  "login_password" text(64), -- 登录密码
  "serial_name" text(64), -- 串口名
  "baud_rate" integer(11), -- 波特率
  "parity" integer(11), -- 检验方式
  "stop_bits" integer(11), -- 停止位
  "data_bits" integer(11), -- 数据位
  "flow_control" integer(11), -- 流控制
  "read_interval" integer(11), -- 读的间隔
  "flags" text(1024), -- 标志，以符号|分隔，如：|Key=Value|
  "remark" text(256), -- 备注
PRIMARY KEY ("id")
);
`

	SqlCreateChannelDevice = `CREATE TABLE IF NOT EXISTS "comm_channel_device" (
  "id" integer NOT NULL PRIMARY KEY AUTOINCREMENT, -- 主键
  "channel_id" integer(20) NOT NULL, -- 通道ID
  "device_id" integer(20) NOT NULL -- 设备ID
);`

	SqlCreateChannelDeviceAttr = `CREATE TABLE IF NOT EXISTS "comm_channel_device_attr" (
  "id" integer NOT NULL PRIMARY KEY AUTOINCREMENT, -- 主键
  "channel_id" integer(20) NOT NULL, -- 通道ID
  "device_id" integer(20) NOT NULL, -- 设备ID
  "device_attr_id" integer(20) NOT NULL -- 设备属性ID
);`

	SqlCreateDevice = `CREATE TABLE IF NOT EXISTS "comm_device" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建时间
  "update_time" integer(20), -- 更新时间
  "create_by" text(64), -- 创建人
  "update_by" text(64), -- 更新人
  "product_code" text(64), -- 产品code
  "product_id" integer(20), -- 产品ID
  "parent_device_code" text(64) NOT NULL, -- 父设备code
  "name" text(64) NOT NULL, -- 设备名
  "code" text(64) NOT NULL, -- 设备code
  "flags" text(1024), -- 标志，以符号|分隔，如：|Key=Value|
  "is_enable" integer(11) NOT NULL, -- 是否可用
  "device_type" text(64), -- 设备type
  "sheet_code" text(64), -- SheetCode
  "cabinet_code" text(64), -- CabinetCode
  "switch_code" text(64), -- SwitchCode
  "remark" text(255), -- 备注
  "sync_iot_time" integer(20), -- 同步时间
  PRIMARY KEY ("id")
);`

	SqlCreateDeviceAttr = `CREATE TABLE IF NOT EXISTS "comm_device_attr" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建时间
  "update_time" integer(20), -- 更新时间
  "create_by" text(64), -- 创建人
  "update_by" text(64), -- 更新人
  "device_id" integer(20) NOT NULL, -- 设备ID
  "product_attr_id" integer(20) NOT NULL, -- 产品属性ID
  "general_attr_id" integer(20) NOT NULL, -- 通用属性ID
  "code" text(64) NOT NULL, -- 编码
  "name" text(64) NOT NULL, -- 名称
  "data_type" integer(11) NOT NULL, -- 数据类型
  "min_value" real(20), -- 最小值
  "max_value" real(20), -- 最大值
  "max_length" integer(20), -- 最大长度
  "unit_name" text(64), -- 单位
  "fraction" integer(11), -- 小数点位置
  "conversion_factor" real(20) NOT NULL, -- 转换系数
  "collection_factor" real(20) NOT NULL, -- 采集系数
  "bad_zone" real(20) NOT NULL, -- 零死区
  "shift_value" real(20) NOT NULL, -- 位置
  "remark" text(255), -- 备注
  "serial_no" integer(11) NOT NULL, -- 序号
  "is_sync_product" integer(11) NOT NULL, -- 是否已同步
  "role" integer(11) NOT NULL DEFAULT 0, -- 角色
  "sync_iot_time" integer(20) NOT NULL, --同步时间
  PRIMARY KEY ("id")
);`

	SqlCreateProgram = `CREATE TABLE IF NOT EXISTS "` + TableNameProgramRun + `" (
  "id" integer NOT NULL, -- 主键
  "path" text DEFAULT '', -- 程序名（可以是FilePath，也可以只是文件名)
  "dir" text DEFAULT '', -- 工作目录
  "args_string" text DEFAULT '', -- 运行参数
  "envs_string" text DEFAULT '', -- 环境参数
  "std_out_path" text DEFAULT '', -- StdOut输出路径
  "max_count" integer DEFAULT 0, -- 最多重启次数
  "max_error" integer DEFAULT 9, -- 最多错误次数
  "min_exit_time" integer DEFAULT 10, -- 最少退出时间
  "first_wait_time" integer DEFAULT 0, -- 首次启动等待时间
  "start_wait_time" integer DEFAULT 100, -- 重启前等待时间
  "role" integer DEFAULT 33, -- 角色
  "enabled" integer DEFAULT 1, -- 是否可用
  "name" text DEFAULT '', -- 服务名
  PRIMARY KEY ("id")
);
`

	SqlCreateProgramRun = `CREATE TABLE IF NOT EXISTS "` + TableNameProgramName + `" (
  "id" integer NOT NULL, -- 主键
  "path" text DEFAULT '', -- 程序名（可以是FilePath，也可以只是文件名)
  "dir" text DEFAULT '', -- 工作目录
  "args_string" text DEFAULT '', -- 运行参数
  "envs_string" text DEFAULT '', -- 环境参数
  "std_out_path" text DEFAULT '', -- StdOut输出路径
  "max_count" integer DEFAULT 0, -- 最多重启次数
  "max_error" integer DEFAULT 9, -- 最多错误次数
  "min_exit_time" integer DEFAULT 10, -- 最少退出时间
  "first_wait_time" integer DEFAULT 0, -- 首次启动等待时间
  "start_wait_time" integer DEFAULT 100, -- 重启前等待时间
  "role" integer DEFAULT 33, -- 角色
  "enabled" integer DEFAULT 1, -- 是否可用
  "name" text DEFAULT '', -- 服务名
  PRIMARY KEY ("id")
);
`

	SqlCreateSettings = `CREATE TABLE IF NOT EXISTS "` + TableNameSettings + `" (
  "key" text(255) NOT NULL, -- Key
  "value" text(255) NOT NULL -- Value
);
`

	SqlCreateSysUser = `CREATE TABLE IF NOT EXISTS "sys_users" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建时间
  "update_time" integer(20), -- 更新时间
  "create_by" text(64), -- 创建人
  "update_by" text(64), -- 更新人
  "uuid" text(191), -- UUID
  "username" text(191), -- 登录用户名
  "password" text(191), -- 登录密码
  "nick_name" text(191), -- NiCK名
  "side_mode" text(191), --
  "header_img" text(191), --
  "base_color" text(191), --
  "active_color" text(191), --
  "authority_id" integer(20), -- 权限ID
  "phone" text(191), -- phone
  "email" text(191), -- email
  "enable" integer(20), -- 是否可用
  PRIMARY KEY ("id")
);`

	SqlCreateAssetProduct = `CREATE TABLE IF NOT EXISTS "comm_product" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建时间
  "update_time" integer(20), -- 更新时间
  "create_by" text(64), -- 创建人
  "update_by" text(64), -- 更新时间
  "code" text(64), -- 设备模型编码
  "name" text(64), -- 名称
  "manufacturer" text(64), -- 厂家
  "product_number" text(64), -- 型号
  "version" text(64), -- 版本
  "up_id" integer(20), -- 上级id
  "serial_no" integer(20), -- 序号
  "level_id" text(64), -- 层次编码
  "remark" text(255), -- 备注
  PRIMARY KEY ("id")
);`

	SqlCreateAssetProductCollection = `CREATE TABLE IF NOT EXISTS "comm_product_attr" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建人
  "update_time" integer(20), -- 创建时间
  "create_by" text(64), -- 更新人
  "update_by" text(64), -- 更新时间
  "product_id" integer(20), -- 租户Id
  "code" text(64), -- 属性编码
  "name" text(64), -- 属性名称
  "unit_name" text(64), -- 单位
  "data_type" integer(11) NOT NULL, -- 数据类型
  "min_value" real(20), -- 最小值
  "max_value" real(20), -- 最大值
  "max_length" integer(20), -- 最大长度
  "fraction" integer(11), -- 小数点位置
  "conversion_factor" real(20) NOT NULL, -- 转换系数
  "collection_factor" real(20) NOT NULL, -- 采集系数
  "bad_zone" real(20) NOT NULL, -- 零死区
  "shift_value" real(20) NOT NULL, -- 位置
  "use_flag" integer(11), -- 是否启用
  "serial_no" integer(20), -- 序号
  "remark" text(255), -- 备注
  PRIMARY KEY ("id")
);`

	SqlCreateAssetSheetTemplate = `CREATE TABLE IF NOT EXISTS "comm_sheet_template" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建人
  "update_time" integer(20), -- 创建时间
  "create_by" text(64), -- 更新人
  "update_by" text(64), -- 更新时间
  "product_id" integer(20), -- 产品Id
  "code" text(64), -- 编号
  "name" text(64), -- 名称
  "protocol_type" integer(11) NOT NULL, -- 规约类型(1:modbus; 2:lw2m2; 3:dlt645)
  "remark" text(255), -- 备注
  PRIMARY KEY ("id")
);`

	SqlCreateIotGeneralAttr = `CREATE TABLE IF NOT EXISTS "iot_general_attr" (
  "id" integer(20) NOT NULL, -- 主键
  "create_time" integer(20), -- 创建人
  "update_time" integer(20), -- 创建时间
  "create_by" text(64), -- 更新人
  "update_by" text(64), -- 更新时间
  "code" text(64) NOT NULL, -- 编码
  "name" text(64) NOT NULL, -- 名称
  "data_type" integer(11) NOT NULL, -- 数据类型
  "min_value" real(20), -- 最小值
  "max_value" real(20), -- 最大值
  "max_length" integer(20), -- 最大长度
  "unit_name" text(64), -- 单位
  "general_tags" text(256), -- 标签
  "serial_no" integer(20), -- 序号
  "remark" text(255), -- 备注
  PRIMARY KEY ("id")
);`
)

type Info struct {
	Name              string
	Path              string
	ProgramId         int
	DefaultArgsString string
	DefaultDir        string
	DefaultEnable     int
}

var (
	Infos = []Info{
		{
			Name:              "守护",
			Path:              DefaultProgramNameDaemon,
			ProgramId:         DefaultProgramIdDaemon,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
		{
			Name:              "MQTT-Server",
			Path:              DefaultProgramNameMosquitto,
			ProgramId:         DefaultProgramIdMosquitto,
			DefaultDir:        "mosquitto",
			DefaultArgsString: "-c ./../conf/mosquitto.conf",
			DefaultEnable:     1,
		},
		{
			Name:              "Redis-Server",
			Path:              DefaultProgramNameRedis,
			ProgramId:         DefaultProgramIdRedis,
			DefaultDir:        "redis",
			DefaultArgsString: "./../conf/redis.conf",
			DefaultEnable:     1,
		},
		{
			Name:              "监控",
			Path:              DefaultProgramNameMonitor,
			ProgramId:         DefaultProgramIdMonitor,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
		{
			Name:              "COMM分发器",
			Path:              DefaultProgramNameCommDispatcher,
			ProgramId:         DefaultProgramIdCommDispatcher,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
		{
			Name:              "COMM抓取器",
			Path:              DefaultProgramNameCommScraper,
			ProgramId:         DefaultProgramIdCommScraper,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
		{
			Name:              "COMM后端",
			Path:              DefaultProgramNameCommServer,
			ProgramId:         DefaultProgramIdCommServer,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
		{
			Name:              "MQTT转传器",
			Path:              DefaultProgramNameMqttTransmission,
			ProgramId:         DefaultProgramIdMqttTransmission,
			DefaultDir:        "",
			DefaultArgsString: "",
			DefaultEnable:     1,
		},
	}

	SqlCreateList = []string{
		SqlCreateCommDbVersion,
		SqlCreateChannel,
		SqlCreateChannelDevice,
		SqlCreateChannelDeviceAttr,
		SqlCreateDevice,
		SqlCreateDeviceAttr,
		SqlCreateProgram,
		SqlCreateProgramRun,
		SqlCreateSettings,
		SqlCreateSysUser,
		SqlCreateAssetProduct,
		SqlCreateAssetProductCollection,
		SqlCreateAssetSheetTemplate,
		SqlCreateIotGeneralAttr,
	}
)
