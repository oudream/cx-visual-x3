package main

import (
	"bufio"
	"encoding/json"
	"gogo/hy/common/program"
	"os"
	"path/filepath"
	"sort"
)

type ProgramEventRecord struct {
	ProgramId int64  `json:"programId,string"`
	PId       int    `json:"pId"`
	Event     int    `json:"event"`
	Time      int64  `json:"time"`
	Msg       string `json:"msg"`
}

func GetRecordFilePath(wd string) string {
	return filepath.Join(program.GetLogPath(wd), myProgram.GetFileName(), "program.event.list")
}

func SaveRecord(wd string, record ProgramEventRecord) error {
	fp := GetRecordFilePath(wd)
	file, err := os.OpenFile(fp, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		return err
	}
	defer func(file *os.File) {
		_ = file.Close()
	}(file)
	// to json
	jsonRecord, err := json.Marshal(record)
	if err != nil {
		return err
	}
	// write file
	_, err = file.Write(jsonRecord)
	if err != nil {
		return err
	}
	_, err = file.WriteString("\n")
	if err != nil {
		return err
	}
	return nil
}

func GetLatestRecord(wd string) (*ProgramEventRecord, error) {
	fp := GetRecordFilePath(wd)
	file, err := os.Open(fp)
	if err != nil {
		return nil, err
	}
	defer func(file *os.File) {
		_ = file.Close()
	}(file)

	scanner := bufio.NewScanner(file)
	var latestRecord *ProgramEventRecord
	for scanner.Scan() {
		var record ProgramEventRecord
		if err := json.Unmarshal(scanner.Bytes(), &record); err != nil {
			return nil, err
		}
		if latestRecord == nil || record.Time > latestRecord.Time {
			latestRecord = &record
		}
	}

	if err := scanner.Err(); err != nil {
		return nil, err
	}

	return latestRecord, nil
}

func GetRecordList(wd string) ([]ProgramEventRecord, error) {
	fp := GetRecordFilePath(wd)
	file, err := os.Open(fp)
	if err != nil {
		return nil, err
	}
	defer func(file *os.File) {
		_ = file.Close()
	}(file)

	scanner := bufio.NewScanner(file)
	var records []ProgramEventRecord
	for scanner.Scan() {
		var record ProgramEventRecord
		if err := json.Unmarshal(scanner.Bytes(), &record); err != nil {
			return nil, err
		}
		records = append(records, record)
	}
	sort.SliceStable(records, func(i, j int) bool {
		return records[i].Time > records[j].Time
	})

	if err := scanner.Err(); err != nil {
		return nil, err
	}

	return records, nil
}
