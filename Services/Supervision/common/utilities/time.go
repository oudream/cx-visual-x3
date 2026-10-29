package utilities

import (
	"fmt"
	"time"
)

const (
	TimeFormatSqlserver = "2006-01-02T15:04:05.999"
	TimeFormatMysql     = "2006-01-02 15:04:05"
	TimeFormatFile      = "2006-01-02T15-04-05"
	TimeDayMilliseconds = int64(86400000)
	TimeInvalid         = int64(-1)
)

func TimeGetDayTimestamp(tm time.Time) (int64, int64) {
	tm1 := time.Date(tm.Year(), tm.Month(), tm.Day(), 0, 0, 0, 0, tm.Location())
	tm2 := tm1.AddDate(0, 0, 1)
	return tm1.UnixNano() / 1e6, tm2.UnixNano() / 1e6
}

func TimeGetDayTimestampAndString(tm time.Time, sec int) (int64, int64, string, string) {
	tm1 := time.Date(tm.Year(), tm.Month(), tm.Day(), 0, 0, sec, 0, tm.Location())
	tm2 := tm1.AddDate(0, 0, 1)
	ms1 := tm1.UnixNano() / 1e6
	ms2 := tm2.UnixNano() / 1e6
	ts1 := tm1.Format(TimeFormatMysql)
	ts2 := tm2.Format(TimeFormatMysql)
	return ms1, ms2, ts1, ts2
}

func TimeGetDayTimeString(tm time.Time) (string, string) {
	tm1 := time.Date(tm.Year(), tm.Month(), tm.Day(), 0, 0, 0, 0, tm.Location())
	tm2 := tm1.AddDate(0, 0, 1)
	return tm1.Format(TimeFormatMysql), tm2.Format(TimeFormatMysql)
}

func TimeGetDayTimeToString(tm1 int64, tm2 int64) (string, string) {
	s1 := time.Unix(tm1/1000, tm1%1000*1e6).Format(TimeFormatMysql)
	s2 := time.Unix(tm2/1000, tm2%1000*1e6).Format(TimeFormatMysql)
	return s1, s2
}

func funcTimeGetCurrentDayTimestamp() (int64, int64) {
	return TimeGetDayTimestamp(time.Now())
}

func TimeGetCurrentTimestamp() int64 {
	return time.Now().UnixNano() / 1e6
}

func TimeGetCurrentMsepoch() int64 {
	return time.Now().UnixNano() / 1e6
}

func TimeGetCurrentTimeString() string {
	return time.Now().Format(TimeFormatMysql)
}

func TimeGetCurrentTimeAscii() string {
	now := time.Now()
	year, month, day := now.Date()
	hour, min, sec := now.Clock()
	return fmt.Sprintf("%04d%02d%02d%02d%02d%02d", year, month, day, hour, min, sec)
}

func TimeGetCurrentTimeMsAscii() string {
	now := time.Now()
	year, month, day := now.Date()
	hour, min, sec := now.Clock()
	ms := now.Nanosecond() / 1e6
	return fmt.Sprintf("%04d%02d%02d%02d%02d%02d%03d", year, month, day, hour, min, sec, ms)
}

func TimeGetCurrentSepoch() int32 {
	return int32(time.Now().UnixNano() / 1e9)
}

func TimeGetOperatorMsepoch() int64 {
	return time.Now().UnixNano() / 1e6
}

func TimeTime2String(tm time.Time) string {
	return tm.Format(TimeFormatMysql)
}

func TimeTimeFromString(dt string) (time.Time, error) {
	return time.Parse(TimeFormatMysql, dt)
}

func TimeTimestamp2String(tm int64) string {
	if tm > 1000000000000 && tm < 9999999999999 {
		return time.Unix(tm/1000, tm%1000*1e6).Format(TimeFormatMysql)
	} else {
		return time.Unix(tm/1000000, tm%1000*1e6).Format(TimeFormatMysql)
	}
}

func TimeTimestamp2Time(tm int64) time.Time {
	return time.Unix(tm/1000, tm%1000*1e6)
}

func TimeTimestampSecond2Time(tm int64) time.Time {
	return time.Unix(tm, 0)
}

func TimeTime2Timestamp(tm time.Time) int64 {
	return tm.UnixNano() / 1e6
}

func TimeTime2TimestampSecond(tm time.Time) int64 {
	return tm.Unix()
}

func TimeDiff2Now(tm time.Time) int64 {
	return (time.Now().UnixNano() / 1e6) - (tm.UnixNano() / 1e6)
}

func TimeDiff2NowTimestamp(tm int64) int64 {
	return (time.Now().UnixNano() / 1e6) - tm
}

// TimeEqual 判斷兩個時間字符所表達的時間是否同一時間
func TimeEqual(tmSqlServer string, tmMysql string) bool {
	tm1, err1 := time.Parse(TimeFormatSqlserver, tmSqlServer)
	tm2, err2 := time.Parse(TimeFormatMysql, tmMysql)
	if err1 != nil || err2 != nil {
		return false
	} else {
		diff := tm2.Unix() - tm1.Unix()
		return diff >= -1 && diff <= 1
	}
}
