import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchOverview, fetchPrograms } from '../api/client'
import type { Overview, Program } from '../types/supervision'

export const useMonitorStore = defineStore('monitor', () => {
  const programs = ref<Program[]>([])
  const overview = ref<Overview | null>(null)
  const loading = ref(false)
  const error = ref('')
  const updatedAt = ref('')

  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const [programResult, summaryResult] = await Promise.all([
        fetchPrograms(),
        fetchOverview(),
      ])
      programs.value = programResult.data
      overview.value = summaryResult.data
      updatedAt.value = summaryResult.generatedAt
      error.value = ''
    } catch (reason) {
      // Keep the last snapshot visible but clearly mark it as stale.
      error.value = reason instanceof Error ? reason.message : 'Monitor 无法连接'
    } finally {
      loading.value = false
    }
  }

  return { programs, overview, loading, error, updatedAt, refresh }
})
