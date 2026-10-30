<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'
import { fetchProgram } from '../api/client'
import type { Program } from '../types/supervision'
import { formatBytes, formatTime, formatHeartbeatAgeAtSnapshot, programName } from '../utils/format'

const store = useMonitorStore()
const { programs, updatedAt } = storeToRefs(store)
const filter = ref('')
const activeOnly = ref(false)
const detailOpen = ref(false)
const detail = ref<Program | null>(null)
const detailError = ref('')
const detailLoading = ref(false)
const detailGeneratedAt = ref('')
const filtered = computed(() => programs.value.filter(p => {
  if (activeOnly.value && !p.running) return false
  const text = filter.value.trim().toLowerCase()
  return !text || p.id.includes(text) || p.path.toLowerCase().includes(text)
}))

async function openDetail(row: Program): Promise<void> {
  detail.value = row
  detailOpen.value = true
  detailError.value = ''
  detailGeneratedAt.value = updatedAt.value
  detailLoading.value = true
  try {
    const snapshot = await fetchProgram(row.id)
    detail.value = snapshot.data
    detailGeneratedAt.value = snapshot.generatedAt
  } catch (err) {
    detailError.value = err instanceof Error ? err.message : '详情读取失败'
  } finally {
    detailLoading.value = false
  }
}
</script>

<template>
  <section class="panel">
    <div class="panel-heading">
      <div><h2>程序与进程</h2><p>{{ filtered.length }} 条 · 只读状态与守护统计 · 心跳时间不代表服务 Ready</p></div>
      <div class="filter-controls">
        <el-input v-model="filter" clearable placeholder="搜索 ID 或路径" class="search-box" />
        <el-checkbox v-model="activeOnly">仅运行中</el-checkbox>
      </div>
    </div>
    <el-table :data="filtered" class="cx-table" row-key="id" empty-text="没有匹配的程序">
      <el-table-column label="程序 / 路径" min-width="240">
        <template #default="{ row }"><strong>{{ programName(row.path, row.id) }}</strong><div class="path-label">{{ row.path || '未提供路径' }}</div></template>
      </el-table-column>
      <el-table-column prop="pid" label="PID" width="90" />
      <el-table-column label="运行状态" width="108">
        <template #default="{ row }"><el-tag :type="row.running ? 'success' : 'info'" effect="plain">{{ row.running ? '运行中' : '未运行' }}</el-tag></template>
      </el-table-column>
      <el-table-column label="CPU" width="95"><template #default="{ row }">{{ row.cpuPercent.toFixed(1) }}%</template></el-table-column>
      <el-table-column label="RSS" width="110"><template #default="{ row }">{{ formatBytes(row.memoryRssBytes) }}</template></el-table-column>
      <el-table-column label="上次心跳（非健康判定）" min-width="210">
        <template #default="{ row }">
          <div>{{ formatTime(row.heartbeatAtMs) }}</div>
          <div class="muted">{{ formatHeartbeatAgeAtSnapshot(row.heartbeatAtMs, updatedAt) }}</div>
        </template>
      </el-table-column>
      <el-table-column label="详情" width="90">
        <template #default="{ row }"><el-button link type="primary" @click="openDetail(row)">查看</el-button></template>
      </el-table-column>
    </el-table>
  </section>
  <el-drawer v-model="detailOpen" title="程序详情" size="min(460px, 100%)">
    <div v-loading="detailLoading">
      <el-alert v-if="detailError" type="warning" :closable="false" :title="'详情未能刷新：' + detailError" />
      <el-descriptions v-if="detail" :column="1" border class="detail-list">
        <el-descriptions-item label="程序名称">{{ programName(detail.path, detail.id) }}</el-descriptions-item>
        <el-descriptions-item label="程序 ID">{{ detail.id }}</el-descriptions-item>
        <el-descriptions-item label="程序路径">{{ detail.path || '未提供' }}</el-descriptions-item>
        <el-descriptions-item label="PID">{{ detail.pid }}</el-descriptions-item>
        <el-descriptions-item label="运行状态">{{ detail.running ? '运行中' : '未运行' }}</el-descriptions-item>
        <el-descriptions-item label="CPU">{{ detail.cpuPercent.toFixed(1) }}%</el-descriptions-item>
        <el-descriptions-item label="RSS">{{ formatBytes(detail.memoryRssBytes) }}</el-descriptions-item>
        <el-descriptions-item label="上次心跳">{{ formatTime(detail.heartbeatAtMs) }}</el-descriptions-item>
        <el-descriptions-item label="相对详情快照">{{ formatHeartbeatAgeAtSnapshot(detail.heartbeatAtMs, detailGeneratedAt) }}</el-descriptions-item>
        <el-descriptions-item label="最后启动">{{ formatTime(detail.lastRunAtMs) }}</el-descriptions-item>
        <el-descriptions-item label="启动次数">{{ detail.startCount }} / {{ detail.maxCount }}</el-descriptions-item>
        <el-descriptions-item label="失败次数">{{ detail.errorCount }} / {{ detail.maxError }}</el-descriptions-item>
      </el-descriptions>
      <p class="muted detail-foot">仅显示 Monitor 的实际记录。启停与重启须待权限和审计完整后开放。</p>
    </div>
  </el-drawer>
</template>
