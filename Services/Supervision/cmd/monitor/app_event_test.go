package main

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"gogo/internal/alerts"
	"gogo/internal/events"
	"gogo/internal/program"
)

func TestElapsedRunSeconds(t *testing.T) {
	for _, tc := range []struct{ now, start, want int64 }{
		{1700000010000, 1700000000000, 10},
		{1700000000500, 1700000000000, 0},
		{1700000000000, 1700000000000, 0},
		{1700000000000, 1700000000500, 0},
		{1700000000000, 0, 0},
	} {
		got := elapsedRunSeconds(tc.now, tc.start)
		if got != tc.want {
			t.Fatalf("now=%d start=%d: want %d, got %d", tc.now, tc.start, tc.want, got)
		}
	}
}


// The Daemon sends raw OS errors, while Monitor persists normalized messages.
// Test the real Daemon->Monitor->historical alert semantic boundary rather
// than searching for translated error messages inside the Daemon itself.
func TestDaemonEventFormattingAndHistoricalClassification(t *testing.T) {
	const nowMs int64 = 1700000010000
	const startMs int64 = 1700000000000
	tests := []struct {
		name      string
		event     program.Event
		wantText  string
		forbidText string
		alertKind string
	}{
		{
			name: "successful startup",
			event: program.Event{Event: program.EventStart,
				Status: program.Status{Id: 12, Pid: 42, IsRun: true, StartCount: 1}},
			wantText: "启动成功",
			forbidText: "启动失败",
		},
		{
			name: "failed startup with raw English OS error",
			event: program.Event{Event: program.EventStart,
				Status: program.Status{Id: 12, StartCount: 1, ErrorCount: 1},
				Message: "exec: file not found"},
			wantText: "启动失败",
			forbidText: "启动成功",
			alertKind: "start-failed",
		},
		{
			name: "failed startup with nil command",
			event: program.Event{Event: program.EventStart,
				Status: program.Status{Id: 12, StartCount: 1, ErrorCount: 1},
				Message: "cmd is nil"},
			wantText: "启动失败",
			alertKind: "start-failed",
		},
		{
			name: "short runtime abnormal exit",
			event: program.Event{Event: program.EventExit,
				Status: program.Status{Id: 12, Pid: 42, ErrorCount: 1, LastRunTime: startMs},
				Message: "exit status 2"},
			wantText: "异常退出（运行时间过短），运行时长：10秒",
		},
		{
			name: "long runtime normal exit",
			event: program.Event{Event: program.EventExit,
				Status: program.Status{Id: 12, Pid: 42, ErrorCount: 0, LastRunTime: startMs},
				Message: "<nil>"},
			wantText: "退出，运行时长：10秒",
			forbidText: "运行时间过短",
		},
		{
			name: "failed restart threshold",
			event: program.Event{Event: program.EventMaxError,
				Status: program.Status{Id: 12, ErrorCount: 9}},
			wantText: "失败次数太多",
			alertKind: "max-error",
		},
		{
			name: "startup count threshold",
			event: program.Event{Event: program.EventMaxCount,
				Status: program.Status{Id: 12, StartCount: 10}},
			wantText: "启动次数太多",
			alertKind: "max-start",
		},
	}
	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			msg := formatProgramDaemonEvent(tc.event, "inspection.exe", 10, 9, nowMs)
			if !strings.Contains(msg, tc.wantText) {
				t.Fatalf("expected %q in formatted event: %q", tc.wantText, msg)
			}
			if tc.forbidText != "" && strings.Contains(msg, tc.forbidText) {
				t.Fatalf("unexpected %q in event: %q", tc.forbidText, msg)
			}
			item, isFailure := alerts.Classify(events.Record{
				ProgramID: tc.event.Status.Id,
				PID: tc.event.Status.Pid,
				Event: tc.event.Event,
				Time: nowMs,
				Message: msg,
			})
			if isFailure != (tc.alertKind != "") {
				t.Fatalf("unexpected historical failure classification: %q %v", msg, isFailure)
			}
			if isFailure && item.Kind != tc.alertKind {
				t.Fatalf("expected failure kind %q, got %q", tc.alertKind, item.Kind)
			}
		})
	}
}


func TestDaemonFailureSerializedHistory(t *testing.T) {
	const programID int64 = 9007199254740995
	const nowMs int64 = 1700000010000
	records := []program.Event{
		{
			Event: program.EventStart,
			Status: program.Status{Id: programID, Pid: 42, IsRun: true, StartCount: 1},
		},
		{
			Event: program.EventStart,
			Status: program.Status{Id: programID, Pid: 0, IsRun: false, StartCount: 2, ErrorCount: 1},
			Message: "exec: no such file or directory",
		},
		{
			Event: program.EventExit,
			Status: program.Status{Id: programID, Pid: 42, IsRun: false, StartCount: 3, ErrorCount: 0, LastRunTime: nowMs - 5000},
			Message: "<nil>",
		},
	}

	// Serialize the actual Monitor record schema, then read it through
	// the same bounded historical alert reader used by /alerts/history.
	path := filepath.Join(t.TempDir(), "program.event.list")
	var journal strings.Builder
	for i, event := range records {
		normalized := formatProgramDaemonEvent(event, "inspection.exe", 10, 9, nowMs)
		encoded, err := json.Marshal(ProgramEventRecord{
			ProgramId: event.Status.Id,
			PId: event.Status.Pid,
			Event: event.Event,
			Time: nowMs + int64(i),
			Msg: normalized,
		})
		if err != nil {
			t.Fatal(err)
		}
		journal.Write(encoded)
		journal.WriteByte('\n')
	}
	if err := os.WriteFile(path, []byte(journal.String()), 0600); err != nil {
		t.Fatal(err)
	}
	incidents, err := alerts.ReadHistory(path, 10)
	if err != nil {
		t.Fatal(err)
	}
	if len(incidents) != 1 || incidents[0].Kind != "start-failed" || incidents[0].ProgramID != programID {
		t.Fatalf("expected exactly the failed start after persistence, got %+v", incidents)
	}
}
