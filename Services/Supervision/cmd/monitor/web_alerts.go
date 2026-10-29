package main

import "gogo/internal/alerts"

// apiAlertHistory reads only the existing bounded Monitor event journal.
func apiAlertHistory(limit int) ([]alerts.Incident, error) {
	return alerts.ReadHistory(GetRecordFilePath(*_workDir), limit)
}
