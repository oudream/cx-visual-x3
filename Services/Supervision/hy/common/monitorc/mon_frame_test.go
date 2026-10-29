package monitorc

import (
    "bytes"
    "testing"
)

type migrationFrameSink struct {
    count int
    code uint32
    data []byte
}

func (s *migrationFrameSink) DealMonFrameData(code uint32, data []byte) {
    s.count++
    s.code = code
    s.data = append([]byte(nil), data...)
}

func TestOriginalFrameRoundTrip(t *testing.T) {
    data := []byte("CxVisual-monitor")
    const code uint32 = CCodeProgramHeartbeatReq
    frame := UMarshal(code, data)
    if len(frame) == 0 { t.Fatal("marshal unexpectedly failed") }
    sink := &migrationFrameSink{}
    CreateMonFrame(sink).Unmarshal(frame)
    if sink.count != 1 || sink.code != code || !bytes.Equal(sink.data, data) {
        t.Fatalf("unexpected decode: count=%d code=%x data=%q", sink.count, sink.code, sink.data)
    }
}

func TestOriginalFrameRejectsBadCRC(t *testing.T) {
    frame := UMarshal(CCodeProgramHeartbeatReq, []byte("corrupt"))
    frame[len(frame)-1] ^= 0xff
    sink := &migrationFrameSink{}
    CreateMonFrame(sink).Unmarshal(frame)
    if sink.count != 0 { t.Fatalf("invalid CRC was accepted: %d frames", sink.count) }
}

func TestOriginalFrameRejectsOversizedPayload(t *testing.T) {
    if got := UMarshal(CCodeProgramHeartbeatReq, make([]byte, MaxPacketLen)); len(got) != 0 {
        t.Fatalf("oversized frame accepted: %d bytes", len(got))
    }
}
