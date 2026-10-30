package daemonc

import (
	"fmt"
	"io"
	"log"
	"os"
	"path/filepath"
	"strings"
	"testing"

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
	cmd, err := startProc([]string{os.Args[0], "-test.run=^TestManagedChildHelper$"}, env, dir, logPath)
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
