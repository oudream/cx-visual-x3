package daemonc

import (
	"context"
	"errors"
	"fmt"
	"os/exec"
	"sync"
)

// StopReceipt represents a local process stop attempt, not an HTTP or
// device-operation contract. Confirmed means the ORIGINAL exec.Cmd.Wait
// returned and its ProcessState was observed. It does not imply business Ready.
type StopReceipt struct {
	ProgramID     int64
	PID           int
	State         string
	SignalError   string
	WaitError     string
	WaitCompleted bool
}

const (
	StopRefused   = "refused"
	StopFailed    = "signal-failed"
	StopUnknown   = "unknown"
	StopConfirmed = "wait-confirmed"
)

type childWaitResult struct {
	waitError error
	reaped    bool
}

type ownedChild struct {
	cmd       *exec.Cmd
	done      chan childWaitResult
	requested bool
}

// StopController belongs to one guardian Worker generation. It does not own
// PID files or discover processes by PID. All signals use the original
// os.Process handle created by exec.Cmd.Start; only the Worker calls Wait.
type StopController struct {
	mu        sync.Mutex
	programID int64
	child     *ownedChild
}

func NewStopController(programID int64) *StopController {
	return &StopController{programID: programID}
}

// ActivePID exposes only the PID of the ORIGINAL attached child. It never
// looks up or resolves a process by a stale PID integer and is safe to call
// while the guardian is starting or being stopped.
func (c *StopController) ActivePID() int {
	if c == nil {
		return 0
	}
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.child == nil || c.child.cmd == nil || c.child.cmd.Process == nil {
		return 0
	}
	return c.child.cmd.Process.Pid
}

func (c *StopController) attach(cmd *exec.Cmd) {
	if c == nil || cmd == nil || cmd.Process == nil {
		return
	}
	c.mu.Lock()
	c.child = &ownedChild{cmd: cmd, done: make(chan childWaitResult, 1)}
	c.mu.Unlock()
}

// finish is called exactly once AFTER the Worker's cmd.Wait. A stop waiter
// receives its receipt even if the Daemon's ordinary event channel is blocked.
func (c *StopController) finish(cmd *exec.Cmd, waitErr error) bool {
	if c == nil || cmd == nil {
		return false
	}
	c.mu.Lock()
	defer c.mu.Unlock()
	child := c.child
	if child != nil && child.cmd == cmd {
		c.child = nil
		child.done <- childWaitResult{waitError: waitErr, reaped: cmd.ProcessState != nil}
		close(child.done)
		return child.requested
	}
	return false
}

// RequestStop never uses os.FindProcess or a bare stored PID. It refuses an
// absent/stale child and prevents repeated stop signals for the same child.
// Timeout yields unknown, never a fabricated "stopped" confirmation.
func (c *StopController) RequestStop(ctx context.Context, expectedPID int) StopReceipt {
	result := StopReceipt{State: StopRefused, PID: expectedPID}
	if c == nil {
		result.SignalError = "guardian has no owned stop controller"
		return result
	}
	result.ProgramID = c.programID
	if ctx == nil || ctx.Err() != nil || expectedPID <= 0 {
		result.SignalError = "invalid or cancelled stop request"
		return result
	}
	c.mu.Lock()
	child := c.child
	if child == nil || child.cmd.Process == nil || child.cmd.Process.Pid != expectedPID {
		c.mu.Unlock()
		result.SignalError = "stale PID or no active owned child"
		return result
	}
	if child.requested {
		c.mu.Unlock()
		result.SignalError = "stop already requested for this child"
		return result
	}
	// Holding mu prevents finish from detaching the child between the
	// generation check and signal. Mark an intentional stop ONLY after the
	// OS accepted the signal. A failed Kill must not erase crash accounting.
	err := child.cmd.Process.Kill()
	if err != nil {
		c.mu.Unlock()
		result.State = StopFailed
		result.SignalError = fmt.Sprintf("stop signal failed: %v", err)
		return result
	}
	child.requested = true
	c.mu.Unlock()
	result.State = StopUnknown
	select {
	case outcome := <-child.done:
		if !outcome.reaped {
			result.SignalError = "Wait returned without a child ProcessState"
			return result
		}
		result.State = StopConfirmed
		result.WaitCompleted = true
		if outcome.waitError != nil && !errors.Is(outcome.waitError, context.Canceled) {
			result.WaitError = outcome.waitError.Error()
		}
		return result
	case <-ctx.Done():
		result.SignalError = "timed out before original Worker Wait confirmation: " + ctx.Err().Error()
		return result
	}
}
