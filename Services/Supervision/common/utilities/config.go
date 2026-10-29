package utilities

type HttpConfig struct {
	Host string
	Port int
}

type MySQLConfig struct {
	Ip           string
	Port         int
	DatabaseName string
	UserName     string
	Password     string
}

type RedisConfig struct {
	Ip       string
	Port     int
	UserName string
	Password string
}
