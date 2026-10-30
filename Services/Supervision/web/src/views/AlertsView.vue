<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useAlertHistoryStore } from '../stores/alerts'
import { formatTime } from '../utils/format'

const store = useAlertHistoryStore()
const { items, error, loading, fetchedAt, coveredBytes } = storeToRefs(store)
const search = ref('')
const onlyCritical = ref(false)
const filtered = computed(() => items.value.filter(item => {
  if (onlyCritical.value && item.severity !== 'critical') return false
  const query = search.value.trim().toLowerCase()
  return !query || item.programId.includes(query) ||
    item.message.toLowerCase().includes(query) || label(item.kind).includes(query)
}))

function label(kind: string): string {
  switch (kind) {
    case 'max-error': return '失败次数上限'
    case 'max-start': return '启动次数上限'
    case 'start-failed': return '启动失败'
    default: return '未分类异常'
  }
}
onMounted(() => { void store.refresh() })
</script>

<template>
  <section class="panel">
    <div class="panel-heading">
      <div>
        <h2>历史异常事件</h2>
        <p>从 Monitor 原始事件日志提取明确的故障记录，非活动告警列表</p>
      </div>
      <el-button :loading="loading" @click="store.refresh">刷新异常</el-button>
    </div>
    <el-alert type="info" :closable="false" show-icon
      title="这里显示的是日志窗口内发生过的异常，不表示故障仍在发生或已恢复。"
      description="未配置登录及访问权限之前，本功能仅允许在 Monitor 主机本地查看；不支持确认、清除或远程处置。" />
    <el-alert v-if="error" type="warning" :closable="false" show-icon
      title="异常记录暂不可用"
      :description="error.startsWith('访问被拒绝') ? '接口仅允许本机访问。远程查看须完成安全鉴权与日志脱敏。' : error" />
    <div class="filter-row">
      <el-input v-model="search" clearable class="search-box" placeholder="搜索程序 ID 或异常内容" />
      <el-checkbox v-model="onlyCritical">仅严重异常</el-checkbox>
      <span class="muted">{{ fetchedAt ? '查询时间 ' + new Date(fetchedAt).toLocaleTimeString('zh-CN', { hour12: false }) : '尚未读取' }}</span>
    </div>
    <el-table :data="filtered" class="cx-table" :empty-text="error ? '查询不可用' : '所查询日志窗口内没有匹配的异常'">
      <el-table-column label="时间" min-width="168">
        <template #default="{ row }">{{ formatTime(row.time) }}</template>
      </el-table-column>
      <el-table-column label="事件" min-width="140">
        <template #default="{ row }"><el-tag :type="row.severity === 'critical' ? 'danger' : 'warning'" effect="plain">{{ label(row.kind) }}</el-tag></template>
      </el-table-column>
      <el-table-column label="程序 ID" min-width="148">
        <template #default="{ row }">
          <RouterLink class="subtle-link" :to="{ path: '/programs', query: { id: row.programId } }">{{ row.programId }}</RouterLink>
        </template>
      </el-table-column>
      <el-table-column prop="pid" label="PID" width="90" />
      <el-table-column prop="message" label="原始说明" min-width="280" show-overflow-tooltip />
    </el-table>
    <p class="muted" style="margin-top: 12px">
      {{ fetchedAt ? '查询窗口：最近 ' + (coveredBytes / 1048576).toFixed(0) + ' MiB 的日志尾部；至多返回 100 条异常。没有记录不代表完整历史中从未发生异常。' : '尚未读取异常日志。' }}
    </p>
  </section>
</template>
