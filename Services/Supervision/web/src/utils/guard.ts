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

/** Displays only the evidence explicitly written by Daemon's startup scan.
 * Neither a matching timestamp nor an executable match grants ownership. */
export function identityEvidenceLabel(reason: string): string {
  const code = /(?:^|\s)identity=([a-z0-9-]+)/.exec(reason)?.[1]
  switch (code) {
    case 'pid-creation-mismatch': return 'PID 创建时间不匹配'
    case 'same-creation-time-executable-mismatch': return '创建时间一致，但可执行文件不同'
    case 'same-creation-time-executable-unreadable':
    case 'same-creation-time-executable-file-unreadable':
      return '创建时间一致，程序文件无法核实'
    case 'pid-no-longer-present': return '历史 PID 已不存在（仍未清算）'
    case 'pre-start-intent-no-PID-confirmation': return '仅有启动意图，无 PID'
    case 'pid-not-observable':
    case 'pid-existence-unknown':
    case 'pid-present-birth-unreadable':
    case 'unverified-original-birth':
    case 'unverified-current-birth':
      return '进程代次证据不足'
    default:
      if (code?.startsWith('same-creation-time-and-executable-observed-pid=')) {
        return '创建时间及程序文件吻合（未证明归属）'
      }
      return '无结构化 OS 核验证据'
  }
}
