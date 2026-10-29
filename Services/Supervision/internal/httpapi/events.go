package httpapi

import (
	"net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/events"
	"gogo/internal/security"
)

// EventProvider reads existing persisted events; it must not modify log files.
type EventProvider func(limit int) ([]events.Record, error)

// registerEvents is deliberately restricted to loopback requests until real
// authentication, authorization and log data redaction are implemented.
func registerEvents(api *gin.RouterGroup, provider EventProvider) {
	api.GET("/events", security.LocalOnly(), func(c *gin.Context) {
		var err error
		limit := 30
		if q := c.Query("limit"); q != "" {
			limit, err = strconv.Atoi(q)
			if err != nil || limit < 1 || limit > 100 {
				c.JSON(http.StatusBadRequest, gin.H{"error": "invalid_limit"})
				return
			}
		}
		records, err := provider(limit)
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": "events_unavailable"})
			return
		}
		c.JSON(http.StatusOK, gin.H{
			"data": records,
			"scope": "local-only",
			"generatedAt": time.Now().UTC(),
		})
	})
}
