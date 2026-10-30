// Package resourceview samples host resource facts without storing a second
// monitor state or inferring service/device/business readiness.
package resourceview

import (
	"math"
	"path/filepath"
	"strings"
	"time"

	"github.com/shirou/gopsutil/v3/cpu"
	"github.com/shirou/gopsutil/v3/disk"
	"github.com/shirou/gopsutil/v3/mem"
)

type CPU struct {
	Available bool    `json:"available"`
	Percent   float64 `json:"percent"`
	SampledAt string  `json:"sampledAt"`
	Error     string  `json:"error,omitempty"`
}

type Storage struct {
	Available bool    `json:"available"`
	TotalBytes uint64 `json:"totalBytes"`
	UsedBytes uint64  `json:"usedBytes"`
	AvailableBytes uint64  `json:"availableBytes"`
	UsedPercent float64 `json:"usedPercent"`
	SampledAt string `json:"sampledAt"`
	Error string `json:"error,omitempty"`
}

type Report struct {
	Scope string `json:"scope"`
	DiskScope string `json:"diskScope"`
	CPU CPU `json:"cpu"`
	Memory Storage `json:"memory"`
	Disk Storage `json:"disk"`
}

// Collect samples host-wide CPU and physical memory, and only the filesystem
// containing workDir for disk. A failed sample is UNKNOWN, not 0% usage.
// CPU uses a bounded interval instead of gopsutil's first-call delta.
func Collect(workDir string) Report {
	out := Report{Scope: "host", DiskScope: "work-dir-filesystem"}
	now := func() string { return time.Now().UTC().Format(time.RFC3339Nano) }

	cpuValues, cpuErr := cpu.Percent(200*time.Millisecond, false)
	out.CPU.SampledAt = now()
	if percent, ok := validCPU(cpuValues, cpuErr); ok {
		out.CPU.Available, out.CPU.Percent = true, percent
	} else {
		out.CPU.Error = sampleError(cpuErr, "invalid host CPU sample")
	}

	memory, memErr := mem.VirtualMemory()
	out.Memory.SampledAt = now()
	if memErr == nil && memory != nil {
		out.Memory = storageSample(memory.Total, memory.Used, memory.Available, out.Memory.SampledAt)
	}
	if !out.Memory.Available {
		out.Memory.Error = sampleError(memErr, "invalid physical memory sample")
	}

	// Empty workDir must never silently mean the Monitor's process CWD.
	// That would report a different disk than the actual Daemon journal.
	out.Disk.SampledAt = now()
	if strings.TrimSpace(workDir) == "" {
		out.Disk.Error = "work directory is not configured"
		return out
	}
	path, pathErr := filepath.Abs(workDir)
	if pathErr == nil && path != "" {
		diskUsage, diskErr := disk.Usage(path)
		if diskErr == nil && diskUsage != nil {
			out.Disk = storageSample(diskUsage.Total, diskUsage.Used, diskUsage.Free, out.Disk.SampledAt)
		} else {
			out.Disk.Error = sampleError(diskErr, "invalid work directory filesystem sample")
		}
	} else {
		out.Disk.Error = sampleError(pathErr, "invalid work directory")
	}
	return out
}

func validCPU(values []float64, err error) (float64, bool) {
	if err != nil || len(values) != 1 {
		return 0, false
	}
	value := values[0]
	return value, !math.IsNaN(value) && !math.IsInf(value, 0) && value >= 0 && value <= 100
}

// storageSample rejects inconsistent totals. It never adjusts or synthesizes
// unavailable readings to make a number look valid.
func storageSample(total, used, free uint64, at string) Storage {
	out := Storage{SampledAt: at}
	if total == 0 || used > total || free > total {
		return out
	}
	out.Available = true
	out.TotalBytes, out.UsedBytes, out.AvailableBytes = total, used, free
	out.UsedPercent = float64(used) * 100 / float64(total)
	return out
}

func sampleError(_ error, fallback string) string {
	// Do not return raw OS paths or credentials through this diagnostics API.
	return fallback
}
