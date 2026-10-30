// Package stopjournal owns a bounded, local, append-only record of Daemon
// stop attempts. It is diagnostic evidence, not S11 authorization or a
// process state machine. There are no remote or mutating API routes.
package stopjournal

import (
	"bufio"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"sync"
	"time"
)

const MaxBytes int64 = 4 * 1024 * 1024
const MaxHistory = 200

var writeMu sync.Mutex

type Entry struct {
	OperationID string `json:"operationId"`
	ProgramID string `json:"programId"`
	PID int `json:"pid"`
	Action string `json:"action"`
	State string `json:"state"`
	WaitCompleted bool `json:"waitCompleted"`
	SignalError string `json:"signalError,omitempty"`
	WaitError string `json:"waitError,omitempty"`
	RecordedAt string `json:"recordedAt"`
}

type History struct {
	Available bool `json:"available"`
	Error string `json:"error,omitempty"`
	Items []Entry `json:"items"`
}

// NewRequest creates an operation identity independent of PID reuse.
// ProgramId is a STRING to avoid JavaScript's int64 precision loss.
func NewRequest(id int64, pid int, action string) (Entry, error) {
	if id <= 0 || pid <= 0 || action == "" {
		return Entry{}, errors.New("invalid stop operation")
	}
	var token [16]byte
	if _, err := rand.Read(token[:]); err != nil {
		return Entry{}, err
	}
	return Entry{
		OperationID: hex.EncodeToString(token[:]),
		ProgramID: strconv.FormatInt(id, 10), PID: pid,
		Action: action, State: "requested",
		RecordedAt: time.Now().UTC().Format(time.RFC3339Nano),
	}, nil
}

func Path(workDir string) string {
	return filepath.Join(workDir, "supervision", "daemon-stop-operations.ndjson")
}

// Append must finish its file Sync before the first Kill request.
// On IO error, the Daemon must REFUSE the operation rather than silently
// produce a stop without durable initial evidence.
func Append(workDir string, entry Entry) error {
	if err := validate(entry); err != nil {
		return err
	}
	data, err := json.Marshal(entry)
	if err != nil {
		return err
	}
	if len(data) > 4096 {
		return errors.New("stop entry exceeds limit")
	}
	writeMu.Lock()
	defer writeMu.Unlock()
	dir := filepath.Dir(Path(workDir))
	if err := os.MkdirAll(dir, 0700); err != nil {
		return err
	}
	dirInfo, err := os.Lstat(dir)
	if err != nil || !dirInfo.IsDir() {
		return errors.New("unsafe stop journal directory")
	}
	target := Path(workDir)
	previous, statErr := os.Lstat(target)
	if statErr == nil {
		if !previous.Mode().IsRegular() {
			return errors.New("stop journal is not a regular file")
		}
	} else if !errors.Is(statErr, os.ErrNotExist) {
		return statErr
	}
	file, err := os.OpenFile(target, os.O_WRONLY|os.O_CREATE|os.O_APPEND, 0600)
	if err != nil {
		return err
	}
	defer file.Close()
	info, err := file.Stat()
	if err != nil || !info.Mode().IsRegular() {
		return errors.New("unsafe opened stop journal")
	}
	if statErr == nil && !os.SameFile(previous, info) {
		return errors.New("stop journal file replaced during open")
	}
	if info.Size()+int64(len(data))+1 > MaxBytes {
		return errors.New("stop journal full: archive offline before further stops")
	}
	if _, err := file.Write(append(data, '\n')); err != nil {
		return err
	}
	return file.Sync()
}

func validate(entry Entry) error {
	if len(entry.OperationID) != 32 || entry.ProgramID == "" || entry.PID <= 0 ||
		entry.Action == "" || len(entry.Action) > 80 || len(entry.SignalError) > 1200 ||
		len(entry.WaitError) > 1200 {
		return errors.New("invalid stop journal entry")
	}
	for _, ch := range entry.OperationID {
		if (ch < '0' || ch > '9') && (ch < 'a' || ch > 'f') {
			return errors.New("invalid operation id")
		}
	}
	id, err := strconv.ParseInt(entry.ProgramID, 10, 64)
	if err != nil || id <= 0 || strconv.FormatInt(id, 10) != entry.ProgramID {
		return errors.New("invalid Program ID")
	}
	switch entry.State {
	case "requested", "unknown", "refused", "signal-failed", "wait-confirmed":
	default:
		return errors.New("invalid stop state")
	}
	if entry.WaitCompleted != (entry.State == "wait-confirmed") {
		return errors.New("stop Wait confirmation disagrees with state")
	}
	if _, err := time.Parse(time.RFC3339Nano, entry.RecordedAt); err != nil {
		return errors.New("invalid stop observation time")
	}
	return nil
}

func rank(state string) int {
	switch state {
	case "wait-confirmed": return 3
	case "refused", "signal-failed": return 2
	case "unknown": return 1
	default: return 0
	}
}

// Read combines events of each operation. A delayed "unknown" can never
// overwrite an earlier late Wait confirmation for that SAME operation.
// Unreadable/corrupt/full logs are UNAVAILABLE, not an empty healthy list.
func Read(workDir string) History {
	history := History{Items: []Entry{}}
	path := Path(workDir)
	before, err := os.Lstat(path)
	if err != nil {
		history.Error = err.Error()
		return history
	}
	if !before.Mode().IsRegular() || before.Size() > MaxBytes {
		history.Error = "unsafe or oversized stop journal"
		return history
	}
	file, err := os.Open(path)
	if err != nil {
		history.Error = err.Error()
		return history
	}
	defer file.Close()
	after, err := file.Stat()
	if err != nil || !after.Mode().IsRegular() || !os.SameFile(before, after) {
		history.Error = "stop journal changed during open"
		return history
	}
	reader := bufio.NewReader(io.LimitReader(file, MaxBytes+1))
	ops := map[string]Entry{}
	order := map[string]int{}
	idx := 0
	for {
		line, err := reader.ReadBytes('\n')
		if err == io.EOF && len(line) == 0 { break }
		if err != nil && err != io.EOF {
			history.Error = err.Error()
			return history
		}
		if len(line) > 4097 || len(line) == 0 || line[len(line)-1] != '\n' {
			history.Error = "partial or oversized stop journal record"
			return history
		}
		var row Entry
		if json.Unmarshal(line, &row) != nil || validate(row) != nil {
			history.Error = "invalid stop journal record"
			return history
		}
		if previous, exists := ops[row.OperationID]; exists {
			if previous.ProgramID != row.ProgramID || previous.PID != row.PID ||
				previous.Action != row.Action {
				history.Error = "stop operation identity changed within journal"
				return history
			}
			if rank(row.State) >= rank(previous.State) {
				ops[row.OperationID] = row
			}
		} else {
			ops[row.OperationID] = row
		}
		order[row.OperationID] = idx
		idx++
		if err == io.EOF { break }
	}
	type indexed struct { row Entry; order int }
	sorted := make([]indexed, 0, len(ops))
	for key, value := range ops {
		sorted = append(sorted, indexed{value, order[key]})
	}
	sort.Slice(sorted, func(i, j int) bool { return sorted[i].order > sorted[j].order })
	for _, item := range sorted {
		history.Items = append(history.Items, item.row)
		if len(history.Items) >= MaxHistory { break }
	}
	history.Available = true
	return history
}

// RecordReceipt clones the initial identity and appends its latest outcome.
func RecordReceipt(workDir string, start Entry, state string, confirmed bool, signalErr string, waitErr string) error {
	row := start
	row.State = state
	row.WaitCompleted = confirmed
	row.SignalError = signalErr
	row.WaitError = waitErr
	row.RecordedAt = time.Now().UTC().Format(time.RFC3339Nano)
	if err := Append(workDir, row); err != nil {
		return fmt.Errorf("durable stop receipt %s: %w", start.OperationID, err)
	}
	return nil
}
