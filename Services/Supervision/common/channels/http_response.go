package channels

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

type PageResult struct {
	List     interface{} `json:"list"`
	Total    int64       `json:"total"`
	Page     int         `json:"page"`
	PageSize int         `json:"pageSize"`
}

type PageResultIn struct {
	PageResult
	InList interface{} `json:"inList"`
}

type Response struct {
	Code int         `json:"status"`
	Data interface{} `json:"data"`
	Msg  string      `json:"msg"`
}

const (
	//ErrorToken = 50002
	ErrorToken = 401
	//ERROR      = 50001
	ERROR = 500
	//SUCCESS = 20000
	SUCCESS = 200
	//WARNING = 60001
	WARNING = 600
)

func (server *HttpServer) Result(code int, data interface{}, msg string, c *gin.Context) {
	// 开始时间
	c.JSON(http.StatusOK, Response{
		code,
		data,
		msg,
	})
}

func (server *HttpServer) Ok(c *gin.Context) {
	server.Result(SUCCESS, map[string]interface{}{}, "操作成功", c)
}

func (server *HttpServer) OkWithMessage(message string, c *gin.Context) {
	server.Result(SUCCESS, map[string]interface{}{}, message, c)
}

func (server *HttpServer) OkWithData(data interface{}, c *gin.Context) {
	server.Result(SUCCESS, data, "查询成功", c)
}

func (server *HttpServer) OkWithDetailed(data interface{}, message string, c *gin.Context) {
	server.Result(SUCCESS, data, message, c)
}

func (server *HttpServer) Fail(c *gin.Context) {
	server.Result(ERROR, map[string]interface{}{}, "操作失败", c)
}

func (server *HttpServer) FailWithMessage(message string, c *gin.Context) {
	server.Logger.Printf(server.Title.Error + message)
	server.Result(ERROR, map[string]interface{}{}, message, c)
}

func (server *HttpServer) FailToken(message string, c *gin.Context) {
	server.Logger.Printf(server.Title.Error + message)
	server.Result(ErrorToken, map[string]interface{}{}, message, c)
}

func (server *HttpServer) FailWithDetailed(data interface{}, message string, c *gin.Context) {
	server.Logger.Printf(server.Title.Error + message)
	server.Result(ERROR, data, message, c)
}

func (server *HttpServer) Warning(c *gin.Context) {
	server.Result(WARNING, map[string]interface{}{}, "操作警告", c)
}

func (server *HttpServer) WarningWithMessage(message string, c *gin.Context) {
	server.Logger.Printf(server.Title.Warn + message)
	server.Result(WARNING, map[string]interface{}{}, message, c)
}

func (server *HttpServer) WarningWithDetailed(data interface{}, message string, c *gin.Context) {
	server.Logger.Printf(server.Title.Warn + message)
	server.Result(WARNING, data, message, c)
}
