package main

import (
	"gogo/internal/events"
)

// apiEventHistory reads the original Monitor event journal only on a local
// request; no duplicate event database or external command endpoint is added.
func apiEventHistory(limit int) ([]events.Record, error) {
	return events.ReadRecent(GetRecordFilePath(*_workDir), limit)
}
