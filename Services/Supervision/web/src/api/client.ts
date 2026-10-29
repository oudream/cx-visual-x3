import type { Envelope, Overview, Program } from '../types/supervision'

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
