<script setup lang="ts">
import { ref, computed, onBeforeUnmount, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useEmailStore } from '@/stores/email'
import { useMailboxStore } from '@/stores/mailbox'
import { formatShortDate } from '@/utils/date'
import type { EmailListResponse } from '@/types/emails/email-list-response'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const props = defineProps<{ mailbox?: string }>()

// CONSTANTS
const emailStore = useEmailStore()
const mailboxStore = useMailboxStore()
const route = useRoute()
const router = useRouter()
const PAGE_SIZE = 20
const SEARCH_DEBOUNCE_MS = 300

// STATE
let searchTimer: ReturnType<typeof setTimeout> | undefined

const page = computed(() => Number(route.query.page) || 1)
const items = computed(() => (props.mailbox ? (emailStore.emails?.items ?? []) : []))
const totalCount = computed(() => emailStore.emails?.totalCount ?? 0)
const totalPages = computed(() => Math.max(1, emailStore.emails?.totalPages ?? 1))

const heading = computed(() => {
  if (!props.mailbox) return 'Mailboxes'
  return (
    mailboxStore.mailboxes.find((m) => m.mailboxId === props.mailbox)?.displayName ??
    items.value[0]?.mailboxDisplayName ??
    ''
  )
})

const isDeleteEmailDialogOpen = ref(false)
const emailToDelete = ref<EmailListResponse | null>(null)

const rangeText = computed(() => {
  if (!totalCount.value) return ''
  const from = (page.value - 1) * PAGE_SIZE + 1
  const to = Math.min(page.value * PAGE_SIZE, totalCount.value)
  return `${from}–${to} of ${totalCount.value}`
})

// METHODS
function load() {
  if (!props.mailbox) return
  emailStore.fetchEmails(props.mailbox, {
    page: page.value,
    pageSize: PAGE_SIZE,
    search: emailStore.search || undefined,
  })
}

function goToPage(p: number) {
  router.push({ query: { ...route.query, page: p === 1 ? undefined : String(p) } })
}

function open(item: EmailListResponse) {
  router.push({
    name: 'mail',
    params: { mailbox: props.mailbox ?? item.mailboxId, id: item.id },
    query: route.query, // so "back" returns to the same page
  })
}

function senderOf(item: EmailListResponse) {
  return item.senderName || item.senderEmail
}

function dateOf(item: EmailListResponse) {
  const date = item.receivedAt ?? item.sentAt
  return date ? formatShortDate(date) : ''
}

function askDeleteEmail(item: EmailListResponse) {
  emailToDelete.value = item
  isDeleteEmailDialogOpen.value = true
}

async function confirmDeleteEmail() {
  const email = emailToDelete.value
  if (!email) return

  const ok = await emailStore.deleteEmail({ id: email.id })
  if (!ok) return

  isDeleteEmailDialogOpen.value = false
  emailToDelete.value = null

  if (route.params.id === email.id) {
    router.push({
      name: 'mailbox',
      params: { mailbox: props.mailbox ?? email.mailboxId },
      query: route.query,
    })
    return
  }

  if (!items.value.length && page.value > 1) {
    goToPage(page.value - 1)
  } else {
    load()
  }
}

// WATCHERS
watch([() => props.mailbox, page], load, { immediate: true })

// A new search starts at page 1, after the user stops typing
watch(
  () => emailStore.search,
  () => {
    clearTimeout(searchTimer)
    searchTimer = setTimeout(() => (page.value === 1 ? load() : goToPage(1)), SEARCH_DEBOUNCE_MS)
  },
)

onBeforeUnmount(() => clearTimeout(searchTimer))

// Mailbox names for the heading, e.g. when opening a link directly
if (!mailboxStore.mailboxes.length) mailboxStore.fetchMailboxes()
</script>

<template>
  <div class="toolbar">
    <span class="heading">{{ heading }}</span>
    <v-spacer />
    <template v-if="mailbox">
      <span class="text-medium-emphasis small">{{ rangeText }}</span>
      <v-btn
        icon="i-mdi-chevron-left"
        variant="text"
        size="small"
        title="Newer"
        :disabled="page <= 1 || emailStore.isLoadingEmails"
        @click="goToPage(page - 1)"
      />
      <v-btn
        icon="i-mdi-chevron-right"
        variant="text"
        size="small"
        title="Older"
        :disabled="page >= totalPages || emailStore.isLoadingEmails"
        @click="goToPage(page + 1)"
      />
      <v-btn
        icon="i-mdi-refresh"
        variant="text"
        size="small"
        title="Refresh"
        :disabled="emailStore.isLoadingEmails"
        @click="load"
      />
    </template>
  </div>
  <v-divider />

  <v-data-iterator
    :items="items"
    :items-per-page="-1"
    :loading="emailStore.isLoadingEmails"
    item-value="id"
    class="scroll scrollbar"
  >
    <template #default="{ items: rows }">
      <v-list class="py-0" lines="one">
        <template v-for="{ raw: item } in rows" :key="item.id">
          <v-list-item class="mail-row" @click="open(item)">
            <div class="row-body">
              <span class="sender">{{ senderOf(item) }}</span>
              <span class="preview">
                <span class="subject">{{ item.subject || '(χωρίς θέμα)' }}</span>
                <span v-if="item.searchSnippet" class="text-medium-emphasis">
                  — {{ item.searchSnippet }}
                </span>
              </span>
            </div>

            <template #append>
              <div class="justify-items-center">
                <v-icon
                  v-if="item.hasAttachments"
                  icon="i-mdi-paperclip"
                  size="small"
                  class="text-medium-emphasis mr-2"
                  :title="item.attachmentFileNames.join(', ')"
                />
                <span class="date">{{ dateOf(item) }}</span>
                <v-icon
                  class="ml-2 email-row-icon"
                  color="black"
                  icon="i-mdi-trash"
                  size="small"
                  title="Διαγραφή"
                  :disabled="emailStore.isDeletingEmail"
                  @click.stop.prevent="askDeleteEmail(item)"
                />
              </div>
            </template>
          </v-list-item>
          <v-divider />
        </template>
      </v-list>
    </template>

    <template #loader>
      <v-skeleton-loader type="list-item@10" />
    </template>

    <template #no-data>
      <div class="empty text-medium-emphasis">
        <template v-if="!mailbox">
          <v-icon icon="i-mdi-inbox-outline" size="48" class="mb-2" />
          <div>Δεν υπάρχουν ακόμα mailboxes</div>
        </template>

        <template v-else-if="emailStore.emailsError">
          <v-icon icon="i-mdi-alert-circle-outline" size="48" class="mb-2" />
          <div>Τα email δεν μπορούν να φορτωθούν</div>
          <v-btn variant="text" class="mt-2" @click="load">Προσπαθήστε ξανά</v-btn>
        </template>

        <template v-else>
          <v-icon icon="i-mdi-email-open-outline" size="48" class="mb-2" />
          <div>
            {{
              emailStore.search
                ? 'Δεν υπάρχει email που να ταιριάζει στην αναζήτηση σας'
                : 'Δεν υπάρχει κάτι εδώ'
            }}
          </div>
        </template>
      </div>
    </template>
  </v-data-iterator>

  <ConfirmDialog
    v-model="isDeleteEmailDialogOpen"
    title="Διαγραφή email"
    confirm-color="error"
    confirm-text="Διαγραφή"
    icon="i-mdi-alert-outline"
    :loading="emailStore.isDeletingEmail"
    @cancel="emailToDelete = null"
    @confirm="confirmDeleteEmail"
  >
    Είστε σίγουροι πως θέλετε να διαγράψετε το email με θέμα
    <strong>{{ emailToDelete?.subject || '(χωρίς θέμα)' }}</strong> από
    <strong>{{ emailToDelete?.senderName || emailToDelete?.senderEmail }}</strong
    >; Τα συνημμένα του θα διαγραφούν οριστικά.
  </ConfirmDialog>
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

.small {
  font-size: 0.8rem;
}

.scroll {
  flex: 1;
  overflow-y: auto;
}

.mail-row {
  cursor: pointer;
}

.row-body {
  display: flex;
  align-items: center;
  gap: 16px;
  min-width: 0;
}

.sender {
  flex: 0 0 180px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.preview {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.date {
  font-size: 0.8rem;
  white-space: nowrap;
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 16px;
}

.email-row-icon {
  opacity: 0;
  pointer-events: none;
  transition: color 0.2s;
}

.v-list-item:hover .email-row-icon,
.v-list-item:focus-within .email-row-icon {
  opacity: 1;
  pointer-events: auto;
}

.email-row-icon:hover {
  color: rgb(var(--v-theme-primary));
}

/* For touch devices since they have no hover */
@media (hover: none) {
  .email-row-icon {
    opacity: 1;
    pointer-events: auto;
  }
}

@media (max-width: 600px) {
  .row-body {
    flex-direction: column;
    align-items: flex-start;
    gap: 2px;
  }

  .sender {
    flex: none;
    max-width: 100%;
  }

  .preview {
    max-width: 100%;
  }
}
</style>
