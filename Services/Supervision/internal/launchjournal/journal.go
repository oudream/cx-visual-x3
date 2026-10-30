// Package launchjournal persists one child-start attempt BEFORE invoking
// exec.Cmd.Start. It is the Daemon's crash-recovery evidence, not a second
// process scheduler, remote control API, or OS process ownership source.
package launchjournal

import (
	"bytes"
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

const Version = 1
const MaxBytes = 8192

type Record struct {
	Version int    `json:"version"`
	ProgramID string `json:"programId"`
	AttemptID string `json:"attemptId"`
	Stage string `json:"stage"`
	PID int `json:"pid"`
	Path string `json:"path"`
	IntendedAt string `json:"intendedAt"`
	ObservedAt string `json:"observedAt,omitempty"`
}

type Journal struct {
	workDir string
	id int64
	active *Record
}

// New creates a private recorder for a SINGLE guardian Worker generation.
// The Daemon gate already guarantees one active Worker per Program ID.
func New(workDir string, id int64) *Journal {
	return &Journal{workDir: workDir, id: id}
}

func recordDir(workDir string) string {
	return filepath.Join(workDir, "supervision", "launch-attempts")
}

func recordPath(workDir string, id int64) string {
	return filepath.Join(recordDir(workDir), strconv.FormatInt(id, 10)+".json")
}

// Begin MUST succeed and synchronize its intent file before the Worker may
// invoke exec.Cmd.Start. An existing artifact is never overwritten silently.
func (j *Journal) Begin(id int64, path string) error {
	if j == nil || id <= 0 || id != j.id || path == "" || j.active != nil {
		return errors.New("invalid or overlapping launch intent")
	}
	var nonce [16]byte
	if _, err := rand.Read(nonce[:]); err != nil {
		return err
	}
	rec := &Record{Version: Version, ProgramID: strconv.FormatInt(id,10),
		AttemptID: hex.EncodeToString(nonce[:]), Stage: "intent", PID: 0,
		Path: path, IntendedAt: time.Now().UTC().Format(time.RFC3339Nano)}
	pathOnDisk := recordPath(j.workDir, id)
	if _, err := os.Lstat(pathOnDisk); err == nil {
		return errors.New("unresolved previous launch evidence requires manual reconciliation")
	} else if !errors.Is(err, os.ErrNotExist) {
		return err
	}
	if err := writeRecord(pathOnDisk, rec); err != nil {
		return err
	}
	j.active = rec
	return nil
}

// Started records the PID from the ORIGINAL exec.Cmd after Start succeeded.
// It is an observation, not yet a verified OS process creation generation.
func (j *Journal) Started(pid int) error {
	if j == nil || j.active == nil || j.active.Stage != "intent" || pid <= 0 {
		return errors.New("launch Started without valid durable intent")
	}
	rec := *j.active
	rec.Stage, rec.PID = "started", pid
	rec.ObservedAt = time.Now().UTC().Format(time.RFC3339Nano)
	if err := writeRecord(recordPath(j.workDir, j.id), &rec); err != nil {
		return err
	}
	j.active = &rec
	return nil
}

// Finished is called only when Start failed (no child) or the original
// Worker reaped the child with cmd.Wait. Failure keeps recovery conservative.
func (j *Journal) Finished() error {
	if j == nil || j.active == nil {
		return errors.New("launch Finished without active evidence")
	}
	path := recordPath(j.workDir, j.id)
	onDisk, err := readRecord(path)
	if err != nil {
		return err
	}
	if onDisk.AttemptID != j.active.AttemptID || onDisk.ProgramID != j.active.ProgramID {
		return errors.New("launch evidence changed between Start and Wait")
	}
	if err := os.Remove(path); err != nil {
		return err
	}
	j.active = nil
	return nil
}

func writeRecord(path string, rec *Record) error {
	data, err := json.Marshal(rec)
	if err != nil { return err }
	if len(data) > MaxBytes { return errors.New("launch evidence exceeds limit") }
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0700); err != nil { return err }
	// A redirected evidence directory could place safety records outside
	// the intended Daemon work directory. Reject symlinks/other file types.
	info, err := os.Lstat(dir)
	if err != nil || !info.IsDir() {
		return errors.New("unsafe launch evidence directory")
	}
	temp, err := os.CreateTemp(dir, ".launch-intent-*")
	if err != nil { return err }
	defer os.Remove(temp.Name())
	if err = temp.Chmod(0600); err != nil { temp.Close(); return err }
	if _,err = temp.Write(data); err != nil { temp.Close(); return err }
	if err = temp.Sync(); err != nil { temp.Close(); return err }
	if err = temp.Close(); err != nil { return err }
	return os.Rename(temp.Name(),path)
}

func readRecord(path string) (Record,error) {
	var rec Record
	info,err:=os.Lstat(path)
	if err != nil { return rec,err }
	if !info.Mode().IsRegular() || info.Size()<1 || info.Size()>MaxBytes {
		return rec,errors.New("unsafe launch evidence file")
	}
	f,err:=os.Open(path)
	if err!=nil{return rec,err}
	defer f.Close()
	opened,err:=f.Stat()
	if err!=nil || !opened.Mode().IsRegular() || !os.SameFile(info,opened) {
		return rec,errors.New("launch evidence changed while opening")
	}
	data,err:=io.ReadAll(io.LimitReader(f,MaxBytes+1))
	if err!=nil || len(data)==0 || len(data)>MaxBytes {
		return rec,errors.New("invalid launch evidence read")
	}
	dec:=json.NewDecoder(bytes.NewReader(data))
	dec.DisallowUnknownFields()
	if err=dec.Decode(&rec);err!=nil{return rec,err}
	if err=dec.Decode(new(interface{}));err!=io.EOF {
		return rec,errors.New("extra launch evidence JSON")
	}
	id,err:=strconv.ParseInt(rec.ProgramID,10,64)
	if err!=nil || id<=0 || strconv.FormatInt(id,10)!=rec.ProgramID ||
		rec.Version!=Version || len(rec.AttemptID)!=32 || strings.Trim(rec.AttemptID,"0123456789abcdef")!="" ||
		rec.Path=="" || (rec.Stage!="intent" && rec.Stage!="started") ||
		(rec.Stage=="intent" && rec.PID!=0) || (rec.Stage=="started" && rec.PID<=0) {
		return rec,errors.New("invalid launch evidence schema")
	}
	if _,err=time.Parse(time.RFC3339Nano,rec.IntendedAt);err!=nil {
		return rec,errors.New("invalid intent time")
	}
	if rec.Stage=="started" {
		if _,err=time.Parse(time.RFC3339Nano,rec.ObservedAt);err!=nil {
			return rec,errors.New("invalid PID observation time")
		}
	}
	return rec,nil
}

// Scan does not issue signals or infer ownership from old PIDs. Any
// uncompleted intent, including PID=0, blocks automatic start for its ID.
// Unknown filenames/corrupt directories are fatal: they cannot be safely
// attributed to a particular Program.
func Scan(workDir string) (map[int64]string,error) {
	out:=make(map[int64]string)
	dir:=recordDir(workDir)
	info,err:=os.Lstat(dir)
	if errors.Is(err,os.ErrNotExist) {return out,nil}
	if err!=nil{return nil,err}
	if !info.IsDir(){return nil,errors.New("unsafe launch evidence directory")}
	entries,err:=os.ReadDir(dir)
	if err!=nil{return nil,err}
	for _,entry:=range entries {
		name:=entry.Name()
		// Atomic writes create temp files BEFORE any child is launched.
		if strings.HasPrefix(name,".launch-intent-") {continue}
		if !strings.HasSuffix(name,".json") {return nil,fmt.Errorf("unrecognized launch evidence file: %q",name)}
		key:=strings.TrimSuffix(name,".json")
		id,err:=strconv.ParseInt(key,10,64)
		if err!=nil || id<=0 || strconv.FormatInt(id,10)!=key {
			return nil,fmt.Errorf("invalid launch evidence ProgramId filename: %q",name)
		}
		rec,err:=readRecord(filepath.Join(dir,name))
		if err!=nil {
			out[id]="unreadable/corrupt prior child launch evidence: "+err.Error()
			continue
		}
		if rec.ProgramID!=key {
			out[id]="launch evidence ProgramId mismatch"
			continue
		}
		out[id]=fmt.Sprintf("unreconciled launch attempt=%s stage=%s pid=%d intent=%s; original Worker Wait not confirmed",
			rec.AttemptID,rec.Stage,rec.PID,rec.IntendedAt)
	}
	return out,nil
}
