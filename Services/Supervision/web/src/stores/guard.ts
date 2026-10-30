import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchGuardReport } from '../api/client'
import type { GuardReport } from '../types/supervision'

// Guard snapshots are local filesystem observations, not live Daemon IPC,
// and must never be used as authorization to stop/restart a process.
export const useGuardStore = defineStore('guard', () => {
  const report = ref<GuardReport | null>(null)
  const loading = ref(false)
  const error = ref('')
  const fetchedAt = ref('')

  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const result = await fetchGuardReport()
      report.value = result.data
      fetchedAt.value = result.generatedAt
      error.value = ''
    } catch (reason) {
      // A failed new request does not erase earlier evidence. The UI must
      // mark that previously fetched report stale rather than healthy.
      error.value = reason instanceof Error ? reason.message : '守护诊断查询失败'
    } finally {
      loading.value = false
    }
  }
  return { report, loading, error, fetchedAt, refresh }
})
