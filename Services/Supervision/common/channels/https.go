package channels

import (
	"bytes"
	"context"
	"fmt"
	"github.com/gin-gonic/gin"
	"gogo/common/utilities"
	"html/template"
	"io"
	"log"
	"mime/multipart"
	"net/http"
	"time"
)

type HttpServer struct {
	Conf       gin.LoggerConfig
	AssetsPath string
	Engine     *gin.Engine
	Server     *http.Server
	Logger     *log.Logger
	Title      utilities.LoggerTitles
}

var htmlIndex = template.Must(template.New("welcome").Parse(`
<html>
<head>
  <title>Welcome HY</title>
</head>
<body>
  <h1 style="color:red;">{{ .title }}!</h1>
</body>
</html>
`))

// assetsPath: empty : No Index.html, No Static WWWRoot, It Is Pure Service

func NewHttpServer(logger *log.Logger, httpPort int, assetsPath string, indexPath string, useCors bool) *HttpServer {
	var emptySkipPaths []string
	var conf = gin.LoggerConfig{
		Formatter: func(param gin.LogFormatterParams) string {
			var statusColor, methodColor, resetColor string
			if param.IsOutputColor() {
				statusColor = param.StatusCodeColor()
				methodColor = param.MethodColor()
				resetColor = param.ResetColor()
			}
			//
			if param.Latency > time.Minute {
				// Truncate in a golang < 1.8 safe way
				param.Latency = param.Latency - param.Latency%time.Second
			}
			if len(param.ErrorMessage) > 0 {
				return fmt.Sprintf("[GIN] %v |%s %3d %s| %13v | %15s |%s %-7s %s %#v\n%s",
					param.TimeStamp.Format("2006/01/02 - 15:04:05"),
					statusColor, param.StatusCode, resetColor,
					param.Latency,
					param.ClientIP,
					methodColor, param.Method, resetColor,
					param.Path,
					param.ErrorMessage,
				)
			} else {
				return ""
			}
		},
		Output:    gin.DefaultWriter,
		SkipPaths: emptySkipPaths,
	}
	//gin.SetMode(gin.ReleaseMode)
	var engine = gin.New()
	engine.Use(gin.LoggerWithConfig(conf), gin.Recovery())
	//engine.Use(CORSMiddleware())
	if utilities.FileSystemIsExist(assetsPath) {
		engine.Static("/assets", assetsPath)
		engine.GET("/index.html", func(c *gin.Context) {
			c.Redirect(http.StatusMovedPermanently, indexPath)
		})
		engine.NoRoute(func(context *gin.Context) {
			context.Redirect(http.StatusMovedPermanently, indexPath)
			logger.Printf("ERROR: NoRoute[%s]\n", context.Request.RequestURI)
		})
	} else {
		engine.GET("/index.html", func(c *gin.Context) {
			c.String(200, "No Index.html\nNo Static WWWRoot\nIt Is Pure Service")
		})
		engine.NoRoute(func(c *gin.Context) {
			c.String(200, "No Index.html\nNo Static WWWRoot\nIt Is Pure Service")
		})
	}
	if useCors {
		engine.Use(Cors())
	}
	httpServer := &HttpServer{
		Conf:       conf,
		AssetsPath: assetsPath,
		Engine:     engine,
		Logger:     logger,
		Title:      utilities.NewLoggerTitles(fmt.Sprintf("HTTP%d", httpPort)),
	}
	return httpServer
}

// Run
// --assetsPath /opt/dev/hello_iec104/assets
// httpAddr: 0.0.0.0:1234
func (server *HttpServer) Run(httpAddr string) {
	server.Server = &http.Server{
		Addr:    httpAddr,
		Handler: server.Engine,
	}
	server.Logger.Printf("HttpServer Listen [ %s ] %s", httpAddr, server.AssetsPath)

	// Initializing the server in a goroutine so that
	// it won't block the graceful shutdown handling below
	if err := server.Server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		server.Logger.Fatalf("Fatal Listen Err: %s\n", err)
	}
}

func (server *HttpServer) Close() {
	server.Logger.Println("HttpServer Shutting Down ..")
	// The context is used to inform the server it has 5 seconds to finish
	// the request it is currently handling
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if err := server.Server.Shutdown(ctx); err != nil {
		server.Logger.Printf("ERROR, HttpServer Forced To Shutdown: %v", err)
	}
	server.Logger.Println("HttpServer Shutting Down complete")
}

// Cors
// 处理跨域请求,支持options访问
func Cors() gin.HandlerFunc {
	return func(context *gin.Context) {
		method := context.Request.Method
		context.Header("Access-Control-Allow-Origin", "*")
		//context.Header("Access-Control-Allow-Headers", "Content-Type, AccessToken, X-CSRF-Token, X-Token, X-User-Id, Authorization, Token, POWERED-BY-AID")
		context.Header("Access-Control-Allow-Headers", "*")
		//context.Header("Access-Control-Allow-Headers", "Content-Type, AccessToken, X-CSRF-Token, Authorization, Token, POWERED-BY-AID")
		context.Header("Access-Control-Allow-Methods", "POST, GET, PATCH, OPTIONS, PUT, DELETE")
		context.Header("Access-Control-Expose-Headers", "Content-Length, Access-Control-Allow-Origin, Access-Control-Allow-Headers, Content-Type")
		context.Header("Access-Control-Allow-Credentials", "true")
		// 放行所有OPTIONS方法
		if method == "OPTIONS" {
			context.AbortWithStatus(http.StatusNoContent)
		}
		// 处理请求
		context.Next()
	}
}

func ReadFileContent(file *multipart.FileHeader) ([]byte, error) {
	content, err := file.Open()
	if err != nil {
		return nil, err
	}
	defer func(content multipart.File) {
		err := content.Close()
		if err != nil {

		}
	}(content)
	b := new(bytes.Buffer)
	_, err = io.Copy(b, content)
	if err != nil {
		fmt.Println("copying contents:", err)
		return nil, err
	}
	return b.Bytes(), nil
}
