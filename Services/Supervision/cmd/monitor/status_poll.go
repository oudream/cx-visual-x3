package main

// Times are Unix milliseconds, as in the existing UDP/status contract. When
// Daemon is absent, retry at most once per 30 seconds rather than every tick.
func shouldRequestDaemonStatus(now, lastResponse, lastRequest int64) bool {
	return now > 0 && (lastResponse == 0 || now-lastResponse > 30*60*1000) &&
		(lastRequest == 0 || now-lastRequest >= 30*1000 || now < lastRequest)
}
