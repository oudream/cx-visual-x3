package stopjournal

import (
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"sync"
	"testing"
	"time"
)

// This launches only this test executable, never a configured managed program.
func TestConcurrentProcessesAndReader(t *testing.T) {
	if dir := os.Getenv("SUPERVISION_STOP_HELPER"); dir != "" {
		for i := 0; i < 4; i++ {
			row, err := NewRequest(9007199254740995, os.Getpid(), "test-child")
			if err == nil {
				err = Append(dir, row)
			}
			if err == nil {
				err = RecordReceipt(dir, row, "wait-confirmed", true, "", "")
			}
			if err != nil {
				t.Fatal(err)
			}
		}
		return
	}
	dir := t.TempDir()
	seed, _ := NewRequest(1, os.Getpid(), "seed")
	if err := Append(dir, seed); err != nil {
		t.Fatal(err)
	}
	var wg sync.WaitGroup
	errs := make(chan error, 6)
	for i := 0; i < 6; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			cmd := exec.Command(os.Args[0], "-test.run=^TestConcurrentProcessesAndReader$", "-test.timeout=30s")
			cmd.Env = append(os.Environ(), "SUPERVISION_STOP_HELPER="+dir)
			if out, err := cmd.CombinedOutput(); err != nil {
				errs <- fmt.Errorf("child: %w: %s", err, out)
			}
		}()
	}
	done := make(chan struct{})
	go func() { wg.Wait(); close(done) }()
	for {
		if h := Read(dir); !h.Available {
			t.Fatalf("reader observed a torn append: %s", h.Error)
		}
		select {
		case <-done:
			goto finished
		default:
			time.Sleep(time.Millisecond)
		}
	}
finished:
	close(errs)
	for err := range errs {
		t.Error(err)
	}
	history := Read(dir)
	if !history.Available || len(history.Items) != 25 {
		t.Fatalf("lost cross-process operations: %+v", history)
	}
	for _, row := range history.Items {
		if row.Action != "seed" && (row.State != "wait-confirmed" || row.ProgramID != "9007199254740995") {
			t.Fatalf("lost child receipt or int64 identity: %+v", row)
		}
	}
}

func TestReceiptWithoutRequestAndOldIdentityAreRejected(t *testing.T) {
	dir := t.TempDir()
	row, _ := NewRequest(17, 101, "shutdown")
	if err := RecordReceipt(dir, row, "unknown", false, "", ""); err == nil {
		t.Fatal("orphan receipt accepted")
	}
	if err := Append(dir, row); err != nil {
		t.Fatal(err)
	}
	if err := Append(dir, row); err == nil {
		t.Fatal("duplicate request accepted")
	}
	// Populate beyond the public UI window without quadratic setup writes.
	f, err := os.OpenFile(Path(dir), os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		t.Fatal(err)
	}
	for i := 0; i < MaxHistory+5; i++ {
		next, _ := NewRequest(int64(i+100), i+100, "older-window")
		data, _ := json.Marshal(next)
		if _, err := f.Write(append(data, '\n')); err != nil {
			t.Fatal(err)
		}
	}
	if err := f.Close(); err != nil {
		t.Fatal(err)
	}
	row.ProgramID = "18"
	if err := RecordReceipt(dir, row, "unknown", false, "", ""); err == nil {
		t.Fatal("old identity beyond UI limit was not checked")
	}
	if !Read(dir).Available {
		t.Fatal("rejection corrupted valid history")
	}
}

func TestRedirectedJournalParentIsRejected(t *testing.T) {
	dir, outside := t.TempDir(), t.TempDir()
	if err := os.Symlink(outside, filepath.Join(dir, "supervision")); err != nil {
		t.Skipf("symlink unavailable: %v", err)
	}
	row, _ := NewRequest(2, 105, "shutdown")
	if err := Append(dir, row); err == nil {
		t.Fatal("redirected parent accepted")
	}
	if Read(dir).Available {
		t.Fatal("redirected read accepted")
	}
	if entries, err := os.ReadDir(outside); err != nil || len(entries) != 0 {
		t.Fatal("wrote outside work directory")
	}
}
