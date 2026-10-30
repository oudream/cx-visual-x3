package main

import (
	"context"
	"sync"

	"gogo/internal/daemonc"
	"gogo/internal/program"
)

// programWorkerGate owns only the lifetime of a Daemon goroutine per Program
// ID. PID and process state remain in the existing Daemon event loop.
// A worker with PID=0 can still be starting, waiting to restart, or stopping;
// these are not reasons to launch another worker for the same Program.
type programWorkerGate struct {
	mu     sync.Mutex
	active map[int64]struct{}
}

func newProgramWorkerGate() *programWorkerGate {
	return &programWorkerGate{active: make(map[int64]struct{})}
}

// start reserves the ID before launching the goroutine. The reservation is
// released only when the worker function actually returns, not when a child
// sends EventExit or the Daemon temporarily sees PID=0.
func (g *programWorkerGate) start(ctx context.Context, id int64, run func()) bool {
	if id <= 0 || ctx.Err() != nil || run == nil {
		return false
	}
	g.mu.Lock()
	if ctx.Err() != nil {
		g.mu.Unlock()
		return false
	}
	if _, exists := g.active[id]; exists {
		g.mu.Unlock()
		return false
	}
	g.active[id] = struct{}{}
	g.mu.Unlock()

	go func() {
		defer func() {
			g.mu.Lock()
			delete(g.active, id)
			g.mu.Unlock()
		}()
		run()
	}()
	return true
}

func (g *programWorkerGate) has(id int64) bool {
	g.mu.Lock()
	_, exists := g.active[id]
	g.mu.Unlock()
	return exists
}

var _programWorkers = newProgramWorkerGate()

// launchProgramWorker retains the old program lookup, event, and process
// owners. It merely deduplicates concurrent starts while a worker is alive.
func launchProgramWorker(ctx context.Context, a program.Program, events chan program.Event) bool {
	return _programWorkers.start(ctx, a.ID, func() {
		daemonc.RunProgramContext(ctx, a, events, findProgramById, _logger, *_workDir)
	})
}
