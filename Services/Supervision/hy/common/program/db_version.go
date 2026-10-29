package program

import (
	"database/sql"
	"fmt"
	"github.com/spf13/viper"
	"strings"
	"time"
)

type DBVersion struct {
	Version int64  `json:"version" db:"version"`
	Date    int64  `json:"Date" db:"date"`
	Sql     string `json:"Sql" db:"sql"`
}

func AddDBVersion(wd string, version int64, sqlList []string) error {
	db, err := OpenDb(wd)
	if err != nil {
		return err
	}
	defer CloseDb(db)
	return AddDBVersionByDB(db, version, sqlList)
}

func AddDBVersionByDB(db *sql.DB, version int64, sqlList []string) error {
	sqlText := ToDBVersionSql(sqlList)
	_, err := db.Exec(`INSERT INTO "comm_db_version" ("version", "date", "sql") VALUES (?, ?, ?);`, version, time.Now().UnixMilli(), sqlText)
	return err
}

func ToDBVersionSql(sqlList []string) string {
	for i := 0; i < len(sqlList); i++ {
		sqlText := sqlList[i]
		sqlText = strings.Replace(sqlText, "\n", "", -1)
		sqlText = strings.Replace(sqlText, "\r", "", -1)
		sqlList[i] = sqlText
	}
	sqlText := strings.Join(sqlList, "\n")
	return sqlText
}

func AddDBVersionByTX(db *sql.Tx, version int64, sqlList []string) error {
	sqlText := ToDBVersionSql(sqlList)
	_, err := db.Exec(`INSERT INTO "comm_db_version" ("version", "date", "sql") VALUES (?, ?, ?);`, version, time.Now().UnixMilli(), sqlText)
	return err
}

func GetLastDBVersion(wd string) (*DBVersion, error) {
	db, err := OpenDb(wd)
	if err != nil {
		return nil, err
	}
	defer CloseDb(db)
	return GetLastDBVersionByDB(db)
}

func GetLastDBVersionByDB(db *sql.DB) (*DBVersion, error) {
	row := db.QueryRow(`SELECT "version", "date", "sql" FROM "comm_db_version" ORDER BY "version" DESC LIMIT 1;`)
	var latestVersion DBVersion
	err := row.Scan(&latestVersion.Version, &latestVersion.Date, &latestVersion.Sql)
	if err != nil {
		return nil, err
		//if err == sql.ErrNoRows {
		//	fmt.Println("No versions found")
		//} else {
		//	return nil, err
		//}
	}
	return &latestVersion, nil
}

func GetUpdateSqlLislMap(wd string) (map[int64][]string, error) {
	type DBVersion struct {
		Version int64
		SQLList []string
	}
	configPath := GetConfPath(wd)
	viper.SetConfigName("db.versions.config")
	viper.AddConfigPath(configPath)
	if err := viper.ReadInConfig(); err != nil {
		return nil, fmt.Errorf("read conf db.versions fail,%s \n", err)
	}
	var dbVersions []DBVersion
	key := "comm"
	if err := viper.UnmarshalKey(key, &dbVersions); err != nil {
		return nil, fmt.Errorf("read conf db.versions fail,%s \n", err)
	}
	ss := map[int64][]string{}
	for i := 0; i < len(dbVersions); i++ {
		ss[dbVersions[i].Version] = dbVersions[i].SQLList
	}
	return ss, nil
}
