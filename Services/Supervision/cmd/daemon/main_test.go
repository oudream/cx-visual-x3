package main

import (
	"context"
	"strings"
	"testing"
	"time"

	"gogo/internal/program"
)

func TestFormatDaemonExitEvent(t *testing.T) {
	const nowMs int64 = 1700000010000
	cases := []struct {
		name string
		event program.Event
		expected string
		notExpected string
	}{
		{
			name: "short abnormal exit",
			event: program.Event{Event: program.EventExit, Status: program.Status{
				Id: 42, Pid: 110, ErrorCount: 1, LastRunTime: nowMs - 3500,
			}, Message: "exit status 1"},
			expected: "异常退出（运行时间过短），运行时长：3秒",
		},
		{
			name: "normal exit without artificial too short label",
			event: program.Event{Event: program.EventExit, Status: program.Status{
				Id: 42, Pid: 110, ErrorCount: 0, LastRunTime: nowMs - 120000,
			}, Message: "<nil>"},
			expected: "退出，运行时长：120秒",
			notExpected: "运行时间过短",
		},
		{
			name: "timestamp in future",
			event: program.Event{Event: program.EventExit, Status: program.Status{
				Id: 42, ErrorCount: 0, LastRunTime: nowMs + 1000,
			}},
			expected: "运行时长：0秒",
		},
		{
			name: "no timestamp",
			event: program.Event{Event: program.EventExit, Status: program.Status{Id: 42}},
			expected: "运行时长：0秒",
		},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			msg := formatDaemonExitEvent("inspection.exe", tc.event, " (id:42)", nowMs)
			if !strings.Contains(msg, tc.expected) {
				t.Fatalf("expected %q in %q", tc.expected, msg)
			}
			if tc.notExpected != "" && strings.Contains(msg, tc.notExpected) {
				t.Fatalf("unexpected %q in %q", tc.notExpected, msg)
			}
		})
	}
}


func TestWaitForFirstStartHonorsCancel(t *testing.T) {
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if waitForFirstStart(ctx, time.Hour) {
		t.Fatal("cancelled initial startup delay should not start a program")
	}
	if !waitForFirstStart(context.Background(), 0) {
		t.Fatal("zero initial wait must not prevent startup")
	}

	ctx, cancel = context.WithCancel(context.Background())
	done := make(chan bool, 1)
	go func() {
		done <- waitForFirstStart(ctx, time.Hour)
	}()
	cancel()
	select {
	case ok := <-done:
		if ok {
			t.Fatal("startup delay continued despite cancellation")
		}
	case <-time.After(2 * time.Second):
		t.Fatal("startup delay did not cancel promptly")
	}
}
