package main

import "testing"

func TestElapsedRunSeconds(t *testing.T) {
	for _, tc := range []struct{ now, start, want int64 }{
		{1700000010000, 1700000000000, 10},
		{1700000000500, 1700000000000, 0},
		{1700000000000, 1700000000000, 0},
		{1700000000000, 1700000000500, 0},
		{1700000000000, 0, 0},
	} {
		got := elapsedRunSeconds(tc.now, tc.start)
		if got != tc.want {
			t.Fatalf("now=%d start=%d: want %d, got %d", tc.now, tc.start, tc.want, got)
		}
	}
}
