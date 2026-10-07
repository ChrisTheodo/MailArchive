<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAuthStore } from '@/stores/auth'
import type { RouteLocationRaw } from 'vue-router'

defineProps({ expanded: Boolean })

// CONSTANTS
const authStore = useAuthStore()

// STATE
const menuItems = computed(() => {
  const items: Array<{
    id: string
    icon: string
    title: string
    to?: RouteLocationRaw
    action?: () => unknown
  }> = [
    { id: 'dashboard', icon: 'i-mdi-chart-bar', title: 'Ταμπλό', to: { name: 'dashboard' } },
    { id: 'settings', icon: 'i-mdi-cog-outline', title: 'Ρυθμίσεις' },
  ]

  if (authStore.isAuthenticated) {
    items.push({
      id: 'logout',
      icon: 'i-mdi-logout',
      title: 'Αποσύνδεση',
      action: authStore.logout,
    })
  } else {
    items.push({ id: 'login', icon: 'i-mdi-login', title: 'Σύνδεση', action: authStore.login })
  }

  return items
})
const menu = ref(false)
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
        </v-list-item>
      </v-list>
    </v-menu>
  </v-list-item>
</template>
