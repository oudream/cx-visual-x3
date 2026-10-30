package channels

import (
	"bufio"
	"net"
	"strconv"
	"time"
)

type UdpC struct {
	IP   string
	Port int
	Conn net.Conn
}

func NewUdpC(ip string, port int, rc chan CRecData) (*UdpC, error) {
	udpC := UdpC{
		IP:   ip,
		Port: port,
	}
	conn, err := net.Dial("udp", net.JoinHostPort(udpC.IP, strconv.Itoa(udpC.Port)))
	if err != nil {
		//fmt.Printf("Some error %v", err)
		return nil, err
	}
	udpC.Conn = conn

	go func() {
		for {
			p := make([]byte, 4096)
			n, err := bufio.NewReader(conn).Read(p)
			if err != nil {
				//errString := err.Error()
				//switch {
				//case strings.Contains(errString, "timeout"):
				//case strings.Contains(errString, "No such host"):
				//case strings.Contains(errString, "Connection refused"):
				//}
				if opErr, ok := err.(*net.OpError); ok && opErr.Err == net.ErrClosed {

				} else {
					rc <- CRecData{
						Err: err,
					}
					time.Sleep(1 * time.Second)
				}

				//if opErr, ok := err.(*net.OpError); ok {
				//	if osErr, ok := opErr.Err.(*os.SyscallError); ok && osErr.Err == syscall.ECONNREFUSED {
				//		time.Sleep(1 * time.Second)
				//	}
				//}
				//fmt.Printf("Some error  %v", err)
			} else if n > 0 && n <= 4096 {
				rc <- CRecData{
					Data: p[0:n],
				}
			}
		}
	}()

	return &udpC, nil
}

func (m *UdpC) Send(data []byte) (int, error) {
	//if m.Conn.RemoteAddr() != nil {
	//	return m.Conn.Write(data)
	//}
	//return -1, errors.New("is not opened")
	return m.Conn.Write(data)
}

func (m *UdpC) Close() error {
	return m.Conn.Close()
}

func (m *UdpC) IsOpened() bool {
	return m.Conn.RemoteAddr() != nil
}
