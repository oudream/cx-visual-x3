package monitorc

import (
	"encoding/binary"
	"unsafe"
)

var Endian = binary.BigEndian

const (
	CCodeFlagTransfer  = 0x00010000
	CCodeFlagBroadcast = 0x00020000
)

const (
	// 监视客户端向监视服务端发送健康心跳
	CCodeProgramHeartbeatReq  = 0x0101
	CCodeProgramHeartbeatResp = 0x0102
	// 守护服务向监视服务端发送守护事件（程序启动、退出）
	CCodeProgramDaemonEventPost = 0x0103
	// 规约驱动器发布时的消息通知
	CCodeProgramDiffEmployReq  = 0x0105 | CCodeFlagTransfer
	CCodeProgramDiffEmployResp = 0x0106 | CCodeFlagTransfer
	// 监视服务端定时向守护服务请求同步程序状态
	CCodeProgramStatusReq  = 0x0107
	CCodeProgramStatusResp = 0x0108
	// 数据库状态检查后的消息通知（现在没有作用，由于没有做重启程序，influxdb修改需要手动重启抓取存储器）
	CCodeCheckDbsEmployReq  = 0x0109 | CCodeFlagTransfer
	CCodeCheckDbsEmployResp = 0x010A | CCodeFlagTransfer
	// 对象变化通知，（点表、通道）变更后通知规约驱动器
	CCodeObjectChangeReq  = 0x010B | CCodeFlagTransfer
	CCodeObjectChangeResp = 0x010C | CCodeFlagTransfer
	// 规约驱动器发布后请求守护服务重启对应程序
	CCodeDaemonRestartProgramReq  = 0x010D | CCodeFlagTransfer
	CCodeDaemonRestartProgramResp = 0x010E | CCodeFlagTransfer
)

type Transfer struct {
	Source      int64
	Target      int64
	TransferLen uint16 // Transfer Length
	Res1        uint16
	Res2        uint32
}

func ExtractTransfer(data []byte) (*Transfer, []byte) {
	var to Transfer
	toLen := int(unsafe.Sizeof(to))
	if len(data) >= toLen {
		to.Source = int64(Endian.Uint64(data[0:]))
		to.Target = int64(Endian.Uint64(data[8:]))
		to.TransferLen = Endian.Uint16(data[16:])
		return &to, data[toLen:]
	}
	return nil, nil
}

type DbsState struct {
	MqttState     bool
	RedisState    bool
	InfluxdbState bool
}

type CodeMessage struct {
	Code int
	Data []byte
}

type CodeStack []CodeMessage

func (s *CodeStack) IsEmpty() bool {
	return len(*s) == 0
}

func (s *CodeStack) Push(msg CodeMessage) {
	*s = append(*s, msg)
}

func (s *CodeStack) Pop() (CodeMessage, bool) {
	if s.IsEmpty() {
		return CodeMessage{}, false
	} else {
		index := len(*s) - 1   // Get the index of the top most element.
		element := (*s)[index] // Index into the slice and obtain the element.
		*s = (*s)[:index]      // Remove it from the stack by slicing it off.
		return element, true
	}
}
