package httpapi

import (
	"net/http"
	"time"

	"github.com/gin-gonic/gin"
	"gogo/internal/security"
	"gogo/internal/stopjournal"
)

// RegisterStopHistory exposes Daemon-written operation receipts as local
// diagnostics only. No POST, stop, restart or unlock command is exposed.
func RegisterStopHistory(engine *gin.Engine, provider func() stopjournal.History) {
	api := engine.Group("/api/v1", security.ReadOnlyHeaders())
	api.GET("/stops/history", security.LocalOnly(), func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{
			"data": provider(),
			"scope": "local-only",
			"kind": "durable-stop-history",
			"generatedAt": time.Now().UTC(),
		})
	})
}
