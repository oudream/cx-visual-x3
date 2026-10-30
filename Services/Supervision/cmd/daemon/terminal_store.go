package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"

	"gogo/internal/program"
)

// terminalLatchFile is a local Daemon safety journal, not a Program DB or
// protocol. JSON map keys preserve all int64 Program IDs without JS rounding.
type terminalLatchFile struct {
	Version  int           `json:"version"`
	Terminal map[int64]int `json:"terminal"`
}

const terminalLatchVersion = 1
const terminalLatchMaxBytes = 1024 * 1024

func readTerminalLatches(path string) (map[int64]int, error) {
	data, err := os.ReadFile(path)
	if errors.Is(err, os.ErrNotExist) {
		return make(map[int64]int), nil
	}
	if err != nil {
		return nil, err
	}
	if len(data) == 0 || len(data) > terminalLatchMaxBytes {
		return nil, fmt.Errorf("invalid terminal latch file size: %d", len(data))
	}
	var state terminalLatchFile
	decoder := json.NewDecoder(bytes.NewReader(data))
	decoder.DisallowUnknownFields()
	if err := decoder.Decode(&state); err != nil {
		return nil, fmt.Errorf("terminal latch decode: %w", err)
	}
	if err := decoder.Decode(new(interface{})); err != io.EOF {
		return nil, errors.New("terminal latch file contains trailing JSON")
	}
	if state.Version != terminalLatchVersion || state.Terminal == nil {
		return nil, fmt.Errorf("unsupported terminal latch schema: %d", state.Version)
	}
	for id, reason := range state.Terminal {
		if id <= 0 || (reason != program.EventMaxError && reason != program.EventMaxCount) {
			return nil, fmt.Errorf("invalid terminal latch entry: id=%d, reason=%d", id, reason)
		}
	}
	return state.Terminal, nil
}

// writeTerminalLatches writes a complete snapshot via a same-directory temp
// file, Sync, and Rename. On any failure the caller must fail closed.
func writeTerminalLatches(path string, terminal map[int64]int) error {
	for id, reason := range terminal {
		if id <= 0 || (reason != program.EventMaxError && reason != program.EventMaxCount) {
			return fmt.Errorf("refuse invalid terminal latch: id=%d reason=%d", id, reason)
		}
	}
	data, err := json.Marshal(terminalLatchFile{Version: terminalLatchVersion, Terminal: terminal})
	if err != nil {
		return err
	}
	if len(data) > terminalLatchMaxBytes {
		return errors.New("terminal latch file exceeds size limit")
	}
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0700); err != nil {
		return err
	}
	temp, err := os.CreateTemp(dir, ".terminal-latches-*")
	if err != nil {
		return err
	}
	defer os.Remove(temp.Name())
	if err := temp.Chmod(0600); err != nil {
		temp.Close()
		return err
	}
	if _, err := temp.Write(data); err != nil {
		temp.Close()
		return err
	}
	if err := temp.Sync(); err != nil {
		temp.Close()
		return err
	}
	if err := temp.Close(); err != nil {
		return err
	}
	if err := os.Rename(temp.Name(), path); err != nil {
		return err
	}
	// Directory fsync is available on Unix, but cannot be relied on for
	// every filesystem/Windows. File Sync and atomic replace are mandatory.
	return nil
}
