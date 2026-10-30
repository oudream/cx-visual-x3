import { describe, expect, it } from 'vitest'
import { formatBytes, formatTime, formatHeartbeatAgeAtSnapshot, programName } from './format'

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

describe('heartbeat age relative to Monitor generatedAt', () => {
  const generatedAt = '2026-10-30T03:00:00.000Z'
  const timestamp = Date.parse(generatedAt)

  it('never mistakes absent observations for healthy', () => {
    expect(formatHeartbeatAgeAtSnapshot(0, generatedAt)).toBe('从未上报')
    expect(formatHeartbeatAgeAtSnapshot(-1, generatedAt)).toBe('从未上报')
    expect(formatHeartbeatAgeAtSnapshot(NaN, generatedAt)).toBe('从未上报')
    expect(formatHeartbeatAgeAtSnapshot(timestamp, 'invalid')).toBe('快照时间未知')
  })

  it('uses Monitor generatedAt, not a skewed browser clock', () => {
    expect(formatHeartbeatAgeAtSnapshot(timestamp - 15_000, generatedAt)).toBe('快照时距心跳 15 秒')
    expect(formatHeartbeatAgeAtSnapshot(timestamp - 125_000, generatedAt)).toBe('快照时距心跳 2 分 5 秒')
    expect(formatHeartbeatAgeAtSnapshot(timestamp - 3_665_000, generatedAt)).toBe('快照时距心跳 1 小时 1 分')
  })

  it('keeps future source times as clock-skew observations, not health', () => {
    expect(formatHeartbeatAgeAtSnapshot(timestamp + 1000, generatedAt))
      .toBe('心跳晚于快照（时钟可能不同步）')
  })
})
