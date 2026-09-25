<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { FOLDERS, useMailStore } from '@/stores/mail'
import { formatShortDate } from '@/utils/date'

const props = defineProps({
  folder: { type: String, default: null },
  label: { type: String, default: null },
})

const mail = useMailStore()
const router = useRouter()

const items = computed(() =>
  props.label ? mail.emailsWithLabel(props.label) : mail.emailsInFolder(props.folder),
)

const heading = computed(() => {
  if (props.label) return mail.getLabel(props.label)?.title ?? props.label
  return FOLDERS.find(f => f.key === props.folder)?.title ?? props.folder
})


function open (item) {
  router.push(props.label
    ? { name: 'label-mail', params: { label: props.label, id: item.id } }
    : { name: 'folder-mail', params: { folder: props.folder, id: item.id } })
}

const snippet = text => text.replace(/\s+/g, ' ').slice(0, 120)
</script>

<template>
  <div class="toolbar">
    <span class="heading">{{ heading }}</span>
    <v-spacer />
    <span class="text-medium-emphasis small">{{ items.length }} conversations</span>
    <v-btn icon="mdi-refresh" variant="text" size="small" class="ml-2" />
  </div>
  <v-divider />

  <div class="scroll">
    <v-list v-if="items.length" class="py-0" lines="one">
      <template v-for="item in items" :key="item.id">
        <v-list-item
          :class="['mail-row', { unread: !item.read }]"
          @click="open(item)"
        >
          <template #prepend>
            <v-btn
              :icon="item.starred ? 'mdi-star' : 'mdi-star-outline'"
              :color="item.starred ? 'amber' : undefined"
              variant="text"
              size="small"
              @click.stop="mail.toggleStar(item.id)"
            />
          </template>

          <div class="row-body">
            <span class="sender">{{ item.folder === 'sent' ? `To: ${item.to}` : item.from.name }}</span>
            <span class="preview">
              <v-chip
                v-for="key in item.labels"
                :key="key"
                :color="mail.getLabel(key)?.color"
                size="x-small"
                label
                class="mr-1"
              >
                {{ mail.getLabel(key)?.title }}
              </v-chip>
              <span class="subject">{{ item.subject }}</span>
              <span class="text-medium-emphasis"> — {{ snippet(item.body) }}</span>
            </span>
          </div>

          <template #append>
            <span class="date">{{ formatShortDate(item.date) }}</span>
          </template>
        </v-list-item>
        <v-divider />
      </template>
    </v-list>

    <div v-else class="empty text-medium-emphasis">
      <v-icon icon="mdi-email-open-outline" size="48" class="mb-2" />
      <div>{{ mail.search ? 'No emails match your search.' : 'Nothing here.' }}</div>
    </div>
  </div>
</template>

<style scoped>

.toolbar {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  min-height: 52px;
}

.heading { font-size: 1.1rem; font-weight: 500; }

.small { font-size: 0.8rem; }

.scroll { flex: 1; overflow-y: auto; }

.mail-row { cursor: pointer; }
.mail-row:not(.unread) { background: rgba(var(--v-theme-background), 0.6); }

.mail-row.unread .sender,
.mail-row.unread .subject,
.mail-row.unread .date { font-weight: 700; }

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

.date { font-size: 0.8rem; white-space: nowrap; }

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 64px 16px;
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
  
  .preview { max-width: 100%; }
}
</style>
