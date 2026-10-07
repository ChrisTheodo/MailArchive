<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { FOLDERS, useMailStore } from '@/stores/mail'
import AppSidebarMenu from '@/components/AppSidebarMenu.vue'
import ShowAllListItem from './ShowAllListItem.vue'

defineProps({ expanded: Boolean, isMobile: Boolean })

// CONSTANTS
const mail = useMailStore()
const route = useRoute()
const MAILBOX_SHOW_COUNT = 4
const LABELS_SHOW_COUNT = 3

// STATE
const mailboxes = ref([
  { id: 'mailbox-1', title: 'Mailbox 1' },
  { id: 'mailbox-2', title: 'Mailbox 2' },
  { id: 'mailbox-3', title: 'Mailbox 3' },
  { id: 'mailbox-4', title: 'Mailbox 4' },
  { id: 'mailbox-5', title: 'Mailbox 5' },
  { id: 'mailbox-6', title: 'Mailbox 6' },
  { id: 'mailbox-7', title: 'Mailbox 7' },
])
const filteredMailboxes = computed(() => {
  if (showAllMailboxes.value) return mailboxes.value
  return mailboxes.value.slice(0, MAILBOX_SHOW_COUNT)
})
const hasHiddenMailboxes = computed(() => mailboxes.value.length >= MAILBOX_SHOW_COUNT)
const showAllMailboxes = ref(false)

const filteredLabels = computed(() => {
  if (showAllLabels.value) return mail.labels
  return mail.labels.slice(0, LABELS_SHOW_COUNT)
})
const hasHiddenLabels = computed(() => mail.labels.length >= LABELS_SHOW_COUNT)
const showAllLabels = ref(false)

const hasRecents = ref(true)

// METHODS
function toggleShowAllMailboxes() {
  showAllMailboxes.value = !showAllMailboxes.value
}

function toggleShowAllLabels() {
  showAllLabels.value = !showAllLabels.value
}

function isFolderActive(mailboxId: string, folderKey: string) {
  return route.params.mailbox === mailboxId && route.params.folder === folderKey
}

function isLabelActive(key: string | string[] | undefined) {
  return route.params.label === key
}
</script>

<template>
  <div ref="root" class="sidebar pr-2 pl-2 pb-2">
    <div :class="['sections', 'scrollbar', { 'hide-scrollbar': !expanded && !isMobile }]">
      <!-- Mailboxes -->
      <v-list density="compact" nav :class="['section', { 'pt-0': expanded || isMobile }]">
        <v-list-subheader v-if="expanded || isMobile" sticky> Mailboxes </v-list-subheader>
        <v-list-group
          v-for="mailbox in filteredMailboxes"
          :key="mailbox.id"
          :value="mailbox.id"
          fluid
        >
          <template #activator="{ props, isOpen }">
            <v-list-item
              v-bind="props"
              :prepend-icon="isOpen ? 'i-mdi-chevron-down' : 'i-mdi-chevron-right'"
              :title="mailbox.title"
              append-icon="i-mdi-dots-horizontal"
              :ripple="false"
              variant="plain"
            >
              <template #append>
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
              </template>
            </v-list-item>
          </template>

          <v-list-item
            v-for="f in FOLDERS"
            :key="f.key"
            :to="{ name: 'folder', params: { mailbox: mailbox.id, folder: f.key } }"
            :active="isFolderActive(mailbox.id, f.key)"
            :prepend-icon="f.icon"
            rounded="pill"
          >
            <template #prepend="{ isActive }">
              <v-icon :icon="f.icon" :class="{ 'text-medium-emphasis': !isActive }" />
            </template>

            <template #append="{ isActive }">
              <div
                :class="[
                  'count',
                  { 'text-medium-emphasis': !isActive, 'd-flex ga-1': mail.unreadCount(f.key) },
                ]"
              >
                <span v-if="mail.unreadCount(f.key)">{{ mail.unreadCount(f.key) }}</span>
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
                        { title: 'Δημιουργία νέου φακέλου' },
                        { title: 'Διαγραφή φακέλου' },
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

            <template #default="{ isActive }">
              <v-list-item-title :class="{ 'text-medium-emphasis': !isActive }">{{
                f.title
              }}</v-list-item-title>
            </template>
          </v-list-item>
        </v-list-group>
        <ShowAllListItem
          :showCondition="expanded && hasHiddenMailboxes"
          :title-condition="showAllMailboxes"
          @click="toggleShowAllMailboxes"
        />
      </v-list>

      <v-divider />

      <!-- Labels -->
      <v-list
        nav
        density="compact"
        :class="['section', { 'pt-0': expanded || isMobile, 'hide-scrollbar': !expanded }]"
      >
        <v-list-subheader v-if="expanded || isMobile" sticky>
          <template #default>
            <div class="d-flex flex-row flex-fill align-center justify-space-between">
              <span>Ετικέτες</span>
              <v-icon-btn
                size="small"
                icon-size="small"
                icon="i-mdi-plus"
                base-variant="plain"
                color="background"
                title="Προσθήκη νέας ετικέτας"
              />
            </div>
          </template>
        </v-list-subheader>
        <v-icon-btn v-else icon="i-mdi-plus" color="background" title="Προσθήκη νέας ετικέτας" />
        <v-list-item
          v-for="l in filteredLabels"
          :key="l.key"
          :to="{ name: 'label', params: { label: l.key } }"
          :active="isLabelActive(l.key)"
          :title="l.title"
          rounded="pill"
        >
          <template #prepend>
            <v-icon icon="i-mdi-label" :color="l.color" />
          </template>

          <template #append>
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
                    { title: 'Επεξεργασία ετικέτας' },
                    { title: 'Διαγραφή ετικέτας' },
                  ]"
                  :key="index"
                  :value="index"
                >
                  <v-list-item-title>{{ item.title }}</v-list-item-title>
                </v-list-item>
              </v-list>
            </v-menu>
          </template>
        </v-list-item>

        <ShowAllListItem
          :showCondition="expanded && hasHiddenLabels"
          :title-condition="showAllLabels"
          @click="toggleShowAllLabels"
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
.v-list-item:focus-within .menu-icon,
.menu-icon--open {
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
