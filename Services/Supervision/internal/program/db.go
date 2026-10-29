package program

import (
	"database/sql"
	"errors"
	"fmt"
	"github.com/go-gorp/gorp"
	_ "github.com/mattn/go-sqlite3"
	"os"
)

func OpenDb(wd string) (*sql.DB, error) {
	dbPath := GetDBFilePath(wd)
	db, err := sql.Open("sqlite3", dbPath)
	if err != nil {
		var eMsg string
		stat, err1 := os.Stat(dbPath)
		if err1 != nil {
			eMsg = fmt.Sprintf("OpenDb [ %s ] Fail, open-err: %s, stat-err: %s", dbPath, err, err1)
		} else {
			eMsg = fmt.Sprintf("OpenDb [ %s ] Fail, open-err: %s, stat: %+v", dbPath, err, stat)
		}
		return nil, errors.New(eMsg)
	}
	return db, nil
}

func CloseDb(db *sql.DB) {
	_ = db.Close()
}

func OpenDbMap(wd string) (gorp.DbMap, error) {
	db, err := OpenDb(wd)
	if err != nil {
		return gorp.DbMap{}, err
	}
	return gorp.DbMap{Db: db, Dialect: gorp.SqliteDialect{}}, nil
}

func CloseDbMap(dbMap gorp.DbMap) {
	CloseDb(dbMap.Db)
}

func ExecSql(db *sql.DB, sqlText string) (int, error) {
	res, err2 := db.Exec(sqlText)
	if err2 != nil {
		return -2, errors.New(fmt.Sprintf("Exec Monitor Db Fail, [ %s ] err: %s", sqlText, err2))
	}
	ra, err3 := res.RowsAffected()
	if err3 != nil {
		return -2, errors.New(fmt.Sprintf("Exec Monitor Db Fail, [ %s ] err: %s", sqlText, err3))
	}
	return int(ra), nil
}

func QuerySqlInt64(db *sql.DB, sqlText string) (int64, error) {
	rows, err := db.Query(sqlText)
	if err != nil {
		return -1, errors.New(fmt.Sprintf("Query Monitor Db Fail, [ %s ] err: %s", sqlText, err))
	}
	defer func(rows *sql.Rows) {
		_ = rows.Close()
	}(rows)
	for rows.Next() {
		var v int64
		if err := rows.Scan(&v); err != nil {
			return -2, errors.New(fmt.Sprintf("Query Monitor Db Fail, Scan Fail [ %s ] err: %s", sqlText, err))
		} else {
			return v, nil
		}
	}
	return -3, errors.New(fmt.Sprintf("Query Monitor Db Fail, Rows Count==0 [ %s ]", sqlText))
}
