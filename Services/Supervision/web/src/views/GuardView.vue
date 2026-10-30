<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useGuardStore } from '../stores/guard'
import { useMonitorStore } from '../stores/monitor'
import type { GuardIssue, GuardSource } from '../types/supervision'
import { observationTime } from '../utils/guard'
import { programName } from '../utils/format'

const guard = useGuardStore()
const monitor = useMonitorStore()
const { report, error, loading, fetchedAt } = storeToRefs(guard)
const { programs } = storeToRefs(monitor)
const query = ref('')
const category = ref('all')

interface DisplayIssue extends GuardIssue {
  key: string
  category: '终止闭锁' | '孤儿隔离'
  path: string
}

const issues = computed<DisplayIssue[]>(() => {
  if (!report.value) return []
  const collected: DisplayIssue[] = []
  const index = new Map(programs.value.map(p => [p.id, p.path]))
  function collect(source: GuardSource, category: DisplayIssue['category']): void {
    if (!source.available) return
    for (const issue of source.items) {
      collected.push({ ...issue, key: category + ':' + issue.programId, category, path: index.get(issue.programId) ?? '' })
    }
  }
  collect(report.value.terminal, '终止闭锁')
  collect(report.value.orphans, '孤儿隔离')
  return collected
})

const filtered = computed(() => issues.value.filter(item => {
  if (category.value !== 'all' && item.category !== category.value) return false
  const q = query.value.trim().toLowerCase()
  return !q || item.programId.includes(q) || item.path.toLowerCase().includes(q) ||
    item.reason.toLowerCase().includes(q) || item.category.includes(q)
}))
const terminalCount = computed(() => report.value?.terminal.available ? report.value.terminal.items.length : null)
const orphanCount = computed(() => report.value?.orphans.available ? report.value.orphans.items.length : null)
const anyUnavailable = computed(() => !report.value || !report.value.terminal.available || !report.value.orphans.available)
onMounted(() => { void guard.refresh() })
</script>

<template>
  <div class="guard-page">
    <div class="guard-summary">
      <div class="metric-card">
        <div class="metric-label">守护上限闭锁</div>
        <div class="metric-value" :class="{ 'guard-alert-number': (terminalCount ?? 0) > 0 }">{{ terminalCount ?? '—' }}</div>
        <div class="metric-note">Daemon 本地持久锁；非活动告警总数</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">启动扫描隔离</div>
        <div class="metric-value" :class="{ 'guard-alert-number': (orphanCount ?? 0) > 0 }">{{ orphanCount ?? '—' }}</div>
        <div class="metric-note">上次启动扫描，不代表实时存活数量</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">诊断来源</div>
        <div class="metric-value smaller">{{ report ? (anyUnavailable ? '部分不可用' : '均可读取') : '未读取' }}</div>
        <div class="metric-note">来源缺失和 API 失败不等于没有故障</div>
      </div>
    </div>

    <el-alert type="info" :closable="false" show-icon class="guard-notice"
      title="安全只读视图 · 不提供启停、重启、解锁或确认"
      description="闭锁来源于 Daemon 终止记录，隔离来源于上次启动的 PID 扫描。均不是实时 OS 进程身份、业务 Ready 或设备安全许可；处理前必须按现场运维规程核查。" />
    <el-alert v-if="error" type="warning" :closable="false" show-icon class="guard-notice"
      :title="'守护诊断查询未刷新：' + error"
      description="已显示的历史快照可能过期；请先确认 Monitor 本机访问与 Daemon 工作目录。" />

    <div class="guard-sources">
      <section class="panel" v-for="source in [
        { key: 'terminal', name: '终止闭锁日志', data: report?.terminal, note: '原文件更新时间，不代表当前进程运行状态' },
        { key: 'orphans', name: '遗留进程扫描', data: report?.orphans, note: 'Daemon 上一次启动扫描时刻，可能过期' },
      ]" :key="source.key">
        <div class="panel-heading">
          <div><h2>{{ source.name }}</h2><p>{{ source.note }}</p></div>
          <el-tag :type="source.data?.available ? 'info' : 'warning'" effect="plain">
            {{ source.data?.available ? '已读取' : '未知 / 不可用' }}
          </el-tag>
        </div>
        <div class="guard-source-time">观察时间：{{ observationTime(source.data?.observedAt) }}</div>
        <div v-if="!source.data?.available" class="muted guard-error">
          {{ source.data?.error || '尚未完成首次查询，或当前目录不具备读取条件。' }}
        </div>
        <div v-else class="muted">本次文件记录 {{ source.data?.items.length ?? 0 }} 项。记录为 0 不表示当前设备、服务或业务健康。</div>
      </section>
    </div>

    <section class="panel">
      <div class="panel-heading">
        <div>
          <h2>闭锁与隔离明细</h2>
          <p>按 Program ID 对齐 Monitor 当前程序清单 · 已读取的来源共 {{ issues.length }} 条</p>
        </div>
        <el-button :loading="loading" @click="guard.refresh">刷新诊断</el-button>
      </div>
      <div class="filter-row">
        <el-input v-model="query" class="search-box" clearable placeholder="搜索 Program ID、路径、原因" />
        <el-select v-model="category" style="width: 140px" aria-label="保护类型筛选">
          <el-option label="全部类型" value="all" />
          <el-option label="终止闭锁" value="终止闭锁" />
          <el-option label="孤儿隔离" value="孤儿隔离" />
        </el-select>
        <span class="muted">查询于 {{ observationTime(fetchedAt) }}</span>
      </div>
      <el-table :data="filtered" class="cx-table" row-key="key"
        :empty-text="anyUnavailable ? '数据源不可用或没有可展示的记录，不代表安全' : '已读取的诊断文件没有匹配记录'">
        <el-table-column label="类型" width="128">
          <template #default="{ row }">
            <el-tag :type="row.category === '终止闭锁' ? 'danger' : 'warning'" effect="plain">{{ row.category }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="Program ID" min-width="165">
          <template #default="{ row }">
            <RouterLink class="subtle-link" :to="{ path: '/programs', query: { id: row.programId } }">{{ row.programId }}</RouterLink>
          </template>
        </el-table-column>
        <el-table-column label="程序" min-width="190">
          <template #default="{ row }">
            {{ row.path ? programName(row.path, row.programId) : 'Monitor 未提供名称' }}
          </template>
        </el-table-column>
        <el-table-column prop="reason" label="记录原因" min-width="300" show-overflow-tooltip />
      </el-table>
      <div class="guard-foot">
        <RouterLink to="/programs" class="subtle-link">核对程序状态</RouterLink>
        <span class="muted">解除闭锁或隔离需现场授权、离线核验与完整审计；这里没有操作按钮。</span>
      </div>
    </section>
  </div>
</template>
