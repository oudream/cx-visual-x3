<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useEventsStore } from '../stores/events'
import { eventLabel, eventLevel, filterEvents } from '../utils/events'
import { formatTime } from '../utils/format'

const store = useEventsStore()
const { items, loading, error, fetchedAt } = storeToRefs(store)
const query = ref('')
const showHeartbeat = ref(false)
const filtered = computed(() => filterEvents(items.value, query.value, showHeartbeat.value))

onMounted(() => { void store.refresh() })
</script>

<template>
  <section class="panel">
    <div class="panel-heading">
      <div>
        <h2>事件记录</h2>
        <p>沿用 Monitor 原始事件文件，最新最多 100 条 · 当前仅允许本机读取</p>
      </div>
      <el-button :loading="loading" @click="store.refresh">刷新事件</el-button>
    </div>
    <el-alert v-if="error" type="warning" show-icon :closable="false"
      title="事件读取不可用"
      :description="error.startsWith('访问被拒绝') ? '当前接口仅允许本机访问。远程查看须先配置登录、授权和日志脱敏。' : error" />
    <div class="filter-row">
      <el-input v-model="query" clearable placeholder="按程序 ID、事件类型或内容搜索" class="search-box" />
      <el-checkbox v-model="showHeartbeat">包含心跳记录</el-checkbox>
      <span class="muted">{{ fetchedAt ? '读取于 ' + new Date(fetchedAt).toLocaleTimeString('zh-CN', { hour12: false }) : '未读取' }}</span>
    </div>
    <el-table :data="filtered" class="cx-table" :empty-text="error ? '事件暂不可用' : '暂无事件记录'">
      <el-table-column label="时间" min-width="170">
        <template #default="{ row }">{{ formatTime(row.time) }}</template>
      </el-table-column>
      <el-table-column label="事件" width="150">
        <template #default="{ row }">
          <el-tag :type="eventLevel(row.event)" effect="plain">{{ eventLabel(row.event) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="程序 ID" min-width="155">
        <template #default="{ row }">
          <RouterLink class="subtle-link" :to="{ path: '/programs', query: { id: row.programId } }">{{ row.programId }}</RouterLink>
        </template>
      </el-table-column>
      <el-table-column prop="pId" label="PID" width="105" />
      <el-table-column prop="msg" label="说明" min-width="300" show-overflow-tooltip />
    </el-table>
  </section>
</template>
