import { createRouter, createWebHashHistory } from 'vue-router'
import DashboardView from '../views/DashboardView.vue'
import ProgramsView from '../views/ProgramsView.vue'
import EventsView from '../views/EventsView.vue'
import AlertsView from '../views/AlertsView.vue'
import GuardView from '../views/GuardView.vue'
import StopHistoryView from '../views/StopHistoryView.vue'

export const router = createRouter({
  // Hash routing works with the legacy Gin static-file hosting arrangement.
  history: createWebHashHistory('/assets/comm/'),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/programs', name: 'programs', component: ProgramsView },
    { path: '/events', name: 'events', component: EventsView },
    { path: '/alerts', name: 'alerts', component: AlertsView },
    { path: '/guard', name: 'guard', component: GuardView },
    { path: '/stops', name: 'stops', component: StopHistoryView },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})
