package httpapi

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"github.com/gin-gonic/gin"
	"gogo/internal/stopjournal"
)

func TestStopHistoryLocalOnlyAndReadOnly(t *testing.T) {
	gin.SetMode(gin.TestMode)
	engine := gin.New()
	calls := 0
	RegisterStopHistory(engine, func() stopjournal.History {
		calls++
		return stopjournal.History{
			Available: true,
			Items: []stopjournal.Entry{{
				OperationID: "0123456789abcdef0123456789abcdef",
				ProgramID: "9007199254740995", PID: 19,
				Action: "shutdown", State: "unknown",
				RecordedAt: "2026-10-30T09:00:00Z",
			}},
		}
	})
	for _, tc := range []struct{ remote, host, forwarded string }{
		{"198.51.100.2:1222","localhost:7811",""},
		{"127.0.0.1:1222","outside.invalid",""},
		{"127.0.0.1:1222","localhost:7811","for=198.51.100.2"},
	} {
		req:=httptest.NewRequest(http.MethodGet,"/api/v1/stops/history",nil)
		req.RemoteAddr,req.Host=tc.remote,tc.host
		if tc.forwarded!=""{req.Header.Set("Forwarded",tc.forwarded)}
		w:=httptest.NewRecorder()
		engine.ServeHTTP(w,req)
		if w.Code!=http.StatusForbidden || strings.Contains(w.Body.String(),"9007199254740995"){
			t.Fatalf("remote read exposed history: %d %s",w.Code,w.Body.String())
		}
	}
	if calls!=0{t.Fatalf("provider called for unauthorized request: %d",calls)}
	req:=httptest.NewRequest(http.MethodGet,"/api/v1/stops/history",nil)
	req.RemoteAddr,req.Host="127.0.0.1:1333","localhost:7811"
	w:=httptest.NewRecorder()
	engine.ServeHTTP(w,req)
	if w.Code!=200 || calls!=1 || !strings.Contains(w.Body.String(),`"programId":"9007199254740995"`){
		t.Fatalf("local history unavailable: %d %s",w.Code,w.Body.String())
	}
	if w.Header().Get("Cache-Control")!="no-store"{t.Fatal("local audit response cacheable")}
	req=httptest.NewRequest(http.MethodPost,"/api/v1/stops/history",nil)
	req.RemoteAddr,req.Host="127.0.0.1:1333","localhost:7811"
	w=httptest.NewRecorder()
	engine.ServeHTTP(w,req)
	if w.Code==200{t.Fatal("stop history endpoint has a write route")}
}
