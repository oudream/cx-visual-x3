package program

import (
	"fmt"
	"gogo/common/utilities"
	"path/filepath"
)

const BackupDirName = "backup"
const LogDirName = "log"
const ConfDirName = "conf"

func GetBackupPath(wd string) string {
	return filepath.Join(wd, BackupDirName)
}

func GetDBPath(wd string) string {
	return filepath.Join(wd, "db")
}

func GetLogPath(wd string) string {
	return filepath.Join(wd, LogDirName)
}

func GetConfPath(wd string) string {
	return filepath.Join(wd, ConfDirName)
}

func GetDBFilePath(wd string) string {
	return filepath.Join(GetDBPath(wd), MonitorDatabaseName)
}

func (a Program) GetFileName() string {
	return utilities.FileSystemGetFileNameOnly(a.Path)
}

func (a Program) GetLogPrefix() string {
	return fmt.Sprintf("%s.%d", a.GetFileName(), a.ID)
}

func (a Program) GetLogPrefixPath(wd string) string {
	return filepath.Join(wd, LogDirName, a.GetLogPrefix())
}

func (a Program) ToString() string {
	return fmt.Sprintf("[%s](%s)(%d) Args[%s] Envs[%s] MaxCount[%d] MaxError[%d] MinExitTime[%d]",
		a.Name, a.Path, a.ID, a.ArgsString, a.EnvsString, a.MaxCount, a.MaxError, a.MinExitTime)
}
