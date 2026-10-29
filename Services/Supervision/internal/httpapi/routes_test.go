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
		router.ServeHTTP(w, httptest.NewRequest(http.MethodGet, tc.path, nil))
		if w.Code != tc.status {
			t.Fatalf("%s: expected %d got %d (%s)", tc.path, tc.status, w.Code, w.Body.String())
		}
		if w.Header().Get("Cache-Control") != "no-store" {
			t.Fatalf("%s: read-only response must not be cached", tc.path)
		}
	}
	w := httptest.NewRecorder()
	router.ServeHTTP(w, httptest.NewRequest(http.MethodGet, "/api/v1/programs", nil))
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
	router.ServeHTTP(w, httptest.NewRequest(http.MethodGet, "/api/v1/overview", nil))
	var total struct { Data supervision.Overview `json:"data"` }
	if err := json.Unmarshal(w.Body.Bytes(), &total); err != nil {
		t.Fatal(err)
	}
	if total.Data.Total != 2 || total.Data.Running != 1 || total.Data.HeartbeatReporting != 1 {
		t.Fatalf("incorrect overview: %+v", total.Data)
	}
	w = httptest.NewRecorder()
	router.ServeHTTP(w, httptest.NewRequest(http.MethodPost, "/api/v1/programs/2", nil))
	if w.Code == http.StatusOK {
		t.Fatal("unexpected mutating route")
	}
}
