package main

import (
	"net"
	"net/http"
	"strings"

	"github.com/gin-gonic/gin"

	"gogo/internal/guardview"
	"gogo/internal/httpapi"
	"gogo/internal/stopjournal"
)

// setupHttpServer retains the legacy endpoint and adds versioned read-only APIs.
// Debug/pprof is intentionally not exposed from production Monitor.
func setupHttpServer(e *gin.Engine, _ *http.Server) {
	e.GET("/programs_status", getProgramStatusList)
	httpapi.Register(e, apiProgramSnapshot, apiEventHistory)
	httpapi.RegisterAlertHistory(e, apiAlertHistory)
	httpapi.RegisterGuardDiagnostics(e, func() guardview.Report {
		return guardview.Read(*_workDir)
	})
	httpapi.RegisterStopHistory(e, func() stopjournal.History {
		return stopjournal.Read(*_workDir)
	})
}

// httpListenMayBeRemote is a conservative diagnostic for the legacy HTTP
// listener. It is not an access-control mechanism: bind/firewall policy must
// still be enforced at deployment.
func httpListenMayBeRemote(host string) bool {
	host = strings.TrimSpace(host)
	if strings.EqualFold(host, "localhost") {
		return false
	}
	ip := net.ParseIP(host)
	return ip == nil || !ip.IsLoopback()
}
