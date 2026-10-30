<script setup lang="ts">
import { computed } from 'vue'
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'
import { useThemeStore } from '../stores/theme'
import { useGuardStore } from '../stores/guard'
import CpuChart from '../components/CpuChart.vue'
import { formatBytes, programName } from '../utils/format'

const monitor = useMonitorStore()
const theme = useThemeStore()
const guard = useGuardStore()
const { report: guardReport, error: guardError } = storeToRefs(guard)
const closedCount = computed(() => guardReport.value?.terminal.available ? guardReport.value.terminal.items.length : null)
const orphanCount = computed(() => guardReport.value?.orphans.available ? guardReport.value.orphans.items.length : null)
const { overview, programs } = storeToRefs(monitor)
</script>

<template>
  <div class="stat-grid">
    <div class="metric-card"><div class="metric-label">程序总数</div><div class="metric-value">{{ overview?.total ?? '—' }}</div><div class="metric-note">Monitor 当前快照</div></div>
    <div class="metric-card"><div class="metric-label">运行中</div><div class="metric-value success">{{ overview?.running ?? '—' }}</div><div class="metric-note">进程运行，不代表业务就绪</div></div>
    <div class="metric-card"><div class="metric-label">未运行</div><div class="metric-value">{{ overview?.stopped ?? '—' }}</div><div class="metric-note">需要结合守护策略判断</div></div>
    <div class="metric-card"><div class="metric-label">监控进程 RSS</div><div class="metric-value smaller">{{ overview ? formatBytes(overview.memoryRssBytes) : '—' }}</div><div class="metric-note">程序内存统计合计</div></div>
  </div>
  <div class="dashboard-guard-strip">
    <RouterLink to="/guard" class="dashboard-guard-item">
      <div class="metric-label">达到守护上限</div>
      <strong :class="{ 'guard-alert-number': (closedCount ?? 0) > 0 }">{{ closedCount ?? '未知' }}</strong>
      <span class="muted">持久闭锁</span>
    </RouterLink>
    <RouterLink to="/guard" class="dashboard-guard-item">
      <div class="metric-label">启动隔离（含凭证）</div>
      <strong :class="{ 'guard-alert-number': (orphanCount ?? 0) > 0 }">{{ orphanCount ?? '未知' }}</strong>
      <span class="muted">上次启动扫描，非实时</span>
    </RouterLink>
    <div class="dashboard-guard-item">
      <div class="metric-label">守护证据状态</div>
      <strong>{{ guardError ? '查询失败' : guardReport ? (closedCount === null || orphanCount === null ? '来源不完整' : '已读取') : '未读取' }}</strong>
      <span class="muted">与业务 Ready 无关</span>
    </div>
  </div>
  <div class="content-grid">
    <section class="panel">
      <div class="panel-heading">
        <div><h2>程序运行状态</h2><p>真实快照 · 最近五个程序</p></div>
        <RouterLink to="/programs" class="subtle-link">查看全部</RouterLink>
      </div>
      <el-table :data="programs.slice(0, 5)" class="cx-table" empty-text="尚无程序记录">
        <el-table-column label="程序" min-width="165">
          <template #default="{ row }">{{ programName(row.path, row.id) }}</template>
        </el-table-column>
        <el-table-column prop="pid" label="PID" width="90" />
        <el-table-column label="状态" width="105">
          <template #default="{ row }"><el-tag :type="row.running ? 'success' : 'info'" effect="plain">{{ row.running ? '运行' : '停止' }}</el-tag></template>
        </el-table-column>
        <el-table-column label="CPU" width="95">
          <template #default="{ row }">{{ row.cpuPercent.toFixed(1) }}%</template>
        </el-table-column>
        <el-table-column label="内存" width="105">
          <template #default="{ row }">{{ formatBytes(row.memoryRssBytes) }}</template>
        </el-table-column>
      </el-table>
    </section>
    <section class="panel">
      <div class="panel-heading"><div><h2>CPU 使用率</h2><p>程序当前采样 · 最多 8 项</p></div></div>
      <CpuChart :items="programs" :theme="theme.mode" />
      <p class="muted chart-caption">按程序使用率排序；非整机 CPU 历史趋势。</p>
    </section>
  </div>
  <div class="dashboard-actions">
    <RouterLink to="/guard" class="subtle-link">检查守护闭锁与遗留进程 →</RouterLink>
    <RouterLink to="/events" class="subtle-link">查看原始事件记录 →</RouterLink>
  </div>
</template>
