package main

import (
	"gogo/internal/monitorc"
)

type DealMonC struct{}

func (c *DealMonC) DealMonFrameData(code uint32, data []byte) {
	if code&monitorc.CCodeFlagTransfer > 0 {
		to, bd := monitorc.ExtractTransfer(data)
		if to != nil {
			switch code {
			case monitorc.CCodeProgramDiffEmployReq:
				_thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramDiffEmployReq, Data: bd})
			case monitorc.CCodeCheckDbsEmployReq:
				_thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeCheckDbsEmployReq, Data: bd})
			case monitorc.CCodeDaemonRestartProgramReq:
				_thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeDaemonRestartProgramReq, Data: bd})
			}
		}
	} else if code&monitorc.CCodeFlagBroadcast > 0 {
		to, _ := monitorc.ExtractTransfer(data)
		if to != nil {
			switch code {
			}
		}
	} else {
		switch code {
		case monitorc.CCodeProgramStatusReq:
			_thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramStatusReq})
		}
	}
}
