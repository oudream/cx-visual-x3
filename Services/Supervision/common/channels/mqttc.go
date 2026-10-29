package channels

// https://github.com/eclipse/paho.mqtt.golang/blob/master/cmd/docker/subscriber/main.go

import (
	"fmt"
	"gogo/common/utilities"
	"log"
	"time"

	mqtt "github.com/eclipse/paho.mqtt.golang"
)

var (
	_titleMqttC = utilities.NewLoggerTitles("MQTTC")
)

const (
	MqttCServerAddress = "tcp://%s:%d"
)

type MqttCOption struct {
	Host     string
	Port     int
	ClientId string
	Username string
	Password string
}

// MqttCDeal Qos 0：消息最多传递一次，如果当时客户端不可用，则会丢失该消息。
// MqttCDeal Qos 1：消息传递至少 1 次。
// MqttCDeal Qos 2：消息仅传送一次。
type MqttCDeal struct {
	Topic string
	Qos   byte
	Deal  mqtt.MessageHandler
}

// MqttC is a simple struct that provides a function to be called when a message is received. The message is parsed
// and the count followed by the raw message is written to the file (this makes it easier to sort the file)
type MqttC struct {
	Option      MqttCOption
	Write2disk  bool
	Client      mqtt.Client
	Logger      *log.Logger
	defaultDeal mqtt.MessageHandler
	deals       []MqttCDeal
}

// NewMqttC
// defaultDeal is called when a message is received
//
//	func (o *MqttC) defaultDeal(_ mqtt.Client, msg mqtt.Message) {
//		if o.out != nil {
//			// Write out the number (make it long enough that sorting works) and the payload
//			if _, err := o.out.WriteString(fmt.Sprintf("%s: %s\n", msg.Topic(), msg.Payload())); err != nil {
//				fmt.Println(fmt.Sprintf("MQTT31.dealM31IOTPayload - ERROR writing to file: %s", err))
//			}
//		}
//	}
func NewMqttC(option MqttCOption, logger *log.Logger, defaultDeal mqtt.MessageHandler, deals []MqttCDeal) *MqttC {
	o := &MqttC{
		Option:      option,
		Write2disk:  false,
		Logger:      logger,
		defaultDeal: defaultDeal,
		deals:       deals,
	}

	// Create a MqttC that will deal with incoming messages
	//h, err1 := M31NewHandler()
	//defer h.Close()

	// Now we establish the connection to the mqtt broker
	opts := mqtt.NewClientOptions()
	opts.AddBroker(fmt.Sprintf(MqttCServerAddress, o.Option.Host, o.Option.Port))
	opts.SetClientID(o.Option.ClientId)
	opts.SetUsername(o.Option.Username)
	opts.SetPassword(o.Option.Password)

	opts.SetOrderMatters(false)       // Allow out of order messages (use this Option unless in order delivery is essential)
	opts.ConnectTimeout = time.Second // Minimal delays on connect
	opts.WriteTimeout = time.Second   // Minimal delays on writes
	opts.KeepAlive = 10               // Keepalive every 10 seconds so we quickly detect network outages
	opts.PingTimeout = time.Second    // local broker so response should be quick

	// Automate connection management (will keep trying to connect and will reconnect if network drops)
	opts.ConnectRetry = true
	opts.AutoReconnect = true

	// If using QOS2 and CleanSession = FALSE then it is possible that we will receive messages on topics that we
	// have not subscribed to here (if they were previously subscribed to they are part of the session and survive
	// disconnect/reconnect). Adding a DefaultPublishHandler lets us detect this.
	// Logger.Println(fmt.Sprintf("MQTT31 - UNEXPECTED MESSAGE: %s", msg))
	opts.DefaultPublishHandler = o.defaultDeal

	// Log events
	opts.OnConnectionLost = func(cl mqtt.Client, err error) {
		o.Logger.Println(_titleMqttC.Error + "Connection Lost")
	}

	// connect established
	opts.OnConnect = func(c mqtt.Client) {
		o.Logger.Printf(_titleMqttC.Info+"Connection Success [ tcp://%s:%d ]\n", o.Option.Host, o.Option.Port)
		for i := 0; i < len(o.deals); i++ {
			d := o.deals[i]
			token := c.Subscribe(d.Topic, d.Qos, d.Deal)
			if token != nil {
				go func() {
					// Can also use '<-t.Done()' in releases > 1.2.0
					_ = token.Wait()
					if token.Error() != nil {
						o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : %s", d.Topic, d.Qos, token.Error())
					} else {
						o.Logger.Printf(_titleMqttC.Info+"Subscribe Success [ topic=%s qos=%d ]", d.Topic, d.Qos)
					}
				}()
			}
		}
	}

	opts.OnReconnecting = func(mqtt.Client, *mqtt.ClientOptions) {
		o.Logger.Println(_titleMqttC.Info + "Attempting to reconnect")
	}

	//
	// Connect to the broker
	//
	o.Client = mqtt.NewClient(opts)

	return o
}

func (o *MqttC) Deal(topic string, qos byte, deal mqtt.MessageHandler) {
	o.deals = append(o.deals, MqttCDeal{
		Topic: topic,
		Qos:   qos,
		Deal:  deal,
	})
	if o.Client.IsConnected() {
		token := o.Client.Subscribe(topic, qos, deal)
		if token != nil {
			go func() {
				// Can also use '<-t.Done()' in releases > 1.2.0
				_ = token.Wait()
				if token.Error() != nil {
					o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : %s", topic, qos, token.Error())
				} else {
					o.Logger.Printf(_titleMqttC.Debug+"Subscribe Success [ topic=%s qos=%d ]", topic, qos)
				}
			}()
		} else {
			o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : token is nil", topic, qos)
		}
	} else {
		o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : client is not connected", topic, qos)
	}
}

func (o *MqttC) DealWait(topic string, qos byte, deal mqtt.MessageHandler, timeout time.Duration) bool {
	o.deals = append(o.deals, MqttCDeal{
		Topic: topic,
		Qos:   qos,
		Deal:  deal,
	})
	if o.Client.IsConnected() {
		token := o.Client.Subscribe(topic, qos, deal)
		if token != nil {
			if token.WaitTimeout(timeout) {
				o.Logger.Printf(_titleMqttC.Debug+"Subscribe Success [ topic=%s qos=%d ]", topic, qos)
				return true
			} else {
				o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : %s", topic, qos, token.Error())
				return false
			}
		} else {
			o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : token is nil", topic, qos)
		}
	} else {
		o.Logger.Printf(_titleMqttC.Error+"Subscribe Fail [ topic=%s qos=%d ] : client is not connected", topic, qos)
	}
	return false
}

func (o *MqttC) UnDeal(topic string) {
	var deals []MqttCDeal
	for i := 0; i < len(o.deals); i++ {
		if o.deals[i].Topic != topic {
			deals = append(deals, o.deals[i])
		}
	}
	o.deals = deals
	if o.Client.IsConnected() {
		token := o.Client.Unsubscribe(topic)
		if token != nil {
			go func() {
				// Can also use '<-t.Done()' in releases > 1.2.0
				_ = token.Wait()
				if token.Error() != nil {
					o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : %s", topic, token.Error())
				} else {
					o.Logger.Printf(_titleMqttC.Debug+"Unsubscribe Success [ topic=%s ]", topic)
				}
			}()
		} else {
			o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : token is nil", topic)
		}
	} else {
		o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : client is not connected", topic)
	}
}

func (o *MqttC) UnDealWait(topic string, timeout time.Duration) bool {
	var deals []MqttCDeal
	for i := 0; i < len(o.deals); i++ {
		if o.deals[i].Topic != topic {
			deals = append(deals, o.deals[i])
		}
	}
	o.deals = deals
	if o.Client.IsConnected() {
		token := o.Client.Unsubscribe(topic)
		if token != nil {
			if token.WaitTimeout(timeout) {
				o.Logger.Printf(_titleMqttC.Debug+"Unsubscribe Success [ topic=%s ]", topic)
				return true
			} else {
				o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : %s", topic, token.Error())
				return false
			}
		} else {
			o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : token is nil", topic)
		}
	} else {
		o.Logger.Printf(_titleMqttC.Error+"Unsubscribe Fail [ topic=%s ] : client is not connected", topic)
	}
	return false
}

func (o *MqttC) Open() {
	if !o.Client.IsConnected() {
		o.Logger.Printf(_titleMqttC.Info+"Connect [ tcp://%s:%d ] ..\n", o.Option.Host, o.Option.Port)

		if token := o.Client.Connect(); token.WaitTimeout(1 * time.Second) {
			o.Logger.Println("Connection is up")
		} else {
			o.Logger.Printf(_titleMqttC.Error+"Connect Fail: %s\n", token.Error())
		}
	}
}

// Close closes the file
func (o *MqttC) Close() {
	o.Client.Disconnect(3000)
}

func (o *MqttC) IsOpened() bool {
	return o.Client.IsConnected()
}
