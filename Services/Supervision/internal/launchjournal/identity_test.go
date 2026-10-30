package launchjournal

import (
	"os"
	"strings"
	"testing"
)

func TestBirthEvidenceDoesNotConfusePIDWithProcessGeneration(t *testing.T) {
	for _, test := range []struct {
		stored, observed int64
		want string
	}{
		{0, 15, "unverified-original-birth"},
		{15, 0, "unverified-current-birth"},
		{15, 16, "pid-creation-mismatch"},
		{15, 15, "same-creation-time"},
	} {
		if got := birthEvidence(test.stored, test.observed); got != test.want {
			t.Fatalf("stored=%d current=%d: got %q want %q", test.stored, test.observed, got, test.want)
		}
	}
}

func TestCurrentPIDIdentityIsOnlyAnObservation(t *testing.T) {
	exe, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	pid := os.Getpid()
	birth := observedBirthMs(pid)
	if birth <= 0 {
		t.Skip("OS process birth timestamp unavailable in this test environment")
	}
	record := Record{Stage: "started", PID: pid, Path: exe, ProcessStartMs: birth}
	result := DescribeCurrentPID(record)
	if !strings.Contains(result, "not-proof-of-ownership") {
		t.Fatalf("matching own process never grants ownership: %q", result)
	}
	record.ProcessStartMs++
	if got := DescribeCurrentPID(record); got != "pid-creation-mismatch" {
		t.Fatalf("same PID with different birth must not match: %q", got)
	}
	record.ProcessStartMs = 0
	if got := DescribeCurrentPID(record); got != "unverified-original-birth" {
		t.Fatalf("missing recorded birth must remain unknown: %q", got)
	}
	record.Stage, record.PID = "intent", 0
	if got := DescribeCurrentPID(record); got != "pre-start-intent-no-PID-confirmation" {
		t.Fatalf("pre-start intent must not impersonate an OS child: %q", got)
	}
}

func TestLegacyStartedEvidenceWithoutBirthRemainsQuarantined(t *testing.T) {
	dir := t.TempDir()
	j := New(dir, 414)
	if err := j.Begin(414, "/missing/exe"); err != nil {
		t.Fatal(err)
	}
	if err := j.Started(2147483647); err != nil {
		t.Fatal(err)
	}
	observations, err := Scan(dir)
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(observations[414], "unreconciled launch") ||
		!strings.Contains(observations[414], "identity=") {
		t.Fatalf("legacy/unavailable birth cannot clear launch evidence: %+v", observations)
	}
}
