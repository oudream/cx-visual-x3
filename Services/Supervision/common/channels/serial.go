package channels

import (
	"fmt"
	"github.com/oudream/go-serial"
	"io"
	"net"
)

// https://github.com/bugst/go-serial
// https://github.com/goburrow/serial
// https://github.com/tarm/serial

type Serial struct {
	Mode serial.Mode
	Port serial.Port
}

func NewSerial(portName string, mode *serial.Mode, rc chan CRecData) (*Serial, error) {
	port, err := serial.Open(portName, mode)
	if err != nil {
		return nil, fmt.Errorf("serial.Open PortName=%s, Mode=[%+v], err=%s", portName, mode, err)
	}

	r := Serial{
		Mode: *mode,
		Port: port,
	}

	go func() {
		for {
			// Set a deadline for reading. Read operation will fail if no data
			// is received after deadline.
			p := make([]byte, 4096)
			// Read tokens delimited by newline
			n, err := port.Read(p)
			if err != nil {
				//if opErr, ok := err.(*net.OpError); ok && opErr.Timeout() {
				//	continue
				//}
				// TcpS.Close
				if opErr, ok := err.(*net.OpError); ok && opErr.Err == net.ErrClosed {
					rc <- CRecData{
						Err: opErr.Err,
					}
					break
				}
				if err != io.EOF || n == 0 {
					rc <- CRecData{
						Err: err,
					}
					break
				}
			} else if n > 0 && n < 4097 {
				rc <- CRecData{
					Data: p[0:n],
				}
			}
		}
	}()

	return &r, nil
}

func (m *Serial) Send(data []byte) (n int, err error) {
	n, err = m.Port.Write(data)
	return
}

func (m *Serial) Close() error {
	return m.Port.Close()
}

func (m *Serial) IsOpened() bool {
	return m.Port != nil
}

func IsValidSerialName(name string) bool {
	return len(name) > 3
}

func GetSerialNames() ([]string, error) {
	return serial.GetPortsList()
}
