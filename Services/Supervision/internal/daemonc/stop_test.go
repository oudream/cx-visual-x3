package daemonc

import (
	"context"
	"io"
	"log"
	"os"
	"os/exec"
	"sync/atomic"
	"testing"
	"time"

	"gogo/internal/program"
)

func TestOwnedStopChildHelper(t *testing.T) {
	if os.Getenv("CX_SUPERVISION_OWNED_STOP_CHILD") != "1" {
		return
	}
	time.Sleep(15 * time.Second)
}

func startOwnedStopChild(t *testing.T) *exec.Cmd {
	t.Helper()
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd := exec.Command(exe, "-test.run=^TestOwnedStopChildHelper$")
	cmd.Env = append(os.Environ(), "CX_SUPERVISION_OWNED_STOP_CHILD=1")
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() {
		_ = cmd.Process.Kill()
		_ = cmd.Wait()
	})
	return cmd
}

func TestOwnedStopConfirmsOriginalChildWait(t *testing.T) {
	cmd := startOwnedStopChild(t)
	controller := NewStopController(42)
	controller.attach(cmd)
	waited := make(chan struct{})
	go func() {
		err := cmd.Wait()
		controller.finish(cmd, err)
		close(waited)
	}()
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	receipt := controller.RequestStop(ctx, cmd.Process.Pid)
	if receipt.State != StopConfirmed || !receipt.WaitCompleted ||
		receipt.PID != cmd.Process.Pid || receipt.ProgramID != 42 {
		t.Fatalf("stop must be confirmed by original Wait, got %+v", receipt)
	}
	select {
	case <-waited:
	case <-time.After(3*time.Second):
		t.Fatal("Worker did not finish original child Wait")
	}
	if r := controller.RequestStop(ctx, cmd.Process.Pid); r.State != StopRefused {
		t.Fatalf("reused exited PID must not be signaled: %+v", r)
	}
}

func TestOwnedStopRejectsStalePIDAndInvalidContext(t *testing.T) {
	cmd := startOwnedStopChild(t)
	controller := NewStopController(91)
	controller.attach(cmd)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	for _, stale := range []int{0, -1, cmd.Process.Pid + 1} {
		receipt := controller.RequestStop(ctx, stale)
		if receipt.State != StopRefused {
			t.Fatalf("mismatching PID %d unexpectedly accepted: %+v", stale, receipt)
		}
	}
	cancelled, stop := context.WithCancel(context.Background())
	stop()
	if r := controller.RequestStop(cancelled, cmd.Process.Pid); r.State != StopRefused {
		t.Fatalf("cancelled stop request unexpectedly accepted: %+v", r)
	}
	waitErr := cmd.Process.Kill()
	if waitErr != nil {
		t.Fatal(waitErr)
	}
	waitErr = cmd.Wait()
	if controlled := controller.finish(cmd, waitErr); controlled {
		t.Fatal("externally ended child must not count as owned controlled stop")
	}
}

func TestOwnedStopTimeoutNeverClaimsConfirmed(t *testing.T) {
	cmd := startOwnedStopChild(t)
	controller := NewStopController(77)
	controller.attach(cmd)
	// Deliberately do not call Wait until AFTER the receipt times out.
	ctx, cancel := context.WithTimeout(context.Background(), 50*time.Millisecond)
	defer cancel()
	receipt := controller.RequestStop(ctx, cmd.Process.Pid)
	if receipt.State != StopUnknown || receipt.WaitCompleted {
		t.Fatalf("without Worker Wait, stop cannot be confirmed: %+v", receipt)
	}
	err := cmd.Wait()
	if !controller.finish(cmd, err) {
		t.Fatal("stop request must still be attributed to original child")
	}
}

func TestRunProgramControlledStopDoesNotCountAsCrash(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	dir := t.TempDir()
	a := program.Program{
		ID:           710,
		Path:         exe,
		Dir:          dir,
		ArgsString:   "-test.run=^TestOwnedStopChildHelper$",
		EnvsString:   "CX_SUPERVISION_OWNED_STOP_CHILD=1",
		Enabled:      program.ENABLE,
		MaxError:     0,
		MaxCount:     10,
		MinExitTime:  60,
	}
	controller := NewStopController(a.ID)
	events := make(chan program.Event, 6)
	done := make(chan struct{})
	var lookups atomic.Int32
	ctx, cancel := context.WithTimeout(context.Background(), 8*time.Second)
	defer cancel()
	go func() {
		defer close(done)
		RunProgramContextWithStop(ctx, a, events,
			func(id int64) (program.Program, error) {
				if lookups.Add(1) > 1 {
					a.Enabled = program.DISABLE
				}
				return a, nil
			},
			log.New(io.Discard, "", 0), dir, nil, controller)
	}()
	var start program.Event
	select {
	case start = <-events:
	case <-ctx.Done():
		t.Fatal("managed test child did not report start")
	}
	if start.Event != program.EventStart || !start.Status.IsRun || start.Status.Pid <= 0 {
		t.Fatalf("expected real start event: %+v", start)
	}
	receipt := controller.RequestStop(ctx, start.Status.Pid)
	if receipt.State != StopConfirmed || !receipt.WaitCompleted {
		t.Fatalf("expected original Worker Wait confirmation: %+v", receipt)
	}
	select {
	case <-done:
	case <-ctx.Done():
		t.Fatal("guardian did not return after disabled configuration")
	}
	select {
	case exit := <-events:
		if exit.Event != program.EventExit || exit.Status.IsRun || exit.Status.ErrorCount != 0 {
			t.Fatalf("intentional stop was counted as crash: %+v", exit)
		}
	default:
		t.Fatal("original Wait completion was not projected as EventExit")
	}
	select {
	case unexpected := <-events:
		t.Fatalf("unexpected additional managed process/terminal event: %+v", unexpected)
	default:
	}
}


func TestOwnedStopFailedSignalNeverMarksControlledExit(t *testing.T) {
	// The child is already reaped, but the controller has deliberately not
	// yet observed its Wait result. Kill must fail rather than setting the
	// intentional-stop flag and concealing a real crash.
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd := exec.Command(exe, "-test.run=^TestOwnedStopFastExitHelper$")
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	controller := NewStopController(1001)
	controller.attach(cmd)
	if err := cmd.Wait(); err != nil {
		t.Fatal(err)
	}
	ctx, cancel := context.WithTimeout(context.Background(), time.Second)
	defer cancel()
	receipt := controller.RequestStop(ctx, cmd.Process.Pid)
	if receipt.State != StopFailed || receipt.WaitCompleted {
		t.Fatalf("Kill after Wait must fail, got %+v", receipt)
	}
	if controlled := controller.finish(cmd, nil); controlled {
		t.Fatal("a failed Kill must never mark the preceding process exit as controlled")
	}
	if controller.ActivePID() != 0 {
		t.Fatal("finished child was still exposed as active")
	}
}

func TestOwnedStopFastExitHelper(t *testing.T) {
	// A deliberately immediate child exit, for TestOwnedStopFailedSignal...
}

func TestOwnedStopActivePIDTracksOnlyAttachedChild(t *testing.T) {
	controller := NewStopController(1002)
	if controller.ActivePID() != 0 {
		t.Fatal("empty guardian unexpectedly exposed a PID")
	}
	cmd := startOwnedStopChild(t)
	controller.attach(cmd)
	if controller.ActivePID() != cmd.Process.Pid {
		t.Fatalf("wrong active child PID: %d", controller.ActivePID())
	}
	if err := cmd.Process.Kill(); err != nil {
		t.Fatal(err)
	}
	err := cmd.Wait()
	controller.finish(cmd, err)
	if controller.ActivePID() != 0 {
		t.Fatal("already reaped child must not appear as active")
	}
}
