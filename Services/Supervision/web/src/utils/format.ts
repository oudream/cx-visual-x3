export function formatBytes(value: number): string {
  if (!Number.isFinite(value) || value < 0) return '—'
  if (value < 1024) return value.toFixed(0) + ' B'
  const labels = ['KB', 'MB', 'GB', 'TB']
  let amount = value
  let unit = -1
  do {
    amount /= 1024
    unit++
  } while (amount >= 1024 && unit < labels.length - 1)
  return amount.toFixed(1) + ' ' + labels[unit]
}

export function formatTime(ms: number): string {
  return ms > 0 ? new Date(ms).toLocaleString('zh-CN', { hour12: false }) : '未上报'
}

export function programName(path: string, id: number): string {
  return path.split(/[\\/]/).filter(Boolean).pop() || '程序 #' + id
}
