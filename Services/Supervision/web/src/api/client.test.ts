import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchGuardReport, fetchOverview, fetchPrograms, fetchStopHistory } from './client'

afterEach(() => vi.unstubAllGlobals())

describe('supervision API security boundary', () => {
  it('explains access denial without claiming the Monitor is disconnected', async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: false, status: 403 })
    vi.stubGlobal('fetch', fetchMock)
    await expect(fetchPrograms()).rejects.toThrow('仅允许在 Monitor 本机查询')
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/programs',
      expect.objectContaining({ cache: 'no-store' }),
    )
  })

  it('keeps other server failures distinguishable from access control', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 500 }))
    await expect(fetchOverview()).rejects.toThrow('Monitor API HTTP 500')
  })

  it('preserves the existing successful API contract', async () => {
    const result = {
      data: {
        total: 2, running: 1, stopped: 1, heartbeatReporting: 0,
        cpuPercent: 0, memoryRssBytes: 0,
      },
      generatedAt: '2026-10-30T00:00:00Z',
    }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => result,
    }))
    await expect(fetchOverview()).resolves.toEqual(result)
  })
})

describe('read-only guard diagnostics', () => {
  it('uses the same local-only API without cache', async () => {
    const envelope = {
      data: {
        terminal: { available: false, error: 'not readable', items: [] },
        orphans: { available: true, observedAt: '2026-10-30T08:00:00Z',
          items: [{ programId: '9007199254740995', reason: 'unverified child' }] },
      },
      scope: 'local-only', kind: 'local-daemon-artifact-observation',
      generatedAt: '2026-10-30T08:01:00Z',
    }
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, status: 200, json: async () => envelope,
    })
    vi.stubGlobal('fetch', fetchMock)
    await expect(fetchGuardReport()).resolves.toEqual(envelope)
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/guard',
      expect.objectContaining({ cache: 'no-store' }))
  })

  it('does not silently reinterpret a blocked remote guard query as no issues', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))
    await expect(fetchGuardReport()).rejects.toThrow('仅允许在 Monitor 本机查询')
  })
})

describe('read-only durable stop history', () => {
  it('preserves exact 64-bit Program ID and operation ID', async () => {
    const item = { operationId: '0123456789abcdef0123456789abcdef',
      programId: '9007199254740995', pid: 231, action: 'shutdown',
      state: 'unknown', waitCompleted: false, recordedAt: '2026-10-30T08:00:00Z' }
    const envelope = { data: { available: true, items: [item] },
      generatedAt: '2026-10-30T08:00:01Z', scope: 'local-only',
      kind: 'durable-stop-history' }
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, status: 200, json: async () => envelope,
    })
    vi.stubGlobal('fetch', fetchMock)
    await expect(fetchStopHistory()).resolves.toEqual(envelope)
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/stops/history',
      expect.objectContaining({ cache: 'no-store' }))
  })
  it('refuses 403 instead of fabricating an empty record', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))
    await expect(fetchStopHistory()).rejects.toThrow('仅允许在 Monitor 本机查询')
  })
})
