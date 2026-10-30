package main

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strconv"
	"strings"

	"github.com/shirou/gopsutil/v3/process"
	"gogo/internal/program"
)

const legacyPIDMaxBytes = 64

// scanLegacyPIDFiles is deliberately READ ONLY. A PID file survived the
// preceding Daemon generation, not necessarily the child or its identity.
// Never kill a candidate by bare PID during startup: quarantine ambiguous
// programs instead of risking an unrelated process or a duplicate child.
func scanLegacyPIDFiles(workDir string, programs []program.Program) map[int64]string {
	quarantined := make(map[int64]string)
	names := make(map[string][]int64)
	for _, item := range programs {
		if item.ID <= 0 || item.ID == program.DefaultProgramIdDaemon {
			continue
		}
		name := item.GetFileName()
		if name == "" {
			quarantined[item.ID] = "configured executable filename is empty"
			continue
		}
		names[name] = append(names[name], item.ID)
	}
	for _, item := range programs {
		if item.ID <= 0 || item.ID == program.DefaultProgramIdDaemon {
			continue
		}
		name := item.GetFileName()
		if name == "" {
			continue
		}
		path := filepath.Join(workDir, "pid", name+".pid")
		info, err := os.Lstat(path)
		if os.IsNotExist(err) {
			continue
		}
		if err != nil {
			quarantined[item.ID] = fmt.Sprintf("cannot inspect legacy PID file: %v", err)
			continue
		}
		if len(names[name]) > 1 {
			quarantined[item.ID] = "legacy PID filename shared by multiple Program IDs"
			continue
		}
		if !info.Mode().IsRegular() {
			quarantined[item.ID] = "legacy PID file is not a regular file"
			continue
		}
		if info.Size() <= 0 || info.Size() > legacyPIDMaxBytes {
			quarantined[item.ID] = "invalid legacy PID file size"
			continue
		}
		file, err := os.Open(path)
		if err != nil {
			quarantined[item.ID] = fmt.Sprintf("cannot read legacy PID file: %v", err)
			continue
		}
		data, readErr := io.ReadAll(io.LimitReader(file, legacyPIDMaxBytes+1))
		closeErr := file.Close()
		if readErr != nil || closeErr != nil || len(data) == 0 || len(data) > legacyPIDMaxBytes {
			quarantined[item.ID] = "cannot reliably read bounded legacy PID file"
			continue
		}
		pid64, err := strconv.ParseInt(strings.TrimSpace(string(data)), 10, 32)
		if err != nil || pid64 <= 0 {
			quarantined[item.ID] = "invalid PID in legacy PID file"
			continue
		}
		pid := int(pid64)
		if !safeManagedPID(pid) {
			quarantined[item.ID] = "legacy PID points to Daemon itself or an unsafe PID"
			continue
		}
		exists, err := process.PidExists(int32(pid))
		if err != nil {
			quarantined[item.ID] = fmt.Sprintf("legacy PID existence is uncertain: %v", err)
			continue
		}
		if !exists {
			// Stale PID files are left untouched for forensic purposes;
			// no living process is evidenced by this PID.
			continue
		}
		if err := verifyManagedExecutablePID(pid, item.Path); err != nil {
			quarantined[item.ID] = fmt.Sprintf("living legacy PID %d has unverified identity: %v", pid, err)
			continue
		}
		quarantined[item.ID] = fmt.Sprintf("living legacy PID %d matches executable but ownership/generation is unverified", pid)
	}
	return quarantined
}
