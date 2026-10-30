<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useResourceStore } from '../stores/resources'
import { useMonitorStore } from '../stores/monitor'
import { formatBytes, programName } from '../utils/format'
import { observationTime } from '../utils/guard'

const resources = useResourceStore()
const monitor = useMonitorStore()
const { report, loading, error, updatedAt } = storeToRefs(resources)
const { programs, error: monitorError, updatedAt: programsAt } = storeToRefs(monitor)
const search = ref('')
const byCPU = computed(() => [...programs.value].sort((a, b) => b.cpuPercent - a.cpuPercent))
const visiblePrograms = computed(() => byCPU.value.filter(p => {
  const term = search.value.trim().toLowerCase()
  return !term || p.id.includes(term) || p.path.toLowerCase().includes(term)
}))
const readableCPU = computed(() =>
  report.value?.cpu.available ? report.value.cpu.percent.toFixed(1) + '%' : '—')
const readableMemory = computed(() =>
  report.value?.memory.available ? formatBytes(report.value.memory.usedBytes) : '—')
const readableDisk = computed(() =>
  report.value?.disk.available ? formatBytes(report.value.disk.usedBytes) : '—')
const anyMissing = computed(() => !report.value || !report.value.cpu.available ||
  !report.value.memory.available || !report.value.disk.available)

function refresh(): void {
  void resources.refresh()
  void monitor.refresh()
}
onMounted(refresh)
</script>

<template>
  <div class="guard-page">
    <el-alert type="info" show-icon :closable="false" class="guard-notice"
      title="主机指标与受管程序指标分别采样"
      description="主机 CPU 和物理内存属于整台机器；磁盘指标只对应 Monitor 工作目录所在文件系统。程序 CPU/RSS 取自 Monitor 的进程快照。没有连续采样历史，因此本页只展示当前值，不生成历史趋势或设备 Ready 结论。" />
    <el-alert v-if="error || monitorError || anyMissing" type="warning" show-icon :closable="false" class="guard-notice"
      :title="error || monitorError ? '最新查询失败，已显示的旧值可能过期' : '部分资源指标无法采集'"
      :description="error || monitorError || '不可用的指标显示为破折号，并非 0% 或资源正常。'" />

    <div class="guard-summary">
      <section class="metric-card">
        <div class="metric-label">主机 CPU 使用率</div>
        <div class="metric-value">{{ readableCPU }}</div>
        <div class="metric-note">整机 · 约 200ms 采样 · {{ observationTime(report?.cpu.sampledAt) }}</div>
        <p v-if="!report?.cpu.available" class="muted guard-error">{{ report?.cpu.error || '未取得采样' }}</p>
      </section>
      <section class="metric-card">
        <div class="metric-label">主机物理内存已用</div>
        <div class="metric-value smaller">{{ readableMemory }}</div>
        <div class="metric-note">
          {{ report?.memory.available ? report.memory.usedPercent.toFixed(1) + '% · 共 ' + formatBytes(report.memory.totalBytes) : '未知' }}
        </div>
        <div v-if="report?.memory.available" class="muted">可用 {{ formatBytes(report.memory.availableBytes) }}</div>
        <p v-else class="muted guard-error">{{ report?.memory.error || '未取得采样' }}</p>
      </section>
      <section class="metric-card">
        <div class="metric-label">工作目录所在磁盘已用</div>
        <div class="metric-value smaller">{{ readableDisk }}</div>
        <div class="metric-note">
          {{ report?.disk.available ? report.disk.usedPercent.toFixed(1) + '% · 共 ' + formatBytes(report.disk.totalBytes) : '未知' }}
        </div>
        <div v-if="report?.disk.available" class="muted">剩余 {{ formatBytes(report.disk.availableBytes) }}</div>
        <p v-else class="muted guard-error">{{ report?.disk.error || '未取得采样' }}</p>
      </section>
    </div>

    <section class="panel">
      <div class="panel-heading">
        <div>
          <h2>受管程序当前资源</h2>
          <p>按进程 CPU 降序 · 程序 RSS 为各自进程的常驻内存，不与整机内存混为一谈</p>
        </div>
        <el-button :loading="loading" @click="refresh">刷新采样</el-button>
      </div>
      <div class="filter-row">
        <el-input v-model="search" clearable class="search-box" placeholder="搜索 Program ID 或可执行路径" />
        <span class="muted">主机采样查询 {{ observationTime(updatedAt) }} · 进程快照 {{ observationTime(programsAt) }}</span>
      </div>
      <el-table :data="visiblePrograms" class="cx-table" row-key="id"
        :empty-text="monitorError ? 'Monitor 进程快照不可用' : '暂无匹配程序'">
        <el-table-column label="Program ID" min-width="165">
          <template #default="{ row }"><RouterLink class="subtle-link"
            :to="{ path: '/programs', query: { id: row.id } }">{{ row.id }}</RouterLink></template>
        </el-table-column>
        <el-table-column label="程序" min-width="210">
          <template #default="{ row }">{{ programName(row.path, row.id) }}</template>
        </el-table-column>
        <el-table-column prop="pid" label="PID" width="105" />
        <el-table-column label="当前运行" width="110">
          <template #default="{ row }"><el-tag effect="plain" :type="row.running ? 'success' : 'info'">
            {{ row.running ? '进程运行' : '未运行' }}
          </el-tag></template>
        </el-table-column>
        <el-table-column label="进程 CPU" width="115">
          <template #default="{ row }">{{ row.cpuPercent.toFixed(1) }}%</template>
        </el-table-column>
        <el-table-column label="进程 RSS" width="130">
          <template #default="{ row }">{{ formatBytes(row.memoryRssBytes) }}</template>
        </el-table-column>
      </el-table>
      <p class="muted detail-foot">
        该视图只读；无法观察到的 GPU、网络、历史趋势和服务/设备就绪不推断、不补零。
      </p>
    </section>
  </div>
</template>
