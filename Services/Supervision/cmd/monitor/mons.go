package main

import (
	"encoding/json"
	"errors"
	"fmt"
	"gogo/common/channels"
	"gogo/common/utilities"
	"gogo/internal/monitorc"
	"gogo/internal/program"
	"net"
)

var (
	_titleMons             = utilities.NewLoggerTitles("MONS")
	_programHeartbeatTimes map[int64]int64
)

func init() {
	_programHeartbeatTimes = make(map[int64]int64)
}

// --- --- ---

// MonA 在服务端中对某个客户端(MonC)的解析器

type MonA struct {
	HP            string
	UdpAddr       net.UDPAddr
	Frame         *monitorc.MonFrame
	ProgramId     int64
	HeartbeatTime int64
	Healthy       int
	LastTime      int64
	S             *MonS
}

// MonS：监视器的服务端
// udpS: Udp通道的服务端
// C: 用来接收Udp监视器的报文
// monAs：客户端MAP,key为：进程程序id

type MonS struct {
	IP    string
	Port  int
	udpS  *channels.UdpS
	C     chan channels.UdpSRecData
	monAs map[string]*MonA
}

func NewMonS(ip string, port int) (*MonS, error) {
	// chan是协程间的通道，只能单向通信
	// channels.UdpSRecData 是通道上数据类型
	c := make(chan channels.UdpSRecData)
	udpS, err := channels.NewUdpS(ip, port, c)
	if err != nil {
		return nil, err
	}
	//
	r := &MonS{
		udpS:  udpS,
		C:     c,
		monAs: make(map[string]*MonA),
	}
	return r, nil
}

// 统一处理所有monc客户端发过来的报文
func (a *MonA) DealMonFrameData(code uint32, data []byte) {
	if code&monitorc.CCodeFlagTransfer > 0 {
		var to monitorc.Transfer
		//to.Source = int64(monitorc.Endian.Uint64(data[0:]))
		to.Target = int64(monitorc.Endian.Uint64(data[8:]))
		//to.TransferLen = monitorc.Endian.Uint16(data[16:])
		packet := monitorc.UMarshal(code, data)
		//除了监视进程以外的包，直接转发：monS.SendUdpSDataByProgramId(packet, to.Target)
		if to.Target == program.DefaultProgramIdMonitor {
			switch code {
			case monitorc.CCodeProgramDiffEmployReq:
				thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramDiffEmployReq, Data: packet})
			case monitorc.CCodeCheckDbsEmployReq:
				thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeCheckDbsEmployReq, Data: packet})
			}
		} else {
			_, _ = monS.SendUdpSDataByProgramId(packet, to.Target)
		}
	} else if code&monitorc.CCodeFlagBroadcast > 0 {
		_, _ = monS.SendUdpSDataBroadcast(code, data, program.DefaultProgramIdMonitor)
	} else {
		switch code {
		case monitorc.CCodeProgramHeartbeatReq:
			a.dealProgramHeartbeat(data)
		case monitorc.CCodeProgramDaemonEventPost:
			a.dealProgramDaemonEvent(data)
		case monitorc.CCodeProgramStatusResp:
			a.dealProgramStatusResp(data)
		}
	}
}

func (a *MonA) dealProgramHeartbeat(data []byte) {
	var hb program.Heartbeat
	err := json.Unmarshal(data, &hb)
	if err != nil {
		logger.Printf(_titleMons.Error+"Deal Program Heartbeat Fail, Internal json.Unmarshal err: %s\n", err)
		return
	}
	ProgramId := hb.ProgramId
	// first login
	if !existHeartbeatProgram(ProgramId) {
		if a.ProgramId == 0 {
			logger.Printf(_titleMons.Info+"Program[ProgramId %d] Login\n", ProgramId)
		} else {
			logger.Printf(_titleMons.Error+"Program[ProgramId %d] Internal Error, Udp Socket Repeat Use...\n", ProgramId)
		}
		a.ProgramId = ProgramId
		logProgramLogin(ProgramId, hb.PId)

		// normal heartbeat
	} else {
		bLog := true
		dtNow := utilities.TimeGetCurrentMsepoch()
		if t, ok := _programHeartbeatTimes[ProgramId]; ok {
			bLog = dtNow-t > 60000
		}
		if bLog {
			logProgramHeartbeat(ProgramId, hb.PId)
		}
		_programHeartbeatTimes[ProgramId] = dtNow
		logger.Printf(_titleMons.Info+"Program[ProgramId %d] Heartbeat\n", ProgramId)
	}

	// update program status
	updateHeartbeatStatus(&hb)

	// response heartbeat
	a.respProgramHeartbeat()
}

func (a *MonA) respProgramHeartbeat() {
	body := []byte{1, 2, 3, 4}
	data := monitorc.UMarshal(monitorc.CCodeProgramHeartbeatResp, body)
	r, err := a.S.SendUdpSData(data, &a.UdpAddr)
	if err != nil {
		logger.Printf(_titleMons.Error+"Resp Program Heartbeat Fail, err: %s\n", err)
	} else {
		logger.Printf(_titleMons.Info+"Resp Program Heartbeat [ %d ].\n", r)
	}
}

func (a *MonA) dealProgramDaemonEvent(data []byte) {
	var event program.Event
	if err := json.Unmarshal(data, &event); err != nil || !validDaemonStatus(event.Status) ||
		event.Event < program.EventStart || event.Event > program.EventMaxCount {
		logger.Printf(_titleMons.Error+"Rejected invalid Daemon event JSON/status: %v", err)
		return
	}
	dealProgramDaemonEvent(&event)
}

func (a *MonA) dealProgramStatusResp(data []byte) {
	var statuses []program.Status
	if err := json.Unmarshal(data, &statuses); err != nil || statuses == nil {
		logger.Printf(_titleMons.Error+"Rejected invalid Daemon status JSON: %v", err)
		return
	}
	seen := make(map[int64]struct{}, len(statuses))
	for _, status := range statuses {
		_, duplicate := seen[status.Id]
		if !validDaemonStatus(status) || duplicate {
			logger.Printf(_titleMons.Error+"Rejected invalid/duplicate Daemon status Program=%d", status.Id)
			return
		}
		seen[status.Id] = struct{}{}
	}
	_programStatusRespTime = utilities.TimeGetCurrentTimestamp()
	if updateDaemonStatuses(statuses) > 0 {
		thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramStatusResp})
	}
	logger.Println(_title.Info + "monS dealProgramStatusResp()")
}

// A syntactically damaged or contradictory response must not make retained
// snapshots appear freshly observed. The existing wire fields remain intact.
func validDaemonStatus(status program.Status) bool {
	return status.Id > 0 && status.Pid >= 0 && (!status.IsRun || status.Pid > 0) &&
		status.StartCount >= 0 && status.StartFailCount >= 0 && status.ErrorCount >= 0 && status.LastRunTime >= 0
}

// channel

func (s *MonS) CheckChannel() {
	if s.udpS == nil {
		udpS, err1 := channels.NewUdpS(s.IP, s.Port, s.C)
		if err1 != nil {
			logger.Println(_titleMons.Error, "NewUdpS Fail,", err1)
		}
		s.udpS = udpS
	}
}

func (s *MonS) DealUdpSData(sData *channels.UdpSRecData) {
	hp := sData.IPAddr.String()
	a, ok := s.monAs[hp]
	if !ok {
		a = &MonA{
			HP:      hp,
			UdpAddr: sData.IPAddr,
		}
		a.LastTime = utilities.TimeGetCurrentMsepoch()
		a.Frame = monitorc.CreateMonFrame(a)
		a.S = s
		s.monAs[hp] = a
	}
	a.Frame.Unmarshal(sData.Data)
}

func (s *MonS) SendUdpSData(data []byte, addr *net.UDPAddr) (int, error) {
	if s.udpS == nil {
		return -1, errors.New("udp Server Is nil")
	}
	return s.udpS.Send(data, addr)
}

func (s *MonS) SendUdpSDataByProgramId(data []byte, ProgramId int64) (int, error) {
	if s.udpS == nil {
		return -1, errors.New("udp Server Is nil")
	}
	//
	var ma *MonA
	for _, v := range s.monAs {
		if v.ProgramId == ProgramId {
			ma = v
			break
		}
	}
	if ma == nil {
		return -2, errors.New(fmt.Sprintf("Can Not Find MonA[ProgramId=%d]", ProgramId))
	}
	return s.udpS.Send(data, &ma.UdpAddr)
}

func (s *MonS) SendUdpSDataBroadcast(code uint32, data []byte, excludeProgram int64) (int, error) {
	if s.udpS == nil {
		return -1, errors.New("udp Server Is nil")
	}
	iSent := 0
	for _, ma := range s.monAs {
		if ma.ProgramId != excludeProgram {
			//to.Source = int64(monitorc.Endian.Uint64(data[0:]))
			monitorc.Endian.PutUint64(data[8:], uint64(ma.ProgramId))
			//to.TransferLen = monitorc.Endian.Uint16(data[16:])
			packet := monitorc.UMarshal(code, data)
			r, err := s.udpS.Send(packet, &ma.UdpAddr)
			if err != nil {
				return iSent, err
			}
			iSent += r
		}
	}
	return iSent, nil
}
