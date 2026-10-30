package launchjournal

import (
	"os"
	"path/filepath"
	"strings"
	"sync"
	"testing"
)

func TestPreStartIntentSurvivesCrashWithoutPID(t *testing.T) {
	dir:=t.TempDir()
	const id int64=9007199254740995
	first:=New(dir,id)
	if err:=first.Begin(id,"/opt/managed/example");err!=nil{t.Fatal(err)}
	// Simulate SIGKILL after durable intent, before exec.Cmd.Start.
	found,err:=Scan(dir)
	if err!=nil{t.Fatal(err)}
	if !strings.Contains(found[id],"stage=intent pid=0"){
		t.Fatalf("pre-spawn crash lost the evidence: %+v",found)
	}
	if err:=New(dir,id).Begin(id,"/opt/managed/example");err==nil{
		t.Fatal("new Daemon must refuse overwriting unresolved prior intent")
	}
}

func TestPostStartCrashWithoutLegacyPIDFileIsQuarantined(t *testing.T) {
	dir:=t.TempDir()
	const id int64=1032
	j:=New(dir,id)
	if err:=j.Begin(id,"/opt/managed/example");err!=nil{t.Fatal(err)}
	if err:=j.Started(4321);err!=nil{t.Fatal(err)}
	found,err:=Scan(dir)
	if err!=nil{t.Fatal(err)}
	if !strings.Contains(found[id],"stage=started pid=4321"){
		t.Fatalf("crash after OS Start should preserve observed PID: %+v",found)
	}
}

func TestOriginalWorkerWaitReconcilesEvidence(t *testing.T) {
	dir:=t.TempDir()
	const id int64=55
	j:=New(dir,id)
	if err:=j.Begin(id,"/opt/example");err!=nil{t.Fatal(err)}
	if err:=j.Started(120);err!=nil{t.Fatal(err)}
	if err:=j.Finished();err!=nil{t.Fatal(err)}
	found,err:=Scan(dir)
	if err!=nil || len(found)!=0{t.Fatalf("reaped child was not reconciled: %+v %v",found,err)}
	if err:=j.Begin(id,"/opt/example");err!=nil{t.Fatalf("normal next generation was blocked: %v",err)}
	if err:=j.Finished();err!=nil{t.Fatal(err)}
}

func TestFailedStartWithoutOSChildClearsIntent(t *testing.T) {
	dir:=t.TempDir()
	j:=New(dir,9)
	if err:=j.Begin(9,"/missing/child");err!=nil{t.Fatal(err)}
	// The failed Start has no child and therefore no Wait is necessary.
	if err:=j.Finished();err!=nil{t.Fatal(err)}
	if results,err:=Scan(dir);err!=nil || len(results)!=0{
		t.Fatalf("failed spawn incorrectly latched: %+v %v",results,err)
	}
}

func TestLaunchJournalRejectsTamperingAndUnsafeEntry(t *testing.T) {
	dir:=t.TempDir()
	j:=New(dir,77)
	if err:=j.Begin(77,"/opt/example");err!=nil{t.Fatal(err)}
	path:=recordPath(dir,77)
	if err:=os.WriteFile(path,[]byte("{invalid"),0600);err!=nil{t.Fatal(err)}
	if err:=j.Finished();err==nil{
		t.Fatal("corrupted evidence must not be silently deleted")
	}
	found,err:=Scan(dir)
	if err!=nil || !strings.Contains(found[77],"corrupt"){
		t.Fatalf("invalid record must quarantine the Program: %+v %v",found,err)
	}
	unknown:=filepath.Join(recordDir(dir),"unknown.data")
	if err:=os.WriteFile(unknown,[]byte("invalid"),0600);err!=nil{t.Fatal(err)}
	if _,err:=Scan(dir);err==nil{t.Fatal("unattributable evidence must fail closed globally")}
}

func TestJournalPreventsOverlappingOrMismatchedAttempts(t *testing.T) {
	dir:=t.TempDir()
	j:=New(dir,11)
	if err:=j.Begin(12,"/opt/example");err==nil{t.Fatal("wrong ID accepted")}
	if err:=j.Begin(11,"/opt/example");err!=nil{t.Fatal(err)}
	if err:=j.Begin(11,"/opt/example");err==nil{t.Fatal("overlap accepted")}
	if err:=j.Started(0);err==nil{t.Fatal("invalid PID accepted")}
	if err:=j.Started(321);err!=nil{t.Fatal(err)}
	if err:=j.Started(321);err==nil{t.Fatal("second Started accepted")}
	if err:=j.Finished();err!=nil{t.Fatal(err)}
	if err:=j.Finished();err==nil{t.Fatal("second Finished accepted")}
}

func TestLaunchEvidenceSymlinkCannotBeTrusted(t *testing.T) {
	dir:=t.TempDir()
	j:=New(dir,9)
	if err:=j.Begin(9,"/opt/example");err!=nil{t.Fatal(err)}
	real:=recordPath(dir,9)
	backup:=filepath.Join(dir,"backup")
	if err:=os.Rename(real,backup);err!=nil{t.Fatal(err)}
	if err:=os.Symlink(backup,real);err!=nil{t.Skipf("symlink unavailable: %v",err)}
	if err:=j.Finished();err==nil{t.Fatal("symlinked evidence must not be cleared")}
	found,err:=Scan(dir)
	if err!=nil || found[9]==""{t.Fatalf("must quarantine symlinked evidence: %+v %v",found,err)}
}


func TestLaunchEvidenceDirectorySymlinkRejected(t *testing.T) {
	wd:=t.TempDir()
	outside:=t.TempDir()
	if err:=os.MkdirAll(filepath.Join(wd,"supervision"),0700);err!=nil{t.Fatal(err)}
	if err:=os.Symlink(outside,recordDir(wd));err!=nil{
		t.Skipf("directory symlink unavailable: %v",err)
	}
	j:=New(wd,104)
	if err:=j.Begin(104,"/managed/child");err==nil{
		t.Fatal("redirected evidence directory must refuse a new process launch")
	}
}


func TestPriorJournalWithoutOSBirthStaysUnresolved(t *testing.T) {
	wd := t.TempDir()
	j := New(wd, 9007199254740995)
	if err := j.Begin(9007199254740995, "/legacy/managed"); err != nil {
		t.Fatal(err)
	}
	// Older JSON snapshots did not include processStartMs. They are
	// read as unknown evidence, never auto-unlatched during migration.
	r, err := readRecord(recordPath(wd, 9007199254740995))
	if err != nil { t.Fatal(err) }
	r.Stage, r.PID, r.ProcessStartMs = "started", 2147483647, 0
	if err := writeRecord(recordPath(wd, 9007199254740995), &r); err != nil {
		t.Fatal(err)
	}
	found, err := Scan(wd)
	if err != nil { t.Fatal(err) }
	if !strings.Contains(found[9007199254740995], "unreconciled launch") ||
		!strings.Contains(found[9007199254740995], "osBirthMs=0 identity=") {
		t.Fatalf("old unbound record must stay quarantined: %+v", found)
	}
	r.ProcessStartMs = -1
	if err := writeRecord(recordPath(wd, 9007199254740995), &r); err != nil { t.Fatal(err) }
	if found, err = Scan(wd); err != nil || !strings.Contains(found[9007199254740995], "corrupt") {
		t.Fatalf("negative OS birth timestamp must be rejected: %+v %v", found, err)
	}
}


func TestConcurrentLaunchBeginsCannotOverwriteOtherAttempt(t *testing.T) {
	wd := t.TempDir()
	const id int64 = 9007199254740995
	const contenders = 32
	var wait sync.WaitGroup
	var success int
	var mu sync.Mutex
	for n := 0; n < contenders; n++ {
		wait.Add(1)
		go func() {
			defer wait.Done()
			j := New(wd, id)
			if err := j.Begin(id, "/managed/child"); err == nil {
				mu.Lock()
				success++
				mu.Unlock()
			}
		}()
	}
	wait.Wait()
	if success != 1 {
		t.Fatalf("exclusive intent reservation admitted %d workers, want exactly 1", success)
	}
	records, err := Scan(wd)
	if err != nil || !strings.Contains(records[id], "stage=intent") {
		t.Fatalf("exclusive Begin lost durable evidence: %+v %v", records, err)
	}
}

func TestStartedRejectsReplacedIntentAndNeverOverwrites(t *testing.T) {
	wd := t.TempDir()
	j := New(wd, 909)
	if err := j.Begin(909, "/managed/child"); err != nil {
		t.Fatal(err)
	}
	path := recordPath(wd, 909)
	replaced, err := readRecord(path)
	if err != nil {
		t.Fatal(err)
	}
	replaced.AttemptID = "0123456789abcdef0123456789abcdef"
	if err := writeRecord(path, &replaced); err != nil {
		t.Fatal(err)
	}
	if err := j.Started(1234); err == nil {
		t.Fatal("Started overwrote another launch attempt")
	}
	still, err := readRecord(path)
	if err != nil {
		t.Fatal(err)
	}
	if still.AttemptID != replaced.AttemptID || still.Stage != "intent" {
		t.Fatal("foreign launch evidence was modified")
	}
	if err := j.Finished(); err == nil {
		t.Fatal("original Worker may not clear another attempt's evidence")
	}
}

func TestStartedRefusesDisappearedOrCorruptIntent(t *testing.T) {
	for _, corrupt := range []bool{false, true} {
		wd := t.TempDir()
		j := New(wd, 902)
		if err := j.Begin(902, "/managed/child"); err != nil {
			t.Fatal(err)
		}
		path := recordPath(wd, 902)
		if corrupt {
			if err := os.WriteFile(path, []byte("bad"), 0600); err != nil {
				t.Fatal(err)
			}
		} else if err := os.Remove(path); err != nil {
			t.Fatal(err)
		}
		if err := j.Started(1234); err == nil {
			t.Fatal("missing/corrupt pre-start evidence must prevent PID recording")
		}
	}
}
