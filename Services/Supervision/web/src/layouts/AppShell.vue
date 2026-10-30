<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'
import { useGuardStore } from '../stores/guard'
import { useStopHistoryStore } from '../stores/stop-history'
import { useThemeStore } from '../stores/theme'
import { useEventsStore } from '../stores/events'
import { useAlertHistoryStore } from '../stores/alerts'

const route = useRoute()
const monitor = useMonitorStore()
const guard = useGuardStore()
const stopHistory = useStopHistoryStore()
const theme = useThemeStore()
const events = useEventsStore()
const alerts = useAlertHistoryStore()
const { error, loading, updatedAt } = storeToRefs(monitor)
const { loading: guardLoading } = storeToRefs(guard)
const { loading: stopHistoryLoading } = storeToRefs(stopHistory)
const toolbarLoading = computed(() => loading.value || guardLoading.value || stopHistoryLoading.value || events.loading || alerts.loading)
const pageUpdatedAt = computed(() => route.path === '/events' ? events.fetchedAt :
  route.path === '/alerts' ? alerts.fetchedAt : route.path === '/stops' ? stopHistory.updatedAt :
  route.path === '/guard' ? guard.fetchedAt : updatedAt.value)

function refreshCurrent(): void {
  void monitor.refresh()
  if (route.path === '/' || route.path === '/guard' || route.path === '/programs') void guard.refresh()
  if (route.path === '/stops') void stopHistory.refresh()
  if (route.path === '/events') void events.refresh()
  if (route.path === '/alerts') void alerts.refresh()
}
watch(() => route.path, refreshCurrent)
let poller: ReturnType<typeof setInterval> | undefined
const titles: Record<string, string> = {
  '/': '运行总览', '/programs': '程序与服务', '/guard': '守护诊断', '/stops': '停止历史', '/alerts': '异常历史', '/events': '事件记录',
}
const title = computed(() => titles[route.path] || '运行总览')

onMounted(() => {
  theme.apply()
  refreshCurrent()
  poller = setInterval(() => {
    if (document.visibilityState === 'visible') {
      refreshCurrent()
    }
  }, 5000)
})
onBeforeUnmount(() => {
  if (poller) clearInterval(poller)
})
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <div class="brand">
        <div class="brand-mark">C</div>
        <div><strong>CxVisual</strong><small>SUPERVISION</small></div>
      </div>
      <nav class="nav" aria-label="主导航">
        <RouterLink to="/"><span class="nav-icon">⌂</span>运行总览</RouterLink>
        <RouterLink to="/programs"><span class="nav-icon">▣</span>程序与服务</RouterLink>
        <RouterLink to="/guard"><span class="nav-icon">◇</span>守护诊断</RouterLink>
        <RouterLink to="/stops"><span class="nav-icon">◎</span>停止历史</RouterLink>
        <RouterLink to="/alerts"><span class="nav-icon">!</span>异常历史</RouterLink>
        <RouterLink to="/events"><span class="nav-icon">♧</span>事件记录</RouterLink>
      </nav>
      <div class="sidebar-foot">
        <span class="connection-indicator" :class="{ offline: !!error }"></span>
        {{ error ? (error.startsWith('访问被拒绝') ? '监控查询权限受限' : 'Monitor 连接异常') : updatedAt ? 'Monitor 已连接' : '等待连接' }}
        <small>只读运维控制台 · V1.0</small>
      </div>
    </aside>
    <main class="workspace">
      <header class="toolbar">
        <div class="toolbar-title">
          <span class="crumb">CxVisual / Supervision</span>
          <h1>{{ title }}</h1>
        </div>
        <div class="toolbar-actions">
          <span class="updated-time">{{ pageUpdatedAt ? '更新 ' + new Date(pageUpdatedAt).toLocaleTimeString('zh-CN', { hour12: false }) : '暂无数据' }}</span>
          <el-button :aria-label="theme.mode === 'light' ? '切换深色主题' : '切换浅色主题'"
            @click="theme.toggle">{{ theme.mode === 'light' ? '☾ 深色' : '☼ 浅色' }}</el-button>
          <el-button type="primary" :loading="toolbarLoading" @click="refreshCurrent">刷新</el-button>
        </div>
      </header>
      <div v-if="error" class="error-banner" role="alert">Monitor 查询失败：{{ error }}。数据可能已过期。</div>
      <div class="page-body"><RouterView /></div>
      <footer class="footer">CxVisual Supervision · 当前为进程监督及只读查询，业务就绪状态应由对应服务提供</footer>
    </main>
  </div>
</template>
