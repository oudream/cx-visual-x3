package main

import (
	"errors"
	"flag"
	"fmt"
	"gogo/hy/common/program"
)

// -d /userdata/hy -i programs1
var (
	_workDir     = flag.String("d", "", "work path, such [ -d /userdata/xx ]")
	_monitorHost = flag.String("h", "127.0.0.1", "monitor server ip")
	_monitorPort = flag.Int("p", program.DefaultPortMonMonitor, "monitor server port")
)

func initConfig() error {
	var err error
	_programs, err = program.GetProgramRunList(*_workDir)
	if err != nil {
		return errors.New(fmt.Sprintf("Fatal GetAllProgramList, err: %s", err))
	}
	return nil
}
