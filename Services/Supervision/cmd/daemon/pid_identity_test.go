package main

import (
	"os"
	"os/exec"
	"path/filepath"
	"testing"
	"time"
)

func TestVerifyManagedExecutablePIDRejectsUnsafeTargets(t *testing.T) {
	executable, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	for _, pid := range []int{0, -1, os.Getpid()} {
		if err := verifyManagedExecutablePID(pid, executable); err == nil {
			t.Fatalf("unsafe or self PID %d must be rejected", pid)
		}
	}
	if err := verifyManagedExecutablePID(os.Getpid()+1, ""); err == nil {
		t.Fatal("missing expected executable must be refused")
	}
}

// Only a disposable child of the current test executable is inspected.
// There is no signal to any external program, production process, or device.
func TestVerifyManagedExecutablePIDWithIsolatedChild(t *testing.T) {
	if os.Getenv("CX_SUPERVISION_PID_IDENTITY_HELPER") == "1" {
		time.Sleep(15 * time.Second)
		return
	}
	executable, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd := exec.Command(executable, "-test.run=^TestVerifyManagedExecutablePIDWithIsolatedChild$")
	cmd.Env = append(os.Environ(), "CX_SUPERVISION_PID_IDENTITY_HELPER=1")
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	defer func() {
		_ = cmd.Process.Kill()
		_ = cmd.Wait()
	}()

	var lastErr error
	for i := 0; i < 50; i++ {
		lastErr = verifyManagedExecutablePID(cmd.Process.Pid, executable)
		if lastErr == nil {
			break
		}
		time.Sleep(20 * time.Millisecond)
	}
	if lastErr != nil {
		t.Fatalf("test child with identical executable rejected: %v", lastErr)
	}
	other := filepath.Join(t.TempDir(), "unrelated-program")
	if err := os.WriteFile(other, []byte("unrelated"), 0600); err != nil {
		t.Fatal(err)
	}
	if err := verifyManagedExecutablePID(cmd.Process.Pid, other); err == nil {
		t.Fatal("PID reused by a different executable must not be accepted")
	}
}
