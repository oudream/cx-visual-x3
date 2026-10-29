package program

import (
	"encoding/json"
	"gogo/common/utilities"
	"os"
	"path/filepath"
	"strings"
)

const (
	IOReportNameMqtt     = "mqtt"
	IOReportNameRedis    = "redis"
	IOReportNameInfluxdb = "influxdb"
)

const (
	IOStatusSuccess = 1
	IOStatusFail    = 0
)

type IOReport struct {
	Name      string `json:"name"`
	Status    int    `json:"status"`
	Msg       string `json:"msg"`
	Time      int64  `json:"time"`
	ProgramId int64  `json:"programId"`
	Pid       int    `json:"pid"`
}

func LoadProgramIOReport(wd, name string) (error, *IOReport) {
	reportFilePath := filepath.Join(GetDBPath(wd), name+".report")
	s, err := utilities.FileSystemRead(reportFilePath)
	if err != nil {
		return err, nil
	}
	if len(s) > 0 {
		var report IOReport
		err = json.Unmarshal(s, &report)
		return err, &report
	}
	return err, nil
}

func SaveProgramIOReport(wd, name string, report IOReport) error {
	reportFilePath := filepath.Join(GetDBPath(wd), name+".report")
	s, err := json.Marshal(report)
	if err != nil {
		return err
	}
	return os.WriteFile(reportFilePath, s, 0666)
}

func IOStatusToString(status int) string {
	if status == IOStatusSuccess {
		return "success"
	} else {
		return "fail"
	}
}

func IOStatusFromString(status string) int {
	if strings.ToLower(status) == "success" {
		return 1
	} else {
		return 0
	}
}
