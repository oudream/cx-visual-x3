package main

import (
	"net/http"

	"github.com/gin-gonic/gin"

	"gogo/internal/httpapi"
)

// setupHttpServer retains the legacy endpoint and adds versioned read-only APIs.
// Debug/pprof is intentionally not exposed from production Monitor.
func setupHttpServer(e *gin.Engine, _ *http.Server) {
	e.GET("/programs_status", getProgramStatusList)
	httpapi.Register(e, apiProgramSnapshot)
}
