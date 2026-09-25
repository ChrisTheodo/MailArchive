<script setup>
import { ref } from 'vue'
import { useMailStore } from '@/stores/mail'
import AppSidebar from '@/components/AppSidebar.vue'

const mail = useMailStore()

const drawer = ref(null)
const rail = ref(false)

function toggleSidebar (isMobile) {
  if (isMobile) drawer.value = !drawer.value
  else rail.value = !rail.value
}
</script>

<template>
  <v-app>
    <v-app-bar flat color="background" height="64">
      <template #prepend>
        <v-app-bar-nav-icon @click="toggleSidebar($vuetify.display.mobile)" />
      </template>

      <div class="brand">
        <v-avatar color="primary" size="36" rounded="lg">
          <v-icon icon="mdi-email-fast-outline" />
        </v-avatar>
        <span class="brand-title">Toolbox Front Office</span>
      </div>

      <v-text-field
        v-model="mail.search"
        class="search mx-4"
        placeholder="Search mail"
        prepend-inner-icon="mdi-magnify"
        variant="solo-filled"
        density="comfortable"
        rounded="pill"
        flat
        hide-details
        clearable
      />

      <template #append>
        <v-btn icon="mdi-cog-outline" variant="text" />
        <v-avatar color="secondary" size="34" class="ml-2">M</v-avatar>
      </template>
    </v-app-bar>

    <v-navigation-drawer
      v-model="drawer"
      :rail="rail && !$vuetify.display.mobile"
      color="background"
      border="0"
      width="256"
    >
      <AppSidebar :rail="rail && !$vuetify.display.mobile" />
    </v-navigation-drawer>

    <v-main>
      <div class="content-wrap">
        <v-sheet class="content" rounded="xl">
          <router-view :key="$route.fullPath" />
        </v-sheet>
      </div>
    </v-main>
  </v-app>
</template>

<style scoped>

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 200px;
}

.brand-title {
  font-size: 1.35rem;
  font-weight: 500;
  white-space: nowrap;
}

.search {
  max-width: 720px;
}

.content-wrap {
  height: calc(100vh - 64px);
  padding: 0 16px 16px 0;
}

.content {
  height: 100%;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

@media (max-width: 600px) {
  .content-wrap { padding: 0; }
  .brand { min-width: 0; }
  .brand-title { display: none; }
}
</style>
