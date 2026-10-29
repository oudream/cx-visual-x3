// Package httpapi exposes read-only Monitor snapshots over versioned HTTP.
// The HTTP boundary never owns process state and never executes OS commands.
package httpapi

import (
	"net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/security"
	"gogo/internal/supervision"
)

// SnapshotProvider returns independent, safe-to-read program value snapshots.
type SnapshotProvider func() []supervision.Program

// Register installs the V1 read-only routes on an existing Gin engine.
// It intentionally does not register any start/stop/restart endpoint.
func Register(engine *gin.Engine, provider SnapshotProvider, eventProviders ...EventProvider) {
	api := engine.Group("/api/v1", security.ReadOnlyHeaders())
	if len(eventProviders) > 0 && eventProviders[0] != nil {
		registerEvents(api, eventProviders[0])
	}
	api.GET("/health", func(c *gin.Context) {
		// Indicates the Monitor HTTP process is responsive, not that every
		// managed program is healthy.
		c.JSON(http.StatusOK, gin.H{"status": "ok", "service": "monitor", "time": time.Now().UTC()})
	})
	api.GET("/overview", func(c *gin.Context) {
		snapshot := supervision.Snapshot(provider())
		c.JSON(http.StatusOK, gin.H{
			"data": supervision.Summarize(snapshot),
			"generatedAt": time.Now().UTC(),
		})
	})
	api.GET("/programs", func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{
			"data": supervision.Snapshot(provider()),
			"generatedAt": time.Now().UTC(),
		})
	})
	api.GET("/programs/:id", func(c *gin.Context) {
		id, err := strconv.ParseInt(c.Param("id"), 10, 64)
		if err != nil || id < 0 {
			c.JSON(http.StatusBadRequest, gin.H{"error": "invalid_program_id"})
			return
		}
		item, ok := supervision.Find(provider(), id)
		if !ok {
			c.JSON(http.StatusNotFound, gin.H{"error": "program_not_found"})
			return
		}
		c.JSON(http.StatusOK, gin.H{"data": item, "generatedAt": time.Now().UTC()})
	})
}
