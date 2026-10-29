import { describe, expect, it } from 'vitest'
import { formatBytes, formatTime, programName } from './format'

describe('display values', () => {
  it('preserves real byte units', () => {
    expect(formatBytes(1024)).toBe('1.0 KB')
    expect(formatBytes(0)).toBe('0 B')
  })
  it('does not invent missing heartbeats', () => {
    expect(formatTime(0)).toBe('未上报')
  })
  it('handles Linux and Windows process paths', () => {
    expect(programName('C:\\apps\\monitor.exe', '3')).toBe('monitor.exe')
    expect(programName('/opt/app/daemon', '2')).toBe('daemon')
  })
})
