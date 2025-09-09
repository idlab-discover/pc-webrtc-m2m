//go:build windows

package timer

import (
	"golang.org/x/sys/windows"
)

var (
	winmm               = windows.NewLazySystemDLL("winmm.dll")
	procTimeBeginPeriod = winmm.NewProc("timeBeginPeriod")
	procTimeEndPeriod   = winmm.NewProc("timeEndPeriod")
)

func TimeBeginPeriod(ms uint32) error {
	r, _, e := procTimeBeginPeriod.Call(uintptr(ms))
	if r != 0 {
		return e
	}
	return nil
}

func TimeEndPeriod(ms uint32) error {
	r, _, e := procTimeEndPeriod.Call(uintptr(ms))
	if r != 0 {
		return e
	}
	return nil
}
