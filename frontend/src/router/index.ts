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
    meta: { requiresAuth: false, title: 'Σύνδεση' },
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
        meta: { title: 'Ταμπλό' },
      },
      {
        path: 'search',
        name: 'search',
        component: EmailList,
        meta: { title: 'Αναζήτηση' },
      },
      {
        path: 'mail/:mailbox',
        name: 'mailbox',
        component: EmailList,
        props: true,
        meta: { title: 'Αρχική' },
      },
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
        meta: { title: 'Δεν βρέθηκε 404' },
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach(async (to) => {
  const authStore = useAuthStore()
  if (authStore.isAuthenticated) await authStore.ensureValidSession()

  if (to.meta.title) {
    document.title = to.meta.title as string
  }

  if (to.meta.requiresAuth !== false && !authStore.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.name === 'login' && authStore.isAuthenticated) {
    return { name: 'home' }
  }
})

// sends the user to the login page, keeping the current page to return to after login
export function redirectToLogin() {
  const currentRoute = router.currentRoute.value
  if (currentRoute.name === 'login') return

  router.replace({ name: 'login', query: { redirect: currentRoute.fullPath } })
}

export default router
