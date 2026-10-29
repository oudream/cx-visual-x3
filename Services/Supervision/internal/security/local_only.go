package security

import (
	"net"
	"net/http"
	"strings"

	"github.com/gin-gonic/gin"
)

// LocalOnly enforces a conservative loopback-only diagnostic boundary until
// proper identity, authorization and audit controls are available.
// Source address alone is insufficient: deny forwarding headers and DNS-rebind
// Host values which could route a remote browser through the local interface.
func LocalOnly() gin.HandlerFunc {
	return func(c *gin.Context) {
		reject := func() { c.AbortWithStatusJSON(http.StatusForbidden, gin.H{"error": "local_access_required"}) }
		if c.GetHeader("Forwarded") != "" || c.GetHeader("X-Forwarded-For") != "" || c.GetHeader("X-Real-IP") != "" {
			reject()
			return
		}
		remoteHost, _, err := net.SplitHostPort(c.Request.RemoteAddr)
		if err != nil || !isLoopbackHost(remoteHost) || !isLoopbackHost(hostWithoutPort(c.Request.Host)) {
			reject()
			return
		}
		if origin := c.GetHeader("Origin"); origin != "" {
			// Cross-origin read access must not be enabled via an older CORS
			// setting in a reused Gin engine.
			reject()
			return
		}
		c.Next()
	}
}

func hostWithoutPort(hostport string) string {
	host, _, err := net.SplitHostPort(hostport)
	if err == nil {
		return host
	}
	return strings.Trim(hostport, "[]")
}

func isLoopbackHost(host string) bool {
	if strings.EqualFold(host, "localhost") {
		return true
	}
	ip := net.ParseIP(host)
	return ip != nil && ip.IsLoopback()
}
