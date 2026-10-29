export interface Program {
  id: string
  pid: number
  path: string
  running: boolean
  lastRunAtMs: number
  heartbeatAtMs: number
  heartbeatHealthy: number
  cpuPercent: number
  memoryRssBytes: number
  startCount: number
  errorCount: number
  maxCount: number
  maxError: number
}

export interface Overview {
  total: number
  running: number
  stopped: number
  heartbeatReporting: number
  cpuPercent: number
  memoryRssBytes: number
}

export interface Envelope<T> {
  data: T
  generatedAt: string
}

export interface ProgramEvent {
  programId: string
  pId: number
  event: number
  time: number
  msg: string
}

export interface EventsEnvelope {
  data: ProgramEvent[]
  scope: 'local-only'
  generatedAt: string
}

export interface HistoricalIncident {
  programId: string
  pid: number
  event: number
  time: number
  severity: 'critical' | 'warning'
  kind: 'max-error' | 'max-start' | 'start-failed'
  message: string
}

export interface AlertHistoryEnvelope {
  data: HistoricalIncident[]
  scope: 'local-only'
  kind: 'historical-failures'
  historyWindowBytes: number
  generatedAt: string
}
