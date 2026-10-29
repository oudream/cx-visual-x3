package program

import (
	"database/sql"
	"fmt"
	"github.com/go-gorp/gorp"
)

const (
	ActionUpdate = "update"
	ActionDelete = "delete"
	ActionInsert = "insert"
	ActionKill   = "kill"
)

type Diff struct {
	Program
	Action string `json:"Action"`
}

type DiffPair struct {
	Old    Program
	New    Program
	Action string `json:"Action"`
}

// GetDiffListByDB
// ProgramRunList vs ProgramList
// 1 update or same
// 2 delete
// 3 insert
func GetDiffListByDB(db *sql.DB) ([]Diff, error) {
	pns, err := GetAllProgram(db)
	if err != nil {
		return nil, err
	}
	pos, err := GetAllRunProgram(db)
	if err != nil {
		return nil, err
	}
	var ads []Diff
	for i := 0; i < len(pos); i++ {
		ad := Diff{Program: pos[i]}
		index := FindProgramIndex(pns, pos[i].ID)
		if index >= 0 {
			po := &pos[i]
			pn := &pns[index]
			if !IsSameProgram(po, pn) {
				ad.Action = ActionUpdate
			} else {
				continue
			}
		} else {
			ad.Action = ActionDelete
		}
		ads = append(ads, ad)
	}
	for i := 0; i < len(pns); i++ {
		index := FindProgramIndex(pos, pns[i].ID)
		if index < 0 {
			ad := Diff{Program: pns[i]}
			ad.Action = ActionInsert
			ads = append(ads, ad)
		}
	}
	return ads, nil
}

func GetDiffPairListByDB(db *sql.DB) ([]DiffPair, error) {
	pns, err := GetAllProgram(db)
	if err != nil {
		return nil, err
	}
	pos, err := GetAllRunProgram(db)
	if err != nil {
		return nil, err
	}
	var ads []DiffPair
	for i := 0; i < len(pos); i++ {
		ad := DiffPair{Old: pos[i]}
		index := FindProgramIndex(pns, pos[i].ID)
		if index >= 0 {
			po := &pos[i]
			pn := &pns[index]
			if !IsSameProgram(po, pn) {
				ad.Action = ActionUpdate
			} else {
				continue
			}
			ad.New = *pn
		} else {
			ad.Action = ActionDelete
		}
		ads = append(ads, ad)
	}
	for i := 0; i < len(pns); i++ {
		index := FindProgramIndex(pos, pns[i].ID)
		if index < 0 {
			ad := DiffPair{
				New: pns[i],
			}
			ad.Action = ActionInsert
			ads = append(ads, ad)
		}
	}
	return ads, nil
}

func GetDiffList(wd string) ([]Diff, error) {
	// db
	db, err := OpenDb(wd)
	if err != nil {
		return nil, err
	}
	defer CloseDb(db)
	return GetDiffListByDB(db)
}

func GetDiffPairList(wd string) ([]DiffPair, error) {
	// db
	db, err := OpenDb(wd)
	if err != nil {
		return nil, err
	}
	defer CloseDb(db)
	return GetDiffPairListByDB(db)
}

func DiffEmploy(wd string) error {
	db, err := OpenDb(wd)
	if err != nil {
		return err
	}
	defer CloseDb(db)
	//
	res := 0
	if res, err = ExecSql(db, fmt.Sprintf("DELETE FROM %s;", TableNameProgramRun)); err != nil {
		return fmt.Errorf("delete table fail, [ %s ], err: %s\n", TableNameProgramRun, err)
	} else if res < 0 {
		//logger.Printf(_title.Error+"Delete Table Fail, [ %s ], RowsAffected[%d] < 0.\n", TableNameProgramRun, res)
	} else {
		//logger.Printf(_title.Error+"Delete Table Success, [ %s ] err: %s.\n", TableNameProgramRun, err)
	}
	//
	if res, err = ExecSql(db, fmt.Sprintf("INSERT INTO %s SELECT * FROM %s;", TableNameProgramRun, TableNameProgramName)); err != nil {
		//logger.Printf(_title.Error+"Delete Table Fail, [ %s ], err: %s\n", TableNameProgramRun, err)
	} else if res < 0 {
		//logger.Printf(_title.Error+"Delete Table Fail, [ %s ], RowsAffected[%d] < 0.\n", TableNameProgramRun, res)
	} else {
		//logger.Printf(_title.Error+"Delete Table Success, [ %s ] err: %s.\n", TableNameProgramRun, err)
	}
	return nil
}

func DeployProgramByDB(db *sql.DB, prog Program) error {
	ads, err := GetDiffListByDB(db)
	if err != nil {
		return err
	}
	if len(ads) > 0 {
		return fmt.Errorf("update program(%d) fail, has %d diff programs, please diff-employ\n", prog.ID, len(ads))
	} else {
		dbMap := gorp.DbMap{Db: db, Dialect: gorp.SqliteDialect{}}
		dbMap.AddTableWithName(prog, TableNameProgramName).SetKeys(true, "id")
		_, err = dbMap.Update(&prog)
		if err != nil {
			return fmt.Errorf("DbMap Update[%s] Fail. err: %s", TableNameProgramName, err)
		}
		dbMap.AddTableWithName(prog, TableNameProgramRun).SetKeys(true, "id")
		_, err = dbMap.Update(&prog)
		if err != nil {
			return fmt.Errorf("DbMap Update[%s] Fail. err: %s", TableNameProgramRun, err)
		}
		return nil
	}
}
