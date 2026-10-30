import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchStopHistory } from '../api/client'
import type { StopHistory } from '../types/supervision'

// Read-only operations. No browser write, stop, restart or unlock action.
export const useStopHistoryStore = defineStore('stop-history', () => {
  const history = ref<StopHistory | null>(null)
  const loading = ref(false)
  const error = ref('')
  const updatedAt = ref('')
  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const result = await fetchStopHistory()
      history.value = result.data
      updatedAt.value = result.generatedAt
      error.value = ''
    } catch (err) {
      error.value = err instanceof Error ? err.message : '停止流水查询失败'
    } finally {
      loading.value = false
    }
  }
  return { history, loading, error, updatedAt, refresh }
})
