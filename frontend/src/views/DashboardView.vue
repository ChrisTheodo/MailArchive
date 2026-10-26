<script setup lang="ts">
import { ref } from 'vue'
import DashboardImports from '../components/DashboardImports.vue'

// STATE
const openPanel = ref<string[]>([])

// METHODS
function isPanelOpen(panelValue: string) {
  return openPanel.value.includes(panelValue)
}
</script>

<template>
  <div class="toolbar">
    <span class="heading">Dashboard</span>
    <v-spacer />
  </div>
  <v-divider />

  <div class="ma-2 flex-1 overflow scrollbar">
    <v-expansion-panels v-model="openPanel" variant="accordion" static multiple>
      <v-expansion-panel value="imports" data-testid="imports-panel">
        <DashboardImports :is-opened="isPanelOpen('imports')" />
      </v-expansion-panel>
    </v-expansion-panels>
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  min-height: 52px;
}

.heading {
  font-size: 1.1rem;
  font-weight: 500;
}
</style>
