package httpapi

import (
	"net/http"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/guardview"
	"gogo/internal/security"
)

// GuardProvider reads local Daemon-authored diagnostic artifacts. It cannot
// mutate daemon guards or issue control actions across processes.
type GuardProvider func() guardview.Report

func RegisterGuardDiagnostics(engine *gin.Engine, provider GuardProvider) {
	api := engine.Group("/api/v1", security.ReadOnlyHeaders())
	api.GET("/guard", security.LocalOnly(), func(c *gin.Context) {
		report := provider()
		c.JSON(http.StatusOK, gin.H{
			"data": report,
			"kind": "local-daemon-artifact-observation",
			"scope": "local-only",
			"generatedAt": time.Now().UTC(),
		})
	})
}
