package httpapi

import (
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/gin-gonic/gin"

	"gogo/internal/alerts"
)

func TestAlertHistoryLocalOnlyAndSemantics(t *testing.T) {
	gin.SetMode(gin.TestMode)
	router := gin.New()
	called := 0
	RegisterAlertHistory(router, func(limit int) ([]alerts.Incident, error) {
		called++
		if limit != 5 { t.Fatalf("expected limit 5, got %d", limit) }
		return []alerts.Incident{{ProgramID: 9007199254740995, Severity:"critical", Kind:"max-error", Time:123}}, nil
	})
	cases:=[]struct{ remote,host,path string; status int }{
		{"127.0.0.1:5000","localhost:7811","/api/v1/alerts/history?limit=5",200},
		{"10.10.0.5:5000","localhost:7811","/api/v1/alerts/history?limit=5",403},
		{"127.0.0.1:5000","attacker.example","/api/v1/alerts/history?limit=5",403},
		{"127.0.0.1:5000","localhost:7811","/api/v1/alerts/history?limit=101",400},
		{"127.0.0.1:5000","localhost:7811","/api/v1/alerts/history?limit=no",400},
	}
	for _,tc:=range cases{
		w:=httptest.NewRecorder()
		req:=httptest.NewRequest(http.MethodGet,tc.path,nil)
		req.RemoteAddr=tc.remote
		req.Host=tc.host
		router.ServeHTTP(w,req)
		if w.Code!=tc.status { t.Fatalf("status %d for %s expected %d",w.Code,tc.path,tc.status) }
		if tc.status==200 {
			if !strings.Contains(w.Body.String(), "\"kind\":\"historical-failures\"") ||
				!strings.Contains(w.Body.String(), "\"programId\":\"9007199254740995\"") {
				t.Fatalf("invalid historical alert response %s",w.Body.String())
			}
		}
	}
	if called!=1 { t.Fatalf("unexpected provider calls %d",called) }
}

func TestAlertHistoryProviderErrorIsOpaque(t *testing.T){
	gin.SetMode(gin.TestMode)
	router:=gin.New()
	RegisterAlertHistory(router,func(limit int)([]alerts.Incident,error){return nil,errors.New("/private/secret/path")})
	w:=httptest.NewRecorder()
	req:=httptest.NewRequest(http.MethodGet,"/api/v1/alerts/history",nil)
	req.Host="localhost:7811"
	req.RemoteAddr="127.0.0.1:2345"
	router.ServeHTTP(w,req)
	if w.Code!=500||strings.Contains(w.Body.String(),"secret"){t.Fatalf("unexpected error response %d %s",w.Code,w.Body.String())}
}
