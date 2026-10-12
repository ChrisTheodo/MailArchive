import { createRouter, createWebHistory } from 'vue-router'
import MailList from '@/views/MailList.vue'
import MailDetail from '@/views/MailDetail.vue'
import { useAuthStore } from '@/stores/auth'
import { useMailboxStore } from '@/stores/mailbox'

const routes = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { requiresAuth: false },
  },

  {
    path: '/',
    component: () => import('@/views/MainLayout.vue'),
    children: [
      {
        path: '',
        name: 'home',
        component: MailList,
        beforeEnter: async () => {
          const mailboxStore = useMailboxStore()
          if (!mailboxStore.mailboxes.length) await mailboxStore.fetchMailboxes()
          const first = mailboxStore.mailboxes[0]
          if (first) return { name: 'mailbox', params: { mailbox: first.mailboxId } }
        },
      },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: () => import('@/views/DashboardView.vue'),
      },
      { path: 'mail/:mailbox', name: 'mailbox', component: MailList, props: true },
      {
        path: 'mail/:mailbox/:id',
        name: 'mail',
        component: MailDetail,
        props: true,
      },
      {
        path: ':pathMatch(.*)*',
        name: 'not-found',
        component: () => import('@/components/NotFound404.vue'),
        props: { code: '404' },
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  const { isAuthenticated } = useAuthStore()
  if (to.meta.requiresAuth !== false && !isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.name === 'login' && isAuthenticated) {
    return { name: 'home' }
  }
})

export default router
