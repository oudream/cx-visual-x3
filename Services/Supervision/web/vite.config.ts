import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  return {
    plugins: [vue()],
    // The original Gin server serves WorkDir/assets/comm/index.html.
    base: '/assets/comm/',
    server: {
      proxy: {
        '/api': {
          target: env.VITE_MONITOR_PROXY || 'http://127.0.0.1:7811',
          changeOrigin: true,
        },
      },
    },
    build: {
      outDir: 'dist',
      emptyOutDir: true,
    },
  }
})
