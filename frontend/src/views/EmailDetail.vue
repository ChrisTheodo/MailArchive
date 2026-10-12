<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useEmailStore } from '@/stores/email'
import { useEmailSettingsStore } from '@/stores/email-settings'
import { useSnackbarMessagesStore } from '@/stores/snackbar-messages'
import { API_URLS } from '@/constants/api-urls'
import api from '@/composables/axios-api'
import { formatLongDate } from '@/utils/date'
import NotFound404 from '@/components/NotFound404.vue'
import type { EmailDetailsResponse } from '@/types/emails/email-details-response'
import type { EmailAttachmentResponse } from '@/types/emails/email-attachment-response'
import type { EmailRecipientResponse } from '@/types/emails/email-recipient-response'

const props = defineProps<{ mailbox: string; id: string }>()

// CONSTANTS
const emailStore = useEmailStore()
const emailSettings = useEmailSettingsStore()
const { addErrorMessage } = useSnackbarMessagesStore()
const route = useRoute()
const router = useRouter()
const RECIPIENT_TYPES = ['To', 'Cc', 'Bcc'] as const
const MAX_FRAME_HEIGHT = 50000

// Injected only when scripts are allowed. The frame then has its own origin,
// so the parent can't read its height directly; the frame reports it instead.
// ('<' + '/script>' is split so the .vue parser doesn't end this block here.)
const RESIZE_SCRIPT =
  `<script>(function () {
  function send() {
    parent.postMessage({ type: 'email-frame-height', height: document.documentElement.scrollHeight }, '*')
  }
  window.addEventListener('load', send)
  if (window.ResizeObserver) new ResizeObserver(send).observe(document.documentElement)
  send()
})()<` + `/script>`

// STATE
const email = ref<EmailDetailsResponse | null>(null)
const isLoading = ref(true)
const downloadingId = ref<string | null>(null)
const showScriptDialog = ref(false)
const frame = ref<HTMLIFrameElement | null>(null)
const frameHeight = ref(0)
let frameObserver: ResizeObserver | undefined

const backTo = computed(() => ({
  name: 'mailbox',
  params: { mailbox: props.mailbox },
  query: route.query, // back to the same page of the list
}))

const senderLabel = computed(() => email.value?.senderName || email.value?.senderEmail || '')
const senderInitial = computed(() => (senderLabel.value.charAt(0) || '?').toUpperCase())
const date = computed(() => email.value?.sentAt ?? email.value?.receivedAt ?? null)

// Recipients grouped as To / Cc / Bcc, skipping empty groups
const recipientGroups = computed(() =>
  RECIPIENT_TYPES.map((type) => ({
    type,
    recipients: (email.value?.recipients ?? []).filter(
      (r) => r.recipientType.toLowerCase() === type.toLowerCase(),
    ),
  })).filter((group) => group.recipients.length),
)

// Rough check, only used to decide whether to offer the "allow scripts" option
const hasScripts = computed(() =>
  /<script\b|\son[a-z]+\s*=|javascript:/i.test(email.value?.bodyHtml ?? ''),
)

// Script mode is only used for emails that actually contain scripts
const scriptsEnabled = computed(() => emailSettings.allowScripts && hasScripts.value)

// NEVER combine allow-scripts with allow-same-origin: together they let the
// email's scripts remove the sandbox and read the app's tokens.
const sandbox = computed(() =>
  scriptsEnabled.value
    ? 'allow-scripts allow-popups allow-popups-to-escape-sandbox'
    : 'allow-same-origin allow-popups allow-popups-to-escape-sandbox',
)

// Email HTML is shown in a sandboxed iframe: by default scripts can't run, and
// its styles can't leak into the app. Links open in a new tab.
const htmlDocument = computed(() => {
  const html = email.value?.bodyHtml
  if (!html) return ''
  return `<!doctype html><html><head><meta charset="utf-8"><base target="_blank">
<style>
  body { margin: 0; font-family: system-ui, sans-serif; font-size: 14px; line-height: 1.5; overflow-wrap: anywhere; }
  img { max-width: 100%; height: auto; }
</style>${scriptsEnabled.value ? RESIZE_SCRIPT : ''}</head><body>${html}</body></html>`
})

// METHODS
const goBack = () => router.push(backTo.value)

function recipientLabel(r: EmailRecipientResponse) {
  return r.recipientName || r.recipientEmail
}

function formatSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

function attachmentIcon(a: EmailAttachmentResponse) {
  const type = a.contentType ?? ''
  if (type.startsWith('image/')) return 'i-mdi-file-image-outline'
  if (type === 'application/pdf') return 'i-mdi-file-pdf-box'
  return 'i-mdi-file-outline'
}

// The download endpoint needs the auth header, so a plain <a href> won't work.
// Fetch it as a blob through the authenticated axios instance instead.
async function download(a: EmailAttachmentResponse) {
  downloadingId.value = a.id
  try {
    const res = await api.get<Blob>(API_URLS.attachments.download(a.id), {
      responseType: 'blob',
    })
    const url = URL.createObjectURL(res.data)
    const link = document.createElement('a')
    link.href = url
    link.download = a.fileName
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch {
    addErrorMessage(`Αποτυχία κατεβάσματος ${a.fileName}`)
  } finally {
    downloadingId.value = null
  }
}

function enableScripts() {
  emailSettings.allowScripts = true
  showScriptDialog.value = false
}

function disableScripts() {
  emailSettings.allowScripts = false
}

// Size the iframe to its content, and keep it sized as images load.
// With scripts enabled the frame reports its height via onFrameMessage instead.
function onFrameLoad() {
  frameObserver?.disconnect()
  if (scriptsEnabled.value) return
  const doc = frame.value?.contentDocument
  if (!doc) return
  const update = () => (frameHeight.value = doc.documentElement.scrollHeight)
  update()
  frameObserver = new ResizeObserver(update)
  frameObserver.observe(doc.documentElement)
}

// Only accept height messages from our own email frame
function onFrameMessage(event: MessageEvent) {
  if (!frame.value || event.source !== frame.value.contentWindow) return
  const data = event.data
  if (data?.type !== 'email-frame-height' || typeof data.height !== 'number') return
  frameHeight.value = Math.min(Math.max(data.height, 0), MAX_FRAME_HEIGHT)
}

// WATCHERS
watch(
  () => props.id,
  async (id) => {
    isLoading.value = true
    email.value = null
    frameHeight.value = 0
    const res = await emailStore.fetchEmail(id)
    if (id !== props.id) return // user already opened another email
    email.value = res
    isLoading.value = false
  },
  { immediate: true },
)

onMounted(() => window.addEventListener('message', onFrameMessage))

onBeforeUnmount(() => {
  window.removeEventListener('message', onFrameMessage)
  frameObserver?.disconnect()
})
</script>

<template>
  <div class="toolbar">
    <v-btn icon="i-mdi-arrow-left" variant="text" size="small" title="Back" @click="goBack" />
    <v-chip
      v-if="email?.folderPath"
      size="small"
      variant="tonal"
      prepend-icon="i-mdi-folder-outline"
      class="ml-2"
    >
      {{ email.folderPath }}
    </v-chip>
  </div>
  <v-divider />

  <v-progress-linear v-if="isLoading" indeterminate color="primary" />

  <div class="scroll scrollbar">
    <div v-if="email" class="mail">
      <h1 class="subject">{{ email.subject || '(χωρίς θέμα)' }}</h1>

      <div class="sender-row">
        <v-avatar color="primary" size="40">{{ senderInitial }}</v-avatar>
        <div class="sender-info">
          <div>
            <strong>{{ senderLabel }}</strong>
            <span v-if="email.senderName && email.senderEmail" class="text-medium-emphasis small">
              &lt;{{ email.senderEmail }}&gt;
            </span>
          </div>
          <div
            v-for="group in recipientGroups"
            :key="group.type"
            class="text-medium-emphasis small recipients"
          >
            {{ group.type.toLowerCase() }}:
            <span v-for="(r, i) in group.recipients" :key="r.id" :title="r.recipientEmail">
              {{ recipientLabel(r) }}<template v-if="i < group.recipients.length - 1">, </template>
            </span>
          </div>
        </div>
        <v-spacer />
        <span v-if="date" class="text-medium-emphasis small date">
          {{ formatLongDate(date) }}
        </span>
      </div>

      <v-alert
        v-if="hasScripts && !emailSettings.allowScripts"
        type="info"
        variant="tonal"
        density="compact"
        class="mt-6"
        text="This email contains scripts, which are blocked."
      >
        <template #append>
          <v-btn variant="text" size="small" @click="showScriptDialog = true">
            Να επιτραπούν για την τωρινή συνεδρία
          </v-btn>
        </template>
      </v-alert>
      <v-alert
        v-else-if="scriptsEnabled"
        type="warning"
        variant="tonal"
        density="compact"
        class="mt-6"
        text="Scripts are enabled for this session."
      >
        <template #append>
          <v-btn variant="text" size="small" @click="disableScripts">Απενεργοποίηση</v-btn>
        </template>
      </v-alert>

      <!-- :key recreates the frame when the sandbox changes; browsers ignore
           sandbox changes on an iframe that is already loaded -->
      <iframe
        v-if="htmlDocument"
        ref="frame"
        :key="sandbox"
        class="html-body"
        :srcdoc="htmlDocument"
        :style="{ height: `${frameHeight}px` }"
        :sandbox="sandbox"
        title="Email content"
        @load="onFrameLoad"
      />
      <div v-else-if="email.bodyText" class="body">{{ email.bodyText }}</div>
      <div v-else class="body text-medium-emphasis">(δεν υπάρχει περιεχόμενο)</div>

      <div v-if="email.attachments.length" class="attachments">
        <div class="text-medium-emphasis small mb-2">
          {{ email.attachments.length }}
          {{ email.attachments.length === 1 ? 'συνημμένο' : 'συνημμένα' }}
        </div>
        <div class="d-flex flex-wrap ga-2">
          <v-chip
            v-for="a in email.attachments"
            :key="a.id"
            :prepend-icon="attachmentIcon(a)"
            :disabled="downloadingId === a.id"
            variant="outlined"
            :title="`Download ${a.fileName}`"
            @click="download(a)"
          >
            {{ a.fileName }}
            <span class="text-medium-emphasis ml-1">({{ formatSize(a.sizeBytes) }})</span>
            <template #append>
              <v-progress-circular
                v-if="downloadingId === a.id"
                indeterminate
                size="14"
                width="2"
                class="ml-2"
              />
              <v-icon v-else icon="i-mdi-download" size="small" class="ml-2" />
            </template>
          </v-chip>
        </div>
      </div>
    </div>

    <NotFound404
      v-else-if="!isLoading"
      title="Email not found"
      message="This email doesn't exist or you don't have access to it."
      icon="i-mdi-email-remove-outline"
      :back-to="backTo"
    />
  </div>

  <v-dialog v-model="showScriptDialog" max-width="460">
    <v-card title="Allow scripts in emails?" rounded="xl">
      <v-card-text>
        Scripts in emails can track when you open them and load content from unknown servers. They
        run isolated from the app and can't access your account or other emails. This applies to
        every email until you turn it off, close the tab or log out.
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="showScriptDialog = false">Ακύρωση</v-btn>
        <v-btn color="warning" variant="flat" @click="enableScripts">Να επιτραπούν scripts</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 12px;
  min-height: 52px;
}

.scroll {
  flex: 1;
  overflow-y: auto;
}

.mail {
  padding: 16px 24px 32px 72px;
}

.subject {
  font-size: 1.4rem;
  font-weight: 400;
  margin: 0 0 20px;
}

.sender-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-left: -52px;
}

.sender-info {
  min-width: 0;
}

.recipients {
  overflow-wrap: anywhere;
}

.small {
  font-size: 0.8rem;
}

.date {
  white-space: nowrap;
}

.body {
  white-space: pre-line;
  margin-top: 24px;
  line-height: 1.6;
}

.html-body {
  display: block;
  width: 100%;
  border: none;
  margin-top: 24px;
  background: #fff; /* email HTML assumes a white background, also in dark mode */
  border-radius: 8px;
}

.attachments {
  margin-top: 32px;
}

@media (max-width: 600px) {
  .mail {
    padding: 16px;
  }
  .sender-row {
    margin-left: 0;
    flex-wrap: wrap;
  }
}
</style>
