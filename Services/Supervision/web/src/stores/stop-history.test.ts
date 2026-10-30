import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStopHistoryStore } from './stop-history'
import { fetchStopHistory } from '../api/client'
import type { StopHistoryEnvelope } from '../types/supervision'

vi.mock('../api/client', () => ({ fetchStopHistory: vi.fn() }))
const response: StopHistoryEnvelope = {
  scope: 'local-only', kind: 'durable-stop-history', generatedAt: '2026-10-30T12:00:00Z',
  data: { available: true, items: [{
    programId: '9007199254740995', operationId: '0123456789abcdef0123456789abcdef',
    pid: 471, action: 'shutdown', state: 'unknown', waitCompleted: false,
    recordedAt: '2026-10-30T11:59:00Z',
  }] },
}
beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks() })
describe('stop history refresh and failure semantics', () => {
  it('retains the exact last observation and time after a network failure', async () => {
    vi.mocked(fetchStopHistory).mockResolvedValueOnce(response).mockRejectedValueOnce(new Error('offline'))
    const store = useStopHistoryStore()
    await store.refresh()
    await store.refresh()
    expect(store.history?.items[0]?.programId).toBe('9007199254740995')
    expect(store.history?.items[0]?.state).toBe('unknown')
    expect(store.updatedAt).toBe(response.generatedAt)
    expect(store.error).toBe('offline')
    expect(store.loading).toBe(false)
  })
  it('deduplicates in-flight refresh and recovers from unavailable sources', async () => {
    let resolve!: (value: StopHistoryEnvelope) => void
    vi.mocked(fetchStopHistory).mockImplementationOnce(() => new Promise(done => { resolve = done }))
    const store = useStopHistoryStore()
    const pending = store.refresh()
    await store.refresh()
    expect(fetchStopHistory).toHaveBeenCalledTimes(1)
    resolve({ ...response, data: { available: false, error: 'corrupt', items: [] } })
    await pending
    expect(store.history?.available).toBe(false)
    vi.mocked(fetchStopHistory).mockResolvedValueOnce(response)
    await store.refresh()
    expect(store.history?.available).toBe(true)
    expect(store.error).toBe('')
  })
})
