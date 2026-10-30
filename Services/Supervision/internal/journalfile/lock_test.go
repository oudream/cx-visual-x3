package journalfile

import (
	"bufio"
	"os"
	"os/exec"
	"testing"
	"time"
)

func TestLockReleasedAfterProcessKill(t *testing.T) {
	if dir := os.Getenv("SUPERVISION_LOCK_HELPER"); dir != "" {
		release, err := Lock(dir, ".crash.lock")
		if err != nil {
			t.Fatal(err)
		}
		defer release()
		os.Stdout.WriteString("locked\n")
		time.Sleep(30 * time.Second)
		return
	}
	dir := t.TempDir()
	cmd := exec.Command(os.Args[0], "-test.run=^TestLockReleasedAfterProcessKill$")
	cmd.Env = append(os.Environ(), "SUPERVISION_LOCK_HELPER="+dir)
	stdout, err := cmd.StdoutPipe()
	if err != nil {
		t.Fatal(err)
	}
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	defer cmd.Process.Kill()
	ready := make(chan bool, 1)
	go func() { scanner := bufio.NewScanner(stdout); ready <- scanner.Scan() && scanner.Text() == "locked" }()
	select {
	case ok := <-ready:
		if !ok {
			t.Fatal("child did not lock")
		}
	case <-time.After(10 * time.Second):
		t.Fatal("child lock timed out")
	}
	if err := tryCompetingLock(dir); err == nil {
		t.Fatal("competing handle bypassed OS lock")
	}
	if err := cmd.Process.Kill(); err != nil {
		t.Fatal(err)
	}
	_ = cmd.Wait()
	release, err := Lock(dir, ".crash.lock")
	if err != nil {
		t.Fatalf("crash left a permanent lock: %v", err)
	}
	release()
}

func tryCompetingLock(dir string) error {
	f, err := os.OpenFile(dir+"/.crash.lock", os.O_RDWR, 0600)
	if err != nil {
		return err
	}
	defer f.Close()
	err = tryLock(f)
	if err == nil {
		unlock(f)
	}
	return err
}
