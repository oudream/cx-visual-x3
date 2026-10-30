package monitorc

import "testing"

// An invalid target fails during address parsing without creating a socket.
// Even on that failure, the client must remember the original target so the
// legacy CheckChannel retry cannot silently dial port 0 or an empty host.
func TestMonCRetainsTargetAcrossInitialDialFailure(t *testing.T) {
	const host = "invalid:udp:address"
	const port = 7811
	client, err := NewMonC(host, port, nil)
	if err == nil {
		if client != nil {
			_ = client.Close()
		}
		t.Fatal("malformed UDP destination must fail")
	}
	if client == nil {
		t.Fatal("MonC must return a recoverable client on initial dial failure")
	}
	if client.IP != host || client.Port != port {
		t.Fatalf("retry lost target: got host=%q port=%d", client.IP, client.Port)
	}
	if client.udpC != nil {
		t.Fatal("failed dial unexpectedly created an active UDP connection")
	}
	if err := client.CheckChannel(); err == nil {
		t.Fatal("retry must still reject the same malformed target")
	}
}
