package main

import (
	"fmt"
	"gogo/common/utilities"
	"gogo/internal/monitorc"
	"gogo/internal/program"
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
	msg := formatProgramDaemonEvent(*e, path, maxCount, maxError, utilities.TimeGetCurrentMsepoch())
	// log file
	logger.Println(_titleMons.Info + msg)
	// log db
	logProgramDaemonEvent(e.Status.Id, e.Status.Pid, e.Event, msg)
	// program status change
	if updateDaemonStatus(&e.Status) > 0 {
		thisQueue.Push(monitorc.CodeMessage{Code: monitorc.CCodeProgramDaemonEventPost})
	}
}

// formatProgramDaemonEvent is the single Monitor-side normalization of the
// legacy Daemon event protocol. Keep the original event codes and status
// untouched: old clients still depend on them.
// ErrorCount is updated by Daemon after Wait: a short run increments it,
// while a sufficiently long run resets it.
func formatProgramDaemonEvent(e program.Event, path string, maxCount, maxError int, nowMs int64) string {
	desc := fmt.Sprintf(" ( id:%d; pid:%d; 启动次数:%d/%d; 失败次数:%d/%d )。",
		e.Status.Id, e.Status.Pid, e.Status.StartCount, maxCount, e.Status.ErrorCount, maxError)
	switch e.Event {
	case program.EventMaxError:
		return path + "退出，失败次数太多，不再守护。" + desc
	case program.EventMaxCount:
		return path + "退出，启动次数太多，不再守护。" + desc
	case program.EventStart:
		if e.Status.IsRun {
			return path + "启动成功。" + desc
		}
		// The Daemon sends raw OS errors (usually English). The normalized
		// Chinese failure marker keeps historical incident classification
		// independent of the locale of the original error.
		return path + "启动失败。" + desc + e.Message
	case program.EventExit:
		elapsed := elapsedRunSeconds(nowMs, e.Status.LastRunTime)
		if e.Status.ErrorCount > 0 {
			return fmt.Sprintf("%s异常退出（运行时间过短），运行时长：%d秒。", path, elapsed) + desc + e.Message
		}
		message := fmt.Sprintf("%s退出，运行时长：%d秒。", path, elapsed) + desc
		if e.Message != "" && e.Message != "<nil>" {
			message += e.Message
		}
		return message
	default:
		return ""
	}
}

// elapsedRunSeconds converts the daemon start timestamp (milliseconds)
 // to seconds, without producing a negative value on clock rollback.
func elapsedRunSeconds(nowMs, startedAtMs int64) int64 {
	if nowMs <= startedAtMs || startedAtMs <= 0 {
		return 0
	}
	return (nowMs - startedAtMs) / 1000
}
