import type { GuardReport } from '../types/supervision'

export type GuardVerdict = 'terminal' | 'orphan' | 'both' | 'unknown' | 'unrecorded'

/** Derived solely from available persisted sources; not a live health verdict. */
export function guardVerdict(report: GuardReport | null, programId: string): GuardVerdict {
  if (!report) return 'unknown'
  const terminal = report.terminal.available &&
    report.terminal.items.some(item => item.programId === programId)
  const orphan = report.orphans.available &&
    report.orphans.items.some(item => item.programId === programId)
  if (terminal && orphan) return 'both'
  if (terminal) return 'terminal'
  if (orphan) return 'orphan'
  if (!report.terminal.available || !report.orphans.available) return 'unknown'
  return 'unrecorded'
}

export function guardLabel(verdict: GuardVerdict): string {
  switch (verdict) {
    case 'both': return '闭锁 + 隔离'
    case 'terminal': return '已达到守护上限'
    case 'orphan': return '遗留进程隔离'
    case 'unrecorded': return '无已记录保护项'
    default: return '来源不可用'
  }
}

export function guardTagType(verdict: GuardVerdict): 'danger' | 'warning' | 'info' {
  return verdict === 'both' || verdict === 'terminal' ? 'danger' :
    verdict === 'orphan' ? 'warning' : 'info'
}

export function observationTime(value?: string): string {
  if (!value) return '未知'
  const timestamp = Date.parse(value)
  return Number.isFinite(timestamp)
    ? new Date(timestamp).toLocaleString('zh-CN', { hour12: false })
    : '无效时间'
}
