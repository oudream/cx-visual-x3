<script setup lang="ts">
import { computed, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'
import { formatBytes, formatTime, programName } from '../utils/format'

const store = useMonitorStore()
const { programs } = storeToRefs(store)
const filter = ref('')
const filtered = computed(() => programs.value.filter(p => {
  const text = filter.value.trim().toLowerCase()
  return !text || String(p.id).includes(text) || p.path.toLowerCase().includes(text)
}))
</script>

<template>
  <section class="panel">
    <div class="panel-heading">
      <div><h2>已管理程序</h2><p>共 {{ filtered.length }} 条 · 仅查询，不允许远程启停</p></div>
      <el-input v-model="filter" placeholder="搜索程序 ID 或路径" clearable class="search-box" />
    </div>
    <el-table :data="filtered" class="cx-table" row-key="id" empty-text="没有匹配的程序">
      <el-table-column label="程序 / 路径" min-width="260">
        <template #default="{ row }">
          <strong>{{ programName(row.path, row.id) }}</strong>
          <div class="path-label">{{ row.path || '未提供路径' }}</div>
        </template>
      </el-table-column>
      <el-table-column prop="id" label="ID" width="90" />
      <el-table-column prop="pid" label="PID" width="90" />
      <el-table-column label="运行状态" width="110">
        <template #default="{ row }"><el-tag :type="row.running ? 'success' : 'info'">{{ row.running ? '运行中' : '未运行' }}</el-tag></template>
      </el-table-column>
      <el-table-column label="CPU" width="105">
        <template #default="{ row }">{{ row.cpuPercent.toFixed(1) }}%</template>
      </el-table-column>
      <el-table-column label="RSS" width="110">
        <template #default="{ row }">{{ formatBytes(row.memoryRssBytes) }}</template>
      </el-table-column>
      <el-table-column label="最后心跳" min-width="190">
        <template #default="{ row }">{{ formatTime(row.heartbeatAtMs) }}</template>
      </el-table-column>
      <el-table-column prop="startCount" label="启动次数" width="105" />
      <el-table-column prop="errorCount" label="错误次数" width="105" />
    </el-table>
  </section>
</template>
