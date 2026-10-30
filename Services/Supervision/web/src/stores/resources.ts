import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchHostResources } from '../api/client'
import type { HostResourceReport } from '../types/supervision'

export const useResourceStore = defineStore('resources', () => {
  const report = ref<HostResourceReport | null>(null)
  const loading = ref(false)
  const error = ref('')
  const updatedAt = ref('')

  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const result = await fetchHostResources()
      report.value = result.data
      updatedAt.value = result.generatedAt
      error.value = ''
    } catch (reason) {
      // Previously sampled measurements are retained but explicitly stale.
      error.value = reason instanceof Error ? reason.message : '主机资源采样不可用'
    } finally {
      loading.value = false
    }
  }

  return { report, loading, error, updatedAt, refresh }
})
