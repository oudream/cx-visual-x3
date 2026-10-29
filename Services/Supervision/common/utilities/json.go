package utilities

import (
	"encoding/json"
	"regexp"
)

func JsonLogString(obj interface{}) string {
	sJson, err := json.Marshal(obj)
	if err == nil {
		re := regexp.MustCompile(`"(\w+)":`)
		return re.ReplaceAllString(string(sJson), "$1:")
	}
	return ""
}
