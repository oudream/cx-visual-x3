import { describe, expect, it } from 'vitest'
import { guardLabel, guardVerdict, identityEvidenceLabel, observationTime } from './guard'
import type { GuardReport } from '../types/supervision'

function report(terminalAvailable: boolean, orphanAvailable: boolean): GuardReport {
  return {
    terminal: {
      available: terminalAvailable,
      observedAt: '2026-10-30T08:00:00Z',
      items: terminalAvailable ? [{ programId: '9007199254740995', reason: 'MaxError' }] : [],
    },
    orphans: {
      available: orphanAvailable,
      observedAt: '2026-10-30T08:30:00Z',
      items: orphanAvailable ? [{ programId: '9007199254740995', reason: 'unverified child' }] : [],
    },
  }
}

describe('guard diagnostics derive only grounded observations', () => {
  it('does not infer a healthy program when data was never fetched', () => {
    expect(guardVerdict(null, '1')).toBe('unknown')
    expect(guardLabel('unknown')).toBe('来源不可用')
  })

  it('retains exact decimal int64 program IDs without coercion to Number', () => {
    expect(guardVerdict(report(true, true), '9007199254740995')).toBe('both')
    expect(guardVerdict(report(true, true), '9007199254740994')).toBe('unrecorded')
  })

  it('identifies a known latch even when the orphan file is missing', () => {
    expect(guardVerdict(report(true, false), '9007199254740995')).toBe('terminal')
    expect(guardVerdict(report(false, true), '9007199254740995')).toBe('orphan')
  })

  it('treats partially unavailable sources as unknown, not unrecorded', () => {
    expect(guardVerdict(report(true, false), '123')).toBe('unknown')
    expect(guardVerdict(report(false, true), '123')).toBe('unknown')
    expect(guardVerdict(report(false, false), '123')).toBe('unknown')
    expect(guardVerdict(report(true, true), '123')).toBe('unrecorded')
  })

  it('keeps observation time unknown when the file is unavailable', () => {
    expect(observationTime()).toBe('未知')
    expect(observationTime('bad')).toBe('无效时间')
  })
})

describe('OS process evidence display remains conservative', () => {
  it('identity matches never grant stop authority', () => {
    expect(identityEvidenceLabel(
      'unreconciled launch stage=started pid=140 identity=same-creation-time-and-executable-observed-pid=140-not-proof-of-ownership'))
      .toContain('未证明归属')
  })
  it('distinguishes reused PID from unknown evidence', () => {
    expect(identityEvidenceLabel('stage=started identity=pid-creation-mismatch'))
      .toContain('不匹配')
    expect(identityEvidenceLabel('stage=started identity=unverified-original-birth'))
      .toContain('证据不足')
    expect(identityEvidenceLabel('unreconciled launch stage=intent pid=0 identity=pre-start-intent-no-PID-confirmation'))
      .toContain('无 PID')
  })
  it('does not fabricate an OS observation from old legacy reasons', () => {
    expect(identityEvidenceLabel('living legacy PID 52 matches executable but ownership unverified'))
      .toBe('无结构化 OS 核验证据')
  })
})
