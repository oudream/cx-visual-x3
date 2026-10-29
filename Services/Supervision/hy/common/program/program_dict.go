package program

// attention! attention! attention! : daemon SaveProgramPidFile [ < DefaultProgramIdCore ]
// attention! attention! attention! : user program id > 0x0300 ( 768 )

const (
	DefaultProgramIdDaemon           = 0x0100 // 256
	DefaultProgramIdMosquitto        = 0x0101 // 257
	DefaultProgramIdRedis            = 0x0102 // 258
	DefaultProgramIdCore             = 0x0110 // 272
	DefaultProgramIdMonitor          = 0x0111 // 273
	DefaultProgramIdIotDispatcher    = 0x0112 // 274
	DefaultProgramIdCommDispatcher   = 0x0113 // 275
	DefaultProgramIdCommScraper      = 0x0114 // 276
	DefaultProgramIdIotServer        = 0x0116 // 278
	DefaultProgramIdCommServer       = 0x0117 // 279
	DefaultProgramIdMqttTransmission = 0x0118 // 280
	DefaultProgramIdUser             = 0x0300 // 768
	DefaultProgramIdFree             = 0x8000
)

const (
	DefaultProgramRole = 33
)

const (
	DefaultProgramNameHead             = "hy3"
	DefaultProgramNameDaemon           = "hy3-daemon"
	DefaultProgramNameMosquitto        = "mosquitto"
	DefaultProgramNameRedis            = "redis-server"
	DefaultProgramNameMonitor          = "hy3-monitor"
	DefaultProgramNameCommDispatcher   = "hy3-comm-dispatcher"
	DefaultProgramNameCommScraper      = "hy3-comm-scraper"
	DefaultProgramNameCommServer       = "hy3-comm-server"
	DefaultProgramNameMqttTransmission = "hy3-mqtt-transmission"
	DefaultProgramNameRestore          = "hy3-restore"
	DefaultProgramNameIotDispatcher    = "hy3-iot-dispatcher"
	DefaultProgramNameIotServer        = "hy3-iot-server"
)
