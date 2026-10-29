// Package events reads bounded historical Monitor events without becoming
// a second event storage owner. The on-disk format remains the legacy NDJSON.
package events

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"sort"
)

const maxTailBytes int64 = 2 << 20
const maxRecordBytes = 64 << 10

// Record matches the old ProgramEventRecord persistence contract.
type Record struct {
	ProgramID int64  `json:"programId,string"`
	PID       int    `json:"pId"`
	Event     int    `json:"event"`
	Time      int64  `json:"time"`
	Message   string `json:"msg"`
}

// ReadRecent returns up to limit original events, newest first.
func ReadRecent(path string, limit int) ([]Record, error) {
	return ReadRecentWhere(path, limit, nil)
}

// ReadRecentWhere filters all decodable records within the bounded tail
// before applying the result limit. This prevents frequent heartbeats from
// displacing rarer error events out of the requested result page.
// A nil predicate includes all records; a missing file returns an empty list.
func ReadRecentWhere(path string, limit int, match func(Record) bool) ([]Record, error) {
	if limit < 1 || limit > 100 {
		return nil, fmt.Errorf("event limit out of range")
	}
	file, err := os.Open(path)
	if os.IsNotExist(err) {
		return []Record{}, nil
	}
	if err != nil {
		return nil, fmt.Errorf("open event log: %w", err)
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil {
		return nil, fmt.Errorf("stat event log: %w", err)
	}
	start := info.Size() - maxTailBytes
	if start < 0 {
		start = 0
	}
	if _, err = file.Seek(start, io.SeekStart); err != nil {
		return nil, fmt.Errorf("seek event log: %w", err)
	}
	data, err := io.ReadAll(io.LimitReader(file, maxTailBytes))
	if err != nil {
		return nil, fmt.Errorf("read event log: %w", err)
	}
	if start > 0 {
		// A tail read can begin inside a JSON record. Discard this fragment.
		idx := bytes.IndexByte(data, '\n')
		if idx < 0 {
			return []Record{}, nil
		}
		data = data[idx+1:]
	}
	rows := bytes.Split(data, []byte{'\n'})
	items := make([]Record, 0)
	for _, row := range rows {
		row = bytes.TrimSpace(row)
		if len(row) == 0 || len(row) > maxRecordBytes {
			continue
		}
		var value Record
		if json.Unmarshal(row, &value) != nil || value.Time <= 0 {
			// Interrupted/legacy-corrupt lines must not block other events.
			continue
		}
		if match == nil || match(value) {
			items = append(items, value)
		}
	}
	sort.SliceStable(items, func(i, j int) bool { return items[i].Time > items[j].Time })
	if len(items) > limit {
		items = items[:limit]
	}
	return items, nil
}
