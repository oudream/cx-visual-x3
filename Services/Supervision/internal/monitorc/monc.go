package monitorc

import (
	"encoding/json"
	"errors"
	"gogo/common/channels"
	"gogo/internal/program"
	"unsafe"
)

type MonC struct {
	IP    string
	Port  int
	udpC  *channels.UdpC
	C     chan channels.CRecData
	Frame *MonFrame
}

func NewMonC(ip string, port int, deal DealMonFrame) (*MonC, error) {
	c := make(chan channels.CRecData)
	udpC, err := channels.NewUdpC(ip, port, c)
	//
	r := &MonC{
		udpC: udpC,
		C:    c,
	}
	r.Frame = CreateMonFrame(deal)
	return r, err
}

func (c *MonC) Send(code uint32, data []byte) (int, error) {
	if c.udpC == nil {
		return -1, errors.New("send Fail, [c.udpC == nil]")
	}
	return c.udpC.Send(UMarshal(code, data))
}

func (c *MonC) SendTransfer(to Transfer, code uint32, data []byte) (int, error) {
	if c.udpC == nil {
		return -1, errors.New("send Fail, [c.udpC == nil]")
	}
	toLen := int(unsafe.Sizeof(to))
	frame := make([]byte, toLen+len(data))
	Endian.PutUint64(frame[0:], uint64(to.Source))
	Endian.PutUint64(frame[8:], uint64(to.Target))
	Endian.PutUint16(frame[16:], to.TransferLen)
	copy(frame[toLen:], data)
	return c.udpC.Send(UMarshal(code, frame))
}

func (c *MonC) Close() error {
	if c.udpC != nil {
		return c.udpC.Close()
	}
	return nil
}

func (c *MonC) SendProgramHeartbeat(hb program.Heartbeat) {
	if c.udpC == nil {
		return
	}
	jData, err := json.Marshal(hb)
	if err == nil {
		_, _ = c.udpC.Send(UMarshal(CCodeProgramHeartbeatReq, jData))
	}
}

func (c *MonC) SendProgramEvent(programEvent program.Event) {
	if c.udpC == nil {
		return
	}
	jData, err1 := json.Marshal(programEvent)
	if err1 == nil {
		_, _ = c.udpC.Send(UMarshal(CCodeProgramDaemonEventPost, jData))
	}
}

func (c *MonC) CheckChannel() error {
	if c.udpC == nil {
		udpC, err := channels.NewUdpC(c.IP, c.Port, c.C)
		if err != nil {
			return err
		}
		c.udpC = udpC
	}
	return nil
}
