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

/** Daemon 的本地持久文件快照，不等于实时进程 / 服务就绪状态。 */
export interface GuardIssue {
  programId: string
  reason: string
}

export interface GuardSource {
  available: boolean
  observedAt?: string
  error?: string
  items: GuardIssue[]
}

export interface GuardReport {
  terminal: GuardSource
  orphans: GuardSource
}

export interface GuardEnvelope extends Envelope<GuardReport> {
  scope: 'local-only'
  kind: 'local-daemon-artifact-observation'
}

/** Append-only Daemon stop attempt, reconciled by operation ID. */
export interface StopOperation {
  operationId: string
  programId: string
  pid: number
  action: string
  state: 'requested' | 'unknown' | 'refused' | 'signal-failed' | 'wait-confirmed'
  waitCompleted: boolean
  signalError?: string
  waitError?: string
  recordedAt: string
}

export interface StopHistory {
  available: boolean
  error?: string
  items: StopOperation[]
}

export interface StopHistoryEnvelope extends Envelope<StopHistory> {
  scope: 'local-only'
  kind: 'durable-stop-history'
}
