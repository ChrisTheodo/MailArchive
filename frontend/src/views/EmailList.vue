<script setup lang="ts">
import { ref, computed, watch } from 'vue'
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
const DEFAULT_PAGE_SIZE = 20
const PAGE_SIZE_OPTIONS = [
  { title: '5', value: 5 },
  { title: '10', value: 10 },
  { title: '20', value: 20 },
  { title: '50', value: 50 },
  { title: '100', value: 100 },
]

// STATE
const isSearch = computed(() => route.name === 'search')
const searchQuery = computed(() => String(route.query.q ?? '').trim())
const hasList = computed(() => isSearch.value || Boolean(props.mailbox))
const result = computed(() => (isSearch.value ? emailStore.emailsFromSearch : emailStore.emails))
const isLoading = computed(() =>
  isSearch.value ? emailStore.isSearching : emailStore.isLoadingEmails,
)
const loadError = computed(() => (isSearch.value ? emailStore.searchError : emailStore.emailsError))

const page = computed(() => Number(route.query.page) || 1)
const pageSize = computed(() => {
  const size = Number(route.query.pageSize)
  return PAGE_SIZE_OPTIONS.some((o) => o.value === size) ? size : DEFAULT_PAGE_SIZE
})
const items = computed(() => (hasList.value ? (result.value?.items ?? []) : []))
const totalCount = computed(() => (hasList.value ? (result.value?.totalCount ?? 0) : 0))
const effectivePageSize = computed(() => result.value?.pageSize || pageSize.value)
const totalPages = computed(() =>
  Math.max(1, Math.ceil(totalCount.value / effectivePageSize.value)),
)

const heading = computed(() => {
  if (isSearch.value) return searchQuery.value ? `Αναζήτηση: "${searchQuery.value}"` : 'Αναζήτηση'
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
  const size = effectivePageSize.value || pageSize.value
  const from = (page.value - 1) * size + 1
  const to = Math.min(page.value * size, totalCount.value)
  return `${from}–${to} of ${totalCount.value}`
})

// METHODS
function load() {
  const query = { page: page.value, pageSize: pageSize.value }

  if (isSearch.value) {
    if (searchQuery.value) emailStore.searchEmails(searchQuery.value, query)
    else emailStore.clearSearch()
    return
  }

  if (props.mailbox) emailStore.fetchEmails(props.mailbox, query)
}

function changePageSize(size: number) {
  const value = size === DEFAULT_PAGE_SIZE ? undefined : String(size)
  router.push({ query: { ...route.query, page: undefined, pageSize: value } })
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
watch([() => props.mailbox, page, isSearch, searchQuery, pageSize], load, { immediate: true })

if (!mailboxStore.mailboxes.length) mailboxStore.fetchMailboxes()
</script>

<template>
  <div class="toolbar">
    <span class="heading">{{ heading }}</span>
    <v-spacer />
    <template v-if="hasList">
      <span class="text-medium-emphasis small">{{ rangeText }}</span>
      <template v-if="totalCount">
        <v-pagination
          :model-value="page"
          :length="totalPages"
          :total-visible="5"
          :disabled="isLoading"
          density="comfortable"
          size="small"
          @update:model-value="goToPage"
          class="mx-2"
        >
          <template #next="{ onClick, disabled }">
            <v-icon icon="i-mdi-chevron-right" size="small" :disabled="disabled" @click="onClick" />
            <v-tooltip activator="parent" location="top center" text="Επόμενη σελίδα" />
          </template>
          <template #prev="{ onClick, disabled }">
            <v-icon icon="i-mdi-chevron-left" size="small" :disabled="disabled" @click="onClick" />
            <v-tooltip activator="parent" location="top center" text="Προηγούμενη σελίδα" />
          </template>
        </v-pagination>
        <v-tooltip text="Ανά σελίδα" location="top center">
          <template #activator="{ props: tooltipProps }">
            <v-select
              v-bind="tooltipProps"
              :model-value="pageSize"
              :items="PAGE_SIZE_OPTIONS"
              :disabled="isLoading"
              label="Ανά σελίδα"
              density="compact"
              variant="outlined"
              hide-details
              class="flex-0-0"
              @update:model-value="changePageSize"
            />
          </template>
        </v-tooltip>
      </template>
      <v-btn
        icon="i-mdi-refresh"
        variant="text"
        size="small"
        title="Refresh"
        :disabled="isLoading"
        @click="load"
      />
    </template>
  </div>
  <v-divider />

  <v-data-iterator
    :items="items"
    :items-per-page="-1"
    :loading="isLoading"
    item-value="id"
    class="scroll scrollbar"
  >
    <template #header> </template>

    <template #default="{ items: rows }">
      <v-list class="py-0" lines="one">
        <template v-for="{ raw: item } in rows" :key="item.id">
          <v-list-item class="mail-row" @click="open(item)">
            <div class="row-body">
              <span class="sender">{{ senderOf(item) }}</span>
              <v-chip v-if="isSearch" size="x-small" label class="flex-0-0">
                {{ item.mailboxDisplayName }}
              </v-chip>
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
        <template v-if="!hasList">
          <v-icon icon="i-mdi-inbox-outline" size="48" class="mb-2" />
          <div>Δεν υπάρχουν ακόμα mailboxes</div>
        </template>

        <template v-else-if="isSearch && !searchQuery">
          <v-icon icon="i-mdi-magnify" size="48" class="mb-2" />
          <div>Γράψτε κάτι στην αναζήτηση</div>
        </template>

        <template v-else-if="loadError">
          <v-icon icon="i-mdi-alert-circle-outline" size="48" class="mb-2" />
          <div>Τα email δεν μπορούν να φορτωθούν</div>
          <v-btn variant="text" class="mt-2" @click="load">Προσπαθήστε ξανά</v-btn>
        </template>

        <template v-else>
          <v-icon icon="i-mdi-email-open-outline" size="48" class="mb-2" />
          <div>
            {{
              isSearch
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

.page-size {
  flex: 0 0 auto;
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
