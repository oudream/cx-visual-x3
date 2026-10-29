package utilities

// CodeCheckSum16 loop data and sum
func CodeCheckSum16(data []byte) uint16 {
	var (
		sum    uint32
		length int = len(data)
		index  int
	)
	for length > 1 {
		sum += uint32(data[index])<<8 + uint32(data[index+1])
		index += 2
		length -= 2
	}
	if length > 0 {
		sum += uint32(data[index])
	}
	sum += sum >> 16
	return uint16(^sum)
}

// CodeCrc16 Modbus CRC16
func CodeCrc16(bs []byte) uint16 {
	once.Do(initCrcTable)

	val := uint16(0xFFFF)
	for _, v := range bs {
		val = (val >> 8) ^ crtTable[(val^uint16(v))&0x00FF]
	}
	return val
}

func initCrcTable() {
	crcPoly16 := uint16(0xa001)
	crtTable = make([]uint16, 256)

	for i := uint16(0); i < 256; i++ {
		crc := uint16(0)
		b := i

		for j := uint16(0); j < 8; j++ {
			if ((crc ^ b) & 0x0001) > 0 {
				crc = (crc >> 1) ^ crcPoly16
			} else {
				crc >>= 1
			}
			b >>= 1
		}
		crtTable[i] = crc
	}
}
