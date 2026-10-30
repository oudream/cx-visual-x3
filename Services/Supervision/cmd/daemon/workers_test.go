package main

import (
	"context"
	"sync"
	"sync/atomic"
	"testing"
	"time"
)

func waitWorkerReleased(t *testing.T, gate *programWorkerGate, id int64) {
	t.Helper()
	deadline := time.Now().Add(3 * time.Second)
	for time.Now().Before(deadline) {
		if !gate.has(id) {
			return
		}
		time.Sleep(time.Millisecond)
	}
	t.Fatalf("worker reservation %d not released", id)
}

func TestProgramWorkerGateSingleOwnerDuringConcurrentStart(t *testing.T) {
	gate := newProgramWorkerGate()
	const id int64 = 9007199254740995
	const n = 128
	block := make(chan struct{})
	var accepted atomic.Int32
	var wg sync.WaitGroup
	for i := 0; i < n; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			if gate.start(context.Background(), id, func() { <-block }) {
				accepted.Add(1)
			}
		}()
	}
	wg.Wait()
	if accepted.Load() != 1 {
		t.Fatalf("same Program ID must have one active guardian, got %d", accepted.Load())
	}
	if !gate.has(id) {
		t.Fatal("worker must remain reserved even with PID not yet reported")
	}
	if gate.start(context.Background(), id, func() { t.Error("duplicate worker ran") }) {
		t.Fatal("same Program ID was accepted during active guardian")
	}
	close(block)
	waitWorkerReleased(t, gate, id)
}

func TestProgramWorkerGateReleasesOnlyAfterWorkerReturn(t *testing.T) {
	gate := newProgramWorkerGate()
	block := make(chan struct{})
	if !gate.start(context.Background(), 41, func() { <-block }) {
		t.Fatal("initial worker not admitted")
	}
	// The process may have exited or be in its restart backoff but the
	// existing guardian must retain its ID reservation.
	if gate.start(context.Background(), 41, func() { t.Error("second worker ran") }) {
		t.Fatal("recovery path duplicated a still-active guardian")
	}
	close(block)
	waitWorkerReleased(t, gate, 41)
	restarted := make(chan struct{})
	if !gate.start(context.Background(), 41, func() { close(restarted) }) {
		t.Fatal("guard must allow a new worker after the old worker returns")
	}
	select {
	case <-restarted:
	case <-time.After(3 * time.Second):
		t.Fatal("replacement guardian did not run")
	}
	waitWorkerReleased(t, gate, 41)
}

func TestProgramWorkerGateScopesIDsAndRejectsCancelledStarts(t *testing.T) {
	gate := newProgramWorkerGate()
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if gate.start(ctx, 11, func() { t.Error("cancelled worker ran") }) {
		t.Fatal("cancelled start must be denied")
	}
	if gate.start(context.Background(), 0, func() {}) {
		t.Fatal("invalid program ID must be denied")
	}
	if gate.start(context.Background(), 12, nil) {
		t.Fatal("nil worker must be denied")
	}
	first := make(chan struct{})
	second := make(chan struct{})
	if !gate.start(context.Background(), 11, func() { <-first }) ||
		!gate.start(context.Background(), 12, func() { <-second }) {
		t.Fatal("independent program IDs must be managed concurrently")
	}
	close(first)
	close(second)
	waitWorkerReleased(t, gate, 11)
	waitWorkerReleased(t, gate, 12)
}
