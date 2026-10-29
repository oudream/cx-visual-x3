package channels

import (
	"net"
)

type UdpS struct {
	IP   net.IP
	Port int
	Conn *net.UDPConn
}

type UdpSRecData struct {
	IPAddr net.UDPAddr
	Data   []byte
}

func NewUdpS(ip string, port int, rc chan UdpSRecData) (*UdpS, error) {
	if ip == "" || ip == "127.0.0.1" {
		ip = "0.0.0.0"
	}
	udpS := UdpS{
		IP:   net.ParseIP(ip),
		Port: port,
	}
	addr := net.UDPAddr{
		Port: udpS.Port,
		IP:   udpS.IP,
	}
	ser, err1 := net.ListenUDP("udp", &addr)
	if err1 != nil {
		// Logger.Printf(_titleMons.Error+"net.ListenUDP(%s:%d) error %v\n", ip, port, err1)
		return nil, err1
	}
	udpS.Conn = ser
	go func() {
		for {
			p := make([]byte, 4096)
			n, remoteAddr, err2 := ser.ReadFromUDP(p)
			if err2 != nil {
				rc <- UdpSRecData{
					IPAddr: net.UDPAddr{
						Port: 0,
						Zone: err2.Error(),
					},
				}
				// fmt.Printf("Some error  %v", err2)
			} else if n > 0 && n < 4097 {
				rc <- UdpSRecData{
					IPAddr: *remoteAddr,
					Data:   p[0:n],
				}
			}
		}
	}()
	return &udpS, nil
}

func (m *UdpS) Send(data []byte, addr *net.UDPAddr) (n int, err error) {
	n, err = m.Conn.WriteToUDP(data, addr)
	return
}

func (m *UdpS) Close() {
	_ = m.Conn.Close()
}
