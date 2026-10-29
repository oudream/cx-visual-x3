package utilities

// ArrayStringExist
// MapString2int
// ConvertBool2Int
// ArgsContainKey

import (
	"os"
	"sync"
)

const (
	MaxUint32  = ^uint32(0)
	MinUint32  = uint32(0)
	MaxInt32   = int32(MaxUint32 >> 1)
	MinInt32   = -MaxInt32 - 1
	MaxUint64  = ^uint64(0)
	MinUint64  = uint64(0)
	MaxInt64   = int64(MaxUint64 >> 1)
	MinInt64   = -MaxInt64 - 1
	Tolerance3 = 0.003
)

// Cyclical Redundancy Checking.
var (
	once     sync.Once
	crtTable []uint16
)

func ArgsContainKey(name string) bool {
	//flag.Visit(func(f *flag.Flag) {
	//	if f.Name == name {
	//		found = true
	//	}
	//})
	for i := 0; i < len(os.Args); i++ {
		if os.Args[i] == name {
			return true
		}
	}
	return false
}
