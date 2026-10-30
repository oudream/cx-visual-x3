package journalfile

import (
	"os"
	"path/filepath"
	"runtime"
	"testing"
)

func TestPlatformTempDirectoryAndRedirectedAncestor(t *testing.T) {
	if err := CheckDir(t.TempDir()); err != nil {
		t.Fatalf("native temporary path rejected: %v", err)
	}
	if runtime.GOOS == "darwin" {
		for _, path := range []string{"/var", "/tmp"} {
			if err := CheckDir(path); err != nil {
				t.Fatalf("system alias rejected: %s: %v", path, err)
			}
		}
	}
	base, outside := t.TempDir(), t.TempDir()
	if err := os.Symlink(outside, filepath.Join(base, "redirect")); err != nil {
		t.Skipf("symlink unavailable: %v", err)
	}
	if err := EnsureDir(filepath.Join(base, "redirect", "nested")); err == nil {
		t.Fatal("redirected ancestor accepted")
	}
	if entries, err := os.ReadDir(outside); err != nil || len(entries) != 0 {
		t.Fatal("created data outside controlled directory")
	}
}
