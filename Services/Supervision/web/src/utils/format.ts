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

export function programName(path: string, id: string): string {
  return path.split(/[\\/]/).filter(Boolean).pop() || '程序 #' + id
}


// Use Monitor's generatedAt as the time reference instead of the browser clock.
// This is observation age only; it is not a HeartbeatHealthy/ServiceReady
// verdict. Different programs may use different heartbeat intervals.
export function formatHeartbeatAgeAtSnapshot(lastAtMs: number, generatedAt: string): string {
  if (!Number.isFinite(lastAtMs) || lastAtMs <= 0) return '从未上报'
  const observedAtMs = Date.parse(generatedAt)
  if (!Number.isFinite(observedAtMs)) return '快照时间未知'
  const ageMs = observedAtMs - lastAtMs
  if (ageMs < 0) return '心跳晚于快照（时钟可能不同步）'
  const seconds = Math.floor(ageMs / 1000)
  if (seconds < 60) return '快照时距心跳 ' + seconds + ' 秒'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return '快照时距心跳 ' + minutes + ' 分 ' + (seconds % 60) + ' 秒'
  return '快照时距心跳 ' + Math.floor(minutes / 60) + ' 小时 ' + (minutes % 60) + ' 分'
}
