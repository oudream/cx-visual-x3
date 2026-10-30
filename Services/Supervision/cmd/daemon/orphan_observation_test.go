package main

import (
	"os"
	"path/filepath"
	"testing"

	"gogo/internal/guardview"
)

func TestOrphanObservationReadOnlyProjection(t *testing.T) {
	dir := t.TempDir()
	const largeID int64 = 9007199254740995
	if err := writeOrphanObservation(dir, map[int64]string{
		largeID: "legacy executable running, generation unverified",
		87: "PID file corrupt",
	}); err != nil {
		t.Fatal(err)
	}
	report := guardview.Read(dir)
	if !report.Orphans.Available || len(report.Orphans.Items) != 2 {
		t.Fatalf("startup scan not visible to read-only Monitor: %+v", report.Orphans)
	}
	if report.Orphans.Items[1].ProgramID != "9007199254740995" ||
		report.Orphans.ObservedAt == "" {
		t.Fatalf("lost 64-bit ProgramId or scan time: %+v", report.Orphans)
	}
	// The absence of a terminal file is not a proven zero-latch state.
	if report.Terminal.Available {
		t.Fatal("missing terminal journal was incorrectly considered available")
	}
	if err := writeOrphanObservation(dir, map[int64]string{}); err != nil {
		t.Fatal(err)
	}
	reloaded := guardview.Read(dir)
	if !reloaded.Orphans.Available || len(reloaded.Orphans.Items) != 0 {
		t.Fatalf("a completed empty startup scan must be distinguishable from missing scan: %+v", reloaded)
	}
}

func TestOrphanObservationRejectsInvalidRecords(t *testing.T) {
	dir := t.TempDir()
	for _, records := range []map[int64]string{
		{0: "invalid ID"}, {-1: "invalid ID"}, {31: ""},
	} {
		if err := writeOrphanObservation(dir, records); err == nil {
			t.Fatalf("invalid scan result accepted: %+v", records)
		}
	}
	if _, err := os.Stat(filepath.Join(dir, "supervision", "daemon-orphan-observation.json")); !os.IsNotExist(err) {
		t.Fatalf("rejected records should not create an orphan observation: %v", err)
	}
}
