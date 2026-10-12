import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useMailboxStore } from '@/stores/mailbox'
import EmailList from '@/views/EmailList.vue'
import EmailDetail from '@/views/EmailDetail.vue'

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
        component: EmailList,
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
      { path: 'mail/:mailbox', name: 'mailbox', component: EmailList, props: true },
      {
        path: 'mail/:mailbox/:id',
        name: 'mail',
        component: EmailDetail,
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
