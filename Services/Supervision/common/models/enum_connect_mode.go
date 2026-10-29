package models

import (
	"strings"
)

type ConnectionMode struct {
}

type ConnectionModeEnum int

const (
	ConnectionModeNone ConnectionModeEnum = iota
	ConnectionModeTcpClient
	ConnectionModeHttpClient
	ConnectionModeUdpClient
	ConnectionModeHttp3Client
	ConnectionModeMQTT
	ConnectionModeSerial
	ConnectionModeTcpServer
	ConnectionModeHttpServer
	ConnectionModeUdpServer
	ConnectionModeHttp3Server
)

const (
	ConnectionModeLabelUnknown = "UNKNOWN"
	ConnectionModeKeyMin       = ConnectionModeNone
	ConnectionModeKeyMax       = ConnectionModeHttp3Server
)

// 转字符串
func (p ConnectionModeEnum) String() string {
	switch p {
	case ConnectionModeNone:
		return "None"
	case ConnectionModeTcpClient:
		return "TcpClient"
	case ConnectionModeUdpClient:
		return "UdpClient"
	case ConnectionModeHttpClient:
		return "HttpClient"
	case ConnectionModeHttp3Client:
		return "Http3Client"
	case ConnectionModeMQTT:
		return "MQTT"
	case ConnectionModeSerial:
		return "Serial"
	case ConnectionModeTcpServer:
		return "TcpServer"
	case ConnectionModeUdpServer:
		return "UdpServer"
	case ConnectionModeHttpServer:
		return "HttpServer"
	case ConnectionModeHttp3Server:
		return "Http3Server"
	default:
		return ConnectionModeLabelUnknown
	}
}

func (cm ConnectionMode) Key(label string) ConnectionModeEnum {
	switch strings.ToLower(label) {
	case "None":
		return ConnectionModeNone
	case "TcpClient":
		return ConnectionModeTcpClient
	case "HttpClient":
		return ConnectionModeHttpClient
	case "UdpClient":
		return ConnectionModeUdpClient
	case "Http3Client":
		return ConnectionModeHttp3Client
	case "MQTT":
		return ConnectionModeMQTT
	case "Serial":
		return ConnectionModeSerial
	case "TcpServer":
		return ConnectionModeTcpServer
	case "HttpServer":
		return ConnectionModeHttpServer
	case "UdpServer":
		return ConnectionModeUdpServer
	case "Http3Server":
		return ConnectionModeHttp3Server
	default:
		return -1
	}
}

// 获取选项数组
func (cm ConnectionMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: ConnectionModeNone, Label: "None"},
		{Key: ConnectionModeTcpClient, Label: "TcpClient"},
		{Key: ConnectionModeHttpClient, Label: "HttpClient"},
		{Key: ConnectionModeUdpClient, Label: "UdpClient"},
		{Key: ConnectionModeHttp3Client, Label: "Http3Client"},
		{Key: ConnectionModeMQTT, Label: "MQTT"},
		{Key: ConnectionModeSerial, Label: "Serial"},
		{Key: ConnectionModeTcpServer, Label: "TcpServer"},
		{Key: ConnectionModeHttpServer, Label: "HttpServer"},
		{Key: ConnectionModeUdpServer, Label: "UdpServer"},
		{Key: ConnectionModeHttp3Server, Label: "Http3Server"},
	}
}
