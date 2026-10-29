package main

import (
	"github.com/shirou/gopsutil/v3/process"
	"time"
)

// ProcessStatus mem is process.MemoryInfoStat
type ProcessStatus struct {
	Cpu       float64 `json:"Cpu"`       // percent
	MemRSS    uint64  `json:"MemRss"`    // bytes
	MemVMS    uint64  `json:"MemVms"`    // bytes
	MemHWM    uint64  `json:"MemHwm"`    // bytes
	MemData   uint64  `json:"MemData"`   // bytes
	MemStack  uint64  `json:"MemStack"`  // bytes
	MemLocked uint64  `json:"MemLocked"` // bytes
	MemSwap   uint64  `json:"MemSwap"`   // bytes
}

func calcProcessStatus(pids []int32, exited chan bool) {
	// create process
	// calc cpu
	// get mem
	// chan exited

	var ps []*process.Process
	for i := 0; i < len(pids); i++ {
		if proc, err := process.NewProcess(pids[i]); err == nil {
			ps = append(ps, proc)
		}
	}

	ticker := time.NewTicker(1000 * time.Millisecond)

	bRun := true
	for bRun {
		select {
		case <-exited:
			//fmt.Println("MAIN - 结束退出..")
			bRun = false
			ticker.Stop()
			break
		case <-ticker.C:
			//fmt.Println("Tick at", t)
			var ss map[int]ProcessStatus
			ss = make(map[int]ProcessStatus)
			for i := 0; i < len(ps); i++ {
				p := ps[i]
				s := ProcessStatus{}
				if v, e := p.Percent(0); e == nil {
					s.Cpu = v
				}
				if v, e := p.MemoryInfo(); e == nil {
					s.MemRSS = v.RSS
					s.MemVMS = v.VMS
					s.MemHWM = v.HWM
					s.MemData = v.Data
					s.MemStack = v.Stack
					s.MemLocked = v.Locked
					s.MemSwap = v.Swap
				}
				ss[int(p.Pid)] = s
			}
			func() {
				_programStatusesMu.Lock()
				defer _programStatusesMu.Unlock()
				for _, as := range _programStatuses {
					if s, ok := ss[as.DaemonStatus.Pid]; ok {
						as.ProcessStatus = s
					}
				}
			}()
		}
	}
}
