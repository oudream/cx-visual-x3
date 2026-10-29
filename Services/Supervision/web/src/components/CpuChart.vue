<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import * as echarts from 'echarts'
import type { ECharts } from 'echarts'
import type { Program } from '../types/supervision'
import { programName } from '../utils/format'

const props = defineProps<{ items: Program[] }>()
const element = ref<HTMLDivElement | null>(null)
let instance: ECharts | undefined
let observer: ResizeObserver | undefined

function render() {
  if (!instance) return
  const items = [...props.items].sort((a, b) => b.cpuPercent - a.cpuPercent).slice(0, 8)
  instance.setOption({
    backgroundColor: 'transparent',
    grid: { top: 16, left: 12, right: 26, bottom: 20, containLabel: true },
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v: unknown) => String(v) + '%' },
    xAxis: { type: 'value', name: '%', axisLine: { show: false }, splitLine: { lineStyle: { color: '#273449' } }, axisLabel: { color: '#9aabc0' } },
    yAxis: { type: 'category', inverse: true, data: items.map(p => programName(p.path, p.id)), axisLabel: { color: '#c7d6e8', width: 135, overflow: 'truncate' }, axisLine: { show: false }, axisTick: { show: false } },
    series: [{ type: 'bar', barMaxWidth: 18, data: items.map(p => Math.max(0, p.cpuPercent)), itemStyle: { color: '#3bb9ad', borderRadius: [0, 4, 4, 0] } }],
  }, true)
}
onMounted(() => {
  if (!element.value) return
  instance = echarts.init(element.value, undefined, { renderer: 'canvas' })
  observer = new ResizeObserver(() => instance?.resize())
  observer.observe(element.value)
  render()
})
watch(() => props.items, render, { deep: true })
onBeforeUnmount(() => {
  observer?.disconnect()
  instance?.dispose()
})
</script>

<template>
  <div v-if="items.length" ref="element" class="cpu-chart"></div>
  <div v-else class="empty-chart">没有可显示的进程数据</div>
</template>
