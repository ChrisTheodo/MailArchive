<script setup lang="ts">
import { computed, watch, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useMailStore } from '@/stores/mail'
import { formatLongDate } from '@/utils/date'

const props = defineProps({
  id: { type: String, required: true },
  folder: { type: String, default: null },
  label: { type: String, default: null },
})

const mail = useMailStore()
const router = useRouter()

const item = computed(() => mail.getById(props.id))

watch(() => props.id, id => mail.markRead(id), { immediate: true })

const backTo = computed(() => props.label
  ? { name: 'label', params: { label: props.label } }
  : { name: 'folder', params: { folder: props.folder } })

const goBack = () => router.push(backTo.value)

function markUnread () {
  mail.markRead(props.id, false)
  goBack()
}

function remove () {
  mail.deleteMail(props.id)
  goBack()
}

function toggleSpam () {
  mail.moveTo(props.id, item.value.folder === 'spam' ? 'inbox' : 'spam')
  goBack()
}
</script>

<template>
  <div class="toolbar">
    <v-btn icon="i-mdi-arrow-left" variant="text" size="small" title="Back" @click="goBack" />
    <template v-if="item">
      <v-btn
        :icon="item.folder === 'spam' ? 'mdi-email-arrow-left-outline' : 'mdi-alert-octagon-outline'"
        :title="item.folder === 'spam' ? 'Not spam' : 'Report spam'"
        variant="text"
        size="small"
        @click="toggleSpam"
      />
      <v-btn icon="i-mdi-delete-outline" title="Delete" variant="text" size="small" @click="remove" />
      <v-btn icon="i-mdi-email-mark-as-unread" title="Mark as unread" variant="text" size="small" @click="markUnread" />
    </template>
  </div>
  <v-divider />

  <div class="scroll">
    <div v-if="item" class="mail">
      <div class="subject-row">
        <h1 class="subject">{{ item.subject }}</h1>
        <v-chip
          v-for="key in item.labels"
          :key="key"
          :color="mail.getLabel(key)?.color"
          size="small"
          label
        >
          {{ mail.getLabel(key)?.title }}
        </v-chip>
      </div>

      <div class="sender-row">
        <v-avatar color="primary" size="40">
          {{ item.from.name.charAt(0) }}
        </v-avatar>
        <div class="sender-info">
          <div>
            <strong>{{ item.from.name }}</strong>
            <span class="text-medium-emphasis small"> &lt;{{ item.from.email }}&gt;</span>
          </div>
          <div class="text-medium-emphasis small">to {{ item.to }}</div>
        </div>
        <v-spacer />
        <span class="text-medium-emphasis small">{{ formatLongDate(item.date) }}</span>
        <v-btn
          :icon="item.starred ? 'mdi-star' : 'mdi-star-outline'"
          :color="item.starred ? 'amber' : undefined"
          variant="text"
          size="small"
          @click="mail.toggleStar(item.id)"
        />
      </div>

      <div class="body">{{ item.body }}</div>

      <div class="d-flex ga-2 mt-8">
        <v-btn prepend-icon="mdi-reply" variant="outlined" rounded="pill">Reply</v-btn>
        <v-btn prepend-icon="mdi-share" variant="outlined" rounded="pill">Forward</v-btn>
      </div>
    </div>

    <div v-else class="pa-8 text-medium-emphasis">
      This email doesn't exist or was deleted.
    </div>
  </div>
</template>

<style scoped>

.toolbar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 12px;
  min-height: 52px;
}

.scroll { flex: 1; overflow-y: auto; }

.mail { padding: 16px 24px 32px 72px; }

.subject-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 20px;
}

.subject { font-size: 1.4rem; font-weight: 400; margin: 0; }

.sender-row {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-left: -52px;
}

.sender-info { min-width: 0; }

.small { font-size: 0.8rem; }

.body {
  white-space: pre-line;
  margin-top: 24px;
  line-height: 1.6;
}

@media (max-width: 600px) {
  .mail { padding: 16px; }
  .sender-row { margin-left: 0; flex-wrap: wrap; }
}
</style>
