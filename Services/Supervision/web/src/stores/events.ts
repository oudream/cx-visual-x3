import { defineStore } from 'pinia'
import { ref } from 'vue'
import { fetchEvents } from '../api/client'
import type { ProgramEvent } from '../types/supervision'

export const useEventsStore = defineStore('events', () => {
  const items = ref<ProgramEvent[]>([])
  const loading = ref(false)
  const error = ref('')
  const fetchedAt = ref('')

  async function refresh(): Promise<void> {
    if (loading.value) return
    loading.value = true
    try {
      const result = await fetchEvents(100)
      items.value = result.data
      fetchedAt.value = result.generatedAt
      error.value = ''
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : '读取事件失败'
    } finally {
      loading.value = false
    }
  }

  return { items, loading, error, fetchedAt, refresh }
})
