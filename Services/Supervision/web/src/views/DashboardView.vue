<script setup lang="ts">
import { storeToRefs } from 'pinia'
import { useMonitorStore } from '../stores/monitor'
import CpuChart from '../components/CpuChart.vue'
import { formatBytes, programName } from '../utils/format'

const store = useMonitorStore()
const { overview, programs } = storeToRefs(store)
</script>

<template>
  <div class="stat-grid">
    <div class="metric-card"><div class="metric-label">程序总数</div><div class="metric-value">{{ overview?.total ?? '—' }}</div><div class="metric-note">当前状态快照</div></div>
    <div class="metric-card"><div class="metric-label">运行中</div><div class="metric-value success">{{ overview?.running ?? '—' }}</div><div class="metric-note">守护状态报告</div></div>
    <div class="metric-card"><div class="metric-label">未运行</div><div class="metric-value">{{ overview?.stopped ?? '—' }}</div><div class="metric-note">不等于业务故障</div></div>
    <div class="metric-card"><div class="metric-label">已报告心跳</div><div class="metric-value">{{ overview?.heartbeatReporting ?? '—' }}</div><div class="metric-note">只表示存在心跳记录</div></div>
  </div>
  <div class="content-grid">
    <section class="panel">
      <div class="panel-heading"><div><h2>进程 CPU 使用率</h2><p>最新快照 · 最多显示 8 个程序</p></div></div>
      <CpuChart :items="programs" />
    </section>
    <section class="panel">
      <div class="panel-heading"><div><h2>资源概览</h2><p>当前受监控程序的统计值</p></div></div>
      <div class="resource-value">{{ overview ? overview.cpuPercent.toFixed(1) + '%' : '—' }}<small>CPU 使用率合计</small></div>
      <div class="resource-value">{{ overview ? formatBytes(overview.memoryRssBytes) : '—' }}<small>内存 RSS 合计</small></div>
      <div class="note">CPU 数值为各程序当前采样的合计，不等于整机 CPU 使用率。</div>
    </section>
  </div>
  <section class="panel recent">
    <div class="panel-heading">
      <div><h2>程序实时状态</h2><p>来源：Monitor 程序快照</p></div>
      <RouterLink class="subtle-link" to="/programs">查看全部程序</RouterLink>
    </div>
    <el-table :data="programs.slice(0, 6)" class="cx-table" empty-text="暂无程序数据">
      <el-table-column label="程序" min-width="180">
        <template #default="{ row }">{{ programName(row.path, row.id) }}</template>
      </el-table-column>
      <el-table-column prop="pid" label="PID" width="95" />
      <el-table-column label="状态" width="112">
        <template #default="{ row }"><el-tag :type="row.running ? 'success' : 'info'">{{ row.running ? '运行中' : '未运行' }}</el-tag></template>
      </el-table-column>
      <el-table-column label="CPU" width="110">
        <template #default="{ row }">{{ row.cpuPercent.toFixed(1) }}%</template>
      </el-table-column>
      <el-table-column label="内存" width="110">
        <template #default="{ row }">{{ formatBytes(row.memoryRssBytes) }}</template>
      </el-table-column>
    </el-table>
  </section>
</template>
