package main

import (
	"errors"
	"fmt"
	"gogo/hy/common/program"
	"sync"
)

var (
	_programs      []program.Program
	_programsMu    sync.Mutex
	_runProgramIds map[int64]int
)

func init() {
	_runProgramIds = make(map[int64]int)
}

func getPrograms() []program.Program {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	var ps []program.Program
	for i := 0; i < len(_programs); i++ {
		ps = append(ps, _programs[i])
	}
	return ps
}

func findProgramById(ProgramId int64) (program.Program, error) {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	for j := 0; j < len(_programs); j++ {
		if _programs[j].ID == ProgramId {
			return _programs[j], nil
		}
	}
	return program.Program{}, errors.New("can Not Be Found")
}

func existProgram(ProgramId int64) bool {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	for j := 0; j < len(_programs); j++ {
		if _programs[j].ID == ProgramId {
			return true
		}
	}
	return false
}

func addProgram(a program.Program) {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	for j := 0; j < len(_programs); j++ {
		if _programs[j].ID == a.ID {
			return
		}
	}
	_programs = append(_programs, a)
}

func updateProgram(a program.Program) (oldEnabled bool, newEnabled bool, err error) {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	newEnabled = a.Enabled == program.ENABLE
	found := false
	for j := 0; j < len(_programs); j++ {
		if _programs[j].ID == a.ID {
			oldEnabled = _programs[j].Enabled == program.ENABLE
			_programs[j] = a
			found = true
			break
		}
	}
	if !found {
		oldEnabled = false
		if newEnabled {
			_programs = append(_programs, a)
		} else {
			err = fmt.Errorf("can not found program(%d)", a.ID)
		}
	}
	return
}

func deleteProgram(ProgramId int64) {
	_programsMu.Lock()
	defer _programsMu.Unlock()
	index := -1
	for j := 0; j < len(_programs); j++ {
		if _programs[j].ID == ProgramId {
			index = j
			break
		}
	}
	if index > -1 {
		_programs = append(_programs[:index], _programs[index+1:]...)
	}
}

func checkSysSetting() error {
	err, mqttReport := program.LoadProgramIOReport(*_workDir, program.IOReportNameMqtt)
	if err != nil {
		return err
	}
	err, redisReport := program.LoadProgramIOReport(*_workDir, program.IOReportNameRedis)
	if err != nil {
		return err
	}
	err, influxdbReport := program.LoadProgramIOReport(*_workDir, program.IOReportNameInfluxdb)
	if err != nil {
		return err
	}
	if mqttReport.Status == program.IOStatusSuccess &&
		redisReport.Status == program.IOStatusSuccess &&
		influxdbReport.Status == program.IOStatusSuccess {
		return nil
	}
	return fmt.Errorf("load mqtt.status[%d] mqtt.status[%d] mqtt.status[%d] mqtt.status[%d]",
		mqttReport.Status, redisReport.Status, influxdbReport.Status)
}
