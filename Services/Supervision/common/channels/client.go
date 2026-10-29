package channels

import (
	"fmt"
	"github.com/oudream/go-serial"
	"gogo/common/models"
	"gogo/common/utilities"
)

const (
	CKeyConnectName     = "Name"
	CKeyClientId        = "ClientId"
	CKeyIpAddress       = "IpAddress"
	CKeyPort            = "Port"
	CKeyRemoteIpAddress = "RemoteIpAddress"
	CKeyRemotePort      = "RemotePort"
	CKeyLoginUser       = "LoginUser"
	CKeyLoginPw         = "LoginPw"
	CKeySslTls          = "SslTls"
	CKeyPortName        = "PortName"
	CKeyBaudRate        = "BaudRate"
	CKeyParity          = "Parity"
	CKeyDataBits        = "DataBits"
	CKeyStopBits        = "StopBits"
)

type CRecData struct {
	Err  error
	Data []byte
}

type Client interface {
	// Send writes data to the connection.
	// Send can be made to time out and return an error after a fixed
	Send(b []byte) (n int, err error)

	// Close closes the connection.
	// Any blocked Read or Write operations will be unblocked and return errors.
	Close() error

	// IsOpened check client is opened
	IsOpened() bool
}

func NewClient(connectionMode models.ConnectionModeEnum, config map[string]string, rc chan CRecData) (Client, error) {
	switch connectionMode {
	case models.ConnectionModeTcpClient:
		localIp, _ := config[CKeyIpAddress]
		localPort, _ := utilities.MapString2Int(config, CKeyPort)
		remoteIp, _ := config[CKeyRemoteIpAddress]
		remotePort, _ := utilities.MapString2Int(config, CKeyRemotePort)
		return NewTcpC(localIp, localPort, remoteIp, remotePort, rc)
	case models.ConnectionModeUdpClient:
		remoteIp, _ := config[CKeyRemoteIpAddress]
		remotePort, _ := utilities.MapString2Int(config, CKeyRemotePort)
		return NewUdpC(remoteIp, remotePort, rc)
	case models.ConnectionModeSerial:
		if !keysExist(config, CKeyPortName, CKeyBaudRate, CKeyParity, CKeyDataBits, CKeyStopBits) {
			return nil, fmt.Errorf("config not found key %s or %s or %s or %s or %s", CKeyPortName, CKeyBaudRate, CKeyParity, CKeyDataBits, CKeyStopBits)
		}
		serialName := config[CKeyPortName]
		baudRate := models.BaudRateEnum(utilities.ConvertString2Int(config[CKeyBaudRate]))
		parity := models.ParityEnum(utilities.ConvertString2Int(config[CKeyParity]))
		dataBits := models.DataBitsEnum(utilities.ConvertString2Int(config[CKeyDataBits]))
		stopBits := models.StopBitsEnum(utilities.ConvertString2Int(config[CKeyStopBits]))
		if IsValidSerialName(serialName) &&
			baudRate.IsValid() &&
			dataBits.IsValid() &&
			parity.IsValid() &&
			stopBits.IsValid() {
			//portName := "COM1"
			//portName := "/dev/ttyS1"
			//portName := "/dev/ttyUSB0"
			mode := &serial.Mode{
				BaudRate: baudRate.Value(),
				DataBits: dataBits.Value(),
				Parity:   serial.Parity(parity.Value()),
				StopBits: serial.StopBits(stopBits.Value()),
			}
			return NewSerial(serialName, mode, rc)
		} else {
			return nil, fmt.Errorf("serial config invalid %s or %s or %s or %s or %s", serialName, baudRate, parity, dataBits, stopBits)
		}
	}
	return nil, fmt.Errorf("connection mode invalid %s, config [%v]", connectionMode, config)
}

func keysExist(m map[string]string, keys ...string) bool {
	for _, key := range keys {
		_, exists := m[key]
		if !exists {
			return false
		}
	}
	return true
}
