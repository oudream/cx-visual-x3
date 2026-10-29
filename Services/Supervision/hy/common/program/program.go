package program

import (
	"database/sql"
	"errors"
	"fmt"
	"github.com/go-gorp/gorp"
	"gogo/common/utilities"
	"path/filepath"
)

const (
	ENABLE  = 1
	DISABLE = 0
)

// Program
//
//	type ProgramItem struct {
//		Id int
//		// exec.Cmd
//		Path       string
//		Dir       string
//		ArgsString string
//		EnvsString string
//		// Daemon
//		StdOutPath    string // 日志文件, 记录守护进程和子进程的标准输出和错误输出. 若为空则不记录
//		MaxCount      int    // 循环重启最大次数, 若为0则无限重启
//		MaxError      int    // 连续启动失败或异常退出的最大次数, 超过此数, 守护进程退出, 不再重启子进程
//		MinExitTime   int64  // 子进程正常退出的最小时间(秒). 小于此时间则认为是异常退出
//		FirstWaitTime int64  // 首次启动时需要等待时间(秒)
//		StartWaitTime int64  // 异常退出后，启动时需要等待时间(秒)
//	}
type Program struct {
	ID            int64  `db:"id" gorm:"column:id;primarykey;autoIncrement:false;" json:"ID,string" form:"ID"`           // - bigint(20) NOT NULL COMMENT '主键',
	Path          string `db:"path" gorm:"column:path;" json:"path" form:"path"`                                         // - varchar(255) NOT NULL DEFAULT '' COMMENT '进程路径',
	Dir           string `db:"dir" gorm:"column:dir;" json:"dir" form:"dir"`                                             // - varchar(255) DEFAULT '' COMMENT '运行时目录',
	ArgsString    string `db:"args_string" gorm:"column:args_string;" json:"argsString" form:"argsString"`               // - varchar(255) DEFAULT '' COMMENT '运行参数',
	EnvsString    string `db:"envs_string" gorm:"column:envs_string;" json:"envsString" form:"envsString"`               // - varchar(255) DEFAULT '' COMMENT '运行时环境参数',
	StdOutPath    string `db:"std_out_path" gorm:"column:std_out_path;" json:"stdOutPath" form:"stdOutPath"`             // - varchar(255) DEFAULT '' COMMENT '日志文件, 记录守护进程和子进程的标准输出和错误输出. 若为空则不记录',
	MaxCount      int    `db:"max_count" gorm:"column:max_count;" json:"maxCount" form:"maxCount"`                       // - int(11) DEFAULT '0' COMMENT '循环重启最大次数, 若为0则无限重启',
	MaxError      int    `db:"max_error" gorm:"column:max_error;" json:"maxError" form:"maxError"`                       // - int(11) DEFAULT '9' COMMENT '连续启动失败或异常退出的最大次数, 超过此数, 守护进程退出, 不再重启子进程',
	MinExitTime   int    `db:"min_exit_time" gorm:"column:min_exit_time;" json:"minExitTime" form:"minExitTime"`         // - int(11) DEFAULT '10' COMMENT '子进程正常退出的最小时间(秒). 小于此时间则认为是异常退出',
	FirstWaitTime int    `db:"first_wait_time" gorm:"column:first_wait_time;" json:"firstWaitTime" form:"firstWaitTime"` // - int(11) DEFAULT '0' COMMENT '首次启动时需要等待时间(秒)',
	StartWaitTime int    `db:"start_wait_time" gorm:"column:start_wait_time;" json:"startWaitTime" form:"startWaitTime"` // - int(11) DEFAULT '100' COMMENT '异常退出后，启动时需要等待时间(秒)',
	Role          int    `db:"role" gorm:"column:role;" json:"role" form:"role"`                                         // - int(11) NOT NULL DEFAULT '33' COMMENT '1: sys; 2: communicator',
	Enabled       int    `db:"enabled" gorm:"column:enabled;" json:"enabled" form:"enabled"`                             // - int(11) NOT NULL DEFAULT '1',
	Name          string `db:"name" gorm:"column:name;" json:"name" form:"name"`                                         // - varchar(255) NOT NULL DEFAULT '' COMMENT '进程路径',
}

// TableName Program 表名
func (a Program) TableName() string {
	return TableNameProgramName
}

type Heartbeat struct {
	ProgramId int64 `json:"ProgramId"`
	PId       int   `json:"PId"`
	Healthy   int   `json:"Healthy"`
	Res1      int   `json:"Res1"`
	Res2      int   `json:"Res2"`
	Res3      int   `json:"Res3"`
	Res4      int   `json:"Res4"`
}

type Status struct {
	Id             int64 `json:"Id"`
	Pid            int   `json:"Pid"`
	IsRun          bool  `json:"Run"`
	LastRunTime    int64 `json:"Time"`
	StartCount     int   `json:"Count"`
	StartFailCount int   `json:"Fail"`
	ErrorCount     int   `json:"Error"`
}

const (
	EventStart int = iota
	EventLogin
	EventHeartbeat
	EventExit
	EventMaxError
	EventMaxCount
)

type Event struct {
	Event   int    `json:"Event"`
	Status  Status `json:"Status"`
	Message string `json:"Message"`
}

func doGetProgramList(wd, tableName string, enabled int) ([]Program, error) {
	dbMap, err1 := OpenDbMap(wd)
	if err1 != nil {
		return nil, errors.New(fmt.Sprintf("OpenDbMap Fail. err: %s", err1))
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Program{}, tableName).SetKeys(true, "id")
	//
	var objs []Program
	var sqlSelect = fmt.Sprintf("select * from `%s` where `Enabled`!=%d", tableName, enabled)
	_, err2 := dbMap.Select(&objs, sqlSelect)
	if err2 != nil {
		return nil, errors.New(fmt.Sprintf("getProgramList [TableNameProgramName: %s] FAIL. err: %s", tableName, err2))
	} else {
		return objs, nil
	}
}

func GetProgramRunList(wd string) ([]Program, error) {
	return doGetProgramList(wd, TableNameProgramRun, 0)
}

func GetProgramById(wd string, id int64) (*Program, error) {
	dbMap, err1 := OpenDbMap(wd)
	if err1 != nil {
		return nil, errors.New(fmt.Sprintf("OpenDbMap Fail. err: %s", err1))
	}
	defer CloseDbMap(dbMap)
	return GetProgramByDbMap(dbMap, id)
}

func GetProgramByDbMap(dbMap gorp.DbMap, id int64) (*Program, error) {
	dbMap.AddTableWithName(Program{}, TableNameProgramRun).SetKeys(true, "id")
	//
	program := Program{}
	var sqlSelect = fmt.Sprintf("select * from `%s` where `Id`=?", TableNameProgramRun)
	err2 := dbMap.SelectOne(&program, sqlSelect, id)
	if err2 != nil {
		return nil, errors.New(fmt.Sprintf("GetProgramById [TableNameProgramName: %s] [Id: %s] FAIL. err: %s", TableNameProgramRun, id, err2))
	} else {
		return &program, nil
	}
}

func FindProgramIndex(programs []Program, id int64) int {
	for i := 0; i < len(programs); i++ {
		if programs[i].ID == id {
			return i
		}
	}
	return -1
}

func IsSameProgram(p1, p2 *Program) bool {
	return p1.Path == p2.Path &&
		p1.Dir == p2.Dir &&
		p1.ArgsString == p2.ArgsString &&
		p1.EnvsString == p2.EnvsString &&
		p1.StdOutPath == p2.StdOutPath &&
		p1.MaxCount == p2.MaxCount &&
		p1.MaxError == p2.MaxError &&
		p1.MinExitTime == p2.MinExitTime &&
		p1.FirstWaitTime == p2.FirstWaitTime &&
		p1.StartWaitTime == p2.StartWaitTime &&
		p1.Enabled == p2.Enabled &&
		p1.Name == p2.Name
}

func CheckValid(p *Program, wd string) error {
	if p == nil {
		return fmt.Errorf("program is nil")
	}
	if len(p.Path) <= 0 {
		return fmt.Errorf("path[ %s ] is invalid", p.Path)
	}
	dir := p.Dir
	if !filepath.IsAbs(dir) {
		dir = filepath.Join(wd, dir)
	}
	isValid, _ := utilities.FileSystemIsValidFile(dir, p.Path)
	if !isValid {
		return fmt.Errorf("dir[ %s ] is invalid", p.Dir)
	}
	return nil
}

func GetAllProgram(db *sql.DB) ([]Program, error) {
	return doGetAllProgram(db, TableNameProgramName)
}

func GetAllRunProgram(db *sql.DB) ([]Program, error) {
	return doGetAllProgram(db, TableNameProgramRun)
}

func doGetAllProgram(db *sql.DB, tableName string) ([]Program, error) {
	dbMap := gorp.DbMap{Db: db, Dialect: gorp.SqliteDialect{}}
	dbMap.AddTableWithName(Program{}, tableName).SetKeys(true, "id")
	//
	var objs []Program
	var sqlSelect = fmt.Sprintf("select * from `%s`;", tableName)
	_, err := dbMap.Select(&objs, sqlSelect)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("getAllProgram [TableNameProgramName: %s] FAIL. err: %s", tableName, err))
	} else {
		return objs, nil
	}
}
