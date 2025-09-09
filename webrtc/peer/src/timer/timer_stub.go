//go:build !windows

package timer

// Stub functions for non-Windows builds
func TimeBeginPeriod(ms uint32) error { return nil }
func TimeEndPeriod(ms uint32) error   { return nil }
