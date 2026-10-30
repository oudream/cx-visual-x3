package main

import (
	"bytes"
	"context"
	"encoding/binary"
	"encoding/json"
	"flag"
	"fmt"
	"log"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"

	"gogo/common/utilities"
	"gogo/internal/daemonc"
	"gogo/internal/monitorc"
	"gogo/internal/program"
)

var (
	_myProgram *program.Program
	_logger    *log.Logger
	_title     = utilities.NewLoggerTitles("MAIN")
	// only main thread call
	_thisQueue monitorc.CodeStack
)

const (
	minute5 = 5 * 60 * 1000
)

// safeManagedPID prevents accidental signals to PID 0/process groups,
// negative identifiers, or the supervising Daemon itself. A positive PID is
// still only a candidate: the process ownership/identity must be verified
// before any future remote-control feature is enabled.
func safeManagedPID(pid int) bool {
	return pid > 0 && pid != os.Getpid()
}

func exitPidFiles(programs []program.Program) {
	for i := 0; i < len(programs); i++ {
		a := &programs[i]
		if a.ID == program.DefaultProgramIdDaemon {
			continue
		}
		name := utilities.FileSystemGetFileNameOnly(a.Path)
		if pid := program.LoadProgramRunPid(*_workDir, name); safeManagedPID(pid) {
			// This protects PID 0 and Daemon self from the legacy PID-file
			// cleanup path. Executable identity and PID reuse still require
			// stronger verification before an industrial stop receipt.
			err := program.KillProcess(pid)
			if err != nil {
				_logger.Printf(_title.Error+"Kill[ %s ] Fail. err= %s", name, err)
			} else {
				_logger.Printf(_title.Error+"Kill[ %s ] Success", name)
			}
		}
	}
}

// FOO=bar bash -c 'SomeCommand SomeArgs | SomeCommand2'
// https://stackoverflow.com/questions/10856129/setting-an-environment-variable-before-a-command-in-bash-is-not-working-for-the
func main() {
	flag.Parse()

	if !utilities.FileSystemConfirmWorkDir(_workDir) {
		return
	}

	if program.IsProgramHadRun(*_workDir, program.DefaultProgramNameDaemon) {
		fmt.Println(_title.Error + "Fatal Run, daemon had run!!!")
		os.Exit(-2)
		return
	}

	// init db
	iRowsAffected, err := program.InitDb(*_workDir)
	if err != nil {
		fmt.Printf(_title.Error+"Fatal program.InitDb, err: %s,  Program Will Exit.\n", err)
		os.Exit(-3)
		return
	}

	_myProgram, err = program.GetProgramById(*_workDir, program.DefaultProgramIdDaemon)
	if err != nil {
		// Logger is not initialized until the program definition is loaded.
		fmt.Fprintf(os.Stderr, _title.Error+"Fatal _myProgram=GetProgramById( %d ), err=%s, Program Will Exit.\n", program.DefaultProgramIdDaemon, err)
		os.Exit(-3)
		return
	}

	_logger, err = utilities.InitLogger(utilities.LoggerConfig{
		FilePath: filepath.Join(_myProgram.GetLogPrefixPath(*_workDir) + ".sys"),
		IsStdOut: true,
	})
	if err != nil {
		fmt.Print(_title.Error + "Fatal initLogger, Program Will Exit!!!")
		os.Exit(-4)
		return
	}
	_logger.Printf(_title.Info+"work dir: %s\n", *_workDir)
	_logger.Printf(_title.Info+"Read _myProgram Complete, %s\n", utilities.JsonLogString(_myProgram))
	_logger.Printf(_title.Info+"InitDb Complete, rows affected: %d \n", iRowsAffected)

	err = initConfig()
	if err != nil {
		_logger.Printf(_title.Error+"Fatal initConfig [ %s ], Program Will Exit.\n", err)
		os.Exit(-5)
		return
	}
	_logger.Printf(_title.Info+"Read Config Complete, %d _programs\n", len(_programs))
	for i := 0; i < len(_programs); i++ {
		prog := &_programs[i]
		_logger.Printf(_title.Info+"Read Program Complete, %s\n", utilities.JsonLogString(prog))
	}

	// Restore terminal limits before starting ANY managed program. A corrupt
	// or unwritable local journal must not silently turn a latched program
	// back on after Daemon restart. This is local state, not the Program DB.
	latchPath := filepath.Join(*_workDir, "supervision", "daemon-terminal-latches.json")
	if err := _programWorkers.enablePersistence(latchPath); err != nil {
		_logger.Printf(_title.Error+"Fatal: terminal latch journal %s unavailable: %v; refusing to start managed programs\n", latchPath, err)
		os.Exit(-6)
		return
	}

	workerCtx, cancelWorkers := context.WithCancel(context.Background())
	defer cancelWorkers()
	ticker := time.NewTicker(1000 * time.Millisecond)
	//cbr:c + break 通知守护进程退出的管道
	cbr := make(chan bool, 1)

	csg := make(chan os.Signal, 1)
	// 订阅系统信号:ctrl+c
	signal.Notify(csg, syscall.SIGHUP, syscall.SIGINT, syscall.SIGTERM, syscall.SIGQUIT)
	defer signal.Stop(csg)
	//从管道csg抓取系统信号：:ctrl+c，并从管道cbr发出消息
	go func() {
		for s := range csg {
			switch s {
			case syscall.SIGHUP, syscall.SIGINT, syscall.SIGTERM, syscall.SIGQUIT:
				_logger.Println(_title.Info+"接收到信號-退出: ", s)
				_logger.Println(_title.Info + "开始退出..")
				// 發出退出信號
				cancelWorkers()
				ticker.Stop()
				select {
				case cbr <- true:
				default:
				}
				_logger.Println(_title.Info + "Ticker stopped")
			default:
				_logger.Println(_title.Info+"接收到信號-other: ", s)
			}
		}
	}()

	// exit pid of file ( in dir pid )
	exitPidFiles(_programs)

	bSysSetting := false
	err = checkSysSetting()
	if err != nil {
		_logger.Printf(_title.Error+"Check DB[ mqtt, redis, influxdb ] Connected Status Fail, err: %s.\n", err)
	} else {
		bSysSetting = true
	}
	_logger.Println(_title.Info + "Run Program List:")
	cae := make(chan program.Event)
	for i := 0; i < len(_programs); i++ {
		a := _programs[i]
		if a.ID == program.DefaultProgramIdDaemon {
			continue
		}
		// sys setting no good, only run inspector and monitor
		if !bSysSetting && a.ID != program.DefaultProgramIdCommServer &&
			a.ID != program.DefaultProgramIdMonitor && a.ID != program.DefaultProgramIdMosquitto &&
			a.ID != program.DefaultProgramIdRedis {
			_logger.Printf(_title.Error+"Check DB[ mqtt, redis, influxdb ] Connected Status Fail, program[ %s ] can not be run.\n", a.Path)
			continue
		}
		if a.FirstWaitTime > 0 && !waitForFirstStart(workerCtx, time.Second*time.Duration(a.FirstWaitTime)) {
			break
		}
		launchProgramWorker(workerCtx, a, cae)
		_logger.Println(_title.Info + "Run Program: " + a.Path)
	}

	// 实现监控数据帧处理接口DealMonFrame的结构体，绑定守护进程对监控数据帧处理函数DealMonFrameData的具体逻辑
	dealC := DealMonC{}
	// MonC，设置好监控数据帧处理结构体
	monC, err := monitorc.NewMonC(*_monitorHost, *_monitorPort, &dealC)
	if err != nil {
		_logger.Println(_title.Error, "NewUdpC Fail,", err)
	}
	var dtCheck int64

	programStatuses := make(map[int64]*program.Status)
	programStatuses[program.DefaultProgramIdDaemon] = &program.Status{
		Id:          program.DefaultProgramIdDaemon,
		Pid:         os.Getpid(),
		IsRun:       true,
		LastRunTime: time.Now().UnixMilli(),
	}

	program.SaveSelfPidFile(*_workDir, program.DefaultProgramNameDaemon)
	_logger.Printf(_title.Info+"%s Start, %s.\n", program.DefaultProgramNameDaemon, *_workDir)

	var dtHeartbeat int64
	dtRecovery := time.Now().UnixMilli() + minute5
	bRunning := true
	for bRunning {
		select {
		// os.Signal
		case <-cbr:
			_logger.Println(_title.Info + "执行清理..")
			_logger.Println(_title.Info + "结束退出..")
			// Signal cancellation first so restart waits and blocked event sends
			// stop before the main loop stops draining program events.
			cancelWorkers()
			daemonc.DaemonRun.Store(false)
			for ProgramId, pid := range _runProgramIds {
				if safeManagedPID(pid) {
					proc, err2 := os.FindProcess(pid)
					if err2 == nil && proc != nil {
						_ = proc.Kill()
						a, err := findProgramById(ProgramId)
						if err == nil {
							program.DeleteProgramPidFile(*_workDir, a.GetFileName())
						}
						//time.Sleep(time.Millisecond * 100)
					}
				}
			}
			bRunning = false

		// ticker
		case <-ticker.C:
			dtNow := utilities.TimeGetCurrentMsepoch()
			if dtNow-dtCheck > 3000 {
				err := monC.CheckChannel()
				if err != nil {
					_logger.Println(_title.Error, "NewUdpC Fail,", err)
				}
				dtCheck = dtNow
			}
			if dtNow-dtHeartbeat > 10000 {
				monC.SendProgramHeartbeat(program.Heartbeat{ProgramId: program.DefaultProgramIdDaemon, PId: os.Getpid(), Healthy: 0})
				dtHeartbeat = dtNow
			}
			if dtNow-dtRecovery > 0 {
				recoveryPrograms(workerCtx, cae)
				dtRecovery += minute5
			}

		// monC 监视客户端协程管道
		case rData := <-monC.C:
			if rData.Err == nil {
				monC.Frame.Unmarshal(rData.Data)
				for !_thisQueue.IsEmpty() {
					if codeMessage, ok := _thisQueue.Pop(); ok {
						// Employ ProgramDiff
						if codeMessage.Code == monitorc.CCodeProgramDiffEmployReq {
							var ad program.Diff
							err = json.Unmarshal(codeMessage.Data, &ad)
							if err == nil {
								_logger.Printf(_title.Info+"program diff-employ %s program[%s](%d)", ad.Action, ad.Program.Path, ad.Program.ID)
								// sync to _programs
								var bKill = false
								var bRun = false
								if ad.Action == program.ActionInsert {
									addProgram(ad.Program)
									bRun = true
								} else if ad.Action == program.ActionUpdate {
									oldEnabled, newEnabled, err := updateProgram(ad.Program)
									if err != nil {
										_logger.Printf(_title.Info+"program diff-employ %s program[%s](%d) updateProgram err: %s\n", ad.Action, ad.Program.Path, ad.Program.ID, err)
									} else {
										bKill = true
										bRun = newEnabled && oldEnabled != newEnabled
									}
								} else {
									deleteProgram(ad.ID)
									bKill = true
								}
								// sync to daemonc
								if bKill {
									if pid, ok := _runProgramIds[ad.ID]; ok && safeManagedPID(pid) {
										proc, err := os.FindProcess(pid)
										if err == nil && proc != nil {
											_ = proc.Kill()
											delete(_runProgramIds, ad.ID)
											if bRun {
												time.Sleep(time.Millisecond * 200)
											}
											_logger.Printf(_title.Info+"program diff-employ %s and kill program[%s](%d)", ad.Action, ad.Path, ad.ID)
										}
									}
								}
								// Only a real enabled-start configuration intent may queue
								// behind a finishing worker; periodic recovery never queues.
								if bKill && !bRun {
									_programWorkers.clearPending(ad.ID)
								}
								if bRun {
									started, queued := requestProgramWorker(workerCtx, ad.Program, cae)
									switch {
									case started:
										_logger.Printf(_title.Info+"program diff-employ %s and run program[%s](%d)", ad.Action, ad.Path, ad.ID)
									case queued:
										_logger.Printf(_title.Info+"program diff-employ %s queued after existing guardian program[%s](%d)", ad.Action, ad.Path, ad.ID)
									default:
										_logger.Printf(_title.Info+"program diff-employ %s refused by stop/terminal latch program[%s](%d)", ad.Action, ad.Path, ad.ID)
									}
								}
							} else {
								_logger.Printf(_title.Info+"program diff-employ fail, json unmarshal err: ", err)
							}

							// Request Status Of Programs
						} else if codeMessage.Code == monitorc.CCodeProgramStatusReq {
							var statuses []*program.Status
							for _, status := range programStatuses {
								statuses = append(statuses, status)
							}
							if len(statuses) > 0 {
								data, err := json.Marshal(statuses)
								if err == nil {
									_, _ = monC.Send(monitorc.CCodeProgramStatusResp, data)
								}
							}

							// Employ Result of Checking Dbs
						} else if codeMessage.Code == monitorc.CCodeCheckDbsEmployReq {
							var dbsState monitorc.DbsState
							//var info Register
							buf := bytes.NewBuffer(codeMessage.Data)
							err = binary.Read(buf, monitorc.Endian, &dbsState)
							if err != nil {
								fmt.Println("checkDbsEmployReq Fail, binary.Read failed:", err)
							}
							if dbsState.InfluxdbState && dbsState.MqttState && dbsState.RedisState {
								recoveryPrograms(workerCtx, cae)
								dtRecovery += minute5
							}

						} else if codeMessage.Code == monitorc.CCodeDaemonRestartProgramReq {
							var ad program.Diff
							err = json.Unmarshal(codeMessage.Data, &ad)
							if err == nil {
								_logger.Printf(_title.Info+"daemon restart program req, program[%s](%d)", ad.Path, ad.ID)
								bProgramHasRun := false
								if pid, ok := _runProgramIds[ad.ID]; ok && safeManagedPID(pid) {
									proc, err := os.FindProcess(pid)
									if err == nil && proc != nil {
										_ = proc.Kill()
										delete(_runProgramIds, ad.ID)
										_logger.Printf(_title.Info+"daemon restart program req, send kill signal to program[%s](%d)", ad.Path, ad.ID)
									}
									bProgramHasRun = pid > 0
								}
								if !bProgramHasRun && ad.Enabled == program.ENABLE {
									if launchProgramWorker(workerCtx, ad.Program, cae) {
										_logger.Printf(_title.Info+"daemon restart program req, run program[%s](%d)", ad.Path, ad.ID)
									} else {
										_logger.Printf(_title.Info+"daemon restart program req, existing guardian retained program[%s](%d)", ad.Path, ad.ID)
									}
								}
							} else {
								_logger.Printf(_title.Info+"daemon kill req fail, json unmarshal err: ", err)
							}
						}
					}
				}
			} else {

			}

		// daemon 守护协程管道
		case programEvent := <-cae:
			aProgram, err := findProgramById(programEvent.Status.Id)
			if err != nil {
				aProgram.MaxCount = -1
				aProgram.MaxError = -1
			}
			aInfo := fmt.Sprintf(" ( id:%d; pid:%d; 启动次数:%d/%d; 失败次数:%d/%d )。",
				programEvent.Status.Id, programEvent.Status.Pid, programEvent.Status.StartCount, aProgram.MaxCount, programEvent.Status.ErrorCount, aProgram.MaxError)
			if programEvent.Status.Id > 0 {
				_runProgramIds[programEvent.Status.Id] = 0
			}
			switch programEvent.Event {
			case program.EventMaxError:
				_logger.Println(_title.Info + aProgram.Path + "退出，失败次数太多，不再守护。" + aInfo)
			case program.EventMaxCount:
				_logger.Println(_title.Info + aProgram.Path + "退出，启动次数太多，不再守护。" + aInfo)
			case program.EventStart:
				if programEvent.Status.IsRun {
					_runProgramIds[programEvent.Status.Id] = programEvent.Status.Pid
					if aProgram.ID > program.DefaultProgramIdDaemon && aProgram.ID < program.DefaultProgramIdCore {
						_ = program.SaveProgramPidFile(*_workDir, utilities.FileSystemGetFileNameOnly(aProgram.GetFileName()), programEvent.Status.Pid)
					}
					_logger.Println(_title.Info + aProgram.Path + " 启动成功。" + aInfo)
				} else {
					_logger.Println(_title.Info + aProgram.Path + " 启动失败。" + aInfo + programEvent.Message)
				}
			case program.EventExit:
				_logger.Println(_title.Info + formatDaemonExitEvent(aProgram.Path, programEvent, aInfo, time.Now().UnixMilli()))
			}
			programStatuses[programEvent.Status.Id] = &programEvent.Status
			if existProgram(programEvent.Status.Id) {
				monC.SendProgramEvent(programEvent)
			}
		}
	}

	program.ExitProgramRun(*_workDir, program.DefaultProgramNameDaemon)
	_logger.Println(_title.Info + "Daemon End")
}


// waitForFirstStart allows Daemon shutdown to interrupt first-start delays
// without creating or killing a process in the waiting worker.
func waitForFirstStart(ctx context.Context, delay time.Duration) bool {
	if ctx.Err() != nil {
		return false
	}
	if delay <= 0 {
		return true
	}
	timer := time.NewTimer(delay)
	defer timer.Stop()
	select {
	case <-ctx.Done():
		return false
	case <-timer.C:
		return ctx.Err() == nil
	}
}

func recoveryPrograms(ctx context.Context, cae chan program.Event) {
	// A disk failure while latching a terminal event disables all automatic
	// admissions until the journal is repaired; never wait FirstWaitTime.
	if _programWorkers.persistenceError() != nil {
		return
	}
	ps := getPrograms()
	for i := 0; i < len(ps); i++ {
		a := ps[i]
		if a.ID == program.DefaultProgramIdDaemon || a.Enabled != program.ENABLE {
			continue
		}
		// Terminal limits are latched before the terminal Event is sent.
		// Recovery must not wait FirstWaitTime or restart a latched Program.
		if _, blocked := _programWorkers.blocked(a.ID); blocked {
			continue
		}
		bProgramHasRun := false
		if pid, ok := _runProgramIds[a.ID]; ok {
			bProgramHasRun = pid > 0
		}
		// PID=0 is not proof that a guardian has finished: it may be
		// starting, in backoff, or waiting for its child to exit.
		if bProgramHasRun || _programWorkers.has(a.ID) {
			continue
		}
		if a.FirstWaitTime > 0 && !waitForFirstStart(ctx, time.Second*time.Duration(a.FirstWaitTime)) {
			return
		}
		if launchProgramWorker(ctx, a, cae) {
			_logger.Printf(_title.Info+"run program[%s](%d)", a.Path, a.ID)
		}
	}
}

// formatDaemonExitEvent preserves the original Daemon event/status contract
// while displaying the millisecond LastRunTime in correct seconds. Daemon
// resets ErrorCount for sufficiently long-lived processes before emitting
// EventExit; a nonzero value denotes an abnormal short-lived exit.
func formatDaemonExitEvent(path string, event program.Event, desc string, nowMs int64) string {
	elapsedSeconds := int64(0)
	startedMs := event.Status.LastRunTime
	if startedMs > 0 && nowMs >= startedMs {
		elapsedSeconds = (nowMs - startedMs) / 1000
	}
	if event.Status.ErrorCount > 0 {
		return fmt.Sprintf("%s异常退出（运行时间过短），运行时长：%d秒。", path, elapsedSeconds) + desc + event.Message
	}
	message := fmt.Sprintf("%s退出，运行时长：%d秒。", path, elapsedSeconds) + desc
	if event.Message != "" && event.Message != "<nil>" {
		message += event.Message
	}
	return message
}
