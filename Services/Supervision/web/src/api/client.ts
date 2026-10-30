import type { AlertHistoryEnvelope, Envelope, EventsEnvelope, GuardEnvelope, Overview, Program } from '../types/supervision'

// Same-origin in production; Vite proxies /api to the local Go Monitor in dev.
// An AbortSignal guarantees that a disconnected Monitor does not hang the UI.
async function request<T>(path: string): Promise<T> {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), 7000)
  try {
    const response = await fetch(path, {
      signal: controller.signal,
      cache: 'no-store',
      headers: { Accept: 'application/json' },
    })
    if (!response.ok) {
      if (response.status === 403) {
        throw new Error('访问被拒绝：监控数据仅允许在 Monitor 本机查询，远程访问尚未启用身份认证')
      }
      throw new Error('Monitor API HTTP ' + response.status)
    }
    return (await response.json()) as T
  } finally {
    clearTimeout(timeout)
  }
}

export function fetchOverview(): Promise<Envelope<Overview>> {
  return request<Envelope<Overview>>('/api/v1/overview')
}

export function fetchPrograms(): Promise<Envelope<Program[]>> {
  return request<Envelope<Program[]>>('/api/v1/programs')
}

export function fetchProgram(id: string): Promise<Envelope<Program>> {
  return request<Envelope<Program>>('/api/v1/programs/' + encodeURIComponent(id))
}

export function fetchEvents(limit = 30): Promise<EventsEnvelope> {
  if (!Number.isInteger(limit) || limit < 1 || limit > 100) {
    return Promise.reject(new Error('事件查询数量必须在 1～100 之间'))
  }
  return request<EventsEnvelope>('/api/v1/events?limit=' + limit)
}

export function fetchAlertHistory(limit = 30): Promise<AlertHistoryEnvelope> {
  if (!Number.isInteger(limit) || limit < 1 || limit > 100) {
    return Promise.reject(new Error('异常历史数量必须在 1～100 之间'))
  }
  return request<AlertHistoryEnvelope>('/api/v1/alerts/history?limit=' + limit)
}

/** Only local, read-only diagnostic artifacts. Never an operation endpoint. */
export function fetchGuardReport(): Promise<GuardEnvelope> {
  return request<GuardEnvelope>('/api/v1/guard')
}
