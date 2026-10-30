package main

import (
	"context"
	"errors"
	"sync"

	"gogo/internal/daemonc"
	"gogo/internal/program"
)

// programWorkerGate owns only per-Program guardian goroutine lifetime,
// an in-process terminal-limit latch, and an explicit enabled-start intent.
// PID/OS process state and Program configuration remain with their old Owners.
type programWorkerGate struct {
	mu       sync.Mutex
	active   map[int64]struct{}
	pending  map[int64]func()
	terminal map[int64]int
	persistPath string
	storageFault error
}

func newProgramWorkerGate() *programWorkerGate {
	return &programWorkerGate{
		active:   make(map[int64]struct{}),
		pending:  make(map[int64]func()),
		terminal: make(map[int64]int),
	}
}

// enablePersistence loads the last Daemon instance's terminal limits before
// any managed worker is admitted. Corrupt/unreadable files are fatal startup
// errors, not a reason to silently erase the limits.
func (g *programWorkerGate) enablePersistence(path string) error {
	if path == "" {
		return errors.New("terminal latch path must not be empty")
	}
	g.mu.Lock()
	defer g.mu.Unlock()
	if len(g.active) != 0 {
		return errors.New("cannot enable terminal persistence with running guardians")
	}
	loaded, err := readTerminalLatches(path)
	if err != nil {
		g.storageFault = err
		return err
	}
	if err := writeTerminalLatches(path, loaded); err != nil {
		g.storageFault = err
		return err
	}
	g.persistPath = path
	g.terminal = loaded
	g.storageFault = nil
	return nil
}

func (g *programWorkerGate) persistenceError() error {
	g.mu.Lock()
	err := g.storageFault
	g.mu.Unlock()
	return err
}

// start reserves the ID before launching. Periodic recovery never queues.
func (g *programWorkerGate) start(ctx context.Context, id int64, run func()) bool {
	started, _ := g.admit(ctx, id, run, false)
	return started
}

// request is for explicit enabled-start intent from a real configuration
// change. If the previous worker is still finishing, the new worker starts
// immediately AFTER its return (never concurrently). It cannot bypass a
// MaxError/MaxCount latch.
func (g *programWorkerGate) request(ctx context.Context, id int64, run func()) (started, queued bool) {
	return g.admit(ctx, id, run, true)
}

func (g *programWorkerGate) admit(ctx context.Context, id int64, run func(), allowQueue bool) (bool, bool) {
	if id <= 0 || ctx.Err() != nil || run == nil {
		return false, false
	}
	g.mu.Lock()
	if ctx.Err() != nil {
		g.mu.Unlock()
		return false, false
	}
	if g.storageFault != nil {
		g.mu.Unlock()
		return false, false
	}
	if _, blocked := g.terminal[id]; blocked {
		g.mu.Unlock()
		return false, false
	}
	if _, exists := g.active[id]; exists {
		if allowQueue {
			g.pending[id] = run
		}
		g.mu.Unlock()
		return false, allowQueue
	}
	g.active[id] = struct{}{}
	g.mu.Unlock()
	go g.run(ctx, id, run)
	return true, false
}

func (g *programWorkerGate) run(ctx context.Context, id int64, run func()) {
	run()
	g.mu.Lock()
	next := g.pending[id]
	delete(g.pending, id)
	if ctx.Err() != nil || next == nil {
		delete(g.active, id)
		g.mu.Unlock()
		return
	}
	if g.storageFault != nil {
		delete(g.active, id)
		g.mu.Unlock()
		return
	}
	if _, blocked := g.terminal[id]; blocked {
		delete(g.active, id)
		g.mu.Unlock()
		return
	}
	// Keep the reservation continuously owned across the handoff.
	g.mu.Unlock()
	go g.run(ctx, id, next)
}

// markTerminal must run in the guardian BEFORE its terminal event is sent.
// Recovery can then never race the event-consumer loop and relaunch a worker.
// The latch lives until Daemon process restart or a future separately
// authenticated administrative reset; legacy UDP does not clear it.
func (g *programWorkerGate) markTerminal(event program.Event) error {
	if event.Status.Id <= 0 ||
		(event.Event != program.EventMaxError && event.Event != program.EventMaxCount) {
		return nil
	}
	g.mu.Lock()
	defer g.mu.Unlock()
	g.terminal[event.Status.Id] = event.Event
	delete(g.pending, event.Status.Id)
	if g.persistPath != "" {
		if err := writeTerminalLatches(g.persistPath, g.terminal); err != nil {
			g.storageFault = err
			return err
		}
	}
	return nil
}

func (g *programWorkerGate) blocked(id int64) (int, bool) {
	g.mu.Lock()
	reason, exists := g.terminal[id]
	g.mu.Unlock()
	return reason, exists
}

func (g *programWorkerGate) clearPending(id int64) {
	g.mu.Lock()
	delete(g.pending, id)
	g.mu.Unlock()
}

func (g *programWorkerGate) has(id int64) bool {
	g.mu.Lock()
	_, exists := g.active[id]
	g.mu.Unlock()
	return exists
}

var _programWorkers = newProgramWorkerGate()

func launchProgramWorker(ctx context.Context, a program.Program, events chan program.Event) bool {
	return _programWorkers.start(ctx, a.ID, func() {
		daemonc.RunProgramContextWithTerminal(ctx, a, events, findProgramById, _logger, *_workDir, func(event program.Event) {
			if err := _programWorkers.markTerminal(event); err != nil {
				_logger.Printf("CRITICAL: failed to persist terminal latch for Program %d: %v; all future starts blocked", event.Status.Id, err)
			}
		})
	})
}

func requestProgramWorker(ctx context.Context, a program.Program, events chan program.Event) (bool, bool) {
	return _programWorkers.request(ctx, a.ID, func() {
		daemonc.RunProgramContextWithTerminal(ctx, a, events, findProgramById, _logger, *_workDir, func(event program.Event) {
			if err := _programWorkers.markTerminal(event); err != nil {
				_logger.Printf("CRITICAL: failed to persist terminal latch for Program %d: %v; all future starts blocked", event.Status.Id, err)
			}
		})
	})
}
