<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { init, use, type EChartsType } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { Program } from '../types/supervision'
import type { ThemeMode } from '../stores/theme'
import { programName } from '../utils/format'

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])
const props = defineProps<{ items: Program[], theme: ThemeMode }>()
const element = ref<HTMLDivElement | null>(null)
let instance: EChartsType | undefined
let observer: ResizeObserver | undefined

function render(): void {
  if (!instance) return
  const dark = props.theme === 'dark'
  const items = [...props.items].sort((a, b) => b.cpuPercent - a.cpuPercent).slice(0, 8)
  instance.setOption({
    backgroundColor: 'transparent',
    grid: { top: 16, left: 8, right: 26, bottom: 12, containLabel: true },
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: (v: unknown) => String(v) + '%' },
    xAxis: {
      type: 'value', name: '%',
      axisLine: { show: false }, splitLine: { lineStyle: { color: dark ? '#29405b' : '#e2eafa' } },
      axisLabel: { color: dark ? '#95acc9' : '#607897' },
    },
    yAxis: {
      type: 'category', inverse: true,
      data: items.map(p => programName(p.path, p.id)),
      axisLabel: { color: dark ? '#c5dbf8' : '#263e64', width: 120, overflow: 'truncate' },
      axisLine: { show: false }, axisTick: { show: false },
    },
    series: [{
      type: 'bar', barMaxWidth: 16,
      data: items.map(p => Math.max(0, p.cpuPercent)),
      itemStyle: { color: dark ? '#33a8ff' : '#1479e8', borderRadius: [0, 4, 4, 0] },
    }],
  }, true)
}
onMounted(() => {
  if (!element.value) return
  instance = init(element.value, undefined, { renderer: 'canvas' })
  observer = new ResizeObserver(() => instance?.resize())
  observer.observe(element.value)
  render()
})
watch(() => [props.items, props.theme], render, { deep: true })
onBeforeUnmount(() => {
  observer?.disconnect()
  instance?.dispose()
})
</script>
<template>
  <div v-if="items.length" ref="element" class="cpu-chart"></div>
  <div v-else class="empty-chart">暂无可显示的 CPU 采样</div>
</template>
