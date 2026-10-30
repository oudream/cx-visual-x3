package main

import (
	"errors"
	"fmt"
	"os"

	"github.com/shirou/gopsutil/v3/process"
)

// verifyManagedExecutablePID is a conservative precondition for legacy
// PID-based stops. A PID file or PID integer alone cannot prove ownership.
// Match the configured executable to the actual OS executable by file
// identity. Failure or uncertain identity MUST refuse the stop request.
//
// This is NOT a proof of PID creation time: a matching process could be
// replaced between inspection and signal. Full stop verification requires
// tracking the original child process handle and waiting for its exit.
func verifyManagedExecutablePID(pid int, configuredPath string) error {
	if !safeManagedPID(pid) || pid > 2147483647 {
		return fmt.Errorf("unsafe process ID: %d", pid)
	}
	if configuredPath == "" {
		return errors.New("managed executable path is empty")
	}
	expected, err := os.Stat(configuredPath)
	if err != nil {
		return fmt.Errorf("configured executable cannot be verified: %w", err)
	}
	actual, err := process.NewProcess(int32(pid))
	if err != nil {
		return fmt.Errorf("target PID cannot be inspected: %w", err)
	}
	executable, err := actual.Exe()
	if err != nil || executable == "" {
		return fmt.Errorf("target executable cannot be verified: %v", err)
	}
	actualFile, err := os.Stat(executable)
	if err != nil {
		return fmt.Errorf("target executable file cannot be inspected: %w", err)
	}
	if !os.SameFile(expected, actualFile) {
		return fmt.Errorf("target PID %d executable differs from configured managed program", pid)
	}
	return nil
}
