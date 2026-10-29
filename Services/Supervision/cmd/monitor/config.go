package main

import (
	"errors"
	"flag"
	"fmt"
	"github.com/spf13/viper"
	"gogo/common/utilities"
	"gogo/internal/program"
	"path/filepath"
)

// -d /userdata/hy -i programs1
var (
	// _workDir：工作目录，程序运行时的当前目录（路径），从入参得到或从系统函数获取
	_workDir = flag.String("d", "", "work path, such [ -d /userdata/xx ]")
	// _assetsDir：前端目录（包括html、css、js、资源文件）
	_assetsDir = flag.String("assets", "", "work path, such [ --assets /userdata/xx ]")
	// _levelString：日志级别，控制日志输出（很少使用）
	//_levelString = flag.String("l", "release", "info level, such [ -l debug ]")
	// monitor：（监视器，是监视进程运行时状态，并通过http对外提供查询功能）
	// _monitorHost：监视服务的绑定IP，"0.0.0.0"表示监听本机所有可用网络接口的请求
	_monitorHost = flag.String("h", "0.0.0.0", "monitor server ip")
	// _monitorHost：监视服务的端口
	_monitorPort = flag.Int("p", program.DefaultPortMonMonitor, "monitor server port")
)

var (
	config Config
)

// Monitor 对应第一层名如：
/*
monitor:
  http:
    host: "0.0.0.0"
    port: 7811
*/

type Config struct {
	Monitor struct {
		Http utilities.HttpConfig
	}
}

func initConfig() error {
	configPath := filepath.Join(*_workDir, "conf")
	viper.SetConfigName("sys.config")
	viper.AddConfigPath(configPath)
	if err := viper.ReadInConfig(); err != nil {
		return errors.New(fmt.Sprintf("initConfig ERROR 1: %s \n", err))
	}
	if err := viper.Unmarshal(&config); err != nil {
		return errors.New(fmt.Sprintf("initConfig ERROR 2: %s \n", err))
	}
	return nil
}
