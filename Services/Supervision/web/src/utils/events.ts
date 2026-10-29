import type { ProgramEvent } from '../types/supervision'

export function eventLabel(code: number): string {
  switch (code) {
    case 0: return '启动'
    case 1: return '登录'
    case 2: return '心跳'
    case 3: return '退出'
    case 4: return '失败次数上限'
    case 5: return '启动次数上限'
    default: return '事件 ' + code
  }
}

export function eventLevel(code: number): 'danger' | 'warning' | 'info' | 'success' {
  if (code === 4 || code === 5) return 'danger'
  if (code === 3) return 'warning'
  if (code === 0 || code === 1) return 'success'
  return 'info'
}

export function filterEvents(items: ProgramEvent[], query: string, showHeartbeat: boolean): ProgramEvent[] {
  const text = query.trim().toLowerCase()
  return items.filter(item => {
    if (!showHeartbeat && item.event === 2) return false
    return !text || item.programId.includes(text) ||
      eventLabel(item.event).toLowerCase().includes(text) ||
      item.msg.toLowerCase().includes(text)
  })
}
