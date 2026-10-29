package program

import (
	"errors"
	"fmt"
	"gogo/common/utilities"
)

const (
	SettingKeyMqttIp   = "MqttIp"
	SettingKeyMqttPort = "MqttPort"

	SettingKeyRedisIp   = "RedisIp"
	SettingKeyRedisPort = "RedisPort"

	SettingKeyInfluxDBIp    = "InfluxDBIp"
	SettingKeyInfluxDBPort  = "InfluxDBPort"
	SettingKeyInfluxDBToken = "InfluxDBToken"
)

var (
	// SqlDefaultSettings
	// attention! attention! attention! : Id, SettingKeyRedisStatus.Id = 12
	SqlDefaultSettings = []Setting{
		{Key: SettingKeyMqttIp, Value: "127.0.0.1"},
		{Key: SettingKeyMqttPort, Value: "7805"},
		//
		{Key: SettingKeyRedisIp, Value: "127.0.0.1"},
		{Key: SettingKeyRedisPort, Value: "7807"},
		//
		{Key: SettingKeyInfluxDBIp, Value: "172.17.0.1"},
		{Key: SettingKeyInfluxDBPort, Value: "7809"},
		{Key: SettingKeyInfluxDBToken, Value: ""},
	}
)

type Setting struct {
	Key   string `db:"key" json:"key"`
	Value string `db:"value" json:"value"`
}

func NewSetting(key, value string) Setting {
	return Setting{Key: key, Value: value}
}

func GetSettings(wd string) ([]Setting, error) {
	dbMap, err := OpenDbMap(wd)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("OpenDbMap Fail. err: %s", err))
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Setting{}, TableNameSettings).SetKeys(true, "key")
	//
	var objs []Setting
	var sqlSelect = fmt.Sprintf("select * from `%s`;", TableNameSettings)
	_, err = dbMap.Select(&objs, sqlSelect)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("getSettingList [TableNameProgramName: %s] FAIL. err: %s", TableNameSettings, err))
	} else {
		return objs, nil
	}
}

func GetSettingsByKeys(wd string, keys []string) ([]Setting, error) {
	dbMap, err := OpenDbMap(wd)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("OpenDbMap Fail. err: %s", err))
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Setting{}, TableNameSettings).SetKeys(true, "key")
	//
	var objs []Setting
	var sqlSelect = fmt.Sprintf(`SELECT "key", "Value" FROM "%s" WHERE "key" IN ( %s );`, TableNameSettings, utilities.ArrayStringJoinFix(keys, "'", ","))
	_, err = dbMap.Select(&objs, sqlSelect)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("getSettingList [TableNameProgramName: %s] FAIL. err: %s", TableNameSettings, err))
	} else {
		return objs, nil
	}
}

func GetSetting(settings []Setting, key string) (string, error) {
	for i := 0; i < len(settings); i++ {
		if settings[i].Key == key {
			return settings[i].Value, nil
		}
	}
	return "", errors.New("can not find key: " + key)
}

func UpdateSettings(wd string, settings []Setting) error {
	dbMap, err := OpenDbMap(wd)
	if err != nil {
		return err
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Setting{}, TableNameSettings).SetKeys(false, "key")
	//
	for i := 0; i < len(settings); i++ {
		setting := settings[i]
		_, err = dbMap.Update(&setting)
		if err != nil {
			return err
		}
	}
	return nil
}

func GetAllSetting(wd string) (map[string]string, error) {
	dbMap, err := OpenDbMap(wd)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("OpenDbMap Fail. err: %s", err))
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Setting{}, TableNameSettings).SetKeys(false, "key")
	//
	var objs []Setting
	var sqlSelect = fmt.Sprintf("select * from `%s`;", TableNameSettings)
	_, err = dbMap.Select(&objs, sqlSelect)
	if err != nil {
		return nil, errors.New(fmt.Sprintf("getSettingList [TableNameProgramName: %s] FAIL. err: %s", TableNameSettings, err))
	} else {
		allSetting := make(map[string]string)
		for i := 0; i < len(objs); i++ {
			allSetting[objs[i].Key] = objs[i].Value
		}
		return allSetting, nil
	}
}

func InsertSettings(wd string, settings []Setting) error {
	dbMap, err := OpenDbMap(wd)
	if err != nil {
		return err
	}
	defer CloseDbMap(dbMap)
	dbMap.AddTableWithName(Setting{}, TableNameSettings).SetKeys(false, "key")
	//
	for i := 0; i < len(settings); i++ {
		setting := settings[i]
		err = dbMap.Insert(&setting)
		if err != nil {
			return err
		}
	}
	return nil
}

func GetInfluxDBConfig(wd string) (error, string, int, string) {
	var (
		// influxdb
		influxdbHost  string
		influxdbPort  int
		influxdbToken string
	)
	if settings, err := GetSettings(wd); err == nil {
		if v, e := GetSetting(settings, SettingKeyInfluxDBIp); e == nil {
			influxdbHost = v
		}
		if v, e := GetSetting(settings, SettingKeyInfluxDBPort); e == nil {
			influxdbPort = utilities.ConvertString2Int(v)
		}
		if v, e := GetSetting(settings, SettingKeyInfluxDBToken); e == nil {
			influxdbToken = v
		}
	} else {
		return err, influxdbHost, influxdbPort, influxdbToken
	}
	return nil, influxdbHost, influxdbPort, influxdbToken
}

func GetRedisConfig(wd string) (error, string, int) {
	var (
		host string
		port int
	)
	if settings, err := GetSettings(wd); err == nil {
		if v, e := GetSetting(settings, SettingKeyRedisIp); e == nil {
			host = v
		}
		if v, e := GetSetting(settings, SettingKeyRedisPort); e == nil {
			port = utilities.ConvertString2Int(v)
		}
	} else {
		return err, host, port
	}
	return nil, host, port
}

func GetMqttConfig(wd string) (error, string, int) {
	var (
		host string
		port int
	)
	if settings, err := GetSettings(wd); err == nil {
		if v, e := GetSetting(settings, SettingKeyMqttIp); e == nil {
			host = v
		}
		if v, e := GetSetting(settings, SettingKeyMqttPort); e == nil {
			port = utilities.ConvertString2Int(v)
		}
	} else {
		return err, host, port
	}
	return nil, host, port
}
