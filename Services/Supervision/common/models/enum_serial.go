package models

import "strings"

//** BaudRate

const (
	UNKNOWN = "UNKNOWN"
)

type BaudRateMode struct {
}

type BaudRateEnum int

const (
	BaudRate110 BaudRateEnum = iota
	BaudRate300
	BaudRate600
	BaudRate1200
	BaudRate2400
	BaudRate4800
	BaudRate9600
	BaudRate14400
	BaudRate19200
	BaudRate38400
	BaudRate56000
	BaudRate57600
	BaudRate115200
	BaudRate128000
	BaudRate256000
)

func (p BaudRateEnum) IsValid() bool {
	return p.String() != UNKNOWN
}

func (p BaudRateEnum) String() string {
	switch p {
	case BaudRate110:
		return "110"
	case BaudRate300:
		return "300"
	case BaudRate600:
		return "600"
	case BaudRate1200:
		return "1200"
	case BaudRate2400:
		return "2400"
	case BaudRate4800:
		return "4800"
	case BaudRate9600:
		return "9600"
	case BaudRate14400:
		return "14400"
	case BaudRate19200:
		return "19200"
	case BaudRate38400:
		return "38400"
	case BaudRate56000:
		return "56000"
	case BaudRate57600:
		return "57600"
	case BaudRate115200:
		return "115200"
	case BaudRate128000:
		return "128000"
	case BaudRate256000:
		return "256000"
	default:
		return UNKNOWN
	}
}

func (p BaudRateEnum) Value() int {
	switch p {
	case BaudRate110:
		return 110
	case BaudRate300:
		return 300
	case BaudRate600:
		return 600
	case BaudRate1200:
		return 1200
	case BaudRate2400:
		return 2400
	case BaudRate4800:
		return 4800
	case BaudRate9600:
		return 9600
	case BaudRate14400:
		return 14400
	case BaudRate19200:
		return 19200
	case BaudRate38400:
		return 38400
	case BaudRate56000:
		return 56000
	case BaudRate57600:
		return 57600
	case BaudRate115200:
		return 115200
	case BaudRate128000:
		return 128000
	case BaudRate256000:
		return 256000
	default:
		return 9600
	}
}

func (cm BaudRateMode) Key(label string) BaudRateEnum {
	switch strings.ToLower(label) {
	case "110":
		return BaudRate110
	case "300":
		return BaudRate300
	case "600":
		return BaudRate600
	case "1200":
		return BaudRate1200
	case "2400":
		return BaudRate2400
	case "4800":
		return BaudRate4800
	case "9600":
		return BaudRate9600
	case "14400":
		return BaudRate14400
	case "19200":
		return BaudRate19200
	case "38400":
		return BaudRate38400
	case "56000":
		return BaudRate56000
	case "57600":
		return BaudRate57600
	case "115200":
		return BaudRate115200
	case "128000":
		return BaudRate128000
	case "256000":
		return BaudRate256000
	default:
		return -1
	}
}

func (cm BaudRateMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: BaudRate110, Label: "110"},
		{Key: BaudRate300, Label: "300"},
		{Key: BaudRate600, Label: "600"},
		{Key: BaudRate1200, Label: "1200"},
		{Key: BaudRate2400, Label: "2400"},
		{Key: BaudRate4800, Label: "4800"},
		{Key: BaudRate9600, Label: "9600"},
		{Key: BaudRate14400, Label: "14400"},
		{Key: BaudRate19200, Label: "19200"},
		{Key: BaudRate38400, Label: "38400"},
		{Key: BaudRate56000, Label: "56000"},
		{Key: BaudRate57600, Label: "57600"},
		{Key: BaudRate115200, Label: "115200"},
		{Key: BaudRate128000, Label: "128000"},
		{Key: BaudRate256000, Label: "256000"},
	}
}

//** Parity

type ParityMode struct {
}

type ParityEnum int

const (
	ParityNone ParityEnum = iota
	ParityOdd
	ParityEven
	ParityMark
	ParitySpace
)

func (p ParityEnum) IsValid() bool {
	return p.String() != UNKNOWN
}

func (p ParityEnum) String() string {
	switch p {
	case ParityNone:
		return "None"
	case ParityOdd:
		return "Odd"
	case ParityEven:
		return "Even"
	case ParityMark:
		return "Mark"
	case ParitySpace:
		return "Space"
	default:
		return UNKNOWN
	}
}

func (p ParityEnum) Value() int {
	return int(p)
}

func (cm ParityMode) Key(label string) ParityEnum {
	switch strings.ToLower(label) {
	case "None":
		return ParityNone
	case "Odd":
		return ParityOdd
	case "Even":
		return ParityEven
	case "Mark":
		return ParityMark
	case "Space":
		return ParitySpace
	default:
		return -1
	}
}

func (cm ParityMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: ParityNone, Label: "None"},
		{Key: ParityOdd, Label: "Odd"},
		{Key: ParityEven, Label: "Even"},
		{Key: ParityMark, Label: "Mark"},
		{Key: ParitySpace, Label: "Space"},
	}
}

//** StopBits

type StopBitsMode struct {
}

type StopBitsEnum int

const (
	StopBitOne StopBitsEnum = iota
	StopBitsOnePointFive
	StopBitsTwo
)

func (p StopBitsEnum) IsValid() bool {
	return p.String() != UNKNOWN
}

func (p StopBitsEnum) String() string {
	switch p {
	case StopBitOne:
		return "One"
	case StopBitsOnePointFive:
		return "OnePointFive"
	case StopBitsTwo:
		return "Two"
	default:
		return UNKNOWN
	}
}

func (p StopBitsEnum) Value() int {
	return int(p)
}

func (cm StopBitsMode) Key(label string) StopBitsEnum {
	switch strings.ToLower(label) {
	case "One":
		return StopBitOne
	case "OnePointFive":
		return StopBitsOnePointFive
	case "Two":
		return StopBitsTwo
	default:
		return -1
	}
}

func (cm StopBitsMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: StopBitOne, Label: "One"},
		{Key: StopBitsOnePointFive, Label: "OnePointFive"},
		{Key: StopBitsTwo, Label: "Two"},
	}
}

//** DataBits

type DataBitsMode struct {
}

type DataBitsEnum int

const (
	DataBitsFive DataBitsEnum = iota
	DataBitsSix
	DataBitsSeven
	DataBitsEight
)

func (p DataBitsEnum) IsValid() bool {
	return p.String() != UNKNOWN
}

func (p DataBitsEnum) String() string {
	switch p {
	case DataBitsFive:
		return "Five"
	case DataBitsSix:
		return "Six"
	case DataBitsSeven:
		return "Seven"
	case DataBitsEight:
		return "Eight"
	default:
		return UNKNOWN
	}
}

func (p DataBitsEnum) Value() int {
	switch p {
	case DataBitsFive:
		return 5
	case DataBitsSix:
		return 6
	case DataBitsSeven:
		return 7
	case DataBitsEight:
		return 8
	default:
		return 8
	}
}

func (cm DataBitsMode) Key(label string) DataBitsEnum {
	switch strings.ToLower(label) {
	case "Five":
		return DataBitsFive
	case "Six":
		return DataBitsSix
	case "Seven":
		return DataBitsSeven
	case "Eight":
		return DataBitsEight
	default:
		return -1
	}
}

func (cm DataBitsMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: DataBitsFive, Label: "Five"},
		{Key: DataBitsSix, Label: "Six"},
		{Key: DataBitsSeven, Label: "Seven"},
		{Key: DataBitsEight, Label: "Eight"},
	}
}

//** FlowControl

type FlowControlMode struct {
}

type FlowControlEnum int

const (
	FlowNone FlowControlEnum = iota
	FlowSoft
	FlowHard
	FlowBoth
)

func (p FlowControlEnum) IsValid() bool {
	return p.String() != UNKNOWN
}

func (p FlowControlEnum) String() string {
	switch p {
	case FlowNone:
		return "None"
	case FlowSoft:
		return "Soft"
	case FlowHard:
		return "Hard"
	case FlowBoth:
		return "Both"
	default:
		return UNKNOWN
	}
}

func (cm FlowControlMode) Key(label string) FlowControlEnum {
	switch strings.ToLower(label) {
	case "None":
		return FlowNone
	case "Soft":
		return FlowSoft
	case "Hard":
		return FlowHard
	case "Both":
		return FlowBoth
	default:
		return -1
	}
}

func (cm FlowControlMode) GetOptions() []ItemOption {
	return []ItemOption{
		{Key: FlowNone, Label: "None"},
		{Key: FlowSoft, Label: "Soft"},
		{Key: FlowHard, Label: "Hard"},
		{Key: FlowBoth, Label: "Both"},
	}
}
