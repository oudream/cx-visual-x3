package main

import (
	"fmt"
	"gogo/common/utilities"
	"gogo/internal/monitorc"
	"gogo/internal/program"
	"time"
)

var (
	_titleProgramEvent = utilities.NewLoggerTitles("APP-EVENT")
)

func logProgramLogin(ProgramId int64, pid int) {
	record := ProgramEventRecord{
		ProgramId: ProgramId,
		PId:       pid,
		Event:     program.EventLogin,
		Time:      utilities.TimeGetCurrentMsepoch(),
		Msg:       "",
	}
	err := SaveRecord(*_workDir, record)
	if err != nil {
		logger.Println(_titleProgramEvent.Error + fmt.Sprintf("Program Login Insert failed. err: %s, program: %+v", err, record))
	}
}

func logProgramHeartbeat(ProgramId int64, pid int) {
	record := ProgramEventRecord{
		ProgramId: ProgramId,
		PId:       pid,
		Event:     program.EventHeartbeat,
		Time:      utilities.TimeGetCurrentMsepoch(),
		Msg:       "",
	}
	err := SaveRecord(*_workDir, record)
	if err != nil {
		logger.Println(_titleProgramEvent.Error + fmt.Sprintf("Program Heartbeat Insert failed. err: %s, program: %+v", err, record))
	}
}

func logProgramDaemonEvent(ProgramId int64, pid int, event int, msg string) {
	record := ProgramEventRecord{
		ProgramId: ProgramId,
		PId:       pid,
		Event:     event,
		Time:      utilities.TimeGetCurrentMsepoch(),
		Msg:       msg,
	}
	err := SaveRecord(*_workDir, record)
	if err != nil {
		logger.Println(_titleProgramEvent.Error + fmt.Sprintf("Program Daemon Event Insert failed. err: %s, program: %+v", err, record))
	}
}

func dealProgramDaemonEvent(e *program.Event) {
	path, maxCount, maxError := getProgramInfo(e.Status.Id)
	desc := fmt.Sprintf(" ( id:%d; pid:%d; 启动次数:%d/%d; 失败次数:%d/%d )。",
		e.Status.Id, e.Status.Pid, e.Status.StartCount, maxCount, e.Status.ErrorCount, maxError)
	msg := ""
	switch e.Event {
	case program.EventMaxError:
		msg = path + "退出，失败次数太多，不再守护。" + desc
	case program.EventMaxCount:
		msg = path + "退出，启动次数太多，不再守护。" + desc
	case program.EventStart:
		if e.Status.IsRun {
			msg = path + "启动成功。" + desc
		} else {
			msg = path + "启动失败。" + desc + e.Message
		}
	case program.EventExit:
		if e.Status.ErrorCount > 0 {
			msg = fmt.Sprintf(path+"退出，运行时长：%d。", time.Now().Unix()-e.Status.LastRunTime) + desc
		} else {
			msg = path + "退出，运行时间太短" + desc + e.Message
		}
	}
	// log file
	logger.Println(_titleMons.Info + msg)
	// log db
	logProgramDaemonEvent(e.Status.Id, e.Status.Pid, e.Event, msg)
	// program status change
	if updateDaemonStatus(&e.Status) > 0 {
		thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramDaemonEventPost})
	}
}
