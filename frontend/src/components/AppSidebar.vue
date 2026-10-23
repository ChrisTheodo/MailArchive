<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppSidebarMenu from '@/components/AppSidebarMenu.vue'
import ShowAllListItem from './ShowAllListItem.vue'
import { useMailboxStore } from '@/stores/mailbox'
import type { MyMailboxResponse } from '@/types/me/my-mailbox-response.ts'
import ConfirmDialog from './ConfirmDialog.vue'

interface MailboxAction {
  id: string
  title: string
  icon: string
  action: (mailbox: MyMailboxResponse) => void | Promise<unknown>
}

defineProps({ expanded: Boolean, isMobile: Boolean })

// CONSTANTS
const mailboxStore = useMailboxStore()
const route = useRoute()
const router = useRouter()
const MAILBOX_SHOW_COUNT = 6
const mailboxActions: MailboxAction[] = [
  {
    id: 'delete_mailbox',
    title: 'Διαγραφή mailbox',
    icon: 'i-mdi-trash-can-outline',
    action: (mailbox) => {
      mailboxToDelete.value = mailbox
      isDeleteDialogOpen.value = true
    },
  },
]

// STATE
const showAllMailboxes = ref(false)
const hasRecents = ref(false)

const filteredMailboxes = computed(() => {
  if (showAllMailboxes.value) return mailboxStore.mailboxes
  return mailboxStore.mailboxes.slice(0, MAILBOX_SHOW_COUNT)
})
const hasHiddenMailboxes = computed(() => mailboxStore.mailboxes.length > MAILBOX_SHOW_COUNT)
const isDeleteDialogOpen = ref(false)
const mailboxToDelete = ref<MyMailboxResponse | null>(null)

// METHODS
function toggleShowAllMailboxes() {
  showAllMailboxes.value = !showAllMailboxes.value
}

function isMailboxImporting(mailbox: MyMailboxResponse) {
  return mailbox.runningImports > 0
}

function isMailboxActive(mailboxId: string) {
  return route.params.mailbox === mailboxId
}

async function confirmDeleteMailbox() {
  const mailbox = mailboxToDelete.value
  if (!mailbox) return

  const ok = await mailboxStore.deleteMailbox({ id: mailbox.mailboxId })
  if (!ok) return

  isDeleteDialogOpen.value = false
  if (isMailboxActive(mailbox.mailboxId)) router.push({ name: 'home' })
  mailboxToDelete.value = null
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
          :to="
            isMailboxImporting(mailbox)
              ? undefined
              : { name: 'mailbox', params: { mailbox: mailbox.mailboxId } }
          "
          :active="isMailboxActive(mailbox.mailboxId)"
          :title="mailbox.displayName"
          :disabled="isMailboxImporting(mailbox)"
          prepend-icon="i-mdi-inbox-outline"
          rounded="lg"
        >
          <v-overlay
            :model-value="isMailboxImporting(mailbox)"
            class="align-center justify-center"
            scrim="grey"
            scroll-strategy="none"
            :close-on-back="false"
            contained
            persistent
            no-click-animation
          >
            <v-progress-circular color="primary" size="20" width="2" indeterminate />
          </v-overlay>

          <template #append>
            <div class="count text-medium-emphasis d-flex align-center ga-1">
              <span v-if="mailbox.totalEmails">{{ mailbox.totalEmails }}</span>
              <v-menu v-if="!isMailboxImporting(mailbox)">
                <template #activator="{ props: menuProps, isActive: menuOpen }">
                  <v-icon
                    v-bind="menuProps"
                    class="justify-self-center menu-icon"
                    :color="menuOpen ? 'primary' : undefined"
                    icon="i-mdi-dots-vertical"
                    @click.stop.prevent
                  />
                </template>

                <v-list>
                  <v-list-item
                    v-for="item in mailboxActions"
                    :key="item.id"
                    :prepend-icon="item.icon"
                    :title="item.title"
                    @click="item.action(mailbox)"
                  />
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

      <v-divider v-if="hasRecents" />

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

    <ConfirmDialog
      v-model="isDeleteDialogOpen"
      title="Διαγραφή mailbox"
      confirm-text="Διαγραφή"
      confirm-color="error"
      icon="i-mdi-alert-outline"
      :loading="mailboxStore.isDeletingMailbox"
      @cancel="mailboxToDelete = null"
      @confirm="confirmDeleteMailbox"
    >
      Θέλετε σίγουρα να διαγράψετε το
      <strong>{{ mailboxToDelete?.displayName }}</strong
      >; Όλα τα emails και τα συνημμένα του θα διαγραφούν οριστικά.
    </ConfirmDialog>
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
