//go:build cgo

package program

import (
	"os"
	"path/filepath"
	"testing"
)

// Exercise the original SQLite driver/table initialization on real local
// storage, without starting Daemon, Monitor, or any configured program.
func TestSQLiteInitializationAndInt64RoundTrip(t *testing.T) {
	dir := t.TempDir()
	if err := os.MkdirAll(filepath.Dir(GetDBFilePath(dir)), 0700); err != nil {
		t.Fatal(err)
	}
	if _, err := InitDb(dir); err != nil {
		t.Fatal(err)
	}
	db, err := OpenDb(dir)
	if err != nil {
		t.Fatal(err)
	}
	defer CloseDb(db)
	const id int64 = 9007199254740995
	if _, err := db.Exec("CREATE TABLE qa_identity (id INTEGER PRIMARY KEY)"); err != nil {
		t.Fatal(err)
	}
	if _, err := db.Exec("INSERT INTO qa_identity(id) VALUES (?)", id); err != nil {
		t.Fatal(err)
	}
	if actual, err := QuerySqlInt64(db, "SELECT id FROM qa_identity"); err != nil || actual != id {
		t.Fatalf("SQLite int64 changed: %d %v", actual, err)
	}
	if _, err := db.Exec("INSERT INTO qa_identity(id) VALUES (?)", id); err == nil {
		t.Fatal("duplicate primary key silently accepted")
	}
}
