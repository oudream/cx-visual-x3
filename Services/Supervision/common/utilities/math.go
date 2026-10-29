package utilities

import (
	"fmt"
	"math"
	"strconv"
)

func MathMin(a, b int) int {
	if a <= b {
		return a
	}
	return b
}

func MathDecimal2A(value float64) float64 {
	value, _ = strconv.ParseFloat(fmt.Sprintf("%.2f", value), 64)
	return value
}

func MathDecimal2(value float64) float64 {
	return math.Trunc(value*1e2+0.5) * 1e-2
}

func MathDecimal3(value float64) float64 {
	return math.Trunc(value*1e3+0.5) * 1e-3
}

func MathFloatEqual(a, b, tolerance float64) bool {
	//return math.Dim(a, b) < tolerance
	return math.Abs(a-b) < tolerance
}

func MathFloatEqual3(a, b float64) bool {
	//return math.Dim(a, b) < tolerance
	return math.Abs(a-b) < Tolerance3
}

func MathFloatPointerEqual3(a, b *float64) bool {
	if a == nil || b == nil {
		return a == b
	}
	return math.Abs(*a-*b) < Tolerance3
}

func MathIntPointerEqual(a, b *int) bool {
	if a == nil || b == nil {
		return a == b
	}
	return *a == *b
}
