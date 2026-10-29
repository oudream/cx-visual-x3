package program

import (
	"errors"
	"fmt"
	"github.com/shirou/gopsutil/v3/process"
	"gogo/common/utilities"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"syscall"
)

func ParseCommandLine(command string) ([]string, error) {
	var args []string
	state := "start"
	current := ""
	quote := "\""
	escapeNext := true
	for i := 0; i < len(command); i++ {
		c := command[i]

		if state == "quotes" {
			if string(c) != quote {
				current += string(c)
			} else {
				args = append(args, current)
				current = ""
				state = "start"
			}
			continue
		}

		if escapeNext {
			current += string(c)
			escapeNext = false
			continue
		}

		if c == '\\' {
			escapeNext = true
			continue
		}

		if c == '"' || c == '\'' {
			state = "quotes"
			quote = string(c)
			continue
		}

		if state == "arg" {
			if c == ' ' || c == '\t' {
				args = append(args, current)
				current = ""
				state = "start"
			} else {
				current += string(c)
			}
			continue
		}

		if c != ' ' && c != '\t' {
			state = "arg"
			current += string(c)
		}
	}

	if state == "quotes" {
		return []string{}, errors.New(fmt.Sprintf("Unclosed quote in command line: %s", command))
	}

	if current != "" {
		args = append(args, current)
	}

	return args, nil
}

func IsProgramHadRun(wd, name string) bool {
	pidDir := filepath.Join(wd, "pid")
	pidFile := filepath.Join(pidDir, name+".pid")
	//
	s, _ := utilities.FileSystemRead(pidFile)
	if len(s) > 0 {
		if pid64, err2 := strconv.ParseInt(string(s), 0, 32); err2 == nil && pid64 > 0 {
			pid := int(pid64)
			self := os.Getpid()
			if pid != self && IsExistProcess(pid) {
				//return int(pid)
				if proc, err := process.NewProcess(int32(pid)); err == nil {
					path, err := proc.Name()
					if err == nil {
						if strings.Contains(path, name) {
							return true
						}
					}
				}
			}
		}
	}
	return false
}

func LoadProgramRunPid(wd, name string) int {
	pidDir := filepath.Join(wd, "pid")
	pidFile := filepath.Join(pidDir, name+".pid")
	//
	s, _ := utilities.FileSystemRead(pidFile)
	if len(s) > 0 {
		if pid, err2 := strconv.ParseInt(string(s), 0, 32); err2 == nil && pid > 0 {
			if IsExistProcess(int(pid)) {
				//return int(pid)
				if proc, err := process.NewProcess(int32(pid)); err == nil {
					path, err := proc.Name()
					if err == nil {
						if strings.Contains(path, name) {
							return int(pid)
						}
					}
				}
			}
		}
	}
	return 0
}

func SaveSelfPidFile(wd, name string) {
	_ = SaveProgramPidFile(wd, name, os.Getpid())
}

func SaveProgramPidFile(wd, name string, pid int) error {
	pidDir := filepath.Join(wd, "pid")
	err := utilities.FileSystemConfirmDir(pidDir)
	if err != nil {
		return err
	}
	pidFile := filepath.Join(pidDir, name+".pid")
	//
	return utilities.FileSystemWrite(pidFile, strconv.Itoa(pid))
}

func DeleteProgramPidFile(wd, name string) {
	pidDir := filepath.Join(wd, "pid")
	if utilities.FileSystemIsExist(pidDir) {
		pidFile := filepath.Join(pidDir, name+".pid")
		_ = os.Remove(pidFile)
	}
}

func ExitProgramRun(wd, name string) {
	pidDir := filepath.Join(wd, "pid")
	pidFile := filepath.Join(pidDir, name+".pid")
	_ = os.Remove(pidFile)
}

func IsExistProcess(pid int) bool {
	if p, err := os.FindProcess(pid); err == nil {
		err := p.Signal(syscall.Signal(0))
		if err != nil {
			return false
		}
		return true
	}
	return false
}

func KillProcess(pid int) error {
	if p, err1 := os.FindProcess(pid); err1 == nil {
		err2 := p.Kill()
		if err2 != nil {
			return err2
		}
		return nil
	} else {
		return err1
	}
}
