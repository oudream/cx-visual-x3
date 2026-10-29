package main

import (
	"net/http"

	"github.com/gin-gonic/gin"

	"gogo/internal/supervision"
)

// getProgramStatusList preserves the original on-wire JSON response.
func getProgramStatusList(c *gin.Context) {
	ss := getProgramStatuses()
	c.JSON(http.StatusOK, gin.H{"status": 200, "params": ss})
}

// apiProgramSnapshot converts the Monitor-owned state into a detached,
// read-only HTTP projection without duplicating its state storage.
func apiProgramSnapshot() []supervision.Program {
	statuses := getProgramStatuses()
	items := make([]supervision.Program, 0, len(statuses))
	for _, s := range statuses {
		items = append(items, supervision.Program{
			ID: s.DaemonStatus.Id,
			PID: s.DaemonStatus.Pid,
			Path: s.Path,
			Running: s.DaemonStatus.IsRun,
			LastRunAtMs: s.DaemonStatus.LastRunTime,
			HeartbeatAtMs: s.HeartbeatStatus.Time,
			HeartbeatHealthy: s.HeartbeatStatus.Healthy,
			CPUPercent: s.ProcessStatus.Cpu,
			MemoryRSSBytes: s.ProcessStatus.MemRSS,
			StartCount: s.DaemonStatus.StartCount,
			ErrorCount: s.DaemonStatus.ErrorCount,
			MaxCount: s.MaxCount,
			MaxError: s.MaxError,
		})
	}
	return items
}
