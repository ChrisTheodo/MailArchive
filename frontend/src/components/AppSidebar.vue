<script setup>
import { useRoute } from 'vue-router'
import { FOLDERS, useMailStore } from '@/stores/mail'

defineProps({ rail: Boolean })

const mail = useMailStore()
const route = useRoute()
// Keep the folder/label highlighted while an email inside it is open
const isFolderActive = key => route.params.folder === key
const isLabelActive = key => route.params.label === key
</script>

<template>
  <div class="pa-2">
    <v-btn
      v-if="!rail"
      prepend-icon="mdi-pencil-outline"
      color="primary"
      variant="tonal"
      size="large"
      rounded="lg"
      class="mb-3 ml-1"
    >
      Compose
    </v-btn>
    <v-btn v-else icon="mdi-pencil-outline" color="primary" variant="tonal" class="mb-3" />

    <!-- Folders -->
    <v-list nav density="compact" color="primary">
      <v-list-item
        v-for="f in FOLDERS"
        :key="f.key"
        :to="{ name: 'folder', params: { folder: f.key } }"
        :active="isFolderActive(f.key)"
        :prepend-icon="f.icon"
        :title="f.title"
        rounded="pill"
      >
        <template v-if="!rail && mail.unreadCount(f.key)" #append>
          <span class="count">{{ mail.unreadCount(f.key) }}</span>
        </template>
      </v-list-item>
    </v-list>

    <v-divider class="my-2" />

    <!-- Labels -->
    <v-list nav density="compact" color="primary">
      <v-list-subheader v-if="!rail">
        Labels
      </v-list-subheader>
      <v-list-item
        v-for="l in mail.labels"
        :key="l.key"
        :to="{ name: 'label', params: { label: l.key } }"
        :active="isLabelActive(l.key)"
        :title="l.title"
        rounded="pill"
      >
        <template #prepend>
          <v-icon icon="mdi-label" :color="l.color" />
        </template>
      </v-list-item>
    </v-list>
  </div>
</template>

<style scoped>
.count {
  font-size: 0.75rem;
  font-weight: 700;
}
</style>
