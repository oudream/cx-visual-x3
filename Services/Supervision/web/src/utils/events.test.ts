import { describe, expect, it } from 'vitest'
import { eventLabel, eventLevel, filterEvents } from './events'
import type { ProgramEvent } from '../types/supervision'

describe('supervision events', () => {
  it('uses original event codes without inventing alarm states', () => {
    expect(eventLabel(2)).toBe('心跳')
    expect(eventLevel(4)).toBe('danger')
    expect(eventLabel(55)).toBe('事件 55')
  })
  it('filters heartbeats and searches exact int64 ids', () => {
    const items: ProgramEvent[] = [
      { programId: '9007199254740995', pId: 8, event: 3, time: 123, msg: '停止' },
      { programId: '2', pId: 8, event: 2, time: 124, msg: '' },
    ]
    expect(filterEvents(items, '', false)).toHaveLength(1)
    expect(filterEvents(items, '9007199254740995', true)).toHaveLength(1)
    expect(filterEvents(items, '', true)).toHaveLength(2)
  })
})
