package main

import (
	"encoding/json"
	"io"
	"log"
	"testing"

	"gogo/internal/program"
)

func TestInvalidDaemonPayloadDoesNotRefreshOrPartiallyMutateState(t *testing.T) {
	oldLogger, oldStatuses, oldTime := logger, _programStatuses, _programStatusRespTime
	oldQueue := thisQueue
	logger = log.New(io.Discard, "", 0)
	_programStatuses = map[int64]*ProgramStatus{17: {DaemonStatus: program.Status{Id: 17, Pid: 50, IsRun: true}}}
	_programStatusRespTime = 1234
	t.Cleanup(func() {
		logger, _programStatuses, _programStatusRespTime = oldLogger, oldStatuses, oldTime
		thisQueue = oldQueue
	})
	a := &MonA{}
	valid := program.Status{Id: 17, Pid: 51, IsRun: true}
	encoded, _ := json.Marshal([]program.Status{valid})
	invalid := valid
	invalid.Id = -1
	mixed, _ := json.Marshal([]program.Status{valid, invalid})
	duplicate, _ := json.Marshal([]program.Status{valid, valid})
	contradiction := valid
	contradiction.Pid = 0
	contradictory, _ := json.Marshal([]program.Status{contradiction})
	truncated := append(append([]byte{}, encoded[:len(encoded)-1]...), ',')
	for _, payload := range [][]byte{[]byte("null"), []byte("{}"), []byte("["), truncated, mixed, duplicate, contradictory} {
		a.dealProgramStatusResp(payload)
		if _programStatusRespTime != 1234 || len(_programStatuses) != 1 || _programStatuses[17].DaemonStatus.Pid != 50 {
			t.Fatalf("invalid payload changed retained observation: %q", payload)
		}
	}
	for _, payload := range [][]byte{[]byte("{"), []byte("{}"), []byte("null"), []byte(`{"Event":999,"Status":{"Id":17}}`)} {
		a.dealProgramDaemonEvent(payload)
		if len(_programStatuses) != 1 || _programStatuses[17].DaemonStatus.Pid != 50 {
			t.Fatal("invalid event mutated state")
		}
	}
	a.dealProgramStatusResp(encoded)
	if _programStatusRespTime <= 1234 || _programStatuses[17].DaemonStatus.Pid != 51 {
		t.Fatal("valid legacy status was rejected")
	}
}
