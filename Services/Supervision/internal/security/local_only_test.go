package security

import (
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/gin-gonic/gin"
)

func TestLocalOnly(t *testing.T) {
	gin.SetMode(gin.TestMode)
	r := gin.New()
	r.GET("/local", LocalOnly(), func(c *gin.Context) { c.Status(http.StatusNoContent) })
	cases := []struct {
		remote, host, origin, xff string
		status int
	}{
		{"127.0.0.1:5000", "127.0.0.1:7811", "", "", 204},
		{"[::1]:5000", "localhost:7811", "", "", 204},
		{"192.0.2.15:5000", "localhost:7811", "", "", 403},
		{"127.0.0.1:5000", "evil.example:7811", "", "", 403},
		{"127.0.0.1:5000", "localhost:7811", "https://evil.example", "", 403},
		{"127.0.0.1:5000", "localhost:7811", "", "192.0.2.15", 403},
	}
	for _, tc := range cases {
		w := httptest.NewRecorder()
		req := httptest.NewRequest(http.MethodGet, "/local", nil)
		req.Host = tc.host
		req.RemoteAddr = tc.remote
		if tc.origin != "" { req.Header.Set("Origin", tc.origin) }
		if tc.xff != "" { req.Header.Set("X-Forwarded-For", tc.xff) }
		r.ServeHTTP(w,req)
		if w.Code != tc.status { t.Errorf("host=%q remote=%q: want %d, got %d",tc.host,tc.remote,tc.status,w.Code) }
	}
}
