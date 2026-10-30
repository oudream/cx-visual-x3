package guardview

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func writeDiagnostic(t *testing.T, dir, name, value string) {
	t.Helper()
	target := filepath.Join(dir, "supervision")
	if err := os.MkdirAll(target, 0700); err != nil { t.Fatal(err) }
	if err := os.WriteFile(filepath.Join(target, name), []byte(value), 0600); err != nil { t.Fatal(err) }
}

func TestGuardReportMissingIsUnknownNotHealthy(t *testing.T) {
	report := Read(t.TempDir())
	if report.Terminal.Available || report.Orphans.Available {
		t.Fatal("missing Daemon artifacts must not be presented as no guards")
	}
	if report.Terminal.Error == "" || report.Orphans.Error == "" {
		t.Fatal("each unavailable source must carry an error")
	}
	if report.Terminal.Items == nil || report.Orphans.Items == nil {
		t.Fatal("response arrays must be JSON arrays, not null")
	}
}

func TestGuardReportTwoIndependentSourcesAndExactIDs(t *testing.T) {
	dir := t.TempDir()
	writeDiagnostic(t, dir, "daemon-terminal-latches.json",
		`{"version":1,"terminal":{"9007199254740995":4,"12":5}}`)
	writeDiagnostic(t, dir, "daemon-orphan-observation.json",
		`{"version":1,"observedAt":"2026-10-30T08:20:00Z","quarantined":{"9007199254740995":"unverified old child"}}`)
	report := Read(dir)
	if !report.Terminal.Available || !report.Orphans.Available {
		t.Fatalf("valid artifacts unavailable: %+v", report)
	}
	if len(report.Terminal.Items) != 2 || report.Terminal.Items[0].ProgramID != "12" ||
		report.Terminal.Items[1].ProgramID != "9007199254740995" ||
		report.Orphans.Items[0].ProgramID != "9007199254740995" {
		t.Fatalf("lost 64-bit Program ID or stable sort: %+v", report)
	}
	if report.Orphans.ObservedAt != "2026-10-30T08:20:00Z" {
		t.Fatalf("must retain orphan scan time: %+v", report.Orphans)
	}
}

func TestGuardReportCorruptTerminalDoesNotHideOrphan(t *testing.T) {
	dir := t.TempDir()
	writeDiagnostic(t, dir, "daemon-terminal-latches.json", "{broken")
	writeDiagnostic(t, dir, "daemon-orphan-observation.json",
		`{"version":1,"observedAt":"2026-10-30T08:20:00Z","quarantined":{}}`)
	report := Read(dir)
	if report.Terminal.Available || report.Terminal.Error == "" {
		t.Fatal("corrupt terminal source must be marked unavailable")
	}
	if !report.Orphans.Available || len(report.Orphans.Items) != 0 {
		t.Fatalf("independent valid empty orphan scan should still be available: %+v", report.Orphans)
	}
}

func TestGuardReportRejectsInvalidAndOversizedArtifacts(t *testing.T) {
	for _, document := range []string{
		`{"version":2,"terminal":{}}`,
		`{"version":1,"terminal":{"0":4}}`,
		`{"version":1,"terminal":{"1":99}}`,
		`{"version":1,"terminal":{}} {"version":1}`,
		strings.Repeat("x", int(maxDiagnosticBytes)+1),
	} {
		dir := t.TempDir()
		writeDiagnostic(t, dir, "daemon-terminal-latches.json", document)
		if got := Read(dir).Terminal; got.Available {
			t.Fatalf("invalid artifact treated as valid: %.60s", document)
		}
	}
}

func TestGuardReportRequiresOrphanObservationTime(t *testing.T) {
	dir := t.TempDir()
	writeDiagnostic(t, dir, "daemon-orphan-observation.json",
		`{"version":1,"observedAt":"not-a-time","quarantined":{}}`)
	if got := Read(dir).Orphans; got.Available {
		t.Fatal("stale/invalid orphan observation without valid time must be unavailable")
	}
}


func TestGuardReportRejectsSymlinkBeforeReading(t *testing.T) {
	dir := t.TempDir()
	fileDir := filepath.Join(dir, "supervision")
	if err := os.MkdirAll(fileDir, 0700); err != nil {
		t.Fatal(err)
	}
	realFile := filepath.Join(dir, "other.json")
	if err := os.WriteFile(realFile, []byte(\`{"version":1,"terminal":{}}\`), 0600); err != nil {
		t.Fatal(err)
	}
	link := filepath.Join(fileDir, "daemon-terminal-latches.json")
	if err := os.Symlink(realFile, link); err != nil {
		// On Windows, creating symlinks can require developer mode/admin.
		t.Skipf("symlink unavailable in this test environment: %v", err)
	}
	if source := Read(dir).Terminal; source.Available {
		t.Fatal("symlinked artifact was accepted despite out-of-directory target")
	}
}
