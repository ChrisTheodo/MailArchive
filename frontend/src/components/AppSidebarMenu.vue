<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useRouter, type RouteLocationRaw } from 'vue-router'
import ImportDialog from './ImportDialog.vue'

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
const router = useRouter()

// STATE
const menuItems = ref<Array<MenuItem>>([
  { id: 'dashboard', icon: 'i-mdi-chart-bar', title: 'Ταμπλό', to: { name: 'dashboard' } },
  { id: 'import', icon: 'i-mdi-import', title: 'Εισαγωγή αρχείου' },
  { id: 'logout', icon: 'i-mdi-logout', title: 'Αποσύνδεση', action: logout },
])

const menu = ref(false)

// METHODS
async function logout() {
  await authStore.logout()
  router.replace({ name: 'login' })
}
</script>

<template>
  <v-list-item
    density="compact"
    variant="elevated"
    :class="['flex-0-0', { 'w-50 align-self-center': expanded }]"
    :prepend-icon="menu ? 'i-mdi-menu-close' : 'i-mdi-menu-open'"
    rounded="md"
    prepend-gap="4"
  >
    Μενού
    <v-menu
      v-model="menu"
      activator="parent"
      close-delay="200"
      open-delay="200"
      location="end center"
    >
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
  </v-list-item>
</template>
