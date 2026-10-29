<script setup lang="ts">
import { onBeforeUnmount, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'

const route = useRoute()
const store = useMonitorStore()
const { error, loading, updatedAt } = storeToRefs(store)
let poller: ReturnType<typeof setInterval> | undefined

onMounted(() => {
  void store.refresh()
  poller = setInterval(() => {
    if (document.visibilityState === 'visible') void store.refresh()
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
        <div class="brand-mark">CX</div>
        <div>
          <strong>CxVisual</strong>
          <small>SUPERVISION</small>
        </div>
      </div>
      <nav class="nav">
        <RouterLink to="/" :class="{ active: route.path === '/' }">总览</RouterLink>
        <RouterLink to="/programs" :class="{ active: route.path === '/programs' }">程序监控</RouterLink>
      </nav>
      <div class="sidebar-foot">Monitor · Daemon<br>只读监控控制台 V1.0</div>
    </aside>
    <main class="workspace">
      <header class="toolbar">
        <div class="toolbar-title">
          <span class="crumb">Supervision / {{ route.path === '/' ? '总览' : '程序监控' }}</span>
          <h1>{{ route.path === '/' ? '运行总览' : '程序监控' }}</h1>
        </div>
        <div class="toolbar-actions">
          <el-tag :type="error ? 'danger' : updatedAt ? 'success' : 'info'" effect="dark">
            {{ error ? '连接异常' : updatedAt ? '已连接' : '等待连接' }}
          </el-tag>
          <span class="updated-time">{{ updatedAt ? '最近更新 ' + new Date(updatedAt).toLocaleTimeString('zh-CN', { hour12: false }) : '暂无实时数据' }}</span>
          <el-button type="primary" :loading="loading" @click="store.refresh">刷新状态</el-button>
        </div>
      </header>
      <div v-if="error" class="error-banner" role="alert">Monitor 查询失败：{{ error }}。当前页面可能显示旧快照，请检查服务和网络。</div>
      <div class="page-body">
        <RouterView />
      </div>
      <footer class="footer">CxVisual Supervision · 数据来自当前 Monitor 状态快照 · 不代表业务就绪判定</footer>
    </main>
  </div>
</template>
