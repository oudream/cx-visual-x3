package alerts

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"gogo/internal/events"
	"gogo/internal/program"
)

func TestClassifyRecognizesOnlyExplicitFailure(t *testing.T) {
	for _, tc := range []struct {
		event int
		msg string
		kind string
		severity string
	}{
		{program.EventMaxError, "连续失败", "max-error", "critical"},
		{program.EventMaxCount, "超过启动次数", "max-start", "critical"},
		{program.EventStart, "程序启动失败", "start-failed", "warning"},
		{program.EventStart, "程序启动成功", "", ""},
		{program.EventExit, "正常退出", "", ""},
		{program.EventHeartbeat, "", "", ""},
	} {
		got, yes := Classify(events.Record{ProgramID: 9007199254740995, Event: tc.event, Message: tc.msg, Time: 10})
		if yes != (tc.kind != "") {
			t.Fatalf("event %d: unexpected classification %v", tc.event, yes)
		}
		if yes && (got.Kind != tc.kind || got.Severity != tc.severity || got.ProgramID != 9007199254740995) {
			t.Fatalf("incorrect classification: %+v", got)
		}
	}
}

func TestHistoryNotHiddenByHeartbeatFlood(t *testing.T) {
	dir := t.TempDir()
	file := filepath.Join(dir, "program.event.list")
	var b strings.Builder
	b.WriteString("{\"programId\":\"9007199254740995\",\"pId\":10,\"event\":4,\"time\":10,\"msg\":\"limit\"}\n")
	for i:=0; i<500; i++ {
		b.WriteString("{\"programId\":\"1\",\"pId\":11,\"event\":2,\"time\":11,\"msg\":\"\"}\n")
	}
	b.WriteString("{\"programId\":\"12\",\"pId\":13,\"event\":0,\"time\":20,\"msg\":\"启动失败\"}\n")
	if err := os.WriteFile(file, []byte(b.String()), 0600); err != nil { t.Fatal(err) }
	items, err := ReadHistory(file, 10)
	if err != nil || len(items)!=2 {
		t.Fatalf("expected 2 failure records, got %+v, %v", items, err)
	}
	if items[0].Time != 20 || items[1].ProgramID != 9007199254740995 { t.Fatalf("unexpected order or ID %+v", items) }
}

func TestNoEventFileIsEmpty(t *testing.T) {
	items, err := ReadHistory(filepath.Join(t.TempDir(), "missing"), 20)
	if err != nil || len(items) != 0 || items == nil { t.Fatalf("expected empty, got %+v %v", items, err) }
}
