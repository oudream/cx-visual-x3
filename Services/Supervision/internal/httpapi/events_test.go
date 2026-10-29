package httpapi

import (
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"

	"gogo/internal/events"
	"gogo/internal/supervision"
)

func TestEventRouteLocalOnly(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	calls := 0
	Register(router, func() []supervision.Program { return []supervision.Program{} },
		func(limit int) ([]events.Record, error) {
			calls++
			if limit != 3 {
				t.Fatalf("event limit = %d", limit)
			}
			return []events.Record{{ProgramID: 9007199254740995, Time: 123, Event: 3}}, nil
		})
	for _, tc := range []struct {
		remote string
		path   string
		status int
	}{
		{"127.0.0.1:12345", "/api/v1/events?limit=3", 200},
		{"[::1]:12345", "/api/v1/events?limit=3", 200},
		{"10.1.2.3:12345", "/api/v1/events?limit=3", 403},
		{"127.0.0.1:12345", "/api/v1/events?limit=101", 400},
		{"127.0.0.1:12345", "/api/v1/events?limit=x", 400},
	} {
		w := httptest.NewRecorder()
		req := httptest.NewRequest(http.MethodGet, tc.path, nil)
		req.RemoteAddr = tc.remote
		router.ServeHTTP(w, req)
		if w.Code != tc.status {
			t.Fatalf("%s from %s: expected %d, got %d", tc.path, tc.remote, tc.status, w.Code)
		}
		if tc.status == 200 && !strings.Contains(w.Body.String(), "9007199254740995") {
			t.Fatalf("large program ID missing: %s", w.Body.String())
		}
	}
	w := httptest.NewRecorder()
	forwarded := httptest.NewRequest(http.MethodGet, "/api/v1/events?limit=3", nil)
	forwarded.RemoteAddr = "127.0.0.1:12345"
	forwarded.Header.Set("X-Forwarded-For", "203.0.113.99")
	router.ServeHTTP(w, forwarded)
	if w.Code != http.StatusForbidden {
		t.Fatalf("forwarded external client bypassed local-only access: %d", w.Code)
	}
	if calls != 2 {
		t.Fatalf("provider should only run for permitted requests, called %d times", calls)
	}
}

func TestEventProviderFailureDoesNotLeakDetails(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	Register(router, func() []supervision.Program { return nil },
		func(_ int) ([]events.Record, error) { return nil, errors.New("confidential/db-path") })
	w := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodGet, "/api/v1/events", nil)
	req.RemoteAddr = "127.0.0.1:3333"
	router.ServeHTTP(w, req)
	if w.Code != 500 || strings.Contains(w.Body.String(), "confidential") {
		t.Fatalf("error leaked internal path: %s", w.Body.String())
	}
}
