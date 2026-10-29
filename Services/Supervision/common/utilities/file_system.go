package utilities

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

func FileSystemIsExist(fp string) bool {
	if _, err := os.Stat(fp); err == nil {
		return true
	} else {
		return false
	}
}

func FileSystemIsValidFile(workDir, fp string) (bool, string) {
	stat, err := os.Stat(fp)
	if err != nil {
		return false, fp
	}
	if stat.Mode().IsRegular() {
		return true, fp
	}
	fp = filepath.Join(workDir, fp)
	stat, err = os.Stat(fp)
	if err != nil {
		return false, fp
	}
	if stat.Mode().IsRegular() {
		return true, fp
	}
	return false, fp
}

func FileSystemConfirmDir(fp string) error {
	if _, err := os.Stat(fp); err != nil {
		return os.MkdirAll(fp, os.ModePerm)
	}
	return nil
}

func FileSystemConfirmFile(fp string) error {
	if !FileSystemIsExist(fp) {
		err := FileSystemConfirmDir(filepath.Dir(fp))
		if err != nil {
			return err
		}
		fd, err := os.Create(fp)
		defer func(fd *os.File) {
			_ = fd.Close()
		}(fd)
		if err != nil {
			return err
		}
	} else {
		return nil
	}
	return nil
}

func FileSystemCreateFile(fp string) error {
	err := FileSystemConfirmDir(filepath.Dir(fp))
	if err != nil {
		return err
	}
	fd, err := os.Create(fp)
	defer func(fd *os.File) {
		_ = fd.Close()
	}(fd)
	return err
}

func FileSystemGetFileNameOnly(fp string) string {
	fn := filepath.Base(fp)
	suffix := filepath.Ext(fn)
	return strings.TrimSuffix(fn, suffix)
}

func FileSystemRead(fp string) ([]byte, error) {
	bytes, err := os.ReadFile(fp)
	if err != nil {
		return nil, err
	} else {
		return bytes, nil
	}
}

func FileSystemWrite(fp, text string) error {
	return os.WriteFile(fp, []byte(text), 0666)
}

func FileSystemGetLineCount(fp string) (int64, error) {
	file, err := os.Open(fp)
	if err != nil {
		return 0, err
	}
	defer func(file *os.File) {
		_ = file.Close()
	}(file)
	scanner := bufio.NewScanner(file)
	var count int64
	for scanner.Scan() {
		count++
	}
	if err = scanner.Err(); err != nil {
		return 0, err
	}
	return count, nil
}

func FileSystemGetDirUsage(dir string) (map[string]int64, error) {
	r := make(map[string]int64)
	files, err := os.ReadDir(dir)
	if err != nil {
		return nil, err
	}
	for _, file := range files {
		if file.IsDir() {
			subDir := filepath.Join(dir, file.Name())
			dirSize, err := FileSystemGetDirSize(subDir)
			if err != nil {
				return nil, err
			}
			r[file.Name()] = dirSize
		}
	}
	return r, nil
}

func FileSystemGetDirSize(dir string) (int64, error) {
	var size int64 = 0
	err := filepath.Walk(dir, func(path string, info os.FileInfo, err error) error {
		if !info.IsDir() {
			size += info.Size()
		}
		return nil
	})
	return size, err
}

func FileSystemConfirmWorkDir(workDir *string) bool {
	name := fmt.Sprintf(" [ %-15s ] ", "MAIN")
	Error := "ERROR" + name + " - "
	Info := "INFO " + name + " - "
	if !filepath.IsAbs(*workDir) {
		wd, err := os.Getwd()
		if err != nil {
			fmt.Println(Error + "Fatal os.Getwd, Work Dir[e.g.: -d /path] Be Must!!!")
			os.Exit(-1)
			return false
		}
		*workDir = wd
	} else {
		err := os.Chdir(*workDir)
		if err != nil {
			fmt.Printf(Error+"Fatal os.Chdir(%s), Work Dir[e.g.: -d /path] Be Must!!!\n", *workDir)
			os.Exit(-1)
			return false
		}
	}
	fmt.Println(Info+"Work Dir:", *workDir)
	return true
}
