// Package guardview reads Daemon-authored local diagnostic files without
// controlling processes or duplicating live Monitor/Daemon state.
package guardview

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"time"
)

const maxDiagnosticBytes int64 = 1024 * 1024
const snapshotVersion = 1

type Item struct {
	ProgramID string `json:"programId"`
	Reason    string `json:"reason"`
}

type Source struct {
	Available  bool   `json:"available"`
	ObservedAt string `json:"observedAt,omitempty"`
	Error      string `json:"error,omitempty"`
	Items      []Item `json:"items"`
}

type Report struct {
	Terminal Source `json:"terminal"`
	Orphans  Source `json:"orphans"`
}

// Read gives each source a separate explicit availability state. Missing or
// unreadable files are NEVER represented as a confirmed empty alarm list.
func Read(workDir string) Report {
	base := filepath.Join(workDir, "supervision")
	return Report{
		Terminal: readSource(filepath.Join(base, "daemon-terminal-latches.json"), false),
		Orphans:  readSource(filepath.Join(base, "daemon-orphan-observation.json"), true),
	}
}

type fileData struct {
	Version     int            `json:"version"`
	Terminal    map[string]int `json:"terminal"`
	Quarantined map[string]string `json:"quarantined"`
	ObservedAt  string         `json:"observedAt"`
}

func readSource(path string, orphan bool) Source {
	result := Source{Items: make([]Item, 0)}
	file, err := os.Open(path)
	if err != nil {
		return unavailable(result, err)
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil || !info.Mode().IsRegular() {
		return unavailable(result, errors.New("diagnostic file is not a regular file"))
	}
	if info.Size() < 1 || info.Size() > maxDiagnosticBytes {
		return unavailable(result, errors.New("diagnostic file has invalid size"))
	}
	content, err := io.ReadAll(io.LimitReader(file, maxDiagnosticBytes+1))
	if err != nil || len(content) < 1 || int64(len(content)) > maxDiagnosticBytes {
		return unavailable(result, errors.New("diagnostic file cannot be read safely"))
	}
	var data fileData
	decoder := json.NewDecoder(bytes.NewReader(content))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&data); err != nil {
		return unavailable(result, err)
	}
	if err := decoder.Decode(new(interface{})); err != io.EOF {
		return unavailable(result, errors.New("unexpected trailing JSON"))
	}
	if data.Version != snapshotVersion {
		return unavailable(result, fmt.Errorf("unsupported diagnostic version %d", data.Version))
	}
	if orphan {
		if data.Quarantined == nil {
			return unavailable(result, errors.New("missing quarantine records"))
		}
		at, err := time.Parse(time.RFC3339Nano, data.ObservedAt)
		if err != nil {
			return unavailable(result, errors.New("invalid orphan observation time"))
		}
		result.ObservedAt = at.UTC().Format(time.RFC3339Nano)
		for id, reason := range data.Quarantined {
			if !validID(id) || reason == "" {
				return unavailable(Source{Items: []Item{}}, errors.New("invalid quarantine record"))
			}
			result.Items = append(result.Items, Item{ProgramID: id, Reason: reason})
		}
	} else {
		if data.Terminal == nil {
			return unavailable(result, errors.New("missing terminal latch records"))
		}
		result.ObservedAt = info.ModTime().UTC().Format(time.RFC3339Nano)
		for id, reason := range data.Terminal {
			if !validID(id) || (reason != 4 && reason != 5) {
				return unavailable(Source{Items: []Item{}}, errors.New("invalid terminal latch record"))
			}
			label := "MaxError / failure limit"
			if reason == 5 {
				label = "MaxCount / start limit"
			}
			result.Items = append(result.Items, Item{ProgramID: id, Reason: label})
		}
	}
	sort.Slice(result.Items, func(i, j int) bool {
		a, _ := strconv.ParseInt(result.Items[i].ProgramID, 10, 64)
		b, _ := strconv.ParseInt(result.Items[j].ProgramID, 10, 64)
		return a < b
	})
	result.Available = true
	return result
}

func validID(id string) bool {
	value, err := strconv.ParseInt(id, 10, 64)
	return err == nil && value > 0 && strconv.FormatInt(value, 10) == id
}

func unavailable(result Source, err error) Source {
	result.Available = false
	result.Error = err.Error()
	result.Items = []Item{}
	result.ObservedAt = ""
	return result
}
