package daemonc

import (
	"context"
	"io"
	"log"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	"gogo/internal/launchjournal"
	"gogo/internal/program"
)

// The real test child has already been OS-started, but the unbuffered
// legacy EventStart has not been read by the main loop. The durable
// journal must still reveal this prior to any legacy PID file write.
func TestLaunchEvidenceClosesStartBeforeEventWindow(t *testing.T) {
	exe, err := os.Executable()
	if err != nil { t.Fatal(err) }
	wd := t.TempDir()
	const id int64 = 9007199254740995
	a := program.Program{
		ID: id, Path: exe, Dir: wd,
		ArgsString: "-test.run=^TestOwnedStopChildHelper$",
		EnvsString: "CX_SUPERVISION_OWNED_STOP_CHILD=1",
		Enabled: program.ENABLE, MaxError: 2, MaxCount: 10,
	}
	events := make(chan program.Event) // intentionally UNBUFFERED
	ctx,cancel := context.WithCancel(context.Background())
	done := make(chan struct{})
	fail := make(chan error,1)
	go func(){
		defer close(done)
		RunProgramContextWithLaunchEvidence(ctx,a,events,
			func(int64)(program.Program,error){return a,nil},
			log.New(io.Discard,"",0),wd,nil,NewStopController(id),
			launchjournal.New(wd,id),func(err error){fail<-err})
	}()
	deadline := time.Now().Add(5*time.Second)
	found := false
	for time.Now().Before(deadline) {
		items,scanErr:=launchjournal.Scan(wd)
		if scanErr!=nil{t.Fatal(scanErr)}
		if strings.Contains(items[id],"stage=started pid=") {
			found=true
			break
		}
		select {
		case err := <-fail: t.Fatalf("launch journal failed: %v",err)
		default:
		}
		time.Sleep(10*time.Millisecond)
	}
	if !found {t.Fatal("worker did not persist PID before an unconsumed EventStart")}
	if _,err:=os.Stat(filepath.Join(wd,"pid"));err==nil{
		t.Fatal("test incorrectly depended on legacy PID file")
	}
	cancel()
	select {
	case <-done:
	case <-time.After(5*time.Second): t.Fatal("worker did not kill/reap its own child on cancellation")
	}
	if items,err:=launchjournal.Scan(wd);err!=nil||len(items)!=0 {
		t.Fatalf("reaped test child left unresolved evidence: %+v %v",items,err)
	}
}

func TestLaunchEvidenceBrokenStoragePreventsStartingChild(t *testing.T) {
	exe,err:=os.Executable()
	if err!=nil{t.Fatal(err)}
	wd:=t.TempDir()
	a:=program.Program{ID: 17,Path: exe,Dir: wd,Enabled:program.ENABLE}
	path:=filepath.Join(wd,"supervision","launch-attempts")
	if err:=os.MkdirAll(filepath.Dir(path),0700);err!=nil{t.Fatal(err)}
	if err:=os.WriteFile(path,[]byte("not a directory"),0600);err!=nil{t.Fatal(err)}
	events:=make(chan program.Event,5)
	var evidenceError error
	RunProgramContextWithLaunchEvidence(context.Background(),a,events,
		func(int64)(program.Program,error){return a,nil},log.New(io.Discard,"",0),
		wd,nil,NewStopController(a.ID),launchjournal.New(wd,a.ID),
		func(err error){evidenceError=err})
	if evidenceError==nil{t.Fatal("unwritable journal allowed a child launch")}
	if len(events)!=0{t.Fatal("child launch event emitted after evidence failure")}
}
