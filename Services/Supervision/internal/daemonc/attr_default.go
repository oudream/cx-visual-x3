//go:build !windows && !plan9
// +build !windows,!plan9

package daemonc

import "syscall"

func NewSysProcAttr() *syscall.SysProcAttr {
	return &syscall.SysProcAttr{
		Setsid: true,
	}
}
