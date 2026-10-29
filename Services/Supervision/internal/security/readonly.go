// Package security holds shared HTTP security restrictions. This middleware
// protects read-only responses from caching and framing; it is NOT authentication.
package security

import "github.com/gin-gonic/gin"

// ReadOnlyHeaders prevents caching of operational status and basic UI embedding.
// No mutating API should be exposed until authentication, authorization and
// audit are implemented and tested.
func ReadOnlyHeaders() gin.HandlerFunc {
	return func(c *gin.Context) {
		c.Header("Cache-Control", "no-store")
		c.Header("X-Content-Type-Options", "nosniff")
		c.Header("X-Frame-Options", "DENY")
		c.Header("Referrer-Policy", "no-referrer")
		c.Next()
	}
}
