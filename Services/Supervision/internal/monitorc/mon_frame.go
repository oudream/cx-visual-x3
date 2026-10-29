package monitorc

import (
	"gogo/common/utilities"
)

const (
	MaxPacketLen = 4096
)

// attention! attention! attention! : this protocol data type default use - BigEndian

const (
	Head1 byte = 0xA5
	Head2 byte = 0x5A
	Head3 byte = 0xA5
	Head4 byte = 0x5A

	HeadVer1 byte = 0x00
	HeadVer2 byte = 0x01

	// BodyLen1 len = BodyCode + BodyData
	// BodyLen1 byte = 0x00
	// BodyLen2 byte = 0x04
	//
	// BodyCode1 byte = 0x00
	// BodyCode2 byte = 0x00
	// BodyCode3 byte = 0x00
	// BodyCode4 byte = 0x00
	//
	// BodyData1 byte = 0x00
	// BodyData2 byte = 0x00
	//
	// Crc1 byte = 0x00
	// Crc2 byte = 0x00
)

type MessageA struct {
	title   string
	content string
}

// DealMonFrame
// 所有 monitor 客户端都要实现 DealMonFrame 接口
// 所有 monitor 客户端的实现文件名统一为：deal_monc.go。例如：守护、规约驱动器、MQTT转发器、MQTT抓取存储器
// 可通过接口中的函数名DealMonFrameData，全局查找实现的结构体
type DealMonFrame interface {
	DealMonFrameData(code uint32, data []byte)
}

// MonFrame：监控报文帧，monitor规约报文格式

type MonFrame struct {
	code  uint32
	state int
	// received index
	ri               int
	len1             int
	buf              []byte
	crc              uint16
	Deal             DealMonFrame
	LastReceivedTime int64
}

func CreateMonFrame(deal DealMonFrame) *MonFrame {
	return &MonFrame{
		Deal: deal,
	}
}

func (u *MonFrame) Unmarshal(data []byte) {
	len0 := len(data)
	bDeal := false
	// 完整包的判断依据
	if len0 > 13 && data[0] == Head1 && data[1] == Head2 && data[2] == Head3 && data[3] == Head4 && data[4] == HeadVer1 && data[5] == HeadVer2 {
		len1 := int(Endian.Uint16(data[6:]))
		if len1 == len0-10 {
			crc1 := Endian.Uint16(data[len0-2:])
			crc2 := utilities.CodeCrc16(data[8 : 8+len1])
			if crc1 == crc2 {
				code := Endian.Uint32(data[8:])
				u.Deal.DealMonFrameData(code, data[12:len0-2])
				u.LastReceivedTime = utilities.TimeGetCurrentMsepoch()
				bDeal = true
			}
		}
	}
	if !bDeal {
		u.unmarshalPush(data)
	} else {
		u.state = 0
		u.ri = 0
	}
}

// 非完整包的处理函数

func (u *MonFrame) unmarshalPush(data []byte) {
	len0 := len(data)
	index := 0
	state := u.state
	for index < len0 {
		switch state {
		case 0:
			if data[index] == Head1 {
				state = 1
			}
			index++
		case 1:
			if data[index] == Head2 {
				state = 2
			} else {
				state = 0
			}
			index++
		case 2:
			if data[index] == Head3 {
				state = 3
			} else {
				state = 0
			}
			index++
		case 3:
			if data[index] == Head4 {
				state = 4
			} else {
				state = 0
			}
			index++
		case 4:
			if data[index] == HeadVer1 {
				state = 5
			} else {
				state = 0
			}
			index++
		case 5:
			if data[index] == HeadVer2 {
				state = 6
			} else {
				state = 0
			}
			index++
		case 6:
			u.len1 = int(data[index])
			state = 7
			index++
		case 7:
			len1 := u.len1
			len1 = len1 | int(data[7])<<8
			if len1 > 23 && len1 < 4087 {
				state = 8
				u.len1 = len1
				u.ri = 0
			} else {
				state = 0
			}
			index++
		case 8:
			remain := u.len1 - u.ri
			rBodyLen := len0 - index
			if remain <= rBodyLen {
				copy(u.buf[u.ri:], data[index:index+remain])
				state = 9
				u.ri = u.len1
				u.state = state
				index += remain
			} else {
				copy(u.buf[u.ri:], data[index:])
				u.ri += rBodyLen
				index += rBodyLen
			}
		case 9:
			u.crc = uint16(data[index])
			state = 10
			index++
		case 10:
			crc1 := uint16(data[index]) | u.crc<<8
			crc2 := utilities.CodeCrc16(u.buf[:u.len1])
			if crc1 == crc2 {
				code := Endian.Uint32(u.buf[0:])
				u.Deal.DealMonFrameData(code, u.buf[4:])
			}
			state = 0
			index++
		default:
			break
		}
	}
}

// UMarshal 组装发送包
func UMarshal(code uint32, data []byte) []byte {
	len1 := 4 + len(data)
	len0 := len1 + 10
	if len0 > MaxPacketLen {
		return []byte{}
	}
	r := make([]byte, len0)
	r[0] = Head1
	r[1] = Head2
	r[2] = Head3
	r[3] = Head4
	r[4] = HeadVer1
	r[5] = HeadVer2
	Endian.PutUint16(r[6:], uint16(len1))
	Endian.PutUint32(r[8:], code)
	copy(r[12:], data)
	crc := utilities.CodeCrc16(r[8 : 8+len1])
	Endian.PutUint16(r[len0-2:], crc)
	return r
}
