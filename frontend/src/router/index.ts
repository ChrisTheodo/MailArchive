import { createRouter, createWebHistory } from 'vue-router'
import MailList from '@/views/MailList.vue'
import MailDetail from '@/views/MailDetail.vue'

const routes = [
  { path: '/', redirect: '/mail/mailbox-1/inbox' },
  {
    path: '/dashboard',
    name: 'dashboard',
    component: () => import('@/views/DashboardView.vue'),
    props: true,
  },
  { path: '/mail/:mailbox/:folder', name: 'folder', component: MailList, props: true },
  { path: '/mail/:mailbox/:folder/:id', name: 'folder-mail', component: MailDetail, props: true },
  { path: '/label/:label', name: 'label', component: MailList, props: true },
  { path: '/label/:label/:id', name: 'label-mail', component: MailDetail, props: true },
  { path: '/:pathMatch(.*)*', redirect: '/mail/mailbox-1/inbox' },
]

export default createRouter({
  history: createWebHistory(),
  routes,
})
