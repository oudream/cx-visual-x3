package httpapi

import (
	"net/http"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/resourceview"
	"gogo/internal/security"
)

// RegisterResources reads actual host-level CPU/memory and the filesystem
// containing Daemon/Monitor workDir. It never exposes process controls.
func RegisterResources(engine *gin.Engine, provider func() resourceview.Report) {
	api := engine.Group("/api/v1", security.ReadOnlyHeaders())
	api.GET("/resources", security.LocalOnly(), func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{
			"data": provider(),
			"scope": "local-only",
			"kind": "host-resource-snapshot",
			"generatedAt": time.Now().UTC(),
		})
	})
}
