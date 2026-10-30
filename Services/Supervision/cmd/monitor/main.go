package main

import (
	"bytes"
	"encoding/binary"
	"flag"
	"fmt"
	"gogo/common/channels"
	"gogo/common/utilities"
	"gogo/internal/monitorc"
	"gogo/internal/program"
	"log"
	"os"
	"os/signal"
	"path/filepath"
	"syscall"
	"time"
)

// *MonS 通过指针实现全局引用
var (
	myProgram *program.Program
	logger    *log.Logger
	_title    = utilities.NewLoggerTitles("MAIN")
	monS      *MonS
	thisQueue monitorc.CodeStack
)

// /opt/dev/device_communicator/assets
func main() {
	flag.Parse()

	if !utilities.FileSystemConfirmWorkDir(_workDir) {
		return
	}

	if program.IsProgramHadRun(*_workDir, program.DefaultProgramNameMonitor) {
		fmt.Println(_title.Error + "Fatal Run, monitor had run!!!")
		os.Exit(-2)
		return
	}

	var err error
	// myProgram：是从数据库monitor.db，表名comm_program_run
	// 根据ProgramID（核心进程的ProgramID是常量，通信器从-i入参来）匹配出一行的数据结构
	// 核心进程：hy3-daemon、hy3-monitor、hy3-comm-dispatcher、hy3-comm-scraper、hy3-comm-server、hy3-mqtt-transmission、mosquitto、redis-server（influxDB是独立不受监控）
	myProgram, err = program.GetProgramById(*_workDir, program.DefaultProgramIdMonitor)
	if err != nil {
		fmt.Printf(_title.Error+"Fatal myProgram=GetProgramById( %d ), err=%s, Program Will Exit.\n", program.DefaultProgramIdMonitor, err)
		os.Exit(-3)
		return
	}

	logger, err = utilities.InitLogger(utilities.LoggerConfig{
		FilePath: filepath.Join(myProgram.GetLogPrefixPath(*_workDir) + ".sys"),
		IsStdOut: true,
	})
	if err != nil {
		fmt.Printf(_title.Error+"Fatal initLogger [ %s ], Program Will Exit.\n", err)
		os.Exit(-4)
		return
	}
	logger.Printf(_title.Info+"work dir: %s\n", *_workDir)
	logger.Printf(_title.Info+"Read myProgram Complete, %s\n", utilities.JsonLogString(myProgram))

	// init local config
	err = initConfig()
	if err != nil {
		logger.Printf(_title.Error+"Fatal local initConfig [ %s ], Program Will Exit.\n", err)
		os.Exit(-5)
		return
	}
	logger.Printf(_title.Info+"Read Config Complete, %s\n", utilities.JsonLogString(config))

	err = initProgramStatuses()
	if err != nil {
		logger.Println(_title.Error+"initProgramStatuses Fail, err: ", err)
		os.Exit(-6)
		return
	}

	// append self
	_programStatuses[program.DefaultProgramIdMonitor] = &ProgramStatus{DaemonStatus: program.Status{Id: program.DefaultProgramIdMonitor,
		Pid: os.Getpid(), IsRun: true, LastRunTime: utilities.TimeGetCurrentMsepoch()}}

	// http server
	assetsPath := filepath.Join(*_workDir, "assets")
	if len(*_assetsDir) > 0 {
		if !filepath.IsAbs(*_assetsDir) {
			assetsPath = filepath.Join(*_workDir, *_assetsDir)
		} else {
			assetsPath = *_assetsDir
		}
	}
	httpAddr := fmt.Sprintf("%s:%d", config.Monitor.Http.Host, config.Monitor.Http.Port)
	if httpListenMayBeRemote(config.Monitor.Http.Host) {
		logger.Printf(_title.Error+"SECURITY WARNING: Monitor HTTP listener %s may be reachable remotely; legacy /programs_status has no authentication. Restrict it with bind/firewall ACLs or an approved authenticated gateway. New /api/v1 process snapshots are loopback-only.\n", httpAddr)
	}
	httpServer := channels.NewHttpServer(logger, config.Monitor.Http.Port, assetsPath, "/assets/comm/index.html", false)
	setupHttpServer(httpServer.Engine, httpServer.Server)
	// start http server
	go httpServer.Run(httpAddr)

	// monitor server
	monS, err = NewMonS(*_monitorHost, *_monitorPort)
	if err != nil {
		logger.Println(_title.Error, "NewUdpS Fail,", err)
		os.Exit(-8)
	}

	// timer loop
	ticker := time.NewTicker(500 * time.Millisecond)
	cbr := make(chan bool)

	// timer once , check daemon and run daemon ( after self run )
	timer := time.NewTimer(1 * time.Second)

	// exit signal
	csg := make(chan os.Signal, 1)
	signal.Notify(csg, syscall.SIGHUP, syscall.SIGINT, syscall.SIGTERM, syscall.SIGQUIT)
	defer signal.Stop(csg)

	// thread receive exit signal
	go func() {
		for s := range csg {
			switch s {
			case syscall.SIGHUP, syscall.SIGINT, syscall.SIGTERM, syscall.SIGQUIT:
				logger.Println(_title.Info+"接收到信號-退出: ", s)
				logger.Println(_title.Info + "开始退出..")
				// 發出退出信號
				ticker.Stop()
				// exit http server, stop, Shutdown
				httpServer.Close()
				// exit main thread
				cbr <- true
				logger.Println(_title.Info + "ticker , httpServer and main-thread stop")
			default:
				logger.Println(_title.Info+"接收到信號-other: ", s)
			}
		}
	}()

	program.SaveSelfPidFile(*_workDir, program.DefaultProgramNameMonitor)
	logger.Printf(_title.Info+"%s Start\n", myProgram.Path)

	// exit notify ( thread is monitor process status )
	var cpe chan bool

	// run loop select chan in main thread
	var dtCheck int64
	var dtHeartbeat int64
	var dtStatusRequest int64
	bRun := true
	for bRun {
		select {

		// os.Signal
		case <-cbr:
			logger.Println(_title.Info + "执行清理..")
			logger.Println(_title.Info + "结束退出..")
			bRun = false

		// ticker
		case <-ticker.C:
			dtNow := utilities.TimeGetCurrentMsepoch()
			if dtNow-dtCheck > 3000 {
				monS.CheckChannel()
				dtCheck = dtNow
			}
			if shouldRequestDaemonStatus(dtNow, _programStatusRespTime, dtStatusRequest) {
				// programStatus request
				daemonStatusesReq()
				dtStatusRequest = dtNow
				logger.Println(_title.Info + "monS programStatusReq2Daemon()")
			}
			// Send ProgramHeartbeat
			if dtNow-dtHeartbeat > 10000 {
				updateDaemonStatus(&program.Status{Id: program.DefaultProgramIdMonitor,
					Pid: os.Getpid(), IsRun: true, LastRunTime: utilities.TimeGetCurrentMsepoch()})
				dtHeartbeat = dtNow
			}

		// timer to after run
		case <-timer.C:
			//runDaemon()

		// monS 监视服务管道
		case rData := <-monS.C:
			// deal monS received data
			monS.DealUdpSData(&rData)
			for !thisQueue.IsEmpty() {
				if codeMessage, ok := thisQueue.Pop(); ok {
					// deal Code Program DaemonEvent | Code Program Heartbeat
					if codeMessage.Code == monitorc.CCodeProgramDaemonEventPost || codeMessage.Code == monitorc.CCodeProgramStatusResp {
						// start calcProcessStatus
						cpe = reCalcProcessStatus(cpe)

						// Employ ProgramDiff
					} else if codeMessage.Code == monitorc.CCodeProgramDiffEmployReq {
						// loadProgramRun try 3 times
						reInitProgramStatuses()

						// Employ Result of Checking Dbs
					} else if codeMessage.Code == monitorc.CCodeCheckDbsEmployReq {
						var dbsState monitorc.DbsState
						//var info Register
						buf := bytes.NewBuffer(codeMessage.Data)
						err = binary.Read(buf, monitorc.Endian, &dbsState)
						if err != nil {
							fmt.Println("checkDbsEmployReq Fail, binary.Read failed:", err)
						}
						if dbsState.MqttState {
							updateHeartbeatTime(program.DefaultProgramIdMosquitto)
						}
						if dbsState.RedisState {
							updateHeartbeatTime(program.DefaultProgramIdRedis)
						}
					}
				}
			}
		}
	}

	program.ExitProgramRun(*_workDir, program.DefaultProgramNameMonitor)
	logger.Printf(_title.Info+"%s End\n", myProgram.Path)
}

func reInitProgramStatuses() {
	err := initProgramStatuses()
	if err != nil {
		err = initProgramStatuses()
		if err != nil {
			err = initProgramStatuses()
			if err != nil {
				logger.Println(_title.Error+"initProgramStatuses Fail, err: ", err)
				os.Exit(-10)
			}
		}
	}
}

func reCalcProcessStatus(cpe chan bool) chan bool {
	// exit calcProcessStatus
	if cpe != nil {
		cpe <- true
	}
	var pidList []int32
	var ProgramIds []int64
	func() {
		_programStatusesMu.Lock()
		defer _programStatusesMu.Unlock()
		// calc process status
		for _, s := range _programStatuses {
			if s.DaemonStatus.IsRun {
				pidList = append(pidList, int32(s.DaemonStatus.Pid))
				ProgramIds = append(ProgramIds, s.DaemonStatus.Id)
			}
		}
	}()
	// start calcProcessStatus
	if len(pidList) > 0 {
		// Calc Program Running Status ( cpu , mem , disk )
		cpe = make(chan bool)
		go calcProcessStatus(pidList, cpe)
		pidListString := fmt.Sprint(pidList)
		ProgramIdsString := fmt.Sprint(ProgramIds)
		logger.Printf(_title.Info+"Run Calc ProcessStatus, pidList[ %s ], ProgramIds[ %s ]", pidListString, ProgramIdsString)
	}
	return cpe
}
