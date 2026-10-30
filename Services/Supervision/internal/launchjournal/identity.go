package launchjournal

import (
	"fmt"
	"os"
	"strconv"

	"github.com/shirou/gopsutil/v3/process"
)

// observedBirthMs is an OS observation of a PID's creation timestamp. It
// does not create a durable process handle or prove parent/child ownership.
// Missing permission, early child exit, and platform errors are recorded as
// unknown (zero), never silently treated as a verified identity.
func observedBirthMs(pid int) int64 {
	if pid <= 0 || pid > 2147483647 {
		return 0
	}
	p, err := process.NewProcess(int32(pid))
	if err != nil {
		return 0
	}
	ms, err := p.CreateTime()
	if err != nil || ms <= 0 {
		return 0
	}
	return ms
}

func birthEvidence(recordedMs, currentMs int64) string {
	if recordedMs <= 0 {
		return "unverified-original-birth"
	}
	if currentMs <= 0 {
		return "unverified-current-birth"
	}
	if recordedMs != currentMs {
		return "pid-creation-mismatch"
	}
	return "same-creation-time"
}

// DescribeCurrentPID is a read-only recovery hint. A matching PID, creation
// timestamp and executable identity is STILL NOT permission to Kill, adopt,
// or automatically clear the launch latch. The original Worker Wait is lost
// after Daemon SIGKILL.
func DescribeCurrentPID(rec Record) string {
	if rec.Stage != "started" || rec.PID <= 0 || rec.PID > 2147483647 {
		return "pre-start-intent-no-PID-confirmation"
	}
	p, err := process.NewProcess(int32(rec.PID))
	if err != nil {
		return "pid-not-observable"
	}
	exists, err := process.PidExists(int32(rec.PID))
	if err != nil {
		return "pid-existence-unknown"
	}
	if !exists {
		return "pid-no-longer-present"
	}
	current, err := p.CreateTime()
	if err != nil {
		return "pid-present-birth-unreadable"
	}
	birth := birthEvidence(rec.ProcessStartMs, current)
	if birth != "same-creation-time" {
		return birth
	}
	actual, err := p.Exe()
	if err != nil || actual == "" {
		return "same-creation-time-executable-unreadable"
	}
	expectedInfo, expectedErr := os.Stat(rec.Path)
	actualInfo, actualErr := os.Stat(actual)
	if expectedErr != nil || actualErr != nil {
		return "same-creation-time-executable-file-unreadable"
	}
	if !os.SameFile(expectedInfo, actualInfo) {
		return "same-creation-time-executable-mismatch"
	}
	return fmt.Sprintf("same-creation-time-and-executable-observed-pid=%s-not-proof-of-ownership", strconv.Itoa(rec.PID))
}
