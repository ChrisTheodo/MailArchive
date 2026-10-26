<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { useEmailStore } from '@/stores/email'
import { useMailboxStore } from '@/stores/mailbox'
import { useRouter, useRoute } from 'vue-router'
import { type SubmitEventPromise, useDate } from 'vuetify'
import type { VTextField } from 'vuetify/components'
import {
  EMPTY_SEARCH_FILTERS,
  countActiveFilters,
  filtersFromQuery,
  filtersToQuery,
  type EmailSearchFilters,
} from '@/utils/email-search-filters'

// CONSTANTS
const emailStore = useEmailStore()
const mailboxStore = useMailboxStore()
const route = useRoute()
const router = useRouter()
const SEARCH_DEBOUNCE_MS = 400
const ATTACHMENT_OPTIONS = [
  { title: 'Όλα', value: 'any' },
  { title: 'Με συνημμένα', value: 'true' },
  { title: 'Χωρίς συνημμένα', value: 'false' },
]
const dateAdapter = useDate()

// STATE
const aiSearchActive = ref(false)
let searchTimer: ReturnType<typeof setTimeout> | undefined
let pageBeforeSearch: string | null = null
const searchField = ref<VTextField>()
const isFilterMenuOpen = ref(false)
const appliedFilters = computed(() => filtersFromQuery(route.query))
const activeFilterCount = computed(() => countActiveFilters(appliedFilters.value))
const draftFilters = ref<EmailSearchFilters>({ ...EMPTY_SEARCH_FILTERS })
const fromDateInput = computed({
  get() {
    const value = draftFilters.value.fromDate
    return value ? dateAdapter.parseISO(value) : null
  },
  set(value) {
    draftFilters.value.fromDate = value ? dateAdapter.toISO(value).slice(0, 10) : null
  },
})

const toDateInput = computed({
  get() {
    const value = draftFilters.value.toDate
    return value ? dateAdapter.parseISO(value) : null
  },
  set(value) {
    draftFilters.value.toDate = value ? dateAdapter.toISO(value).slice(0, 10) : null
  },
})

const mailboxOptions = computed(() =>
  mailboxStore.mailboxes.map((m) => ({ title: m.displayName, value: m.mailboxId })),
)

const attachmentsFilter = computed({
  get: () => {
    const value = draftFilters.value.hasAttachments
    return value === null ? 'any' : String(value)
  },
  set: (value: string) => {
    draftFilters.value.hasAttachments = value === 'any' ? null : value === 'true'
  },
})

const toDateRules = [
  (value: Date | null) =>
    !value ||
    !draftFilters.value.fromDate ||
    dateAdapter.toISO(value).slice(0, 10) >= draftFilters.value.fromDate ||
    'Η ημερομηνία λήξης δεν μπορεί να είναι πριν την έναρξη',
]

// METHODS
function toggleAISearchMode() {
  aiSearchActive.value = !aiSearchActive.value
}

function goToSearch(q: string, filters: EmailSearchFilters) {
  const onSearchPage = route.name === 'search'

  if (!q && !countActiveFilters(filters)) {
    if (onSearchPage) router.push(pageBeforeSearch ?? { name: 'home' })
    return
  }

  const location = { name: 'search', query: { q: q || undefined, ...filtersToQuery(filters) } }
  if (onSearchPage) {
    router.replace(location)
    return
  }

  if (route.query.q === undefined && !activeFilterCount.value) pageBeforeSearch = route.fullPath
  router.push(location)
}

function submitSearch() {
  clearTimeout(searchTimer)
  const q = emailStore.search.trim()
  const onSearchPage = route.name === 'search'

  if (!q && !onSearchPage) return
  if (onSearchPage && (route.query.q ?? '') === q) return
  goToSearch(q, appliedFilters.value)
}

function onSearchInput(value: string | null) {
  emailStore.search = value ?? ''
  clearTimeout(searchTimer)
  searchTimer = setTimeout(submitSearch, SEARCH_DEBOUNCE_MS)
}

function onSearchClear() {
  emailStore.search = ''
  submitSearch()
}

async function applyFilters(event: SubmitEventPromise) {
  const { valid } = await event
  if (!valid) return

  clearTimeout(searchTimer)
  isFilterMenuOpen.value = false
  goToSearch(emailStore.search.trim(), draftFilters.value)
}

function resetDraftFilters() {
  draftFilters.value = { ...EMPTY_SEARCH_FILTERS }
}

// WATCHERS
watch(
  () => route.query.q,
  (q) => {
    if (typeof q === 'string') {
      if (q !== emailStore.search.trim()) emailStore.search = q
    } else if (route.name !== 'search') emailStore.search = ''
  },
  { immediate: true },
)

watch(isFilterMenuOpen, (isOpen) => {
  if (isOpen) draftFilters.value = { ...appliedFilters.value }
})

onBeforeUnmount(() => clearTimeout(searchTimer))
</script>

<template>
  <v-text-field
    ref="searchField"
    class="search"
    :model-value="emailStore.search"
    :placeholder="aiSearchActive ? 'Αναζήτηση με AI' : 'Αναζήτηση email'"
    :prepend-inner-icon="aiSearchActive ? 'i-mdi-face-agent' : 'i-mdi-magnify'"
    variant="outlined"
    density="comfortable"
    rounded="pill"
    flat
    hide-details
    clearable
    bg-color="surface"
    @update:model-value="onSearchInput"
    @keydown.enter="submitSearch"
    @click:clear="onSearchClear"
  >
    <template #append-inner>
      <v-menu
        v-model="isFilterMenuOpen"
        :target="searchField"
        :close-on-content-click="false"
        location="bottom"
        offset="4"
      >
        <template #activator="{ props }">
          <v-icon-btn
            v-bind="props"
            class="filter-icon"
            :icon="activeFilterCount ? 'i-mdi-filter' : 'i-mdi-filter-outline'"
            title="Φίλτρα"
          />
        </template>

        <v-card rounded="xl" class="pa-4">
          <v-form @submit.prevent="applyFilters">
            <v-row density="compact">
              <v-col cols="12" sm="6">
                <v-text-field
                  v-model="draftFilters.sender"
                  label="Από"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field
                  v-model="draftFilters.recipient"
                  label="Προς"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-model="draftFilters.subject"
                  label="Θέμα"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-date-input
                  v-model="fromDateInput"
                  label="Από ημερομηνία"
                  density="compact"
                  input-format="dd/MM/yyyy"
                  variant="outlined"
                  hide-details
                  clearable
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-date-input
                  v-model="toDateInput"
                  label="Έως ημερομηνία"
                  density="compact"
                  input-format="dd/MM/yyyy"
                  :min="fromDateInput ?? undefined"
                  :rules="toDateRules"
                  variant="outlined"
                  hide-details="auto"
                  clearable
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-select
                  v-model="draftFilters.mailboxId"
                  :items="mailboxOptions"
                  label="Mailbox"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-select
                  v-model="attachmentsFilter"
                  :items="ATTACHMENT_OPTIONS"
                  label="Συνημμένα"
                  variant="outlined"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  v-model="draftFilters.attachmentFileName"
                  label="Όνομα συνημμένου"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                />
              </v-col>
            </v-row>

            <div class="d-flex justify-end ga-2 mt-4">
              <v-btn variant="text" @click="resetDraftFilters">Καθαρισμός</v-btn>
              <v-btn type="submit" color="primary" variant="flat">Αναζήτηση</v-btn>
            </div>
          </v-form>
        </v-card>
      </v-menu>
    </template>

    <template #append>
      <v-icon-btn
        v-if="false"
        :active="aiSearchActive"
        icon="i-mdi-comment-search-outline"
        active-icon="i-mdi-comment-search"
        tag="button"
        @click="toggleAISearchMode"
        title="Αναζήτηση με AI"
      />
    </template>
  </v-text-field>
</template>

<style scoped>
.filter-icon {
  transition: color 0.2s;
}

.filter-icon:hover {
  color: rgb(var(--v-theme-primary));
}
</style>
