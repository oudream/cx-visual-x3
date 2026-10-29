package httpapi

import (
	"net"
	"net/http"
	"strconv"
	"time"

	"github.com/gin-gonic/gin"

	"gogo/internal/events"
)

// EventProvider reads existing persisted events; it must not modify log files.
type EventProvider func(limit int) ([]events.Record, error)

// registerEvents is deliberately restricted to loopback requests until real
// authentication, authorization and log data redaction are implemented.
func registerEvents(api *gin.RouterGroup, provider EventProvider) {
	api.GET("/events", func(c *gin.Context) {
		host, _, err := net.SplitHostPort(c.Request.RemoteAddr)
		if err != nil {
			c.JSON(http.StatusForbidden, gin.H{"error": "local_access_required"})
			return
		}
		// A local reverse proxy can hide the remote peer behind 127.0.0.1.
		// Fail closed when forwarding headers are present until proper auth exists.
		if c.GetHeader("Forwarded") != "" || c.GetHeader("X-Forwarded-For") != "" || c.GetHeader("X-Real-IP") != "" {
			c.JSON(http.StatusForbidden, gin.H{"error": "local_access_required"})
			return
		}
		ip := net.ParseIP(host)
		if ip == nil || !ip.IsLoopback() {
			c.JSON(http.StatusForbidden, gin.H{"error": "local_access_required"})
			return
		}
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
