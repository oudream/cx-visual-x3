package main

import (
	"context"
	"fmt"
	"log"
	"time"

	"gogo/internal/daemonc"
	"gogo/internal/stopjournal"
)

// ownedStopOutcome is a Daemon-local receipt. No new HTTP/UDP control
// endpoint or protocol is created. A result is conclusive only after the
// ORIGINAL worker's cmd.Wait has returned.
type ownedStopOutcome struct {
	Action  string
	Receipt daemonc.StopReceipt
}

const ownedStopWaitTimeout = 8 * time.Second

// queueOwnedStop dispatches a stop to the correct guardian generation without
// blocking the Daemon's main event loop. It never falls back to killing a
// process found only by a stored integer PID.
func queueOwnedStop(id int64, pid int, action string, receipts chan<- ownedStopOutcome) bool {
	if id <= 0 || !safeManagedPID(pid) || receipts == nil {
		return false
	}
	controller := _programWorkers.stopController(id)
	if controller == nil {
		return false
	}
	// Persist requested BEFORE any signal can be emitted. A failure refuses
	// the stop, leaving the managed child untouched and the result unknown.
	request, err := stopjournal.NewRequest(id, pid, action)
	if err != nil {
		if _logger != nil { _logger.Printf("CRITICAL: invalid stop operation: %v", err) }
		return false
	}
	if err := stopjournal.Append(*_workDir, request); err != nil {
		if _logger != nil {
			_logger.Printf("CRITICAL: refusing unjournaled stop Program=%d PID=%d: %v", id, pid, err)
		}
		return false
	}
	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), ownedStopWaitTimeout)
		defer cancel()
		result := controller.RequestStopWithLateReceipt(ctx, pid, func(late daemonc.StopReceipt) {
			// A timed-out request is UNKNOWN, not the final truth. The
			// original Worker's eventual Wait result is written even if
			// the Daemon main loop is no longer reading stopReceipts.
			if err := stopjournal.RecordReceipt(*_workDir, request, late.State, late.WaitCompleted, late.SignalError, late.WaitError); err != nil {
				if _logger != nil { _logger.Printf("CRITICAL: lost durable late Wait receipt %s: %v", request.OperationID, err) }
			}
			writeOwnedStopLog(_logger, ownedStopOutcome{
				Action: action + "/late", Receipt: late,
			})
		})
		if err := stopjournal.RecordReceipt(*_workDir, request, result.State, result.WaitCompleted, result.SignalError, result.WaitError); err != nil {
			if _logger != nil { _logger.Printf("CRITICAL: stop result not durable %s: %v", request.OperationID, err) }
		}
		outcome := ownedStopOutcome{Action: action, Receipt: result}
		// The response queue is bounded and the Daemon owns the reader. If
		// unavailable, a receipt must not be mistaken for success.
		select {
		case receipts <- outcome:
		default:
			if _logger != nil {
				_logger.Printf("CRITICAL: stop receipt queue full; writing result directly")
				writeOwnedStopLog(_logger, outcome)
			}
		}
	}()
	return true
}

func formatOwnedStopOutcome(outcome ownedStopOutcome) string {
	result := outcome.Receipt
	return fmt.Sprintf("stop result action=%s program=%d pid=%d state=%s waitCompleted=%t signalError=%q waitError=%q",
		outcome.Action, result.ProgramID, result.PID, result.State,
		result.WaitCompleted, result.SignalError, result.WaitError)
}

func writeOwnedStopLog(logger *log.Logger, outcome ownedStopOutcome) {
	if logger != nil {
		logger.Print(formatOwnedStopOutcome(outcome))
	}
}
