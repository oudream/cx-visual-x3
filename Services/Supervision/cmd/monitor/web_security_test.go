package main

import "testing"

// This is only a warning predicate. It must never be used as authentication
// or as a replacement for network ACLs and the legacy route migration.
func TestHTTPListenExposureWarning(t *testing.T) {
	for _, tc := range []struct {
		host string
		remote bool
	}{
		{"127.0.0.1", false},
		{"::1", false},
		{"localhost", false},
		{"LOCALHOST", false},
		{"0.0.0.0", true},
		{"::", true},
		{"192.168.1.55", true},
		{"example.com", true},
		{"", true},
		{"unknown", true},
	} {
		if actual := httpListenMayBeRemote(tc.host); actual != tc.remote {
			t.Errorf("host=%q: remote=%t, want %t", tc.host, actual, tc.remote)
		}
	}
}
