package program

import (
	"fmt"
	"gogo/common/utilities"
)

func UpdateDb(wd string) (int64, error) {
	db, err := OpenDb(wd)
	if err != nil {
		return 0, err
	}
	defer CloseDb(db)
	dbVersion, err := GetLastDBVersionByDB(db)
	if err != nil {
		return 0, err
	}
	var rowsAffected int64
	version := dbVersion.Version
	if version < CurrentDBVersion {
		sqlListMap, err := GetUpdateSqlLislMap(wd)
		if err != nil {
			return 0, err
		}
		//
		for version <= CurrentDBVersion {
			version++
			if sqlList, ok := sqlListMap[version]; ok && len(sqlList) > 0 {
				tx, err := db.Begin()
				if err != nil {
					return 0, err
				}
				for i := 0; i < len(sqlList); i++ {
					sqlText := sqlList[i]
					rs, err := tx.Exec(sqlText)
					if err != nil {
						_ = tx.Rollback()
						return rowsAffected, nil
					}
					rowAffected, err := rs.RowsAffected()
					if err != nil {
						_ = tx.Rollback()
						return rowsAffected, nil
					}
					rowsAffected += rowAffected
				}
				err = AddDBVersionByTX(tx, CurrentDBVersion, sqlList)
				if err != nil {
					return 0, err
				}
				if err = tx.Commit(); err != nil {
					_ = tx.Rollback()
					return 0, err
				}
			}
		}
	}
	return rowsAffected, nil
}

// InitDb
// 1 db file create
// 2 create tables
// 3 insert data
func InitDb(wd string) (int, error) {
	dbPath := GetDBFilePath(wd)
	if !utilities.FileSystemIsExist(dbPath) {
		err := utilities.FileSystemCreateFile(dbPath)
		if err != nil {
			return 0, err
		} else {
			return initNewDB(wd)
		}
	} else {
		rowsAffected, err := UpdateDb(wd)
		if err != nil {
			return 0, err
		}
		return int(rowsAffected), nil
	}
}

func initNewDB(wd string) (int, error) {
	db, err := OpenDb(wd)
	if err != nil {
		return 0, err
	}
	defer CloseDb(db)
	//
	var sqlList []string
	// create tables
	for i := 0; i < len(SqlCreateList); i++ {
		sqlCreate := SqlCreateList[i]
		if _, err = ExecSql(db, sqlCreate); err != nil {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], err: %s", sqlCreate, err)
		}
	}
	sqlList = append(sqlList, SqlCreateList...)
	iRowsAffected := 0
	// insert program
	for i := 0; i < len(Infos); i++ {
		info := Infos[i]
		sInsert := `INSERT INTO "%s" VALUES (%d, '%s', '%s', '%s', '', '', 0, 9, 10, 0, 100, 1, %d, '%s');`
		sSql := fmt.Sprintf(sInsert, TableNameProgramName, info.ProgramId, info.Path, info.DefaultDir, info.DefaultArgsString, info.DefaultEnable, info.Name)
		if res, err := ExecSql(db, sSql); err != nil {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], err: %s", sSql, err)
		} else if res < 0 {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], result < 0 ", sSql)
		} else {
			iRowsAffected = iRowsAffected + res
			sqlList = append(sqlList, sSql)
		}
		sSql = fmt.Sprintf(sInsert, TableNameProgramRun, info.ProgramId, info.Path, info.DefaultDir, info.DefaultArgsString, info.DefaultEnable, info.Name)
		if res, err := ExecSql(db, sSql); err != nil {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], err: %s", sSql, err)
		} else if res < 0 {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], result < 0 ", sSql)
		} else {
			iRowsAffected = iRowsAffected + res
			sqlList = append(sqlList, sSql)
		}
	}
	// insert setting
	for i := 0; i < len(SqlDefaultSettings); i++ {
		ds := SqlDefaultSettings[i]
		sSql := fmt.Sprintf(`INSERT INTO "%s" VALUES ('%s', '%s');`, TableNameSettings, ds.Key, ds.Value)
		if res, err := ExecSql(db, sSql); err != nil {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], err: %s", sSql, err)
		} else if res < 0 {
			return 0, fmt.Errorf("InitDb Fail, sql[ %s ], result < 0 ", sSql)
		} else {
			iRowsAffected = iRowsAffected + res
			sqlList = append(sqlList, sSql)
		}
	}
	err = AddDBVersionByDB(db, CurrentDBVersion, sqlList)
	if err != nil {
		return 0, err
	}
	return iRowsAffected, nil
}
