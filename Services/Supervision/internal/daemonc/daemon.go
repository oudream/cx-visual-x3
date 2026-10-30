package daemonc

import (
	"errors"
	"fmt"
	"gogo/common/utilities"
	"gogo/internal/program"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"time"
)

const EnvName = "APP_ID"

var (
	DaemonRun = true
)

var (
	_titleDaemon = utilities.NewLoggerTitles("DAEMON")
)

type FindProgramCallback func(id int64) (program.Program, error)

func RunProgram(a0 program.Program, programEvent chan program.Event, fnFindProgram FindProgramCallback, logger *log.Logger, wd string) {
	var status program.Status
	a1 := a0

	//守护进程启动一个子进程, 并循环监视
	var t int64
	status.Id = a1.ID
	status.IsRun = false
	status.StartCount = 0
	status.ErrorCount = 0
	for DaemonRun {
		a2, err := fnFindProgram(a1.ID)
		if err != nil {
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) Can Not Be Find. 退出: %v\n", a1.Path, a1.ID, err)
			break
		}
		if a2.Enabled != program.ENABLE {
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) disable. 退出\n", a2.Path, a2.ID)
			break
		}

		a1 = a2

		//daemon 信息描述
		if status.ErrorCount > a1.MaxError {
			programEvent <- program.Event{Event: program.EventMaxError, Status: status}
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) 失败次数太多,退出\n", a1.Path, a1.ID)
			break
		}
		if a1.MaxCount > 0 && status.StartCount > a1.MaxCount {
			programEvent <- program.Event{Event: program.EventMaxCount, Status: status}
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) 重启次数太多退出\n", a1.Path, a1.ID)
			break
		}

		if status.StartCount > 0 && a1.StartWaitTime > 0 {
			time.Sleep(time.Millisecond * time.Duration(a1.StartWaitTime))
			logger.Printf(_titleDaemon.Info+"进程[%s](%d) 异常退出后，启动时需要等待时间( %d 毫秒)\n", a1.Path, a1.ID, a1.StartWaitTime)
		}

		// 启动时间戳
		t = time.Now().Unix()

		// envs
		envs, err := program.ParseCommandLine(a1.EnvsString)
		if err != nil {
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) ParseCommandLine [ envs ] %s\n", a1.Path, a1.ID, err)
			envs = []string{}
		}
		envs = append(os.Environ(), envs...)
		envs = append(envs, fmt.Sprintf("%s=%d", EnvName, a1.ID))

		// args
		args, err := program.ParseCommandLine(a1.ArgsString)
		if err != nil {
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) ParseCommandLine [ args ] %s %v\n", a1.Path, a1.ID, err)
			args = []string{}
		}
		if a1.ID > program.DefaultProgramIdUser {
			args = append([]string{a1.Path, "-i", fmt.Sprintf("%d", a1.ID)}, args...)
		} else {
			args = append([]string{a1.Path}, args...)
		}

		// logFile
		// logFile 若不为空,子程序的标准输出和错误输出将记入此文件
		logFile := a1.StdOutPath
		if len(logFile) > 0 && !filepath.IsAbs(logFile) {
			workDir, err := os.Getwd()
			if err != nil {
				logger.Printf(_titleDaemon.Error+"进程[%s](%d) os.Getwd() error: %s\n", a1.Path, a1.ID, err)
			} else {
				logFile = filepath.Join(workDir, logFile)
			}
		}

		// dir working directory of the command
		dir := a1.Dir
		if !filepath.IsAbs(dir) {
			dir = filepath.Join(wd, dir)
		}

		// 启动子进程
		status.StartCount++
		cmd, err := startProc(args, envs, dir, logFile)
		if err != nil {
			status.IsRun = false
			status.Pid = 0
			status.StartFailCount++
			status.ErrorCount++
			programEvent <- program.Event{Event: program.EventStart, Status: status, Message: err.Error()}
			logger.Printf(_titleDaemon.Error+"进程[%s](%d) 启动子进程失败: %s\n", a1.Path, a1.ID, err)
			continue
		}

		//
		if cmd == nil {
			status.IsRun = false
			status.Pid = 0
			status.StartFailCount++
			status.ErrorCount++
			programEvent <- program.Event{Event: program.EventStart, Status: status, Message: "cmd is nil"}
			continue
		}

		// 执行成功
		status.Pid = cmd.Process.Pid
		status.IsRun = true
		status.LastRunTime = utilities.TimeGetCurrentMsepoch()
		programEvent <- program.Event{Event: program.EventStart, Status: status}

		// 父进程的守护协程阻塞: 等待子进程退出
		err = cmd.Wait()
		// 子进程运行秒数
		dat := time.Now().Unix() - t
		// 异常退出
		if dat < int64(a1.MinExitTime) {
			status.ErrorCount++
		} else {
			// 正常退出
			status.StartFailCount = 0
			status.ErrorCount = 0
		}
		status.IsRun = false
		status.Pid = 0
		//被守护的进程停止后，通过管道向守护进程发出消息
		programEvent <- program.Event{Event: program.EventExit, Status: status, Message: fmt.Sprintf("%v", err)}
		logger.Printf(_titleDaemon.Error+"进程[%s](%d) pid(%d)退出, 共运行了%d秒: %s\n", a1.Path, a1.ID, cmd.ProcessState.Pid(), dat, err)
	}
}

func ExecProgram(a program.Program, wd string) (*exec.Cmd, error) {
	// envs
	envs, err1 := program.ParseCommandLine(a.EnvsString)
	if err1 != nil {
		return nil, errors.New(fmt.Sprintf("ParseCommandLine [ envs ] %s %v\n", a.Path, err1))
	}
	envs = append(os.Environ(), envs...)
	envs = append(envs, fmt.Sprintf("APP_ID=%d", a.ID))

	// args
	args, err2 := program.ParseCommandLine(a.ArgsString)
	if err2 != nil {
		return nil, errors.New(fmt.Sprintf("ParseCommandLine [ args ] %s %v\n", a.Path, err2))
	}
	args = append([]string{a.Path}, args...)

	// logFile 若不为空,子程序的标准输出和错误输出将记入此文件
	logFile := a.StdOutPath
	if len(logFile) > 0 && !filepath.IsAbs(logFile) {
		wd2, err5 := os.Getwd()
		if err5 == nil {
			logFile = filepath.Join(wd2, logFile)
		} else {
			return nil, errors.New(fmt.Sprintf("os.Getwd() error: %v\n", err5))
		}
	}

	// dir working directory of the command
	dir := a.Dir
	if !filepath.IsAbs(dir) {
		dir = filepath.Join(wd, dir)
	}

	// 启动子进程
	cmd, err3 := startProc(args, envs, dir, logFile)
	if err3 != nil {
		return nil, errors.New(fmt.Sprintf("启动子进程失败: %s\n", err3))
	}
	return cmd, nil
}

func startProc(args, env []string, dir string, logFile string) (*exec.Cmd, error) {
	cmd := &exec.Cmd{
		Path:        args[0],
		Dir:         dir,
		Args:        args,
		Env:         env,
		SysProcAttr: NewSysProcAttr(),
	}

	if logFile != "" {
		stdout, err := os.OpenFile(logFile, os.O_WRONLY|os.O_APPEND|os.O_CREATE, 0666)
		if err != nil {
			return nil, errors.New(fmt.Sprintf("redirect stdout fail, err= %s", err))
		}
		// os/exec duplicates the descriptor for the child at Start.
		// Close the parent's copy on both success and error, otherwise
		// long-running Daemon restarts accumulate open log descriptors.
		cmd.Stderr = stdout
		cmd.Stdout = stdout
		err = cmd.Start()
		_ = stdout.Close()
		if err != nil {
			return nil, err
		}
		return cmd, nil
	}

	if err := cmd.Start(); err != nil {
		return nil, err
	}
	return cmd, nil
}
