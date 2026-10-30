package main

import (
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"strconv"
	"time"
)

type orphanObservationFile struct {
	Version     int               `json:"version"`
	ObservedAt  string            `json:"observedAt"`
	Quarantined map[string]string `json:"quarantined"`
}

// writeOrphanObservation publishes only the startup scan's observations for
// read-only Monitor diagnostics. The active quarantine remains exclusively
// in programWorkerGate; stale observation files never grant start authority.
func writeOrphanObservation(workDir string, observed map[int64]string) error {
	entries := make(map[string]string, len(observed))
	for id, reason := range observed {
		if id <= 0 || reason == "" {
			return errors.New("invalid orphan observation")
		}
		entries[strconv.FormatInt(id, 10)] = reason
	}
	value := orphanObservationFile{
		Version: 1, ObservedAt: time.Now().UTC().Format(time.RFC3339Nano),
		Quarantined: entries,
	}
	data, err := json.Marshal(value)
	if err != nil {
		return err
	}
	if len(data) > terminalLatchMaxBytes {
		return errors.New("orphan observation exceeds size limit")
	}
	dir := filepath.Join(workDir, "supervision")
	if err := os.MkdirAll(dir, 0700); err != nil {
		return err
	}
	file, err := os.CreateTemp(dir, ".orphan-observation-*")
	if err != nil {
		return err
	}
	defer os.Remove(file.Name())
	if err := file.Chmod(0600); err != nil {
		file.Close()
		return err
	}
	if _, err := file.Write(data); err != nil {
		file.Close()
		return err
	}
	if err := file.Sync(); err != nil {
		file.Close()
		return err
	}
	if err := file.Close(); err != nil {
		return err
	}
	return os.Rename(file.Name(), filepath.Join(dir, "daemon-orphan-observation.json"))
}
