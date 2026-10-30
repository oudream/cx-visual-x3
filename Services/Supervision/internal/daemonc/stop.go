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
	late      func(StopReceipt)
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
	child := c.child
	if child == nil || child.cmd != cmd {
		c.mu.Unlock()
		return false
	}
	c.child = nil
	outcome := childWaitResult{waitError: waitErr, reaped: cmd.ProcessState != nil}
	child.done <- outcome
	close(child.done)
	requested := child.requested
	late := child.late
	receipt := receiptAfterWait(c.programID, cmd.Process.Pid, outcome)
	c.mu.Unlock()
	// Do not invoke callbacks under the controller lock: the Daemon might
	// inspect the active PID or issue another request while reporting.
	if requested && late != nil {
		late(receipt)
	}
	return requested
}

// receiptAfterWait is the only path from an original Worker's Wait result
// to a confirmed receipt. A successful OS Kill by itself never reaches it.
func receiptAfterWait(id int64, pid int, outcome childWaitResult) StopReceipt {
	receipt := StopReceipt{ProgramID: id, PID: pid, State: StopUnknown}
	if !outcome.reaped {
		receipt.SignalError = "Wait returned without a child ProcessState"
		return receipt
	}
	receipt.State = StopConfirmed
	receipt.WaitCompleted = true
	if outcome.waitError != nil && !errors.Is(outcome.waitError, context.Canceled) {
		receipt.WaitError = outcome.waitError.Error()
	}
	return receipt
}

// RequestStop never uses os.FindProcess or a bare stored PID. It refuses an
// absent/stale child and prevents repeated stop signals for the same child.
// Timeout yields unknown, never a fabricated "stopped" confirmation.
func (c *StopController) RequestStop(ctx context.Context, expectedPID int) StopReceipt {
	return c.RequestStopWithLateReceipt(ctx, expectedPID, nil)
}

// RequestStopWithLateReceipt preserves the immediate bounded receipt and,
// only if it timed out, delivers the original Worker's eventual Wait outcome.
// It does not spawn a goroutine waiting forever on a lost child, and it does
// not perform any new PID lookup or signal after the first stop request.
func (c *StopController) RequestStopWithLateReceipt(ctx context.Context, expectedPID int, onLate func(StopReceipt)) StopReceipt {
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
		return receiptAfterWait(c.programID, expectedPID, outcome)
	case <-ctx.Done():
		result.SignalError = "timed out before original Worker Wait confirmation: " + ctx.Err().Error()
		if onLate != nil {
			c.mu.Lock()
			stillActive := c.child == child
			if stillActive {
				child.late = onLate
			}
			c.mu.Unlock()
			if !stillActive {
				// finish raced the timeout and has already buffered its
				// definitive Wait result. This select branch chose ctx;
				// do not lose the reconciliation.
				outcome := <-child.done
				onLate(receiptAfterWait(c.programID, expectedPID, outcome))
			}
		}
		return result
	}
}
