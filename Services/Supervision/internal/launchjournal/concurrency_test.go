package launchjournal

import (
	"os"
	"sync"
	"testing"
)

func TestConcurrentStartedAndFinishedSameJournal(t *testing.T) {
	wd := t.TempDir()
	j := New(wd, 47)
	if err := j.Begin(47, os.Args[0]); err != nil {
		t.Fatal(err)
	}
	var wg sync.WaitGroup
	for i := 0; i < 12; i++ {
		wg.Add(1)
		go func(i int) {
			defer wg.Done()
			if i%2 == 0 {
				_ = j.Started(os.Getpid())
			} else {
				_ = j.Finished()
			}
		}(i)
	}
	wg.Wait()
	// Exactly one serialized outcome is allowed: cleared, or a valid,
	// quarantined record. No success can fabricate a different attempt.
	rows, err := Scan(wd)
	if err != nil {
		t.Fatal(err)
	}
	if len(rows) > 1 {
		t.Fatalf("unexpected evidence: %v", rows)
	}
	if _, err := os.Stat(recordPath(wd, 47)); err == nil {
		if _, err := readRecord(recordPath(wd, 47)); err != nil {
			t.Fatal(err)
		}
	}
}

func TestStartedAndFinishedRejectSameAttemptMetadataReplacement(t *testing.T) {
	for _, change := range []func(*Record){
		func(r *Record) { r.Path = "/different/executable" },
		func(r *Record) { r.IntendedAt = "2026-10-29T00:00:00Z" },
	} {
		wd := t.TempDir()
		j := New(wd, 48)
		if err := j.Begin(48, os.Args[0]); err != nil {
			t.Fatal(err)
		}
		row, err := readRecord(recordPath(wd, 48))
		if err != nil {
			t.Fatal(err)
		}
		change(&row)
		if err := writeRecord(recordPath(wd, 48), &row); err != nil {
			t.Fatal(err)
		}
		if err := j.Started(os.Getpid()); err == nil {
			t.Fatal("modified intent accepted")
		}
		if err := j.Finished(); err == nil {
			t.Fatal("modified intent deleted")
		}
	}
}
