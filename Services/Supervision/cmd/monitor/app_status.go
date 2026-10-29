package main

import (
	"gogo/common/utilities"
	"gogo/internal/monitorc"
	"gogo/internal/program"
	"sync"
)

var (
	// _programStatuses 1 received from daemon(sub.thread)
	// _programStatuses 2 received from program heartbeat(sub.thread)
	_programStatuses       map[int64]*ProgramStatus
	_programStatusesMu     sync.Mutex
	_programStatusRespTime int64
)

type ProgramStatus struct {
	DaemonStatus    program.Status
	HeartbeatStatus HeartbeatStatus
	ProcessStatus   ProcessStatus
	Path            string `json:"Path"`
	MaxCount        int    `json:"MaxCount"`
	MaxError        int    `json:"MaxError"`
}

type HeartbeatStatus struct {
	Time    int64 `json:"Time"`
	Healthy int   `json:"Healthy"`
}

type EditType int

const (
	EditTypeInsert EditType = iota
	EditTypeUpdate
	EditTypeDelete
	EditTypeEnum
)

func init() {
	_programStatuses = make(map[int64]*ProgramStatus)
}

// 初始化进程列表（守护的程序列表：从表comm_program_run读取）状态信息

// modify _programStatuses
func initProgramStatuses() error {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	programs, err := program.GetProgramRunList(*_workDir)
	if err == nil {
		// delete not exist
		for ProgramId, status := range _programStatuses {
			bExist := false
			for i := 0; i < len(programs); i++ {
				a := &programs[i]
				if a.ID == status.DaemonStatus.Id {
					bExist = true
				}
			}
			if !bExist {
				delete(_programStatuses, ProgramId)
			}
		}
		// add new
		for i := 0; i < len(programs); i++ {
			a := &programs[i]
			if _, ok := _programStatuses[a.ID]; !ok {
				_programStatuses[a.ID] = &ProgramStatus{
					DaemonStatus: program.Status{
						Id:    a.ID,
						IsRun: false,
					},
					Path:     a.Path,
					MaxCount: a.MaxCount,
					MaxError: a.MaxError,
				}
			}
		}
	}
	return err
}

func existHeartbeatProgram(ProgramId int64) bool {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	// save to cache
	if s, ok := _programStatuses[ProgramId]; ok {
		return s.HeartbeatStatus.Time > 0
	} else {
		return false
	}
}

func getProgramInfo(ProgramId int64) (string, int, int) {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	// save to cache
	if s, ok := _programStatuses[ProgramId]; ok {
		return s.Path, s.MaxCount, s.MaxError
	}
	return "", -1, -1
}

// modify _programStatuses
// return pid change count
func updateDaemonStatus(status *program.Status) int {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	pidChange := 0
	// save to cache
	if s, ok := _programStatuses[status.Id]; ok {
		if s.DaemonStatus.Pid != status.Pid {
			pidChange++
		}
		s.DaemonStatus = *status
	} else {
		_programStatuses[status.Id] = &ProgramStatus{DaemonStatus: *status}
		pidChange++
	}
	return pidChange
}

func daemonStatusesReq() {
	body := []byte{1, 2, 3, 4}
	data := monitorc.UMarshal(monitorc.CCodeProgramStatusReq, body)
	_, _ = monS.SendUdpSDataByProgramId(data, program.DefaultProgramIdDaemon)
}

// modify _programStatuses
// return pid change count
func updateDaemonStatuses(statuses []program.Status) int {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	pidChange := 0
	for i := 0; i < len(statuses); i++ {
		status := &statuses[i]
		// save to cache
		if s, ok := _programStatuses[status.Id]; ok {
			if s.DaemonStatus.Pid != status.Pid {
				pidChange++
			}
			s.DaemonStatus = *status
		} else {
			_programStatuses[status.Id] = &ProgramStatus{DaemonStatus: *status}
			pidChange++
		}
	}
	return pidChange
}

func updateHeartbeatStatus(hb *program.Heartbeat) EditType {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	// save to cache
	if s, ok := _programStatuses[hb.ProgramId]; ok {
		s.HeartbeatStatus.Time = utilities.TimeGetCurrentMsepoch()
		s.HeartbeatStatus.Healthy = hb.Healthy
		return EditTypeUpdate
	} else {
		_programStatuses[hb.ProgramId] = &ProgramStatus{
			DaemonStatus: program.Status{
				Id:    hb.ProgramId,
				Pid:   hb.PId,
				IsRun: true,
			},
			HeartbeatStatus: HeartbeatStatus{Time: utilities.TimeGetCurrentMsepoch(), Healthy: hb.Healthy},
		}
		return EditTypeInsert
	}
}

func updateHeartbeatTime(ProgramId int64) {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	// save to cache
	if s, ok := _programStatuses[ProgramId]; ok {
		s.HeartbeatStatus.Time = utilities.TimeGetCurrentMsepoch()
		s.DaemonStatus.IsRun = true
	}
}

func getProgramStatuses() []ProgramStatus {
	_programStatusesMu.Lock()
	defer _programStatusesMu.Unlock()
	var ss []ProgramStatus
	for _, s := range _programStatuses {
		ss = append(ss, *s)
	}
	return ss
}
