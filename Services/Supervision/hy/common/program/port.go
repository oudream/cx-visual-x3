package program

// DefaultPortMonBegin
// connect to monitor udp port
const (
	DefaultPortMonBegin   = 7600
	DefaultPortMonDaemon  = 7601
	DefaultPortMonMonitor = 7603
)

// DefaultHttpPortBegin
// listen http server port
const (
	DefaultHttpPortBegin          = 7800
	DefaultHttpPortInspector      = 7803 // iot.inspector 配置、监测（Mysql、MQTT、Redis、influxdb）连接
	DefaultHttpPortMQTT           = 7805 // MQTT Broker
	DefaultHttpPortRedis          = 7807 // Redis Server
	DefaultHttpPortInfluxDB       = 7809 // influxdb
	DefaultHttpPortMonitor        = 7811 // iot.monitor  监控（IOT 各 APP 进程资源、健康状态）
	DefaultHttpPortIotDispatcher  = 7813 // iot.dispatcher 调试分发向外提供服务
	DefaultHttpPortCommScraper    = 7815 // iot.scraper  时序库向外提供服务
	DefaultHttpPortIotServer      = 7817 // iot server 配置、监测（Mysql、MQTT、Redis、influxdb）连接
	DefaultHttpPortCommServer     = 7819 // comm server 配置、监测（Mysql、MQTT、Redis、influxdb）连接
	DefaultHttpPortCommDispatcher = 7821 // iot.explorer 调试分发向外提供服务

	DefaultUserPortBegin = 7900
)

const (
	DefaultCommunicatorPort = 6000
)
