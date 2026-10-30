package main

import "testing"

func TestAbsentDaemonRequestsAreBoundedAndFreshResponsesSuppressRetry(t *testing.T) {
	const now int64 = 1793350000000
	if !shouldRequestDaemonStatus(now, 0, 0) {
		t.Fatal("initial request suppressed")
	}
	for elapsed := int64(500); elapsed < 30000; elapsed += 500 {
		if shouldRequestDaemonStatus(now+elapsed, 0, now) {
			t.Fatal("absent Daemon caused tick-rate polling")
		}
	}
	if !shouldRequestDaemonStatus(now+30000, 0, now) {
		t.Fatal("retry never admitted")
	}
	if shouldRequestDaemonStatus(now+30000, now, now) {
		t.Fatal("fresh status ignored")
	}
	if !shouldRequestDaemonStatus(now+1800001, now, now) {
		t.Fatal("stale status never queried")
	}
	if !shouldRequestDaemonStatus(now-1, 0, now) {
		t.Fatal("clock rollback blocked retry")
	}
}
