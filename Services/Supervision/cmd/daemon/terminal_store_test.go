package main

import (
	"context"
	"os"
	"path/filepath"
	"strings"
	"testing"

	"gogo/internal/program"
)

func TestTerminalLatchFilePersistsAcrossDaemonGateInstances(t *testing.T) {
	path := filepath.Join(t.TempDir(), "supervision", "daemon-terminal-latches.json")
	first := newProgramWorkerGate()
	if err := first.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	const bigID int64 = 9007199254740995
	if err := first.markTerminal(program.Event{
		Event: program.EventMaxError, Status: program.Status{Id: bigID},
	}); err != nil {
		t.Fatal(err)
	}
	if err := first.markTerminal(program.Event{
		Event: program.EventMaxCount, Status: program.Status{Id: 56},
	}); err != nil {
		t.Fatal(err)
	}
	again := newProgramWorkerGate()
	if err := again.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	for _, tc := range []struct{ id int64; reason int }{
		{bigID, program.EventMaxError}, {56, program.EventMaxCount},
	} {
		if reason, blocked := again.blocked(tc.id); !blocked || reason != tc.reason {
			t.Fatalf("persisted Program %d latch missing: %d, %t", tc.id, reason, blocked)
		}
		if again.start(context.Background(), tc.id, func() { t.Error("persisted latch bypassed") }) {
			t.Fatal("automatic restart bypassed persisted terminal latch")
		}
	}
	data, err := os.ReadFile(path)
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(data), "9007199254740995") {
		t.Fatal("64-bit ProgramId was lost/rounded in the persisted journal")
	}
}

func TestTerminalLatchStoreFailsClosedOnCorruption(t *testing.T) {
	for _, content := range []string{
		"",
		"{",
		"{}",
		"{\"version\":99,\"terminal\":{}}",
		"{\"version\":1,\"terminal\":{\"0\":4}}",
		"{\"version\":1,\"terminal\":{\"5\":1}}",
		"{\"version\":1,\"terminal\":{}} {}",
		"{\"version\":1,\"terminal\":null}",
	} {
		t.Run(content, func(t *testing.T) {
			path := filepath.Join(t.TempDir(), "terminal.json")
			if err := os.WriteFile(path, []byte(content), 0600); err != nil {
				t.Fatal(err)
			}
			gate := newProgramWorkerGate()
			if err := gate.enablePersistence(path); err == nil {
				t.Fatal("malformed terminal latch must stop Daemon startup")
			}
			if gate.start(context.Background(), 11, func() { t.Error("fail-open worker") }) {
				t.Fatal("failed persistence loading must not start a worker")
			}
		})
	}
}

func TestTerminalLatchStoreRejectsUnwritableDestination(t *testing.T) {
	path := filepath.Join(t.TempDir(), "directory")
	if err := os.Mkdir(path, 0700); err != nil {
		t.Fatal(err)
	}
	gate := newProgramWorkerGate()
	if err := gate.enablePersistence(path); err == nil {
		t.Fatal("journal path pointing to directory must fail")
	}
	if gate.start(context.Background(), 1, func() { t.Error("worker ran") }) {
		t.Fatal("startup ignored journal failure")
	}
}

func TestTerminalLatchStoreIgnoresNonTerminalEvent(t *testing.T) {
	path := filepath.Join(t.TempDir(), "journal.json")
	gate := newProgramWorkerGate()
	if err := gate.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	if err := gate.markTerminal(program.Event{
		Event: program.EventExit, Status: program.Status{Id: 14},
	}); err != nil {
		t.Fatal(err)
	}
	reloaded := newProgramWorkerGate()
	if err := reloaded.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	if _, blocked := reloaded.blocked(14); blocked {
		t.Fatal("normal exit incorrectly persisted as terminal lock")
	}
}


func TestTerminalLatchWriteFaultBlocksFutureWorkerStarts(t *testing.T) {
	root := t.TempDir()
	dir := filepath.Join(root, "supervision")
	path := filepath.Join(dir, "daemon-terminal-latches.json")
	gate := newProgramWorkerGate()
	if err := gate.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	// Simulate a storage path disappearing after initial successful load.
	// Make the old directory path a plain file so MkdirAll cannot recreate it.
	moved := filepath.Join(root, "moved")
	if err := os.Rename(dir, moved); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(dir, []byte("blocked"), 0600); err != nil {
		t.Fatal(err)
	}
	err := gate.markTerminal(program.Event{
		Event: program.EventMaxError, Status: program.Status{Id: 91},
	})
	if err == nil || gate.persistenceError() == nil {
		t.Fatal("failed journal write must be surfaced and recorded")
	}
	for _, id := range []int64{91, 92} {
		if gate.start(context.Background(), id, func() { t.Error("worker started after storage fault") }) {
			t.Fatalf("storage fault must reject Program %d", id)
		}
		if started, queued := gate.request(context.Background(), id, func() {
			t.Error("queued worker after storage fault")
		}); started || queued {
			t.Fatal("storage fault must reject explicit enabled-start intent")
		}
	}
}


func TestTerminalSnapshotIsReadOnly(t *testing.T) {
	path := filepath.Join(t.TempDir(), "terminal.json")
	gate := newProgramWorkerGate()
	if err := gate.enablePersistence(path); err != nil {
		t.Fatal(err)
	}
	if err := gate.markTerminal(program.Event{
		Event: program.EventMaxCount, Status: program.Status{Id: 65},
	}); err != nil {
		t.Fatal(err)
	}
	diagnostic := gate.terminalSnapshot()
	delete(diagnostic, 65)
	diagnostic[66] = program.EventMaxError
	if reason, blocked := gate.blocked(65); !blocked || reason != program.EventMaxCount {
		t.Fatal("diagnostic view modified real terminal latch")
	}
	if _, blocked := gate.blocked(66); blocked {
		t.Fatal("diagnostic view created an unauthorized terminal latch")
	}
}


func TestTerminalLatchStoreRejectsOversizedFile(t *testing.T) {
	path := filepath.Join(t.TempDir(), "too-large.json")
	file, err := os.Create(path)
	if err != nil {
		t.Fatal(err)
	}
	if err := file.Truncate(terminalLatchMaxBytes + 1); err != nil {
		file.Close()
		t.Fatal(err)
	}
	if err := file.Close(); err != nil {
		t.Fatal(err)
	}
	gate := newProgramWorkerGate()
	if err := gate.enablePersistence(path); err == nil {
		t.Fatal("oversized journal must block Daemon startup")
	}
	if gate.start(context.Background(), 9001, func() { t.Error("unexpected worker") }) {
		t.Fatal("oversized journal must not be treated as empty")
	}
}
