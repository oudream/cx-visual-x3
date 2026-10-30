package stopjournal

import (
	"os"
	"strings"
	"sync"
	"testing"
)

func TestDurableStopOperationSurvivesReloadAndLateWait(t *testing.T) {
	dir := t.TempDir()
	const id int64 = 9007199254740995
	start, err := NewRequest(id, 419, "legacy-restart")
	if err != nil { t.Fatal(err) }
	if err := Append(dir, start); err != nil { t.Fatal(err) }
	first := Read(dir)
	if !first.Available || len(first.Items) != 1 || first.Items[0].State != "requested" ||
		first.Items[0].ProgramID != "9007199254740995" {
		t.Fatalf("crash after request must remain unresolved: %+v", first)
	}
	if err := RecordReceipt(dir, start, "unknown", false, "timed out", ""); err != nil { t.Fatal(err) }
	if err := RecordReceipt(dir, start, "wait-confirmed", true, "", "signal: killed"); err != nil { t.Fatal(err) }
	// The Worker may report late Wait completion before the request goroutine
	// returns and writes unknown. Reconciliation must be monotonic.
	if err := RecordReceipt(dir, start, "unknown", false, "raced timeout", ""); err != nil { t.Fatal(err) }
	history := Read(dir)
	if !history.Available || len(history.Items) != 1 {
		t.Fatalf("event stream did not deduplicate operation: %+v", history)
	}
	result := history.Items[0]
	if result.OperationID != start.OperationID || result.State != "wait-confirmed" ||
		!result.WaitCompleted || result.ProgramID != "9007199254740995" {
		t.Fatalf("late UNKNOWN downgraded verified Wait: %+v", result)
	}
}

func TestJournalRejectsMissingCorruptAndCrossIdentity(t *testing.T) {
	dir := t.TempDir()
	if got := Read(dir); got.Available || got.Error == "" {
		t.Fatal("missing audit file is unknown, not a healthy empty history")
	}
	initial, err := NewRequest(17, 123, "shutdown")
	if err != nil { t.Fatal(err) }
	if err := Append(dir, initial); err != nil { t.Fatal(err) }
	foreign := initial
	foreign.ProgramID = "18"
	foreign.State = "unknown"
	if err := Append(dir, foreign); err != nil { t.Fatal(err) }
	if history := Read(dir); history.Available || !strings.Contains(history.Error, "identity") {
		t.Fatalf("same operation changed ProgramId yet accepted: %+v", history)
	}
}

func TestJournalPreventsUnsafeFilesAndPartialRecords(t *testing.T) {
	dir := t.TempDir()
	record, err := NewRequest(17, 123, "shutdown")
	if err != nil { t.Fatal(err) }
	if err := os.MkdirAll(Path(dir), 0700); err != nil { t.Fatal(err) }
	if err := Append(dir, record); err == nil {
		t.Fatal("directory journal should refuse stop before Kill")
	}
	dir = t.TempDir()
	if err := os.MkdirAll(dir+"/supervision", 0700); err != nil { t.Fatal(err) }
	if err := os.WriteFile(Path(dir), []byte("{partial"), 0600); err != nil { t.Fatal(err) }
	if history := Read(dir); history.Available || history.Error == "" {
		t.Fatalf("broken journal was considered valid: %+v", history)
	}
}

func TestConcurrentStopReceiptAppendRetainsAllOperations(t *testing.T) {
	dir := t.TempDir()
	var wg sync.WaitGroup
	errs := make(chan error, 24)
	for i := 0; i < 24; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			row, err := NewRequest(int64(i+1), i+100, "config-change")
			if err == nil { err = Append(dir, row) }
			if err == nil { err = RecordReceipt(dir, row, "signal-failed", false, "test", "") }
			if err != nil { errs <- err }
		}(i)
	}
	wg.Wait()
	close(errs)
	for err := range errs { t.Fatal(err) }
	history := Read(dir)
	if !history.Available || len(history.Items) != 24 {
		t.Fatalf("lost some concurrent stop operations: count=%d error=%s", len(history.Items), history.Error)
	}
	for _, row := range history.Items {
		if row.State != "signal-failed" { t.Fatalf("partial result: %+v", row) }
	}
}

func TestJournalRejectsInvalidWaitAuthority(t *testing.T) {
	dir := t.TempDir()
	row, err := NewRequest(3, 100, "shutdown")
	if err != nil { t.Fatal(err) }
	row.State, row.WaitCompleted = "wait-confirmed", false
	if err := Append(dir, row); err == nil {
		t.Fatal("Wait confirmation cannot be claimed without WaitCompleted")
	}
	row.State, row.WaitCompleted = "unknown", true
	if err := Append(dir, row); err == nil {
		t.Fatal("UNKNOWN must not carry confirmed Wait")
	}
}

func TestUnsafePriorHistoryRefusesNextStopBeforeSignal(t *testing.T) {
	for _, broken := range []string{"", "{incomplete", "{\"operationId\":\"bad\"}\n"} {
		dir := t.TempDir()
		if err := os.MkdirAll(dir+"/supervision", 0700); err != nil { t.Fatal(err) }
		if err := os.WriteFile(Path(dir), []byte(broken), 0600); err != nil { t.Fatal(err) }
		request, err := NewRequest(420, 256, "config-change")
		if err != nil { t.Fatal(err) }
		if err := Append(dir, request); err == nil {
			t.Fatal("unsafe prior journal accepted a new stop operation")
		}
	}
}
