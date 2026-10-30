import { afterEach, describe, expect, it, vi } from 'vitest'
import { fetchOverview, fetchPrograms } from './client'

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
