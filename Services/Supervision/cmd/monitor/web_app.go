package main

import (
	"github.com/gin-gonic/gin"
)

// --- --- getProcessStatusList
func getProgramStatusList(c *gin.Context) {
	ss := getProgramStatuses()
	content := gin.H{"status": 200, "params": ss}
	c.JSON(200, content)
}
