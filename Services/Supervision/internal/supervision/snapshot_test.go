package supervision

import "testing"

func TestSnapshotSortedAndDetached(t *testing.T) {
	original := []Program{{ID: 4}, {ID: 1, Running: true}}
	got := Snapshot(original)
	if len(got) != 2 || got[0].ID != 1 {
		t.Fatalf("unexpected order: %+v", got)
	}
	got[0].Path = "modified"
	if original[1].Path != "" {
		t.Fatal("snapshot shares caller data")
	}
}

func TestSummarize(t *testing.T) {
	o := Summarize([]Program{
		{Running: true, HeartbeatAtMs: 12, CPUPercent: 10.5, MemoryRSSBytes: 400},
		{Running: false, CPUPercent: 2, MemoryRSSBytes: 200},
	})
	if o.Total != 2 || o.Running != 1 || o.Stopped != 1 ||
		o.HeartbeatReporting != 1 || o.CPUPercent != 12.5 || o.MemoryRSSBytes != 600 {
		t.Fatalf("incorrect aggregate: %+v", o)
	}
}
