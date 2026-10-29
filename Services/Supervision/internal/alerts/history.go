// Package alerts derives historical failure records from the original Monitor
// event journal. It deliberately does not infer current active alarms or
// persist a second event database.
package alerts

import (
	"strings"

	"gogo/internal/events"
	"gogo/internal/program"
)

// Incident is an observed historical failure signal, not an active alarm.
// Time is a Unix-millisecond timestamp; ProgramID stays an exact string.
type Incident struct {
	ProgramID int64  `json:"programId,string"`
	PID       int    `json:"pid"`
	Event     int    `json:"event"`
	Time      int64  `json:"time"`
	Severity  string `json:"severity"`
	Kind      string `json:"kind"`
	Message   string `json:"message"`
}

// Classify intentionally recognizes only unambiguous existing failure events.
// A normal exit is not necessarily a fault, and a heartbeat is not a failure.
func Classify(record events.Record) (Incident, bool) {
	item := Incident{
		ProgramID: record.ProgramID,
		PID:       record.PID,
		Event:     record.Event,
		Time:      record.Time,
		Message:   record.Message,
	}
	switch record.Event {
	case program.EventMaxError:
		item.Severity = "critical"
		item.Kind = "max-error"
	case program.EventMaxCount:
		item.Severity = "critical"
		item.Kind = "max-start"
	case program.EventStart:
		if !strings.Contains(record.Message, "启动失败") {
			return Incident{}, false
		}
		item.Severity = "warning"
		item.Kind = "start-failed"
	default:
		return Incident{}, false
	}
	return item, true
}

// ReadHistory filters the bounded on-disk event window before limiting the
// result count, so frequent heartbeats do not bury rare failure records.
func ReadHistory(path string, limit int) ([]Incident, error) {
	records, err := events.ReadRecentWhere(path, limit, func(record events.Record) bool {
		_, detected := Classify(record)
		return detected
	})
	if err != nil {
		return nil, err
	}
	out := make([]Incident, 0, len(records))
	for _, record := range records {
		incident, ok := Classify(record)
		if ok {
			out = append(out, incident)
		}
	}
	return out, nil
}
