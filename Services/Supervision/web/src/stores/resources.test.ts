import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useResourceStore } from './resources'
import { fetchHostResources } from '../api/client'

vi.mock('../api/client', () => ({ fetchHostResources: vi.fn() }))
const mockedFetch = vi.mocked(fetchHostResources)

beforeEach(() => {
  setActivePinia(createPinia())
  mockedFetch.mockReset()
})

describe('host resource sampling store', () => {
  it('retains sampled facts and individual availability', async () => {
    mockedFetch.mockResolvedValue({
      scope: 'local-only', kind: 'host-resource-snapshot', generatedAt: '2026-10-30T13:00:00Z',
      data: {
        scope: 'host', diskScope: 'work-dir-filesystem',
        cpu: { available: false, percent: 0, sampledAt: '2026-10-30T12:59:59Z', error: 'CPU unknown' },
        memory: { available: true, totalBytes: 4096, usedBytes: 2048,
          availableBytes: 2048, usedPercent: 50, sampledAt: '2026-10-30T12:59:59Z' },
        disk: { available: true, totalBytes: 8192, usedBytes: 4096,
          availableBytes: 4096, usedPercent: 50, sampledAt: '2026-10-30T12:59:59Z' },
      },
    })
    const store = useResourceStore()
    await store.refresh()
    expect(store.report?.cpu.available).toBe(false)
    expect(store.report?.memory.available).toBe(true)
    expect(store.updatedAt).toBe('2026-10-30T13:00:00Z')
    expect(store.error).toBe('')
  })

  it('preserves old sample but explicitly reports refresh failure', async () => {
    const store = useResourceStore()
    mockedFetch.mockResolvedValueOnce({
      scope: 'local-only', kind: 'host-resource-snapshot', generatedAt: '2026-10-30T13:00:00Z',
      data: {
        scope: 'host', diskScope: 'work-dir-filesystem',
        cpu: { available: true, percent: 25, sampledAt: '2026-10-30T13:00:00Z' },
        memory: { available: false, totalBytes: 0, usedBytes: 0, availableBytes: 0,
          usedPercent: 0, sampledAt: '2026-10-30T13:00:00Z', error: 'unavailable' },
        disk: { available: false, totalBytes: 0, usedBytes: 0, availableBytes: 0,
          usedPercent: 0, sampledAt: '2026-10-30T13:00:00Z', error: 'unavailable' },
      },
    })
    await store.refresh()
    mockedFetch.mockRejectedValueOnce(new Error('Monitor disconnected'))
    await store.refresh()
    expect(store.report?.cpu.percent).toBe(25)
    expect(store.error).toBe('Monitor disconnected')
    expect(store.updatedAt).toBe('2026-10-30T13:00:00Z')
  })
})
