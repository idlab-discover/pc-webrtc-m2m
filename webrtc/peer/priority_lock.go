package main

import (
	"sync"
)

type PriorityLock interface {
	Lock()
	Unlock()
	HighPriorityLock()
	HighPriorityUnlock()
}

type PriorityPreferenceLock struct {
	dataMutex           sync.Mutex
	nextToAccess        sync.Mutex
	lowPriorityMutex    sync.Mutex
	highPriorityWaiting sync.WaitGroup
}

func NewPriorityPreferenceLock() *PriorityPreferenceLock {
	lock := PriorityPreferenceLock{
		highPriorityWaiting: sync.WaitGroup{},
	}
	return &lock
}

func (lock *PriorityPreferenceLock) Lock() {
	lock.lowPriorityMutex.Lock()
	lock.highPriorityWaiting.Wait()
	lock.nextToAccess.Lock()
	lock.dataMutex.Lock()
	lock.nextToAccess.Unlock()
}

func (lock *PriorityPreferenceLock) Unlock() {
	lock.dataMutex.Unlock()
	lock.lowPriorityMutex.Unlock()
}

func (lock *PriorityPreferenceLock) HighPriorityLock() {
	lock.highPriorityWaiting.Add(1)
	lock.nextToAccess.Lock()
	lock.dataMutex.Lock()
	lock.nextToAccess.Unlock()
}

func (lock *PriorityPreferenceLock) HighPriorityUnlock() {
	lock.dataMutex.Unlock()
	lock.highPriorityWaiting.Done()
}
