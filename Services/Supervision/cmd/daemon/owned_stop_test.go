package main

import (
	"context"
	"strings"
	"testing"
	"time"

	"gogo/internal/daemonc"
)

func TestOwnedStopResultDistinguishesVerifiedFromUnknown(t *testing.T) {
	confirmed := formatOwnedStopOutcome(ownedStopOutcome{
		Action: "config-change",
		Receipt: daemonc.StopReceipt{
			ProgramID: 72, PID: 140, State: daemonc.StopConfirmed, WaitCompleted: true,
			WaitError: "signal: killed",
		},
	})
	if !strings.Contains(confirmed, "state=wait-confirmed") ||
		!strings.Contains(confirmed, "waitCompleted=true") {
		t.Fatalf("confirmed stop result not explicit: %s", confirmed)
	}
	unknown := formatOwnedStopOutcome(ownedStopOutcome{
		Action: "shutdown",
		Receipt: daemonc.StopReceipt{
			ProgramID: 72, PID: 140, State: daemonc.StopUnknown,
			SignalError: "timeout before Wait",
		},
	})
	if strings.Contains(unknown, "waitCompleted=true") ||
		!strings.Contains(unknown, "state=unknown") {
		t.Fatalf("unknown stop was falsely confirmed: %s", unknown)
	}
}

func TestOwnedStopRejectsWithoutMatchingGuardian(t *testing.T) {
	original := _programWorkers
	_programWorkers = newProgramWorkerGate()
	t.Cleanup(func() { _programWorkers = original })
	receipts := make(chan ownedStopOutcome, 1)
	if queueOwnedStop(0, 42, "test", receipts) {
		t.Fatal("invalid Program ID accepted")
	}
	if queueOwnedStop(41, 0, "test", receipts) {
		t.Fatal("invalid PID accepted")
	}
	if queueOwnedStop(41, 421, "test", receipts) {
		t.Fatal("no matching guardian must be refused")
	}
	if len(receipts) != 0 {
		t.Fatal("refused request created false stop receipt")
	}
}

func TestOwnedStopDoesNotCrossGuardianGeneration(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 419
	first := daemonc.NewStopController(id)
	second := daemonc.NewStopController(id)
	releaseFirst := make(chan struct{})
	releaseSecond := make(chan struct{})
	secondStarted := make(chan struct{})
	if !gate.startWithStop(context.Background(), id, func() { <-releaseFirst }, first) {
		t.Fatal("could not launch initial guardian")
	}
	if started, queued := gate.requestWithStop(context.Background(), id,
		func() { close(secondStarted); <-releaseSecond }, second); started || !queued {
		t.Fatal("second guardian must queue")
	}
	if gate.stopController(id) != first {
		t.Fatal("queued second guardian replaced first's process authority")
	}
	close(releaseFirst)
	select {
	case <-secondStarted:
	case <-time.After(3 * time.Second):
		t.Fatal("queued guardian not resumed")
	}
	if gate.stopController(id) != second {
		t.Fatal("stop authority was not handed off after original Worker returned")
	}
	close(releaseSecond)
	waitWorkerReleased(t, gate, id)
	if gate.stopController(id) != nil {
		t.Fatal("stop authority was retained after worker completion")
	}
}
