package daemonc

import (
	"context"
	"fmt"
	"io"
	"log"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"gogo/internal/program"
)

// A child process is only created from this test executable in an isolated
// temporary directory. No production programs, process IDs or DB are touched.
func TestManagedChildHelper(t *testing.T) {
	if os.Getenv("CX_SUPERVISION_TEST_CHILD") != "1" {
		return
	}
	fmt.Fprintln(os.Stdout, "supervision-stdout")
	fmt.Fprintln(os.Stderr, "supervision-stderr")
	os.Exit(0)
}

func TestStartProcRejectsEmptyPath(t *testing.T) {
	for _, args := range [][]string{nil, {}, {""}} {
		cmd, err := startProc(args, os.Environ(), t.TempDir(), "")
		if err == nil || cmd != nil {
			t.Fatalf("empty executable must be rejected: args=%q cmd=%v err=%v", args, cmd, err)
		}
	}
}

func TestStartProcRedirectsAndReleasesParentLogHandle(t *testing.T) {
	dir := t.TempDir()
	logPath := filepath.Join(dir, "managed-output.log")
	env := append(os.Environ(), "CX_SUPERVISION_TEST_CHILD=1")
	executable, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd, err := startProc([]string{executable, "-test.run=^TestManagedChildHelper$"}, env, dir, logPath)
	if err != nil {
		t.Fatalf("failed to start isolated child: %v", err)
	}
	if err := cmd.Wait(); err != nil {
		t.Fatalf("child failed: %v", err)
	}
	data, err := os.ReadFile(logPath)
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(data), "supervision-stdout") ||
		!strings.Contains(string(data), "supervision-stderr") {
		t.Fatalf("missing redirected child output: %q", data)
	}
	// On Windows, a leaked parent's open log descriptor can prevent deleting
	// the file. The child has already exited, so no handle should remain.
	if err := os.Remove(logPath); err != nil {
		t.Fatalf("parent appears to hold child's logfile: %v", err)
	}
}

func TestStartProcFailureClosesLogFile(t *testing.T) {
	dir := t.TempDir()
	logPath := filepath.Join(dir, "failed-start.log")
	cmd, err := startProc(
		[]string{filepath.Join(dir, "no-such-executable")},
		os.Environ(), dir, logPath,
	)
	if err == nil || cmd != nil {
		t.Fatalf("missing executable must fail: cmd=%v err=%v", cmd, err)
	}
	if err := os.Remove(logPath); err != nil {
		t.Fatalf("failed child start leaked its log descriptor: %v", err)
	}
}

func TestRunProgramCountsFailuresBeforeEvents(t *testing.T) {
	dir := t.TempDir()
	a := program.Program{
		ID: 4101,
		Path: filepath.Join(dir, "missing-managed-binary"),
		Dir: dir,
		Enabled: program.ENABLE,
		MaxError: 1,
		MaxCount: 50,
		StartWaitTime: 0,
	}
	events := make(chan program.Event, 8)
	find := func(id int64) (program.Program, error) {
		if id != a.ID {
			t.Fatalf("wrong program ID %d", id)
		}
		return a, nil
	}
	RunProgram(a, events, find, log.New(io.Discard, "", 0), dir)
	if got := len(events); got != 3 {
		t.Fatalf("expected 2 start failures and a max-error event; got %d", got)
	}
	for count := 1; count <= 2; count++ {
		event := <-events
		if event.Event != program.EventStart || event.Status.IsRun ||
			event.Status.Pid != 0 || event.Status.ErrorCount != count ||
			event.Status.StartFailCount != count || event.Message == "" {
			t.Fatalf("failure %d has inaccurate status: %+v", count, event)
		}
	}
	threshold := <-events
	if threshold.Event != program.EventMaxError || threshold.Status.ErrorCount != 2 {
		t.Fatalf("expected actual failure limit result, got %+v", threshold)
	}
}


func TestWaitForRestartIsCancellable(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	done := make(chan bool, 1)
	go func() { done <- waitForRestart(ctx, time.Hour) }()
	cancel()
	select {
	case completed := <-done:
		if completed {
			t.Fatal("canceled restart wait must not proceed to next start")
		}
	case <-time.After(2 * time.Second):
		t.Fatal("restart wait did not respond to cancellation")
	}
}

func TestRunProgramContextStopsDuringFailureBackoff(t *testing.T) {
	dir := t.TempDir()
	a := program.Program{
		ID: 4201,
		Path: filepath.Join(dir, "missing-executable"),
		Dir: dir,
		Enabled: program.ENABLE,
		MaxError: 100,
		MaxCount: 200,
		StartWaitTime: 3600000, // 1 hour; must be interruptible
	}
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	events := make(chan program.Event, 4)
	done := make(chan struct{})
	go func() {
		defer close(done)
		RunProgramContext(ctx, a, events, func(id int64) (program.Program, error) {
			return a, nil
		}, log.New(io.Discard, "", 0), dir)
	}()
	select {
	case first := <-events:
		if first.Event != program.EventStart || first.Status.ErrorCount != 1 {
			t.Fatalf("expected one failed start before backoff, got %+v", first)
		}
	case <-time.After(2 * time.Second):
		t.Fatal("missing isolated startup failure")
	}
	cancel()
	select {
	case <-done:
	case <-time.After(2 * time.Second):
		t.Fatal("worker did not stop promptly during long backoff")
	}
	if len(events) != 0 {
		t.Fatalf("worker retried after cancellation: pending=%d", len(events))
	}
}

func TestEmitProgramEventDoesNotBlockAfterCancel(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if emitProgramEvent(ctx, make(chan program.Event), program.Event{Event: program.EventStart}) {
		t.Fatal("canceled worker must not send into an unconsumed channel")
	}
}


// Exercise the real RunProgram -> os/exec -> Wait -> Event path, using only
// the current test binary. A short-lived process intentionally fails the
// configured minimum runtime, then the original MaxError policy stops retries.
func TestRunProgramContextWithIsolatedChild(t *testing.T) {
	dir := t.TempDir()
	executable, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	a := program.Program{
		ID: 700, // below DefaultProgramIdUser: do not append the legacy -i flag
		Path: executable,
		Dir: dir,
		ArgsString: "-test.run=^TestManagedChildHelper$",
		EnvsString: "CX_SUPERVISION_TEST_CHILD=1",
		Enabled: program.ENABLE,
		MaxError: 1,
		MaxCount: 20,
		MinExitTime: 60,
		StartWaitTime: 0,
	}
	ctx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
	defer cancel()
	events := make(chan program.Event, 10)
	done := make(chan struct{})
	go func() {
		defer close(done)
		RunProgramContext(ctx, a, events, func(id int64) (program.Program, error) {
			return a, nil
		}, log.New(io.Discard, "", 0), dir)
	}()
	select {
	case <-done:
	case <-ctx.Done():
		t.Fatalf("supervisor did not finish bounded isolated restarts: %v", ctx.Err())
	}
	if len(events) != 5 {
		t.Fatalf("want 2 starts + 2 exits + max-error, got %d", len(events))
	}
	for index := 1; index <= 2; index++ {
		start := <-events
		exit := <-events
		if start.Event != program.EventStart || !start.Status.IsRun || start.Status.Pid <= 0 ||
			start.Status.StartCount != index {
			t.Fatalf("unexpected start %d: %+v", index, start)
		}
		if exit.Event != program.EventExit || exit.Status.IsRun || exit.Status.Pid != start.Status.Pid ||
			exit.Status.ErrorCount != index {
			t.Fatalf("unexpected exit %d: %+v", index, exit)
		}
	}
	limit := <-events
	if limit.Event != program.EventMaxError || limit.Status.ErrorCount != 2 {
		t.Fatalf("error limit event must report actual failed runs, got %+v", limit)
	}
}
