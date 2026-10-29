package utilities

import (
	"fmt"
	rotatelogs "github.com/lestrrat-go/file-rotatelogs"
	"io"
	"log"
	"os"
	"path/filepath"
	"time"
)

type LoggerTitles struct {
	Debug string
	Info  string
	Warn  string
	Error string
}

type LoggerConfig struct {
	FilePath string
	IsStdOut bool
}

func NewLoggerTitles(moduleName string) LoggerTitles {
	name := fmt.Sprintf(" [ %-15s ] ", moduleName)
	r := LoggerTitles{
		Debug: "DEBUG" + name + " > ",
		Info:  "INFO " + name + "-> ",
		Warn:  "WARN " + name + "-- ",
		Error: "ERROR" + name + " - ",
	}
	return r
}

// InitLogger
// logFile : /userdata/xxx/aa
func InitLogger(logConfig LoggerConfig) (*log.Logger, error) {
	return InitLoggerWithSuffix(logConfig, ".log")
}

func InitLoggerWithSuffix(logConfig LoggerConfig, fileSuffix string) (*log.Logger, error) {
	logPath, err := filepath.Abs(logConfig.FilePath)
	if err != nil {
		//fmt.Printf("initLogger ERROR -> 1 : %s \n", err)
		return nil, err
	}

	logf, err := rotatelogs.New(
		logPath+".%Y-%m-%dT%H-%M-%S"+fileSuffix,
		rotatelogs.WithRotationTime(24*time.Hour),
		rotatelogs.WithRotationCount(7),
	)
	if err != nil {
		//fmt.Printf("initLogger ERROR -> 2 : %s \n", err)
		return nil, err
	}
	if logConfig.IsStdOut {
		writers := []io.Writer{logf, os.Stdout}
		fileAndStdoutWriter := io.MultiWriter(writers...)
		r := log.New(fileAndStdoutWriter, "", log.Ldate|log.Ltime|log.Lshortfile)
		//r.Printf("initLogger - complete!")
		return r, nil
	} else {
		writers := []io.Writer{logf}
		fileAndStdoutWriter := io.MultiWriter(writers...)
		r := log.New(fileAndStdoutWriter, "", log.Ldate|log.Ltime|log.Lshortfile)
		//r.Printf("initLogger - complete!")
		return r, nil
	}
}
