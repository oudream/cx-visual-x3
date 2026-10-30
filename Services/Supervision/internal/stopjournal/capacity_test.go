package stopjournal

import (
	"encoding/json"
	"os"
	"strings"
	"testing"
)

func TestOutstandingReceiptsReserveCapacityBeforeNewStop(t *testing.T) {
	dir := t.TempDir()
	seed, _ := NewRequest(1, 100, "shutdown")
	if err := Append(dir, seed); err != nil {
		t.Fatal(err)
	}
	f, err := os.OpenFile(Path(dir), os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		t.Fatal(err)
	}
	// Legacy logs can have many unresolved operations. New requests must
	// leave room for their already-authorized outcomes, even below MaxBytes.
	for i := 0; i < int(MaxBytes/(2*4097)); i++ {
		row, _ := NewRequest(int64(i+2), i+102, "legacy-unresolved")
		data, _ := json.Marshal(row)
		if _, err := f.Write(append(data, '\n')); err != nil {
			t.Fatal(err)
		}
	}
	if err := f.Close(); err != nil {
		t.Fatal(err)
	}
	next, _ := NewRequest(9999, 234, "shutdown")
	if err := Append(dir, next); err == nil || !strings.Contains(err.Error(), "reserved") {
		t.Fatalf("request consumed receipt budget: %v", err)
	}
	if err := RecordReceipt(dir, seed, "wait-confirmed", true, "", ""); err != nil {
		t.Fatalf("reserved outcome refused: %v", err)
	}
	if !Read(dir).Available {
		t.Fatal("capacity refusal corrupted history")
	}
}

func BenchmarkAppendBoundedHistory(b *testing.B) {
	dir := b.TempDir()
	seed, _ := NewRequest(1, 100, "seed")
	if err := Append(dir, seed); err != nil {
		b.Fatal(err)
	}
	f, err := os.OpenFile(Path(dir), os.O_APPEND|os.O_WRONLY, 0600)
	if err != nil {
		b.Fatal(err)
	}
	for i := 0; i < 2300; i++ {
		row, _ := NewRequest(int64(i+2), i+102, "completed")
		data, _ := json.Marshal(row)
		if _, err := f.Write(append(data, '\n')); err != nil {
			b.Fatal(err)
		}
		row.State = "wait-confirmed"
		row.WaitCompleted = true
		row.WaitError = strings.Repeat("x", 1000)
		data, _ = json.Marshal(row)
		if _, err := f.Write(append(data, '\n')); err != nil {
			b.Fatal(err)
		}
	}
	if err := f.Close(); err != nil {
		b.Fatal(err)
	}
	info, _ := os.Stat(Path(dir))
	b.SetBytes(info.Size())
	b.ReportAllocs()
	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		if err := RecordReceipt(dir, seed, "wait-confirmed", true, "", ""); err != nil {
			b.Fatal(err)
		}
	}
}
