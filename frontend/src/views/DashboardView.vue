<script setup lang="ts">
import { ref, computed, watch, onBeforeUnmount, onMounted } from 'vue'
import { useImportStore } from '@/stores/import'
import { ImportBatchStatus } from '@/types/imports/import-batch-status'
import { toDate, type DotNetDateTime } from '@/types/date-time'
import { formatLongDate } from '@/utils/date'
import type { MyImportResponse } from '@/types/me/my-import-response'
import { useMailboxStore } from '@/stores/mailbox'
import ImportCancelDialog from '@/components/ImportCancelDialog.vue'

interface StatusDisplay {
  label: string
  icon: string
  color: string
  spins: boolean
}

interface ImportAction {
  id: string
  title: string
  icon: string
  isAvailable: (item: MyImportResponse) => boolean
  action: (item: MyImportResponse) => void | Promise<unknown>
}

// CONSTANTS
const importStore = useImportStore()
const mailboxStore = useMailboxStore()
const REFRESH_INTERVAL_MS = 30_000
const IMPORTS_PER_PAGE = 10
// GET api/me/imports caps pageSize at 100, so search and filtering cover the latest 100 imports
const MAX_IMPORTS_PAGE_SIZE = 100
const STATUS_DISPLAY: Record<ImportBatchStatus, StatusDisplay> = {
  [ImportBatchStatus.Pending]: {
    label: 'Εκκρεμεί',
    icon: 'i-mdi-loading',
    color: 'primary',
    spins: true,
  },
  [ImportBatchStatus.Running]: {
    label: 'Σε εξέλιξη',
    icon: 'i-mdi-loading',
    color: 'primary',
    spins: true,
  },
  [ImportBatchStatus.Queued]: {
    label: 'Στην ουρά',
    icon: 'i-mdi-timer-sand',
    color: 'info',
    spins: false,
  },
  [ImportBatchStatus.Completed]: {
    label: 'Ολοκληρώθηκε',
    icon: 'i-mdi-check-circle',
    color: 'success',
    spins: false,
  },
  [ImportBatchStatus.CompletedWithErrors]: {
    label: 'Ολοκληρώθηκε με σφάλματα',
    icon: 'i-mdi-alert',
    color: 'warning',
    spins: false,
  },
  [ImportBatchStatus.Failed]: {
    label: 'Απέτυχε',
    icon: 'i-mdi-close-circle',
    color: 'error',
    spins: false,
  },
  [ImportBatchStatus.Cancelled]: {
    label: 'Ακυρώθηκε',
    icon: 'i-mdi-cancel',
    color: 'grey',
    spins: false,
  },
}
const UNKNOWN_STATUS_DISPLAY: StatusDisplay = {
  label: 'Άγνωστη κατάσταση',
  icon: 'i-mdi-help-circle-outline',
  color: 'grey',
  spins: false,
}
const STATUS_OPTIONS = Object.values(ImportBatchStatus).map((status) => ({
  title: STATUS_DISPLAY[status].label,
  value: status,
}))
const relativeTimeFormat = new Intl.RelativeTimeFormat('el', { numeric: 'auto' })
const RELATIVE_TIME_UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 31_536_000],
  ['month', 2_592_000],
  ['week', 604_800],
  ['day', 86_400],
  ['hour', 3_600],
  ['minute', 60],
]
const CANCELLABLE_STATUSES: string[] = [ImportBatchStatus.Pending, ImportBatchStatus.Running]

const importActions: ImportAction[] = [
  {
    id: 'cancel_import',
    title: 'Ακύρωση εισαγωγής αρχείου',
    icon: 'i-mdi-upload-off',
    isAvailable: (item) => CANCELLABLE_STATUSES.includes(item.status),
    action: (item) => askCancelImport(item),
  },
]

// STATE
const search = ref<string | null>('')
const selectedStatuses = ref<ImportBatchStatus[]>([])
const isSearchingOrFiltering = computed(
  () => Boolean(search.value) || selectedStatuses.value.length > 0,
)
const page = ref(1)
let refreshTimer: ReturnType<typeof setInterval> | undefined
const openPanel = ref<string | null>(null)
const isImportCancelDialogOpen = ref(false)
const importToCancel = ref<MyImportResponse | null>(null)

// MyImportResponse.status is typed as string, but the backend always sends an ImportBatchStatus
const filteredImports = computed(() =>
  selectedStatuses.value.length
    ? importStore.imports.filter((i) => selectedStatuses.value.some((s) => s === i.status))
    : importStore.imports,
)

// METHODS
function actionsFor(item: MyImportResponse) {
  return importActions.filter((a) => a.isAvailable(item))
}

function isImportBatchStatus(value: string): value is ImportBatchStatus {
  return (Object.values(ImportBatchStatus) as string[]).includes(value)
}

function statusOf(item: MyImportResponse) {
  return isImportBatchStatus(item.status) ? STATUS_DISPLAY[item.status] : UNKNOWN_STATUS_DISPLAY
}

function titleOf(item: MyImportResponse) {
  return item.mailboxDisplayName
    ? `${item.pstFilename} (${item.mailboxDisplayName})`
    : item.pstFilename
}

function formatRelativeDate(value: DotNetDateTime) {
  const diffSeconds = Math.round((toDate(value).getTime() - Date.now()) / 1000)
  for (const [unit, unitSeconds] of RELATIVE_TIME_UNITS) {
    if (Math.abs(diffSeconds) >= unitSeconds) {
      return relativeTimeFormat.format(Math.round(diffSeconds / unitSeconds), unit)
    }
  }
  return relativeTimeFormat.format(0, 'second')
}

function startedAtOf(item: MyImportResponse) {
  return `Ξεκίνησε ${formatRelativeDate(item.startedAt)} · ${formatLongDate(item.startedAt)}`
}

function isBeingCancelled(item: MyImportResponse) {
  return importStore.isCancelling && importToCancel.value?.id === item.id
}

function runningImportIds() {
  return importStore.imports
    .filter((i) => i.status === ImportBatchStatus.Running)
    .map((i) => i.id)
    .sort()
    .join(',')
}

function clearSearchAndFilters() {
  search.value = null
  selectedStatuses.value = []
}

async function loadImports() {
  if (importStore.isLoadingImports) return
  const runningBefore = runningImportIds()
  const ok = await importStore.fetchImports({ pageSize: MAX_IMPORTS_PAGE_SIZE })

  if (ok && runningImportIds() !== runningBefore) mailboxStore.fetchMailboxes()
}

// The timer skips while searching or filtering so the list doesn't shift under the user
function refreshImports() {
  if (!isSearchingOrFiltering.value) loadImports()
}

function askCancelImport(item: MyImportResponse) {
  importToCancel.value = item
  isImportCancelDialogOpen.value = true
}

function onImportCancelled() {
  importToCancel.value = null
  // The store already updated the row's status, so loadImports can't detect the change itself
  mailboxStore.fetchMailboxes()
}

// WATCHERS
watch([search, selectedStatuses], () => {
  page.value = 1
})

watch(openPanel, (panel) => {
  if (panel !== 'imports') clearSearchAndFilters()
})

onMounted(() => {
  loadImports()
  refreshTimer = setInterval(refreshImports, REFRESH_INTERVAL_MS)
})

onBeforeUnmount(() => {
  clearInterval(refreshTimer)
})
</script>

<template>
  <div class="toolbar">
    <span class="heading">Dashboard</span>
    <v-spacer />
  </div>
  <v-divider />

  <div class="ma-2 flex-1 overflow scrollbar">
    <v-expansion-panels v-model="openPanel">
      <v-expansion-panel value="imports">
        <v-expansion-panel-title>
          <v-icon icon="i-mdi-database-import-outline" class="mr-3" />
          <span class="text-subtitle-1 font-weight-medium">Εισαγωγές</span>
          <v-chip size="small" label class="ml-2">{{ importStore.imports.length }}</v-chip>
        </v-expansion-panel-title>

        <v-expansion-panel-text>
          <v-row density="compact" class="mb-2">
            <v-col cols="12" sm="6">
              <v-text-field
                v-model="search"
                label="Αναζήτηση αρχείου ή mailbox"
                prepend-inner-icon="i-mdi-magnify"
                density="compact"
                variant="outlined"
                hide-details
                clearable
              />
            </v-col>
            <v-col cols="12" sm>
              <v-select
                v-model="selectedStatuses"
                :items="STATUS_OPTIONS"
                label="Κατάσταση"
                density="compact"
                variant="outlined"
                multiple
                chips
                closable-chips
                clearable
                hide-details
              />
            </v-col>
            <v-col cols="auto" class="d-flex align-center">
              <v-btn
                icon="i-mdi-refresh"
                variant="text"
                size="small"
                title="Ανανέωση"
                :loading="importStore.isLoadingImports"
                @click="loadImports"
              />
            </v-col>
          </v-row>

          <v-data-iterator
            v-model:page="page"
            :items="filteredImports"
            :items-per-page="IMPORTS_PER_PAGE"
            :search="search ?? ''"
            :filter-keys="['pstFilename', 'mailboxDisplayName']"
            :loading="importStore.isLoadingImports && !importStore.imports.length"
            item-value="id"
          >
            <template #default="{ items }">
              <v-list class="py-0" lines="two">
                <template v-for="({ raw: item }, index) in items" :key="item.id">
                  <v-divider v-if="index > 0" />
                  <v-list-item :title="titleOf(item)" :subtitle="startedAtOf(item)">
                    <v-overlay
                      :model-value="isBeingCancelled(item)"
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

                    <template #prepend>
                      <v-icon
                        :color="statusOf(item).color"
                        :icon="statusOf(item).icon"
                        :class="{ spin: statusOf(item).spins }"
                      />
                    </template>

                    <template #append>
                      <v-chip :color="statusOf(item).color" size="small" variant="tonal" label>
                        {{ statusOf(item).label }}
                      </v-chip>
                      <v-menu :disabled="!actionsFor(item).length">
                        <template #activator="{ props: menuProps, isActive: menuOpen }">
                          <v-tooltip
                            text="Δεν υπάρχουν διαθέσιμες ενέργειες"
                            location="top"
                            :disabled="actionsFor(item).length > 0"
                          >
                            <template #activator="{ props: tooltipProps }">
                              <!-- Span element here to activate hover for the tooltip -->
                              <span v-bind="tooltipProps" class="d-inline-flex ml-2">
                                <v-icon
                                  v-bind="menuProps"
                                  class="menu-icon"
                                  :color="menuOpen ? 'primary' : undefined"
                                  :disabled="!actionsFor(item).length"
                                  icon="i-mdi-dots-vertical"
                                  @click.stop.prevent
                                />
                              </span>
                            </template>
                          </v-tooltip>
                        </template>

                        <v-list>
                          <v-list-item
                            v-for="importAction in actionsFor(item)"
                            :key="importAction.id"
                            :prepend-icon="importAction.icon"
                            :title="importAction.title"
                            @click="importAction.action(item)"
                          />
                        </v-list>
                      </v-menu>
                    </template>
                  </v-list-item>
                </template>
              </v-list>
            </template>

            <template #footer="{ pageCount }">
              <v-pagination
                v-if="pageCount > 1"
                v-model="page"
                :length="pageCount"
                total-visible="5"
                density="comfortable"
                class="mt-2"
              />
            </template>

            <template #loader>
              <v-skeleton-loader type="list-item-avatar-two-line@3" />
            </template>

            <template #no-data>
              <div class="d-flex flex-column align-center pa-6 text-medium-emphasis">
                <template v-if="importStore.importsError">
                  <v-icon icon="i-mdi-alert-circle-outline" size="40" class="mb-2" />
                  <div>Οι εισαγωγές δεν μπορούν να φορτωθούν</div>
                  <v-btn variant="text" class="mt-2" @click="loadImports">Προσπαθήστε ξανά</v-btn>
                </template>
                <template v-else-if="importStore.imports.length">
                  <v-icon icon="i-mdi-filter-off-outline" size="40" class="mb-2" />
                  <div>Καμία εισαγωγή δεν ταιριάζει στα φίλτρα</div>
                </template>
                <template v-else>
                  <v-icon icon="i-mdi-tray-arrow-up" size="40" class="mb-2" />
                  <div>Δεν υπάρχουν ακόμα εισαγωγές</div>
                </template>
              </div>
            </template>
          </v-data-iterator>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </div>

  <ImportCancelDialog
    v-if="importToCancel"
    v-model="isImportCancelDialogOpen"
    :import-id="importToCancel.id"
    :import-name="titleOf(importToCancel)"
    :status-label="statusOf(importToCancel).label"
    @cancelled="onImportCancelled"
  />
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

.spin {
  animation: spin 1s linear infinite;
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

.menu-icon:not(.v-icon--disabled):hover {
  color: rgb(var(--v-theme-primary));
}

.v-list-item:hover .menu-icon.v-icon--disabled,
.v-list-item:focus-within .menu-icon.v-icon--disabled {
  opacity: 0.4;
  color: rgb(var(--v-theme-on-surface));
  pointer-events: none;
}

/* For touch devices since they have no hover */
@media (hover: none) {
  .menu-icon {
    opacity: 1;
    pointer-events: auto;
  }

  .menu-icon.v-icon--disabled {
    opacity: 0.4;
    pointer-events: none;
  }
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .spin {
    animation: none;
  }
}
</style>
