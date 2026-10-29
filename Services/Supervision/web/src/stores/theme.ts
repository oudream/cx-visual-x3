import { defineStore } from 'pinia'
import { ref } from 'vue'

export type ThemeMode = 'light' | 'dark'
const key = 'cxvisual-supervision-theme'

function readTheme(): ThemeMode {
  try {
    const saved = localStorage.getItem(key)
    if (saved === 'light' || saved === 'dark') return saved
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  } catch {
    return 'light'
  }
}

export const useThemeStore = defineStore('theme', () => {
  const mode = ref<ThemeMode>(readTheme())

  function apply(): void {
    document.documentElement.dataset.theme = mode.value
  }

  function toggle(): void {
    mode.value = mode.value === 'light' ? 'dark' : 'light'
    apply()
    try {
      localStorage.setItem(key, mode.value)
    } catch {
      // Private browsing may disable storage; the active page still switches.
    }
  }

  return { mode, apply, toggle }
})
