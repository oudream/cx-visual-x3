import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchAlertHistory } from '../api/client'
import type { HistoricalIncident } from '../types/supervision'

export const useAlertHistoryStore = defineStore('alert-history', () => {
  const items = ref<HistoricalIncident[]>([])
  const loading = ref(false)
  const error = ref('')
  const fetchedAt = ref('')
  const coveredBytes = ref(0)

  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const result = await fetchAlertHistory(100)
      items.value = result.data
      coveredBytes.value = result.historyWindowBytes
      fetchedAt.value = result.generatedAt
      error.value = ''
    } catch (e) {
      error.value = e instanceof Error ? e.message : '异常历史暂不可用'
    } finally {
      loading.value = false
    }
  }

  return { items, loading, error, fetchedAt, coveredBytes, refresh }
})
