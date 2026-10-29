package utilities

import (
	"fmt"
	"strconv"
	"strings"
)

func ArrayStringExist(arr []string, s string) bool {
	for i := 0; i < len(arr); i++ {
		if arr[i] == s {
			return true
		}
	}
	return false
}

func ArrayIntJoin(a []int, sep string) string {
	return strings.Trim(strings.Replace(fmt.Sprint(a), " ", sep, -1), "[]")
	//return strings.Trim(strings.Join(strings.Split(fmt.Sprint(a), " "), delim), "[]")
	//return strings.Trim(strings.Join(strings.Fields(fmt.Sprint(a)), delim), "[]")
}

func ArrayInt64Join(a []int64, sep string) string {
	return strings.Trim(strings.Replace(fmt.Sprint(a), " ", sep, -1), "[]")
}

func ArrayInt64JoinFormat(nn []int64, format string, sep string) string {
	var ss []string
	for i := 0; i < len(nn); i++ {
		ss = append(ss, fmt.Sprintf(format, nn[i]))
	}
	return strings.Join(ss, sep)
}

// ArrayStringJoinFix
// ArrayStringJoinFix([a, b, c], "\"", ",") return "a","b","c"
func ArrayStringJoinFix(arr []string, fix, s string) string {
	var ss []string
	for i := 0; i < len(arr); i++ {
		ss = append(ss, fix+arr[i]+fix)
	}
	return strings.Join(ss, s)
}

// ArrayText2Strings
// ArrayText2Strings('a, 1, 2', return [a, "1", "2"]
func ArrayText2Strings(A string, sep string) []string {
	result := strings.Split(A, sep)
	for i, elem := range result {
		result[i] = strings.TrimSpace(elem)
	}
	return result
}

// ArrayText2Int64s
// ArrayText2Int64s('a, 1, 2', return [a, b, c]
func ArrayText2Int64s(A string, sep string) []int64 {
	a := strings.Split(A, sep)
	return ArrayStringInt64s(a)
}

func ArrayStringInt64s(a []string) []int64 {
	b := make([]int64, len(a))
	for i, v := range a {
		value, err := strconv.ParseInt(v, 10, 64)
		if err == nil {
			b[i] = value
		}
	}
	return b
}

// ArrayText2Float64s
// ArrayText2Int64s
// ArrayText2Int64s('a, 1, 2', return [a, b, c]
func ArrayText2Float64s(A string, sep string) []float64 {
	a := strings.Split(A, sep)
	return ArrayStringFloat64s(a)
}

func ArrayStringFloat64s(a []string) []float64 {
	b := make([]float64, len(a))
	for i, v := range a {
		value, err := strconv.ParseFloat(v, 64)
		if err == nil {
			b[i] = value
		}
	}
	return b
}

func MapString2Int(m map[string]string, k string) (v int, ok bool) {
	var s string
	s, ok = m[k]
	if ok {
		var err error
		v, err = strconv.Atoi(s)
		ok = err == nil
	}
	return
}

func ConvertBool2Int(b bool) int {
	if b {
		return 1
	}
	return 0
}

func ConvertString2Bool(s string) bool {
	i, err := strconv.Atoi(s)
	if err == nil && i == 1 {
		return true
	}
	return false
}

func ConvertString2Int(s string) int {
	i, err := strconv.Atoi(s)
	if err == nil {
		return i
	}
	return -1
}

func ConvertString2Int64(s string) int64 {
	i, err := strconv.ParseInt(s, 10, 64)
	if err == nil {
		return i
	}
	return -1
}

func ConvertString2Float32(s string) float32 {
	f, err := strconv.ParseFloat(s, 32)
	if err == nil {
		return float32(f)
	}
	return 0
}

func ConvertString2Float64(s string) float64 {
	f, err := strconv.ParseFloat(s, 64)
	if err == nil {
		return f
	}
	return 0
}

func ConvertMapString(text string, pairSep, sep string) map[string]string {
	m := make(map[string]string)
	if len(text) == 0 {
		return m
	}
	entries := strings.Split(text, pairSep)
	for _, e := range entries {
		parts := strings.Split(e, sep)
		if len(parts) > 1 {
			m[parts[0]] = parts[1]
		}
	}
	return m
}

func ConvertMapInt(text string, pairSep, sep string) map[string]int {
	m := make(map[string]int)
	entries := strings.Split(text, pairSep)
	for _, e := range entries {
		parts := strings.Split(e, sep)
		if len(parts) > 1 {
			i, err := strconv.Atoi(strings.TrimSpace(parts[1]))
			if err == nil {
				m[parts[0]] = i
			}
		}
	}
	return m
}

func ConvertMapIntUpper(text string, pairSep, sep string) map[string]int {
	m := make(map[string]int)
	entries := strings.Split(text, pairSep)
	for _, e := range entries {
		parts := strings.Split(e, sep)
		if len(parts) > 1 {
			i, err := strconv.Atoi(strings.TrimSpace(parts[1]))
			if err == nil {
				k := strings.TrimSpace(strings.ToUpper(parts[0]))
				m[k] = i
			}
		}
	}
	return m
}

func ConvertMapFloat64(text string, pairSep, sep string) map[string]float64 {
	m := make(map[string]float64)
	entries := strings.Split(text, pairSep)
	for _, e := range entries {
		parts := strings.Split(e, sep)
		if len(parts) > 1 {
			float, err := strconv.ParseFloat(strings.TrimSpace(parts[1]), 64)
			if err == nil {
				m[parts[0]] = float
			}
		}
	}
	return m
}

func ArrayRemoveDuplicatesInt64(ii []int64) (r []int64) {
	mi := map[int64]int{}
	for i := 0; i < len(ii); i++ {
		mi[ii[i]] = 0
	}
	for i := range mi {
		r = append(r, i)
	}
	return
}
