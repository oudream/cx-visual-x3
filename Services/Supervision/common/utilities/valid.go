package utilities

import (
	"regexp"
)

// https://www.jianshu.com/p/29476ab55bf1
// https://github.com/cdoco/common-regex

func IsValidPort(port int) bool {
	return port >= 0 && port <= 65535
}

func IsValidIP(ip string) bool {
	pattern := `^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(ip)
	} else {
		return false
	}
}

func IsValidHost(host string) bool {
	pattern := `^(?:[0-9]{1,3}\.){3}[0-9]{1,3}:[1-9][0-9]{0,4}$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(host)
	} else {
		return false
	}
}

func IsValidUrl(url string) bool {
	pattern := `^((http://)|(https://))?([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?.)+[a-zA-Z]{2,6}(/)`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(url)
	} else {
		return false
	}
}

func IsValidEmail(email string) bool {
	pattern := `^[a-zA-Z0-9_-]+@[a-zA-Z0-9_-]+(.[a-zA-Z0-9_-]+)+$`
	//pattern := `^[A-Za-z0-9u4e00-u9fa5]+@[a-zA-Z0-9_-]+(.[a-zA-Z0-9_-]+)+$`
	//pattern := `\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(email)
	} else {
		return false
	}
}

func IsValidCode(code string) bool {
	pattern := `^[a-zA-Z0-9][a-zA-Z0-9_-]{2,64}$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(code)
	} else {
		return false
	}
}

func IsValidTelPhone(phone string) bool {
	pattern := `((d{3,4})|d{3,4}-|s)?d{8}`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(phone)
	} else {
		return false
	}
}

func IsValidPhone(phone string) bool {
	pattern := `^1[0-9]{10}$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(phone)
	} else {
		return false
	}
}

func IsValidInt(integer string) bool {
	pattern := `^[0-9]+$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(integer)
	} else {
		return false
	}
}

func IsValidFloat(float string) bool {
	pattern := `^\\d+\\.[0-9]+$`
	r, err := regexp.Compile(pattern)
	if err == nil {
		return r.MatchString(float)
	} else {
		return false
	}
}
