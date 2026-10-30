package main

import (
	"context"
	"errors"
	"fmt"
	"io"
	"log"
	"os"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"gogo/internal/daemonc"
	"gogo/internal/program"
)

func waitWorkerReleased(t *testing.T, gate *programWorkerGate, id int64) {
	t.Helper()
	deadline := time.Now().Add(3 * time.Second)
	for time.Now().Before(deadline) {
		if !gate.has(id) {
			return
		}
		time.Sleep(time.Millisecond)
	}
	t.Fatalf("worker reservation %d not released", id)
}

func TestProgramWorkerGateSingleOwnerDuringConcurrentStart(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 9007199254740995
	const n = 128
	block := make(chan struct{})
	var accepted atomic.Int32
	var wg sync.WaitGroup
	for i := 0; i < n; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			if gate.start(context.Background(), id, func() { <-block }) {
				accepted.Add(1)
			}
		}()
	}
	wg.Wait()
	if accepted.Load() != 1 {
		t.Fatalf("same Program ID must have one active guardian, got %d", accepted.Load())
	}
	if !gate.has(id) {
		t.Fatal("worker must remain reserved even with PID not yet reported")
	}
	if gate.start(context.Background(), id, func() { t.Error("duplicate worker ran") }) {
		t.Fatal("same Program ID was accepted during active guardian")
	}
	close(block)
	waitWorkerReleased(t, gate, id)
}

func TestProgramWorkerGateReleasesOnlyAfterWorkerReturn(t *testing.T) {
	gate := newProgramWorkerGate()
	block := make(chan struct{})
	if !gate.start(context.Background(), 41, func() { <-block }) {
		t.Fatal("initial worker not admitted")
	}
	// The process may have exited or be in its restart backoff but the
	// existing guardian must retain its ID reservation.
	if gate.start(context.Background(), 41, func() { t.Error("second worker ran") }) {
		t.Fatal("recovery path duplicated a still-active guardian")
	}
	close(block)
	waitWorkerReleased(t, gate, 41)
	restarted := make(chan struct{})
	if !gate.start(context.Background(), 41, func() { close(restarted) }) {
		t.Fatal("guard must allow a new worker after the old worker returns")
	}
	select {
	case <-restarted:
	case <-time.After(3 * time.Second):
		t.Fatal("replacement guardian did not run")
	}
	waitWorkerReleased(t, gate, 41)
}

func TestProgramWorkerGateScopesIDsAndRejectsCancelledStarts(t *testing.T) {
	gate := newProgramWorkerGate()
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if gate.start(ctx, 11, func() { t.Error("cancelled worker ran") }) {
		t.Fatal("cancelled start must be denied")
	}
	if gate.start(context.Background(), 0, func() {}) {
		t.Fatal("invalid program ID must be denied")
	}
	if gate.start(context.Background(), 12, nil) {
		t.Fatal("nil worker must be denied")
	}
	first := make(chan struct{})
	second := make(chan struct{})
	if !gate.start(context.Background(), 11, func() { <-first }) ||
		!gate.start(context.Background(), 12, func() { <-second }) {
		t.Fatal("independent program IDs must be managed concurrently")
	}
	close(first)
	close(second)
	waitWorkerReleased(t, gate, 11)
	waitWorkerReleased(t, gate, 12)
}

func TestRecoveryProgramsSkipsReservedGuardian(t *testing.T) {
	// A PID=0 occurs before EventStart and during restart backoff. Recovery
	// must not wait out FirstWaitTime or start another guardian in this state.
	const id int64 = 517
	previousPrograms := getPrograms()
	previousWorkers := _programWorkers
	previousPIDs := _runProgramIds
	_programWorkers = newProgramWorkerGate()
	_runProgramIds = map[int64]int{id: 0}
	_programsMu.Lock()
	_programs = []program.Program{{ID: id, Enabled: program.ENABLE, FirstWaitTime: 3600}}
	_programsMu.Unlock()
	t.Cleanup(func() {
		_programsMu.Lock()
		_programs = previousPrograms
		_programsMu.Unlock()
		_programWorkers = previousWorkers
		_runProgramIds = previousPIDs
	})

	held := make(chan struct{})
	if !_programWorkers.start(context.Background(), id, func() { <-held }) {
		t.Fatal("could not reserve the test guardian")
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	done := make(chan struct{})
	go func() {
		recoveryPrograms(ctx, make(chan program.Event))
		close(done)
	}()
	select {
	case <-done:
		if ctx.Err() != nil {
			t.Fatal("recovery waited for initial delay despite active guardian")
		}
	case <-ctx.Done():
		t.Fatal("recovery blocked during another worker's startup/backoff")
	}
	close(held)
	waitWorkerReleased(t, _programWorkers, id)
}


func TestProgramWorkerGateTerminalLatchSurvivesRecovery(t *testing.T) {
	for _, reason := range []int{program.EventMaxError, program.EventMaxCount} {
		t.Run(fmt.Sprint(reason), func(t *testing.T) {
			gate := newProgramWorkerGate()
			const id int64 = 918
			firstStarted := make(chan struct{})
			release := make(chan struct{})
			if !gate.start(context.Background(), id, func() {
				close(firstStarted)
				<-release
			}) {
				t.Fatal("guardian not started")
			}
			<-firstStarted
			gate.markTerminal(program.Event{Event: reason, Status: program.Status{Id: id}})
			if got, ok := gate.blocked(id); !ok || got != reason {
				t.Fatalf("terminal lock not recorded: reason=%d locked=%t", got, ok)
			}
			if gate.start(context.Background(), id, func() { t.Error("automatic recovery ran") }) {
				t.Fatal("recovery bypassed a terminal limit")
			}
			if started, queued := gate.request(context.Background(), id, func() {
				t.Error("configuration request bypassed latch")
			}); started || queued {
				t.Fatal("unauthenticated re-enable must not reset terminal latch")
			}
			close(release)
			waitWorkerReleased(t, gate, id)
			if gate.start(context.Background(), id, func() { t.Error("later recovery ran") }) {
				t.Fatal("later periodic recovery bypassed terminal latch")
			}
		})
	}
}

func TestProgramWorkerGateQueuesEnabledIntentUntilOldWorkerReturns(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 712
	releaseOld := make(chan struct{})
	oldStarted := make(chan struct{})
	nextStarted := make(chan struct{})
	releaseNext := make(chan struct{})
	var simultaneous atomic.Int32
	var maximum atomic.Int32
	enter := func() {
		count := simultaneous.Add(1)
		if count > maximum.Load() {
			maximum.Store(count)
		}
	}
	leave := func() { simultaneous.Add(-1) }
	if !gate.start(context.Background(), id, func() {
		enter()
		defer leave()
		close(oldStarted)
		<-releaseOld
	}) {
		t.Fatal("old guardian not started")
	}
	<-oldStarted
	started, queued := gate.request(context.Background(), id, func() {
		enter()
		defer leave()
		close(nextStarted)
		<-releaseNext
	})
	if started || !queued {
		t.Fatal("explicit enable must queue behind existing guardian")
	}
	if gate.start(context.Background(), id, func() { t.Error("periodic recovery duplicated worker") }) {
		t.Fatal("periodic recovery may not overtake pending explicit enable")
	}
	select {
	case <-nextStarted:
		t.Fatal("next guardian started before previous returned")
	default:
	}
	close(releaseOld)
	select {
	case <-nextStarted:
	case <-time.After(3*time.Second):
		t.Fatal("queued enabled intent was not resumed after old worker returned")
	}
	if !gate.has(id) || maximum.Load() != 1 {
		t.Fatalf("handoff lost reservation or overlapped: active=%t max=%d", gate.has(id), maximum.Load())
	}
	close(releaseNext)
	waitWorkerReleased(t, gate, id)
}

func TestProgramWorkerGateSuppressesQueuedIntentAfterLimit(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 733
	release := make(chan struct{})
	if !gate.start(context.Background(), id, func() { <-release }) {
		t.Fatal("unable to start")
	}
	var next atomic.Bool
	if started, queued := gate.request(context.Background(), id, func() { next.Store(true) }); started || !queued {
		t.Fatal("expected pending enabled request")
	}
	gate.markTerminal(program.Event{Event: program.EventMaxError, Status: program.Status{Id: id}})
	close(release)
	waitWorkerReleased(t, gate, id)
	if next.Load() {
		t.Fatal("terminal limit should discard queued restart")
	}
	gate.clearPending(id)
	if _, blocked := gate.blocked(id); !blocked {
		t.Fatal("clearing pending configuration must not clear terminal latch")
	}
}

func TestProgramWorkerGateRejectsTerminalNoise(t *testing.T) {
	gate := newProgramWorkerGate()
	gate.markTerminal(program.Event{Event: program.EventExit, Status: program.Status{Id: 23}})
	gate.markTerminal(program.Event{Event: program.EventMaxError, Status: program.Status{Id: 0}})
	if _, found := gate.blocked(23); found {
		t.Fatal("normal exit is not a terminal lock")
	}
	if _, found := gate.blocked(0); found {
		t.Fatal("invalid program ID cannot be terminally locked")
	}
}


func TestRecoveryProgramsSkipsDisabledAndTerminalLatch(t *testing.T) {
	oldPrograms := getPrograms()
	oldWorkers := _programWorkers
	oldPIDs := _runProgramIds
	_programWorkers = newProgramWorkerGate()
	_runProgramIds = make(map[int64]int)
	_programsMu.Lock()
	_programs = []program.Program{
		{ID: 810, Enabled: program.DISABLE, FirstWaitTime: 3600},
		{ID: 811, Enabled: program.ENABLE, FirstWaitTime: 3600},
	}
	_programsMu.Unlock()
	t.Cleanup(func() {
		_programsMu.Lock()
		_programs = oldPrograms
		_programsMu.Unlock()
		_programWorkers = oldWorkers
		_runProgramIds = oldPIDs
	})
	_programWorkers.markTerminal(program.Event{
		Event: program.EventMaxError, Status: program.Status{Id: 811},
	})
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	done := make(chan struct{})
	go func() {
		recoveryPrograms(ctx, make(chan program.Event))
		close(done)
	}()
	select {
	case <-done:
		if ctx.Err() != nil {
			t.Fatal("disabled or terminally locked program caused an unnecessary wait")
		}
	case <-ctx.Done():
		t.Fatal("recovery waited despite disabled/terminally locked configuration")
	}
	if _programWorkers.has(810) || _programWorkers.has(811) {
		t.Fatal("recovery started a disabled or terminally locked guardian")
	}
}


func TestProgramWorkerGateStopControllerHandoff(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 902
	oldController := daemonc.NewStopController(id)
	newController := daemonc.NewStopController(id)
	oldRelease := make(chan struct{})
	newRelease := make(chan struct{})
	newEntered := make(chan struct{})
	if !gate.startWithStop(context.Background(), id, func() {
		<-oldRelease
	}, oldController) {
		t.Fatal("old guardian not admitted")
	}
	if gate.stopController(id) != oldController {
		t.Fatal("original worker controller was not registered")
	}
	if started, queued := gate.requestWithStop(context.Background(), id, func() {
		close(newEntered)
		<-newRelease
	}, newController); started || !queued {
		t.Fatal("new guardian must queue behind old generation")
	}
	if gate.stopController(id) != oldController {
		t.Fatal("queued worker prematurely replaced original process owner")
	}
	close(oldRelease)
	select {
	case <-newEntered:
	case <-time.After(3 * time.Second):
		t.Fatal("new generation was not admitted after the previous return")
	}
	if gate.stopController(id) != newController {
		t.Fatal("stop control did not hand off to the new worker generation")
	}
	close(newRelease)
	waitWorkerReleased(t, gate, id)
	if gate.stopController(id) != nil {
		t.Fatal("finished worker retained stop authority")
	}
}


func TestOwnedChildrenSleepHelper(t *testing.T) {
	if os.Getenv("CX_SUPERVISION_UNCONSUMED_START_CHILD") != "1" {
		return
	}
	time.Sleep(15 * time.Second)
}

func TestWorkerOwnedChildrenVisibleBeforeStartEventConsumption(t *testing.T) {
	// The original Daemon _runProgramIds map is not populated until the main
	// event loop reads EventStart. With an unbuffered, unread event channel,
	// the guardian has started a real child but the old map would still be 0.
	// Shutdown must discover it from its owned StopController.
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	dir := t.TempDir()
	const programID int64 = 126
	a := program.Program{
		ID: programID,
		Path: exe,
		Dir: dir,
		ArgsString: "-test.run=^TestOwnedChildrenSleepHelper$",
		EnvsString: "CX_SUPERVISION_UNCONSUMED_START_CHILD=1",
		Enabled: program.ENABLE,
		MaxError: 10,
		MaxCount: 10,
		MinExitTime: 60,
	}
	controller := daemonc.NewStopController(programID)
	gate := newProgramWorkerGate()
	ctx, cancel := context.WithTimeout(context.Background(), 6*time.Second)
	defer cancel()
	events := make(chan program.Event) // intentionally no receiver
	if !gate.startWithStop(ctx, programID, func() {
		daemonc.RunProgramContextWithStop(ctx, a, events,
			func(id int64) (program.Program, error) { return a, nil },
			log.New(io.Discard, "", 0), dir, nil, controller)
	}, controller) {
		t.Fatal("guardian not admitted")
	}
	var ownedPID int
	deadline := time.After(4*time.Second)
	for ownedPID == 0 {
		select {
		case <-deadline:
			cancel()
			t.Fatal("real child was not attached before EventStart consumption")
		default:
		}
		ownedPID = gate.ownedChildren()[programID]
		if ownedPID == 0 {
			time.Sleep(5 * time.Millisecond)
		}
	}
	if controller.ActivePID() != ownedPID {
		t.Fatalf("shutdown snapshot PID and original child differ: %d vs %d",
			ownedPID, controller.ActivePID())
	}
	// Cancel wakes the blocked EventStart emission; the existing Worker
	// owns Kill + Wait on this early-cancel path.
	cancel()
	waitWorkerReleased(t, gate, programID)
	if _, present := gate.ownedChildren()[programID]; present {
		t.Fatal("exited guardian still exposes a stoppable child")
	}
}


func TestLaunchEvidenceStorageFaultBlocksEveryAdmission(t *testing.T) {
	gate := newProgramWorkerGate()
	gate.failStorage(errors.New("journal fsync failed"))
	if gate.persistenceError() == nil { t.Fatal("storage failure was not latched") }
	if gate.start(context.Background(), 501, func(){t.Error("worker started")}) {
		t.Fatal("persistent evidence failure allowed new worker")
	}
	if started,queued:=gate.request(context.Background(),501,func(){t.Error("queued after journal failure")});started||queued {
		t.Fatal("evidence failure allowed queued replacement")
	}
}
