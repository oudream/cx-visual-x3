package channels

import (
	"bufio"
	"fmt"
	"io"
	"net"
)

type TcpS struct {
	IP       net.IP
	Port     int
	Listener net.Listener
	// [Conn]=flag , conn's manage in main thread ( Communicator's thread ）
	ConnMap map[net.Conn]int
}

type TcpSAccept struct {
	Conn net.Conn
}

type TcpSRecData struct {
	Err  error
	Conn net.Conn
	Data []byte
}

type TcpSErrorDeal func(ip string, port int, err error)

func NewTcpS(ip string, port int, eDeal TcpSErrorDeal, ca chan TcpSAccept, crd chan TcpSRecData) (*TcpS, error) {
	if ip == "" || ip == "127.0.0.1" {
		ip = "0.0.0.0"
	}
	tcpS := TcpS{
		IP:      net.ParseIP(ip),
		Port:    port,
		ConnMap: make(map[net.Conn]int),
	}
	listener, err1 := net.Listen("tcp", fmt.Sprintf("%s:%d", ip, port))
	if err1 != nil {
		// Logger.Printf(_titleMons.Error+"net.ListenUDP(%s:%d) error %v\n", ip, port, err1)
		return nil, err1
	}
	tcpS.Listener = listener
	go func() {
		for {
			// Get net.TCPConn object
			conn, err := listener.Accept()
			if err != nil {
				if eDeal != nil {
					if opErr, ok := err.(*net.OpError); ok && opErr.Err == net.ErrClosed {
						eDeal(ip, port, opErr.Err)
					} else {
						eDeal(ip, port, err)
					}
				}
				// fmt.Printf("%s:%d listener.Accept err: %s", ip, port, err)
				break
			}

			ca <- TcpSAccept{
				Conn: conn,
			}
			//tcpS.ConnMap[conn] = 1
			go handleConnection(conn, crd)
		}
	}()
	return &tcpS, nil
}

func (m *TcpS) Close() {
	if m.Listener != nil {
		_ = m.Listener.Close()
		m.Listener = nil
	}
	for conn := range m.ConnMap {
		_ = conn.Close()
	}
	m.ConnMap = make(map[net.Conn]int)
}

func (m *TcpS) CloseConn(conn net.Conn) {
	// conn.(*net.TCPConn).CloseRead()
	if _, ok := m.ConnMap[conn]; ok {
		delete(m.ConnMap, conn)
		_ = conn.Close()
	}
}

func (m *TcpS) CloseTag(tag int) {
	for conn, v := range m.ConnMap {
		if v == tag {
			delete(m.ConnMap, conn)
			_ = conn.Close()
		}
	}
}

func handleConnection(conn net.Conn, rc chan TcpSRecData) {
	// Close connection when this function ends
	//defer func() {
	//	_ = conn.Close()
	//}()

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
				rc <- TcpSRecData{
					Err:  opErr.Err,
					Conn: conn,
				}
				break
			}
			if err != io.EOF || n == 0 {
				rc <- TcpSRecData{
					Err:  err,
					Conn: conn,
				}
				break
			}
		} else if n > 0 && n < 4097 {
			rc <- TcpSRecData{
				Conn: conn,
				Data: p[0:n],
			}
		}
	}
}

func (m *TcpS) SendAll(data []byte) (n int) {
	for conn := range m.ConnMap {
		if t, _ := conn.Write(data); t > 0 {
			n++
		}
	}
	return
}

func (m *TcpS) Send(data []byte, tag int) (n int, err error) {
	for k, v := range m.ConnMap {
		if v == tag {
			return k.Write(data)
		}
	}
	return
}
