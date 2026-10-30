package httpapi

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"

	"gogo/internal/supervision"
)

// localRequest creates the same-origin loopback request expected by the
// unauthenticated read-only Monitor UI. It does not grant a logged-in identity.
func localRequest(method, path string) *http.Request {
	req := httptest.NewRequest(method, path, nil)
	req.Host = "localhost:7811"
	req.RemoteAddr = "127.0.0.1:5000"
	return req
}

func TestQueryRoutes(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	Register(router, func() []supervision.Program {
		return []supervision.Program{
			{ID: 9, Running: false}, {ID: 2, Running: true, HeartbeatAtMs: 123},
		}
	})
	for _, tc := range []struct {
		path   string
		status int
	}{
		{"/api/v1/health", 200},
		{"/api/v1/overview", 200},
		{"/api/v1/programs", 200},
		{"/api/v1/programs/2", 200},
		{"/api/v1/programs/xyz", 400},
		{"/api/v1/programs/99", 404},
		{"/api/v1/programs/-1", 400},
	} {
		w := httptest.NewRecorder()
		router.ServeHTTP(w, localRequest(http.MethodGet, tc.path))
		if w.Code != tc.status {
			t.Fatalf("%s: expected %d got %d (%s)", tc.path, tc.status, w.Code, w.Body.String())
		}
		if w.Header().Get("Cache-Control") != "no-store" {
			t.Fatalf("%s: read-only response must not be cached", tc.path)
		}
	}
	w := httptest.NewRecorder()
	router.ServeHTTP(w, localRequest(http.MethodGet, "/api/v1/programs"))
	var envelope struct { Data []supervision.Program `json:"data"` }
	if err := json.Unmarshal(w.Body.Bytes(), &envelope); err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(w.Body.String(), `"id":"2"`) {
		t.Fatalf("program ID must remain exact decimal string: %s", w.Body.String())
	}
	if len(envelope.Data) != 2 || envelope.Data[0].ID != 2 {
		t.Fatalf("programs must be sorted snapshots: %+v", envelope.Data)
	}
	w = httptest.NewRecorder()
	router.ServeHTTP(w, localRequest(http.MethodGet, "/api/v1/overview"))
	var total struct { Data supervision.Overview `json:"data"` }
	if err := json.Unmarshal(w.Body.Bytes(), &total); err != nil {
		t.Fatal(err)
	}
	if total.Data.Total != 2 || total.Data.Running != 1 || total.Data.HeartbeatReporting != 1 {
		t.Fatalf("incorrect overview: %+v", total.Data)
	}
	w = httptest.NewRecorder()
	router.ServeHTTP(w, localRequest(http.MethodPost, "/api/v1/programs/2"))
	if w.Code == http.StatusOK {
		t.Fatal("unexpected mutating route")
	}
}


func TestSensitiveSnapshotRoutesAreLocalOnly(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	providerCalls := 0
	Register(router, func() []supervision.Program {
		providerCalls++
		return []supervision.Program{{ID: 9, Path: "/sensitive/customer/recipe.json", Running: true}}
	})
	for _, path := range []string{"/api/v1/overview", "/api/v1/programs", "/api/v1/programs/9"} {
		for _, tc := range []struct {
			name string
			remote string
			host string
			origin string
			xff string
			forwarded string
			realIP string
		}{
			{"remote client", "192.0.2.9:1234", "localhost:7811", "", "", "", ""},
			{"spoofed host", "127.0.0.1:1234", "outside.example:7811", "", "", "", ""},
			{"forwarded client", "127.0.0.1:1234", "localhost:7811", "", "192.0.2.9", "", ""},
			{"forwarded header", "127.0.0.1:1234", "localhost:7811", "", "", "for=192.0.2.9", ""},
			{"real IP header", "127.0.0.1:1234", "localhost:7811", "", "", "", "192.0.2.9"},
			{"cross origin", "127.0.0.1:1234", "localhost:7811", "https://outside.example", "", "", ""},
			{"malformed source", "invalid", "localhost:7811", "", "", "", ""},
		} {
			t.Run(path+"/"+tc.name, func(t *testing.T) {
				w := httptest.NewRecorder()
				req := httptest.NewRequest(http.MethodGet, path, nil)
				req.RemoteAddr = tc.remote
				req.Host = tc.host
				if tc.origin != "" { req.Header.Set("Origin", tc.origin) }
				if tc.xff != "" { req.Header.Set("X-Forwarded-For", tc.xff) }
				if tc.forwarded != "" { req.Header.Set("Forwarded", tc.forwarded) }
				if tc.realIP != "" { req.Header.Set("X-Real-IP", tc.realIP) }
				router.ServeHTTP(w, req)
				if w.Code != http.StatusForbidden {
					t.Fatalf("want 403, got %d: %s", w.Code, w.Body.String())
				}
				if strings.Contains(w.Body.String(), "sensitive") {
					t.Fatalf("sensitive snapshot was leaked: %s", w.Body.String())
				}
				if w.Header().Get("Cache-Control") != "no-store" {
					t.Fatal("rejected snapshot must not be cached")
				}
			})
		}
	}
	if providerCalls != 0 {
		t.Fatalf("snapshot provider ran for %d rejected requests", providerCalls)
	}

	// Health is an HTTP liveness probe only, with no process snapshot.
	w := httptest.NewRecorder()
	req := httptest.NewRequest(http.MethodGet, "/api/v1/health", nil)
	req.RemoteAddr = "192.0.2.9:1234"
	req.Host = "outside.example:7811"
	router.ServeHTTP(w, req)
	if w.Code != http.StatusOK || strings.Contains(w.Body.String(), "sensitive") || providerCalls != 0 {
		t.Fatalf("health must not disclose the snapshot or call its provider: %d %s", w.Code, w.Body.String())
	}
}
