package httpapi

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"
	"gogo/internal/guardview"
)

func TestGuardEndpointStaysLocalAndNeverExecutesActions(t *testing.T) {
	gin.SetMode(gin.TestMode)
	engine := gin.New()
	calls := 0
	RegisterGuardDiagnostics(engine, func() guardview.Report {
		calls++
		return guardview.Report{
			Terminal: guardview.Source{Available: false, Error: "not mounted", Items: []guardview.Item{}},
			Orphans: guardview.Source{Available: true, ObservedAt: "2026-10-30T08:00:00Z",
				Items: []guardview.Item{{ProgramID: "9007199254740995", Reason: "orphan"}}},
		}
	})
	for _, tc := range []struct {
		remote, host, forward, origin string
	}{
		{"192.0.2.3:5000", "localhost:7811", "", ""},
		{"127.0.0.1:5000", "outside.example", "", ""},
		{"127.0.0.1:5000", "localhost:7811", "for=192.0.2.3", ""},
		{"127.0.0.1:5000", "localhost:7811", "", "https://remote.example"},
	} {
		req := httptest.NewRequest(http.MethodGet, "/api/v1/guard", nil)
		req.RemoteAddr, req.Host = tc.remote, tc.host
		if tc.forward != "" { req.Header.Set("Forwarded", tc.forward) }
		if tc.origin != "" { req.Header.Set("Origin", tc.origin) }
		rec := httptest.NewRecorder()
		engine.ServeHTTP(rec, req)
		if rec.Code != http.StatusForbidden || strings.Contains(rec.Body.String(), "orphan") {
			t.Fatalf("remote access leaked diagnosis: %d %s", rec.Code, rec.Body.String())
		}
	}
	if calls != 0 {
		t.Fatalf("provider was invoked before access control: %d", calls)
	}
	req := httptest.NewRequest(http.MethodGet, "/api/v1/guard", nil)
	req.RemoteAddr, req.Host = "127.0.0.1:5000", "localhost:7811"
	rec := httptest.NewRecorder()
	engine.ServeHTTP(rec, req)
	if rec.Code != 200 || calls != 1 {
		t.Fatalf("local view unavailable: %d %s", rec.Code, rec.Body.String())
	}
	body := rec.Body.String()
	if !strings.Contains(body, `"programId":"9007199254740995"`) ||
		!strings.Contains(body, `"available":false`) ||
		!strings.Contains(body, `"available":true`) {
		t.Fatalf("diagnostic source availability or exact ID lost: %s", body)
	}
	if rec.Header().Get("Cache-Control") != "no-store" {
		t.Fatal("guard response must not be cached")
	}
	req = httptest.NewRequest(http.MethodPost, "/api/v1/guard", nil)
	req.RemoteAddr, req.Host = "127.0.0.1:5000", "localhost:7811"
	rec = httptest.NewRecorder()
	engine.ServeHTTP(rec, req)
	if rec.Code == 200 {
		t.Fatal("new read-only diagnostics accidentally exposed a mutating route")
	}
}
