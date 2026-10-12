<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import AppSidebarMenu from '@/components/AppSidebarMenu.vue'
import ShowAllListItem from './ShowAllListItem.vue'
import { useMailboxStore } from '@/stores/mailbox'

defineProps({ expanded: Boolean, isMobile: Boolean })

// CONSTANTS
const mailboxStore = useMailboxStore()
const route = useRoute()
const MAILBOX_SHOW_COUNT = 4

// STATE
const showAllMailboxes = ref(false)
const hasRecents = ref(true)

const filteredMailboxes = computed(() => {
  if (showAllMailboxes.value) return mailboxStore.mailboxes
  return mailboxStore.mailboxes.slice(0, MAILBOX_SHOW_COUNT)
})
const hasHiddenMailboxes = computed(() => mailboxStore.mailboxes.length > MAILBOX_SHOW_COUNT)

// METHODS
function toggleShowAllMailboxes() {
  showAllMailboxes.value = !showAllMailboxes.value
}

function isMailboxActive(mailboxId: string) {
  return route.params.mailbox === mailboxId
}

if (!mailboxStore.mailboxes.length) mailboxStore.fetchMailboxes()
</script>

<template>
  <div ref="root" class="sidebar pr-2 pl-2 pb-2">
    <div :class="['sections', 'scrollbar', { 'hide-scrollbar': !expanded && !isMobile }]">
      <!-- Mailboxes -->
      <v-list density="compact" nav :class="['section', { 'pt-0': expanded || isMobile }]">
        <v-list-subheader v-if="expanded || isMobile" sticky> Mailboxes </v-list-subheader>

        <v-skeleton-loader
          v-if="mailboxStore.isLoadingMailboxes && !mailboxStore.mailboxes.length"
          type="list-item@3"
          color="background"
        />

        <v-list-item
          v-for="mailbox in filteredMailboxes"
          :key="mailbox.mailboxId"
          :to="{ name: 'mailbox', params: { mailbox: mailbox.mailboxId } }"
          :active="isMailboxActive(mailbox.mailboxId)"
          :title="mailbox.displayName"
          prepend-icon="i-mdi-inbox-outline"
          rounded="pill"
        >
          <template #append>
            <div class="count text-medium-emphasis d-flex align-center ga-1">
              <span v-if="mailbox.totalEmails">{{ mailbox.totalEmails }}</span>
              <v-menu>
                <template #activator="{ props: menuProps, isActive: menuOpen }">
                  <v-icon
                    v-bind="menuProps"
                    class="justify-self-center menu-icon"
                    :color="menuOpen ? 'primary' : undefined"
                    icon="i-mdi-dots-horizontal"
                    @click.stop.prevent
                  />
                </template>

                <v-list>
                  <v-list-item
                    v-for="(item, index) in [
                      { title: 'Δημιουργία νέου mailbox' },
                      { title: 'Διαγραφή mailbox' },
                    ]"
                    :key="index"
                    :value="index"
                  >
                    <v-list-item-title>{{ item.title }}</v-list-item-title>
                  </v-list-item>
                </v-list>
              </v-menu>
            </div>
          </template>
        </v-list-item>
        <ShowAllListItem
          :showCondition="expanded && hasHiddenMailboxes"
          :title-condition="showAllMailboxes"
          @click="toggleShowAllMailboxes"
        />
      </v-list>

      <v-divider />

      <!-- Recent -->
      <v-list
        v-if="hasRecents"
        nav
        density="compact"
        :class="['section', { 'pt-0': expanded || isMobile, 'hide-scrollbar': !expanded }]"
      >
        <v-list-subheader v-if="expanded || isMobile">Πρόσφατα</v-list-subheader>
        <v-list-item> </v-list-item>
      </v-list>
    </div>

    <AppSidebarMenu :expanded="expanded" />
  </div>
</template>

<style scoped>
.count {
  font-size: 0.75rem;
  font-weight: 700;
}

:deep(.v-list-subheader) {
  background-color: rgb(var(--v-theme-background));
}

:deep(.v-list-subheader__text) {
  display: flex;
  flex: 1;
}

.sidebar {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.sections {
  flex: 1 1 auto;
  min-height: 0;
  scrollbar-color: transparent transparent;
}

.sections:hover {
  scrollbar-color: rgba(var(--v-theme-on-surface), 0.3) transparent;
}

.section {
  flex: 0 0 auto; /* share the space equally */
  overflow: visible;
}

.menu-icon {
  opacity: 0;
  pointer-events: none;
  transition: color 0.2s;
}

.v-list-item:hover .menu-icon,
.v-list-item:focus-within .menu-icon {
  opacity: 1;
  pointer-events: auto;
}

.menu-icon:hover {
  color: rgb(var(--v-theme-primary));
}

/* For touch devices since they have no hover */
@media (hover: none) {
  .menu-icon {
    opacity: 1;
    pointer-events: auto;
  }
}
</style>
