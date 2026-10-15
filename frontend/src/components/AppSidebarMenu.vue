<script setup lang="ts">
import { ref, computed, onBeforeMount } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import ImportDialog from './ImportDialog.vue'
import { useUserStore } from '@/stores/user.ts'

interface MenuItem {
  id: string
  icon: string
  title: string
  to?: RouteLocationRaw
  action?: () => unknown
}

defineProps({ expanded: Boolean })

// CONSTANTS
const authStore = useAuthStore()
const userStore = useUserStore()
const router = useRouter()

// STATE
const menuItems = ref<Array<MenuItem>>([
  { id: 'dashboard', icon: 'i-mdi-chart-bar', title: 'Ταμπλό', to: { name: 'dashboard' } },
  { id: 'import', icon: 'i-mdi-import', title: 'Εισαγωγή αρχείου' },
  { id: 'logout', icon: 'i-mdi-logout', title: 'Αποσύνδεση', action: logout },
])

const menu = ref(false)
const avatarText = computed(
  () => userStore.currentUser?.displayName?.trim().charAt(0).toUpperCase() || 'Ο',
)

// METHODS
async function logout() {
  await authStore.logout()
  router.replace({ name: 'login' })
}

onBeforeMount(() => userStore.fetchMe())
</script>

<template>
  <v-skeleton-loader
    :loading="userStore.isMeLoading"
    type="list-item-avatar-two-line"
    color="background"
    class="flex-0-0"
  >
    <v-list-item variant="text" class="flex-0-0" rounded="md" lines="one">
      <template #prepend>
        <v-avatar color="primary">
          <span :class="['text-headline-small', { 'text-label-small': !expanded }]">{{
            avatarText
          }}</span>
        </v-avatar>
      </template>

      <v-list-item-title v-if="expanded">
        {{ userStore.currentUser?.displayName ?? 'Όνομα χρήστη' }}
        <v-tooltip activator="parent" location="end" open-delay="150">
          <div class="font-weight-medium">
            {{ userStore.currentUser?.displayName ?? 'Όνομα χρήστη' }}
          </div>
        </v-tooltip>
      </v-list-item-title>

      <v-list-item-subtitle v-if="expanded">
        {{ userStore.currentUser?.email ?? 'Email χρήστη' }}
        <v-tooltip activator="parent" location="end" open-delay="150">
          <div class="font-weight-medium">{{ userStore.currentUser?.email ?? 'Email χρήστη' }}</div>
        </v-tooltip>
      </v-list-item-subtitle>

      <template v-if="expanded" #append>
        <v-menu v-model="menu" close-delay="200" open-delay="200" location="end center">
          <template #activator="{ props: menuProps }">
            <v-btn
              v-bind="menuProps"
              id="menu-btn"
              :icon="menu ? 'i-mdi-menu-close' : 'i-mdi-menu-open'"
              size="x-small"
              variant="text"
            />
          </template>

          <v-list density="compact" slim>
            <v-list-item
              v-for="(item, index) in menuItems"
              :key="item.id"
              :value="index"
              :append-icon="item.icon"
              @click="item.action?.()"
              :to="item.to"
            >
              {{ item.title }}

              <ImportDialog v-if="item.id === 'import'" />
            </v-list-item>
          </v-list>
        </v-menu>
      </template>
    </v-list-item>
  </v-skeleton-loader>
</template>
