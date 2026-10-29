package main

import (
	"github.com/gin-gonic/gin"
	_ "github.com/mattn/go-sqlite3"
	"gogo/common/channels"
	"net/http"
)

// block thread
// --assetsPath /opt/dev/hello_iec104/assets
func setupHttpServer(e *gin.Engine, server *http.Server) {
	channels.GinRegister(e)
	//
	e.GET("/programs_status", getProgramStatusList)
}
