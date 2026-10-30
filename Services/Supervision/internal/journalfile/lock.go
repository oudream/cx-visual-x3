// Package journalfile coordinates local safety journals across cooperating
// Daemon/Monitor processes. It does not grant process-control authority.
package journalfile

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"time"
)

// EnsureDir checks each existing ancestor before creating a child directory.
func EnsureDir(dir string) error {
	if _, err := os.Lstat(dir); err == nil {
		return CheckDir(dir)
	} else if !errors.Is(err, os.ErrNotExist) {
		return err
	}
	parent := filepath.Dir(dir)
	if parent == dir {
		return errors.New("missing journal filesystem root")
	}
	if err := EnsureDir(parent); err != nil {
		return err
	}
	if err := os.Mkdir(dir, 0700); err != nil && !errors.Is(err, os.ErrExist) {
		return err
	}
	return CheckDir(dir)
}

// CheckDir rejects redirected ancestors, including a symlink above the final
// directory. The deployment owner must restrict directory write permissions.
func CheckDir(dir string) error {
	abs, err := filepath.Abs(dir)
	if err != nil {
		return err
	}
	for current := abs; ; current = filepath.Dir(current) {
		info, err := os.Lstat(current)
		if err != nil {
			return err
		}
		if !info.IsDir() || info.Mode()&os.ModeSymlink != 0 {
			// macOS itself redirects /var and /tmp to /private. Go's
			// t.TempDir and normal deployments may legitimately use them.
			// Only these exact root-owned aliases and targets are allowed;
			// links within the supervision/work directory remain forbidden.
			if runtime.GOOS == "darwin" && (current == "/var" || current == "/tmp") {
				if target, err := filepath.EvalSymlinks(current); err == nil && target == "/private"+current {
					continue
				}
			}
			return fmt.Errorf("unsafe journal directory: %s", current)
		}
		if filepath.Dir(current) == current {
			return nil
		}
	}
}

// Lock uses a persistent lock inode, never a create/delete lock protocol.
// The OS releases the lock after a crash. Callers must not delete lock files.
func Lock(dir, name string) (func(), error) {
	if err := CheckDir(dir); err != nil {
		return nil, err
	}
	path := filepath.Join(dir, name)
	before, err := os.Lstat(path)
	flags := os.O_RDWR
	if errors.Is(err, os.ErrNotExist) {
		flags |= os.O_CREATE | os.O_EXCL
	} else if err != nil {
		return nil, err
	} else if !before.Mode().IsRegular() {
		return nil, errors.New("unsafe journal lock")
	}
	f, err := os.OpenFile(path, flags, 0600)
	if errors.Is(err, os.ErrExist) {
		return Lock(dir, name)
	}
	if err != nil {
		return nil, err
	}
	fail := func(err error) (func(), error) { f.Close(); return nil, err }
	opened, err := f.Stat()
	if err != nil {
		return fail(err)
	}
	if !opened.Mode().IsRegular() || (before != nil && !os.SameFile(before, opened)) {
		return fail(errors.New("journal lock replaced while opening"))
	}
	deadline := time.Now().Add(3 * time.Second)
	for {
		if err := tryLock(f); err == nil {
			break
		} else if !lockBusy(err) || time.Now().After(deadline) {
			return fail(err)
		}
		time.Sleep(5 * time.Millisecond)
	}
	current, err := os.Lstat(path)
	if err != nil || !os.SameFile(opened, current) {
		unlock(f)
		return fail(errors.New("journal lock replaced while waiting"))
	}
	return func() { unlock(f); f.Close() }, nil
}
