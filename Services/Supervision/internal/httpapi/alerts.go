package httpapi

import (
	"net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/alerts"
	"gogo/internal/security"
)

// AlertHistoryProvider reads bounded historic failure records, not active alarms.
type AlertHistoryProvider func(limit int) ([]alerts.Incident, error)

func registerAlertHistory(api *gin.RouterGroup, provider AlertHistoryProvider) {
	api.GET("/alerts/history", security.LocalOnly(), func(c *gin.Context) {
		limit := 30
		if v := c.Query("limit"); v != "" {
			n, err := strconv.Atoi(v)
			if err != nil || n < 1 || n > 100 {
				c.JSON(http.StatusBadRequest, gin.H{"error": "invalid_limit"})
				return
			}
			limit = n
		}
		items, err := provider(limit)
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": "alerts_unavailable"})
			return
		}
		c.JSON(http.StatusOK, gin.H{
			"data": items,
			"scope": "local-only",
			"kind": "historical-failures",
			"historyWindowBytes": 2097152,
			"generatedAt": time.Now().UTC(),
		})
	})
}

 // RegisterAlertHistory installs only the read-only, loopback-restricted
 // historical failure view. This is separate from all control operations.
func RegisterAlertHistory(engine *gin.Engine, provider AlertHistoryProvider) {
	api := engine.Group("/api/v1", security.ReadOnlyHeaders())
	registerAlertHistory(api, provider)
}
