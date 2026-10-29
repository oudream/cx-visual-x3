package events

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestReadRecent(t *testing.T) {
	path := filepath.Join(t.TempDir(), "program.event.list")
	content := `{"programId":"9007199254740995","pId":20,"event":2,"time":100,"msg":"first"}` + "\n" +
		`{"programId":"3","pId":21,"event":5,"time":300,"msg":"last"}` + "\n" +
		"invalid line\n" +
		`{"programId":"4","pId":22,"event":3,"time":200,"msg":"middle"}` + "\n"
	if err := os.WriteFile(path, []byte(content), 0600); err != nil {
		t.Fatal(err)
	}
	records, err := ReadRecent(path, 2)
	if err != nil || len(records) != 2 {
		t.Fatalf("recent events: %+v, %v", records, err)
	}
	if records[0].Time != 300 || records[1].Time != 200 {
		t.Fatalf("expected descending timestamps: %+v", records)
	}
	all, err := ReadRecent(path, 20)
	if err != nil || len(all) != 3 || all[2].ProgramID != 9007199254740995 {
		t.Fatalf("int64 ID lost or log parse incorrect: %+v, %v", all, err)
	}
}

func TestNoLogAndBadLimits(t *testing.T) {
	records, err := ReadRecent(filepath.Join(t.TempDir(), "not-found"), 10)
	if err != nil || records == nil || len(records) != 0 {
		t.Fatalf("no event log must be an empty result: %+v %v", records, err)
	}
	if _, err := ReadRecent("unused", 0); err == nil {
		t.Fatal("invalid limit accepted")
	}
}

func TestBoundedTailSkipsIncompleteLine(t *testing.T) {
	path := filepath.Join(t.TempDir(), "events")
	prefix := `{"programId":"1","time":10,"msg":"` + strings.Repeat("x", int(maxTailBytes)+32) + `"}` + "\n"
	last := `{"programId":"7","time":99,"msg":"valid"}` + "\n"
	if err := os.WriteFile(path, []byte(prefix+last), 0600); err != nil {
		t.Fatal(err)
	}
	items, err := ReadRecent(path, 10)
	if err != nil || len(items) != 1 || items[0].ProgramID != 7 {
		t.Fatalf("bad large-record handling: %+v %v", items, err)
	}
}
