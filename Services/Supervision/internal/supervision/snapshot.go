// Package supervision provides read-only, immutable snapshots of the running
// programs. Monitor remains the sole owner of live process state.
package supervision

import "sort"

// Program is a read-only projection of the existing Monitor status.
// Time values ending in Ms are Unix milliseconds; memory values are bytes.
// HeartbeatHealthy is not interpreted as a boolean: old programs use their
// own integer health codes, and absence of a heartbeat is not a failure.
type Program struct {
	ID               int64   `json:"id"`
	PID              int     `json:"pid"`
	Path             string  `json:"path"`
	Running          bool    `json:"running"`
	LastRunAtMs      int64   `json:"lastRunAtMs"`
	HeartbeatAtMs    int64   `json:"heartbeatAtMs"`
	HeartbeatHealthy int     `json:"heartbeatHealthy"`
	CPUPercent       float64 `json:"cpuPercent"`
	MemoryRSSBytes   uint64  `json:"memoryRssBytes"`
	StartCount       int     `json:"startCount"`
	ErrorCount       int     `json:"errorCount"`
	MaxCount         int     `json:"maxCount"`
	MaxError         int     `json:"maxError"`
}

// Overview aggregates an instantaneous status snapshot. These totals are not
// historical or predictive health metrics.
type Overview struct {
	Total              int     `json:"total"`
	Running            int     `json:"running"`
	Stopped            int     `json:"stopped"`
	HeartbeatReporting int     `json:"heartbeatReporting"`
	CPUPercent         float64 `json:"cpuPercent"`
	MemoryRSSBytes     uint64  `json:"memoryRssBytes"`
}

// Snapshot returns an ID-sorted copy. It never publishes a mutable live map.
func Snapshot(items []Program) []Program {
	out := append([]Program{}, items...)
	sort.Slice(out, func(i, j int) bool { return out[i].ID < out[j].ID })
	return out
}

// Find locates a program in a detached snapshot.
func Find(items []Program, id int64) (Program, bool) {
	for _, item := range items {
		if item.ID == id {
			return item, true
		}
	}
	return Program{}, false
}

// Summarize aggregates the supplied programs without querying processes or IO.
func Summarize(items []Program) Overview {
	var result Overview
	result.Total = len(items)
	for _, item := range items {
		if item.Running {
			result.Running++
		} else {
			result.Stopped++
		}
		if item.HeartbeatAtMs > 0 {
			result.HeartbeatReporting++
		}
		result.CPUPercent += item.CPUPercent
		result.MemoryRSSBytes += item.MemoryRSSBytes
	}
	return result
}
