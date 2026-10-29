package utilities

import "reflect"

func IsNil(input interface{}) bool {
	if input == nil {
		return true
	}
	if reflect.TypeOf(input).Kind() == reflect.Ptr && reflect.ValueOf(input).IsNil() {
		return true
	}
	return false
}
