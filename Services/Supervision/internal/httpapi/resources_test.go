package httpapi

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"
	"gogo/internal/resourceview"
)

func TestResourceEndpointLoopbackOnlyAndDoesNotInvokeProviderForRemote(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	calls := 0
	RegisterResources(router, func() resourceview.Report {
		calls++
		return resourceview.Report{
			Scope: "host", DiskScope: "work-dir-filesystem",
			CPU: resourceview.CPU{Available: false, Error: "sample unavailable"},
			Memory: resourceview.Storage{Available: true, TotalBytes: 1000, UsedBytes: 250, FreeBytes: 750, UsedPercent: 25, SampledAt: "2026-10-30T01:00:00Z"},
			Disk: resourceview.Storage{Available: false, Error: "disk unavailable"},
		}
	})
	for _, tc := range []struct { name,remote,host,forwarded string } {
		{"remote", "192.0.2.3:1234","localhost:7811",""},
		{"spoofed-host","127.0.0.1:1234","outside.example:7811",""},
		{"forwarded","127.0.0.1:1234","localhost:7811","for=192.0.2.3"},
	} {
		t.Run(tc.name,func(t *testing.T) {
			req := httptest.NewRequest(http.MethodGet,"/api/v1/resources",nil)
			req.RemoteAddr,req.Host=tc.remote,tc.host
			if tc.forwarded!="" { req.Header.Set("Forwarded",tc.forwarded) }
			w:=httptest.NewRecorder()
			router.ServeHTTP(w,req)
			if w.Code!=http.StatusForbidden || strings.Contains(w.Body.String(),"1000") {
				t.Fatalf("resource data exposed to remote: %d %s",w.Code,w.Body.String())
			}
		})
	}
	if calls!=0 {t.Fatalf("unauthorized request invoked sampler %d times",calls)}
	req := httptest.NewRequest(http.MethodGet,"/api/v1/resources",nil)
	req.RemoteAddr,req.Host="127.0.0.1:1234","localhost:7811"
	w:=httptest.NewRecorder()
	router.ServeHTTP(w,req)
	if w.Code!=200 || calls!=1 || !strings.Contains(w.Body.String(),`"diskScope":"work-dir-filesystem"`) ||
		!strings.Contains(w.Body.String(),`"available":false`) {
		t.Fatalf("resource contract: %d %s",w.Code,w.Body.String())
	}
	if w.Header().Get("Cache-Control")!="no-store" {t.Fatal("resource response must not be cached")}
	req=httptest.NewRequest(http.MethodPost,"/api/v1/resources",nil)
	req.RemoteAddr,req.Host="127.0.0.1:1234","localhost:7811"
	w=httptest.NewRecorder()
	router.ServeHTTP(w,req)
	if w.Code==http.StatusOK {t.Fatal("mutating endpoint unexpectedly added")}
}
