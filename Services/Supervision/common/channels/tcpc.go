package channels

import (
	"bufio"
	"fmt"
	"io"
	"net"
)

type TcpC struct {
	LocalIp    string
	LocalPort  int
	RemoteIP   string
	RemotePort int
	Conn       *net.TCPConn
}

func NewTcpC(localIp string, localPort int, remoteIp string, remotePort int, rc chan CRecData) (*TcpC, error) {
	var lAddr *net.TCPAddr
	if len(localIp) > 0 && localPort > 0 {
		var err1 error
		lAddr, err1 = net.ResolveTCPAddr("tcp", fmt.Sprintf("%s:%d", localIp, localPort))
		if err1 != nil {
			return nil, err1
		}
	}
	rAddr, err2 := net.ResolveTCPAddr("tcp", fmt.Sprintf("%s:%d", remoteIp, remotePort))
	if err2 != nil {
		return nil, err2
	}
	conn, err3 := net.DialTCP("tcp", lAddr, rAddr)
	if err3 != nil {
		return nil, err3
	}
	tcpC := TcpC{
		LocalIp:    localIp,
		LocalPort:  localPort,
		RemoteIP:   remoteIp,
		RemotePort: remotePort,
		Conn:       conn,
	}

	go func() {
		//_ = conn.SetDeadline(time.Now().Add(200 * time.Millisecond))
		bufReader := bufio.NewReader(conn)

		for {
			// Set a deadline for reading. Read operation will fail if no data
			// is received after deadline.
			p := make([]byte, 4096)
			// Read tokens delimited by newline
			n, err := bufReader.Read(p)
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

	return &tcpC, nil
}

func (m *TcpC) Send(data []byte) (int, error) {
	return m.Conn.Write(data)
}

func (m *TcpC) Close() error {
	return m.Conn.Close()
}

func (m *TcpC) IsOpened() bool {
	return m.Conn.RemoteAddr() != nil
}
