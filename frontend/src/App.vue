<script setup lang="ts">
import { computed, ref } from 'vue'
import { useMailStore } from '@/stores/mail'
import AppSidebar from '@/components/AppSidebar.vue'
import toolboxLogo from '@/assets/toolbox-logo.svg'
import { getSidebarRailWidth, SIDEBAR_DENSITY } from './constants/layout/app-side-bar'
import { useDisplay } from 'vuetify'

// CONSTANTS
const mail = useMailStore()

// STATE
const drawer = ref<boolean | null>(true)
const rail = ref(false)
const aiSearchActive = ref(false)
const { mobile } = useDisplay()
const isHovering = ref(false)
const sidebarExpanded = computed(() => !rail.value || isHovering.value)

// FUNCTIONS
function toggleSidebar() {
  if (mobile.value) {
    drawer.value = !drawer.value
  } else {
    rail.value = !rail.value
  }
}

function toggleAISearchMode() {
  aiSearchActive.value = !aiSearchActive.value
}
</script>

<template>
  <v-app>
    <v-app-bar flat color="background" height="64">
      <template #prepend>
        <v-app-bar-nav-icon @click="toggleSidebar" />
        <div class="brand">
          <v-avatar color="transparent" size="75">
            <img :src="toolboxLogo" />
          </v-avatar>
          <span class="brand-title">Mail Archive</span>
        </div>
      </template>

      <v-app-bar-title>
        <v-text-field
          v-model="mail.search"
          class="search"
          :placeholder="aiSearchActive ? 'Αναζήτηση με AI' : 'Αναζήτηση email'"
          :prepend-inner-icon="aiSearchActive ? 'i-mdi-face-agent' : 'i-mdi-magnify'"
          variant="outlined"
          density="comfortable"
          rounded="pill"
          flat
          hide-details
          clearable
          bg-color="surface"
          @click:clear="mail.clearSearch"
        >
          <template #append>
            <v-icon-btn
              :active="aiSearchActive"
              icon="i-mdi-comment-search-outline"
              active-icon="i-mdi-comment-search"
              tag="button"
              @click="toggleAISearchMode"
              title="Αναζήτηση με AI"
            />
          </template>
        </v-text-field>
      </v-app-bar-title>
    </v-app-bar>

    <v-navigation-drawer
      v-model="drawer"
      :rail="rail && !mobile"
      :rail-width="getSidebarRailWidth(SIDEBAR_DENSITY.COMPACT)"
      color="background"
      border="none"
      expand-on-hover
      @update:rail="(collapsed: boolean) => (isHovering = !collapsed)"
      open-delay="200"
      close-delay="200"
    >
      <AppSidebar :expanded="sidebarExpanded" :isMobile="mobile" />
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
}

.brand-title {
  font-size: 1.25rem;
  white-space: nowrap;
}

.search {
  max-width: 500px;
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

:deep(.v-navigation-drawer__content) {
  overflow: hidden;
}

@media (max-width: 600px) {
  .content-wrap {
    padding: 0;
  }
  .brand {
    min-width: 0;
  }
  .brand-title {
    display: none;
  }
}
</style>
