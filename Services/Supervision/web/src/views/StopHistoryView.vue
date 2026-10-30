<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useStopHistoryStore } from '../stores/stop-history'
import { observationTime } from '../utils/guard'
import type { StopOperation } from '../types/supervision'

const store = useStopHistoryStore()
const { history, loading, error, updatedAt } = storeToRefs(store)
const search = ref('')
const onlyUnresolved = ref(false)
const rows = computed(() => history.value?.available ? history.value.items : [])
const filtered = computed(() => rows.value.filter(row => {
  if (onlyUnresolved.value && row.state !== 'unknown' && row.state !== 'requested') return false
  const q = search.value.trim().toLowerCase()
  return !q || row.programId.includes(q) || row.operationId.includes(q) ||
    row.action.toLowerCase().includes(q) || row.state.includes(q)
}))
const unresolved = computed(() => rows.value.filter(row => row.state === 'unknown' || row.state === 'requested').length)
const confirmed = computed(() => rows.value.filter(row => row.state === 'wait-confirmed').length)

function statusName(state: StopOperation['state']): string {
  switch (state) {
    case 'requested': return '仅记录请求'
    case 'unknown': return '退出结果未知'
    case 'refused': return '拒绝停止'
    case 'signal-failed': return '终止信号失败'
    case 'wait-confirmed': return '原 Worker Wait 已确认'
  }
}
function statusTone(state: StopOperation['state']): 'success'|'warning'|'danger'|'info' {
  if (state === 'wait-confirmed') return 'success'
  if (state === 'signal-failed' || state === 'refused') return 'danger'
  return 'warning'
}
onMounted(() => { void store.refresh() })
</script>

<template>
  <div class="guard-page">
    <div class="guard-summary">
      <div class="metric-card">
        <div class="metric-label">未结清停止请求</div>
        <div class="metric-value" :class="{ 'guard-alert-number': unresolved > 0 }">{{ history?.available ? unresolved : '—' }}</div>
        <div class="metric-note">requested / unknown；不是进程仍存活的证明</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">原 Worker 已确认退出</div>
        <div class="metric-value">{{ history?.available ? confirmed : '—' }}</div>
        <div class="metric-note">Wait 已返回，不等于设备 Ready</div>
      </div>
      <div class="metric-card">
        <div class="metric-label">流水来源</div>
        <div class="metric-value smaller">{{ history?.available ? '可读取' : '未知 / 不可用' }}</div>
        <div class="metric-note">最多展示最新 200 个操作；不是完整不可篡改审计</div>
      </div>
    </div>
    <el-alert type="info" show-icon :closable="false" class="guard-notice"
      title="本机只读停止结果 · Operation ID 区分每次实际请求"
      description="操作流水记录 requested → unknown / refused / signal-failed / wait-confirmed。即使 Kill 已发出，也不能推断进程退出；只有原 Worker 的 Wait 返回才确认。Daemon 强制结束前尚未记录结果时仍为 requested，不能当作已停止。" />
    <el-alert v-if="error || !history?.available" type="warning" show-icon :closable="false" class="guard-notice"
      :title="error ? '查询失败，所见数据可能过期：' + error : '停止流水不可用或尚未产生'"
      :description="history?.error || '不能把缺失操作历史理解为没有发生停止；请核对 Daemon 工作目录及访问权限。'" />
    <section class="panel">
      <div class="panel-heading">
        <div><h2>持久停止操作记录</h2>
          <p>以 Operation ID 去重，迟到的 Wait 确认优先于历史 unknown · 查询于 {{ observationTime(updatedAt) }}</p>
        </div>
        <el-button :loading="loading" @click="store.refresh">刷新</el-button>
      </div>
      <div class="filter-row">
        <el-input v-model="search" class="search-box" clearable placeholder="Program ID、Operation ID、操作、状态" />
        <el-checkbox v-model="onlyUnresolved">仅未结清</el-checkbox>
        <RouterLink to="/guard" class="subtle-link">守护诊断</RouterLink>
      </div>
      <el-table :data="filtered" row-key="operationId" class="cx-table"
        :empty-text="history?.available ? '已读取的最近记录中没有匹配操作' : '停止流水不可用，不能推断不存在操作'">
        <el-table-column label="最终已观察状态" min-width="185">
          <template #default="{ row }"><el-tag effect="plain" :type="statusTone(row.state)">{{ statusName(row.state) }}</el-tag></template>
        </el-table-column>
        <el-table-column label="Program ID" min-width="155">
          <template #default="{ row }">
            <RouterLink class="subtle-link" :to="{ path: '/programs', query: { id: row.programId } }">{{ row.programId }}</RouterLink>
          </template>
        </el-table-column>
        <el-table-column prop="pid" label="历史 PID" width="105" />
        <el-table-column prop="action" label="来源动作" min-width="132" />
        <el-table-column label="观察时间" min-width="176">
          <template #default="{ row }">{{ observationTime(row.recordedAt) }}</template>
        </el-table-column>
        <el-table-column prop="operationId" label="Operation ID" min-width="240" show-overflow-tooltip />
        <el-table-column label="错误 / Wait 退出信息" min-width="200">
          <template #default="{ row }">
            {{ row.signalError || row.waitError || '—' }}
          </template>
        </el-table-column>
      </el-table>
      <p class="muted detail-foot">不提供停止、重启、解锁按钮。记录来源为 Daemon 工作目录，受大小上限保护；若已满，后续停止将拒绝执行，须受控离线归档。</p>
    </section>
  </div>
</template>
