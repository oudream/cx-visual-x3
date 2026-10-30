package main

import (
	"context"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"

	"gogo/internal/program"
)

func TestOrphanGuardChildHelper(t *testing.T) {
	if os.Getenv("CX_SUPERVISION_ORPHAN_CHILD") == "1" {
		time.Sleep(15 * time.Second)
	}
}

func createLegacyPIDForTest(t *testing.T, wd string, item program.Program, contents string) string {
	t.Helper()
	if err := os.MkdirAll(filepath.Join(wd, "pid"), 0700); err != nil {
		t.Fatal(err)
	}
	path := filepath.Join(wd, "pid", item.GetFileName()+".pid")
	if err := os.WriteFile(path, []byte(contents), 0600); err != nil {
		t.Fatal(err)
	}
	return path
}

func TestStartupOrphanScanQuarantinesLivingOwnedCandidateWithoutKill(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	cmd := exec.Command(exe, "-test.run=^TestOrphanGuardChildHelper$")
	cmd.Env = append(os.Environ(), "CX_SUPERVISION_ORPHAN_CHILD=1")
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	defer func() {
		_ = cmd.Process.Kill()
		_ = cmd.Wait()
	}()

	a := program.Program{ID: 9012, Path: exe, Enabled: program.ENABLE}
	wd := t.TempDir()
	createLegacyPIDForTest(t, wd, a, strconv.Itoa(cmd.Process.Pid))
	orphans := scanLegacyPIDFiles(wd, []program.Program{a})
	if !strings.Contains(orphans[a.ID], "ownership/generation is unverified") {
		t.Fatalf("living prior child must be quarantined, got %q", orphans[a.ID])
	}
	gate := newProgramWorkerGate()
	if err := gate.quarantineOrphans(orphans); err != nil {
		t.Fatal(err)
	}
	if gate.start(context.Background(), a.ID, func() { t.Error("duplicate child launched") }) {
		t.Fatal("startup bypassed orphan quarantine")
	}
	if admitted, queued := gate.request(context.Background(), a.ID, func() {
		t.Error("configuration bypassed quarantine")
	}); admitted || queued {
		t.Fatal("configuration request bypassed orphan quarantine")
	}
	// Production scan is strictly read-only. The candidate is still
	// alive and its PID file remains for operator inspection.
	if _, err := os.Stat(filepath.Join(wd, "pid", a.GetFileName()+".pid")); err != nil {
		t.Fatal("orphan scan unexpectedly removed forensic PID file:", err)
	}
}

func TestOrphanScanRejectsUncertainAndMalformedPIDFiles(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	a := program.Program{ID: 9031, Path: exe}
	for _, contents := range []string{"", "bad-pid", "0", "-1", "2147483648", strings.Repeat("1", 65), strconv.Itoa(os.Getpid())} {
		t.Run(strconv.Itoa(len(contents))+"-"+strings.ReplaceAll(contents[:min(10,len(contents))], "/", "_"), func(t *testing.T) {
			wd := t.TempDir()
			createLegacyPIDForTest(t, wd, a, contents)
			if _, ok := scanLegacyPIDFiles(wd, []program.Program{a})[a.ID]; !ok {
				t.Fatalf("ambiguous old PID file %q was not quarantined", contents)
			}
		})
	}
}

func TestOrphanScanSkipsMissingAndReapedPIDWithoutRemovingFiles(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	a := program.Program{ID: 9041, Path: exe}
	wd := t.TempDir()
	if got := scanLegacyPIDFiles(wd, []program.Program{a}); len(got) != 0 {
		t.Fatalf("missing PID file is not an orphan: %+v", got)
	}
	cmd := exec.Command(exe, "-test.run=^TestOrphanGuardChildHelper$")
	cmd.Env = append(os.Environ(), "CX_SUPERVISION_ORPHAN_CHILD=1")
	if err := cmd.Start(); err != nil {
		t.Fatal(err)
	}
	pid := cmd.Process.Pid
	if err := cmd.Process.Kill(); err != nil {
		t.Fatal(err)
	}
	if err := cmd.Wait(); err == nil {
		t.Fatal("killed test helper unexpectedly returned success")
	}
	createLegacyPIDForTest(t, wd, a, strconv.Itoa(pid))
	if got := scanLegacyPIDFiles(wd, []program.Program{a}); len(got) != 0 {
		t.Fatalf("reaped PID with no live process must not block startup: %+v", got)
	}
	if _, err := os.Stat(filepath.Join(wd, "pid", a.GetFileName()+".pid")); err != nil {
		t.Fatal("stale PID file should be retained, not silently deleted")
	}
}

func TestOrphanScanRejectsSharedLegacyPIDFilename(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	a := program.Program{ID: 1001, Path: exe}
	b := program.Program{ID: 1002, Path: exe}
	wd := t.TempDir()
	createLegacyPIDForTest(t, wd, a, strconv.Itoa(os.Getpid()))
	result := scanLegacyPIDFiles(wd, []program.Program{a,b})
	if !strings.Contains(result[a.ID], "shared") || !strings.Contains(result[b.ID], "shared") {
		t.Fatalf("same legacy filename must quarantine both IDs: %+v", result)
	}
}

func TestOrphanQuarantineCannotBeClearedByNormalRecovery(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 9301
	if err := gate.quarantineOrphans(map[int64]string{id:"unverified prior generation"}); err != nil {
		t.Fatal(err)
	}
	if gate.start(context.Background(), id, func(){t.Error("recovered orphan")}) {
		t.Fatal("automatic recovery bypassed orphan quarantine")
	}
	if started,queued:=gate.request(context.Background(),id,func(){t.Error("queued orphan")}); started||queued {
		t.Fatal("explicit config update bypassed orphan quarantine")
	}
	if reason,ok:=gate.orphanReason(id);!ok||reason==""{
		t.Fatal("quarantine reason was lost")
	}
}
